using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Tables;
using API.Interfaces.Steps;
using API.Interfaces.Table;
using API.Problems.NPComplete.NPC_EDITDISTANCE.Solvers;

namespace API.Problems.NPComplete.NPC_EDITDISTANCE.Visualizations;

/// <summary>
/// Edit Distance as the Table picture's grid (format=frames): x down the side, y across the top, each cell filled from
/// the neighbors it compares, then the cheapest edit path traced back from the corner. The list format draws the empty
/// grid and the solved one. The problem's default visualization is still <c>DummyVisualization</c>.
/// </summary>
class EditDistanceVisualization : IVisualization<EDITDISTANCE, API_TableJSON>, ITableVisualization<EDITDISTANCE, TableCells> {
    public string visualizationName { get; } = "Edit Distance Table Visualization";
    public string visualizationDefinition { get; } = "Draws the dynamic-programming grid, x down the side and y across the top. Each cell is the cost of turning a prefix of x into a prefix of y, worked out from the three cells before it; the cheapest path back from the corner spells out the edits.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.DynamicTable;
    public ISolver solver { get; } = new EditDistanceDPSolver();

    // The Table picture (format=frames only). Implemented explicitly so it is not part of /info.
    Type? IVisualization.StepShape => typeof(TableCells);
    TableSpec ITableVisualization<EDITDISTANCE, TableCells>.Describe(EDITDISTANCE problem) => Spec(problem);
    IReadOnlyList<TableDraft> ITableVisualization<EDITDISTANCE, TableCells>.Draw(EDITDISTANCE problem, IReadOnlyList<SolverStep<TableCells>> steps, bool incremental) =>
        Drafts(problem, steps);
    TableDraft ITableVisualization<EDITDISTANCE, TableCells>.Blank(EDITDISTANCE problem) => Grid(problem, null);

    public EditDistanceVisualization() { }

    /// <summary>Row i is the first i letters of x (row 0 the empty prefix, ε), column j the first j letters of y.</summary>
    internal static TableSpec Spec(EDITDISTANCE problem) =>
        new(TableShape.Grid, new[] { "ε" }.Concat(problem.targetString.Select(c => c.ToString())).Select(l => new TableColumnSpec(l)).ToList()) {
            RowLabels = new[] { "ε" }.Concat(problem.sourceString.Select(c => c.ToString())).ToList(),
            RowAxis = "source x",
            ColumnAxis = "target y",
        };

    // The grid with the values of `values` (every cell empty when null).
    private static TableDraft Grid(EDITDISTANCE problem, IReadOnlyDictionary<string, string>? values) {
        var draft = new TableDraft();
        int m = problem.sourceString.Length, n = problem.targetString.Length;
        for (int i = 0; i <= m; i++) {
            int row = i;
            draft.AddRow(Enumerable.Range(0, n + 1).Select(j => values != null && values.TryGetValue(TableFrames.CellId(row, j), out var v) ? v : null).ToArray());
        }
        return draft;
    }

    // A draft per step: the cells filled so far, the cell being filled (the focus) with arrows from the cells it compared,
    // and the trace-back's cells as Solution (the run is incremental: they are the answer so far). Cells whose value is new
    // at this step are marked changed.
    internal static IReadOnlyList<TableDraft> Drafts(EDITDISTANCE problem, IReadOnlyList<SolverStep<TableCells>> steps) {
        var drafts = new List<TableDraft>();
        IReadOnlyDictionary<string, string> before = new Dictionary<string, string>();
        foreach (var step in steps) {
            var cells = step.Partial;
            var draft = Grid(problem, cells.Values);
            foreach (var (id, value) in cells.Values)
                if ((!before.TryGetValue(id, out var old) || old != value) && TableFrames.ParseCellId(id) is var (i, j))
                    draft.MarkChanged(i, j);
            foreach (var id in cells.Path)
                if (TableFrames.ParseCellId(id) is var (i, j)) draft.MarkCell(i, j, PictureState.Solution);
            foreach (var id in step.Focus)
                if (TableFrames.ParseCellId(id) is var (i, j)) draft.FocusCell(i, j);
            if (step.Focus.Length == 1)
                for (int k = 0; k < cells.Reads.Length; k++) draft.AddArrow(cells.Reads[k], step.Focus[0], best: k == 0);
            before = cells.Values;
            drafts.Add(draft);
        }
        return drafts;
    }

    // The solved grid of the list format: the solver's last step, the whole table and its trace-back.
    private static TableFrame SolvedFrame(EDITDISTANCE problem) {
        var run = ((ISolver)new EditDistanceDPSolver()).Run(problem.instance, withSteps: true);
        var done = run.Steps.Cast<SolverStep<TableCells>>().Last();
        return TableFrames.Frame(Spec(problem), Drafts(problem, [done])[0], StepEvent.Done, done.Caption, done.Ok, run.Incremental);
    }

    public API_TableJSON visualize(EDITDISTANCE problem) =>
        TableFrames.ToListTable(Spec(problem), TableFrames.Frame(Spec(problem), Grid(problem, null), StepEvent.Try, ""));

    public API_JSON SolvedVisualization(EDITDISTANCE problem, string solution) => TableFrames.ToListTable(Spec(problem), SolvedFrame(problem));
}
