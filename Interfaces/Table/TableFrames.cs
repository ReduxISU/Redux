using System.Globalization;
using System.Text.Json.Serialization;
using API.Interfaces.Graphs;
using API.Interfaces.JSON_Objects.Tables;
using API.Interfaces.Steps;

namespace API.Interfaces.Table;

/// <summary>How a Table picture reads; one renderer draws all three.</summary>
enum TableShape {
    /// <summary>A dynamic-programming grid (Edit Distance): every cell is worked out from cells filled before it, and the
    /// frames carry arrows from the cells a fill read.</summary>
    Grid,
    /// <summary>A constraint matrix (0-1 Integer Linear Programming): one row per constraint, with its total against its
    /// bound.</summary>
    Matrix,
    /// <summary>A trace table (DFA, NFA, SSSP, SPSP): rows are added as the run reaches them, so a frame may show fewer rows
    /// than the payload lists.</summary>
    Trace,
}

/// <summary>One column as a problem describes it: its header and, optionally, a stable name a frontend can key on.</summary>
/// <param name="Label">The header, as written (<c>Step</c>, <c>x1</c>, <c>ε</c>).</param>
/// <param name="Key">A camelCase name for the column (<c>step</c>, <c>total</c>), or null for a column that is only a
/// position (the letters of a grid).</param>
sealed record TableColumnSpec(string Label, string? Key = null);

/// <summary>
/// What a problem hands the Table picture: its shape and columns, and the labels of its rows when they are fixed (a grid's
/// letters, a matrix's constraints). No ids, no states; the base derives those.
/// </summary>
sealed record TableSpec(TableShape Shape, IReadOnlyList<TableColumnSpec> Columns) {
    /// <summary>The row headers, one per row, when the rows are known up front. Null for a trace table, whose rows are
    /// whatever the frames reach (unlabelled; a <c>Step</c> column numbers them).</summary>
    public IReadOnlyList<string>? RowLabels { get; init; }
    /// <summary>A caption for the whole table, or null.</summary>
    public string? Title { get; init; }
    /// <summary>What the row headers are (a grid's <c>source x</c>), or null.</summary>
    public string? RowAxis { get; init; }
    /// <summary>What the column headers are (a grid's <c>target y</c>), or null.</summary>
    public string? ColumnAxis { get; init; }
}

// --- JSON shapes (explicit names so the contract does not depend on serializer naming options) ---

/// <summary>A column: its id (<c>c0</c>, by position), its header and, when the problem gives one, its key.</summary>
sealed record TableColumnDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("label")] string Label) {
    [JsonPropertyName("key")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Key { get; init; }
}

/// <summary>A row: its id (<c>r0</c>, by position) and, for fixed rows, its header.</summary>
sealed record TableRowDto([property: JsonPropertyName("id")] string Id) {
    [JsonPropertyName("label")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Label { get; init; }
}

/// <summary>
/// The base picture of the instance: the cells' text with nothing worked out, written the way a frame writes its
/// <c>values</c> (<c>values[i][j]</c> is cell <c>r{i}c{j}</c>, null for an empty cell). It has no states, no focus, nothing
/// changed and no arrows: it is what a frontend draws before Run, so it shows none of the solver's work.
/// </summary>
sealed record TableBlank([property: JsonPropertyName("values")] IReadOnlyList<IReadOnlyList<string?>> Values);

/// <summary>
/// The static part of a Table picture, sent once per response: the shape, the columns, every row any frame (or the blank)
/// shows, and the blank: the base picture of the instance, drawn before Run. Cell <c>(i, j)</c> is <c>r{i}c{j}</c>: row
/// <c>rows[i]</c>, column <c>columns[j]</c>.
/// </summary>
sealed record TablePayload(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("shape")] string Shape,
    [property: JsonPropertyName("columns")] IReadOnlyList<TableColumnDto> Columns,
    [property: JsonPropertyName("rows")] IReadOnlyList<TableRowDto> Rows) {
    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; init; }

    [JsonPropertyName("rowAxis")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RowAxis { get; init; }

    [JsonPropertyName("columnAxis")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ColumnAxis { get; init; }

    /// <summary>The base picture of the instance (<see cref="TableBlank"/>): what a frontend draws before Run, instead of
    /// a solver's first frame. Always sent by <see cref="TableFrames.Build"/>; null (left out) only for a payload made
    /// without one.</summary>
    [JsonPropertyName("blank")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TableBlank? Blank { get; init; }
}

/// <summary>An arrow in a grid, from a cell a fill read to the cell it filled. <c>best</c> marks the one whose value it
/// took.</summary>
sealed record TableArrow(
    [property: JsonPropertyName("from")] string From,
    [property: JsonPropertyName("to")] string To,
    [property: JsonPropertyName("best")] bool Best);

/// <summary>
/// One picture of the table.
/// <list type="bullet">
/// <item><c>values</c>: the text of every cell shown, row by row (<c>values[i][j]</c> is cell <c>r{i}c{j}</c>), null for a
/// cell that is still empty. It has one entry per row shown, the first rows of the payload; a trace table shows more rows
/// as the run goes on.</item>
/// <item><c>rows</c> and <c>cells</c>: the state of whole rows and of single cells, listing only those whose state is not
/// <see cref="PictureState.Background"/>. A cell's own state shows over its row's.</item>
/// <item><c>changed</c>: the cells whose value changed at this step (drawn underlined): an annotation, not a state.</item>
/// <item><c>arrows</c> (grid only): from the cells the fill read to the cell it filled.</item>
/// </list>
/// </summary>
sealed record TableFrame(
    [property: JsonPropertyName("event")] StepEvent Event,
    [property: JsonPropertyName("caption")] string Caption,
    [property: JsonPropertyName("focus")] string[] Focus,
    [property: JsonPropertyName("values")] IReadOnlyList<IReadOnlyList<string?>> Values,
    [property: JsonPropertyName("rows")] IReadOnlyDictionary<string, StateRef> Rows,
    [property: JsonPropertyName("cells")] IReadOnlyDictionary<string, StateRef> Cells,
    [property: JsonPropertyName("changed")] string[] Changed) {
    /// <summary>The solver step this frame was drawn from (see <see cref="FramesResponse"/>, #895); null for an added frame. Always written.</summary>
    [JsonPropertyName("step")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int? Step { get; init; }

    [JsonPropertyName("arrows")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<TableArrow>? Arrows { get; init; }

    [JsonPropertyName("ok")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Ok { get; init; }
}

/// <summary>
/// One table as a visualization draws it for one step: the cells' text, and the states it gives rows and cells. The
/// visualization fills it in the problem's own terms; <see cref="TableFrames"/> turns it into a <see cref="TableFrame"/>.
/// States are raised by precedence (see <see cref="GraphSubsetFrames.Raise"/>), so marking an element twice keeps the
/// stronger state.
/// </summary>
sealed class TableDraft {
    internal readonly List<string?[]> Values = new();
    internal readonly Dictionary<string, PictureState> RowStates = new(StringComparer.Ordinal);
    internal readonly Dictionary<string, PictureState> CellStates = new(StringComparer.Ordinal);
    internal readonly List<string> ChangedCells = new();
    internal readonly List<TableArrow> ArrowList = new();
    internal readonly List<string> FocusIds = new();

    /// <summary>How many rows are shown.</summary>
    public int RowCount => Values.Count;

    /// <summary>Adds a row with these cells (null: an empty cell); returns its index.</summary>
    public int AddRow(params string?[] cells) {
        Values.Add(cells);
        return Values.Count - 1;
    }

    /// <summary>The text of cell (<paramref name="row"/>, <paramref name="column"/>).</summary>
    public string? this[int row, int column] {
        get => Values[row][column];
        set => Values[row][column] = value;
    }

    public void MarkRow(int row, PictureState state) => GraphSubsetFrames.Raise(RowStates, TableFrames.RowId(row), state);

    public void MarkCell(int row, int column, PictureState state) => GraphSubsetFrames.Raise(CellStates, TableFrames.CellId(row, column), state);

    /// <summary>Marks a cell as changed at this step (underlined).</summary>
    public void MarkChanged(int row, int column) {
        string id = TableFrames.CellId(row, column);
        if (!ChangedCells.Contains(id)) ChangedCells.Add(id);
    }

    /// <summary>An arrow from a cell the fill read to the cell it filled (grid only).</summary>
    public void AddArrow(string from, string to, bool best) => ArrowList.Add(new TableArrow(from, to, best));

    /// <summary>Names a cell as the focus: what the step works on. The base marks it (see <see cref="TableFrames.Frame"/>).</summary>
    public void FocusCell(int row, int column) => FocusIds.Add(TableFrames.CellId(row, column));

    /// <summary>Names a whole row as the focus.</summary>
    public void FocusRow(int row) => FocusIds.Add(TableFrames.RowId(row));
}

/// <summary>
/// Derives the Table picture (a dynamic-programming grid, a constraint matrix or a trace table) from (spec, a draft per
/// step, step event). Nothing here knows about a particular problem or solver.
/// <para>
/// <b>Ids.</b> Row <c>i</c> is <c>r{i}</c>, column <c>j</c> is <c>c{j}</c> and their cell is <c>r{i}c{j}</c> (0-based
/// positions). Ids are positions, never contents, so repeated letters, labels with separators and equal values stay
/// distinct and no id needs escaping.
/// </para>
/// <para>
/// <b>States</b> (precedence <c>Rejected &gt; ElementHighlight &gt; Solution &gt; Committed &gt; Covered &gt; Blocked &gt; Background</c>,
/// with <c>Untraveled</c> only where nothing else applies; see <see cref="GraphSubsetFrames.Raise"/>). The visualization
/// gives rows and cells their states in its own terms (a constraint that holds is <c>Covered</c>, a trace-back cell
/// <c>Solution</c>); the base adds the focus: <c>ElementHighlight</c>, or <c>Rejected</c> in a Reject step of an incremental
/// run (<see cref="GraphSubsetFrames.FocusMark"/>). <c>Solution</c> is for the confirmed answer only (the Done, or an Accept
/// that ends the run); what a solver has kept before then is <c>Committed</c>, and what a Done with ok = false keeps is
/// <c>Rejected</c>, never <c>Solution</c> (<see cref="GraphSubsetFrames.Picked"/>, #886).
/// </para>
/// <para>
/// A run stopped by its time limit ends with a Done frame, ok = false, that shows the instance with nothing worked out and
/// every element Background (<see cref="StoppedFrame"/>).
/// </para>
/// </summary>
static class TableFrames {
    public const string Kind = "table";

    // ---- Ids ----

    public static string RowId(int row) => $"r{row}";

    public static string ColumnId(int column) => $"c{column}";

    public static string CellId(int row, int column) => $"r{row}c{column}";

    /// <summary>The (row, column) of a cell id made by <see cref="CellId"/>, or null for anything else.</summary>
    public static (int Row, int Column)? ParseCellId(string id) {
        if (id.Length < 4 || id[0] != 'r') return null;
        int c = id.IndexOf('c');
        if (c < 2) return null;
        return int.TryParse(id.AsSpan(1, c - 1), NumberStyles.None, CultureInfo.InvariantCulture, out int row)
            && int.TryParse(id.AsSpan(c + 1), NumberStyles.None, CultureInfo.InvariantCulture, out int column)
            ? (row, column) : null;
    }

    public static string ShapeName(TableShape shape) => shape switch {
        TableShape.Grid => "grid",
        TableShape.Matrix => "matrix",
        TableShape.Trace => "trace",
        _ => throw new ArgumentOutOfRangeException(nameof(shape)),
    };

    // ---- The payload ----

    /// <summary>
    /// The payload. Its rows are the spec's labelled rows, or for a trace table as many unlabelled rows as the longest
    /// frame, or the <paramref name="blank"/>, shows (<paramref name="rowsShown"/>). <paramref name="blank"/> is the base
    /// picture of the instance (the instance with nothing worked out), sent as the payload's <c>blank</c> with its cells'
    /// text only; null leaves it out.
    /// </summary>
    public static TablePayload Payload(TableSpec spec, int rowsShown, TableDraft? blank = null) {
        if (blank != null) rowsShown = Math.Max(rowsShown, blank.RowCount);
        var rows = spec.RowLabels is { } labels
            ? labels.Select((label, i) => new TableRowDto(RowId(i)) { Label = label }).ToList()
            : Enumerable.Range(0, rowsShown).Select(i => new TableRowDto(RowId(i))).ToList();
        return new TablePayload(Kind, ShapeName(spec.Shape),
            spec.Columns.Select((c, j) => new TableColumnDto(ColumnId(j), c.Label) { Key = c.Key }).ToList(), rows) {
            Title = spec.Title,
            RowAxis = spec.RowAxis,
            ColumnAxis = spec.ColumnAxis,
            Blank = blank == null ? null : new TableBlank(CheckedValues(spec, blank)),
        };
    }

    // ---- Frames ----

    private static IReadOnlyList<IReadOnlyList<string?>> CheckedValues(TableSpec spec, TableDraft draft) {
        foreach (var row in draft.Values)
            if (row.Length != spec.Columns.Count)
                throw new InvalidOperationException($"A table row has {row.Length} cells but the table has {spec.Columns.Count} columns.");
        if (spec.RowLabels is { } labels && draft.Values.Count > labels.Count)
            throw new InvalidOperationException($"A frame shows {draft.Values.Count} rows but the table has {labels.Count}.");
        return draft.Values.Select(r => (IReadOnlyList<string?>)r.ToArray()).ToList();
    }

    /// <summary>
    /// The frame for one step. <paramref name="incremental"/> says whether the step comes from an incremental run
    /// (<see cref="SolveRun.Incremental"/>): then a Reject's focus is what failed, so <c>Rejected</c>.
    /// </summary>
    public static TableFrame Frame(TableSpec spec, TableDraft draft, StepEvent ev, string caption, bool? ok = null, bool incremental = false) {
        var values = CheckedValues(spec, draft);
        var rows = new Dictionary<string, PictureState>(draft.RowStates, StringComparer.Ordinal);
        var cells = new Dictionary<string, PictureState>(draft.CellStates, StringComparer.Ordinal);
        var mark = incremental ? GraphSubsetFrames.FocusMark(ev) : PictureState.ElementHighlight;
        foreach (var id in draft.FocusIds) {
            if (ParseCellId(id) is not null) GraphSubsetFrames.Raise(cells, id, mark);
            else GraphSubsetFrames.Raise(rows, id, mark);
        }
        return new TableFrame(ev, caption, draft.FocusIds.ToArray(), values, GraphSubsetFrames.ToRefs(rows), GraphSubsetFrames.ToRefs(cells),
            draft.ChangedCells.ToArray()) {
            Arrows = spec.Shape == TableShape.Grid ? draft.ArrowList.ToList() : null,
            Ok = ok,
        };
    }

    /// <summary>
    /// The frame that closes a run stopped by its time limit (<see cref="SolveRun.TimedOut"/>): a Done frame with ok = false
    /// showing <paramref name="blank"/> (the instance with nothing worked out), every element Background, nothing changed
    /// and no arrows. The solver had not finished, so nothing is an answer or judged.
    /// </summary>
    public static TableFrame StoppedFrame(TableSpec spec, TableDraft blank, string caption) =>
        new(StepEvent.Done, caption, [], CheckedValues(spec, blank), new SortedDictionary<string, StateRef>(StringComparer.Ordinal),
            new SortedDictionary<string, StateRef>(StringComparer.Ordinal), []) {
            Arrows = spec.Shape == TableShape.Grid ? [] : null,
            Ok = false,
        };

    // ---- The whole response ----

    /// <summary>
    /// The whole response: the payload, a frame per kept step, then a solved frame built from the answer unless the last
    /// step already is the Done step. A run stopped by its time limit ends with <see cref="StoppedFrame"/> instead, and its
    /// answer string (<see cref="SolveRun.TimedOutAnswer"/>) is never read. The payload carries the blank (the base picture
    /// of the instance, drawn before Run) so a frontend never has to draw a solver's first frame before the run.
    /// </summary>
    /// <param name="spec">The table's shape, columns and fixed rows.</param>
    /// <param name="run">The solver's run.</param>
    /// <param name="draw">One draft per step given, in order.</param>
    /// <param name="blank">The instance with nothing worked out: the payload's blank, and the closing frame of a stopped
    /// run.</param>
    /// <param name="solved">The answer drawn without steps, or null to show <paramref name="blank"/>.</param>
    /// <param name="verifyAnswer">The problem's verifier on an answer.</param>
    /// <param name="start">Optional: the instance with nothing worked out as the run's steps write it (a trace table whose
    /// start row depends on the solver), or null to use <paramref name="blank"/>. Used in place of
    /// <paramref name="blank"/> everywhere.</param>
    public static FramesResponse Build<TPartial>(TableSpec spec, SolveRun run,
        Func<IReadOnlyList<SolverStep<TPartial>>, IReadOnlyList<TableDraft>> draw, Func<TableDraft> blank,
        Func<string, TableDraft?> solved, Func<string, bool> verifyAnswer,
        Func<IReadOnlyList<SolverStep<TPartial>>, TableDraft?>? start = null) {
        var steps = run.StepsFor(typeof(TPartial)).OfType<SolverStep<TPartial>>().ToList();
        SolverStep<TPartial>? stoppedDone = run.TimedOut && steps.Count > 0 && steps[^1].Event == StepEvent.Done ? steps[^1] : null;
        if (stoppedDone != null) steps.RemoveAt(steps.Count - 1);
        TableDraft Blank() => start?.Invoke(steps) ?? blank();

        var drafts = steps.Count > 0 ? draw(steps) : [];
        if (drafts.Count != steps.Count)
            throw new InvalidOperationException($"The table drew {drafts.Count} frames for {steps.Count} steps.");

        var frames = new List<TableFrame>();
        for (int i = 0; i < steps.Count; i++)
            frames.Add(Frame(spec, drafts[i], steps[i].Event, steps[i].Caption, steps[i].Ok, run.Incremental) with { Step = i });

        if (run.TimedOut) {
            // The stopped run's own Done step (taken off the end of the steps above) keeps its index; an added stopped frame has none.
            frames.Add(StoppedFrame(spec, Blank(), stoppedDone?.Caption ?? GraphSubsetFrames.StoppedCaption) with { Step = stoppedDone == null ? null : steps.Count });
        } else if (steps.Count == 0 || steps[^1].Event != StepEvent.Done) {
            bool ok = verifyAnswer(run.Answer);
            var draft = (ok ? solved(run.Answer) : null) ?? Blank();
            frames.Add(Frame(spec, draft, StepEvent.Done, ok ? $"The answer is {run.Answer}." : "No answer was found.", ok));
        }
        return new FramesResponse(nameof(VisualizationType.Table), Payload(spec, frames.Count == 0 ? 0 : frames.Max(f => f.Values.Count), Blank()),
            frames.Cast<object>().ToList());
    }

    // ---- The list format ----

    /// <summary>
    /// One frame as the list format's <see cref="API_TableJSON"/> (the legacy <c>DynamicTable</c>), for a visualization
    /// that has no older list output to keep. A table with row headers gets a first column, <c>row</c>, holding them. Column
    /// keys are the spec's keys, or the column ids; empty cells are <c>""</c>; row and cell states become the row's
    /// <c>color</c> and its <c>cellColors</c>.
    /// </summary>
    public static API_TableJSON ToListTable(TableSpec spec, TableFrame frame) {
        var table = new API_TableJSON { title = spec.Title };
        if (spec.RowLabels != null) table.columns.Add(new TableColumn { key = "row", label = spec.RowAxis ?? "" });
        var keys = spec.Columns.Select((c, j) => c.Key ?? ColumnId(j)).ToList();
        for (int j = 0; j < keys.Count; j++) table.columns.Add(new TableColumn { key = keys[j], label = spec.Columns[j].Label });
        for (int i = 0; i < frame.Values.Count; i++) {
            var cells = new Dictionary<string, string>();
            if (spec.RowLabels is { } labels) cells["row"] = labels[i];
            var colors = new Dictionary<string, string>();
            for (int j = 0; j < keys.Count; j++) {
                cells[keys[j]] = frame.Values[i][j] ?? "";
                if (frame.Cells.TryGetValue(CellId(i, j), out var state)) colors[keys[j]] = state.State.ToString();
            }
            table.rows.Add(new TableRow {
                id = RowId(i),
                cells = cells,
                color = frame.Rows.TryGetValue(RowId(i), out var row) ? row.State.ToString() : null,
                cellColors = colors.Count > 0 ? colors : null,
            });
        }
        return table;
    }

    // ---- Captions ----

    /// <summary>A number as captions and cells write it.</summary>
    public static string Num(int n) => n.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// A visualization drawn as the Table picture. The visualization declares what the table is (<see cref="Describe"/>), how
/// each recorded step looks (<see cref="Draw"/>) and the instance with nothing worked out (<see cref="Blank"/>, the base
/// picture the payload carries); the frames hook then comes for free. Implement members explicitly so nothing new appears
/// in /info. The class must also declare
/// <c>Type? IVisualization.StepShape =&gt; typeof(TPartial)</c>.
/// </summary>
interface ITableVisualization<TProblem, TPartial> : IFramesVisualization where TProblem : IProblem {
    TableSpec Describe(TProblem problem);

    /// <summary>One draft per step, in order. <paramref name="incremental"/> is <see cref="SolveRun.Incremental"/>. A run
    /// stopped by its time limit is given its steps without the closing Done.</summary>
    IReadOnlyList<TableDraft> Draw(TProblem problem, IReadOnlyList<SolverStep<TPartial>> steps, bool incremental);

    /// <summary>The instance with nothing worked out and no states: the base picture the payload carries as its blank (what a
    /// frontend draws before Run), and the frame that closes a stopped run.</summary>
    TableDraft Blank(TProblem problem);

    /// <summary>The instance with nothing worked out as the run's steps write it, or null to use <see cref="Blank"/>: for a
    /// trace table whose start row depends on the solver (an NFA's subset simulation starts in its ε-closure).</summary>
    TableDraft? Start(TProblem problem, IReadOnlyList<SolverStep<TPartial>> steps) => null;

    /// <summary>The answer drawn without steps (used only when the run's steps cannot be drawn), or null to show
    /// <see cref="Blank"/> with the answer in the caption.</summary>
    TableDraft? Solved(TProblem problem, string answer) => null;

    FramesResponse IFramesVisualization.BuildFrames(string instance, SolveRun run) {
        TProblem problem = ParseGuard.CreateProblem<TProblem>(instance);
        return TableFrames.Build<TPartial>(Describe(problem), run,
            steps => Draw(problem, steps, run.Incremental),
            () => Blank(problem),
            answer => Solved(problem, answer),
            answer => {
                // The problem's own verifier decides; a certificate it cannot read is not correct.
                try { return problem.defaultVerifier.verify(instance, answer); } catch { return false; }
            },
            steps => Start(problem, steps));
    }
}
