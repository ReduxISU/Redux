using System.Globalization;
using System.Text.Json.Serialization;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;

namespace API.Interfaces.Board;

/// <summary>Which puzzle a board holds; it decides how a frontend draws the squares (boxes or a checkerboard).</summary>
enum BoardGame {
    /// <summary>Digits in squares, with clues given, thick lines around the boxes.</summary>
    Sudoku,
    /// <summary>A chessboard with queens on it.</summary>
    Queens,
}

/// <summary>A square the instance fills in: a Sudoku clue.</summary>
/// <param name="Row">0-based row.</param>
/// <param name="Column">0-based column.</param>
/// <param name="Value">What the square holds, as the instance writes it.</param>
sealed record BoardClue(int Row, int Column, string Value);

/// <summary>
/// What a puzzle hands the Board picture: the game, the board's size and, for Sudoku, the box size and the clues. No ids, no
/// states; the base derives those.
/// </summary>
/// <param name="Game">The puzzle.</param>
/// <param name="Size">Squares per side.</param>
sealed record BoardSpec(BoardGame Game, int Size) {
    /// <summary>The side of a box (Sudoku's 3), or null for a board without boxes.</summary>
    public int? Box { get; init; }
    /// <summary>The number the row and column labels start from: 1 for Sudoku (R1C1), 0 for N-Queens (its certificate counts
    /// from 0).</summary>
    public int LabelsFrom { get; init; }
    /// <summary>The squares the instance fills in, which a solver never changes.</summary>
    public IReadOnlyList<BoardClue> Clues { get; init; } = [];
    /// <summary>The kind of piece a solver puts down (<c>queen</c>), or null when it writes text (Sudoku's digits). With a
    /// piece kind, every square a step's partial holds that is not a clue is drawn as that piece.</summary>
    public string? PieceKind { get; init; }
}

// --- JSON shapes (explicit names so the contract does not depend on serializer naming options) ---

/// <summary>A clue: its square's id, row and column, and what it holds.</summary>
sealed record BoardClueDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("r")] int R,
    [property: JsonPropertyName("c")] int C,
    [property: JsonPropertyName("value")] string Value);

/// <summary>
/// The static part of a Board picture, sent once per response: the game, the board's size, the box size (Sudoku only), where
/// the row and column labels start, and the clues. Square <c>(i, j)</c> is <c>r{i}c{j}</c>.
/// </summary>
sealed record BoardPayload(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("game")] string Game,
    [property: JsonPropertyName("size")] int Size) {
    [JsonPropertyName("box")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Box { get; init; }

    [JsonPropertyName("labelsFrom")]
    public int LabelsFrom { get; init; }

    [JsonPropertyName("clues")]
    public IReadOnlyList<BoardClueDto> Clues { get; init; } = [];
}

/// <summary>A piece on a square: its square's id, row and column, and its kind (<c>queen</c>).</summary>
sealed record BoardPieceDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("r")] int R,
    [property: JsonPropertyName("c")] int C,
    [property: JsonPropertyName("kind")] string Kind);

/// <summary>A link from a square that was ruled out to the square that rules it out, drawn dashed in its state's color.</summary>
sealed record BoardLinkDto(
    [property: JsonPropertyName("from")] string From,
    [property: JsonPropertyName("to")] string To,
    [property: JsonPropertyName("state")] PictureState State);

/// <summary>
/// One picture of the board.
/// <list type="bullet">
/// <item><c>values</c>: the text of every square that holds text, clues included (Sudoku's digits), keyed by square id.</item>
/// <item><c>pieces</c>: the pieces on the board (N-Queens' queens).</item>
/// <item><c>cells</c>: the state of each square whose state is not <see cref="PictureState.Background"/>. A piece takes its
/// square's state.</item>
/// <item><c>links</c>: from each square this step ruled out to the square that rules it out.</item>
/// </list>
/// </summary>
sealed record BoardFrame(
    [property: JsonPropertyName("event")] StepEvent Event,
    [property: JsonPropertyName("caption")] string Caption,
    [property: JsonPropertyName("focus")] string[] Focus,
    [property: JsonPropertyName("values")] IReadOnlyDictionary<string, string> Values,
    [property: JsonPropertyName("pieces")] IReadOnlyList<BoardPieceDto> Pieces,
    [property: JsonPropertyName("cells")] IReadOnlyDictionary<string, StateRef> Cells,
    [property: JsonPropertyName("links")] IReadOnlyList<BoardLinkDto> Links) {
    /// <summary>The solver step this frame was drawn from (see <see cref="FramesResponse"/>, #895); null for an added frame. Always written.</summary>
    [JsonPropertyName("step")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int? Step { get; init; }

    [JsonPropertyName("ok")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Ok { get; init; }
}

/// <summary>
/// Derives the Board picture (Sudoku, N-Queens) from (spec, a step's <see cref="BoardCells"/>, step event, focus). Nothing
/// here knows about a particular puzzle or solver.
/// <para>
/// <b>Ids.</b> The square in row <c>i</c> and column <c>j</c> (0-based) is <c>r{i}c{j}</c>. Ids are positions, never
/// contents, so equal digits stay distinct squares and no id needs escaping.
/// </para>
/// <para>
/// <b>States</b> (precedence <c>Rejected &gt; ElementHighlight &gt; Solution &gt; Committed &gt; Covered &gt; Blocked &gt; Background</c>; see
/// <see cref="GraphSubsetFrames.Raise"/>):
/// </para>
/// <list type="bullet">
/// <item>A square the solver filled (a digit or a piece that is not a clue) is <c>Solution</c> only once the board is
/// confirmed as the answer: in the Done frame with ok = true, or an Accept that ends the run (#839). Before that it is
/// <c>Committed</c>, placed so far but not confirmed (#886): in every step of an incremental run
/// (<see cref="SolveRun.Incremental"/>) and in an Accept or Backtrack the run goes on from, so Solution colour never shows
/// while the solver is still searching. A whole candidate that is only being tried (Try, Reject of a run that is not
/// incremental) is <c>ElementHighlight</c>. In a Done frame with ok = false it is <c>Rejected</c>
/// (<see cref="GraphSubsetFrames.Picked"/>).</item>
/// <item>A clue is fixed: it is never <c>Solution</c>, and is <c>Rejected</c> only when a step names it.</item>
/// <item>A square a step ruled out (a conflict's square) is <c>Rejected</c>, with a <c>Rejected</c> link to the square that
/// rules it out.</item>
/// <item>The focus is <c>ElementHighlight</c>; in a Reject step of an incremental run it is what failed, so <c>Rejected</c>
/// (<see cref="GraphSubsetFrames.FocusMark"/>).</item>
/// <item>A run stopped by its time limit ends with a Done frame, ok = false, showing only the clues, with every square
/// Background (<see cref="StoppedFrame"/>).</item>
/// </list>
/// </summary>
static class BoardFrames {
    public const string Kind = "board";

    // ---- Ids ----

    public static string CellId(int row, int column) => $"r{row}c{column}";

    /// <summary>The (row, column) of a square id made by <see cref="CellId"/>, or null for anything else.</summary>
    public static (int Row, int Column)? ParseCellId(string id) {
        if (id.Length < 4 || id[0] != 'r') return null;
        int c = id.IndexOf('c');
        if (c < 2) return null;
        return int.TryParse(id.AsSpan(1, c - 1), NumberStyles.None, CultureInfo.InvariantCulture, out int row)
            && int.TryParse(id.AsSpan(c + 1), NumberStyles.None, CultureInfo.InvariantCulture, out int column)
            ? (row, column) : null;
    }

    public static string GameName(BoardGame game) => game switch {
        BoardGame.Sudoku => "sudoku",
        BoardGame.Queens => "queens",
        _ => throw new ArgumentOutOfRangeException(nameof(game)),
    };

    // ---- The payload ----

    public static BoardPayload Payload(BoardSpec spec) =>
        new(Kind, GameName(spec.Game), spec.Size) {
            Box = spec.Box,
            LabelsFrom = spec.LabelsFrom,
            Clues = spec.Clues.Select(c => new BoardClueDto(CellId(c.Row, c.Column), c.Row, c.Column, c.Value)).ToList(),
        };

    // ---- Frames ----

    private static Dictionary<string, string> ClueValues(BoardSpec spec) =>
        spec.Clues.ToDictionary(c => CellId(c.Row, c.Column), c => c.Value, StringComparer.Ordinal);

    // Whether an id names a square of this board.
    private static bool OnBoard(BoardSpec spec, string id) =>
        ParseCellId(id) is var (r, c) && r < spec.Size && c < spec.Size;

    /// <summary>
    /// The frame for one step. <paramref name="incremental"/> says whether the step comes from an incremental run
    /// (<see cref="SolveRun.Incremental"/>); <paramref name="endsRun"/> whether it is an Accept that ends the run
    /// (<see cref="GraphSubsetFrames.EndsRun"/>).
    /// </summary>
    public static BoardFrame Frame(BoardSpec spec, BoardCells partial, StepEvent ev, string[] focus, string caption, bool? ok = null, bool incremental = false, bool endsRun = false) {
        var clues = ClueValues(spec);
        var values = new SortedDictionary<string, string>(clues, StringComparer.Ordinal);
        var pieces = new List<BoardPieceDto>();
        var states = new Dictionary<string, PictureState>(StringComparer.Ordinal);

        // The squares the solver filled follow the shared rule (#839, #886); ok is set only on the Done.
        var picked = GraphSubsetFrames.Picked(ev, incremental, endsRun, ok == true);
        foreach (var (id, value) in partial.Values) {
            if (clues.ContainsKey(id) || !OnBoard(spec, id)) continue;
            var (r, c) = ParseCellId(id)!.Value;
            if (spec.PieceKind is { } kind) pieces.Add(new BoardPieceDto(id, r, c, kind));
            else values[id] = value;
            GraphSubsetFrames.Raise(states, id, picked);
        }

        var links = new List<BoardLinkDto>();
        foreach (var conflict in partial.Conflicts) {
            if (!OnBoard(spec, conflict.Cell) || !OnBoard(spec, conflict.With)) continue;
            GraphSubsetFrames.Raise(states, conflict.Cell, PictureState.Rejected);
            links.Add(new BoardLinkDto(conflict.Cell, conflict.With, PictureState.Rejected));
        }

        var mark = incremental ? GraphSubsetFrames.FocusMark(ev) : PictureState.ElementHighlight;
        foreach (var id in focus)
            if (OnBoard(spec, id)) GraphSubsetFrames.Raise(states, id, mark);

        return new BoardFrame(ev, caption, focus, values, pieces.OrderBy(p => p.R).ThenBy(p => p.C).ToList(), GraphSubsetFrames.ToRefs(states), links) {
            Ok = ok,
        };
    }

    /// <summary>
    /// The frame that closes a run stopped by its time limit (<see cref="SolveRun.TimedOut"/>): a Done frame with ok = false
    /// showing only the instance (its clues), every square Background, no pieces and no links. The solver had not finished,
    /// so nothing is an answer or judged.
    /// </summary>
    public static BoardFrame StoppedFrame(BoardSpec spec, string caption) =>
        new(StepEvent.Done, caption, [], new SortedDictionary<string, string>(ClueValues(spec), StringComparer.Ordinal), [],
            new SortedDictionary<string, StateRef>(StringComparer.Ordinal), []) { Ok = false };

    // ---- The whole response ----

    /// <summary>
    /// The whole response: the payload, a frame per kept step, then a solved frame built from the answer unless the last
    /// step already is the Done step. A run stopped by its time limit ends with <see cref="StoppedFrame"/> instead, and its
    /// answer string (<see cref="SolveRun.TimedOutAnswer"/>) is never read.
    /// </summary>
    public static FramesResponse Build(BoardSpec spec, SolveRun run, Func<string, BoardCells> parseAnswer, Func<string, bool> verifyAnswer) {
        var frames = new List<object>();
        SolverStep<BoardCells>? last = null;
        foreach (var (step, endsRun, index) in GraphSubsetFrames.DrawnSteps<BoardCells>(run)) {
            frames.Add((run.TimedOut && step.Event == StepEvent.Done
                ? StoppedFrame(spec, step.Caption)
                : Frame(spec, step.Partial, step.Event, step.Focus, step.Caption, step.Ok, run.Incremental, endsRun)) with { Step = index });
            last = step;
        }
        if (run.TimedOut) {
            if (last is not { Event: StepEvent.Done }) frames.Add(StoppedFrame(spec, GraphSubsetFrames.StoppedCaption));
        } else if (last is not { Event: StepEvent.Done }) {
            bool ok = verifyAnswer(run.Answer);
            frames.Add(ok
                ? Frame(spec, parseAnswer(run.Answer), StepEvent.Done, [], $"The answer is {run.Answer}.", true)
                : Frame(spec, new BoardCells([], []), StepEvent.Done, [], "No answer was found.", false));
        }
        return new FramesResponse(nameof(VisualizationType.Board), Payload(spec), frames);
    }

    // ---- Captions ----

    /// <summary>A number as captions write it, with thousands separators (1,234).</summary>
    public static string Count(int n) => n.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>"a", "a or b", "a, b or c" for captions.</summary>
    public static string OrList(IReadOnlyList<string> items) =>
        items.Count <= 2 ? string.Join(" or ", items) : string.Join(", ", items.Take(items.Count - 1)) + " or " + items[^1];

    /// <summary>"a", "a and b", "a, b and c" for captions.</summary>
    public static string AndList(IReadOnlyList<string> items) =>
        items.Count <= 2 ? string.Join(" and ", items) : string.Join(", ", items.Take(items.Count - 1)) + " and " + items[^1];
}

/// <summary>
/// A visualization for a puzzle drawn as the Board picture. The visualization declares only what the board is
/// (<see cref="Describe"/>) and how to read an answer as a board (<see cref="ParseAnswer"/>); the frames hook then comes for
/// free. Implement members explicitly so nothing new appears in /info. The class must also declare
/// <c>Type? IVisualization.StepShape =&gt; typeof(BoardCells)</c>.
/// </summary>
interface IBoardVisualization<TProblem> : IFramesVisualization where TProblem : IProblem {
    BoardSpec Describe(TProblem problem);

    /// <summary>Reads a solver's answer string as a board (used only when the run's steps cannot be drawn).</summary>
    BoardCells ParseAnswer(TProblem problem, string answer);

    FramesResponse IFramesVisualization.BuildFrames(string instance, SolveRun run) {
        TProblem problem = ParseGuard.CreateProblem<TProblem>(instance);
        return BoardFrames.Build(Describe(problem), run,
            answer => ParseAnswer(problem, answer),
            answer => {
                // The problem's own verifier decides; a certificate it cannot read is not correct.
                try { return problem.defaultVerifier.verify(instance, answer); } catch { return false; }
            });
    }
}
