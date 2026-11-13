using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Steps;
using API.Interfaces.JSON_Objects.Graphs;
using Microsoft.AspNetCore.Mvc.Diagnostics;
using API.Interfaces.Tools;
using API.Interfaces.JSON_Objects;
using API.Tools;
using Antlr4.Runtime;
using API.Tools.ApiParameters;
using System.Dynamic;

/// <summary>
/// Core controller for Problems.
/// </summary>
[ApiController]
[Route("[controller]")]
[Tags("Problem Provider")]
public class ProblemProvider : ControllerBase {
    /// <summary>
    /// The older configuration key for the time budget of a solve that records steps for a picture (#885). The one
    /// limit for every compute request is now <see cref="ComputeLimits.TimeLimitKey"/>; this key, when it is set
    /// explicitly, still decides the visualize limit, so a deployment that set it keeps what it chose.
    /// </summary>
    public const string SolveTimeLimitKey = "Visualization:SolveTimeLimitSeconds";
    /// <summary>The budget used when neither key is set.</summary>
    public const double DefaultSolveTimeLimitSeconds = ComputeLimits.DefaultTimeLimitSeconds;

    private readonly IConfiguration? _configuration;

    /// <summary>Creates the controller; <paramref name="configuration"/> supplies the solve time limit.</summary>
    public ProblemProvider(IConfiguration? configuration = null) {
        _configuration = configuration;
    }

    /// <summary>
    /// The time budget for the visualize endpoint: <see cref="SolveTimeLimitKey"/> when it is set, else
    /// <see cref="ComputeLimits.TimeLimitKey"/> (seconds, default <see cref="DefaultSolveTimeLimitSeconds"/>). Zero or
    /// less means no limit. See <see cref="ISolver.Run"/> for how a solver is stopped.
    /// </summary>
    private TimeSpan? SolveTimeLimit {
        get {
            double? old = _configuration?.GetValue<double?>(SolveTimeLimitKey);
            return old is { } seconds ? ComputeLimits.FromSeconds(seconds) : ComputeLimit;
        }
    }

    /// <summary>The time limit of every other compute request, from <see cref="ComputeLimits.TimeLimitKey"/>.</summary>
    private TimeSpan? ComputeLimit => ComputeLimits.TimeLimit(_configuration);

    /// <summary>
    /// <see cref="ComputeLimits.WithBackstop{T}"/> with this request's slot in the cap on jobs running at once (#936), so
    /// work the backstop gives up on keeps the slot until it finishes.
    /// </summary>
    private Task<(bool Finished, T? Result)> Backstop<T>(Func<T> work, TimeSpan? backstop) =>
        ComputeLimits.WithBackstop(work, backstop, HttpContext?.Items[ComputeJob.ItemKey] as ComputeJob);

    /// <summary>The backstop for a request whose solver stops itself at <paramref name="limit"/> (see <see cref="ComputeLimits.SolverBackstopGrace"/>).</summary>
    private static TimeSpan? SolverBackstop(TimeSpan? limit) => limit is { } l ? l + ComputeLimits.SolverBackstopGrace : null;

    /// <summary>HTTP 422 with the <c>time_limit</c> body (#935); see <see cref="ComputeLimits.TimeLimitBody"/>.</summary>
    private IActionResult TimeLimitError(string job, TimeSpan limit, string detail, string hint, IReadOnlyDictionary<string, object?>? context = null) =>
        UnprocessableEntity(ComputeLimits.TimeLimitBody(job, limit, detail, hint, context));

    /// <summary>
    /// The hint and context of a solve stopped by a limit (time or memory): the solver, its problem, and the problem's
    /// other solvers, which the hint suggests.
    /// </summary>
    private static (string Hint, Dictionary<string, object?> Context) SolveLimitContext(Type solverType) {
        Type? problemType = ProblemTypeOf(solverType, typeof(ISolver<>));
        var others = problemType == null ? new List<string>() : Solvers.Values
            .Where(t => t != solverType && ProblemTypeOf(t, typeof(ISolver<>)) == problemType)
            .Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
        string? problemName = null;
        try { problemName = problemType == null ? null : (Activator.CreateInstance(problemType) as IProblem)?.problemName; } catch { /* unnamed */ }
        string hint = others.Count > 0
            ? $"Try a smaller instance, or a faster solver for this problem: {string.Join(", ", others)}."
            : "Try a smaller instance.";
        return (hint, new Dictionary<string, object?> {
            ["solver"] = solverType.Name,
            ["problem"] = problemName,
            ["options"] = others,
        });
    }

    /// <summary>The time-limit error of a solve, naming the solver, its problem and the problem's other solvers.</summary>
    private IActionResult SolveTimeLimitError(string job, Type solverType, TimeSpan limit, string detail) {
        var (hint, context) = SolveLimitContext(solverType);
        return TimeLimitError(job, limit, detail, hint, context);
    }

    /// <summary>HTTP 422 with the <c>memory_limit</c> body of a solve (#936); see <see cref="ComputeLimits.MemoryLimitBody"/>.</summary>
    private IActionResult SolveMemoryLimitError(Type solverType, int limitMB, string detail) {
        var (hint, context) = SolveLimitContext(solverType);
        return UnprocessableEntity(ComputeLimits.MemoryLimitBody("solve", limitMB, detail, hint, context));
    }

    /// <summary>The time-limit error of a reduction (or a chain of them, separated by dashes).</summary>
    private IActionResult ReductionTimeLimitError(TimeSpan limit, string reduction) {
        var names = ReductionNames(reduction);
        return TimeLimitError(names.Job, limit, ComputeLimits.BackstopDetail, names.Hint, names.Context);
    }

    // --- Compute jobs in worker processes (#936, #991) ---------------------
    // With ComputeLimits:IsolateSolves on (the default), every compute endpoint runs its work in a worker process of its
    // own (SolveWorkerPool), which is killed at the time limit or when it grows past the memory limit. The worker runs the
    // same static *Core method the in-process path runs, and for every endpoint but solve it sends back the finished
    // response body, so both paths answer byte for byte alike. With the setting off, each endpoint runs its *Core method
    // here under the #943 backstop, as before.

    /// <summary>
    /// How a compute job is named in the errors of its limits and of its worker: <see cref="Job"/> in the message ("The
    /// verification ran past ..."), <see cref="Context"/> and <see cref="Hint"/> as in the endpoint's time-limit error,
    /// <see cref="Failed"/> and <see cref="Report"/> in its <c>worker_failed</c>
    /// body, and <see cref="RunsSolver"/> for the solver wording of the details (solve and visualize).
    /// </summary>
    private sealed record JobNames(string Job, string Hint, IReadOnlyDictionary<string, object?> Context,
                                   IReadOnlyDictionary<string, object?> Failed, string Report, bool RunsSolver);

    private const string VerifyHint = "Try a smaller instance or a shorter certificate.";

    private static JobNames VerifyNames(string verifier) {
        var context = new Dictionary<string, object?> { ["verifier"] = verifier };
        return new("verification", VerifyHint, context, context, "the verifier", RunsSolver: false);
    }

    private static JobNames ReductionNames(string reduction) {
        var context = new Dictionary<string, object?> { ["reduction"] = reduction };
        return new("reduction", "Try a smaller instance.", context, context, "the reduction", RunsSolver: false);
    }

    private static JobNames VisualizeNames(string visualization, Type solverType) {
        var (hint, context) = SolveLimitContext(solverType);
        return new("visualization", hint, context,
            new Dictionary<string, object?> { ["visualization"] = visualization, ["solver"] = solverType.Name },
            "the visualization, the solver", RunsSolver: true);
    }

    /// <summary>The worker pool when compute jobs run in worker processes (<see cref="ComputeLimits.IsolateSolvesKey"/>), else null.</summary>
    private SolveWorkerPool? WorkerPool =>
        HttpContext?.RequestServices.GetService<SolveWorkerPool>() is { Enabled: true } pool ? pool : null;

    /// <summary>
    /// Runs <paramref name="job"/> in a worker, killed at <paramref name="deadline"/>, and answers with its outcome;
    /// <paramref name="limit"/> is the time limit the errors name.
    /// </summary>
    private async Task<IActionResult> InWorker(SolveWorkerPool pool, WorkerJob job, TimeSpan? limit, TimeSpan? deadline,
                                               string subject, Func<JobNames> names) {
        string jobName = job.Kind == WorkerJobKind.Visualize ? "visualization"
            : job.Kind == WorkerJobKind.Verify ? "verification" : "reduction";
        var outcome = await pool.RunAsync(job, deadline, jobName, subject, HttpContext.RequestAborted);
        return JobResult(outcome, limit, pool.MemoryLimitMB, names);
    }

    /// <summary>
    /// The response of a compute job other than solve, from its outcome in this process (a *Core method: the finished
    /// body, or a 400 body) or in a worker (also the limit errors, in the error contract of <c>solve</c>).
    /// </summary>
    private IActionResult JobResult(SolveOutcome outcome, TimeSpan? limit, int? memoryLimitMB, Func<JobNames> namesOf) {
        if (outcome.Kind == SolveOutcomeKind.Answered)
            return Content(outcome.Answer!, "application/json");
        if (outcome.Kind == SolveOutcomeKind.Rejected)
            return BadRequest(outcome.Body);
        JobNames names = namesOf();
        switch (outcome.Kind) {
            case SolveOutcomeKind.Killed:
                return TimeLimitError(names.Job, limit!.Value,
                    names.RunsSolver ? ComputeLimits.CancelledDetail : ComputeLimits.JobCancelledDetail, names.Hint, names.Context);
            case SolveOutcomeKind.OutOfMemory:
                return UnprocessableEntity(ComputeLimits.MemoryLimitBody(names.Job, memoryLimitMB ?? 0,
                    names.RunsSolver ? ComputeLimits.HeapLimitDetail : ComputeLimits.JobHeapLimitDetail, names.Hint, names.Context));
            case SolveOutcomeKind.KilledForMemory:
                return UnprocessableEntity(ComputeLimits.MemoryLimitBody(names.Job, memoryLimitMB ?? 0,
                    names.RunsSolver ? ComputeLimits.MemoryWatchDetail : ComputeLimits.JobMemoryWatchDetail, names.Hint, names.Context));
            case SolveOutcomeKind.Unavailable:
                // Thrown here, so SolverUnavailableFilter answers 503 exactly as for the same job in this process.
                throw outcome.Unavailable!;
            case SolveOutcomeKind.Failed:
                // Thrown here, so an unexpected exception in the worker is a 500, as it is in this process.
                throw new ComputeWorkerException(outcome.Detail ?? $"The {names.Job} failed in its worker process.");
            default: // SolveOutcomeKind.Crashed: the worker died without answering.
                return StatusCode(StatusCodes.Status500InternalServerError,
                    SolveWorkerPool.WorkerFailedBody(names.Job, names.Report, names.Failed, outcome.Detail));
        }
    }

    /// <summary>
    /// The work of a compute endpoint other than solve, in a worker process (<see cref="ComputeWorker"/>): the same
    /// *Core method the endpoint runs in this process, with the arguments the endpoint checked. The names in
    /// <paramref name="job"/> are known (the endpoint checked them); any exception propagates, as in this process.
    /// </summary>
    internal static SolveOutcome RunJob(WorkerJob job, TimeSpan? limit) {
        switch (job.Kind) {
            case WorkerJobKind.Verify:
                return VerifyCore(job.Verifier!, job.Instance, job.Solution!);
            case WorkerJobKind.Reduce:
                return ReduceCore(job.Reduction!, job.Instance, job.IncludePictures);
            case WorkerJobKind.MapSolution:
                return MapSolutionCore(job.Reduction!, job.Solution!, job.Instance);
            case WorkerJobKind.MapSolutionBack:
                return MapSolutionBackCore(job.Reduction!, job.Solution!, job.Instance, job.Steps);
            case WorkerJobKind.Gadgets:
                return GadgetsCore(job.Reduction!, job.Instance);
            case WorkerJobKind.Visualize: {
                    IVisualization vis = Visualization(job.Visualization!);
                    ISolver chosen = string.IsNullOrEmpty(job.Solver) ? vis.solver : Solver(job.Solver);
                    return VisualizeCore(vis, chosen, job.AsFrames, job.Instance, limit);
                }
            case WorkerJobKind.VisualizeReduction:
                return VisualizeReductionCore(job.Reduction!.Split("-").ToList(), job.Solution!, job.Instance, job.IncludePictures);
            default:
                throw new InvalidOperationException($"The worker cannot run a job of kind {job.Kind}.");
        }
    }

    // Buckets every loaded type into its marker-interface dictionary in a single pass over
    // AppDomain.GetAssemblies().SelectMany(GetTypes()) instead of one full assembly scan per
    // dictionary. .Add (not the indexer) preserves the original ToDictionary behavior of
    // throwing on a duplicate lowercased type name within the same bucket.
    private static (
        Dictionary<string, Type> Problems,
        Dictionary<string, Type> Verifiers,
        Dictionary<string, Type> Solvers,
        Dictionary<string, Type> Visualizers,
        Dictionary<string, Type> Reductions
    ) ScanTypes() {
        Dictionary<string, Type> problems = new();
        Dictionary<string, Type> verifiers = new();
        Dictionary<string, Type> solvers = new();
        Dictionary<string, Type> visualizers = new();
        Dictionary<string, Type> reductions = new();

        foreach (Type type in AppDomain.CurrentDomain.GetAssemblies().SelectMany(s => s.GetTypes())) {
            if (!type.IsClass) continue;
            string key = type.Name.ToLower();

            if (typeof(IProblem).IsAssignableFrom(type)) problems.Add(key, type);
            if (typeof(IVerifier).IsAssignableFrom(type)) verifiers.Add(key, type);
            if (typeof(ISolver).IsAssignableFrom(type)) solvers.Add(key, type);
            if (typeof(IVisualization).IsAssignableFrom(type)) visualizers.Add(key, type);
            if (typeof(IReduction).IsAssignableFrom(type)) reductions.Add(key, type);
        }

        return (problems, verifiers, solvers, visualizers, reductions);
    }

    private static readonly (
        Dictionary<string, Type> Problems,
        Dictionary<string, Type> Verifiers,
        Dictionary<string, Type> Solvers,
        Dictionary<string, Type> Visualizers,
        Dictionary<string, Type> Reductions
    ) _typeMaps = ScanTypes();

    /// <summary>A dictionary of all of the problems mapped to their C# type.</summary>
    public static readonly Dictionary<string, Type> Problems = _typeMaps.Problems;

    /// <summary>A dictionary of all of the graph problems in Redux mapped to their C# type.</summary>
    public static readonly Dictionary<string, Type> GraphProblems = Problems.Where(p => typeof(IGraphProblem).IsAssignableFrom(p.Value)).ToDictionary(x => x.Key, x => x.Value);

    /// <summary>A dictionary of all verifiers in Redux mapped to their C# type.</summary>
    public static readonly Dictionary<string, Type> Verifiers = _typeMaps.Verifiers;

    /// <summary>A dictionary of all solvers in Redux mapped to their C# type.</summary>
    public static readonly Dictionary<string, Type> Solvers = _typeMaps.Solvers;

    /// <summary>A dictionary of all visualizers in Redux mapped to their C# type.</summary>
    public static readonly Dictionary<string, Type> Visualizers = _typeMaps.Visualizers;

    /// <summary>A dictionary of all reductions in Redux mapped to their C# type.</summary>
    public static readonly Dictionary<string, Type> Reductions = _typeMaps.Reductions;

    /// <summary>A dictionary of all problems, verifiers, solvers, visualizers and reductions in Redux mapped to their C# type.</summary>
    public static readonly Dictionary<string, Type> Interfaces = (new[] { Problems, Verifiers, Solvers, Visualizers, Reductions }).SelectMany(d => d).ToDictionary(x => x.Key, x => x.Value);

    /// <summary>
    /// Serializer options for the <c>info</c> and <c>problemInstance</c> endpoints, which reflect over an
    /// arbitrary problem/interface object. <see cref="JsonSerializerOptions.IncludeFields"/> is enabled so that
    /// public fields (e.g. <c>ARCSET.K</c>, the <c>UtilCollectionGraph.Nodes</c>/<c>Edges</c> fields) are emitted,
    /// matching the previous Newtonsoft.Json output which serialized public fields by default. Also writes the
    /// defaults a plain-IProblem problem inherits (<see cref="ProblemDefaultsJson"/>) and the tag lists
    /// (<see cref="TagListJson"/>).
    /// </summary>
    private static readonly JsonSerializerOptions ReflectedObjectJsonOptions =
        TagListJson.Apply(ProblemDefaultsJson.Apply(new() { IncludeFields = true }));

    // The reduce endpoint's options, one object so GuiView.Options makes its copy once. No IncludeFields and no
    // ProblemDefaultsJson (the reduction is written through IReduction, as it always was), but the tag lists, as
    // every response that carries a tag has them beside its single field.
    private static readonly JsonSerializerOptions ReduceJsonOptions = TagListJson.Apply(new() { WriteIndented = true });

#pragma warning disable CS8603 // Possible null reference return.
    static IProblem Problem(string name) {
        return Activator.CreateInstance(Problems[name.ToLower()]) as IProblem; // guaranteed success by `IsAssignableFrom`
    }

    static IProblem ProblemInstance(string name, string instance) {
        return ParseGuard.CreateProblem(Problems[name.ToLower()], instance);
    }

    static IGraphProblem GraphProblem(string name, string instance) {
        return (IGraphProblem)ParseGuard.CreateProblem(GraphProblems[name.ToLower()], instance);
    }

    static IVerifier Verifier(string name) {
        return Activator.CreateInstance(Verifiers[name.ToLower()]) as IVerifier; // guaranteed success by `IsAssignableFrom`
    }

    static ISolver Solver(string name) {
        return Activator.CreateInstance(Solvers[name.ToLower()]) as ISolver; // guaranteed success by `IsAssignableFrom`
    }

    static IVisualization Visualization(string name) {
        return Activator.CreateInstance(Visualizers[name.ToLower()]) as IVisualization;
    }

    // A reduction's constructor parses the source-problem instance and then runs the reduction, so
    // a failure there is not necessarily bad input. When the constructor throws anything other than
    // a parse exception, re-parse the instance with the source problem's own constructor: if that
    // fails the input was malformed (ReductionInputException -> 400); if it succeeds the failure is
    // inside the reduction itself and propagates unchanged.
    static IReduction Reduction(string name, string instance) {
        Type type = Reductions[name.ToLower()];
        try {
            return (IReduction)Activator.CreateInstance(type, instance)!;
        } catch (Exception ex) when (ex is not OutOfMemoryException && !IsParseError(ex)) {
            IReduction? template = null;
            try { template = Activator.CreateInstance(type) as IReduction; } catch { /* no default instance to describe */ }
            if (template != null) {
                try {
                    ParseGuard.CreateProblem(template.reductionFrom.GetType(), instance);
                } catch (ProblemParseException parseFailure) {
                    throw new ReductionInputException(template, instance, template.reductionFrom.instanceFormat, parseFailure.Message, parseFailure);
                }
            }
            throw;
        }
    }

    // mapSolutions parses the certificate and maps it in one method, so parsing cannot be guarded on
    // its own; only the exception types malformed text produces are translated (see ParseGuard).
    static string MapSolutions(IReduction red, string solution) {
        try {
            return red.mapSolutions(solution);
        } catch (Exception ex) when (ParseGuard.IsCertificateParseFailure(ex)) {
            throw new ReductionInputException(red, solution, red.reductionFrom.certificateFormat, ex.Message, ex);
        }
    }

    // The backward map reads B's answer, so malformed text is reported against B's certificate format
    // (and B's name), not A's. Same translation as MapSolutions above.
    static SolveRun MapSolutionsBack(IReduction red, string solution, bool withSteps) {
        try {
            return red.MapBackRun(solution, withSteps)!;
        } catch (Exception ex) when (ParseGuard.IsCertificateParseFailure(ex)) {
            throw new ReductionInputException(red, solution, red.reductionTo.certificateFormat, ex.Message, ex, red.reductionTo.problemName);
        }
    }

    static IReduction Reduction(string name) {
        return Activator.CreateInstance(Reductions[name.ToLower()]) as IReduction;
    }

#pragma warning restore CS8603 // Possible null reference return.

    /// <summary>
    /// Takes a problem instance, a solution certificate, and returns if that is a correct solution
    /// </summary>
    /// <param name="verifier" example = "cliqueverifier">the verifier to use</param>
    /// <param name="verify">The certificate and problem instance</param>
    /// <returns>true or false</returns>
    [ProducesResponseType(typeof(bool), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(422)]
    [HttpPost("verify")]
    [ComputeJob]
    public async Task<IActionResult> verify(string verifier, [FromBody] Verify verify) {
        // Verifiers have no time checks of their own, so the backstop, or a worker's deadline, is the limit itself (see
        // ComputeLimits).
        TimeSpan? limit = ComputeLimit;
        // An unknown verifier fails in this process, as it always has (it is a 500, not a 400 like an unknown solver).
        if (WorkerPool is { } pool && Verifiers.ContainsKey(verifier.ToLower()))
            return await InWorker(pool,
                new WorkerJob(WorkerJobKind.Verify, verify.ProblemInstance) { Verifier = verifier, Solution = verify.Certificate },
                limit, limit, verifier, () => VerifyNames(verifier));
        var (finished, outcome) = await Backstop(() => VerifyCore(verifier, verify.ProblemInstance, verify.Certificate), limit);
        if (!finished)
            return TimeLimitError("verification", limit!.Value, ComputeLimits.BackstopDetail, VerifyHint, VerifyNames(verifier).Context);
        return JobResult(outcome!, limit, null, () => VerifyNames(verifier));
    }

    /// <summary>The work of <see cref="verify"/>, in this process or in a worker: the verdict, or the 400 body of rejected input.</summary>
    internal static SolveOutcome VerifyCore(string verifier, string instance, string certificate) {
        try {
            bool verdict = Verifier(verifier).verify(instance, certificate);
            return SolveOutcome.Answered(JsonSerializer.Serialize(
                verdict.ToString(),
                new JsonSerializerOptions { WriteIndented = true }
            ));
        } catch (ProblemParseException ex) {
            return SolveOutcome.Rejected(ParseErrorBody("instance_parse_error", ex.ProblemName,
                LookupInstanceFormat(ex.ProblemName), ex.Received, ex.Message));
        } catch (CertificateParseException ex) {
            return SolveOutcome.Rejected(ParseErrorBody("certificate_parse_error", ex.Problem.problemName,
                ex.Problem.certificateFormat, ex.Received, ex.Message));
        } catch (API.DummyClasses.NoVerifierException ex) {
            return SolveOutcome.Rejected(new { error = "no_verifier", received = verifier, detail = ex.Message });
        } catch (ExactSearchLimitExceededException ex) {
            return SolveOutcome.Rejected(ExactSearchLimitBody(ex));
        }
    }

    private static object ParseErrorBody(string error, string problemName, string expected, string received, string detail) {
        return new {
            error,
            problem = problemName,
            expected,
            received,
            detail
        };
    }

    // --- Parse-error handling shared across endpoints -----------------------
    // Reflective construction (Activator.CreateInstance) wraps constructor
    // exceptions in TargetInvocationException; direct calls (e.g. mapSolutions,
    // verify) throw the parse exception unwrapped. Unwrap normalizes both so
    // callers can pattern-match on the real exception type.
    private static Exception Unwrap(Exception ex) =>
        ex is System.Reflection.TargetInvocationException { InnerException: { } inner } ? inner : ex;

    /// <summary>True when <paramref name="ex"/> (once unwrapped) is a parse error we translate to HTTP 400.</summary>
    private static bool IsParseError(Exception ex) =>
        Unwrap(ex) is ProblemParseException or ReductionInputException or CertificateParseException;

    private static bool IsExactSearchLimitError(Exception ex) =>
        Unwrap(ex) is ExactSearchLimitExceededException;

    private IActionResult ExactSearchLimitError(Exception ex) => BadRequest(ExactSearchLimitBody(ex));

    private static object ExactSearchLimitBody(Exception ex) {
        var limit = (ExactSearchLimitExceededException)Unwrap(ex);
        if (limit.SearchSpaceSize is { } size)
            return new {
                error = "exact_search_limit_exceeded",
                searchSpaceSize = size.ToString(System.Globalization.CultureInfo.InvariantCulture),
                maxAssignments = limit.MaxAssignments,
                detail = limit.Message
            };
        return new {
            error = "exact_search_limit_exceeded",
            variableCount = limit.VariableCount,
            maxVariables = limit.MaxVariables,
            detail = limit.Message
        };
    }

    /// <summary>Maps a parse exception to a 400 BadRequest with the matching structured body.</summary>
    private IActionResult ParseError(Exception ex) => BadRequest(ParseErrorBodyOf(ex));

    /// <summary>The 400 body of a parse exception (see <see cref="IsParseError"/>); rethrows any other exception.</summary>
    private static object ParseErrorBodyOf(Exception ex) => Unwrap(ex) switch {
        ProblemParseException p => ParseErrorBody("instance_parse_error", p.ProblemName,
                                           LookupInstanceFormat(p.ProblemName), p.Received, p.Message),
        ReductionInputException r => ReductionParseErrorBody("reduction_input_parse_error",
                                           r.Reduction, r.ExpectedFormat, r.Received, r.Message, r.ProblemName),
        CertificateParseException c => ParseErrorBody("certificate_parse_error",
                                           c.Problem.problemName, c.Problem.certificateFormat, c.Received, c.Message),
        _ => throw ex,
    };

    private static string LookupInstanceFormat(string problemName) {
        // problemName may be either the class name (Problems key) or the
        // friendly problemName property — scan both. Error path: O(N) is fine.
        if (Problems.TryGetValue(problemName.ToLower(), out var type)) {
            try { return (Activator.CreateInstance(type) as IProblem)?.instanceFormat ?? ""; } catch { /* fall through */ }
        }
        foreach (var kv in Problems) {
            try {
                if (Activator.CreateInstance(kv.Value) is IProblem p
                    && string.Equals(p.problemName, problemName, StringComparison.OrdinalIgnoreCase)) {
                    return p.instanceFormat;
                }
            } catch { /* skip */ }
        }
        return "";
    }

    /// <summary>
    /// Takes a problem instance and a solver and returns the solution
    /// </summary>
    /// <param name="solver" example = "Sat3BacktrackingSolver">The solver to use</param>
    /// <param name="problemInstance" example = "(x1 | !x2 | x3) &amp; (!x1 | x3 | x1) &amp; (x2 | !x3 | x1)">The instance of the problem to solve</param>
    /// <returns>solution certificate</returns>
    [HttpPost("solve")]
    [ComputeJob]
    [ProducesResponseType(400)]
    [ProducesResponseType(422)]
    public async Task<IActionResult> solve(string solver, [FromBody] string problemInstance) {
        if (!Solvers.TryGetValue(solver.ToLower(), out var solverType))
            return BadRequest(new { error = "unknown_solver", received = solver });
        TimeSpan? limit = ComputeLimit;
        // With ComputeLimits:IsolateSolves on, the solve runs in a worker process of its own, which is killed when it
        // does not stop at the time limit or grows past the memory limit (#936). The worker runs SolveCore too, so it
        // answers exactly as the in-process path below.
        if (WorkerPool is { } pool)
            return SolveResult(solverType, limit, pool.MemoryLimitMB,
                await pool.RunAsync(solver, problemInstance, limit, HttpContext.RequestAborted));
        // The solver stops itself at the limit (ISolver.Run: the recorder of a typed solver, the timerHasExpired flag of a
        // legacy one), and the run is then marked TimedOut; the backstop answers a little later for a solver that does
        // not stop (#935).
        var (finished, outcome) = await Backstop(() => SolveCore(solver, problemInstance, limit), SolverBackstop(limit));
        if (!finished)
            return SolveTimeLimitError("solve", solverType, limit!.Value, ComputeLimits.BackstopDetail);
        return SolveResult(solverType, limit, null, outcome!);
    }

    /// <summary>
    /// The solve itself, shared by the in-process path of <see cref="solve"/> and the worker process
    /// (<see cref="ComputeWorker"/>), so both answer alike: the answer, a run the solver stopped at
    /// <paramref name="limit"/>, or the 400 body of a parse error or an exact-search limit. With no limit this is
    /// <c>solve(instance)</c>, as before #935. Any other exception propagates. <paramref name="solver"/> must be a key
    /// of <see cref="Solvers"/> (any case).
    /// </summary>
    internal static SolveOutcome SolveCore(string solver, string instance, TimeSpan? limit) {
        try {
            ISolver chosen = Solver(solver);
            SolveRun run = limit == null ? new SolveRun(chosen.solve(instance), new List<object>(), null)
                                         : chosen.Run(instance, withSteps: false, limit);
            return run.TimedOut ? SolveOutcome.Stopped : SolveOutcome.Answered(run.Answer);
        } catch (Exception ex) when (IsParseError(ex)) {
            return SolveOutcome.Rejected(ParseErrorBodyOf(ex));
        } catch (Exception ex) when (IsExactSearchLimitError(ex)) {
            return SolveOutcome.Rejected(ExactSearchLimitBody(ex));
        }
    }

    /// <summary>The response of a solve, from its outcome in this process (<see cref="SolveCore"/>) or in a worker.</summary>
    private IActionResult SolveResult(Type solverType, TimeSpan? limit, int? memoryLimitMB, SolveOutcome outcome) {
        switch (outcome.Kind) {
            case SolveOutcomeKind.Answered:
                return Content(
                    JsonSerializer.Serialize(
                        outcome.Answer,
                        new JsonSerializerOptions { WriteIndented = true }),
                    "application/json");
            case SolveOutcomeKind.Rejected:
                return BadRequest(outcome.Body);
            case SolveOutcomeKind.Stopped:
                return SolveTimeLimitError("solve", solverType, limit!.Value, ComputeLimits.StoppedBySolverDetail);
            case SolveOutcomeKind.Killed:
                return SolveTimeLimitError("solve", solverType, limit!.Value, ComputeLimits.CancelledDetail);
            case SolveOutcomeKind.OutOfMemory:
                return SolveMemoryLimitError(solverType, memoryLimitMB ?? 0, ComputeLimits.HeapLimitDetail);
            case SolveOutcomeKind.KilledForMemory:
                return SolveMemoryLimitError(solverType, memoryLimitMB ?? 0, ComputeLimits.MemoryWatchDetail);
            case SolveOutcomeKind.Unavailable:
                // Thrown here, so SolverUnavailableFilter answers 503 exactly as for a solve in this process.
                throw outcome.Unavailable!;
            case SolveOutcomeKind.Failed:
                // Thrown here, so an unexpected exception in the worker is a 500, as it is in this process.
                throw new ComputeWorkerException(outcome.Detail ?? "The solve failed in its worker process.");
            default: // SolveOutcomeKind.Crashed: the worker died without answering.
                return StatusCode(StatusCodes.Status500InternalServerError, SolveWorkerPool.WorkerFailedBody(solverType.Name, outcome.Detail));
        }
    }

    /// <summary>
    /// Gets info about a particular object
    /// </summary>
    /// <param name="interface" example = "SAT3">the name of the object to get the info of</param>
    /// <param name="includePictures">True for a client that draws the redesigned visualizations: a problem's default and a visualization's type are its own. Default false, what Redux_GUI reads: what they were before the redesign (see GuiView).</param>
    /// <returns>an object instance</returns>
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(400)]
    [HttpGet("info")]
    public IActionResult info(string @interface, [FromQuery] bool includePictures = false) {
        if (string.IsNullOrEmpty(@interface) || !Interfaces.TryGetValue(@interface.ToLower(), out var type))
            return BadRequest(new { error = "unknown_interface", received = @interface });
        object? obj = Activator.CreateInstance(type);
        // Serialize by the runtime type: System.Text.Json serializes by the declared type, and `object`/`IProblem`
        // would emit an empty `{}` instead of the concrete object's members.
        return Content(JsonSerializer.Serialize(obj, obj?.GetType() ?? type, GuiView.Options(ReflectedObjectJsonOptions, includePictures)), "application/json");
    }

    /// <summary>
    /// Makes an instance of a problem from an instance string
    /// </summary>
    /// <param name="problem" example = "3SAT">problem name</param>
    /// <param name="problemInstance" example = "(x1 | !x2 | x3) &amp; (!x1 | x3 | x1) &amp; (x2 | !x3 | x1)">instance string</param>
    /// <param name="includePictures">True for a client that draws the redesigned visualizations; default false, what Redux_GUI reads (see GuiView).</param>
    /// <returns>object instance</returns>
    [ProducesResponseType(typeof(IProblem), 200)]
    [ProducesResponseType(400)]
    [HttpPost("problemInstance")]
    public IActionResult problemInstance(string problem, [FromBody] string problemInstance, [FromQuery] bool includePictures = false) {
        try {
            IProblem p = ProblemInstance(problem, problemInstance);
            // Serialize by the runtime type; see the note in `info`.
            return Content(
                JsonSerializer.Serialize(p, p.GetType(), GuiView.Options(ReflectedObjectJsonOptions, includePictures)),
                "application/json");
        } catch (System.Reflection.TargetInvocationException tie) when (tie.InnerException is ProblemParseException ex) {
            return BadRequest(ParseErrorBody("instance_parse_error", ex.ProblemName,
                LookupInstanceFormat(ex.ProblemName), ex.Received, ex.Message));
        } catch (ProblemParseException ex) {
            return BadRequest(ParseErrorBody("instance_parse_error", ex.ProblemName,
                LookupInstanceFormat(ex.ProblemName), ex.Received, ex.Message));
        }
    }

    private static bool IsEmptyVisualization(API_JSON item) {
        if (item is API_empty) return true;

        if (item is API_QUANTUMCIRCUIT circuit) {
            bool hasSolution = !string.IsNullOrWhiteSpace(circuit.solution);
            bool hasQasm = !string.IsNullOrWhiteSpace(circuit.qasm);
            bool hasD3 = circuit.d3.HasValue && circuit.d3.Value.ValueKind != JsonValueKind.Undefined && circuit.d3.Value.ValueKind != JsonValueKind.Null;

            return !(hasSolution || hasQasm || hasD3);
        }

        return false;
    }

    internal static JsonSerializerOptions VisualizeJsonOptions() {
        var options = new JsonSerializerOptions {
            WriteIndented = true,
        };
        options.Converters.Add(new API_JSON_Converter());
        return options;
    }

    /// <summary>
    /// Builds the non-empty visualization items in display order: the initial visual, any solver steps, then the
    /// solved visual. The initial visual is returned separately so each response shape can place it.
    /// <para>
    /// For a run stopped by the time limit (<paramref name="stopped"/>) there is no answer to draw: the frames are
    /// the recorded steps only (ending with the stopped Done step, for a visualization that draws Done), and
    /// <paramref name="unsolvedLast"/> asks for the unsolved picture in place of the solved one (the list format).
    /// </para>
    /// </summary>
    private static (API_JSON? Initial, List<API_JSON> Frames) buildFrames(IVisualization visualization, List<Object> steps, string solution, string instance, bool stopped = false, bool unsolvedLast = false, TimeSpan? timeLimit = null) {
        API_JSON visual = visualization.visualize(instance);
        List<API_JSON> apiSteps = visualization.StepsVisualization(instance, steps, timeLimit);

        List<API_JSON> frames = new List<API_JSON>();
        frames.AddRange(apiSteps.Where(step => !IsEmptyVisualization(step)));
        if (!stopped || unsolvedLast) {
            API_JSON last = stopped ? visualization.visualize(instance) : visualization.SolvedVisualization(instance, solution);
            if (!IsEmptyVisualization(last))
                frames.Add(last);
        }

        return (IsEmptyVisualization(visual) ? null : visual, frames);
    }

    private static string getVisualize(IVisualization visualization, List<Object> steps, string solution, string instance, bool stopped = false, TimeSpan? timeLimit = null) {
        var (initial, frames) = buildFrames(visualization, steps, solution, instance, stopped, unsolvedLast: true, timeLimit);
        List<API_JSON> list = new List<API_JSON>();
        if (initial != null)
            list.Add(initial);
        list.AddRange(frames);
        return JsonSerializer.Serialize(list, VisualizeJsonOptions());
    }

    // Declared-type properties (not object) so payload/frames go through API_JSON_Converter exactly like list items.
    private sealed record VisualizeFramesResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("type")] string Type,
        [property: System.Text.Json.Serialization.JsonPropertyName("payload")] API_JSON? Payload,
        [property: System.Text.Json.Serialization.JsonPropertyName("frames")] List<API_JSON> Frames);

    private static string getVisualizeFrames(IVisualization visualization, List<Object> steps, string solution, string instance, bool stopped = false, TimeSpan? timeLimit = null) {
        var (initial, frames) = buildFrames(visualization, steps, solution, instance, stopped, timeLimit: timeLimit);
        var body = new VisualizeFramesResponse(Tags.First(visualization.visualizationType), initial, frames);
        return JsonSerializer.Serialize(body, VisualizeJsonOptions());
    }

    // The T of the first ISolver<T> (or IVisualization<U>'s U) a type implements, if any.
    internal static Type? ProblemTypeOf(Type type, Type openGeneric) =>
        type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == openGeneric)
            ?.GetGenericArguments()[0];

    /// <summary>
    /// Whether a solver can run on the problem a visualization draws. The visualization's own solver always
    /// matches. Any other solver must declare its problem through ISolver&lt;T&gt;; if either problem type cannot
    /// be determined, the solver is rejected.
    /// </summary>
    internal static bool SolverMatchesVisualization(Type solverType, IVisualization vis) {
        if (solverType == vis.solver.GetType())
            return true;
        Type? solverProblem = ProblemTypeOf(solverType, typeof(ISolver<>));
        Type? visProblem = ProblemTypeOf(vis.GetType(), typeof(IVisualization<>))
            ?? ProblemTypeOf(vis.solver.GetType(), typeof(ISolver<>));
        if (solverProblem == null || visProblem == null)
            return false;
        return solverProblem == visProblem || solverProblem.IsAssignableFrom(visProblem);
    }

    /// <summary>
    /// Gets the visualization of an object
    /// </summary>
    /// <param name="visualization" example = "Sat3DefaultVisualization">The visualization to use</param>
    /// <param name="instance" example = "(x1 | !x2 | x3) &amp; (!x1 | x3 | x1) &amp; (x2 | !x3 | x1)">the instance of the problem</param>
    /// <param name="solver" example = "Sat3BacktrackingSolver">Optional. The solver whose steps and solution are visualized. Defaults to the visualization's own solver. Must solve the same problem as the visualization</param>
    /// <param name="format" example = "frames">Optional. "list" (default) returns a flat list of [initial, ...steps, solved]. "frames" returns an object with "type" (the visualization type), "payload" (the initial visual, or null) and "frames" (the steps followed by the solved visual). A picture type that builds its own frames (type "Graph") returns its own payload and frames instead</param>
    /// <returns>the basic visualization, any steps from the solver, and the solved visualization, shaped by <paramref name="format"/></returns>
    [HttpPost("visualize")]
    [ComputeJob]
    [ProducesResponseType(400)]
    [ProducesResponseType(422)]
    public async Task<IActionResult> visualize(string visualization, [FromBody] string instance, string? solver = null, string? format = null) {
        if (!Visualizers.TryGetValue(visualization.ToLower(), out _))
            return BadRequest(new { error = "unknown_visualization", received = visualization });
        bool asFrames;
        if (string.IsNullOrEmpty(format) || format.Equals("list", StringComparison.OrdinalIgnoreCase))
            asFrames = false;
        else if (format.Equals("frames", StringComparison.OrdinalIgnoreCase))
            asFrames = true;
        else
            return BadRequest(new { error = "unknown_format", received = format });
        var vis = Visualization(visualization);
        ISolver chosen = vis.solver;
        if (!string.IsNullOrEmpty(solver)) {
            if (!Solvers.TryGetValue(solver.ToLower(), out var solverType))
                return BadRequest(new { error = "unknown_solver", received = solver });
            if (!SolverMatchesVisualization(solverType, vis))
                return BadRequest(new { error = "solver_mismatch", received = solver, visualization });
            chosen = Solver(solver);
        }
        // A solver that runs past the time limit is stopped and its run comes back with TimedOut set (a typed one by
        // its recorder, a legacy one by its timerHasExpired flag); the stopped run is drawn, not reported as an
        // error (#885). Only a solve or a drawing that does not stop runs into the backstop, which answers time_limit,
        // or in a worker process into the deadline, where the worker is killed (#991).
        TimeSpan? limit = SolveTimeLimit;
        if (WorkerPool is { } pool)
            return await InWorker(pool,
                new WorkerJob(WorkerJobKind.Visualize, instance) {
                    Visualization = visualization, Solver = solver, AsFrames = asFrames, TimeLimitTicks = limit?.Ticks,
                },
                limit, SolverBackstop(limit), chosen.GetType().Name, () => VisualizeNames(visualization, chosen.GetType()));
        var (finished, outcome) = await Backstop(() => VisualizeCore(vis, chosen, asFrames, instance, limit), SolverBackstop(limit));
        if (!finished)
            return SolveTimeLimitError("visualization", chosen.GetType(), limit!.Value, ComputeLimits.BackstopDetail);
        return JobResult(outcome!, limit, null, () => VisualizeNames(visualization, chosen.GetType()));
    }

    /// <summary>
    /// The work of <see cref="visualize"/>, in this process or in a worker: <paramref name="chosen"/> runs with steps
    /// (stopping itself at <paramref name="limit"/>) and <paramref name="vis"/> draws the run in the asked format, or the
    /// 400 body of rejected input.
    /// </summary>
    internal static SolveOutcome VisualizeCore(IVisualization vis, ISolver chosen, bool asFrames, string instance, TimeSpan? limit) {
        try {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var run = chosen.Run(instance, withSteps: true, limit);
            // A picture type that builds its own frames serves them only for format=frames.
            if (asFrames && vis is IFramesVisualization framesVis)
                return SolveOutcome.Answered(JsonSerializer.Serialize(framesVis.BuildFrames(instance, run), FramesResponse.JsonOptions));
            // A visualization that does slow work of its own while drawing the steps (the NFA run table) gets what
            // is left of the limit, so the whole request stays within it (#764).
            TimeSpan? left = limit is { } total ? (total > clock.Elapsed ? total - clock.Elapsed : TimeSpan.Zero) : null;
            // The list format is the old frontends' contract. A visualization with a frames hook declares a typed
            // step shape for that hook only; its list methods keep getting the (legacy) steps they always did.
            var steps = run.StepsFor(vis is IFramesVisualization ? null : vis.StepShape);
            var solution = run.Answer;
            return SolveOutcome.Answered(asFrames
                ? getVisualizeFrames(vis, steps, solution, instance, run.TimedOut, left)
                : getVisualize(vis, steps, solution, instance, run.TimedOut, left));
        } catch (Exception ex) when (IsParseError(ex)) {
            return SolveOutcome.Rejected(ParseErrorBodyOf(ex));
        } catch (Exception ex) when (IsExactSearchLimitError(ex)) {
            return SolveOutcome.Rejected(ExactSearchLimitBody(ex));
        }
    }

    /// <summary>
    /// Reduces a problem and returns the visualization of the reduction
    /// </summary>
    /// <param name="reduction" example = "SipserReduceToCliqueStandard">List of reductions to use, separated by a dash. Can use a single reduction</param>
    /// <param name="solution" example = "(x1:True,x2:True)">The solution to visualize</param>
    /// <param name="instance" example = "(x1 | !x2 | x3) &amp; (!x1 | x3 | x1) &amp; (x2 | !x3 | x1)">the instance string of the problem</param>
    /// <param name="includePictures">True for a client that draws the redesigned visualizations: the target's own default is drawn. Default false, what Redux_GUI reads: an opt-in default is drawn as the placeholder (see GuiView).</param>
    /// <returns>a list containing the basic visualization, any steps from the solver, and the solved visualization</returns>
    [HttpPost("visualizeReduction")]
    [ComputeJob]
    [ProducesResponseType(400)]
    [ProducesResponseType(422)]
    public async Task<IActionResult> visualizeReduction(string reduction, string solution, [FromBody] string instance, [FromQuery] bool includePictures = false) {
        List<string> reds = reduction.Split("-").ToList();
        foreach (string r in reds)
            if (!Reductions.TryGetValue(r.ToLower(), out _))
                return BadRequest(new { error = "unknown_reduction", received = r });

        // Reductions have no time checks of their own, so the backstop, or a worker's deadline, is the limit itself
        // (see ComputeLimits).
        TimeSpan? limit = ComputeLimit;
        if (WorkerPool is { } pool)
            return await InWorker(pool,
                new WorkerJob(WorkerJobKind.VisualizeReduction, instance) { Reduction = reduction, Solution = solution, IncludePictures = includePictures },
                limit, limit, reduction, () => ReductionNames(reduction));
        var (finished, outcome) = await Backstop(() => VisualizeReductionCore(reds, solution, instance, includePictures), limit);
        if (!finished)
            return ReductionTimeLimitError(limit!.Value, reduction);
        return JobResult(outcome!, limit, null, () => ReductionNames(reduction));
    }

    /// <summary>
    /// The work of <see cref="visualizeReduction"/>, in this process or in a worker: the solution mapped through each
    /// reduction of the chain and the last target drawn with it, or the 400 body of rejected input.
    /// </summary>
    internal static SolveOutcome VisualizeReductionCore(List<string> reds, string solution, string instance, bool includePictures) {
        try {
            IReduction? red = null;
            foreach (string reductionname in reds) {
                red = Reduction(reductionname, instance);
                solution = MapSolutions(red, solution);
                instance = red.reductionTo.instance;
            }
            if (red is null)
                return SolveOutcome.Rejected(new { error = "no_reductions_provided" });
            return SolveOutcome.Answered(getVisualize(GuiView.Shown(red.visualization, includePictures), new List<Object>(), solution, instance));
        } catch (Exception ex) when (IsParseError(ex)) {
            return SolveOutcome.Rejected(ParseErrorBodyOf(ex));
        }
    }

    /// <summary>
    /// returns the reduction of a problem
    /// </summary>
    /// <param name="reduction" example = "SipserReduceToCliqueStandard">the reduction to use</param>
    /// <param name="instance" example = "(x1 | !x2 | x3) &amp; (!x1 | x3 | x1) &amp; (x2 | !x3 | x1)">instance of a problem</param>
    /// <param name="includePictures">True for a client that draws the redesigned visualizations; default false, what Redux_GUI reads (see GuiView).</param>
    /// <returns>reduction</returns>
    [HttpPost("reduce")]
    [ComputeJob]
    [ProducesResponseType(400)]
    [ProducesResponseType(422)]
    public async Task<IActionResult> reduce(string reduction, [FromBody] string instance, [FromQuery] bool includePictures = false) {
        if (!Reductions.TryGetValue(reduction.ToLower(), out _))
            return BadRequest(new { error = "unknown_reduction", received = reduction });
        // The reduction runs in its constructor, and serializing it can run more (computed members), so both are
        // inside the backstop, or the worker, whose limit is the limit itself: reductions have no time checks (see
        // ComputeLimits).
        TimeSpan? limit = ComputeLimit;
        if (WorkerPool is { } pool)
            return await InWorker(pool,
                new WorkerJob(WorkerJobKind.Reduce, instance) { Reduction = reduction, IncludePictures = includePictures },
                limit, limit, reduction, () => ReductionNames(reduction));
        var (finished, outcome) = await Backstop(() => ReduceCore(reduction, instance, includePictures), limit);
        if (!finished)
            return ReductionTimeLimitError(limit!.Value, reduction);
        return JobResult(outcome!, limit, null, () => ReductionNames(reduction));
    }

    /// <summary>The work of <see cref="reduce"/>, in this process or in a worker: the serialized reduction, or the 400 body of rejected input.</summary>
    internal static SolveOutcome ReduceCore(string reduction, string instance, bool includePictures) {
        try {
            return SolveOutcome.Answered(
                JsonSerializer.Serialize(Reduction(reduction, instance), GuiView.Options(ReduceJsonOptions, includePictures)));
        } catch (Exception ex) when (IsParseError(ex)) {
            return SolveOutcome.Rejected(ParseErrorBodyOf(ex));
        }
    }

    /// <summary>
    /// Maps the solution from one problem to another
    /// </summary>
    /// <param name="reduction" example = "SipserReduceToCliqueStandard">reduction to use</param>
    /// <param name="solution" example = "(x1:True)">solution certificate</param>
    /// <param name="instance" example = "(x1 | !x2 | x3) &amp; (!x1 | x3 | x1) &amp; (x2 | !x3 | x1)">instance of the problem</param>
    /// <returns>solution certificate of the reduction</returns>
    [HttpPost("mapSolution")]
    [ComputeJob]
    [ProducesResponseType(400)]
    [ProducesResponseType(422)]
    public async Task<IActionResult> mapSolution(string reduction, string solution, [FromBody] string instance) {
        if (!Reductions.TryGetValue(reduction.ToLower(), out _))
            return BadRequest(new { error = "unknown_reduction", received = reduction });
        TimeSpan? limit = ComputeLimit;
        if (WorkerPool is { } pool)
            return await InWorker(pool,
                new WorkerJob(WorkerJobKind.MapSolution, instance) { Reduction = reduction, Solution = solution },
                limit, limit, reduction, () => ReductionNames(reduction));
        var (finished, outcome) = await Backstop(() => MapSolutionCore(reduction, solution, instance), limit);
        if (!finished)
            return ReductionTimeLimitError(limit!.Value, reduction);
        return JobResult(outcome!, limit, null, () => ReductionNames(reduction));
    }

    /// <summary>The work of <see cref="mapSolution"/>, in this process or in a worker: the mapped solution, or the 400 body of rejected input.</summary>
    internal static SolveOutcome MapSolutionCore(string reduction, string solution, string instance) {
        try {
            string mappedSolution = MapSolutions(Reduction(reduction, instance), solution);
            return SolveOutcome.Answered(
                JsonSerializer.Serialize(mappedSolution, new JsonSerializerOptions() { WriteIndented = true }));
        } catch (Exception ex) when (IsParseError(ex)) {
            return SolveOutcome.Rejected(ParseErrorBodyOf(ex));
        }
    }

    /// <summary>
    /// Maps an answer for the target problem back to an answer for the source problem, the direction
    /// students need after solving the target. The body is the SOURCE instance (the reduction is rebuilt from it,
    /// so the backward map knows the construction); <c>solution</c> is the target problem's certificate.
    /// "{}" (no answer) maps to "{}", unless the empty set is itself a valid answer for the target instance
    /// (e.g. the empty cover of an edgeless graph); then it is mapped like any other answer. Not every reduction has a backward map yet: those answer 400 with
    /// <c>no_backward_map</c> (a request this reduction cannot serve, like <c>unknown_reduction</c>, rather
    /// than a server fault). With <c>steps=true</c> the response is <c>{ answer, steps }</c> instead of the bare string.
    /// </summary>
    /// <param name="reduction" example = "SipserReduceToCliqueStandard">reduction to use</param>
    /// <param name="solution" example = "{x1_0,x1_1,x1_2}">solution certificate of the TARGET problem</param>
    /// <param name="steps" example = "false">true to also return the steps that mapped the answer back</param>
    /// <param name="instance" example = "(x1 | !x2 | x3) &amp; (!x1 | x3 | x1) &amp; (x2 | !x3 | x1)">instance of the SOURCE problem</param>
    /// <returns>solution certificate of the source problem, or { answer, steps }</returns>
    [HttpPost("mapSolutionBack")]
    [ComputeJob]
    [ProducesResponseType(400)]
    public async Task<IActionResult> mapSolutionBack(string reduction, string solution, [FromBody] string instance, bool steps = false) {
        if (!Reductions.TryGetValue(reduction.ToLower(), out _))
            return BadRequest(new { error = "unknown_reduction", received = reduction });
        // In a worker the job gets the limit of every other reduction endpoint (#991). In this process it runs inline,
        // with no limit, as it always has.
        if (WorkerPool is { } pool) {
            TimeSpan? limit = ComputeLimit;
            return await InWorker(pool,
                new WorkerJob(WorkerJobKind.MapSolutionBack, instance) { Reduction = reduction, Solution = solution, Steps = steps },
                limit, limit, reduction, () => ReductionNames(reduction));
        }
        return JobResult(MapSolutionBackCore(reduction, solution, instance, steps), null, null, () => ReductionNames(reduction));
    }

    /// <summary>
    /// The work of <see cref="mapSolutionBack"/>, in this process or in a worker: the source answer (with its steps when
    /// <paramref name="steps"/>), or the 400 body of rejected input or of a reduction with no backward map.
    /// </summary>
    internal static SolveOutcome MapSolutionBackCore(string reduction, string solution, string instance, bool steps) {
        try {
            IReduction red = Reduction(reduction, instance);
            if (!red.hasBackwardMap())
                return SolveOutcome.Rejected(new { error = "no_backward_map", reduction });
            SolveRun run = MapSolutionsBack(red, solution, steps);
            object body = steps ? new { answer = run.Answer, steps = run.Steps } : run.Answer;
            return SolveOutcome.Answered(
                JsonSerializer.Serialize(body, new JsonSerializerOptions() { WriteIndented = true }));
        } catch (Exception ex) when (IsParseError(ex)) {
            return SolveOutcome.Rejected(ParseErrorBodyOf(ex));
        }
    }

    private static object ReductionParseErrorBody(string error, IReduction reduction, string expected, string received, string detail, string? problem = null) {
        return new {
            error,
            reduction = reduction.reductionName,
            problem = problem ?? reduction.reductionFrom.problemName,
            expected,
            received,
            detail
        };
    }


    /// <summary>
    /// Gets the gadget map of a reduction
    /// </summary>
    /// <param name="reduction" example = "SipserReduceToCliqueStandard">reduction to use</param>
    /// <param name="instance" example = "(x1 | !x2 | x3) &amp; (!x1 | x3 | x1) &amp; (x2 | !x3 | x1)">the instance of the problem</param>
    /// <returns>gadget map</returns>
    [HttpPost("gadgets")]
    [ComputeJob]
    [ProducesResponseType(400)]
    [ProducesResponseType(422)]
    public async Task<IActionResult> gadgets(string reduction, [FromBody] string instance) {
        if (!Reductions.TryGetValue(reduction.ToLower(), out _))
            return BadRequest(new { error = "unknown_reduction", received = reduction });
        TimeSpan? limit = ComputeLimit;
        if (WorkerPool is { } pool)
            return await InWorker(pool, new WorkerJob(WorkerJobKind.Gadgets, instance) { Reduction = reduction },
                limit, limit, reduction, () => ReductionNames(reduction));
        var (finished, outcome) = await Backstop(() => GadgetsCore(reduction, instance), limit);
        if (!finished)
            return ReductionTimeLimitError(limit!.Value, reduction);
        return JobResult(outcome!, limit, null, () => ReductionNames(reduction));
    }

    /// <summary>The work of <see cref="gadgets"/>, in this process or in a worker: the gadget map, or the 400 body of rejected input.</summary>
    internal static SolveOutcome GadgetsCore(string reduction, string instance) {
        try {
            return SolveOutcome.Answered(
                JsonSerializer.Serialize(Reduction(reduction, instance).gadgets, new JsonSerializerOptions() { WriteIndented = true }));
        } catch (Exception ex) when (IsParseError(ex)) {
            return SolveOutcome.Rejected(ParseErrorBodyOf(ex));
        }
    }
}
