using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Board;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Board picture's base on its own: payload, square ids, clues, the states of each frame, conflicts and their links, the
// answer-only Done frame, a stopped run and a capped one. The solvers' steps and the endpoint are tested in
// BoardSolvers_Tests and BoardFrames_Endpoint_Tests.
public class BoardFrames_Tests {
    // A 4 x 4 Sudoku-like board with boxes of 2 and two clues; small enough to read every state.
    private static readonly BoardSpec Sudoku4 = new(BoardGame.Sudoku, 4) {
        Box = 2, LabelsFrom = 1, Clues = [new BoardClue(0, 0, "1"), new BoardClue(1, 2, "3")],
    };

    private static readonly BoardSpec Queens4 = new(BoardGame.Queens, 4) { PieceKind = "queen" };

    private static string Json(object value) => JsonSerializer.Serialize(value, FramesResponse.JsonOptions);

    private static string States(IReadOnlyDictionary<string, StateRef> map) => string.Join(" ", map.Select(p => $"{p.Key}:{p.Value.State}"));

    private static BoardCells B(params (string Id, string Value)[] values) =>
        new(values.Select(v => new KeyValuePair<string, string>(v.Id, v.Value)), []);

    private static BoardCells B(BoardConflict[] conflicts, params (string Id, string Value)[] values) =>
        new(values.Select(v => new KeyValuePair<string, string>(v.Id, v.Value)), conflicts);

    // ---- Ids ----

    [Theory]
    [InlineData(0, 0, "r0c0")]
    [InlineData(8, 5, "r8c5")]
    [InlineData(12, 10, "r12c10")]
    public void CellIds_ArePositions_AndRoundTrip(int row, int column, string id) {
        Assert.Equal(id, BoardFrames.CellId(row, column));
        Assert.Equal((row, column), BoardFrames.ParseCellId(id));
    }

    [Theory]
    [InlineData("r0")]
    [InlineData("c0r0")]
    [InlineData("rc0")]
    [InlineData("r-1c0")]
    [InlineData("r1c")]
    [InlineData("I1")]
    public void ParseCellId_RefusesWhatIsNotASquareId(string id) => Assert.Null(BoardFrames.ParseCellId(id));

    // ---- Payload ----

    [Fact]
    public void Payload_Sudoku_HasItsSizeBoxesLabelsAndClues() {
        var payload = BoardFrames.Payload(Sudoku4);
        Assert.Equal(("board", "sudoku", 4, 2, 1), (payload.Kind, payload.Game, payload.Size, payload.Box, payload.LabelsFrom));
        Assert.Equal(["r0c0 0 0 1", "r1c2 1 2 3"], payload.Clues.Select(c => $"{c.Id} {c.R} {c.C} {c.Value}"));
    }

    [Fact]
    public void Payload_Json_HasTheContractFieldsInOrder_AndABoxOnlyWhenThereAreBoxes() {
        using var sudoku = JsonDocument.Parse(Json(BoardFrames.Payload(Sudoku4)));
        Assert.Equal(["kind", "game", "size", "box", "labelsFrom", "clues"], sudoku.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal(["id", "r", "c", "value"], sudoku.RootElement.GetProperty("clues")[0].EnumerateObject().Select(p => p.Name));
        using var queens = JsonDocument.Parse(Json(BoardFrames.Payload(Queens4)));
        Assert.Equal(["kind", "game", "size", "labelsFrom", "clues"], queens.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal("{\"kind\":\"board\",\"game\":\"queens\",\"size\":4,\"labelsFrom\":0,\"clues\":[]}",
            JsonSerializer.Serialize(JsonDocument.Parse(Json(BoardFrames.Payload(Queens4))).RootElement));
    }

    // ---- Frames ----

    [Fact]
    public void Frame_Json_HasTheContractFieldsInOrder_AndOkOnlyOnDone() {
        var frame = BoardFrames.Frame(Sudoku4, B(("r0c0", "1"), ("r0c1", "2")), StepEvent.Try, ["r0c1"], "Try 2.", incremental: true);
        using var doc = JsonDocument.Parse(Json(frame));
        Assert.Equal(["event", "caption", "focus", "values", "pieces", "cells", "links"], doc.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal("Try", doc.RootElement.GetProperty("event").GetString());
        var done = BoardFrames.Frame(Sudoku4, B(), StepEvent.Done, [], "No.", ok: false, incremental: true);
        Assert.False(JsonDocument.Parse(Json(done)).RootElement.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public void Frame_Values_HoldTheCluesInEveryFrame_AndThePlacedDigits() {
        var frame = BoardFrames.Frame(Sudoku4, B(("r0c1", "2")), StepEvent.Try, ["r0c1"], "c", incremental: true);
        Assert.Equal(["r0c0=1", "r0c1=2", "r1c2=3"], frame.Values.Select(p => $"{p.Key}={p.Value}"));
        Assert.Empty(frame.Pieces);
        // The empty board still shows its clues.
        Assert.Equal(["r0c0=1", "r1c2=3"], BoardFrames.Frame(Sudoku4, B(), StepEvent.Done, [], "c", ok: false).Values.Select(p => $"{p.Key}={p.Value}"));
    }

    [Fact]
    public void Frame_Incremental_KeepsWhatIsPlacedSolution_AndOnlyTheTriedSquareHighlighted() {
        var frame = BoardFrames.Frame(Sudoku4, B(("r0c0", "1"), ("r0c1", "2"), ("r0c2", "4")), StepEvent.Try, ["r0c2"], "c", incremental: true);
        // The clue r0c0 is fixed: never Solution.
        Assert.Equal("r0c1:Solution r0c2:ElementHighlight", States(frame.Cells));
    }

    [Fact]
    public void Frame_IncrementalReject_MarksTheFailedSquareRejected() {
        var frame = BoardFrames.Frame(Sudoku4, B(("r0c1", "2")), StepEvent.Reject, ["r0c2"], "c", incremental: true);
        Assert.Equal("r0c1:Solution r0c2:Rejected", States(frame.Cells));
    }

    [Fact]
    public void Frame_WholeCandidate_IsOnlyHighlightedWhileTried_AndSolutionOnlyInAnAcceptOrTheAnswer() {
        var board = B(("r0c1", "Q"), ("r1c3", "Q"));
        Assert.Equal("r0c1:ElementHighlight r1c3:ElementHighlight", States(BoardFrames.Frame(Queens4, board, StepEvent.Try, [], "c").Cells));
        Assert.Equal("r0c1:ElementHighlight r1c3:ElementHighlight", States(BoardFrames.Frame(Queens4, board, StepEvent.Reject, [], "c").Cells));
        Assert.Equal("r0c1:Solution r1c3:Solution", States(BoardFrames.Frame(Queens4, board, StepEvent.Accept, [], "c").Cells));
        Assert.Equal("r0c1:Solution r1c3:Solution", States(BoardFrames.Frame(Queens4, board, StepEvent.Done, [], "c", ok: true).Cells));
        // A claimed answer that is wrong failed.
        Assert.Equal("r0c1:Rejected r1c3:Rejected", States(BoardFrames.Frame(Queens4, board, StepEvent.Done, [], "c", ok: false).Cells));
    }

    [Fact]
    public void Frame_Pieces_AreDrawnAsPieces_InReadingOrder_NotAsText() {
        var frame = BoardFrames.Frame(Queens4, B(("r2c0", "Q"), ("r0c1", "Q"), ("r1c3", "Q")), StepEvent.Try, ["r2c0"], "c", incremental: true);
        Assert.Equal(["r0c1 0 1 queen", "r1c3 1 3 queen", "r2c0 2 0 queen"], frame.Pieces.Select(p => $"{p.Id} {p.R} {p.C} {p.Kind}"));
        Assert.Empty(frame.Values);
        Assert.Equal("r0c1:Solution r1c3:Solution r2c0:ElementHighlight", States(frame.Cells));
        using var doc = JsonDocument.Parse(Json(frame));
        Assert.Equal(["id", "r", "c", "kind"], doc.RootElement.GetProperty("pieces")[0].EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public void Frame_Conflicts_AreRejectedSquares_WithALinkToWhatRulesThemOut() {
        // Row 1: columns 0 and 1 are attacked by the queen at (0,0) and (0,0); the queen goes to column 2.
        var board = B([new BoardConflict("r1c0", "r0c0"), new BoardConflict("r1c1", "r0c0")], ("r0c0", "Q"), ("r1c2", "Q"));
        var frame = BoardFrames.Frame(Queens4, board, StepEvent.Try, ["r1c2"], "c", incremental: true);
        Assert.Equal("r0c0:Solution r1c0:Rejected r1c1:Rejected r1c2:ElementHighlight", States(frame.Cells));
        Assert.Equal(["r1c0>r0c0 Rejected", "r1c1>r0c0 Rejected"], frame.Links.Select(l => $"{l.From}>{l.To} {l.State}"));
        using var doc = JsonDocument.Parse(Json(frame));
        Assert.Equal(["from", "to", "state"], doc.RootElement.GetProperty("links")[0].EnumerateObject().Select(p => p.Name));
        Assert.Equal("Rejected", doc.RootElement.GetProperty("links")[0].GetProperty("state").GetString());
    }

    [Fact]
    public void Frame_ClashingClues_AreRejected_OnlyWhenAStepNamesThem() {
        var spec = Sudoku4 with { Clues = [new BoardClue(0, 0, "1"), new BoardClue(0, 3, "1")] };
        Assert.Empty(BoardFrames.Frame(spec, B(), StepEvent.Try, [], "c", incremental: true).Cells);
        var clash = BoardFrames.Frame(spec, B([new BoardConflict("r0c3", "r0c0")]), StepEvent.Reject, ["r0c0", "r0c3"], "c", incremental: true);
        Assert.Equal("r0c0:Rejected r0c3:Rejected", States(clash.Cells));
        var link = Assert.Single(clash.Links);
        Assert.Equal("r0c3>r0c0", $"{link.From}>{link.To}");
    }

    [Fact]
    public void Frame_IdsOffTheBoard_AreIgnored() {
        var board = B([new BoardConflict("r9c9", "r0c0"), new BoardConflict("r0c1", "x")], ("r4c0", "Q"), ("r0c0", "Q"), ("bad", "Q"));
        var frame = BoardFrames.Frame(Queens4, board, StepEvent.Try, ["r0c7", "nope"], "c", incremental: true);
        Assert.Equal(["r0c0"], frame.Pieces.Select(p => p.Id));
        Assert.Empty(frame.Links);
        Assert.Equal("r0c0:Solution", States(frame.Cells));
    }

    [Fact]
    public void Precedence_RejectedBeatsHighlightBeatsSolution() {
        // The focus is also placed (Solution) and also a conflict (Rejected): Rejected shows.
        var frame = BoardFrames.Frame(Queens4, B([new BoardConflict("r1c1", "r0c0")], ("r0c0", "Q"), ("r1c1", "Q")), StepEvent.Try, ["r1c1"], "c", incremental: true);
        Assert.Equal("r0c0:Solution r1c1:Rejected", States(frame.Cells));
        // Focus on a placed square in a Try: highlighted over Solution.
        Assert.Equal("r0c0:ElementHighlight", States(BoardFrames.Frame(Queens4, B(("r0c0", "Q")), StepEvent.Try, ["r0c0"], "c", incremental: true).Cells));
    }

    // ---- Build ----

    private static SolveRun Run(bool incremental, bool timedOut, params SolverStep<BoardCells>[] steps) =>
        new(timedOut ? SolveRun.TimedOutAnswer : "{(0,1),(1,3),(2,0),(3,2)}", steps, typeof(BoardCells)) { Incremental = incremental, TimedOut = timedOut };

    private static readonly BoardCells Solved4 = B(("r0c1", "Q"), ("r1c3", "Q"), ("r2c0", "Q"), ("r3c2", "Q"));

    [Fact]
    public void Build_AFramePerStep_EndingWithTheDoneStep() {
        var run = Run(true, false,
            new SolverStep<BoardCells>(B(("r0c0", "Q")), StepEvent.Try, ["r0c0"], "Row 0: column 0 is safe. Place the queen there."),
            new SolverStep<BoardCells>(Solved4, StepEvent.Done, [], "All 4 queens are placed.") { Ok = true });
        var response = BoardFrames.Build(Queens4, run, _ => throw new InvalidOperationException("not read"), _ => throw new InvalidOperationException("not read"));
        Assert.Equal("Board", response.Type);
        var frames = response.Frames.Cast<BoardFrame>().ToList();
        Assert.Equal([StepEvent.Try, StepEvent.Done], frames.Select(f => f.Event));
        Assert.Equal("r0c1:Solution r1c3:Solution r2c0:Solution r3c2:Solution", States(frames[1].Cells));
        Assert.True(frames[1].Ok);
    }

    [Fact]
    public void Build_WithoutDrawableSteps_AddsTheAnswer_OrABlankBoard() {
        var legacy = new SolveRun("{(0,1),(1,3),(2,0),(3,2)}", ["legacy"], null);
        var done = (BoardFrame)Assert.Single(BoardFrames.Build(Queens4, legacy, _ => Solved4, _ => true).Frames);
        Assert.Equal((StepEvent.Done, true, "The answer is {(0,1),(1,3),(2,0),(3,2)}."), (done.Event, done.Ok, done.Caption));
        Assert.Equal(4, done.Pieces.Count);
        var blank = (BoardFrame)Assert.Single(BoardFrames.Build(Sudoku4, new SolveRun("{}", [], null), _ => throw new InvalidOperationException(), _ => false).Frames);
        Assert.Equal(("No answer was found.", false), (blank.Caption, blank.Ok));
        Assert.Empty(blank.Cells);
        Assert.Equal(["r0c0", "r1c2"], blank.Values.Keys);
    }

    [Fact]
    public void Build_AStoppedRun_EndsWithTheCluesOnly_NotOk_AndNeverReadsItsAnswer() {
        var run = Run(true, true,
            new SolverStep<BoardCells>(B(("r0c0", "1"), ("r0c1", "2")), StepEvent.Try, ["r0c1"], "Try 2."),
            new SolverStep<BoardCells>(B(), StepEvent.Done, [], "Stopped after 0.05 s and 1 step; the solver had not finished.") { Ok = false });
        var frames = BoardFrames.Build(Sudoku4, run, _ => throw new InvalidOperationException("read"), _ => throw new InvalidOperationException("read"))
            .Frames.Cast<BoardFrame>().ToList();
        Assert.Equal(2, frames.Count);
        var done = frames[^1];
        Assert.Equal((StepEvent.Done, false), (done.Event, done.Ok));
        Assert.StartsWith("Stopped after", done.Caption);
        Assert.Equal(["r0c0=1", "r1c2=3"], done.Values.Select(p => $"{p.Key}={p.Value}"));
        Assert.Empty(done.Cells);
        Assert.Empty(done.Pieces);
        Assert.Empty(done.Links);
        Assert.Empty(done.Focus);

        // Steps of another shape cannot be drawn: the stopped frame is added, with the generic caption.
        var other = new SolveRun(SolveRun.TimedOutAnswer, [], typeof(NodeSet)) { TimedOut = true };
        var added = (BoardFrame)Assert.Single(BoardFrames.Build(Sudoku4, other, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException()).Frames);
        Assert.Equal(GraphSubsetFrames.StoppedCaption, added.Caption);
        Assert.Equal(false, added.Ok);
    }

    [Fact]
    public void Build_ACappedRun_KeepsTheFirstStepsThenShowsTheFinishedBoard() {
        // 400 tries through a recorder with the default cap: 150 are kept, then the Done step with the whole board.
        var rec = new StepRecorder<BoardCells>();
        for (int i = 0; i < 400; i++) {
            int k = i;
            rec.Try(() => B(("r0c0", "Q")), () => $"Try {k}.", () => ["r0c0"]);
        }
        rec.Done(Solved4, true, "All 4 queens are placed.");
        var run = new SolveRun("{(0,1),(1,3),(2,0),(3,2)}", rec.Steps.Cast<object>().ToList(), typeof(BoardCells)) { Incremental = true };
        var frames = BoardFrames.Build(Queens4, run, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException())
            .Frames.Cast<BoardFrame>().ToList();
        Assert.Equal(StepRecorder<BoardCells>.DefaultCap + 1, frames.Count);
        Assert.Equal("Try 149.", frames[^2].Caption);
        Assert.Equal("All 4 queens are placed. (250 not shown)", frames[^1].Caption);
        Assert.Equal(4, frames[^1].Pieces.Count);
        Assert.All(frames[^1].Cells.Values, s => Assert.Equal(PictureState.Solution, s.State));
    }

    // ---- The shape ----

    [Fact]
    public void BoardCells_HasValueEquality_AndAnEmptyAnswer() {
        Assert.Equal(B(("r0c1", "Q"), ("r1c3", "Q")), B(("r1c3", "Q"), ("r0c1", "Q")));
        Assert.NotEqual(B(("r0c1", "Q")), B(("r0c1", "2")));
        Assert.NotEqual(B(("r0c1", "Q")), B([new BoardConflict("r1c1", "r0c1")], ("r0c1", "Q")));
        Assert.Equal(B(), Shapes.EmptyAnswer(typeof(BoardCells)));
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(B([new BoardConflict("r1c1", "r0c1")], ("r0c1", "Q"))));
        Assert.Equal("{\"values\":{\"r0c1\":\"Q\"},\"conflicts\":[{\"cell\":\"r1c1\",\"with\":\"r0c1\"}]}", JsonSerializer.Serialize(doc.RootElement));
    }
}
