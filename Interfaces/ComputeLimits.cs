namespace API.Interfaces;

/// <summary>
/// The time limit on compute requests (solve, verify, reduce, visualize and the other endpoints that run a reduction),
/// step 1 of the compute limits decided in #915 (#935).
/// <para>
/// Three mechanisms share the one configured limit:
/// <list type="bullet">
/// <item>Typed solvers (<see cref="ISolver{T, TPartial}"/>) are stopped by their <see cref="Steps.StepRecorder{TPartial}"/>
/// at its next call after the deadline, whether or not steps are recorded (<see cref="SolveRun.Record{TPartial}"/>).</item>
/// <item>Legacy solvers are stopped by the cooperative <see cref="ISolver.timerHasExpired"/> flag, which
/// <see cref="SolverTimer"/> sets from a thread-pool timer; each solver checks it in its main loops.</item>
/// <item>Everything else (verify, reduce, and any solver check that is missed or coarse) is covered by
/// <see cref="WithBackstop{T}"/>, which stops the server from waiting, not the work.</item>
/// </list>
/// None of these is true cancellation: a flag or a recorder check stops the work only at its next checkpoint, and the
/// backstop only stops waiting while the abandoned work runs on until it finishes.
/// </para>
/// <para>
/// Worker processes are the exception: when <see cref="IsolateSolvesKey"/> is on (the default), each compute job (a
/// solve since #936; verify, reduce, mapSolution, mapSolutionBack, gadgets, visualize and visualizeReduction since #991)
/// runs in a worker process of its own (<see cref="SolveWorkerPool"/>), which the server kills when the job does not
/// stop by itself at the limit, so the work really is cancelled (<see cref="CancelledDetail"/>,
/// <see cref="JobCancelledDetail"/>). The worker also has a memory limit (<see cref="SolveMemoryKey"/>). With the
/// setting off, every endpoint uses the backstop as before; real cancellation inside one process comes with #937.
/// </para>
/// </summary>
static class ComputeLimits {
    /// <summary>The configuration key for the time limit, in seconds, of every compute request.</summary>
    public const string TimeLimitKey = "ComputeLimits:TimeLimitSeconds";
    /// <summary>The limit used when <see cref="TimeLimitKey"/> is not set.</summary>
    public const double DefaultTimeLimitSeconds = 10;
    /// <summary>
    /// How much longer than the limit the backstop waits on a solve or visualize, whose solvers stop themselves at the
    /// limit: the solver's own stop answers first (for visualize, with the stopped picture), and the backstop only
    /// answers for a solver that does not stop.
    /// </summary>
    public static readonly TimeSpan SolverBackstopGrace = TimeSpan.FromSeconds(2);

    /// <summary>Seconds to a limit: zero or less means no limit.</summary>
    public static TimeSpan? FromSeconds(double seconds) => seconds > 0 ? TimeSpan.FromSeconds(seconds) : null;

    /// <summary>The limit from <see cref="TimeLimitKey"/> (default <see cref="DefaultTimeLimitSeconds"/>).</summary>
    public static TimeSpan? TimeLimit(IConfiguration? configuration) =>
        FromSeconds(configuration?.GetValue<double?>(TimeLimitKey) ?? DefaultTimeLimitSeconds);

    /// <summary>
    /// Runs <paramref name="work"/> on the thread pool and waits for it at most <paramref name="backstop"/>. Returns
    /// (true, result) when it finished in time, and rethrows its exception if it failed; returns (false, default) when
    /// the backstop ran out first. Then the work is NOT cancelled: it keeps running in the background until it finishes
    /// or reaches a checkpoint of its own (see the class remarks). With no backstop the work runs inline, as before.
    /// <para>
    /// <paramref name="job"/> is the request's slot in the cap on jobs running at once (#936). Work the backstop gives up
    /// on keeps that slot until it finishes, so abandoned work still counts toward the cap.
    /// </para>
    /// </summary>
    public static async Task<(bool Finished, T? Result)> WithBackstop<T>(Func<T> work, TimeSpan? backstop, ComputeJob? job = null) {
        if (backstop is not { } wait)
            return (true, work());
        var task = Task.Run(work);
        using var stopWaiting = new CancellationTokenSource();
        var first = await Task.WhenAny(task, Task.Delay(wait, stopWaiting.Token)).ConfigureAwait(false);
        if (first != task) {
            job?.FinishWhen(task);
            // Observe a later failure of the abandoned work, so it is not reported as an unobserved task exception.
            _ = task.ContinueWith(t => _ = t.Exception, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            return (false, default);
        }
        stopWaiting.Cancel();
        return (true, await task.ConfigureAwait(false));
    }

    /// <summary>
    /// The body of a request stopped by the time limit, in the error contract of #933 (as in #934's
    /// <c>solver_unavailable</c>): a stable <c>error</c> code, a plain <c>message</c>, the technical <c>detail</c>, context,
    /// and a <c>hint</c> saying what to try next. <paramref name="job"/> names what ran ("solve", "verification", ...).
    /// </summary>
    public static object TimeLimitBody(string job, TimeSpan limit, string detail, string hint, IReadOnlyDictionary<string, object?>? context = null) {
        string seconds = limit.TotalSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        var body = new Dictionary<string, object?> {
            ["error"] = TimeLimitError,
            ["message"] = $"The {job} ran past the {seconds}-second time limit, so it was stopped.",
            ["detail"] = detail,
            ["limitSeconds"] = limit.TotalSeconds,
        };
        if (context != null)
            foreach (var (key, value) in context)
                body[key] = value;
        body["hint"] = hint;
        return body;
    }

    /// <summary>The <c>error</c> code of a request stopped by the time limit.</summary>
    public const string TimeLimitError = "time_limit";

    /// <summary>The <c>detail</c> when a solver stopped itself at the limit.</summary>
    public const string StoppedBySolverDetail = "The solver checked the time limit and stopped before it finished; there is no answer.";

    /// <summary>The <c>detail</c> when the backstop answered: the work did not stop on its own.</summary>
    public const string BackstopDetail = "The server stopped waiting at the time limit. The computation was not cancelled and may run on for a while.";

    /// <summary>
    /// The <c>detail</c> when a solve in a worker process did not stop by itself at the limit, so the server killed the
    /// process: unlike <see cref="BackstopDetail"/>, nothing runs on.
    /// </summary>
    public const string CancelledDetail = "The solver did not stop at the time limit, so the server stopped the process running it. The computation was cancelled.";

    /// <summary>
    /// The detail when a job other than a solve or a visualize (a verify or a reduction) in a worker process did not
    /// finish by the limit, so the server killed the process. Those jobs have no time checks of their own.
    /// </summary>
    public const string JobCancelledDetail = "The computation did not finish by the time limit, so the server stopped the process running it. The computation was cancelled.";

    /// <summary>
    /// The configuration key that runs each compute job (a solve since #936, every compute endpoint since #991) in a
    /// worker process of its own, with real cancellation at the time limit and a memory limit. Default true; false runs
    /// every job inside the server, as before. The name is from when only solves moved.
    /// </summary>
    public const string IsolateSolvesKey = "ComputeLimits:IsolateSolves";

    /// <summary>Whether compute jobs run in worker processes (<see cref="IsolateSolvesKey"/>, default true).</summary>
    public static bool IsolateSolves(IConfiguration? configuration) =>
        configuration?.GetValue<bool?>(IsolateSolvesKey) ?? true;

    /// <summary>The configuration key for the memory limit, in megabytes, of a compute job (any endpoint's) run in a worker process.</summary>
    public const string SolveMemoryKey = "ComputeLimits:SolveMemoryMB";
    /// <summary>The memory limit used when <see cref="SolveMemoryKey"/> is not set.</summary>
    public const int DefaultSolveMemoryMB = 512;

    /// <summary>The memory limit from <see cref="SolveMemoryKey"/> (default <see cref="DefaultSolveMemoryMB"/>); zero or less means none.</summary>
    public static int? SolveMemoryMB(IConfiguration? configuration) {
        int mb = configuration?.GetValue<int?>(SolveMemoryKey) ?? DefaultSolveMemoryMB;
        return mb > 0 ? mb : null;
    }

    /// <summary>
    /// The body of a compute job stopped by the memory limit, in the same error contract as <see cref="TimeLimitBody"/>:
    /// <c>error</c>, a plain <c>message</c>, the technical <c>detail</c>, <c>limitMB</c>, context, and a <c>hint</c>.
    /// </summary>
    public static object MemoryLimitBody(string job, int limitMB, string detail, string hint, IReadOnlyDictionary<string, object?>? context = null) {
        var body = new Dictionary<string, object?> {
            ["error"] = MemoryLimitError,
            ["message"] = $"The {job} used more than the {limitMB} MB memory limit, so it was stopped.",
            ["detail"] = detail,
            ["limitMB"] = limitMB,
        };
        if (context != null)
            foreach (var (key, value) in context)
                body[key] = value;
        body["hint"] = hint;
        return body;
    }

    /// <summary>The <c>error</c> code of a solve stopped by the memory limit.</summary>
    public const string MemoryLimitError = "memory_limit";

    /// <summary>The <c>detail</c> when the solver asked for more memory than the worker's heap limit allows.</summary>
    public const string HeapLimitDetail = "The solver asked for more memory than the limit allows and was stopped; there is no answer.";

    /// <summary>The <c>detail</c> when the server saw the worker process grow past the limit and killed it.</summary>
    public const string MemoryWatchDetail = "The process running the solve grew past the memory limit, so the server stopped it; there is no answer.";

    /// <summary>The <see cref="HeapLimitDetail"/> of a verify or a reduction, which runs no solver.</summary>
    public const string JobHeapLimitDetail = "The computation asked for more memory than the limit allows and was stopped; there is no answer.";

    /// <summary>The <see cref="MemoryWatchDetail"/> of a verify or a reduction, which runs no solver.</summary>
    public const string JobMemoryWatchDetail = "The process running the computation grew past the memory limit, so the server stopped it; there is no answer.";
}

/// <summary>
/// Starts the cooperative timer of a legacy solver: <see cref="ISolver.ResetTimer"/> now, and
/// <see cref="ISolver.TimerExpired"/> from a <see cref="System.Threading.Timer"/> when the limit runs out.
/// <para>
/// This replaces the implementation turned off in 0ea080fc, which ran every solve on a new thread and blocked the
/// request thread in <c>Thread.Join</c> (twice past the limit, the second time with no bound), so each request held two
/// threads and could starve the pool. Here the solve runs on the request's own thread and the timer holds no thread:
/// its callback only sets a flag. A zero or negative limit sets the flag at once.
/// </para>
/// <para>
/// The flag lives on the solver instance. Every endpoint makes a new solver for each request (ProblemProvider.Solver,
/// and a new visualization, whose solver is its own instance field), so two requests never share a flag.
/// </para>
/// </summary>
static class SolverTimer {
    private sealed class NoTimer : IDisposable {
        public static readonly NoTimer Instance = new();
        public void Dispose() { }
    }

    /// <summary>Starts the timer of <paramref name="solver"/>; disposing the result stops it. No limit, no timer.</summary>
    public static IDisposable Start(ISolver solver, TimeSpan? limit) {
        if (limit is not { } wait)
            return NoTimer.Instance;
        solver.ResetTimer();
        if (wait <= TimeSpan.Zero) {
            solver.TimerExpired();
            return NoTimer.Instance;
        }
        return new Timer(static state => ((ISolver)state!).TimerExpired(), solver, wait, Timeout.InfiniteTimeSpan);
    }
}
