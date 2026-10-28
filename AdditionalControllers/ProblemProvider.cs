using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using API.Interfaces;
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

    /// <summary>The backstop for a request whose solver stops itself at <paramref name="limit"/> (see <see cref="ComputeLimits.SolverBackstopGrace"/>).</summary>
    private static TimeSpan? SolverBackstop(TimeSpan? limit) => limit is { } l ? l + ComputeLimits.SolverBackstopGrace : null;

    /// <summary>HTTP 422 with the <c>time_limit</c> body (#935); see <see cref="ComputeLimits.TimeLimitBody"/>.</summary>
    private IActionResult TimeLimitError(string job, TimeSpan limit, string detail, string hint, IReadOnlyDictionary<string, object?>? context = null) =>
        UnprocessableEntity(ComputeLimits.TimeLimitBody(job, limit, detail, hint, context));

    /// <summary>The time-limit error of a solve, naming the solver, its problem and the problem's other solvers.</summary>
    private IActionResult SolveTimeLimitError(string job, Type solverType, TimeSpan limit, string detail) {
        Type? problemType = ProblemTypeOf(solverType, typeof(ISolver<>));
        var others = problemType == null ? new List<string>() : Solvers.Values
            .Where(t => t != solverType && ProblemTypeOf(t, typeof(ISolver<>)) == problemType)
            .Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
        string? problemName = null;
        try { problemName = problemType == null ? null : (Activator.CreateInstance(problemType) as IProblem)?.problemName; } catch { /* unnamed */ }
        string hint = others.Count > 0
            ? $"Try a smaller instance, or a faster solver for this problem: {string.Join(", ", others)}."
            : "Try a smaller instance.";
        return TimeLimitError(job, limit, detail, hint, new Dictionary<string, object?> {
            ["solver"] = solverType.Name,
            ["problem"] = problemName,
            ["options"] = others,
        });
    }

    /// <summary>The time-limit error of a reduction (or a chain of them, separated by dashes).</summary>
    private IActionResult ReductionTimeLimitError(TimeSpan limit, string reduction) =>
        TimeLimitError("reduction", limit, ComputeLimits.BackstopDetail, "Try a smaller instance.",
            new Dictionary<string, object?> { ["reduction"] = reduction });

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
    /// matching the previous Newtonsoft.Json output which serialized public fields by default.
    /// </summary>
    private static readonly JsonSerializerOptions ReflectedObjectJsonOptions = new() { IncludeFields = true };

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
    public async Task<IActionResult> verify(string verifier, [FromBody] Verify verify) {
        try {
            // Verifiers have no time checks of their own, so the backstop is the limit itself (see ComputeLimits).
            TimeSpan? limit = ComputeLimit;
            var (finished, verdict) = await ComputeLimits.WithBackstop(
                () => Verifier(verifier).verify(verify.ProblemInstance, verify.Certificate), limit);
            if (!finished)
                return TimeLimitError("verification", limit!.Value, ComputeLimits.BackstopDetail,
                    "Try a smaller instance or a shorter certificate.", new Dictionary<string, object?> { ["verifier"] = verifier });
            string result = JsonSerializer.Serialize(
                verdict.ToString(),
                new JsonSerializerOptions { WriteIndented = true }
            );
            return Content(result, "application/json");
        } catch (ProblemParseException ex) {
            return BadRequest(ParseErrorBody("instance_parse_error", ex.ProblemName,
                LookupInstanceFormat(ex.ProblemName), ex.Received, ex.Message));
        } catch (CertificateParseException ex) {
            return BadRequest(ParseErrorBody("certificate_parse_error", ex.Problem.problemName,
                ex.Problem.certificateFormat, ex.Received, ex.Message));
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

    /// <summary>Maps a parse exception to a 400 BadRequest with the matching structured body.</summary>
    private IActionResult ParseError(Exception ex) => Unwrap(ex) switch {
        ProblemParseException p => BadRequest(ParseErrorBody("instance_parse_error", p.ProblemName,
                                           LookupInstanceFormat(p.ProblemName), p.Received, p.Message)),
        ReductionInputException r => BadRequest(ReductionParseErrorBody("reduction_input_parse_error",
                                           r.Reduction, r.ExpectedFormat, r.Received, r.Message)),
        CertificateParseException c => BadRequest(ParseErrorBody("certificate_parse_error",
                                           c.Problem.problemName, c.Problem.certificateFormat, c.Received, c.Message)),
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
    [ProducesResponseType(400)]
    [ProducesResponseType(422)]
    public async Task<IActionResult> solve(string solver, [FromBody] string problemInstance) {
        if (!Solvers.TryGetValue(solver.ToLower(), out var solverType))
            return BadRequest(new { error = "unknown_solver", received = solver });
        try {
            // The solver stops itself at the limit (ISolver.Run: the recorder of a typed solver, the timerHasExpired
            // flag of a legacy one), and the run is then marked TimedOut; the backstop answers a little later for a
            // solver that does not stop (#935). With no limit this is solve(problemInstance), as before.
            TimeSpan? limit = ComputeLimit;
            ISolver chosen = Solver(solver);
            var (finished, run) = await ComputeLimits.WithBackstop(
                () => limit == null ? new SolveRun(chosen.solve(problemInstance), new List<object>(), null)
                                    : chosen.Run(problemInstance, withSteps: false, limit),
                SolverBackstop(limit));
            if (!finished || run!.TimedOut)
                return SolveTimeLimitError("solve", solverType, limit!.Value,
                    finished ? ComputeLimits.StoppedBySolverDetail : ComputeLimits.BackstopDetail);
            return Content(
                JsonSerializer.Serialize(
                    run.Answer,
                    new JsonSerializerOptions { WriteIndented = true }),
                "application/json");
        } catch (System.Reflection.TargetInvocationException tie) when (tie.InnerException is ProblemParseException ex) {
            return BadRequest(ParseErrorBody("instance_parse_error", ex.ProblemName,
                LookupInstanceFormat(ex.ProblemName), ex.Received, ex.Message));
        } catch (ProblemParseException ex) {
            return BadRequest(ParseErrorBody("instance_parse_error", ex.ProblemName,
                LookupInstanceFormat(ex.ProblemName), ex.Received, ex.Message));
        }
    }

    /// <summary>
    /// Gets info about a particular object
    /// </summary>
    /// <param name="interface" example = "SAT3">the name of the object to get the info of</param>
    /// <returns>an object instance</returns>
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(400)]
    [HttpGet("info")]
    public IActionResult info(string @interface) {
        if (string.IsNullOrEmpty(@interface) || !Interfaces.TryGetValue(@interface.ToLower(), out var type))
            return BadRequest(new { error = "unknown_interface", received = @interface });
        object? obj = Activator.CreateInstance(type);
        // Serialize by the runtime type: System.Text.Json serializes by the declared type, and `object`/`IProblem`
        // would emit an empty `{}` instead of the concrete object's members.
        return Content(JsonSerializer.Serialize(obj, obj?.GetType() ?? type, ReflectedObjectJsonOptions), "application/json");
    }

    /// <summary>
    /// Makes an instance of a problem from an instance string
    /// </summary>
    /// <param name="problem" example = "3SAT">problem name</param>
    /// <param name="problemInstance" example = "(x1 | !x2 | x3) &amp; (!x1 | x3 | x1) &amp; (x2 | !x3 | x1)">instance string</param>
    /// <returns>object instance</returns>
    [ProducesResponseType(typeof(IProblem), 200)]
    [ProducesResponseType(400)]
    [HttpPost("problemInstance")]
    public IActionResult problemInstance(string problem, [FromBody] string problemInstance) {
        try {
            IProblem p = ProblemInstance(problem, problemInstance);
            // Serialize by the runtime type; see the note in `info`.
            return Content(
                JsonSerializer.Serialize(p, p.GetType(), ReflectedObjectJsonOptions),
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

    private string getVisualize(IVisualization visualization, List<Object> steps, string solution, string instance, bool stopped = false, TimeSpan? timeLimit = null) {
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

    private string getVisualizeFrames(IVisualization visualization, List<Object> steps, string solution, string instance, bool stopped = false, TimeSpan? timeLimit = null) {
        var (initial, frames) = buildFrames(visualization, steps, solution, instance, stopped, timeLimit: timeLimit);
        var body = new VisualizeFramesResponse(visualization.visualizationType.ToString(), initial, frames);
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
    /// <param name="format" example = "frames">Optional. "list" (default) returns a flat list of [initial, ...steps, solved]. "frames" returns an object with "type" (the visualization type), "payload" (the initial visual, or null) and "frames" (the steps followed by the solved visual)</param>
    /// <returns>the basic visualization, any steps from the solver, and the solved visualization, shaped by <paramref name="format"/></returns>
    [HttpPost("visualize")]
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
        try {
            // A solver that runs past the time limit is stopped and its run comes back with TimedOut set (a typed one by
            // its recorder, a legacy one by its timerHasExpired flag); the stopped run is drawn, not reported as an
            // error (#885). Only a solve or a drawing that does not stop runs into the backstop, which answers time_limit.
            TimeSpan? limit = SolveTimeLimit;
            var (finished, body) = await ComputeLimits.WithBackstop(() => {
                var clock = System.Diagnostics.Stopwatch.StartNew();
                var run = chosen.Run(instance, withSteps: true, limit);
                // A visualization that does slow work of its own while drawing the steps (the NFA run table) gets what
                // is left of the limit, so the whole request stays within it (#764).
                TimeSpan? left = limit is { } total ? (total > clock.Elapsed ? total - clock.Elapsed : TimeSpan.Zero) : null;
                var steps = run.StepsFor(vis.StepShape);
                var solution = run.Answer;
                return asFrames
                    ? getVisualizeFrames(vis, steps, solution, instance, run.TimedOut, left)
                    : getVisualize(vis, steps, solution, instance, run.TimedOut, left);
            }, SolverBackstop(limit));
            if (!finished)
                return SolveTimeLimitError("visualization", chosen.GetType(), limit!.Value, ComputeLimits.BackstopDetail);
            return Content(body!, "application/json");
        } catch (Exception ex) when (IsParseError(ex)) {
            return ParseError(ex);
        }
    }

    /// <summary>
    /// Reduces a problem and returns the visualization of the reduction
    /// </summary>
    /// <param name="reduction" example = "SipserReduceToCliqueStandard">List of reductions to use, separated by a dash. Can use a single reduction</param>
    /// <param name="solution" example = "(x1:True,x2:True)">The solution to visualize</param>
    /// <param name="instance" example = "(x1 | !x2 | x3) &amp; (!x1 | x3 | x1) &amp; (x2 | !x3 | x1)">the instance string of the problem</param>
    /// <returns>a list containing the basic visualization, any steps from the solver, and the solved visualization</returns>
    [HttpPost("visualizeReduction")]
    [ProducesResponseType(400)]
    [ProducesResponseType(422)]
    public async Task<IActionResult> visualizeReduction(string reduction, string solution, [FromBody] string instance) {
        List<string> reds = reduction.Split("-").ToList();
        foreach (string r in reds)
            if (!Reductions.TryGetValue(r.ToLower(), out _))
                return BadRequest(new { error = "unknown_reduction", received = r });

        // Reductions have no time checks of their own, so the backstop is the limit itself (see ComputeLimits).
        TimeSpan? limit = ComputeLimit;
        bool finished;
        string? body;
        try {
            (finished, body) = await ComputeLimits.WithBackstop(() => {
                IReduction? red = null;
                foreach (string reductionname in reds) {
                    red = Reduction(reductionname, instance);
                    solution = MapSolutions(red, solution);
                    instance = red.reductionTo.instance;
                }
                return red is null ? null : getVisualize(red.visualization, new List<Object>(), solution, instance);
            }, limit);
        } catch (Exception ex) when (IsParseError(ex)) {
            return ParseError(ex);
        }
        if (!finished)
            return ReductionTimeLimitError(limit!.Value, reduction);

        if (body is null)
            return BadRequest(new { error = "no_reductions_provided" });

        return Content(body, "application/json");
    }

    /// <summary>
    /// returns the reduction of a problem
    /// </summary>
    /// <param name="reduction" example = "SipserReduceToCliqueStandard">the reduction to use</param>
    /// <param name="instance" example = "(x1 | !x2 | x3) &amp; (!x1 | x3 | x1) &amp; (x2 | !x3 | x1)">instance of a problem</param>
    /// <returns>reduction</returns>
    [HttpPost("reduce")]
    [ProducesResponseType(400)]
    [ProducesResponseType(422)]
    public async Task<IActionResult> reduce(string reduction, [FromBody] string instance) {
        if (!Reductions.TryGetValue(reduction.ToLower(), out _))
            return BadRequest(new { error = "unknown_reduction", received = reduction });
        try {
            // The reduction runs in its constructor, and serializing it can run more (computed members), so both are
            // inside the backstop, which is the limit itself: reductions have no time checks (see ComputeLimits).
            TimeSpan? limit = ComputeLimit;
            var (finished, body) = await ComputeLimits.WithBackstop(
                () => JsonSerializer.Serialize(Reduction(reduction, instance), new JsonSerializerOptions() { WriteIndented = true }), limit);
            if (!finished)
                return ReductionTimeLimitError(limit!.Value, reduction);
            return Content(body!, "application/json");
        } catch (Exception ex) when (IsParseError(ex)) {
            return ParseError(ex);
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
    [ProducesResponseType(400)]
    [ProducesResponseType(422)]
    public async Task<IActionResult> mapSolution(string reduction, string solution, [FromBody] string instance) {
        if (!Reductions.TryGetValue(reduction.ToLower(), out _))
            return BadRequest(new { error = "unknown_reduction", received = reduction });
        try {
            TimeSpan? limit = ComputeLimit;
            var (finished, mappedSolution) = await ComputeLimits.WithBackstop(
                () => MapSolutions(Reduction(reduction, instance), solution), limit);
            if (!finished)
                return ReductionTimeLimitError(limit!.Value, reduction);
            return Content(
                JsonSerializer.Serialize(mappedSolution, new JsonSerializerOptions() { WriteIndented = true }),
                "application/json");
        } catch (Exception ex) when (IsParseError(ex)) {
            return ParseError(ex);
        }
    }

    private static object ReductionParseErrorBody(string error, IReduction reduction, string expected, string received, string detail) {
        return new {
            error,
            reduction = reduction.reductionName,
            problem = reduction.reductionFrom.problemName,
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
    [ProducesResponseType(400)]
    [ProducesResponseType(422)]
    public async Task<IActionResult> gadgets(string reduction, [FromBody] string instance) {
        if (!Reductions.TryGetValue(reduction.ToLower(), out _))
            return BadRequest(new { error = "unknown_reduction", received = reduction });
        try {
            TimeSpan? limit = ComputeLimit;
            var (finished, body) = await ComputeLimits.WithBackstop(
                () => JsonSerializer.Serialize(Reduction(reduction, instance).gadgets, new JsonSerializerOptions() { WriteIndented = true }), limit);
            if (!finished)
                return ReductionTimeLimitError(limit!.Value, reduction);
            return Content(body!, "application/json");
        } catch (Exception ex) when (IsParseError(ex)) {
            return ParseError(ex);
        }
    }
}
