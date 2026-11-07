using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using API.Interfaces;
using API.Interfaces.Steps;
using Microsoft.AspNetCore.Mvc;

// One solver run, every picture (#895): POST /ProblemProvider/visualizeRun.
public partial class ProblemProvider {
    /// <summary>The reasons <c>visualizeRun</c> gives for a visualization it does not draw.</summary>
    internal static class UnavailableReason {
        /// <summary>The visualization has no frames picture: it serves only the older list format (/info <c>pictureType</c> null).</summary>
        public const string NoFrames = "no_frames";
        /// <summary>Its picture cannot draw this solver's steps (another <c>StepShape</c>), and cannot draw the answer alone either.</summary>
        public const string ShapeMismatch = "shape_mismatch";
        /// <summary>The solver cannot run on the problem this visualization draws.</summary>
        public const string SolverMismatch = "solver_mismatch";
    }

    /// <summary>One picture of a <c>visualizeRun</c> response: a <c>format=frames</c> response, and the visualization that drew it.</summary>
    internal sealed record RunPicture(
        [property: JsonPropertyName("visualization")] string Visualization,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("payload")] object Payload,
        [property: JsonPropertyName("frames")] IReadOnlyList<object> Frames);

    /// <summary>A visualization a <c>visualizeRun</c> response does not draw, and why.</summary>
    internal sealed record RunUnavailable(
        [property: JsonPropertyName("visualization")] string Visualization,
        [property: JsonPropertyName("reason")] string Reason,
        [property: JsonPropertyName("detail")] string Detail);

    /// <summary>The body of <c>visualizeRun</c>.</summary>
    internal sealed record RunResponse(
        [property: JsonPropertyName("solver")] string Solver,
        [property: JsonPropertyName("solverName")] string SolverName,
        [property: JsonPropertyName("answer")] string Answer,
        [property: JsonPropertyName("ok")] bool? Ok,
        [property: JsonPropertyName("timedOut")] bool TimedOut,
        [property: JsonPropertyName("pictures")] IReadOnlyList<RunPicture> Pictures,
        [property: JsonPropertyName("unavailable")] IReadOnlyList<RunUnavailable> Unavailable) {
        /// <summary>The seed a randomized solver ran with (#932); absent for every other solver.</summary>
        [JsonPropertyName("seed")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Seed { get; init; }
    }

    // Problem type -> the class names of its visualizations, the default first, then the rest by class name.
    private static readonly ConcurrentDictionary<Type, IReadOnlyList<string>> ProblemVisualizations = new();

    /// <summary>The problem a visualization draws: its <c>IVisualization&lt;P&gt;</c>, else its own solver's <c>ISolver&lt;P&gt;</c>.</summary>
    internal static Type? ProblemOfVisualization(Type visualizationType) {
        Type? problem = ProblemTypeOf(visualizationType, typeof(IVisualization<>));
        if (problem != null) return problem;
        try {
            return Activator.CreateInstance(visualizationType) is IVisualization vis && vis.solver != null
                ? ProblemTypeOf(vis.solver.GetType(), typeof(ISolver<>))
                : null;
        } catch {
            return null;
        }
    }

    /// <summary>
    /// The visualizations of <paramref name="problemType"/>, in the problem's own order: its default visualization first, then
    /// the others by class name (the order of the Navigation list).
    /// </summary>
    internal static IReadOnlyList<string> VisualizationsOf(Type problemType) => ProblemVisualizations.GetOrAdd(problemType, problem => {
        string? defaultName = null;
        try {
            if (Activator.CreateInstance(problem) is IProblem p) defaultName = p.defaultVisualization?.GetType().Name;
        } catch { }
        var names = Visualizers.Values
            .Where(t => ProblemOfVisualization(t) == problem)
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (defaultName != null && names.Remove(defaultName)) names.Insert(0, defaultName);
        return names;
    });

    /// <summary>
    /// Runs a solver once and draws every picture of its problem from that one run
    /// </summary>
    /// <remarks>
    /// The solver runs once, with the same time limit and stop rules as <c>visualize</c>, and every visualization of its problem
    /// is built from that one run, so a solver that makes random choices (PPZ, Schoening, WalkSAT) shows the same run in every
    /// picture. Each picture is a <c>visualize?format=frames</c> response (<c>type</c>, <c>payload</c>, <c>frames</c>) plus its
    /// <c>visualization</c>; every frame's <c>step</c> is the index of the solver step it was drawn from (null for an added solved
    /// or stopped frame), so pictures line up by it. Every visualization of the problem appears once, in the problem's own order
    /// (the default first, then by class name), either in <c>pictures</c> or in <c>unavailable</c> with a reason:
    /// <c>no_frames</c> (it serves only the older list format), <c>shape_mismatch</c> (its picture cannot draw this solver's
    /// steps, nor the answer alone), <c>solver_mismatch</c> (the solver does not solve the problem it draws). A picture that
    /// cannot draw the steps but can draw the answer alone is returned with only its solved frame (<c>step</c> null). <c>ok</c>
    /// is the problem's default verifier on the answer: null when the run was stopped or the verifier cannot read the answer.
    /// Errors are those of <c>visualize</c>: 400 <c>unknown_solver</c>, <c>solver_mismatch</c> (a solver of no single problem),
    /// <c>unknown_visualization</c>, <c>instance_parse_error</c>.
    /// </remarks>
    /// <param name="solver" example = "Sat3BacktrackingSolver">The solver to run (class name)</param>
    /// <param name="instance" example = "(x1 | !x2 | x3) &amp; (!x1 | x3 | x1) &amp; (x2 | !x3 | x1)">The instance of the solver's problem</param>
    /// <param name="visualizations" example = "Sat3DefaultVisualization">Optional. Comma-separated visualization class names to draw; the default is every visualization of the problem. Another problem's visualization asked for by name is listed as solver_mismatch</param>
    /// <param name="seed" example = "12345">Optional. The seed of a randomized solver (WalkSAT): the same seed on the same instance gives the same run. Without it a seed is chosen; the response reports the seed used as "seed" (randomized solvers only). Other solvers ignore it (#932)</param>
    /// <returns>{ solver, solverName, answer, ok, timedOut, seed (randomized solvers only), pictures: [{ visualization, type, payload, frames }], unavailable: [{ visualization, reason, detail }] }</returns>
    [HttpPost("visualizeRun")]
    [ComputeJob]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(400)]
    public IActionResult visualizeRun(string? solver, [FromBody] string instance, string? visualizations = null, int? seed = null) {
        if (string.IsNullOrEmpty(solver) || !Solvers.TryGetValue(solver.ToLower(), out var solverType))
            return BadRequest(new { error = "unknown_solver", received = solver });
        Type? problemType = ProblemTypeOf(solverType, typeof(ISolver<>));
        // A solver for an interface or abstract problem (DummySolver solves IProblem) cannot build its problem from text.
        if (problemType == null || problemType.IsInterface || problemType.IsAbstract)
            return BadRequest(new { error = "solver_mismatch", received = solver, detail = $"{solverType.Name} does not solve one problem, so it has no pictures." });

        var names = VisualizationsOf(problemType).ToList();
        if (!string.IsNullOrWhiteSpace(visualizations)) {
            var asked = new List<string>();
            foreach (string v in visualizations.Split(',').Select(v => v.Trim()).Where(v => v.Length > 0)) {
                if (!Visualizers.TryGetValue(v.ToLower(), out var vt))
                    return BadRequest(new { error = "unknown_visualization", received = v });
                if (!asked.Contains(vt.Name)) asked.Add(vt.Name);
            }
            // The problem's own order for its visualizations; another problem's, asked for by name, follow in the order asked.
            names = names.Where(asked.Contains).Concat(asked.Where(n => !names.Contains(n))).ToList();
        }

        ISolver chosen = Solver(solver);
        RandomizedSolvers.Apply(chosen, seed);
        IProblem problem = (IProblem)Activator.CreateInstance(problemType)!;
        try {
            // One run for every picture, with the visualize endpoint's time limit and stop rules.
            var run = chosen.Run(instance, withSteps: true, SolveTimeLimit);
            // The problem's own verifier judges the answer; a stopped run has none, and an answer it cannot read gets no verdict.
            bool? ok = null;
            if (!run.TimedOut) {
                try { ok = problem.defaultVerifier.verify(instance, run.Answer); } catch { ok = null; }
            }
            var pictures = new List<RunPicture>();
            var unavailable = new List<RunUnavailable>();
            foreach (string name in names) {
                var vis = Visualization(name);
                if (!SolverMatchesVisualization(solverType, vis)) {
                    // A picture of the solver's own problem that refuses its run (the Search Tree and a randomized solver, #932).
                    bool refused = vis is IFramesVisualization picture && !picture.DrawsSolver(solverType);
                    unavailable.Add(new(name, UnavailableReason.SolverMismatch, refused
                        ? $"{name} cannot draw {chosen.solverName}'s run."
                        : $"{chosen.solverName} does not solve the problem {name} draws."));
                } else if (vis is not IFramesVisualization frames) {
                    unavailable.Add(new(name, UnavailableReason.NoFrames,
                        $"{name} has no frames picture; it serves only the older list format."));
                } else if (run.StepShape != vis.StepShape && !frames.DrawsAnswerAlone()) {
                    unavailable.Add(new(name, UnavailableReason.ShapeMismatch,
                        $"{name} draws {ShapeName(vis.StepShape)} steps, but {chosen.solverName} records {ShapeName(run.StepShape)} steps, and the picture cannot show the answer alone."));
                } else {
                    // A picture that cannot draw the steps is handed none (SolveRun.StepsFor), so it draws the answer alone.
                    var built = frames.BuildFrames(instance, run);
                    pictures.Add(new(name, built.Type, built.Payload, built.Frames));
                }
            }
            var body = new RunResponse(solverType.Name, chosen.solverName, run.Answer, ok, run.TimedOut, pictures, unavailable) { Seed = run.Seed };
            return Content(JsonSerializer.Serialize(body, FramesResponse.JsonOptions), "application/json");
        } catch (Exception ex) when (IsParseError(ex)) {
            return ParseError(ex);
        }
    }

    private static string ShapeName(Type? shape) => shape?.Name ?? "untyped";
}
