using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace API.Interfaces;

/// <summary>What became of a solve, in this process or in a worker process (#936).</summary>
internal enum SolveOutcomeKind {
    /// <summary>The solver answered (<see cref="SolveOutcome.Answer"/>).</summary>
    Answered,
    /// <summary>The solver stopped itself at the time limit.</summary>
    Stopped,
    /// <summary>The input was rejected: a parse error or an exact-search limit, with the 400 <see cref="SolveOutcome.Body"/>.</summary>
    Rejected,
    /// <summary>The solver's outside service failed (<see cref="SolveOutcome.Unavailable"/>, HTTP 503).</summary>
    Unavailable,
    /// <summary>The solver threw an unexpected exception (<see cref="SolveOutcome.Detail"/>), HTTP 500 as in this process.</summary>
    Failed,
    /// <summary>The solver asked for more memory than the worker's heap limit allows.</summary>
    OutOfMemory,
    /// <summary>The worker did not stop at the time limit, so the server killed it.</summary>
    Killed,
    /// <summary>The worker grew past the memory limit, so the server killed it.</summary>
    KilledForMemory,
    /// <summary>The worker died, or could not be started, without answering (<see cref="SolveOutcome.Detail"/>).</summary>
    Crashed,
}

/// <summary>
/// The outcome of a solve: <see cref="ProblemProvider.SolveCore"/> makes the first three kinds in whichever process runs
/// the solver, and <see cref="SolveWorkerPool"/> adds the ones only a worker process can have.
/// </summary>
internal sealed record SolveOutcome(SolveOutcomeKind Kind) {
    /// <summary>The solver's answer, for <see cref="SolveOutcomeKind.Answered"/>.</summary>
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
/// An unexpected exception thrown by a solver in a worker process, thrown again in the server so the request ends with
/// HTTP 500, as it does when the same solver throws in the server itself. The message is the worker's exception, in full.
/// </summary>
internal sealed class ComputeWorkerException : Exception {
    public ComputeWorkerException(string detail) : base(detail) { }
}

/// <summary>A job sent to a worker process: the solver (a key of <see cref="ProblemProvider.Solvers"/>), the instance, and the time limit.</summary>
internal sealed record WorkerJob(string Solver, string Instance, long? TimeLimitTicks);

/// <summary>The one reply of a worker process to its <see cref="WorkerJob"/>.</summary>
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
/// The worker mode of the API binary (<c>dotnet API.dll --compute-worker</c>), which runs exactly one solve for
/// <see cref="SolveWorkerPool"/> (#936). Program.cs checks for <see cref="Argument"/> before it builds the web host.
/// <para>
/// The protocol is one JSON line each way on the standard streams. The worker loads the problem and solver registry,
/// sets up what the solvers need (the quantum service settings, from the environment the server passes), and writes
/// <see cref="ReadyLine"/>; the server keeps it waiting like that until a solve comes in. Then it reads one
/// <see cref="WorkerJob"/>, runs <see cref="ProblemProvider.SolveCore"/> as the server would, writes one
/// <see cref="WorkerReply"/>, and exits. Anything else a solver writes to the console goes to standard error, so
/// standard output carries only the protocol.
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
        // Solvers may write to the console; keep standard output for the protocol.
        Console.SetOut(Console.Error);

        // Pay the start-up costs now, before the job comes in: the registry scan, the quantum settings, and the
        // protocol's JSON code.
        _ = ProblemProvider.Solvers.Count;
        SetUpQuantumService();
        _ = JsonSerializer.Serialize(JsonSerializer.Deserialize<WorkerJob>("{\"solver\":\"\",\"instance\":\"\"}", WireOptions), WireOptions);
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
            var outcome = ProblemProvider.SolveCore(job.Solver, job.Instance, limit);
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
            // The heap limit (DOTNET_GCHeapHardLimit) stopped the solver; what it allocated is garbage by now.
            return new WorkerReply(SolveOutcomeKind.OutOfMemory);
        } catch (Exception ex) {
            return new WorkerReply(SolveOutcomeKind.Failed) { Detail = ex.ToString() };
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
