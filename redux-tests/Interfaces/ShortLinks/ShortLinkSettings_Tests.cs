using System.Text.Json;
using API.Interfaces.ShortLinks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The short-link settings, ids and content hashes (#844).
public class ShortLinkSettings_Tests {
    private static ShortLinkSettings Settings(Dictionary<string, string?> values) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(values).Build());

    [Fact]
    public void Defaults_WhenNothingIsSet() {
        var settings = Settings([]);
        Assert.Null(settings.ConnectionString);
        Assert.Equal(TimeSpan.FromDays(183), settings.Lifetime);
        Assert.Equal(1024 * 1024, settings.MaxViewBytes);
        Assert.Equal(30, settings.CreatesPerHour);
        Assert.Equal(TimeSpan.FromMinutes(60), settings.CleanupInterval);
    }

    // appsettings.json carries the same defaults as the code, so the two can't drift apart unnoticed.
    [Fact]
    public void AppSettingsJson_MatchesTheDefaults() {
        var configuration = new ConfigurationBuilder().AddJsonFile(Path.Combine(ProjectSourcePath.Value, "appsettings.json")).Build();
        var settings = new ShortLinkSettings(configuration);
        var defaults = Settings([]);
        Assert.Null(settings.ConnectionString);
        Assert.Equal(defaults.Lifetime, settings.Lifetime);
        Assert.Equal(defaults.MaxViewBytes, settings.MaxViewBytes);
        Assert.Equal(defaults.CreatesPerHour, settings.CreatesPerHour);
        Assert.Equal(defaults.CleanupInterval, settings.CleanupInterval);
    }

    [Fact]
    public void ReadsEverySetting() {
        var settings = Settings(new() {
            ["ConnectionStrings:ShortLinks"] = "Host=db",
            [ShortLinkSettings.LifetimeDaysKey] = "7",
            [ShortLinkSettings.MaxViewBytesKey] = "5000",
            [ShortLinkSettings.CreatesPerHourKey] = "4",
            [ShortLinkSettings.CleanupMinutesKey] = "1.5",
        });
        Assert.Equal("Host=db", settings.ConnectionString);
        Assert.Equal(TimeSpan.FromDays(7), settings.Lifetime);
        Assert.Equal(5000, settings.MaxViewBytes);
        Assert.Equal(4, settings.CreatesPerHour);
        Assert.Equal(TimeSpan.FromSeconds(90), settings.CleanupInterval);
    }

    // Zero or less turns the rate limit off; elsewhere it falls back to the default, and the size cap has a ceiling.
    [Fact]
    public void OutOfRangeValues_TurnTheLimitOff_OrFallBack() {
        var settings = Settings(new() {
            ["ConnectionStrings:ShortLinks"] = "  ",
            [ShortLinkSettings.LifetimeDaysKey] = "0",
            [ShortLinkSettings.MaxViewBytesKey] = int.MaxValue.ToString(),
            [ShortLinkSettings.CreatesPerHourKey] = "-1",
            [ShortLinkSettings.CleanupMinutesKey] = "-5",
        });
        Assert.Null(settings.ConnectionString);
        Assert.Equal(TimeSpan.FromDays(ShortLinkSettings.DefaultLifetimeDays), settings.Lifetime);
        Assert.Equal(ShortLinkSettings.MaxViewBytesCeiling, settings.MaxViewBytes);
        Assert.Null(settings.CreatesPerHour);
        Assert.Equal(TimeSpan.FromMinutes(ShortLinkSettings.DefaultCleanupMinutes), settings.CleanupInterval);

        Assert.Equal(ShortLinkSettings.DefaultMaxViewBytes, Settings(new() { [ShortLinkSettings.MaxViewBytesKey] = "0" }).MaxViewBytes);
    }

    [Fact]
    public void NewId_IsTenBase62Characters_AndDiffersEachTime() {
        var ids = Enumerable.Range(0, 1000).Select(_ => ShortLinkContent.NewId()).ToList();
        Assert.All(ids, id => Assert.True(ShortLinkContent.IsWellFormedId(id), id));
        Assert.Equal(ids.Count, ids.Distinct().Count());
        // Every character of the alphabet turns up, so none is left out.
        Assert.Equal(ShortLinkContent.IdAlphabet.Length, ids.SelectMany(id => id).Distinct().Count());
    }

    [Theory]
    [InlineData("0123456789", true)]
    [InlineData("abcdeFGHIJ", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("abcdefghi", false)]
    [InlineData("abcdefghijk", false)]
    [InlineData("abcdefghi_", false)]
    [InlineData("abcdéfghij", false)]
    public void IsWellFormedId(string? id, bool expected) => Assert.Equal(expected, ShortLinkContent.IsWellFormedId(id));

    [Fact]
    public void Hash_IgnoresSpacing_ButNotContent() {
        static byte[] Hash(string json) => ShortLinkContent.Hash(JsonDocument.Parse(json).RootElement);
        Assert.Equal(Hash("""{"problem":"SAT3","n":[1,2]}"""), Hash("""{ "problem" : "SAT3", "n" : [ 1, 2 ] }"""));
        Assert.NotEqual(Hash("""{"problem":"SAT3","n":[1,2]}"""), Hash("""{"problem":"SAT3","n":[2,1]}"""));
        Assert.Equal(32, Hash("{}").Length);
    }
}

// The hourly delete of expired links (#844): it deletes them, logs and survives a failing store, and does nothing
// without a database.
public class ShortLinkCleanupService_Tests {
    private static readonly DateTimeOffset Start = new(2026, 11, 24, 0, 0, 0, TimeSpan.Zero);

    private static ShortLinkSettings Settings(string minutes) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [ShortLinkSettings.CleanupMinutesKey] = minutes }).Build());

    private static ShortLinkCleanupService Service(IShortLinkStore store, TimeProvider time, string minutes = "60") =>
        new(store, Settings(minutes), time, NullLogger<ShortLinkCleanupService>.Instance);

    [Fact]
    public async Task CleanUpOnce_DeletesOnlyExpiredLinks() {
        var store = new InMemoryShortLinkStore();
        var time = new ManualTimeProvider(Start);
        var token = TestContext.Current.CancellationToken;
        var old = await store.CreateAsync(JsonDocument.Parse("""{"problem":"A"}""").RootElement, Start, TimeSpan.FromDays(1), token);
        var fresh = await store.CreateAsync(JsonDocument.Parse("""{"problem":"B"}""").RootElement, Start, TimeSpan.FromDays(3), token);
        time.Advance(TimeSpan.FromDays(2));

        Assert.Equal(1, await Service(store, time).CleanUpOnceAsync(token));
        Assert.Null(await store.GetAsync(old.Id, Start, token));
        Assert.NotNull(await store.GetAsync(fresh.Id, Start, token));
    }

    [Fact]
    public async Task CleanUpOnce_StoreThatFails_ReturnsNullInsteadOfThrowing() {
        var service = Service(new FailingShortLinkStore(), new ManualTimeProvider(Start));
        Assert.Null(await service.CleanUpOnceAsync(TestContext.Current.CancellationToken));
    }

    // With no database the service ends at once and never touches the store.
    [Fact]
    public async Task Execute_WithoutADatabase_EndsAtOnce() {
        using var service = Service(new UnavailableShortLinkStore(), TimeProvider.System, minutes: "0.001");
        await service.StartAsync(TestContext.Current.CancellationToken);
        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.True(service.ExecuteTask.IsCompletedSuccessfully);
    }

    // The service deletes at start and then on every tick, keeps going after a failure, and stops cleanly.
    [Fact]
    public async Task Execute_DeletesOnEveryTick_UntilStopped() {
        var store = new InMemoryShortLinkStore();
        using var service = Service(store, TimeProvider.System, minutes: "0.0005"); // 30 ms
        await service.StartAsync(TestContext.Current.CancellationToken);
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (store.DeleteCalls < 3 && DateTime.UtcNow < deadline)
            await Task.Delay(10, TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);
        Assert.True(store.DeleteCalls >= 3);
        Assert.True(service.ExecuteTask!.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Execute_StoreThatFails_KeepsRunning() {
        using var service = Service(new FailingShortLinkStore(), TimeProvider.System, minutes: "0.0005");
        await service.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(150, TestContext.Current.CancellationToken);
        Assert.False(service.ExecuteTask!.IsCompleted);
        await service.StopAsync(TestContext.Current.CancellationToken);
        Assert.True(service.ExecuteTask.IsCompletedSuccessfully);
    }
}
