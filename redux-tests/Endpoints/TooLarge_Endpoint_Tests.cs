using System.Diagnostics;
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

// The app with the Big-O precheck turned off (#843) and a half-second limit, so a hopeless solve runs into the timer.
public class PrecheckOffAppFactory : AppFactory {
    protected override void ConfigureWebHost(IWebHostBuilder builder) {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> {
            [ComputePrecheck.MarginKey] = "0",
            [ComputeLimits.TimeLimitKey] = "0.5",
        }));
    }
}

internal static class TooLargeChecks {
    public static async Task<(HttpStatusCode Status, JsonElement Body, TimeSpan Took)> Post(HttpClient client, string path, string instance) {
        var sw = Stopwatch.StartNew();
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await client.PostAsync(path, content, TestContext.Current.CancellationToken);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        sw.Stop();
        return (response.StatusCode, JsonDocument.Parse(text).RootElement.Clone(), sw.Elapsed);
    }

    // The #933 error contract, as time_limit has it: a code, a plain message, the technical detail, the numbers, the
    // solver's context, and a hint.
    public static void AssertTooLargeBody(JsonElement body, string job, double limitSeconds) {
        Assert.Equal("too_large", body.GetProperty("error").GetString());
        string message = body.GetProperty("message").GetString()!;
        Assert.StartsWith("The Traveling Salesperson Brute Force solver would need about 2.1 × 10^15 steps for this instance", message);
        Assert.EndsWith($"so the {job} was not started.", message);
        Assert.StartsWith("The solver's declared running time, O(|N|! * |N| * (|E| + 1)), comes to about 2.1 × 10^15 steps at |N| = 15, |E| = 105.",
            body.GetProperty("detail").GetString());
        Assert.Equal(limitSeconds, body.GetProperty("limitSeconds").GetDouble());
        Assert.True(body.GetProperty("estimatedSteps").GetDouble() > body.GetProperty("refuseAboveSteps").GetDouble());
        Assert.Equal(body.GetProperty("stepsInLimit").GetDouble() * ComputePrecheck.DefaultMargin,
            body.GetProperty("refuseAboveSteps").GetDouble(), 1e-6);
        Assert.Equal("|N|! * |N| * (|E| + 1)", body.GetProperty("timeComplexity").GetString());
        Assert.Equal(15, body.GetProperty("measures").GetProperty("|N|").GetDouble());
        Assert.Equal(105, body.GetProperty("measures").GetProperty("|E|").GetDouble());
        Assert.Equal("TSPBruteForce", body.GetProperty("solver").GetString());
        Assert.Equal("Traveling Salesperson", body.GetProperty("problem").GetString());
        var options = body.GetProperty("options").EnumerateArray().Select(o => o.GetString()).ToList();
        Assert.Contains("TSPGreedy", options);
        Assert.DoesNotContain("TSPBruteForce", options);
        Assert.StartsWith("Try a smaller instance, or a faster solver for this problem: ", body.GetProperty("hint").GetString());
    }
}

// The Big-O precheck at the endpoints (#843): a hopeless solve is refused at once, and nothing else changes.
[Collection(TimingCollection.Name)]
public class TooLarge_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly AppFactory _factory;
    private readonly HttpClient _client;

    public TooLarge_Endpoint_Tests(AppFactory factory) {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Solve_HopelessInstance_Returns422TooLarge_AtOnce() {
        var (status, body, took) = await TooLargeChecks.Post(_client, "/ProblemProvider/solve?solver=TSPBruteForce", PrecheckInputs.HopelessTsp);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        // Refused before it runs: far inside the 30-second limit (the parse and the estimate take milliseconds).
        Assert.True(took < TimeSpan.FromSeconds(3), $"took {took}");
        TooLargeChecks.AssertTooLargeBody(body, "solve", ComputeLimits.DefaultTimeLimitSeconds);
    }

    [Fact]
    public async Task Solve_RefusedRequest_GivesItsJobSlotBack() {
        var limiter = _factory.Services.GetRequiredService<ComputeJobLimiter>();
        var (status, _, _) = await TooLargeChecks.Post(_client, "/ProblemProvider/solve?solver=TSPBruteForce", PrecheckInputs.HopelessTsp);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.Equal(0, limiter.Running);
    }

    [Fact]
    public async Task Solve_SmallInstance_IsSolvedAsBefore() {
        string instance = new API.Problems.NPComplete.NPC_TSP.TSP().instance;
        var (status, body, _) = await TooLargeChecks.Post(_client, "/ProblemProvider/solve?solver=TSPBruteForce", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(((ISolver)new API.Problems.NPComplete.NPC_TSP.Solvers.TSPBruteForce()).solve(instance), body.GetString());
    }

    [Fact]
    public async Task Solve_HopelessInstance_WithAFastSolver_IsSolved() {
        var (status, _, _) = await TooLargeChecks.Post(_client, "/ProblemProvider/solve?solver=TSPGreedy", PrecheckInputs.HopelessTsp);
        Assert.Equal(HttpStatusCode.OK, status);
    }

    [Fact]
    public async Task Solve_UnparsableInstance_StillAnswers400() {
        var (status, body, _) = await TooLargeChecks.Post(_client, "/ProblemProvider/solve?solver=TSPBruteForce", "not a graph");
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("instance_parse_error", body.GetProperty("error").GetString());
    }
}

// With the precheck off, a hopeless solve runs until the timer stops it, as before #843.
[Collection(TimingCollection.Name)]
public class TooLarge_PrecheckOff_Tests : IClassFixture<PrecheckOffAppFactory> {
    private readonly HttpClient _client;

    public TooLarge_PrecheckOff_Tests(PrecheckOffAppFactory factory) {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Solve_HopelessInstance_RunsIntoTheTimeLimit() {
        var (status, body, took) = await TooLargeChecks.Post(_client, "/ProblemProvider/solve?solver=TSPBruteForce", PrecheckInputs.HopelessTsp);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.Equal("time_limit", body.GetProperty("error").GetString());
        Assert.True(took >= TimeSpan.FromSeconds(0.5), $"took {took}");
    }
}

// The worker-pool app (SolveWorkersAppFactory) with the precheck on, and one worker.
public class SolveWorkersPrecheckAppFactory : SolveWorkersAppFactory {
    protected override Dictionary<string, string?> Settings() {
        var settings = base.Settings();
        settings.Remove(ComputePrecheck.MarginKey);
        settings[ComputeJobLimiter.MaxJobsKey] = "1";
        return settings;
    }
}

// The precheck refuses in the server, before any worker is used: no worker takes the job, the warm worker stays warm,
// and the job slot is given back.
[Collection(TimingCollection.Name)]
public class TooLarge_Workers_Tests : IClassFixture<SolveWorkersPrecheckAppFactory> {
    private readonly SolveWorkersPrecheckAppFactory _factory;
    private readonly HttpClient _client;

    public TooLarge_Workers_Tests(SolveWorkersPrecheckAppFactory factory) {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HopelessSolve_IsRefusedTooLarge_WithoutAWorker() {
        var pool = _factory.Pool;
        Assert.True(pool.Enabled);
        await SolveWorkerChecks.WaitUntilWarm(pool);
        int before = pool.LastJobPid;
        var (status, _, text, took) = await SolveWorkerChecks.PostSolve(_client, "TSPBruteForce", PrecheckInputs.HopelessTsp);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.True(took < TimeSpan.FromSeconds(SolveWorkersAppFactory.TimeLimitSeconds), $"took {took}");
        TooLargeChecks.AssertTooLargeBody(JsonDocument.Parse(text).RootElement, "solve", SolveWorkersAppFactory.TimeLimitSeconds);
        Assert.Equal(before, pool.LastJobPid);
        Assert.Equal(pool.Size, pool.IdleWorkers);
        Assert.Equal(0, _factory.Services.GetRequiredService<ComputeJobLimiter>().Running);
    }

    [Fact]
    public async Task SmallSolve_StillRunsInAWorker() {
        var pool = _factory.Pool;
        await SolveWorkerChecks.WaitUntilWarm(pool);
        int before = pool.LastJobPid;
        var (status, _, _, _) = await SolveWorkerChecks.PostSolve(_client, "TSPBruteForce", new API.Problems.NPComplete.NPC_TSP.TSP().instance);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.NotEqual(before, pool.LastJobPid);
    }
}

// Visualize is not prechecked yet (#1025): Redux_GUI shows nothing for an error answer, so a hopeless visualize still
// runs to its limit and draws the run so far (#885). A 1-second limit keeps the test short; the precheck stays on, and
// the same instance's solve is refused.
public class OneSecondLimitPrecheckAppFactory : AppFactory {
    protected override void ConfigureWebHost(IWebHostBuilder builder) {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { [ComputeLimits.TimeLimitKey] = "1" }));
    }
}

[Collection(TimingCollection.Name)]
public class TooLarge_Visualize_Tests : IClassFixture<OneSecondLimitPrecheckAppFactory> {
    private readonly HttpClient _client;

    public TooLarge_Visualize_Tests(OneSecondLimitPrecheckAppFactory factory) {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Visualize_HopelessInstance_IsNotRefused_DrawsTheStoppedRun() {
        var (solveStatus, solveBody, _) = await TooLargeChecks.Post(_client, "/ProblemProvider/solve?solver=TSPBruteForce", PrecheckInputs.HopelessTsp);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, solveStatus);
        Assert.Equal(ComputePrecheck.TooLargeError, solveBody.GetProperty("error").GetString());

        var (status, body, _) = await TooLargeChecks.Post(_client,
            "/ProblemProvider/visualize?visualization=TSPDefaultVisualization&solver=TSPBruteForce", PrecheckInputs.HopelessTsp);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.NotEqual(JsonValueKind.Undefined, body.ValueKind);
    }
}
