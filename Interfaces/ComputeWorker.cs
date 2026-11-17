using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace API.Interfaces;

/// <summary>
/// What became of a compute job (a solve, or since #991 the work of any compute endpoint), in this process or in a
/// worker process (#936).
/// </summary>
internal enum SolveOutcomeKind {
    /// <summary>The job answered (<see cref="SolveOutcome.Answer"/>).</summary>
    Answered,
    /// <summary>The solver stopped itself at the time limit (a solve only).</summary>
    Stopped,
    /// <summary>The input was rejected: a parse error or an exact-search limit, with the 400 <see cref="SolveOutcome.Body"/>.</summary>
    Rejected,
    /// <summary>The solver's outside service failed (<see cref="SolveOutcome.Unavailable"/>, HTTP 503).</summary>
    Unavailable,
    /// <summary>The job threw an unexpected exception (<see cref="SolveOutcome.Detail"/>), HTTP 500 as in this process.</summary>
    Failed,
    /// <summary>The job asked for more memory than the worker's heap limit allows.</summary>
    OutOfMemory,
    /// <summary>The worker did not stop at the time limit, so the server killed it.</summary>
    Killed,
    /// <summary>The worker grew past the memory limit, so the server killed it.</summary>
    KilledForMemory,
    /// <summary>The worker died, or could not be started, without answering (<see cref="SolveOutcome.Detail"/>).</summary>
    Crashed,
}

/// <summary>
/// The outcome of a compute job: <see cref="ProblemProvider.SolveCore"/> and <see cref="ProblemProvider.RunJob"/> make
/// the first three kinds in whichever process runs the job, and <see cref="SolveWorkerPool"/> adds the ones only a
/// worker process can have.
/// </summary>
internal sealed record SolveOutcome(SolveOutcomeKind Kind) {
    /// <summary>
    /// For <see cref="SolveOutcomeKind.Answered"/>: the solver's answer of a solve, or the finished response body of any
    /// other job (<see cref="ProblemProvider.RunJob"/>), which the server sends as it is.
    /// </summary>
    public string? Answer { get; init; }
    /// <summary>The 400 body, for <see cref="SolveOutcomeKind.Rejected"/> (an anonymous object here, JSON from a worker).</summary>
    public object? Body { get; init; }
    /// <summary>The exception to answer 503 with, for <see cref="SolveOutcomeKind.Unavailable"/>.</summary>
    public SolverUnavailableException? Unavailable { get; init; }
    /// <summary>What went wrong, for <see cref="SolveOutcomeKind.Failed"/> and <see cref="SolveOutcomeKind.Crashed"/>.</summary>
    public string? Detail { get; init; }

    public static readonly SolveOutcome Stopped = new(SolveOutcomeKind.Stopped);
    public static SolveOutcome Answered(string answer) => new(SolveOutcomeKind.Answered) { Answer = answer };
    public static SolveOutcome Rejected(object body) => new(SolveOutcomeKind.Rejected) { Body = body };
}

/// <summary>
/// An unexpected exception thrown by a job in a worker process, thrown again in the server so the request ends with
/// HTTP 500, as it does when the same code throws in the server itself. The message is the worker's exception, in full.
/// </summary>
internal sealed class ComputeWorkerException : Exception {
    public ComputeWorkerException(string detail) : base(detail) { }
}

/// <summary>The compute endpoint whose work a <see cref="WorkerJob"/> runs (#936, #991).</summary>
internal enum WorkerJobKind {
    /// <summary><c>solve</c>: <see cref="WorkerJob.Solver"/> on the instance.</summary>
    Solve,
    /// <summary><c>verify</c>: <see cref="WorkerJob.Verifier"/> on the instance and the certificate (<see cref="WorkerJob.Solution"/>).</summary>
    Verify,
    /// <summary><c>reduce</c>: <see cref="WorkerJob.Reduction"/> of the instance.</summary>
    Reduce,
    /// <summary><c>mapSolution</c>: <see cref="WorkerJob.Solution"/> mapped through <see cref="WorkerJob.Reduction"/> of the instance.</summary>
    MapSolution,
    /// <summary><c>mapSolutionBack</c>: the target's <see cref="WorkerJob.Solution"/> mapped back, with <see cref="WorkerJob.Steps"/> or not.</summary>
    MapSolutionBack,
    /// <summary><c>gadgets</c>: the gadget map of <see cref="WorkerJob.Reduction"/> of the instance.</summary>
    Gadgets,
    /// <summary><c>visualize</c>: <see cref="WorkerJob.Visualization"/> of the instance, with <see cref="WorkerJob.Solver"/> or its own solver.</summary>
    Visualize,
    /// <summary><c>visualizeReduction</c>: the chain <see cref="WorkerJob.Reduction"/> (dash-separated) with <see cref="WorkerJob.Solution"/>.</summary>
    VisualizeReduction,
}

/// <summary>
/// A job sent to a worker process: which endpoint's work it is (<see cref="Kind"/>), the instance, and that endpoint's
/// arguments, as the server checked them. Names are keys of the <see cref="ProblemProvider"/> registries (any case).
/// </summary>
internal sealed record WorkerJob(WorkerJobKind Kind, string Instance) {
    /// <summary>The solver of a solve, or the solver asked for by a visualize (null or empty: the visualization's own).</summary>
    public string? Solver { get; init; }
    /// <summary>The verifier of a verify.</summary>
    public string? Verifier { get; init; }
    /// <summary>The reduction, or for a visualizeReduction the dash-separated chain of them.</summary>
    public string? Reduction { get; init; }
    /// <summary>The visualization of a visualize.</summary>
    public string? Visualization { get; init; }
    /// <summary>The certificate of a verify, or the solution of a mapSolution, mapSolutionBack or visualizeReduction.</summary>
    public string? Solution { get; init; }
    /// <summary>For a visualize: format=frames rather than the list format.</summary>
    public bool AsFrames { get; init; }
    /// <summary>For reduce and visualizeReduction: the includePictures query parameter.</summary>
    public bool IncludePictures { get; init; }
    /// <summary>For mapSolutionBack: the steps query parameter.</summary>
    public bool Steps { get; init; }
    /// <summary>The time limit the solver of a solve or a visualize stops itself at; null for none.</summary>
    public long? TimeLimitTicks { get; init; }
    /// <summary>For tests only (<see cref="SolveWorkerPool.TestFault"/>): makes the worker misbehave on purpose.</summary>
    public string? TestFault { get; init; }

    /// <summary>The job of a solve.</summary>
    public static WorkerJob ForSolve(string solver, string instance, TimeSpan? limit) =>
        new(WorkerJobKind.Solve, instance) { Solver = solver, TimeLimitTicks = limit?.Ticks };
}

/// <summary>
/// The one reply of a worker process to its <see cref="WorkerJob"/>. <see cref="Answer"/> is the solver's answer for a
/// solve, and the finished response body for every other kind of job.
/// </summary>
internal sealed record WorkerReply(SolveOutcomeKind Kind) {
    public string? Answer { get; init; }
    public JsonElement? Body { get; init; }
    public string? Detail { get; init; }
    // For SolveOutcomeKind.Unavailable: what the SolverUnavailableException carried.
    public string? Solver { get; init; }
    public string? SolverName { get; init; }
    public string? ProblemName { get; init; }
}

/// <summary>
/// The worker mode of the API binary (<c>dotnet API.dll --compute-worker</c>), which runs exactly one compute job (a
/// solve, a verify, a reduction or a visualization) for <see cref="SolveWorkerPool"/> (#936, #991). Program.cs checks
/// for <see cref="Argument"/> before it builds the web host.
/// <para>
/// The protocol is one JSON line each way on the standard streams. The worker loads the problem and solver registry,
/// sets up what the solvers need (the quantum service settings, from the environment the server passes), and writes
/// <see cref="ReadyLine"/>; the server keeps it waiting like that until a job comes in. Then it reads one
/// <see cref="WorkerJob"/>, runs the code the endpoint runs in the server (<see cref="ProblemProvider.SolveCore"/> for a
/// solve, <see cref="ProblemProvider.RunJob"/> for the others), writes one <see cref="WorkerReply"/>, and exits. For
/// every endpoint but solve the reply carries the finished response body, so the server sends exactly the text the
/// in-process path would; it can be large (the steps of a visualization) and travels as one line however long it is.
/// Anything else the job writes to the console goes to standard error, so standard output carries only the protocol.
/// </para>
/// <para>
/// The worker exits as soon as its standard input closes, so a worker whose server has gone away (crashed or killed)
/// does not run on.
/// </para>
/// </summary>
internal static class ComputeWorker {
    /// <summary>The command-line argument that starts the binary as a worker.</summary>
    public const string Argument = "--compute-worker";

    /// <summary>The line a worker writes once it is ready for its job.</summary>
    public const string ReadyLine = "redux-compute-worker ready";

    /// <summary>The JSON options of the protocol, both ways.</summary>
    internal static readonly JsonSerializerOptions WireOptions = new(JsonSerializerDefaults.Web) {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // The options MVC writes a response body with by default, so a 400 body built here reads exactly as the server's.
    private static readonly JsonSerializerOptions BodyOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Runs the worker: ready, one job, one reply. Returns the process exit code.</summary>
    public static int Run() {
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var protocol = new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true };
        var input = new StreamReader(Console.OpenStandardInput(), utf8);
        // Jobs may write to the console; keep standard output for the protocol.
        Console.SetOut(Console.Error);

        // Pay the start-up costs now, before the job comes in: the registry scan, the quantum settings, and the
        // protocol's JSON code.
        _ = ProblemProvider.Solvers.Count;
        SetUpQuantumService();
        _ = JsonSerializer.Serialize(JsonSerializer.Deserialize<WorkerJob>("{\"kind\":\"Solve\",\"instance\":\"\",\"solver\":\"\"}", WireOptions), WireOptions);
        _ = JsonSerializer.Serialize(new WorkerReply(SolveOutcomeKind.Answered) { Answer = "" }, WireOptions);

        protocol.WriteLine(ReadyLine);
        string? line = input.ReadLine();
        if (line == null)
            return 0; // the server went away before sending a job
        var job = JsonSerializer.Deserialize<WorkerJob>(line, WireOptions)
            ?? throw new InvalidOperationException("The worker received an empty job.");

        // The server never writes again; the stream closes only when the server is gone, and then so is the job.
        var watch = new Thread(() => {
            try { input.ReadLine(); } catch { /* the stream broke: the server is gone too */ }
            Environment.Exit(2);
        }) { IsBackground = true, Name = "compute-worker stdin watch" };
        watch.Start();

        protocol.WriteLine(JsonSerializer.Serialize(Execute(job), WireOptions));
        return 0;
    }

    /// <summary>Runs one job and builds the reply. Every exception becomes a reply, so the server always hears back.</summary>
    internal static WorkerReply Execute(WorkerJob job) {
        TimeSpan? limit = job.TimeLimitTicks is { } ticks ? TimeSpan.FromTicks(ticks) : null;
        try {
            Misbehave(job.TestFault);
            var outcome = job.Kind == WorkerJobKind.Solve
                ? ProblemProvider.SolveCore(job.Solver ?? "", job.Instance, limit)
                : ProblemProvider.RunJob(job, limit);
            return outcome.Kind switch {
                SolveOutcomeKind.Answered => new WorkerReply(SolveOutcomeKind.Answered) { Answer = outcome.Answer },
                SolveOutcomeKind.Rejected => new WorkerReply(SolveOutcomeKind.Rejected) {
                    Body = JsonSerializer.SerializeToElement(outcome.Body, outcome.Body!.GetType(), BodyOptions),
                },
                _ => new WorkerReply(outcome.Kind),
            };
        } catch (Exception ex) when (SolverUnavailableException.Find(ex) is { } unavailable) {
            return new WorkerReply(SolveOutcomeKind.Unavailable) {
                Solver = unavailable.Solver,
                SolverName = unavailable.SolverName,
                ProblemName = unavailable.ProblemName,
                Detail = unavailable.Message,
            };
        } catch (Exception ex) when (IsOutOfMemory(ex)) {
            // The heap limit (DOTNET_GCHeapHardLimit) stopped the job; what it allocated is garbage by now.
            return new WorkerReply(SolveOutcomeKind.OutOfMemory);
        } catch (Exception ex) {
            return new WorkerReply(SolveOutcomeKind.Failed) { Detail = ex.ToString() };
        }
    }

    /// <summary>The <see cref="WorkerJob.TestFault"/> that never answers, like work that ignores the time limit.</summary>
    internal const string HangFault = "hang";
    /// <summary>The <see cref="WorkerJob.TestFault"/> that exits the process without answering, like a stack overflow.</summary>
    internal const string CrashFault = "crash";
    /// <summary>The <see cref="WorkerJob.TestFault"/> that allocates (and touches) memory until it is stopped.</summary>
    internal const string AllocateFault = "allocate";
    /// <summary>The <see cref="WorkerJob.TestFault"/> that throws an unexpected exception (<see cref="ThrowFaultMessage"/>).</summary>
    internal const string ThrowFault = "throw";
    /// <summary>The message of the exception <see cref="ThrowFault"/> throws.</summary>
    internal const string ThrowFaultMessage = "A test fault threw in the worker.";

    // For tests only: the faults a test reaches the kill, memory, crash and failure paths of every kind of job with,
    // where no honest input does it reliably.
    private static void Misbehave(string? fault) {
        switch (fault) {
            case null:
                return;
            case HangFault:
                Thread.Sleep(Timeout.Infinite);
                return;
            case CrashFault:
                Environment.Exit(3);
                return;
            case ThrowFault:
                throw new InvalidOperationException(ThrowFaultMessage);
            case AllocateFault:
                var held = new List<byte[]>();
                while (true) {
                    var block = new byte[16 * 1024 * 1024];
                    // Write every page, so the memory is really in the working set.
                    for (int i = 0; i < block.Length; i += 4096)
                        block[i] = 1;
                    held.Add(block);
                }
            default:
                throw new InvalidOperationException($"Unknown test fault \"{fault}\".");
        }
    }

    private static bool IsOutOfMemory(Exception? ex) {
        for (; ex != null; ex = ex.InnerException)
            if (ex is OutOfMemoryException)
                return true;
        return false;
    }

    // The quantum solvers call an HTTP service through QuantumSolverSettingsGlobal, which the web host sets in
    // Program.cs; here it is set from the environment, where the server put its own settings (QuantumSolver__BaseURL).
    private static void SetUpQuantumService() {
        var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        var services = new ServiceCollection();
        services.Configure<QuantumSolverSettings>(configuration.GetSection("QuantumSolver"));
        services.AddQuantumHttpClient();
        var provider = services.BuildServiceProvider();
        QuantumSolverSettingsGlobal.QuantumSolver = provider.GetRequiredService<IOptionsMonitor<QuantumSolverSettings>>().CurrentValue;
        QuantumSolverSettingsGlobal.HttpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
    }
}
