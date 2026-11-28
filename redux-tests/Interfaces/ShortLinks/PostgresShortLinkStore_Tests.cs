using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces.ShortLinks;
using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// One PostgreSQL container for a test class (#844); each test gets an empty database of its own in it. Without Docker the
// tests are skipped on a developer's machine, but fail when the CI environment variable is set, so CI never skips them
// unnoticed.
public sealed class PostgresFixture : IAsyncLifetime {
    public const string Image = "postgres:17";

    private PostgreSqlContainer? _container;
    private Exception? _dockerMissing;

    public async ValueTask InitializeAsync() {
        try {
            _container = new PostgreSqlBuilder(Image).Build();
            await _container.StartAsync();
        } catch (DockerUnavailableException e) {
            _dockerMissing = e;
        }
    }

    public async ValueTask DisposeAsync() {
        if (_container is not null)
            await _container.DisposeAsync();
    }

    // A connection string to a new, empty database.
    public async Task<string> NewDatabaseAsync() {
        if (_dockerMissing is not null) {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")))
                Assert.Fail($"Docker is required on CI for the PostgreSQL tests: {_dockerMissing.Message}");
            Assert.Skip($"Docker is not running: {_dockerMissing.Message}");
        }
        string name = "t_" + Guid.NewGuid().ToString("N");
        await using (var admin = new NpgsqlConnection(_container!.GetConnectionString())) {
            await admin.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE {name}", admin);
            await command.ExecuteNonQueryAsync();
        }
        return new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = name }.ConnectionString;
    }
}

// PostgresShortLinkStore against a real PostgreSQL (#844): the table made at first use, the same view getting the same
// link, expiry, the cleanup, and a taken id.
public class PostgresShortLinkStore_Tests : IClassFixture<PostgresFixture> {
    private static readonly DateTimeOffset Start = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(183);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly PostgresFixture _postgres;

    public PostgresShortLinkStore_Tests(PostgresFixture postgres) {
        _postgres = postgres;
    }

    private static JsonElement View(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static async Task<long> Scalar(NpgsqlDataSource dataSource, string sql) {
        await using var command = dataSource.CreateCommand(sql);
        return Convert.ToInt64(await command.ExecuteScalarAsync(Token));
    }

    [Fact]
    public async Task FirstUse_CreatesTheTableAndIndex_AndASecondStoreUsesThemAsTheyAre() {
        await using var dataSource = NpgsqlDataSource.Create(await _postgres.NewDatabaseAsync());
        const string tableExists = "SELECT count(*) FROM information_schema.tables WHERE table_name = 'share_links'";
        Assert.Equal(0, await Scalar(dataSource, tableExists));

        var first = new PostgresShortLinkStore(dataSource);
        Assert.Null(await first.GetAsync("AAAAAAAAAA", Start, Token));
        Assert.Equal(1, await Scalar(dataSource, tableExists));
        Assert.Equal(1, await Scalar(dataSource, "SELECT count(*) FROM pg_indexes WHERE indexname = 'share_links_expires_at'"));

        var created = await first.CreateAsync(View("""{"problem":"SAT3"}"""), Start, Lifetime, Token);
        var second = new PostgresShortLinkStore(dataSource);
        Assert.NotNull(await second.GetAsync(created.Id, Start, Token));
        Assert.Equal(created.Id, (await second.CreateAsync(View("""{"problem":"SAT3"}"""), Start, Lifetime, Token)).Id);
    }

    [Fact]
    public async Task CreateThenGet_ReturnsTheViewAndTimes() {
        await using var dataSource = NpgsqlDataSource.Create(await _postgres.NewDatabaseAsync());
        var store = new PostgresShortLinkStore(dataSource);
        var view = View("""{"problem":"CLIQUE","instance":"{{1,2},{{1,2}},2}","step":3,"nested":{"z":[1,2.5,null,true],"a":"ü"}}""");

        var created = await store.CreateAsync(view, Start, Lifetime, Token);
        Assert.True(ShortLinkContent.IsWellFormedId(created.Id));
        Assert.Equal(Start, created.CreatedAt);
        Assert.Equal(Start + Lifetime, created.ExpiresAt);

        var link = await store.GetAsync(created.Id, Start + TimeSpan.FromDays(1), Token);
        Assert.NotNull(link);
        Assert.Equal(created.Id, link.Id);
        Assert.True(JsonElement.DeepEquals(view, link.View));
        Assert.Equal(created.CreatedAt, link.CreatedAt);
        Assert.Equal(created.ExpiresAt, link.ExpiresAt);
    }

    // Saving a view again keeps its id and creation time and moves its expiry out; an earlier save never shortens it.
    [Fact]
    public async Task CreateAgain_KeepsTheId_AndExtendsTheExpiry() {
        await using var dataSource = NpgsqlDataSource.Create(await _postgres.NewDatabaseAsync());
        var store = new PostgresShortLinkStore(dataSource);
        var view = View("""{"problem":"SAT3","instance":"(x1 | !x2)"}""");
        var first = await store.CreateAsync(view, Start, Lifetime, Token);
        var later = await store.CreateAsync(View("""{ "problem": "SAT3", "instance": "(x1 | !x2)" }"""), Start + TimeSpan.FromDays(30), Lifetime, Token);

        Assert.Equal(first.Id, later.Id);
        Assert.Equal(Start, later.CreatedAt);
        Assert.Equal(Start + TimeSpan.FromDays(30) + Lifetime, later.ExpiresAt);

        var withShorterLife = await store.CreateAsync(view, Start + TimeSpan.FromDays(31), TimeSpan.FromDays(1), Token);
        Assert.Equal(later.ExpiresAt, withShorterLife.ExpiresAt);
        Assert.Equal(1, await Scalar(dataSource, "SELECT count(*) FROM share_links"));
    }

    [Fact]
    public async Task Get_ExpiredLink_ReturnsNull() {
        await using var dataSource = NpgsqlDataSource.Create(await _postgres.NewDatabaseAsync());
        var store = new PostgresShortLinkStore(dataSource);
        var created = await store.CreateAsync(View("""{"problem":"SAT3"}"""), Start, Lifetime, Token);

        Assert.NotNull(await store.GetAsync(created.Id, created.ExpiresAt - TimeSpan.FromMilliseconds(1), Token));
        Assert.Null(await store.GetAsync(created.Id, created.ExpiresAt, Token));
        Assert.Null(await store.GetAsync(created.Id, created.ExpiresAt + TimeSpan.FromDays(1), Token));
    }

    // An expired view saved again (before the cleanup deleted it) comes back as if just made.
    [Fact]
    public async Task CreateAgain_AfterExpiry_RevivesTheLinkForTheFullLifetime() {
        await using var dataSource = NpgsqlDataSource.Create(await _postgres.NewDatabaseAsync());
        var store = new PostgresShortLinkStore(dataSource);
        var view = View("""{"problem":"SAT3"}""");
        var first = await store.CreateAsync(view, Start, Lifetime, Token);
        var now = first.ExpiresAt + TimeSpan.FromDays(5);

        var revived = await store.CreateAsync(view, now, Lifetime, Token);
        Assert.Equal(first.Id, revived.Id);
        Assert.Equal(now, revived.CreatedAt);
        Assert.Equal(now + Lifetime, revived.ExpiresAt);
        Assert.NotNull(await store.GetAsync(first.Id, now, Token));
    }

    [Fact]
    public async Task DeleteExpired_DeletesOnlyExpiredLinks() {
        await using var dataSource = NpgsqlDataSource.Create(await _postgres.NewDatabaseAsync());
        var store = new PostgresShortLinkStore(dataSource);
        var old = await store.CreateAsync(View("""{"problem":"A"}"""), Start, TimeSpan.FromDays(1), Token);
        var fresh = await store.CreateAsync(View("""{"problem":"B"}"""), Start, TimeSpan.FromDays(3), Token);

        Assert.Equal(0, await store.DeleteExpiredAsync(Start, Token));
        Assert.Equal(1, await store.DeleteExpiredAsync(Start + TimeSpan.FromDays(2), Token));
        Assert.Equal(0, await store.DeleteExpiredAsync(Start + TimeSpan.FromDays(2), Token));
        Assert.Null(await store.GetAsync(old.Id, Start, Token));
        Assert.NotNull(await store.GetAsync(fresh.Id, Start, Token));
    }

    // A new id that another view already has is replaced with another; one that keeps being taken gives up.
    [Fact]
    public async Task Create_IdTakenByAnotherView_TriesAnotherId() {
        await using var dataSource = NpgsqlDataSource.Create(await _postgres.NewDatabaseAsync());
        await new PostgresShortLinkStore(dataSource, () => "AAAAAAAAAA").CreateAsync(View("""{"problem":"A"}"""), Start, Lifetime, Token);

        var ids = new Queue<string>(["AAAAAAAAAA", "AAAAAAAAAA", "BBBBBBBBBB"]);
        var retried = await new PostgresShortLinkStore(dataSource, ids.Dequeue).CreateAsync(View("""{"problem":"B"}"""), Start, Lifetime, Token);
        Assert.Equal("BBBBBBBBBB", retried.Id);

        var stuck = new PostgresShortLinkStore(dataSource, () => "AAAAAAAAAA");
        var e = await Assert.ThrowsAsync<ShortLinksUnavailableException>(() => stuck.CreateAsync(View("""{"problem":"C"}"""), Start, Lifetime, Token));
        Assert.IsType<PostgresException>(e.InnerException);
    }

    // Many saves of one view at once all get the one link.
    [Fact]
    public async Task Create_SameViewAtOnce_GivesEveryoneTheSameId() {
        await using var dataSource = NpgsqlDataSource.Create(await _postgres.NewDatabaseAsync());
        var store = new PostgresShortLinkStore(dataSource);
        var view = View("""{"problem":"SAT3","instance":"same"}""");
        var created = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => store.CreateAsync(view, Start, Lifetime, Token)));
        Assert.Single(created.Select(c => c.Id).Distinct());
    }

    // The whole path: the app set up with a real database saves a view and opens it again.
    [Fact]
    public async Task Endpoints_WithARealDatabase_SaveAndOpenAView() {
        string connectionString = await _postgres.NewDatabaseAsync();
        using var factory = new AppFactory().WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> {
                [$"ConnectionStrings:{ShortLinkSettings.ConnectionStringName}"] = connectionString,
            })));
        var client = factory.CreateClient();
        const string view = """{"problem":"SAT3","instance":"(x1 | x2)","solver":"SAT3BruteForceSolver"}""";

        var created = await client.PostAsync("/ShareLinks", new StringContent($$"""{"view":{{view}}}""", Encoding.UTF8, "application/json"), Token);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        string id = (await created.ReadJson()).GetProperty("id").GetString()!;

        var opened = await client.GetAsync($"/ShareLinks/{id}", Token);
        Assert.Equal(HttpStatusCode.OK, opened.StatusCode);
        Assert.True(JsonElement.DeepEquals(View(view), (await opened.ReadJson()).GetProperty("view")));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/ShareLinks/AAAAAAAAAA", Token)).StatusCode);
    }
}
