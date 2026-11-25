using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace API.Interfaces;

/// <summary>
/// The cap on compute jobs running at once, step 2 of the compute limits decided in #915 (#936). Every endpoint marked
/// <see cref="ComputeJobAttribute"/> takes a slot for as long as its work runs; when every slot is taken, the request
/// is turned away at once with HTTP 429 and the <c>server_busy</c> body, so a crowded server says so right away
/// instead of leaving pages hanging. Cheap endpoints (info, navigation) are not counted.
/// <para>
/// A slot is held until the work itself finishes, not just until the response is sent: when the time-limit backstop
/// (<see cref="ComputeLimits.WithBackstop{T}"/>) stops waiting, the work it gave up on keeps running and keeps its slot
/// (<see cref="ComputeJob.FinishWhen"/>). So the cap counts what is really using the processor. A job in a worker
/// process (<see cref="SolveWorkerPool"/>) needs no hand-off: the request waits for its worker, and a worker that does
/// not stop is killed, so the slot is given back when the request answers. Since #991 that is every compute endpoint
/// when the worker pool is on, and only work left to the backstop (the pool off) hands its slot off.
/// </para>
/// </summary>
public sealed class ComputeJobLimiter {
    /// <summary>The configuration key for the number of compute jobs that may run at once.</summary>
    public const string MaxJobsKey = "ComputeLimits:MaxConcurrentJobs";

    /// <summary>The <c>Retry-After</c> header, in seconds, of a request turned away because the server is busy.</summary>
    public const int RetryAfterSeconds = 5;

    /// <summary>The <c>error</c> code of a request turned away because every slot is taken.</summary>
    public const string ServerBusyError = "server_busy";

    private int _running;

    /// <summary>
    /// The cap from <see cref="MaxJobsKey"/>; when it is not set, the number of processors (at least 2). Zero or less
    /// turns the cap off.
    /// </summary>
    public ComputeJobLimiter(IConfiguration? configuration) {
        int max = configuration?.GetValue<int?>(MaxJobsKey) ?? Math.Max(2, Environment.ProcessorCount);
        MaxJobs = max > 0 ? max : null;
    }

    /// <summary>The number of jobs that may run at once, or null for no cap.</summary>
    public int? MaxJobs { get; }

    /// <summary>The number of jobs holding a slot now, including work the backstop stopped waiting for.</summary>
    public int Running => Volatile.Read(ref _running);

    /// <summary>Takes a slot, or returns null when every slot is taken. Disposing the job gives the slot back.</summary>
    public ComputeJob? TryStart() {
        if (MaxJobs is not { } max)
            return new ComputeJob(null);
        while (true) {
            int running = Volatile.Read(ref _running);
            if (running >= max)
                return null;
            if (Interlocked.CompareExchange(ref _running, running + 1, running) == running)
                return new ComputeJob(this);
        }
    }

    internal void Finish() => Interlocked.Decrement(ref _running);

    /// <summary>The body of a request turned away because every slot is taken, in the error contract of #933.</summary>
    public object ServerBusyBody() => new Dictionary<string, object?> {
        ["error"] = ServerBusyError,
        ["message"] = "The server is busy with other jobs right now, so this one wasn't started.",
        ["detail"] = $"At most {MaxJobs} compute jobs can run at once, and all of them are in use.",
        ["maxConcurrentJobs"] = MaxJobs,
        ["retryAfterSeconds"] = RetryAfterSeconds,
        ["hint"] = $"Wait about {RetryAfterSeconds} seconds and try again.",
    };
}

/// <summary>
/// One slot of <see cref="ComputeJobLimiter"/>. The request that took it gives it back when it ends
/// (<see cref="Dispose"/>), unless its work was handed off to run on (<see cref="FinishWhen"/>); then the slot is given
/// back when that work finishes. The slot is given back only once either way.
/// </summary>
public sealed class ComputeJob : IDisposable {
    /// <summary>The <see cref="HttpContext.Items"/> key the current request's job is stored under.</summary>
    public static readonly object ItemKey = new();

    private readonly ComputeJobLimiter? _limiter;
    private int _released;
    private int _handedOff;

    internal ComputeJob(ComputeJobLimiter? limiter) {
        _limiter = limiter;
    }

    /// <summary>
    /// Keeps the slot until <paramref name="work"/> finishes, for work that runs on after the request has answered
    /// (the backstop stopped waiting for it). The request's own <see cref="Dispose"/> then leaves the slot alone.
    /// </summary>
    public void FinishWhen(Task work) {
        Interlocked.Exchange(ref _handedOff, 1);
        work.ContinueWith(static (_, state) => ((ComputeJob)state!).Release(), this, CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    /// <summary>Gives the slot back, unless the work was handed off with <see cref="FinishWhen"/>.</summary>
    public void Dispose() {
        if (Volatile.Read(ref _handedOff) == 0)
            Release();
    }

    private void Release() {
        if (Interlocked.Exchange(ref _released, 1) == 0)
            _limiter?.Finish();
    }
}

/// <summary>
/// Marks a compute endpoint: the request takes a slot of <see cref="ComputeJobLimiter"/> before the action runs and
/// gives it back when the action ends, or answers HTTP 429 <c>server_busy</c> with a <c>Retry-After</c> header when
/// every slot is taken (#936). The job is stored in <see cref="HttpContext.Items"/> under
/// <see cref="ComputeJob.ItemKey"/>, so the action can hand it to work that outlives the request.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ComputeJobAttribute : Attribute, IAsyncActionFilter {
    /// <inheritdoc/>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next) {
        var limiter = context.HttpContext.RequestServices.GetService<ComputeJobLimiter>();
        if (limiter == null) {
            await next();
            return;
        }
        using var job = limiter.TryStart();
        if (job == null) {
            context.HttpContext.Response.Headers.RetryAfter = ComputeJobLimiter.RetryAfterSeconds.ToString();
            context.Result = new ObjectResult(limiter.ServerBusyBody()) { StatusCode = StatusCodes.Status429TooManyRequests };
            return;
        }
        context.HttpContext.Items[ComputeJob.ItemKey] = job;
        await next();
    }
}
