using System.Globalization;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Tables;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_INTPROGRAMMING01.Solvers;
using API.Problems.NPComplete.NPC_INTPROGRAMMING01.Verifiers;
using static API.Interfaces.Graphs.FormulaExpr;

namespace API.Problems.NPComplete.NPC_INTPROGRAMMING01.Visualizations;

/// <summary>
/// 0-1 Integer Linear Programming's Formula picture (#929): the constraints Cx ≤ d written out, one per line
/// (<c>2x₁ + 3x₂ − x₃ ≤ 5</c>), joined by ∧, with the 0/1 chips of x underneath. It is served only by <c>format=frames</c>
/// (type <c>Formula</c>) and is an extra, opt-in view: the problem's default stays the Table
/// (<see cref="IntProgrammingVisualization"/>), whose list format this entry serves unchanged.
/// <para>
/// <b>Ids.</b> Row i (0-based) is the group <c>r{i}</c> (label R{i+1}) and its relation <c>r{i}-rel</c>; in it, the occurrence of
/// x_{j+1} is <c>r{i}-x{j}</c>, its coefficient (when not ±1) <c>r{i}-c{j}</c> under <c>r{i}-m{j}</c>, a −x <c>r{i}-n{j}</c>,
/// the k-th run of + or − <c>r{i}-s{k}</c>, a row with no terms <c>r{i}-zero</c>, and the bound <c>r{i}-d</c>. The chips are
/// <c>v:x{j+1}</c>.
/// </para>
/// <para>
/// <b>Frames</b> follow 0-1 ILP Brute Force's steps: each try sets every chip to 0 or 1 (a 1 takes the try's state, a 0 is
/// <c>False</c>); a row that holds is <c>Covered</c> and one over its bound <c>Rejected</c>, each with its total as the note
/// (<c>total 6 &gt; 5</c>); a variable's occurrences in a row take the try's state, <c>Rejected</c> inside a row it breaks,
/// and <c>False</c> when it is 0. A Reject's focus is the rows over their bounds.
/// </para>
/// </summary>
class IntProgrammingFormulaVisualization : IVisualization<INTPROGRAMMING01, API_TableJSON>, IFormulaExprVisualization<INTPROGRAMMING01, Assignment> {
    public string visualizationName { get; } = "0-1 Integer Programming Formula";
    public string visualizationDefinition { get; } = "The constraints Cx <= d written out as formulas, one per line, each with its total under the assignment being tried, and the 0/1 value of each variable underneath. A row that holds is met; a row over its bound fails.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.DynamicTable;
    public ISolver solver { get; } = new IntegerProgrammingBruteForce();

    private readonly IntProgrammingVisualization _listFormat = new();

    public API_TableJSON visualize(INTPROGRAMMING01 problem) => _listFormat.visualize(problem);

    public API_JSON SolvedVisualization(INTPROGRAMMING01 problem, string solution) => _listFormat.SolvedVisualization(problem, solution);

    // --- Formula picture (format=frames only) ---
    Type? IVisualization.StepShape => typeof(Assignment);

    private static int Variables(INTPROGRAMMING01 problem) => problem.C.Count == 0 ? 0 : problem.C[0].Count;

    internal static string Row(int i) => "r" + i.ToString(CultureInfo.InvariantCulture);
    internal static string Var(int j) => GroupedGraphFrames.VariableId(IntegerProgrammingBruteForce.Variable(j));
    private static string Id(int i, string piece, int j) => $"{Row(i)}-{piece}{j.ToString(CultureInfo.InvariantCulture)}";

    FormulaPayload IFormulaExprVisualization<INTPROGRAMMING01, Assignment>.Describe(INTPROGRAMMING01 problem) => Payload(problem);

    internal static FormulaPayload Payload(INTPROGRAMMING01 problem) =>
        new(FormulaFrames.KindName, FormulaLayouts.Rows, "assignment", "constraints",
            problem.C.Select((row, i) => new FormulaGroupDto(Row(i), "R" + (i + 1).ToString(CultureInfo.InvariantCulture)) { Expr = RowExpr(problem, i) }).ToList(),
            null,
            Enumerable.Range(0, Variables(problem)).Select(j => FormulaVariableDto.Integer(Var(j), IntegerProgrammingBruteForce.Variable(j), 0, 1)).ToList()) {
            Outer = FormulaOps.And, Arrange = FormulaLayouts.Stack, Noun = "constraint",
        };

    /// <summary>Row i as written: its left side ≤ its bound (the left side alone when the row has no bound).</summary>
    internal static FormulaExpr RowExpr(INTPROGRAMMING01 problem, int i) {
        var left = LeftSide(problem.C[i], i);
        return i < problem.d.Count ? Operator(Row(i) + "-rel", FormulaOps.Le, left, Number(Row(i) + "-d", problem.d[i])) : left;
    }

    // −x₁ + x₂ − 2x₃: a coefficient of 1 is left out, −1 is a negation, any other a product; the first term keeps its sign and
    // every later one is added or subtracted by its absolute value. A run of + (or of −) is one n-ary node.
    private static FormulaExpr LeftSide(List<int> row, int i) {
        var terms = row.Select((c, j) => (c, j)).Where(t => t.c != 0).ToList();
        if (terms.Count == 0) return Number(Row(i) + "-zero", 0);
        FormulaExpr Term(int c, int j) {
            var x = VarRef(Id(i, "x", j), Var(j));
            return c == 1 ? x : c == -1 ? Operator(Id(i, "n", j), FormulaOps.Neg, x) : Operator(Id(i, "m", j), FormulaOps.Mul, Number(Id(i, "c", j), c, Var(j)), x);
        }
        var expr = Term(terms[0].c, terms[0].j);
        List<FormulaExpr>? chainArgs = null;
        int k = 0;
        foreach (var (c, j) in terms.Skip(1)) {
            string op = c < 0 ? FormulaOps.Minus : FormulaOps.Plus;
            var rhs = Term(Math.Abs(c), j);
            if (chainArgs != null && expr.Op == op) {
                chainArgs.Add(rhs);
                expr = expr with { Args = chainArgs.ToArray() };
            } else {
                chainArgs = [expr, rhs];
                expr = Operator(Id(i, "s", k++), op, [.. chainArgs]);
            }
        }
        return expr;
    }

    FormulaExprFrame IFormulaExprVisualization<INTPROGRAMMING01, Assignment>.Draw(INTPROGRAMMING01 problem, SolverStep<Assignment> step, bool incremental, bool endsRun) {
        int[]? x = ValuesOf(problem, step.Partial);
        var pick = GraphSubsetFrames.Picked(step.Event, incremental, endsRun, step.Ok == true);
        return Frame(problem, x, pick, step.Event, step.Caption, step.Ok);
    }

    FormulaExprFrame IFormulaExprVisualization<INTPROGRAMMING01, Assignment>.Solved(INTPROGRAMMING01 problem, string answer) {
        bool ok;
        try { ok = new GenericVerifier01INTP().verify(problem, answer); } catch { ok = false; }
        int[]? x = null;
        try {
            var parsed = new GenericVerifier01INTP().parseCertificate(answer);
            if (parsed.Count == Variables(problem)) x = parsed.ToArray();
        } catch { }
        string caption = ok ? $"The answer is {answer.Trim()}." : "No 0-1 assignment satisfies every row.";
        return Frame(problem, ok ? x : null, ok ? PictureState.Solution : PictureState.Rejected, StepEvent.Done, caption, ok);
    }

    // The assignment of a step, by column, or null when nothing is assigned.
    private static int[]? ValuesOf(INTPROGRAMMING01 problem, Assignment partial) {
        if (partial.Values.Count == 0) return null;
        return Enumerable.Range(0, Variables(problem))
            .Select(j => partial.Values.TryGetValue(IntegerProgrammingBruteForce.Variable(j), out var v) && int.TryParse(v, out int b) ? b : 0).ToArray();
    }

    private static FormulaExprFrame Frame(INTPROGRAMMING01 problem, int[]? x, PictureState pick, StepEvent ev, string caption, bool? ok) {
        var draft = new FormulaFrameDraft();
        var focus = new List<string>();
        if (x != null) {
            for (int j = 0; j < x.Length; j++) {
                draft.Value(Var(j), x[j]);
                draft.Var(Var(j), x[j] != 0 ? pick : PictureState.False);
            }
            for (int i = 0; i < problem.C.Count; i++) {
                var row = problem.C[i];
                int total = IntegerProgrammingBruteForce.Total(row, x);
                bool bounded = i < problem.d.Count;
                bool holds = !bounded || total <= problem.d[i];
                string Num(int v) => v.ToString(CultureInfo.InvariantCulture);
                draft.Group(Row(i), holds ? PictureState.Covered : PictureState.Rejected,
                    bounded ? $"total {Num(total)} {(holds ? "≤" : ">")} {Num(problem.d[i])}" : $"total {Num(total)}");
                if (!holds && ev == StepEvent.Reject) focus.Add(Row(i));
                for (int j = 0; j < row.Count && j < x.Length; j++) {
                    if (row[j] == 0) continue;
                    var s = x[j] == 0 ? PictureState.False : holds ? pick : PictureState.Rejected;
                    draft.Node(Id(i, "x", j), s);
                    if (Math.Abs(row[j]) != 1) draft.Node(Id(i, "c", j), s);
                }
            }
        }
        return draft.ToFrame(ev, caption, [.. focus], ok);
    }
}
