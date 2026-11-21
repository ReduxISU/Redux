using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The cap on compute jobs running at once (#936), on its own: slots are taken, given back once, and kept by work the
// backstop stopped waiting for.
public class ComputeJobLimiter_Tests {
    private static ComputeJobLimiter Limiter(string? max) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [ComputeJobLimiter.MaxJobsKey] = max }).Build());

    [Fact]
    public void TryStart_TurnsAwayTheJobPastTheCap_UntilASlotIsGivenBack() {
        var limiter = Limiter("2");
        var first = limiter.TryStart();
        var second = limiter.TryStart();
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Null(limiter.TryStart());
        Assert.Equal(2, limiter.Running);

        first!.Dispose();
        Assert.Equal(1, limiter.Running);
        Assert.NotNull(limiter.TryStart());
    }

    [Fact]
    public void Dispose_GivesTheSlotBackOnlyOnce() {
        var limiter = Limiter("2");
        var job = limiter.TryStart()!;
        _ = limiter.TryStart();
        job.Dispose();
        job.Dispose();
        Assert.Equal(1, limiter.Running);
    }

    [Fact]
    public async Task FinishWhen_KeepsTheSlotUntilTheWorkFinishes_EvenAfterTheRequestEnds() {
        var limiter = Limiter("1");
        var work = new TaskCompletionSource();
        var job = limiter.TryStart()!;
        job.FinishWhen(work.Task);
        job.Dispose(); // the request answered; its work runs on
        Assert.Equal(1, limiter.Running);
        Assert.Null(limiter.TryStart());

        work.SetResult();
        await work.Task;
        Assert.Equal(0, limiter.Running);
    }

    [Fact]
    public async Task WithBackstop_HandsTheSlotToWorkItGivesUpOn() {
        var limiter = Limiter("1");
        using var release = new ManualResetEventSlim();
        var job = limiter.TryStart()!;
        var (finished, _) = await ComputeLimits.WithBackstop(() => { release.Wait(); return 0; }, TimeSpan.FromMilliseconds(50), job);
        job.Dispose();
        Assert.False(finished);
        Assert.Equal(1, limiter.Running);

        release.Set();
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (limiter.Running != 0 && DateTime.UtcNow < deadline)
            await Task.Delay(10, TestContext.Current.CancellationToken);
        Assert.Equal(0, limiter.Running);
    }

    [Fact]
    public async Task WithBackstop_WorkThatFinishesInTime_LeavesTheSlotToTheRequest() {
        var limiter = Limiter("1");
        var job = limiter.TryStart()!;
        var (finished, result) = await ComputeLimits.WithBackstop(() => 7, TimeSpan.FromSeconds(5), job);
        Assert.True(finished);
        Assert.Equal(7, result);
        Assert.Equal(1, limiter.Running);
        job.Dispose();
        Assert.Equal(0, limiter.Running);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public void ZeroOrLess_TurnsTheCapOff(string max) {
        var limiter = Limiter(max);
        Assert.Null(limiter.MaxJobs);
        for (int i = 0; i < 100; i++)
            Assert.NotNull(limiter.TryStart());
        Assert.Equal(0, limiter.Running);
    }

    [Fact]
    public void Unset_UsesTheProcessorCount_AtLeastTwo() {
        Assert.Equal(Math.Max(2, Environment.ProcessorCount), Limiter(null).MaxJobs);
    }
}

// The app with room for one compute job at a time and a 2-second time limit (#936).
public class OneComputeJobAppFactory : AppFactory {
    public const double TimeLimitSeconds = 2;

    protected override void ConfigureWebHost(IWebHostBuilder builder) {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> {
                [ComputeJobLimiter.MaxJobsKey] = "1",
                [ComputeLimits.TimeLimitKey] = TimeLimitSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
            }));
    }
}

[Collection(TimingCollection.Name)]
public class ComputeJobs_Endpoint_Tests : IClassFixture<OneComputeJobAppFactory> {
    // A partition search that cannot finish: it holds the one slot until the time limit stops it.
    private const string BlockingSolver = "PartitionBruteForce";
    private const string BlockingInstance = "{1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25,26,27,28,29,30}";

    private readonly OneComputeJobAppFactory _factory;
    private readonly HttpClient _client;

    public ComputeJobs_Endpoint_Tests(OneComputeJobAppFactory factory) {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private ComputeJobLimiter Limiter => _factory.Services.GetRequiredService<ComputeJobLimiter>();

    private Task<HttpResponseMessage> PostSolve(string solver, string instance) =>
        _client.PostAsync($"/ProblemProvider/solve?solver={solver}",
            new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);

    // Starts the blocking solve and waits until it holds the slot.
    private async Task<Task<HttpResponseMessage>> StartBlockingSolve() {
        var blocking = PostSolve(BlockingSolver, BlockingInstance);
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (Limiter.Running == 0 && DateTime.UtcNow < deadline)
            await Task.Delay(10, TestContext.Current.CancellationToken);
        Assert.Equal(1, Limiter.Running);
        return blocking;
    }

    private async Task WaitForFreeSlot() {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (Limiter.Running != 0 && DateTime.UtcNow < deadline)
            await Task.Delay(10, TestContext.Current.CancellationToken);
        Assert.Equal(0, Limiter.Running);
    }

    [Fact]
    public async Task ASecondJobWhileTheOnlySlotIsTaken_Gets429ServerBusy_WithRetryAfter() {
        await WaitForFreeSlot();
        var blocking = await StartBlockingSolve();

        var busy = await PostSolve("PartitionBruteForce", "{1,2,3}");
        Assert.Equal(HttpStatusCode.TooManyRequests, busy.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(ComputeJobLimiter.RetryAfterSeconds), busy.Headers.RetryAfter?.Delta);
        var body = JsonDocument.Parse(await busy.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement;
        Assert.Equal("server_busy", body.GetProperty("error").GetString());
        Assert.Equal("The server is busy with other jobs right now, so this one wasn't started.", body.GetProperty("message").GetString());
        Assert.Equal(1, body.GetProperty("maxConcurrentJobs").GetInt32());
        Assert.Equal(ComputeJobLimiter.RetryAfterSeconds, body.GetProperty("retryAfterSeconds").GetInt32());
        Assert.StartsWith("Wait about", body.GetProperty("hint").GetString());

        // The other compute endpoints are turned away too.
        var verify = await _client.PostAsync("/ProblemProvider/verify?verifier=PartitionVerifier",
            new StringContent(JsonSerializer.Serialize(new { problemInstance = "{1,1}", certificate = "{1}" }), Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.TooManyRequests, verify.StatusCode);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await blocking).StatusCode); // stopped at the time limit
    }

    [Fact]
    public async Task CheapEndpoints_StillAnswer_WhileTheOnlySlotIsTaken() {
        await WaitForFreeSlot();
        var blocking = await StartBlockingSolve();

        var info = await _client.GetAsync("/ProblemProvider/info?interface=SAT3", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, info.StatusCode);
        var health = await _client.GetAsync("/health", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);

        await blocking;
    }

    [Fact]
    public async Task TheSlotIsFreeAgain_OnceTheJobEnds() {
        await WaitForFreeSlot();
        var blocking = await StartBlockingSolve();
        await blocking;
        await WaitForFreeSlot();

        var next = await PostSolve("PartitionBruteForce", "{1,1,2}");
        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
        Assert.Equal(0, Limiter.Running);
    }

    [Fact]
    public async Task RejectedAndFailedRequests_GiveTheirSlotBack() {
        await WaitForFreeSlot();
        Assert.Equal(HttpStatusCode.BadRequest, (await PostSolve("NoSuchSolver", "{1}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostSolve("PartitionBruteForce", "not a partition instance")).StatusCode);
        Assert.Equal(0, Limiter.Running);
    }
}
