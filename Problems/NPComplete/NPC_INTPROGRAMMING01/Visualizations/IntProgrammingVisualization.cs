using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Tables;
using API.Interfaces.Steps;
using API.Interfaces.Table;
using API.Problems.NPComplete.NPC_INTPROGRAMMING01.Solvers;
using API.Problems.NPComplete.NPC_INTPROGRAMMING01.Verifiers;

namespace API.Problems.NPComplete.NPC_INTPROGRAMMING01.Visualizations;

/// <summary>
/// 0-1 Integer Linear Programming as the Table picture's matrix (format=frames): the assignment on top, then one row per
/// constraint with its coefficients, its total under the assignment, its bound and whether it holds, step by step for the
/// selected solver. The list format draws the matrix before and after. It is the problem's default visualization.
/// </summary>
class IntProgrammingVisualization : IVisualization<INTPROGRAMMING01, API_TableJSON>, ITableVisualization<INTPROGRAMMING01, Assignment> {
    public string visualizationName { get; } = "0-1 Integer Programming Table Visualization";
    public string visualizationDefinition { get; } = "Draws the constraint matrix: the assignment x on top, then one row per inequality with its coefficients, its total Cx under the assignment and its bound d. A row that holds is met; a row over its bound fails.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.DynamicTable;
    public ISolver solver { get; } = new IntegerProgrammingBruteForce();

    // The Table picture (format=frames only). Implemented explicitly so it is not part of /info.
    Type? IVisualization.StepShape => typeof(Assignment);
    TableSpec ITableVisualization<INTPROGRAMMING01, Assignment>.Describe(INTPROGRAMMING01 problem) => Spec(problem);
    IReadOnlyList<TableDraft> ITableVisualization<INTPROGRAMMING01, Assignment>.Draw(INTPROGRAMMING01 problem, IReadOnlyList<SolverStep<Assignment>> steps, bool incremental) =>
        GraphSubsetFrames.WithEndsRun(steps).Select(p => Draft(problem, ValuesOf(problem, p.Step.Partial), p.Step.Event, p.Step.Ok, incremental, p.Step.Focus, p.EndsRun)).ToList();
    TableDraft ITableVisualization<INTPROGRAMMING01, Assignment>.Blank(INTPROGRAMMING01 problem) => Matrix(problem, null);
    TableDraft? ITableVisualization<INTPROGRAMMING01, Assignment>.Solved(INTPROGRAMMING01 problem, string answer) => SolvedDraft(problem, answer);

    public IntProgrammingVisualization() { }

    // The number of variables: the verifier's (the columns of C's first row).
    private static int Variables(INTPROGRAMMING01 problem) => problem.C.Count == 0 ? 0 : problem.C[0].Count;

    /// <summary>Columns x1 ... xn, then Total, Bound and Holds; row 0 is the assignment, row r (1-based) constraint r.</summary>
    internal static TableSpec Spec(INTPROGRAMMING01 problem) {
        int n = Variables(problem);
        var columns = Enumerable.Range(0, n).Select(j => new TableColumnSpec(IntegerProgrammingBruteForce.Variable(j), IntegerProgrammingBruteForce.Variable(j)))
            .Concat([new TableColumnSpec("Total", "total"), new TableColumnSpec("Bound", "bound"), new TableColumnSpec("Holds?", "holds")]).ToList();
        return new TableSpec(TableShape.Matrix, columns) {
            RowLabels = new[] { "x =" }.Concat(Enumerable.Range(1, problem.C.Count).Select(r => $"row {r}")).ToList(),
        };
    }

    // The assignment of a step, by column, or null when nothing is assigned (the empty answer).
    private static int[]? ValuesOf(INTPROGRAMMING01 problem, Assignment partial) {
        if (partial.Values.Count == 0) return null;
        return Enumerable.Range(0, Variables(problem))
            .Select(j => partial.Values.TryGetValue(IntegerProgrammingBruteForce.Variable(j), out var v) && int.TryParse(v, out int x) ? x : 0).ToArray();
    }

    // The matrix under assignment x (null: nothing assigned, so every x is "?" and no totals).
    private static TableDraft Matrix(INTPROGRAMMING01 problem, int[]? x) {
        int n = Variables(problem);
        var draft = new TableDraft();
        draft.AddRow(Enumerable.Range(0, n).Select(j => (string?)(x == null ? "?" : TableFrames.Num(x[j]))).Concat([null, null, null]).ToArray());
        for (int r = 0; r < problem.C.Count; r++) {
            var row = problem.C[r];
            int? bound = r < problem.d.Count ? problem.d[r] : null;
            int? total = x == null ? null : IntegerProgrammingBruteForce.Total(row, x);
            string? holds = total is int t && bound is int b ? (t <= b ? "yes" : "no") : null;
            draft.AddRow(Enumerable.Range(0, n).Select(j => j < row.Count ? TableFrames.Num(row[j]) : null)
                .Concat([total is int tt ? TableFrames.Num(tt) : null, bound is int bb ? TableFrames.Num(bb) : null, holds]).ToArray());
        }
        return draft;
    }

    // One step: the assignment is a candidate (ElementHighlight) while it is only tried, kept (Committed) in an Accept the run
    // goes on from, and the answer (Solution) in an Accept that ends the run or a correct Done (#886). A row over its bound is Rejected; a row that holds is Covered while the assignment is
    // tried, and Solution once it is the answer. A Done with no solution keeps the last try (#905): its x is Rejected, its
    // rows Rejected or Covered as in the try frames, so the run closes on what failed, not on the blank.
    private static TableDraft Draft(INTPROGRAMMING01 problem, int[]? x, StepEvent ev, bool? ok, bool incremental, IEnumerable<string> focus, bool endsRun = false) {
        var draft = Matrix(problem, x);
        if (x != null) {
            var picked = GraphSubsetFrames.Picked(ev, incremental, endsRun, ok == true);
            for (int j = 0; j < x.Length; j++) draft.MarkCell(0, j, picked);
            for (int r = 0; r < problem.C.Count && r < problem.d.Count; r++) {
                bool holds = IntegerProgrammingBruteForce.Total(problem.C[r], x) <= problem.d[r];
                draft.MarkRow(r + 1, !holds ? PictureState.Rejected : picked == PictureState.Solution ? PictureState.Solution : PictureState.Covered);
            }
        }
        // The focus names rows (a solver's constraint) or cells (the Reduction View's variable or coefficient, #693).
        foreach (var id in focus) {
            if (TableFrames.ParseCellId(id) is (int cellRow, int column)) {
                if (cellRow < draft.RowCount && column < Variables(problem) + 3) draft.FocusCell(cellRow, column);
            } else if (id.Length > 1 && id[0] == 'r' && int.TryParse(id.AsSpan(1), out int row) && row < draft.RowCount) draft.FocusRow(row);
        }
        return draft;
    }

    // An answer drawn without steps: the matrix under it, as the answer, or null when it is not an assignment of every
    // variable.
    private static TableDraft? SolvedDraft(INTPROGRAMMING01 problem, string answer) {
        List<int> x;
        try { x = new GenericVerifier01INTP().parseCertificate(answer); } catch { return null; }
        if (x.Count != Variables(problem)) return null;
        return Draft(problem, x.ToArray(), StepEvent.Done, true, false, []);
    }

    public API_TableJSON visualize(INTPROGRAMMING01 problem) =>
        TableFrames.ToListTable(Spec(problem), TableFrames.Frame(Spec(problem), Matrix(problem, null), StepEvent.Try, ""));

    public API_JSON SolvedVisualization(INTPROGRAMMING01 problem, string solution) {
        bool ok;
        try { ok = new GenericVerifier01INTP().verify(problem, solution); } catch { ok = false; }
        var draft = (ok ? SolvedDraft(problem, solution) : null) ?? Matrix(problem, null);
        return TableFrames.ToListTable(Spec(problem), TableFrames.Frame(Spec(problem), draft, StepEvent.Done, "", ok));
    }
}
