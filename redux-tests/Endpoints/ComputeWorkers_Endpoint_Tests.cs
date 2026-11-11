using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The app with every compute job in worker processes (#991) and the limits the in-process AppFactory has (the default
// 10-second time limit, the default 512 MB memory limit, and a quantum service that refuses connections, as the
// in-process host's does), so the two hosts answer alike; a pool of four, so the sample below can run four jobs at once.
// (The silent quantum service of SolveWorkersAppFactory would leave the visualizations that call it hanging in a worker
// until the time limit, where the in-process host's refused connection answers at once.)
public class ComputeWorkersAppFactory : SolveWorkersAppFactory {
    protected override Dictionary<string, string?> Settings() {
        var settings = base.Settings();
        settings[ComputeLimits.TimeLimitKey] = ComputeLimits.DefaultTimeLimitSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        settings[ComputeLimits.SolveMemoryKey] = ComputeLimits.DefaultSolveMemoryMB.ToString();
        settings[ComputeJobLimiter.MaxJobsKey] = "4";
        settings["QuantumSolver:BaseURL"] = DownQuantumService.Url.Value;
        return settings;
    }
}

internal sealed record ComputeRequest(string Path, string Body) {
    public override string ToString() => Path;
}

internal sealed record ComputeResponse(HttpStatusCode Status, string? ContentType, string Body, TimeSpan Took);

internal static class ComputeWorkerChecks {
    public static string Json(object value) => JsonSerializer.Serialize(value);

    public static string Q(string value) => Uri.EscapeDataString(value);

    public static ComputeRequest Verify(string verifier, string instance, string certificate) =>
        new($"/ProblemProvider/verify?verifier={Q(verifier)}", Json(new { Certificate = certificate, ProblemInstance = instance }));

    public static ComputeRequest Reduce(string reduction, string instance, bool includePictures = false) =>
        new($"/ProblemProvider/reduce?reduction={Q(reduction)}{(includePictures ? "&includePictures=true" : "")}", Json(instance));

    public static ComputeRequest Gadgets(string reduction, string instance) =>
        new($"/ProblemProvider/gadgets?reduction={Q(reduction)}", Json(instance));

    public static ComputeRequest MapSolution(string reduction, string solution, string instance) =>
        new($"/ProblemProvider/mapSolution?reduction={Q(reduction)}&solution={Q(solution)}", Json(instance));

    public static ComputeRequest MapSolutionBack(string reduction, string solution, string instance, bool steps) =>
        new($"/ProblemProvider/mapSolutionBack?reduction={Q(reduction)}&solution={Q(solution)}&steps={(steps ? "true" : "false")}", Json(instance));

    public static ComputeRequest Visualize(string visualization, string instance, string? format = null, string? solver = null) =>
        new($"/ProblemProvider/visualize?visualization={Q(visualization)}{(format == null ? "" : "&format=" + format)}{(solver == null ? "" : "&solver=" + Q(solver))}",
            Json(instance));

    public static ComputeRequest VisualizeReduction(string reduction, string solution, string instance, bool includePictures = false) =>
        new($"/ProblemProvider/visualizeReduction?reduction={Q(reduction)}&solution={Q(solution)}{(includePictures ? "&includePictures=true" : "")}",
            Json(instance));

    public static async Task<ComputeResponse> Post(HttpClient client, ComputeRequest request, CancellationToken? cancel = null) {
        var sw = Stopwatch.StartNew();
        var response = await client.PostAsync(request.Path, new StringContent(request.Body, Encoding.UTF8, "application/json"),
            cancel ?? TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        sw.Stop();
        return new(response.StatusCode, response.Content.Headers.ContentType?.ToString(), body, sw.Elapsed);
    }

    // The source-problem default instance of a reduction and the answers the sweep maps; null when there is none.
    public static (string Instance, string? SourceAnswer, string? TargetAnswer)? ReductionInputs(Type reduction) {
        try {
            var template = (IReduction)Activator.CreateInstance(reduction)!;
            string instance = template.reductionFrom.instance;
            if (string.IsNullOrWhiteSpace(instance))
                return null;
            string? source = SolveQuietly(template.reductionFrom.defaultSolver, instance);
            string? target = null;
            try {
                var built = (IReduction)Activator.CreateInstance(reduction, instance)!;
                target = SolveQuietly(built.reductionTo.defaultSolver, built.reductionTo.instance);
            } catch {
                // The reduction of the default instance throws; the sweep compares that too.
            }
            return (instance, source, target);
        } catch {
            return null;
        }
    }

    // A default solver's answer, or null for one that needs the quantum service, throws, or does not answer within a
    // few seconds (some default instances are slow for their default solver).
    public static string? SolveQuietly(ISolver solver, string instance) {
        if (solver.GetType().Name.Contains("Quantum", StringComparison.OrdinalIgnoreCase))
            return null;
        try {
            var run = Task.Run(() => solver.Run(instance, withSteps: false, TimeSpan.FromSeconds(2)));
            if (!run.Wait(TimeSpan.FromSeconds(5)) || run.Result.TimedOut)
                return null;
            return run.Result.Answer;
        } catch {
            return null;
        }
    }

    public static string? DefaultInstanceOf(Type? problem) {
        if (problem == null)
            return null;
        try {
            return ((IProblem)Activator.CreateInstance(problem)!).defaultInstance;
        } catch {
            return null;
        }
    }

    public static async Task WaitForRunningJobs(ComputeJobLimiter limiter, int count) {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (limiter.Running < count && DateTime.UtcNow < deadline)
            await Task.Delay(5, TestContext.Current.CancellationToken);
        Assert.Equal(count, limiter.Running);
    }
}

// Every compute endpoint answers from a worker process byte for byte as it does in the server (#991): the same
// status, content type and body. The requests are a fixed sample, chosen to be deterministic (no randomized solver), that
// covers every kind of job (verify, reduce, mapSolution, mapSolutionBack, gadgets, visualize in both formats,
// visualizeReduction), a 400 parse-error body of each, and a spread of problem families: formulas (SAT, 3SAT, weighted
// Max-SAT), graphs (Clique, Vertex Cover), numbers and sets (Subset Sum, Partition, Exact Cover, Set Cover), automata
// (DFA, NFA) and table pictures (NFA, SSSP). Nothing here is timed, so the class runs in parallel with the rest of the
// suite, not in the timing collection.
public class ComputeWorkers_SameAnswers_Tests : IClassFixture<ComputeWorkersAppFactory>, IClassFixture<AppFactory> {
    private readonly ComputeWorkersAppFactory _factory;
    private readonly HttpClient _isolated;
    private readonly HttpClient _inProcess;

    public ComputeWorkers_SameAnswers_Tests(ComputeWorkersAppFactory factory, AppFactory inProcess) {
        _factory = factory;
        _isolated = factory.CreateClient();
        _inProcess = inProcess.CreateClient();
        _isolated.Timeout = TimeSpan.FromMinutes(2);
        _inProcess.Timeout = TimeSpan.FromMinutes(2);
    }

    private const string Malformed = "not an instance ((";
    private const string MalformedCertificate = "not a certificate ((";

    // Reductions across the families: formula to graph, graph to graph, set to number, number to number, graph to set,
    // formula to formula. The reversible ones also map an answer back.
    private static readonly string[] SampleReductions = {
        "SipserReduceToCliqueStandard", "sipserReductionVertexCover", "KarpExactCoverToSubsetSum",
        "SubsetSumToPartitionReduction", "KarpVertexCoverToSetCover", "KarpSATToSAT3",
    };

    // Visualizations across the families, among them the automata and the table pictures.
    private static readonly string[] SampleVisualizations = {
        "Sat3DefaultVisualization", "WeightedMaxSatVisualization", "CliqueDefaultVisualization",
        "VertexCoverDefaultVisualization", "PartitionDefaultVisualization", "SetCoverDefaultVisualization",
        "DFAVisualization", "NFATableVisualization", "SSSPTableVisualization",
    };

    // Problems whose default verifier is checked.
    private static readonly string[] SampleProblems = {
        "sat3", "clique", "vertexcover", "subsetsum", "setcover", "dfa", "nfa", "sssp",
    };

    // Each request once in the server and once in a worker, four at a time. A 500 is compared by status only: its body
    // is the exception page, which carries a stack trace.
    private async Task AssertSameAnswers(IReadOnlyList<ComputeRequest> requests) {
        await SolveWorkerChecks.WaitUntilWarm(_factory.Pool);
        var mismatches = new System.Collections.Concurrent.ConcurrentBag<string>();
        using var gate = new SemaphoreSlim(_factory.Pool.Size);
        await Task.WhenAll(requests.Select(async request => {
            await gate.WaitAsync(TestContext.Current.CancellationToken);
            try {
                var server = await ComputeWorkerChecks.Post(_inProcess, request);
                var worker = await ComputeWorkerChecks.Post(_isolated, request);
                if (worker.Status != server.Status)
                    mismatches.Add($"{request}: status {worker.Status} in a worker, {server.Status} in the server; worker body {Head(worker.Body)}");
                else if (server.Status != HttpStatusCode.InternalServerError
                         && (worker.ContentType != server.ContentType || worker.Body != server.Body))
                    mismatches.Add($"{request}: {worker.ContentType} {Head(worker.Body)} in a worker, {server.ContentType} {Head(server.Body)} in the server");
            } finally {
                gate.Release();
            }
        }));
        Assert.True(mismatches.IsEmpty, string.Join(Environment.NewLine, mismatches.OrderBy(m => m, StringComparer.Ordinal)));
    }

    private static string Head(string body) => body.Length > 300 ? body[..300] + "..." : body;

    // At least one answer of each status the sample is meant to reach, so a sample that silently stops reaching the work
    // (every request a 400, say) fails.
    private async Task AssertReaches(ComputeRequest request, HttpStatusCode status) {
        var response = await ComputeWorkerChecks.Post(_isolated, request);
        Assert.True(response.Status == status, $"{request}: {response.Status} {Head(response.Body)}");
    }

    [Fact]
    public async Task Reductions_AnswerExactlyAsInTheServer() {
        var requests = new List<ComputeRequest>();
        int mappedBack = 0;
        foreach (string name in SampleReductions) {
            Type type = ProblemProvider.Reductions[name.ToLower()];
            var inputs = ComputeWorkerChecks.ReductionInputs(type);
            Assert.True(inputs is { SourceAnswer: not null }, $"{name} has no default instance and answer");
            var (instance, source, target) = inputs!.Value;
            requests.Add(ComputeWorkerChecks.Reduce(name, instance));
            requests.Add(ComputeWorkerChecks.Reduce(name, instance, includePictures: true));
            requests.Add(ComputeWorkerChecks.Gadgets(name, instance));
            requests.Add(ComputeWorkerChecks.MapSolution(name, source!, instance));
            requests.Add(ComputeWorkerChecks.VisualizeReduction(name, source!, instance));
            requests.Add(ComputeWorkerChecks.VisualizeReduction(name, source!, instance, includePictures: true));
            if (target != null && ((IReduction)Activator.CreateInstance(type)!).hasBackwardMap()) {
                requests.Add(ComputeWorkerChecks.MapSolutionBack(name, target, instance, steps: false));
                requests.Add(ComputeWorkerChecks.MapSolutionBack(name, target, instance, steps: true));
                mappedBack++;
            }
        }
        Assert.True(mappedBack >= 2, $"only {mappedBack} reductions mapped an answer back");
        // Malformed input: the 400 parse-error bodies of each kind.
        const string sipser = "SipserReduceToCliqueStandard";
        requests.Add(ComputeWorkerChecks.Reduce(sipser, Malformed));
        requests.Add(ComputeWorkerChecks.Gadgets(sipser, Malformed));
        requests.Add(ComputeWorkerChecks.MapSolution(sipser, "(x1:True)", Malformed));
        requests.Add(ComputeWorkerChecks.MapSolution(sipser, MalformedCertificate, "(x1 | x2 | x3)"));
        requests.Add(ComputeWorkerChecks.MapSolutionBack(sipser, "{x1_0}", Malformed, steps: false));
        requests.Add(ComputeWorkerChecks.MapSolutionBack(sipser, MalformedCertificate, "(x1 | x2 | x3)", steps: true));
        requests.Add(ComputeWorkerChecks.VisualizeReduction(sipser, "(x1:True)", Malformed));
        requests.Add(ComputeWorkerChecks.Reduce("KarpExactCoverToSubsetSum", Malformed));
        // A reduction with no backward map: the no_backward_map 400.
        requests.Add(ComputeWorkerChecks.MapSolutionBack("KarpExactCoverToSubsetSum", "{1}", "({1,2},{{1},{2}})", steps: false));
        // A chain of two reductions.
        requests.Add(ComputeWorkerChecks.VisualizeReduction("SipserReduceToCliqueStandard-sipserReductionVertexCover", "(x1:True,x2:True)",
            "(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | x1)"));
        await AssertSameAnswers(requests);
        await AssertReaches(requests[0], HttpStatusCode.OK);
        await AssertReaches(ComputeWorkerChecks.Reduce(sipser, Malformed), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Visualizations_AnswerExactlyAsInTheServer() {
        var requests = new List<ComputeRequest>();
        foreach (string name in SampleVisualizations) {
            Type type = ProblemProvider.Visualizers[name.ToLower()];
            var vis = (IVisualization)Activator.CreateInstance(type)!;
            string? instance = ComputeWorkerChecks.DefaultInstanceOf(
                ProblemProvider.ProblemTypeOf(type, typeof(IVisualization<>)) ?? ProblemProvider.ProblemTypeOf(vis.solver.GetType(), typeof(ISolver<>)));
            Assert.False(string.IsNullOrWhiteSpace(instance), $"{name} has no default instance");
            requests.Add(ComputeWorkerChecks.Visualize(name, instance!));
            requests.Add(ComputeWorkerChecks.Visualize(name, instance!, "frames"));
        }
        // Malformed input, in both formats: the 400 parse-error bodies.
        requests.Add(ComputeWorkerChecks.Visualize("Sat3DefaultVisualization", Malformed));
        requests.Add(ComputeWorkerChecks.Visualize("CliqueDefaultVisualization", Malformed, "frames"));
        requests.Add(ComputeWorkerChecks.Visualize("NFATableVisualization", Malformed));
        await AssertSameAnswers(requests);
        await AssertReaches(requests[0], HttpStatusCode.OK);
        await AssertReaches(ComputeWorkerChecks.Visualize("Sat3DefaultVisualization", Malformed), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Verifiers_AnswerExactlyAsInTheServer() {
        var requests = new List<ComputeRequest>();
        ComputeRequest? solved = null;
        foreach (string key in SampleProblems) {
            var problem = (IProblem)Activator.CreateInstance(ProblemProvider.Problems[key])!;
            string verifier = problem.defaultVerifier.GetType().Name;
            string instance = problem.defaultInstance;
            string? answer = ComputeWorkerChecks.SolveQuietly(problem.defaultSolver, instance);
            Assert.True(answer != null, $"{key}'s default solver gave no answer");
            var request = ComputeWorkerChecks.Verify(verifier, instance, answer!);
            solved ??= request;
            requests.Add(request);
            requests.Add(ComputeWorkerChecks.Verify(verifier, instance, "{}"));
            requests.Add(ComputeWorkerChecks.Verify(verifier, instance, MalformedCertificate));
            requests.Add(ComputeWorkerChecks.Verify(verifier, Malformed, "{}"));
        }
        await AssertSameAnswers(requests);
        await AssertReaches(solved!, HttpStatusCode.OK);
        await AssertReaches(ComputeWorkerChecks.Verify("SAT3Verifier", Malformed, "{}"), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ALargeVisualization_TravelsWhole() {
        // A 15-clause formula over 12 variables (the first half of the time-limit tests' formula), reduced to a 45-node
        // CLIQUE graph and drawn: a reply of about a megabyte, sent back on one line.
        string sat3 = string.Join(" & ", Enumerable.Range(0, 15).Select(i =>
            $"(x{i % 12 + 1} | !x{(i * 7 + 3) % 12 + 1} | x{(i * 13 + 5) % 12 + 1})"));
        string answer = ComputeWorkerChecks.SolveQuietly((ISolver)Activator.CreateInstance(ProblemProvider.Solvers["sat3backtrackingsolver"])!, sat3)!;
        var requests = new[] {
            ComputeWorkerChecks.VisualizeReduction("SipserReduceToCliqueStandard", answer, sat3),
        };
        await AssertSameAnswers(requests);
        var largest = await ComputeWorkerChecks.Post(_isolated, requests[0]);
        Assert.Equal(HttpStatusCode.OK, largest.Status);
        Assert.True(largest.Body.Length > 500_000, $"only {largest.Body.Length} characters");
    }
}

// The limits stop every compute endpoint's job in a worker, with the errors solve answers with (#991).
[Collection(TimingCollection.Name)]
public class ComputeWorkers_Limits_Tests : IClassFixture<SolveWorkersAppFactory> {
    private readonly SolveWorkersAppFactory _factory;
    private readonly HttpClient _client;

    public ComputeWorkers_Limits_Tests(SolveWorkersAppFactory factory) {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private SolveWorkerPool Pool => _factory.Pool;

    private const string Sat3 = "(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | x1)";

    // One request per endpoint, with the job name its errors use and the context field that names what it ran.
    public static TheoryData<string, string, string, string> Endpoints() => new() {
        { "verify", "verification", "verifier", "SAT3Verifier" },
        { "reduce", "reduction", "reduction", "SipserReduceToCliqueStandard" },
        { "gadgets", "reduction", "reduction", "SipserReduceToCliqueStandard" },
        { "mapSolution", "reduction", "reduction", "SipserReduceToCliqueStandard" },
        { "mapSolutionBack", "reduction", "reduction", "SipserReduceToCliqueStandard" },
        { "visualizeReduction", "reduction", "reduction", "SipserReduceToCliqueStandard" },
        { "visualize", "visualization", "solver", "Sat3BacktrackingSolver" },
    };

    private static ComputeRequest RequestFor(string endpoint) => endpoint switch {
        "verify" => ComputeWorkerChecks.Verify("SAT3Verifier", Sat3, "(x1:True,x2:True)"),
        "reduce" => ComputeWorkerChecks.Reduce("SipserReduceToCliqueStandard", Sat3),
        "gadgets" => ComputeWorkerChecks.Gadgets("SipserReduceToCliqueStandard", Sat3),
        "mapSolution" => ComputeWorkerChecks.MapSolution("SipserReduceToCliqueStandard", "(x1:True,x2:True)", Sat3),
        "mapSolutionBack" => ComputeWorkerChecks.MapSolutionBack("SipserReduceToCliqueStandard", "{x1_0,x1_1,x1_2}", Sat3, steps: false),
        "visualizeReduction" => ComputeWorkerChecks.VisualizeReduction("SipserReduceToCliqueStandard", "(x1:True,x2:True)", Sat3),
        "visualize" => ComputeWorkerChecks.Visualize("Sat3DefaultVisualization", Sat3, solver: "Sat3BacktrackingSolver"),
        _ => throw new ArgumentException(endpoint),
    };

    private async Task<(ComputeResponse Response, JsonElement Body, int Pid)> PostWithFault(string endpoint, string fault) {
        await SolveWorkerChecks.WaitUntilWarm(Pool);
        Pool.TestFault = fault;
        try {
            var response = await ComputeWorkerChecks.Post(_client, RequestFor(endpoint));
            return (response, JsonDocument.Parse(response.Body).RootElement.Clone(), Pool.LastJobPid);
        } finally {
            Pool.TestFault = null;
        }
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task EveryEndpoint_AnswersFromAWorker(string endpoint, string job, string field, string name) {
        _ = (job, field, name);
        await SolveWorkerChecks.WaitUntilWarm(Pool);
        int before = Pool.LastJobPid;
        var response = await ComputeWorkerChecks.Post(_client, RequestFor(endpoint));
        Assert.True(response.Status is HttpStatusCode.OK or HttpStatusCode.BadRequest, $"{response.Status}: {response.Body}");
        Assert.NotEqual(before, Pool.LastJobPid);
        await SolveWorkerChecks.AssertGone(Pool.LastJobPid);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task AJobPastTheLimit_IsKilled_Answers422TimeLimit(string endpoint, string job, string field, string name) {
        var (response, body, pid) = await PostWithFault(endpoint, ComputeWorker.HangFault);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.Status);
        bool runsSolver = endpoint == "visualize";
        // Verify and the reductions have no time checks, so the worker is killed at the limit itself; a visualize,
        // whose solver stops itself, gets the grace a solve gets.
        TimeSpan wait = TimeSpan.FromSeconds(SolveWorkersAppFactory.TimeLimitSeconds) + (runsSolver ? ComputeLimits.SolverBackstopGrace : TimeSpan.Zero);
        Assert.True(response.Took >= wait && response.Took < wait + TimeSpan.FromSeconds(2), $"took {response.Took}");
        Assert.Equal("time_limit", body.GetProperty("error").GetString());
        Assert.Equal($"The {job} ran past the 1-second time limit, so it was stopped.", body.GetProperty("message").GetString());
        Assert.Equal(runsSolver ? ComputeLimits.CancelledDetail : ComputeLimits.JobCancelledDetail, body.GetProperty("detail").GetString());
        Assert.Equal(1, body.GetProperty("limitSeconds").GetDouble());
        Assert.Equal(name, body.GetProperty(field).GetString());
        Assert.StartsWith("Try a smaller instance", body.GetProperty("hint").GetString());
        await SolveWorkerChecks.AssertGone(pid);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task AJobPastTheHeapLimit_Answers422MemoryLimit(string endpoint, string job, string field, string name) {
        var (response, body, pid) = await PostWithFault(endpoint, ComputeWorker.AllocateFault);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.Status);
        bool runsSolver = endpoint == "visualize";
        Assert.Equal("memory_limit", body.GetProperty("error").GetString());
        Assert.Equal($"The {job} used more than the 128 MB memory limit, so it was stopped.", body.GetProperty("message").GetString());
        Assert.Equal(runsSolver ? ComputeLimits.HeapLimitDetail : ComputeLimits.JobHeapLimitDetail, body.GetProperty("detail").GetString());
        Assert.Equal(SolveWorkersAppFactory.MemoryMB, body.GetProperty("limitMB").GetInt32());
        Assert.Equal(name, body.GetProperty(field).GetString());
        Assert.StartsWith("Try a smaller instance", body.GetProperty("hint").GetString());
        await SolveWorkerChecks.AssertGone(pid);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task AWorkerThatDies_Answers500WorkerFailed(string endpoint, string job, string field, string name) {
        var (response, body, pid) = await PostWithFault(endpoint, ComputeWorker.CrashFault);

        Assert.Equal(HttpStatusCode.InternalServerError, response.Status);
        Assert.Equal(SolveWorkerPool.WorkerFailedError, body.GetProperty("error").GetString());
        Assert.Equal($"The {job} stopped unexpectedly, so there is no answer.", body.GetProperty("message").GetString());
        Assert.Equal($"The worker process running the {job} exited (code 3) before it answered.", body.GetProperty("detail").GetString());
        Assert.Equal(name, body.GetProperty(field).GetString());
        Assert.StartsWith("Try again.", body.GetProperty("hint").GetString());
        await SolveWorkerChecks.AssertGone(pid);
        // The pool replaces the dead worker, and the next job works.
        await SolveWorkerChecks.WaitUntilWarm(Pool);
        var next = await ComputeWorkerChecks.Post(_client, RequestFor(endpoint));
        Assert.True(next.Status is HttpStatusCode.OK or HttpStatusCode.BadRequest, $"{next.Status}: {next.Body}");
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task AnUnexpectedExceptionInAWorker_Answers500(string endpoint, string job, string field, string name) {
        _ = (job, field, name);
        await SolveWorkerChecks.WaitUntilWarm(Pool);
        Pool.TestFault = ComputeWorker.ThrowFault;
        ComputeResponse response;
        try {
            response = await ComputeWorkerChecks.Post(_client, RequestFor(endpoint));
        } finally {
            Pool.TestFault = null;
        }
        Assert.Equal(HttpStatusCode.InternalServerError, response.Status);
        // The developer exception page of the test host shows the worker's exception, rethrown in the server.
        Assert.Contains(ComputeWorker.ThrowFaultMessage, response.Body);
    }

    [Fact]
    public async Task VisualizeWithASolverThatDoesNotStop_IsKilled_Answers422Cancelled() {
        // Shor's solver blocks in its call to the silent quantum service and never checks the time limit.
        await SolveWorkerChecks.WaitUntilWarm(Pool);
        var response = await ComputeWorkerChecks.Post(_client, ComputeWorkerChecks.Visualize("ShorsDefaultVisualization", "15", solver: "ShorsQuantumSolver"));
        int pid = Pool.LastJobPid;

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.Status);
        var body = JsonDocument.Parse(response.Body).RootElement;
        Assert.Equal("time_limit", body.GetProperty("error").GetString());
        Assert.Equal("The visualization ran past the 1-second time limit, so it was stopped.", body.GetProperty("message").GetString());
        Assert.Equal(ComputeLimits.CancelledDetail, body.GetProperty("detail").GetString());
        Assert.Equal("ShorsQuantumSolver", body.GetProperty("solver").GetString());
        await SolveWorkerChecks.AssertGone(pid);
    }

    [Fact]
    public async Task VisualizeThatOutgrowsTheHeapLimit_Answers422MemoryLimit() {
        // The pump schedule whose DP tables are about 800 MB, drawn: its solver runs out of the 128 MB heap.
        await SolveWorkerChecks.WaitUntilWarm(Pool);
        var response = await ComputeWorkerChecks.Post(_client, ComputeWorkerChecks.Visualize("PumpSchedulingCMVisualization", SolveWorkerChecks.HugePumpSchedule));
        int pid = Pool.LastJobPid;

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.Status);
        var body = JsonDocument.Parse(response.Body).RootElement;
        Assert.Equal(new[] { "error", "message", "detail", "limitMB", "solver", "problem", "options", "hint" },
            body.EnumerateObject().Select(p => p.Name));
        Assert.Equal("memory_limit", body.GetProperty("error").GetString());
        Assert.Equal("The visualization used more than the 128 MB memory limit, so it was stopped.", body.GetProperty("message").GetString());
        Assert.Equal(ComputeLimits.HeapLimitDetail, body.GetProperty("detail").GetString());
        Assert.Equal("PumpSchedulingCMSolver", body.GetProperty("solver").GetString());
        await SolveWorkerChecks.AssertGone(pid);
    }

    [Fact]
    public async Task WorkerJobs_HoldTheirSlots_SoTheCapTurnsTheNextAway() {
        // Two hung verifications fill both job slots; a reduction, a visualize and a solve are turned away with 429
        // until the workers are killed at the limit, and then they run.
        await SolveWorkerChecks.WaitUntilWarm(Pool);
        var limiter = _factory.Services.GetRequiredService<ComputeJobLimiter>();
        Pool.TestFault = ComputeWorker.HangFault;
        Task<ComputeResponse>[] held;
        try {
            held = Enumerable.Range(0, Pool.Size).Select(_ => ComputeWorkerChecks.Post(_client, RequestFor("verify"))).ToArray();
            await ComputeWorkerChecks.WaitForRunningJobs(limiter, Pool.Size);
            foreach (string endpoint in new[] { "reduce", "visualize", "gadgets" }) {
                var busy = await ComputeWorkerChecks.Post(_client, RequestFor(endpoint));
                Assert.Equal(HttpStatusCode.TooManyRequests, busy.Status);
                Assert.Equal(ComputeJobLimiter.ServerBusyError, JsonDocument.Parse(busy.Body).RootElement.GetProperty("error").GetString());
            }
        } finally {
            Pool.TestFault = null;
        }
        var answers = await Task.WhenAll(held);
        Assert.All(answers, a => Assert.Equal(HttpStatusCode.UnprocessableEntity, a.Status));
        Assert.Equal(0, limiter.Running);
        await SolveWorkerChecks.WaitUntilWarm(Pool);
        var after = await ComputeWorkerChecks.Post(_client, RequestFor("reduce"));
        Assert.Equal(HttpStatusCode.OK, after.Status);
    }

    [Fact]
    public async Task AClientThatGoesAway_KillsTheWorkerOfItsJob() {
        await SolveWorkerChecks.WaitUntilWarm(Pool);
        int before = Pool.LastJobPid;
        Pool.TestFault = ComputeWorker.HangFault;
        Task request;
        int pid;
        using var leave = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        try {
            request = ComputeWorkerChecks.Post(_client, RequestFor("reduce"), leave.Token);
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (Pool.LastJobPid == before && DateTime.UtcNow < deadline)
                await Task.Delay(10, TestContext.Current.CancellationToken);
            pid = Pool.LastJobPid;
            Assert.NotEqual(before, pid);
        } finally {
            Pool.TestFault = null;
        }
        leave.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
        var sw = Stopwatch.StartNew();
        while (SolveWorkerChecks.IsRunning(pid) && sw.Elapsed < TimeSpan.FromSeconds(5))
            await Task.Delay(10, TestContext.Current.CancellationToken);
        Assert.False(SolveWorkerChecks.IsRunning(pid), "the worker of the abandoned job is still running");
        // Gone long before the 1-second limit would have killed it.
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(1), $"the worker took {sw.Elapsed} to go");
        await SolveWorkerChecks.WaitUntilWarm(Pool);
    }
}

// With no heap limit in the worker, the server's own watch on the working set stops a job of any endpoint that grows
// past the memory limit (#991).
[Collection(TimingCollection.Name)]
public class ComputeWorkers_NoHeapLimit_Tests : IClassFixture<SolveWorkersNoHeapLimitAppFactory> {
    private readonly SolveWorkersNoHeapLimitAppFactory _factory;
    private readonly HttpClient _client;

    public ComputeWorkers_NoHeapLimit_Tests(SolveWorkersNoHeapLimitAppFactory factory) {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public static TheoryData<string, string, string> Requests() => new() {
        { "verification", ComputeWorkerChecks.Verify("SAT3Verifier", "(x1 | x2 | x3)", "(x1:True)").Path, ComputeWorkerChecks.Json(new { Certificate = "(x1:True)", ProblemInstance = "(x1 | x2 | x3)" }) },
        { "reduction", ComputeWorkerChecks.Reduce("SipserReduceToCliqueStandard", "(x1 | x2 | x3)").Path, ComputeWorkerChecks.Json("(x1 | x2 | x3)") },
        { "visualization", ComputeWorkerChecks.Visualize("Sat3DefaultVisualization", "(x1 | x2 | x3)").Path, ComputeWorkerChecks.Json("(x1 | x2 | x3)") },
    };

    [Theory]
    [MemberData(nameof(Requests))]
    public async Task AJobThatOutgrowsTheMemoryLimit_IsKilled_Answers422MemoryLimit(string job, string path, string requestBody) {
        await SolveWorkerChecks.WaitUntilWarm(_factory.Pool);
        _factory.Pool.TestFault = ComputeWorker.AllocateFault;
        ComputeResponse response;
        try {
            response = await ComputeWorkerChecks.Post(_client, new ComputeRequest(path, requestBody));
        } finally {
            _factory.Pool.TestFault = null;
        }
        int pid = _factory.Pool.LastJobPid;

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.Status);
        var body = JsonDocument.Parse(response.Body).RootElement;
        Assert.Equal("memory_limit", body.GetProperty("error").GetString());
        Assert.Equal($"The {job} used more than the 128 MB memory limit, so it was stopped.", body.GetProperty("message").GetString());
        Assert.Equal(job == "visualization" ? ComputeLimits.MemoryWatchDetail : ComputeLimits.JobMemoryWatchDetail,
            body.GetProperty("detail").GetString());
        await SolveWorkerChecks.AssertGone(pid);
    }
}

// With the pool off (ComputeLimits:IsolateSolves false, as in AppFactory), no compute endpoint starts a worker: each
// runs in the server, under the #943 backstop, as before #991.
[Collection(TimingCollection.Name)]
public class ComputeWorkers_PoolOff_Tests : IClassFixture<AppFactory>, IClassFixture<TinyComputeLimitAppFactory> {
    private readonly AppFactory _factory;
    private readonly HttpClient _client;
    private readonly TinyComputeLimitAppFactory _tiny;
    private readonly HttpClient _tinyClient;

    public ComputeWorkers_PoolOff_Tests(AppFactory factory, TinyComputeLimitAppFactory tiny) {
        _factory = factory;
        _client = factory.CreateClient();
        _tiny = tiny;
        _tinyClient = tiny.CreateClient();
    }

    private const string Sat3 = "(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | x1)";

    [Fact]
    public async Task EveryEndpoint_RunsInTheServer() {
        var pool = _factory.Services.GetRequiredService<SolveWorkerPool>();
        Assert.False(pool.Enabled);
        foreach (var request in new[] {
                     ComputeWorkerChecks.Verify("SAT3Verifier", Sat3, "(x1:True,x2:True)"),
                     ComputeWorkerChecks.Reduce("SipserReduceToCliqueStandard", Sat3),
                     ComputeWorkerChecks.Gadgets("SipserReduceToCliqueStandard", Sat3),
                     ComputeWorkerChecks.MapSolution("SipserReduceToCliqueStandard", "(x1:True,x2:True)", Sat3),
                     ComputeWorkerChecks.MapSolutionBack("SipserReduceToCliqueStandard", "{x1_0,x1_1,x1_2}", Sat3, steps: true),
                     ComputeWorkerChecks.VisualizeReduction("SipserReduceToCliqueStandard", "(x1:True,x2:True)", Sat3),
                     ComputeWorkerChecks.Visualize("Sat3DefaultVisualization", Sat3, "frames"),
                 }) {
            var response = await ComputeWorkerChecks.Post(_client, request);
            Assert.True(response.Status is HttpStatusCode.OK or HttpStatusCode.BadRequest, $"{request}: {response.Status} {response.Body}");
        }
        Assert.Equal(0, pool.LiveWorkers);
        Assert.Equal(0, pool.LastJobPid);
    }

    [Fact]
    public async Task TheBackstop_StillAnswersWithItsOwnDetail() {
        var reduce = await ComputeWorkerChecks.Post(_tinyClient, ComputeWorkerChecks.Reduce("SipserReduceToCliqueStandard", SlowInputs.Sat3));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reduce.Status);
        Assert.Equal(ComputeLimits.BackstopDetail, JsonDocument.Parse(reduce.Body).RootElement.GetProperty("detail").GetString());
        Assert.Equal(0, _tiny.Services.GetRequiredService<SolveWorkerPool>().LiveWorkers);
    }
}
