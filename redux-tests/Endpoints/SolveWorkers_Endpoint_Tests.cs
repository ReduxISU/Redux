using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// A quantum service that accepts connections and never answers, so a quantum solver blocks in its HTTP call (30 s
// timeout) and does not stop at the time limit by itself: the honest way to reach the kill path of a solve worker.
internal static class SilentQuantumService {
    private static readonly List<TcpClient> Held = new();

    public static readonly Lazy<string> Url = new(() => {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        _ = Task.Run(async () => {
            while (true) {
                var client = await listener.AcceptTcpClientAsync();
                lock (Held) Held.Add(client);
            }
        });
        return $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}";
    });
}

// The app with solves in worker processes (#936): a pool of two, a 1-second time limit, a 128 MB memory limit, and the
// silent quantum service.
public class SolveWorkersAppFactory : AppFactory {
    public const double TimeLimitSeconds = 1;
    public const int MemoryMB = 128;
    public const int PoolSize = 2;

    protected virtual Dictionary<string, string?> Settings() => new() {
        [ComputeLimits.IsolateSolvesKey] = "true",
        [ComputeJobLimiter.MaxJobsKey] = PoolSize.ToString(),
        [ComputeLimits.TimeLimitKey] = TimeLimitSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
        [ComputeLimits.SolveMemoryKey] = MemoryMB.ToString(),
        ["QuantumSolver:BaseURL"] = SilentQuantumService.Url.Value,
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder) {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(Settings()));
    }

    public SolveWorkerPool Pool => Services.GetRequiredService<SolveWorkerPool>();
}

// A quantum service that is down: nothing listens on its port, so every connection is refused.
internal static class DownQuantumService {
    public static readonly Lazy<string> Url = new(() => {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return $"http://127.0.0.1:{port}";
    });
}

// The same, with a pool of one, no heap limit in the worker (so only the server's watch on the working set enforces the
// memory limit), and a quantum service that is down.
public class SolveWorkersNoHeapLimitAppFactory : SolveWorkersAppFactory {
    protected override Dictionary<string, string?> Settings() {
        var settings = base.Settings();
        settings[SolveWorkerPool.HeapHardLimitKey] = "false";
        settings[ComputeJobLimiter.MaxJobsKey] = "1";
        settings["QuantumSolver:BaseURL"] = DownQuantumService.Url.Value;
        return settings;
    }
}

internal static class SolveWorkerChecks {
    public static async Task<(HttpStatusCode Status, string? ContentType, string Body, TimeSpan Took)> PostSolve(HttpClient client, string solver, string instance) {
        var sw = Stopwatch.StartNew();
        var response = await client.PostAsync($"/ProblemProvider/solve?solver={solver}",
            new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        sw.Stop();
        return (response.StatusCode, response.Content.Headers.ContentType?.ToString(), body, sw.Elapsed);
    }

    // Waits until the pool has all its warm workers, so a test starts from the steady state.
    public static async Task WaitUntilWarm(SolveWorkerPool pool) {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (pool.IdleWorkers < pool.Size && DateTime.UtcNow < deadline)
            await Task.Delay(20, TestContext.Current.CancellationToken);
        Assert.Equal(pool.Size, pool.IdleWorkers);
    }

    public static bool IsRunning(int pid) {
        try {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        } catch (ArgumentException) {
            return false;
        } catch (InvalidOperationException) {
            return false;
        }
    }

    public static async Task AssertGone(int pid) {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (IsRunning(pid) && DateTime.UtcNow < deadline)
            await Task.Delay(20, TestContext.Current.CancellationToken);
        Assert.False(IsRunning(pid), $"worker process {pid} is still running");
    }

    // A pump schedule whose DP tables are about 800 MB: one pump and a million tank buckets, 24 hours.
    public static readonly string HugePumpSchedule =
        "((1000000,500000),((" + string.Join(",", Enumerable.Repeat("600", 24)) + "),(8,9),(0.12,0.06)),((PumpA,200,5.0,2.5)))";
}

// Solves in worker processes answer exactly as solves in the server do, and the limits really stop them (#936).
[Collection(TimingCollection.Name)]
public class SolveWorkers_Endpoint_Tests : IClassFixture<SolveWorkersAppFactory>, IClassFixture<AppFactory> {
    private readonly SolveWorkersAppFactory _factory;
    private readonly HttpClient _isolated;
    private readonly HttpClient _inProcess;

    public SolveWorkers_Endpoint_Tests(SolveWorkersAppFactory factory, AppFactory inProcess) {
        _factory = factory;
        _isolated = factory.CreateClient();
        _inProcess = inProcess.CreateClient();
    }

    private SolveWorkerPool Pool => _factory.Pool;

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public static TheoryData<string> Solvers() => new() {
        "Sat3BacktrackingSolver", "CliqueBruteForce", "NFASolver", "SSSPSolver", "KnapsackDP", "SubsetSumBruteForce",
        "PumpSchedulingCMSolver", "HamiltonianBruteForce", "DancingLinks",
    };

    [Fact]
    public void ThePoolIsOn_AndTheTestHostsAreNot() {
        Assert.True(Pool.Enabled);
        Assert.Equal(SolveWorkersAppFactory.PoolSize, Pool.Size);
        Assert.Equal(SolveWorkersAppFactory.MemoryMB, Pool.MemoryLimitMB);
        var appFactory = new AppFactory();
        try {
            appFactory.CreateClient();
            Assert.False(appFactory.Services.GetRequiredService<SolveWorkerPool>().Enabled);
            Assert.Equal(0, appFactory.Services.GetRequiredService<SolveWorkerPool>().LiveWorkers);
        } finally {
            appFactory.Dispose();
        }
    }

    [Theory]
    [MemberData(nameof(Solvers))]
    public async Task Solve_DefaultInstance_AnswersExactlyAsInTheServer(string solver) {
        Type type = ProblemProvider.Solvers[solver.ToLower()];
        string instance = SolverTimeLimit_Tests.DefaultInstance(type);
        await SolveWorkerChecks.WaitUntilWarm(Pool);

        var worker = await SolveWorkerChecks.PostSolve(_isolated, solver, instance);
        var server = await SolveWorkerChecks.PostSolve(_inProcess, solver, instance);

        Assert.Equal(HttpStatusCode.OK, worker.Status);
        Assert.Equal(server.Status, worker.Status);
        Assert.Equal(server.ContentType, worker.ContentType);
        Assert.Equal(server.Body, worker.Body);
        Assert.Equal(JsonSerializer.Serialize(((ISolver)Activator.CreateInstance(type)!).solve(instance), Indented), worker.Body);
    }

    public static TheoryData<string, string> Rejected() => new() {
        { "CliqueBruteForce", "not a graph" },
        { "Sat3BacktrackingSolver", "(x1 | " },
        { "NoSuchSolver", "{1}" },
        // 21 variables: past the exact search's limit of 20 (exact_search_limit_exceeded).
        { "WeightedMaxSatBranchAndBoundSolver", "((" + "(" + string.Join(" | ", Enumerable.Range(0, 21).Select(i => $"x{i}")) + ")" + "),(1))" },
    };

    [Theory]
    [MemberData(nameof(Rejected))]
    public async Task RejectedInput_AnswersExactlyAsInTheServer(string solver, string instance) {
        var worker = await SolveWorkerChecks.PostSolve(_isolated, solver, instance);
        var server = await SolveWorkerChecks.PostSolve(_inProcess, solver, instance);

        Assert.Equal(HttpStatusCode.BadRequest, worker.Status);
        Assert.Equal(server.Status, worker.Status);
        Assert.Equal(server.ContentType, worker.ContentType);
        Assert.Equal(server.Body, worker.Body);
    }

    public static TheoryData<string, string> Hard() {
        var data = new TheoryData<string, string>();
        foreach (var row in SolverTimeLimit_Tests.HardInstances)
            if (row.Data.Item1 is "NFASolver" or "PartitionBruteForce" or "Sat3BacktrackingSolver" or "CliqueBruteForce")
                data.Add(row.Data.Item1, row.Data.Item2);
        return data;
    }

    [Theory]
    [MemberData(nameof(Hard))]
    public async Task SolverPastTheLimit_StopsItself_Answers422TimeLimit_AndItsWorkerIsGone(string solver, string instance) {
        await SolveWorkerChecks.WaitUntilWarm(Pool);
        var (status, _, text, took) = await SolveWorkerChecks.PostSolve(_isolated, solver, instance);
        int pid = Pool.LastJobPid;

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.True(took < TimeSpan.FromSeconds(SolveWorkersAppFactory.TimeLimitSeconds) + ComputeLimits.SolverBackstopGrace, $"took {took}");
        var body = JsonDocument.Parse(text).RootElement;
        Assert.Equal("time_limit", body.GetProperty("error").GetString());
        Assert.Equal("The solve ran past the 1-second time limit, so it was stopped.", body.GetProperty("message").GetString());
        Assert.Equal(ComputeLimits.StoppedBySolverDetail, body.GetProperty("detail").GetString());
        Assert.Equal(solver, body.GetProperty("solver").GetString());
        Assert.StartsWith("Try a smaller instance", body.GetProperty("hint").GetString());
        await SolveWorkerChecks.AssertGone(pid);
    }

    [Fact]
    public async Task SolverThatDoesNotStop_IsKilledAtTheLimitPlusGrace_Answers422Cancelled() {
        // Shor's solver blocks in its call to the silent quantum service and never checks the time limit.
        await SolveWorkerChecks.WaitUntilWarm(Pool);
        var (status, _, text, took) = await SolveWorkerChecks.PostSolve(_isolated, "ShorsQuantumSolver", "15");
        int pid = Pool.LastJobPid;

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        TimeSpan wait = TimeSpan.FromSeconds(SolveWorkersAppFactory.TimeLimitSeconds) + ComputeLimits.SolverBackstopGrace;
        Assert.True(took >= wait && took < wait + TimeSpan.FromSeconds(2), $"took {took}");
        var body = JsonDocument.Parse(text).RootElement;
        Assert.Equal("time_limit", body.GetProperty("error").GetString());
        Assert.Equal(ComputeLimits.CancelledDetail, body.GetProperty("detail").GetString());
        Assert.Equal("ShorsQuantumSolver", body.GetProperty("solver").GetString());
        Assert.Equal(1, body.GetProperty("limitSeconds").GetDouble());
        // Kill returns before the process is gone (on Linux it is reaped a moment later), so wait for it.
        await SolveWorkerChecks.AssertGone(pid);

        // The pool replaces the killed worker, and the next solve works.
        await SolveWorkerChecks.WaitUntilWarm(Pool);
        var next = await SolveWorkerChecks.PostSolve(_isolated, "Sat3BacktrackingSolver", "(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | x1)");
        Assert.Equal(HttpStatusCode.OK, next.Status);
    }

    [Fact]
    public async Task AClientThatGoesAway_KillsItsWorker() {
        await SolveWorkerChecks.WaitUntilWarm(Pool);
        int before = Pool.LastJobPid;
        using var leave = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var request = _isolated.PostAsync("/ProblemProvider/solve?solver=ShorsQuantumSolver",
            new StringContent(JsonSerializer.Serialize("15"), Encoding.UTF8, "application/json"), leave.Token);
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (Pool.LastJobPid == before && DateTime.UtcNow < deadline)
            await Task.Delay(10, TestContext.Current.CancellationToken);
        int pid = Pool.LastJobPid;
        Assert.NotEqual(before, pid);
        Assert.True(SolveWorkerChecks.IsRunning(pid));

        leave.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
        // Gone long before the time limit and its grace (3 s) would have killed it.
        var sw = Stopwatch.StartNew();
        while (SolveWorkerChecks.IsRunning(pid) && sw.Elapsed < TimeSpan.FromSeconds(5))
            await Task.Delay(10, TestContext.Current.CancellationToken);
        Assert.False(SolveWorkerChecks.IsRunning(pid), "the worker of the abandoned solve is still running");
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(1), $"the worker took {sw.Elapsed} to go");
    }

    [Fact]
    public async Task SolveThatOutgrowsTheHeapLimit_Answers422MemoryLimit() {
        await SolveWorkerChecks.WaitUntilWarm(Pool);
        var (status, _, text, _) = await SolveWorkerChecks.PostSolve(_isolated, "PumpSchedulingCMSolver", SolveWorkerChecks.HugePumpSchedule);
        int pid = Pool.LastJobPid;

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        var body = JsonDocument.Parse(text).RootElement;
        Assert.Equal(new[] { "error", "message", "detail", "limitMB", "solver", "problem", "options", "hint" },
            body.EnumerateObject().Select(p => p.Name));
        Assert.Equal("memory_limit", body.GetProperty("error").GetString());
        Assert.Equal("The solve used more than the 128 MB memory limit, so it was stopped.", body.GetProperty("message").GetString());
        Assert.Equal(ComputeLimits.HeapLimitDetail, body.GetProperty("detail").GetString());
        Assert.Equal(SolveWorkersAppFactory.MemoryMB, body.GetProperty("limitMB").GetInt32());
        Assert.Equal("PumpSchedulingCMSolver", body.GetProperty("solver").GetString());
        Assert.Equal("Pump Scheduling Cost Minimization", body.GetProperty("problem").GetString());
        Assert.StartsWith("Try a smaller instance", body.GetProperty("hint").GetString());
        await SolveWorkerChecks.AssertGone(pid);
    }

    [Fact]
    public async Task ABurstAsLargeAsThePool_AllSucceed() {
        await SolveWorkerChecks.WaitUntilWarm(Pool);
        string instance = SolverTimeLimit_Tests.DefaultInstance(ProblemProvider.Solvers["sat3backtrackingsolver"]);
        var burst = await Task.WhenAll(Enumerable.Range(0, Pool.Size)
            .Select(_ => SolveWorkerChecks.PostSolve(_isolated, "Sat3BacktrackingSolver", instance)));
        Assert.All(burst, r => Assert.Equal(HttpStatusCode.OK, r.Status));
        Assert.Single(burst.Select(r => r.Body).Distinct());
    }

    [Fact]
    public async Task ASolveWithNoWarmWorker_StartsOneForItself() {
        // Two at once take both warm workers; the third right after them finds none ready and starts its own.
        await SolveWorkerChecks.WaitUntilWarm(Pool);
        string instance = SolverTimeLimit_Tests.DefaultInstance(ProblemProvider.Solvers["sat3backtrackingsolver"]);
        var first = await Task.WhenAll(Enumerable.Range(0, Pool.Size)
            .Select(_ => SolveWorkerChecks.PostSolve(_isolated, "Sat3BacktrackingSolver", instance)));
        var third = await SolveWorkerChecks.PostSolve(_isolated, "Sat3BacktrackingSolver", instance);
        Assert.All(first, r => Assert.Equal(HttpStatusCode.OK, r.Status));
        Assert.Equal(HttpStatusCode.OK, third.Status);
    }

    [Fact]
    public async Task DisposingTheHost_KillsEveryWorker() {
        var factory = new SolveWorkersAppFactory();
        List<int> pids;
        try {
            factory.CreateClient();
            await SolveWorkerChecks.WaitUntilWarm(factory.Pool);
            pids = factory.Pool.LivePids().ToList();
            Assert.Equal(factory.Pool.Size, pids.Count);
            Assert.All(pids, pid => Assert.True(SolveWorkerChecks.IsRunning(pid)));
        } finally {
            factory.Dispose();
        }
        foreach (int pid in pids)
            await SolveWorkerChecks.AssertGone(pid);
    }
}

// The second worker host: with no heap limit, the server's own watch on the worker's working set stops a solve that
// grows past the limit; and a quantum service that is down still answers 503 from a worker.
[Collection(TimingCollection.Name)]
public class SolveWorkers_NoHeapLimit_Tests : IClassFixture<SolveWorkersNoHeapLimitAppFactory> {
    private readonly SolveWorkersNoHeapLimitAppFactory _factory;
    private readonly HttpClient _client;

    public SolveWorkers_NoHeapLimit_Tests(SolveWorkersNoHeapLimitAppFactory factory) {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SolveThatOutgrowsTheMemoryLimit_IsKilled_Answers422MemoryLimit() {
        await SolveWorkerChecks.WaitUntilWarm(_factory.Pool);
        var (status, _, text, _) = await SolveWorkerChecks.PostSolve(_client, "PumpSchedulingCMSolver", SolveWorkerChecks.HugePumpSchedule);
        int pid = _factory.Pool.LastJobPid;

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        var body = JsonDocument.Parse(text).RootElement;
        Assert.Equal("memory_limit", body.GetProperty("error").GetString());
        Assert.Equal(ComputeLimits.MemoryWatchDetail, body.GetProperty("detail").GetString());
        // Kill returns before the process is gone (on Linux it is reaped a moment later), so wait for it.
        await SolveWorkerChecks.AssertGone(pid);
    }

    [Fact]
    public async Task QuantumServiceDown_InAWorker_Answers503SolverUnavailable() {
        await SolveWorkerChecks.WaitUntilWarm(_factory.Pool);
        var (status, _, text, _) = await SolveWorkerChecks.PostSolve(_client, "ShorsQuantumSolver", "15");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, status);
        var body = JsonDocument.Parse(text).RootElement;
        Assert.Equal("solver_unavailable", body.GetProperty("error").GetString());
        Assert.Equal("ShorsQuantumSolver", body.GetProperty("solver").GetString());
        var shor = (ISolver)Activator.CreateInstance(ProblemProvider.Solvers["shorsquantumsolver"])!;
        Assert.Equal($"{shor.solverName} couldn't get an answer from the quantum service, so {body.GetProperty("problem").GetString()} wasn't solved.",
            body.GetProperty("message").GetString());
        Assert.Contains(DownQuantumService.Url.Value, body.GetProperty("detail").GetString());
        Assert.Contains("runs without the quantum service", body.GetProperty("hint").GetString());
    }
}
