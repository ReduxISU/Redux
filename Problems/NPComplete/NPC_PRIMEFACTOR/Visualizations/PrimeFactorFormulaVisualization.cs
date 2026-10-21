using System.Globalization;
using System.Numerics;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.JSON_Objects;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_PRIMEFACTOR;
using API.Problems.NPComplete.NPC_PRIMEFACTOR.Solvers;
using static API.Interfaces.Graphs.FormulaExpr;

/// <summary>
/// Prime Factorization's Formula picture (#929): the equation <c>n = ?</c>, built up step by step to
/// <c>n = 2 × 2 × 3 × 5</c> as trial division finds the factors, with the strip's integer chips <c>i</c> (the divisor
/// tried) and <c>rest</c> (what is left to factor). It is served only by <c>format=frames</c> (type <c>Formula</c>) and is
/// an extra, opt-in view: the problem's default stays the Quantum Circuit (<see cref="ShorsDefaultVisualization"/>), whose
/// list format this entry serves unchanged.
/// <para>
/// <b>Ids.</b> The group <c>e0</c>; the equation <c>eq</c>; the number <c>n</c>; the k-th factor found <c>f{k}</c>; what is left
/// <c>rest</c> (tied to the chip <c>v:rest</c>); their product <c>prod</c>; the chips <c>v:i</c> and <c>v:rest</c>.
/// </para>
/// <para>
/// <b>Frames</b> follow Prime Factorization Trial Division's steps (<see cref="QuantumProgress.Divisor"/> and
/// <see cref="QuantumProgress.Remaining"/>): each replaces the right side (<c>exprs</c>) with the factors so far times what is
/// left. A Reject marks <c>i</c> Rejected (it does not divide); a division's Accept highlights the new factor, <c>i</c> and
/// <c>rest</c>; the last prime's Accept highlights it and drops <c>rest</c>; the factors found are kept (<c>Committed</c>)
/// until the Done, where they are the answer (<c>Solution</c>) and the equation holds (<c>Covered</c>).
/// </para>
/// </summary>
class PrimeFactorFormulaVisualization : IVisualization<PRIMEFACTOR, API_QUANTUMCIRCUIT>, IFormulaExprVisualization<PRIMEFACTOR, QuantumProgress> {
    public string visualizationName { get; } = "Prime Factorization Formula";
    public string visualizationDefinition { get; } = "The number as an equation, n = ?, built up step by step into n = the product of its prime factors as trial division finds them, with the divisor being tried and what is left to factor underneath.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.QuantumCircuitQjs;
    public ISolver solver { get; } = new PrimeFactorSolver();

    private readonly ShorsDefaultVisualization _listFormat = new();

    public API_QUANTUMCIRCUIT visualize(PRIMEFACTOR instance) => _listFormat.visualize(instance);

    public API_JSON SolvedVisualization(PRIMEFACTOR instance, string solution) => _listFormat.SolvedVisualization(instance, solution);

    // --- Formula picture (format=frames only) ---
    Type? IVisualization.StepShape => typeof(QuantumProgress);

    internal const string Group = "e0";
    internal const string I = "v:i";
    internal const string Rest = "v:rest";

    FormulaPayload IFormulaExprVisualization<PRIMEFACTOR, QuantumProgress>.Describe(PRIMEFACTOR problem) => Payload(problem);

    internal static FormulaPayload Payload(PRIMEFACTOR problem) {
        BigInteger n = BigInteger.Parse(problem.instance);
        var root = Sqrt(n) + 1;
        return new FormulaPayload(FormulaFrames.KindName, FormulaLayouts.Equation, "values", "factorization",
            [new FormulaGroupDto(Group, "") { Expr = Operator("eq", FormulaOps.Eq, Num("n", n), Placeholder("rest", "?", Rest)) }],
            null,
            [FormulaVariableDto.Integer(I, "i", 2, Fits(root) ? (long)root : null), FormulaVariableDto.Integer(Rest, "rest", 1, Fits(n) ? (long)n : null)]) {
            Noun = "equation",
        };
    }

    FormulaExprFrame IFormulaExprVisualization<PRIMEFACTOR, QuantumProgress>.Draw(PRIMEFACTOR problem, SolverStep<QuantumProgress> step, bool incremental, bool endsRun) {
        BigInteger n = BigInteger.Parse(problem.instance);
        var factors = Factors(step.Partial.Result);
        BigInteger? divisor = Parse(step.Partial.Divisor), remaining = Parse(step.Partial.Remaining);
        bool done = step.Event == StepEvent.Done;
        // A division's Accept: the newest factor is the divisor. The last prime's Accept: no divisor, and nothing is left.
        bool division = step.Event == StepEvent.Accept && divisor != null;
        bool lastPrime = step.Event == StepEvent.Accept && divisor == null;
        bool keepRest = !done && !lastPrime && remaining != null;
        var draft = new FormulaFrameDraft();
        var kept = done ? GraphSubsetFrames.Picked(StepEvent.Done, incremental, endsRun, step.Ok == true) : endsRun ? PictureState.Solution : PictureState.Committed;
        for (int k = 0; k < factors.Count; k++) draft.Node(FactorId(k), kept);
        if (step.Event == StepEvent.Accept && factors.Count > 0) draft.Node(FactorId(factors.Count - 1), PictureState.ElementHighlight);
        if (division) {
            draft.Node("rest", PictureState.ElementHighlight).Var(I, PictureState.ElementHighlight).Var(Rest, PictureState.ElementHighlight);
        } else if (step.Event == StepEvent.Reject && divisor != null) {
            draft.Var(I, PictureState.Rejected);
        }
        if (done) draft.Group(Group, step.Ok == true ? PictureState.Covered : PictureState.Rejected);
        if (divisor is BigInteger d && Fits(d)) draft.Value(I, (long)d);
        if (remaining is BigInteger r && Fits(r)) draft.Value(Rest, (long)r);
        draft.Expr(Group, Equation(n, factors, keepRest ? remaining : null));
        string[] focus = step.Event == StepEvent.Accept && factors.Count > 0 ? [FactorId(factors.Count - 1)]
            : step.Event == StepEvent.Reject && divisor != null ? [I]
            : [];
        return draft.ToFrame(step.Event, step.Caption, focus, step.Ok);
    }

    FormulaExprFrame IFormulaExprVisualization<PRIMEFACTOR, QuantumProgress>.Solved(PRIMEFACTOR problem, string answer) {
        BigInteger n = BigInteger.Parse(problem.instance);
        var factors = Factors(answer);
        bool ok;
        try { ok = problem.defaultVerifier.verify(problem, answer); } catch { ok = false; }
        var draft = new FormulaFrameDraft();
        for (int k = 0; k < factors.Count; k++) draft.Node(FactorId(k), ok ? PictureState.Solution : PictureState.Rejected);
        draft.Group(Group, ok ? PictureState.Covered : PictureState.Rejected).Expr(Group, Equation(n, factors, null));
        if (ok) draft.Value(Rest, 1);
        string caption = factors.Count == 0 ? $"The answer is {answer.Trim()}." : $"The answer is {string.Join(" × ", factors)}.";
        return draft.ToFrame(StepEvent.Done, caption, [], ok);
    }

    // ---- the equation ----

    internal static string FactorId(int k) => "f" + k.ToString(CultureInfo.InvariantCulture);

    /// <summary>n = the factors so far × what is left (left out once it is a factor itself, or 1 at the end); n = ? before any factor.</summary>
    internal static FormulaExpr Equation(BigInteger n, IReadOnlyList<BigInteger> factors, BigInteger? rest) {
        var right = factors.Select((f, k) => Num(FactorId(k), f)).ToList();
        if (rest is BigInteger r) right.Add(Num("rest", r, Rest));
        if (right.Count == 0) right.Add(Placeholder("rest", "?", Rest));
        return Operator("eq", FormulaOps.Eq, Num("n", n), right.Count == 1 ? right[0] : Operator("prod", FormulaOps.Times, [.. right]));
    }

    /// <summary>A number piece, or a placeholder with its digits when it is too large for a JSON number Redux writes.</summary>
    private static FormulaExpr Num(string id, BigInteger value, string? variable = null) =>
        Fits(value) ? Number(id, (long)value, variable) : Placeholder(id, value.ToString(CultureInfo.InvariantCulture), variable);

    private static bool Fits(BigInteger value) => value >= long.MinValue && value <= long.MaxValue;

    private static BigInteger? Parse(string? text) => text != null && BigInteger.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;

    /// <summary>The factors of an answer like <c>(2,2,3)</c>; none for anything else.</summary>
    internal static List<BigInteger> Factors(string? answer) {
        if (answer == null) return [];
        string t = answer.Trim();
        if (t.Length < 2 || t[0] != '(' || t[^1] != ')') return [];
        var parts = t[1..^1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var factors = new List<BigInteger>();
        foreach (var part in parts) {
            if (!BigInteger.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var f)) return [];
            factors.Add(f);
        }
        return factors;
    }

    /// <summary>⌊√n⌋ for n ≥ 0.</summary>
    private static BigInteger Sqrt(BigInteger n) {
        if (n < 2) return n < 0 ? 0 : n;
        BigInteger x = (BigInteger)Math.Sqrt((double)n);
        while (x * x > n) x--;
        while ((x + 1) * (x + 1) <= n) x++;
        return x;
    }
}
