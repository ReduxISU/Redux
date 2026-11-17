using System.Collections.Concurrent;
using System.Globalization;

namespace API.Interfaces;

/// <summary>
/// The size and Big-O precheck, step 3 of the compute limits decided in #915 (#843): before a solve (visualize: #1025)
/// starts, the server parses the instance, reads its size measures (<see cref="IProblem.SizeMeasures"/>) and evaluates
/// the solver's declared <see cref="ISolver.timeComplexity"/> on them. When the estimate is hopelessly over what fits in
/// the time limit, the request is refused at once with HTTP 422 and the <c>too_large</c> body
/// (<see cref="TooLargeBody"/>), instead of running until the timer stops it.
/// <para>
/// Big-O has no constant factors, so the estimate is a count of abstract steps. The budget is
/// <see cref="StepsPerSecondKey"/> steps a second (measured on known brute-force solvers, see
/// <see cref="DefaultStepsPerSecond"/>) for the length of the time limit, and only an estimate more than
/// <see cref="MarginKey"/> times that is refused. A borderline job is left to the timer, which is still the backstop.
/// </para>
/// <para>
/// A solver is skipped (left to the timer) when it declares no Big-O, when its problem reports no measures, when its
/// Big-O uses a name its problem does not report, or when it prunes its search (<see cref="SolverType.Backtracking"/>),
/// whose worst case its pruning exists to avoid (<see cref="SkipReason"/>). So a solver is checked only once its Big-O
/// is written in the names of its problem's grammar.
/// </para>
/// <para>
/// The check runs in the server, inside the request's job slot (<see cref="ComputeJobAttribute"/>) but before any
/// worker process is used: a refused request holds its slot only while its instance is parsed. A parse that fails is
/// not reported here; the request goes on and the solve itself reports it, as without the precheck. Because that parse
/// is outside the worker's limits, an instance longer than <see cref="MaxInstanceLength"/> is not prechecked.
/// </para>
/// </summary>
static class ComputePrecheck {
    /// <summary>The configuration key for how many Big-O steps a second the server is taken to do.</summary>
    public const string StepsPerSecondKey = "ComputeLimits:PrecheckStepsPerSecond";

    /// <summary>
    /// The steps a second used when <see cref="StepsPerSecondKey"/> is not set. Measured on 2026-11-17 on the
    /// development machine (Windows 11, Release build, one solve at a time) by timing brute-force solvers on instances
    /// that make them search everything, and dividing their declared Big-O at that size by the time taken:
    /// <list type="bullet">
    /// <item>TSPBruteForce, O(|N|! * |N| * |E|), complete graphs with K = 0 (no tour is short enough): |N| = 7 in
    /// 0.21 s (3.5e6 steps a second), 8 in 0.89 s (1.0e7), 9 in 5.3 s (2.2e7).</item>
    /// <item>HamiltonianBruteForce, O(|N|! * |N| * |E|), a complete graph on |N| - 1 nodes plus one node of degree 1:
    /// |N| = 8 in 0.70 s (1.0e7), 9 in 3.0 s (3.2e7). DirectedHamiltonianBruteForce, the same graphs with arcs both
    /// ways: 7 in 0.044 s (2.5e7), 8 in 0.37 s (3.9e7), 9 in 3.3 s (5.7e7).</item>
    /// <item>CliqueBruteForce, O(C(|N|, K) * K^2 * |E|), complete bipartite graphs with K = |N| / 2: 14 in 0.33 s
    /// (2.5e7), 16 in 1.3 s (4.2e7), 18 in 5.6 s (5.7e7).</item>
    /// <item>IndependentSetBruteForce, the same Big-O, complete graphs with K = |N| / 2: 16 in 0.87 s (1.1e8), 18 in
    /// 2.9 s (2.1e8), 20 in 16 s (2.2e8).</item>
    /// <item>VertexCoverBruteForce, O(C(|N|, K) * K * (|N| + |E|)), complete graphs with K = |N| / 2: 18 in 0.28 s
    /// (2.7e8), 20 in 0.64 s (6.1e8), 22 in 2.7 s (7.3e8).</item>
    /// <item>MaxCutSolver, O(2^|N| * (|N| + |E|)), random graphs: 16 in 0.49 s (9.2e6), 18 in 1.9 s (1.3e7), 20 in
    /// 8.3 s (1.6e7).</item>
    /// </list>
    /// The rates spread over two orders of magnitude, because a Big-O drops constant factors. The default is the fastest
    /// rate seen, rounded up (1e9), so the budget errs toward running a job: with the <see cref="DefaultMargin"/> of 100,
    /// a 30-second solve is refused above 3e12 steps. Brute-force TSP on 15 nodes
    /// (15! * 15 * 105, about 2.1e15 steps) is refused; on 12 nodes (about 3.8e11 steps, hours at the measured rate)
    /// it is left to the timer.
    /// </summary>
    public const double DefaultStepsPerSecond = 1e9;

    /// <summary>
    /// The configuration key for how many times the steps that fit in the time limit an estimate must exceed before it
    /// is refused. Zero or less turns the precheck off.
    /// </summary>
    public const string MarginKey = "ComputeLimits:PrecheckMargin";

    /// <summary>The margin used when <see cref="MarginKey"/> is not set.</summary>
    public const double DefaultMargin = 100;

    /// <summary>
    /// The longest instance, in characters, the precheck parses. The parse runs in the server, outside the worker's time
    /// and memory limits, and the instance parser's time grows with the square of the length: measured on complete TSP
    /// graphs, 17,000 characters parse in 0.05 s, 69,000 in 0.6 s, 141,000 in 2.4 s and 1.6 million in about 2 minutes.
    /// A longer instance is left to the time limit, as before the precheck.
    /// </summary>
    public const int MaxInstanceLength = 65_536;

    /// <summary>The <c>error</c> code of a request refused by the precheck.</summary>
    public const string TooLargeError = "too_large";

    /// <summary>The rate from <see cref="StepsPerSecondKey"/> (default <see cref="DefaultStepsPerSecond"/>); zero or less turns the precheck off.</summary>
    public static double? StepsPerSecond(IConfiguration? configuration) {
        double rate = configuration?.GetValue<double?>(StepsPerSecondKey) ?? DefaultStepsPerSecond;
        return rate > 0 ? rate : null;
    }

    /// <summary>The margin from <see cref="MarginKey"/> (default <see cref="DefaultMargin"/>); null when the precheck is off.</summary>
    public static double? Margin(IConfiguration? configuration) {
        double margin = configuration?.GetValue<double?>(MarginKey) ?? DefaultMargin;
        return margin > 0 ? margin : null;
    }

    /// <summary>
    /// What the precheck knows about a solver before seeing an instance: its problem type and Big-O, or why it is
    /// skipped. Worked out once per solver type, from the problem's default instance.
    /// </summary>
    private sealed record SolverPlan(Type? ProblemType, MathematicalFunction? Function, string? Skip);

    private static readonly ConcurrentDictionary<Type, SolverPlan> Plans = new();

    /// <summary>
    /// Why the precheck leaves <paramref name="solverType"/> to the time limit, or null when it checks the solver: it
    /// declares no Big-O, its problem reports no size measures, or its Big-O uses a name its problem does not report.
    /// </summary>
    public static string? SkipReason(Type solverType) => PlanFor(solverType).Skip;

    private static SolverPlan PlanFor(Type solverType) => Plans.GetOrAdd(solverType, static type => {
        Type? problemType = ProblemProvider.ProblemTypeOf(type, typeof(ISolver<>));
        if (problemType == null)
            return new SolverPlan(null, null, "it does not name its problem (ISolver<T>)");
        MathematicalFunction? function;
        IReadOnlyList<SizeMeasure> measures;
        try {
            function = (Activator.CreateInstance(type) as ISolver)?.timeComplexity;
            if (function == null)
                return new SolverPlan(problemType, null, "it declares no timeComplexity");
            measures = (Activator.CreateInstance(problemType) as IProblem)?.SizeMeasures() ?? [];
        } catch (Exception ex) {
            return new SolverPlan(problemType, null, $"it or its problem could not be created ({ex.GetType().Name})");
        }
        if (measures.Count == 0)
            return new SolverPlan(problemType, function, $"{problemType.Name} reports no size measures");
        IReadOnlyList<string> names;
        try {
            names = function.VariableNames;
        } catch (Exception ex) {
            return new SolverPlan(problemType, function, $"its timeComplexity does not parse ({ex.Message})");
        }
        var reported = measures.Select(m => m.Part).ToHashSet(StringComparer.Ordinal);
        var missing = names.Where(n => !reported.Contains(n)).ToList();
        if (missing.Count > 0)
            return new SolverPlan(problemType, function,
                $"its timeComplexity uses {string.Join(", ", missing)}, which {problemType.Name} does not report");
        // A solver that prunes its search declares the worst case, which its pruning exists to avoid: TSPBranchAndBound
        // solved a 15-node complete graph in 0.17 s, where its O(|N|^2 * |N|!) is 2.9e14 steps, about 10,000
        // times what fits in 30 seconds at DefaultStepsPerSecond. Its Big-O says too little about its real running time to refuse on.
        if (SolverTypeOf(type).HasFlag(SolverType.Backtracking))
            return new SolverPlan(problemType, function,
                "it prunes its search (SolverType.Backtracking), so its worst-case Big-O says little about its running time");
        return new SolverPlan(problemType, function, null);
    });

    private static SolverType SolverTypeOf(Type solverType) {
        try { return (Activator.CreateInstance(solverType) as ISolver)?.solverType ?? SolverType.Unclassified; } catch { return SolverType.Unclassified; }
    }

    /// <summary>
    /// A solver's Big-O evaluated on an instance, against the budget of the time limit.
    /// </summary>
    /// <param name="Solver">The solver's friendly name.</param>
    /// <param name="Function">The solver's declared Big-O.</param>
    /// <param name="Measures">The measures the Big-O reads, as the instance reported them.</param>
    /// <param name="Steps">The Big-O evaluated on <paramref name="Measures"/> (may be infinite).</param>
    /// <param name="StepsPerSecond">The configured rate.</param>
    /// <param name="Margin">The configured margin.</param>
    /// <param name="Limit">The time limit of the request.</param>
    public sealed record Estimate(string Solver, MathematicalFunction Function, IReadOnlyList<SizeMeasure> Measures,
                                  double Steps, double StepsPerSecond, double Margin, TimeSpan Limit) {
        /// <summary>The steps that fit in the time limit at <see cref="StepsPerSecond"/>.</summary>
        public double StepsInLimit => StepsPerSecond * Limit.TotalSeconds;

        /// <summary>The estimate above which the request is refused: <see cref="Margin"/> times <see cref="StepsInLimit"/>.</summary>
        public double RefuseAbove => StepsInLimit * Margin;

        /// <summary>True when the estimate is over <see cref="RefuseAbove"/>.</summary>
        public bool TooLarge => Steps > RefuseAbove;
    }

    /// <summary>
    /// The estimate of running <paramref name="solverType"/> on <paramref name="instance"/> within
    /// <paramref name="limit"/>, or null when there is none to make: the precheck is off, there is no limit, the solver
    /// is skipped (<see cref="SkipReason"/>), the instance is longer than <see cref="MaxInstanceLength"/>, it does not
    /// parse (the solve reports that itself), or the Big-O does not evaluate to a number.
    /// </summary>
    public static Estimate? Evaluate(Type solverType, string instance, TimeSpan? limit, IConfiguration? configuration) {
        if (limit is not { } time || time <= TimeSpan.Zero || Margin(configuration) is not { } margin
            || StepsPerSecond(configuration) is not { } rate)
            return null;
        if (instance.Length > MaxInstanceLength)
            return null;
        SolverPlan plan = PlanFor(solverType);
        if (plan.Skip != null || plan.Function is not { } function || plan.ProblemType is not { } problemType)
            return null;
        IReadOnlyList<SizeMeasure> measures;
        try {
            measures = ParseGuard.CreateProblem(problemType, instance).SizeMeasures();
        } catch {
            return null; // The solve parses the instance again and reports what is wrong with it.
        }
        var byPart = new Dictionary<string, SizeMeasure>(StringComparer.Ordinal);
        foreach (var measure in measures)
            byPart.TryAdd(measure.Part, measure);
        var used = new List<SizeMeasure>();
        foreach (string name in function.VariableNames) {
            if (!byPart.TryGetValue(name, out var measure))
                return null;
            used.Add(measure);
        }
        double steps;
        try {
            steps = function.Evaluate(used.ToDictionary(m => m.Part, m => m.Value));
        } catch (InvalidOperationException) {
            return null;
        }
        if (double.IsNaN(steps))
            return null;
        string solverName = solverType.Name;
        try { solverName = (Activator.CreateInstance(solverType) as ISolver)?.solverName ?? solverName; } catch { /* the class name */ }
        return new Estimate(solverName, function, used, steps, rate, margin, time);
    }

    /// <summary>
    /// The estimate when it is too large to run (<see cref="Estimate.TooLarge"/>), else null: the request goes ahead.
    /// </summary>
    public static Estimate? Refusal(Type solverType, string instance, TimeSpan? limit, IConfiguration? configuration) =>
        Evaluate(solverType, instance, limit, configuration) is { TooLarge: true } estimate ? estimate : null;

    /// <summary>
    /// The body of a request refused by the precheck, in the error contract of #933 (as <see cref="ComputeLimits.TimeLimitBody"/>):
    /// <c>error</c> <see cref="TooLargeError"/>, a plain <c>message</c> naming the solver, the estimate and what fits in
    /// the limit, the technical <c>detail</c>, the numbers (<c>limitSeconds</c>, <c>estimatedSteps</c>,
    /// <c>stepsInLimit</c>, <c>refuseAboveSteps</c>, <c>timeComplexity</c>, <c>measures</c>), context, and a <c>hint</c>.
    /// <paramref name="job"/> names what was refused ("solve", "visualization").
    /// </summary>
    public static object TooLargeBody(string job, Estimate estimate, string hint, IReadOnlyDictionary<string, object?>? context = null) {
        string seconds = estimate.Limit.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);
        string measures = string.Join(", ", estimate.Measures.Select(m => $"{m.Label} = {Plain(m.Value)}"));
        var body = new Dictionary<string, object?> {
            ["error"] = TooLargeError,
            ["message"] = $"The {estimate.Solver} solver would need about {Approximately(estimate.Steps)} steps for this " +
                          $"instance, but only about {Approximately(estimate.StepsInLimit)} fit in the {seconds}-second " +
                          $"time limit, so the {job} was not started.",
            // Redux_Frontend shows the detail without the message, so the detail repeats the numbers.
            ["detail"] = $"The solver's declared running time, O({estimate.Function.Function}), comes to about " +
                         $"{Approximately(estimate.Steps)} steps at {(measures.Length > 0 ? measures : "this instance's size")}. " +
                         $"Big-O leaves out constant factors, so the server counts {Approximately(estimate.StepsPerSecond)} " +
                         $"steps a second ({Approximately(estimate.StepsInLimit)} in the {seconds}-second limit) and refuses " +
                         $"a job only when its estimate is more than {Plain(estimate.Margin)} times that.",
            ["limitSeconds"] = estimate.Limit.TotalSeconds,
            // JSON has no infinity: an estimate past double's range is sent as null (the message says how large).
            ["estimatedSteps"] = double.IsFinite(estimate.Steps) ? estimate.Steps : null,
            ["stepsInLimit"] = estimate.StepsInLimit,
            ["refuseAboveSteps"] = estimate.RefuseAbove,
            ["timeComplexity"] = estimate.Function.Function,
            ["measures"] = estimate.Measures.ToDictionary(m => m.Label, m => m.Value),
        };
        if (context != null)
            foreach (var (key, value) in context)
                body[key] = value;
        body["hint"] = hint;
        return body;
    }

    /// <summary>A whole number with digit grouping, or a short decimal.</summary>
    private static string Plain(double value) =>
        value == Math.Floor(value) && Math.Abs(value) < 1e15
            ? value.ToString("#,0", CultureInfo.InvariantCulture)
            : value.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>
    /// A count of steps in words a reader can take in: grouped digits below a million, else "2.9 × 10^14". Past
    /// double's range, "more than 10^308".
    /// </summary>
    internal static string Approximately(double steps) {
        if (!double.IsFinite(steps))
            return "more than 10^308";
        if (Math.Abs(steps) < 1e6)
            return Math.Round(steps).ToString("#,0", CultureInfo.InvariantCulture);
        int exponent = (int)Math.Floor(Math.Log10(Math.Abs(steps)));
        double mantissa = Math.Round(steps / Math.Pow(10, exponent), 1);
        if (Math.Abs(mantissa) >= 10) {
            mantissa /= 10;
            exponent++;
        }
        string digits = mantissa.ToString("0.#", CultureInfo.InvariantCulture);
        return digits == "1" ? $"10^{exponent}" : $"{digits} × 10^{exponent}";
    }
}
