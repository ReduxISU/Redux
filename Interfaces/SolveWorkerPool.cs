using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace API.Interfaces;

/// <summary>
/// Runs each solve in a worker process of its own (#936), so the time limit really cancels it and a memory limit can
/// stop it. On when <see cref="ComputeLimits.IsolateSolvesKey"/> is (the default); off, <see cref="Enabled"/> is false,
/// no process is started, and the solve runs in the server as before.
/// <para>
/// The pool keeps <see cref="Size"/> workers started ahead of time, each already past .NET start-up and the registry
/// load (<see cref="ComputeWorker"/>), so a solve does not pay for them. A worker serves one solve and then exits, and a
/// new one is started in the background to take its place; a worker that dies while it waits is replaced too. When no
/// warm worker is ready (at start-up, or in a burst), one is started for the request.
/// </para>
/// <para>
/// The limits: the server waits for the reply up to the time limit plus <see cref="ComputeLimits.SolverBackstopGrace"/>
/// (the solver's own stop at the limit answers first), then kills the worker. The worker's managed heap is capped at
/// <see cref="ComputeLimits.SolveMemoryKey"/> (<c>DOTNET_GCHeapHardLimit</c>), and the server also kills a worker whose
/// working set passes that limit plus <see cref="MemoryAllowanceMB"/>. A client that goes away kills its worker too.
/// The request keeps its slot of <see cref="ComputeJobLimiter"/> while it waits, and a killed worker leaves nothing
/// running, so the slot is free as soon as the request answers.
/// </para>
/// </summary>
public sealed class SolveWorkerPool : IDisposable {
    /// <summary>
    /// How far past the memory limit, in megabytes, a worker's working set may go before the server kills it. The heap
    /// limit stops the solver's own allocations; this covers what the heap limit does not count: the runtime, the
    /// loaded code, and native memory (an idle worker has about 62 MB resident in the Linux Docker image, and 43 MB of
    /// working set on Windows), with room to spare, so a solver that stays under the heap limit is not killed for its runtime's overhead.
    /// </summary>
    public const int MemoryAllowanceMB = 128;

    /// <summary>How often the server reads a busy worker's working set.</summary>
    public static readonly TimeSpan MemoryPollInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>How long a worker may take to start before the server gives up on it.</summary>
    public static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(60);

    /// <summary>The <c>error</c> code of a solve whose worker process died without answering.</summary>
    public const string WorkerFailedError = "worker_failed";

    // For tests only: false leaves out DOTNET_GCHeapHardLimit, so only the server's watch on the working set enforces
    // the memory limit and a test can reach it.
    internal const string HeapHardLimitKey = "ComputeLimits:Testing:WorkerHeapHardLimit";

    private readonly ILogger<SolveWorkerPool> _logger;
    private readonly IOptionsMonitor<QuantumSolverSettings> _quantum;
    private readonly string _contentRoot;
    private readonly bool _heapHardLimit;
    private readonly object _lock = new();
    private readonly List<Worker> _idle = new();
    private readonly ConcurrentDictionary<Worker, byte> _live = new();
    private readonly CancellationTokenSource _stopping = new();
    private int _starting;
    private int _startFailures;
    private bool _started;
    private bool _disposed;
    private int _lastJobPid;
    private volatile bool _refillPaused;

    /// <summary>Reads the settings; no process starts until <see cref="Start"/>.</summary>
    public SolveWorkerPool(IConfiguration configuration, ComputeJobLimiter limiter, IHostEnvironment environment,
                           IOptionsMonitor<QuantumSolverSettings> quantum, ILogger<SolveWorkerPool> logger) {
        _logger = logger;
        _quantum = quantum;
        _contentRoot = environment.ContentRootPath;
        Enabled = ComputeLimits.IsolateSolves(configuration);
        // The cap on jobs running at once already bounds the solves running at once, so more warm workers would idle.
        Size = limiter.MaxJobs ?? Math.Max(2, Environment.ProcessorCount);
        MemoryLimitMB = ComputeLimits.SolveMemoryMB(configuration);
        _heapHardLimit = configuration.GetValue<bool?>(HeapHardLimitKey) ?? true;
    }

    /// <summary>Whether solves run in worker processes (<see cref="ComputeLimits.IsolateSolvesKey"/>).</summary>
    public bool Enabled { get; }

    /// <summary>How many warm workers the pool keeps: <see cref="ComputeJobLimiter.MaxJobs"/>, or the number of processors (at least 2) when the cap is off.</summary>
    public int Size { get; }

    /// <summary>The memory limit of a solve, in megabytes, or null for none (<see cref="ComputeLimits.SolveMemoryKey"/>).</summary>
    public int? MemoryLimitMB { get; }

    /// <summary>The worker processes alive now, warm or busy.</summary>
    internal int LiveWorkers => _live.Count;

    /// <summary>The process ids of the worker processes alive now.</summary>
    internal IReadOnlyList<int> LivePids() => _live.Keys.Select(w => w.Pid).Where(pid => pid != 0).ToList();

    /// <summary>The warm workers waiting for a solve.</summary>
    internal int IdleWorkers { get { lock (_lock) return _idle.Count; } }

    /// <summary>The process id of the worker that took the latest solve, for tests that check it is gone afterwards.</summary>
    internal int LastJobPid => Volatile.Read(ref _lastJobPid);

    /// <summary>The workers being started to become warm.</summary>
    internal int StartingWorkers { get { lock (_lock) return _starting; } }

    /// <summary>
    /// For tests only: while true, the pool starts no warm workers, so a test can run it out of them; setting it back to
    /// false starts the ones missing.
    /// </summary>
    internal bool RefillPaused {
        get => _refillPaused;
        set {
            _refillPaused = value;
            if (!value)
                Refill();
        }
    }

    /// <summary>Starts the warm workers (from <see cref="SolveWorkerPoolService"/>); does nothing when the pool is off.</summary>
    public void Start() {
        lock (_lock) {
            if (!Enabled || _started || _disposed)
                return;
            _started = true;
        }
        var (file, arguments) = WorkerCommand();
        _logger.LogInformation("Solves run in worker processes: {Size} warm, memory limit {MemoryMB} MB, started as {File} {Arguments}.",
            Size, MemoryLimitMB?.ToString() ?? "none", file, string.Join(' ', arguments));
        Refill();
    }

    /// <summary>
    /// Runs one solve in a worker and returns its outcome; see the class remarks for the limits. Cancelling
    /// <paramref name="aborted"/> (the client went away) kills the worker and throws <see cref="OperationCanceledException"/>.
    /// </summary>
    internal async Task<SolveOutcome> RunAsync(string solver, string instance, TimeSpan? limit, CancellationToken aborted) {
        Worker? worker = TakeIdle();
        Refill();
        if (worker == null) {
            try {
                worker = await StartWorkerAsync(aborted).ConfigureAwait(false);
            } catch (OperationCanceledException) when (aborted.IsCancellationRequested) {
                throw;
            } catch (Exception ex) {
                _logger.LogError(ex, "Could not start a worker process for a solve with {Solver}.", solver);
                return Crashed($"The server could not start a worker process for the solve: {ex.Message}");
            }
        }
        Volatile.Write(ref _lastJobPid, worker.Pid);

        using var done = CancellationTokenSource.CreateLinkedTokenSource(aborted);
        bool answered = false;
        try {
            string job = JsonSerializer.Serialize(new WorkerJob(solver, instance, limit?.Ticks), ComputeWorker.WireOptions);
            try {
                await worker.Process.StandardInput.WriteLineAsync(job.AsMemory(), done.Token).ConfigureAwait(false);
                await worker.Process.StandardInput.FlushAsync(done.Token).ConfigureAwait(false);
            } catch (IOException) {
                // The worker died after it was taken; the checks below report how.
            }

            Task<string?> reply = Observed(worker.Process.StandardOutput.ReadLineAsync(done.Token).AsTask());
            Task exited = Observed(worker.Process.WaitForExitAsync(done.Token));
            Task overMemory = MemoryLimitMB is { } mb
                ? Observed(WatchMemoryAsync(worker, (long)(mb + MemoryAllowanceMB) * 1024 * 1024, done.Token))
                : Observed(Task.Delay(Timeout.Infinite, done.Token));
            Task deadline = Observed(limit is { } l
                ? Task.Delay(l + ComputeLimits.SolverBackstopGrace, done.Token)
                : Task.Delay(Timeout.Infinite, done.Token));

            Task first = await Task.WhenAny(reply, exited, overMemory, deadline).ConfigureAwait(false);
            if (aborted.IsCancellationRequested) {
                worker.Kill();
                aborted.ThrowIfCancellationRequested();
            }
            // A worker exits right after its reply, so the exit can be seen first; the reply is still in the pipe.
            if (first == exited && !reply.IsCompleted)
                await Task.WhenAny(reply, Task.Delay(TimeSpan.FromSeconds(1), CancellationToken.None)).ConfigureAwait(false);

            if (reply.IsCompletedSuccessfully && reply.Result != null) {
                answered = true;
                return Read(reply.Result, worker);
            }
            if (first == overMemory && overMemory.IsCompletedSuccessfully) {
                worker.Kill();
                return new SolveOutcome(SolveOutcomeKind.KilledForMemory);
            }
            if (first == deadline && deadline.IsCompletedSuccessfully) {
                worker.Kill();
                return new SolveOutcome(SolveOutcomeKind.Killed);
            }
            worker.Kill();
            string code = worker.Process.HasExited ? worker.Process.ExitCode.ToString() : "unknown";
            _logger.LogError("The worker process for a solve with {Solver} exited with code {Code} before it answered. Its last output: {Output}",
                solver, code, worker.ErrorTail());
            return Crashed($"The worker process running the solve exited (code {code}) before it answered.");
        } finally {
            done.Cancel();
            // A worker that answered exits by itself. Any other way out (the client going away while the job is being
            // written, say, or anything else that throws) kills it here, so it is not left running after the request
            // gives back its job slot.
            if (!answered)
                worker.Kill();
            Retire(worker);
        }
    }

    /// <summary>The 500 body of a solve whose worker died without answering, in the error contract of #933.</summary>
    internal static object WorkerFailedBody(string solver, string? detail) => new Dictionary<string, object?> {
        ["error"] = WorkerFailedError,
        ["message"] = "The solve stopped unexpectedly, so there is no answer.",
        ["detail"] = detail ?? "The worker process running the solve stopped before it answered.",
        ["solver"] = solver,
        ["hint"] = "Try again. If it keeps happening, report the solver and the instance.",
    };

    private static SolveOutcome Crashed(string detail) => new(SolveOutcomeKind.Crashed) { Detail = detail };

    private SolveOutcome Read(string line, Worker worker) {
        WorkerReply? reply;
        try {
            reply = JsonSerializer.Deserialize<WorkerReply>(line, ComputeWorker.WireOptions);
        } catch (JsonException ex) {
            _logger.LogError(ex, "A solve worker sent a reply the server cannot read: {Line}", line);
            return Crashed("The worker process running the solve sent a reply the server cannot read.");
        }
        return reply?.Kind switch {
            SolveOutcomeKind.Answered => new SolveOutcome(SolveOutcomeKind.Answered) { Answer = reply.Answer },
            SolveOutcomeKind.Stopped => SolveOutcome.Stopped,
            SolveOutcomeKind.Rejected when reply.Body is { } body => SolveOutcome.Rejected(body),
            SolveOutcomeKind.Unavailable => new SolveOutcome(SolveOutcomeKind.Unavailable) {
                Unavailable = new SolverUnavailableException(reply.Solver ?? "", reply.SolverName ?? "", reply.ProblemName ?? "", reply.Detail ?? ""),
            },
            SolveOutcomeKind.OutOfMemory => new SolveOutcome(SolveOutcomeKind.OutOfMemory),
            SolveOutcomeKind.Failed => new SolveOutcome(SolveOutcomeKind.Failed) { Detail = reply.Detail },
            _ => Crashed("The worker process running the solve sent a reply the server cannot read."),
        };
    }

    // Completes when the worker's working set passes killAt; never completes otherwise (cancel it).
    private static async Task WatchMemoryAsync(Worker worker, long killAt, CancellationToken cancel) {
        using var timer = new PeriodicTimer(MemoryPollInterval);
        while (await timer.WaitForNextTickAsync(cancel).ConfigureAwait(false)) {
            long used;
            try {
                worker.Process.Refresh();
                used = worker.Process.WorkingSet64;
            } catch (InvalidOperationException) {
                break; // it exited
            }
            if (used > killAt)
                return;
        }
        await Task.Delay(Timeout.Infinite, cancel).ConfigureAwait(false);
    }

    // A task the server may stop waiting for: a later failure (a read from a killed worker) is observed, not reported.
    private static T Observed<T>(T task) where T : Task {
        task.ContinueWith(t => _ = t.Exception, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return task;
    }

    private Worker? TakeIdle() {
        lock (_lock) {
            while (_idle.Count > 0) {
                Worker worker = _idle[0];
                _idle.RemoveAt(0);
                if (!worker.Process.HasExited)
                    return worker;
                Forget(worker);
            }
        }
        return null;
    }

    // Starts workers in the background until the warm and starting ones make Size. Busy workers are not counted, so the
    // next solve finds a warm worker even when every job slot is busy: at the worst, Size warm or starting workers live
    // beside one busy worker per job slot (Documentation/production.md counts their memory).
    private void Refill() {
        int toStart;
        lock (_lock) {
            if (!_started || _disposed || _refillPaused)
                return;
            toStart = Size - _idle.Count - _starting;
            if (toStart <= 0)
                return;
            _starting += toStart;
        }
        for (int i = 0; i < toStart; i++)
            _ = WarmOneAsync();
    }

    private async Task WarmOneAsync() {
        Worker worker;
        try {
            worker = await StartWorkerAsync(_stopping.Token).ConfigureAwait(false);
        } catch (Exception ex) {
            int failures;
            lock (_lock) {
                _starting--;
                failures = ++_startFailures;
            }
            if (_stopping.IsCancellationRequested)
                return;
            _logger.LogError(ex, "Could not start a warm worker process for solves.");
            // Back off, so a worker that cannot start is not restarted in a tight loop: 1 s, 2 s, 4 s, ... up to a minute.
            try {
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(60, 1 << Math.Min(failures - 1, 6))), _stopping.Token).ConfigureAwait(false);
            } catch (OperationCanceledException) {
                return;
            }
            Refill();
            return;
        }
        bool keep;
        lock (_lock) {
            _starting--;
            _startFailures = 0;
            keep = !_disposed && !worker.Process.HasExited;
            if (keep)
                _idle.Add(worker);
        }
        if (!keep) {
            worker.Kill();
            Forget(worker);
            Refill();
        }
    }

    private async Task<Worker> StartWorkerAsync(CancellationToken cancel) {
        var (file, arguments) = WorkerCommand();
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var start = new ProcessStartInfo(file) {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = utf8,
            StandardOutputEncoding = utf8,
            StandardErrorEncoding = utf8,
            WorkingDirectory = _contentRoot,
        };
        foreach (string argument in arguments)
            start.ArgumentList.Add(argument);
        // One small heap: the server GC the API's runtimeconfig asks for keeps a heap per processor in every worker.
        start.Environment["DOTNET_gcServer"] = "0";
        if (MemoryLimitMB is { } mb && _heapHardLimit)
            start.Environment["DOTNET_GCHeapHardLimit"] = ((long)mb * 1024 * 1024).ToString("X");
        else
            start.Environment.Remove("DOTNET_GCHeapHardLimit");
        // The worker reads the quantum service settings from its environment (see ComputeWorker).
        start.Environment["QuantumSolver__BaseURL"] = _quantum.CurrentValue.BaseURL;

        var process = new Process { StartInfo = start, EnableRaisingEvents = true };
        var worker = new Worker(process);
        process.ErrorDataReceived += (_, e) => worker.AddError(e.Data);
        process.Exited += (_, _) => OnExited(worker);
        _live[worker] = 0;
        try {
            process.Start();
        } catch {
            _live.TryRemove(worker, out _);
            process.Dispose();
            throw;
        }
        worker.Pid = process.Id;
        if (process.HasExited)
            _live.TryRemove(worker, out _);
        bool disposed;
        lock (_lock)
            disposed = _disposed;
        if (disposed) {
            worker.Kill();
            Forget(worker);
            throw new ObjectDisposedException(nameof(SolveWorkerPool));
        }
        process.BeginErrorReadLine();

        using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        wait.CancelAfter(StartTimeout);
        string? line;
        try {
            line = await process.StandardOutput.ReadLineAsync(wait.Token).ConfigureAwait(false);
        } catch (OperationCanceledException) {
            worker.Kill();
            Forget(worker);
            if (cancel.IsCancellationRequested)
                throw;
            throw new TimeoutException($"The worker process did not start within {StartTimeout.TotalSeconds} seconds.");
        }
        if (line != ComputeWorker.ReadyLine) {
            worker.Kill();
            string output = worker.ErrorTail();
            Forget(worker);
            throw new InvalidOperationException(
                $"The worker process did not start: {(line == null ? "it exited" : $"it wrote \"{line}\"")}. Its last output: {output}");
        }
        return worker;
    }

    private void OnExited(Worker worker) {
        _live.TryRemove(worker, out _);
        bool wasIdle;
        lock (_lock)
            wasIdle = _idle.Remove(worker);
        if (!wasIdle)
            return;
        _logger.LogWarning("A warm solve worker (process {Pid}) exited while it waited; starting another. Its last output: {Output}",
            worker.Pid, worker.ErrorTail());
        Forget(worker);
        Refill();
    }

    // After a solve: a worker that answered exits by itself; one that has not exited within a few seconds is killed.
    private void Retire(Worker worker) {
        _ = Task.Run(async () => {
            try {
                using var wait = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try {
                    await worker.Process.WaitForExitAsync(wait.Token).ConfigureAwait(false);
                } catch (OperationCanceledException) {
                    worker.Kill();
                }
            } finally {
                Forget(worker);
            }
        });
    }

    private void Forget(Worker worker) {
        _live.TryRemove(worker, out _);
        worker.Process.Dispose();
    }

    /// <summary>Kills every worker, warm or busy. Called when the host stops.</summary>
    public void Dispose() {
        List<Worker> all;
        lock (_lock) {
            if (_disposed)
                return;
            _disposed = true;
            _idle.Clear();
            all = _live.Keys.ToList();
        }
        _stopping.Cancel();
        foreach (Worker worker in all) {
            worker.Kill();
            try { worker.Process.WaitForExit(2000); } catch (Exception) { /* already gone or disposed */ }
        }
        _live.Clear();
        _stopping.Dispose();
    }

    /// <summary>
    /// The command that starts a worker: the API's own app host when the server was started through it (<c>dotnet
    /// run</c>, or a self-contained publish), else the <c>dotnet</c> host with the API assembly
    /// (<c>dotnet API.dll</c> in the Docker image, and the test host, whose process is the test runner).
    /// </summary>
    internal static (string File, string[] Arguments) WorkerCommand() {
        string assembly = typeof(Program).Assembly.Location;
        string? process = Environment.ProcessPath;
        if (process != null && Path.GetFileNameWithoutExtension(process)
                .Equals(Path.GetFileNameWithoutExtension(assembly), StringComparison.OrdinalIgnoreCase))
            return (process, new[] { ComputeWorker.Argument });
        return (DotnetHost(process), new[] { assembly, ComputeWorker.Argument });
    }

    /// <summary>
    /// The <c>dotnet</c> host: this process when it is the host, else <c>DOTNET_HOST_PATH</c> (set by the SDK for
    /// <c>dotnet test</c> and <c>dotnet run</c>), else the host beside the running shared runtime, else <c>dotnet</c> on the PATH.
    /// </summary>
    internal static string DotnetHost(string? process) {
        string exe = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
        if (process != null && Path.GetFileName(process).Equals(exe, StringComparison.OrdinalIgnoreCase))
            return process;
        if (Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } host && File.Exists(host))
            return host;
        // The shared runtime is <dotnet root>/shared/Microsoft.NETCore.App/<version>/, and the host is in <dotnet root>.
        string? runtime = Path.GetDirectoryName(typeof(object).Assembly.Location);
        if (runtime != null && new DirectoryInfo(runtime).Parent?.Parent?.Parent is { } root
            && File.Exists(Path.Combine(root.FullName, exe)))
            return Path.Combine(root.FullName, exe);
        return exe;
    }

    // One worker process, as the pool sees it.
    private sealed class Worker {
        private const int TailLines = 20;
        private readonly Queue<string> _errorTail = new();

        public Worker(Process process) {
            Process = process;
        }

        public Process Process { get; }
        public int Pid { get; set; }

        public void AddError(string? line) {
            if (line == null)
                return;
            lock (_errorTail) {
                _errorTail.Enqueue(line);
                while (_errorTail.Count > TailLines)
                    _errorTail.Dequeue();
            }
        }

        public string ErrorTail() {
            lock (_errorTail)
                return _errorTail.Count == 0 ? "(none)" : string.Join(Environment.NewLine, _errorTail);
        }

        public void Kill() {
            try {
                Process.Kill(entireProcessTree: true);
            } catch (InvalidOperationException) {
                // It has exited, or never started.
            } catch (Win32Exception) {
                // It is exiting.
            }
        }
    }
}

/// <summary>Starts <see cref="SolveWorkerPool"/> with the host and kills its workers when the host stops.</summary>
public sealed class SolveWorkerPoolService : IHostedService {
    private readonly SolveWorkerPool _pool;

    /// <summary>The service for <paramref name="pool"/>.</summary>
    public SolveWorkerPoolService(SolveWorkerPool pool) {
        _pool = pool;
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken) {
        _pool.Start();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) {
        _pool.Dispose();
        return Task.CompletedTask;
    }
}
