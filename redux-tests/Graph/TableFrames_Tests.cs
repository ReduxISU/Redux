using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Interfaces.Table;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Table picture's base on its own: payload, position ids (labels with separators and repeated letters stay distinct),
// the states of each frame, the answer-only Done frame and a stopped run. The solvers' steps and the endpoint are tested in
// TableSolvers_Tests and TableFrames_Endpoint_Tests.
public class TableFrames_Tests {
    private static string Json(object value) => JsonSerializer.Serialize(value, FramesResponse.JsonOptions);

    private static string Compact(object value) => JsonSerializer.Serialize(JsonDocument.Parse(Json(value)).RootElement);

    private static string States(IReadOnlyDictionary<string, StateRef> map) => string.Join(" ", map.Select(p => $"{p.Key}:{p.Value.State}"));

    private static readonly TableSpec Grid = new(TableShape.Grid, [new("ε"), new("a"), new("a")]) {
        RowLabels = ["ε", "a-b", "r0c1"],
        RowAxis = "source x",
        ColumnAxis = "target y",
    };

    private static readonly TableSpec Trace = new(TableShape.Trace, [new("Step", "step"), new("To", "to")]);

    private static TableDraft Rows(int count, int columns) {
        var draft = new TableDraft();
        for (int i = 0; i < count; i++) draft.AddRow(Enumerable.Range(0, columns).Select(j => (string?)$"{i}.{j}").ToArray());
        return draft;
    }

    // ---- Ids and payload ----

    [Fact]
    public void Ids_ArePositions_AndCellIdsReadBack() {
        Assert.Equal(("r0", "c3", "r12c3"), (TableFrames.RowId(0), TableFrames.ColumnId(3), TableFrames.CellId(12, 3)));
        Assert.Equal((12, 3), TableFrames.ParseCellId("r12c3"));
        Assert.Null(TableFrames.ParseCellId("r12"));
        Assert.Null(TableFrames.ParseCellId("c3"));
        Assert.Null(TableFrames.ParseCellId("rxc3"));
        Assert.Null(TableFrames.ParseCellId("r1c-3"));
    }

    [Fact]
    public void Payload_Grid_LabelsRowsAndColumns_ByPosition_WhateverTheLabelsSay() {
        var payload = TableFrames.Payload(Grid, rowsShown: 0);
        Assert.Equal(
            "{\"kind\":\"table\",\"shape\":\"grid\",\"columns\":[{\"id\":\"c0\",\"label\":\"\\u03B5\"},{\"id\":\"c1\",\"label\":\"a\"},{\"id\":\"c2\",\"label\":\"a\"}],"
            + "\"rows\":[{\"id\":\"r0\",\"label\":\"\\u03B5\"},{\"id\":\"r1\",\"label\":\"a-b\"},{\"id\":\"r2\",\"label\":\"r0c1\"}],"
            + "\"rowAxis\":\"source x\",\"columnAxis\":\"target y\"}",
            Compact(payload));
    }

    [Fact]
    public void Payload_Trace_HasAsManyUnlabelledRowsAsTheLongestFrame_AndColumnKeys() {
        var payload = TableFrames.Payload(Trace, rowsShown: 2);
        Assert.Equal(
            "{\"kind\":\"table\",\"shape\":\"trace\",\"columns\":[{\"id\":\"c0\",\"label\":\"Step\",\"key\":\"step\"},{\"id\":\"c1\",\"label\":\"To\",\"key\":\"to\"}],"
            + "\"rows\":[{\"id\":\"r0\"},{\"id\":\"r1\"}]}",
            Compact(payload));
    }

    // ---- Frames ----

    [Fact]
    public void Frame_HasEveryFieldInOrder_EmptyCellsAreNull_AndArrowsOnlyInAGrid() {
        var draft = new TableDraft();
        draft.AddRow("0", "1", "2");
        draft.AddRow("1", null, null);
        draft.AddRow(null, null, null);
        draft.FocusCell(1, 1);
        draft.AddArrow("r0c0", "r1c1", true);
        draft.AddArrow("r0c1", "r1c1", false);
        draft.MarkChanged(1, 1);
        var frame = TableFrames.Frame(Grid, draft, StepEvent.Try, "Fill (1,1).");
        Assert.Equal(
            "{\"event\":\"Try\",\"caption\":\"Fill (1,1).\",\"focus\":[\"r1c1\"],\"values\":[[\"0\",\"1\",\"2\"],[\"1\",null,null],[null,null,null]],"
            + "\"rows\":{},\"cells\":{\"r1c1\":{\"state\":\"ElementHighlight\"}},\"changed\":[\"r1c1\"],"
            + "\"arrows\":[{\"from\":\"r0c0\",\"to\":\"r1c1\",\"best\":true},{\"from\":\"r0c1\",\"to\":\"r1c1\",\"best\":false}]}",
            Compact(frame));

        var trace = Rows(1, 2);
        trace.AddArrow("r0c0", "r0c1", true);
        var traceFrame = TableFrames.Frame(Trace, trace, StepEvent.Done, "Done.", ok: true);
        Assert.Equal(["event", "caption", "focus", "values", "rows", "cells", "changed", "ok"],
            JsonDocument.Parse(Json(traceFrame)).RootElement.EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public void Frame_KeepsTheStrongerState_AndOmitsBackground() {
        var draft = Rows(3, 2);
        draft.MarkRow(0, PictureState.Solution);
        draft.MarkRow(0, PictureState.Covered);
        draft.MarkRow(1, PictureState.Covered);
        draft.MarkRow(1, PictureState.Rejected);
        draft.MarkRow(2, PictureState.Background);
        draft.MarkCell(2, 1, PictureState.Untraveled);
        draft.MarkCell(2, 1, PictureState.Solution);
        var frame = TableFrames.Frame(Trace, draft, StepEvent.Accept, "x");
        Assert.Equal("r0:Solution r1:Rejected", States(frame.Rows));
        Assert.Equal("r2c1:Solution", States(frame.Cells));
    }

    [Theory]
    [InlineData("Try", false, "ElementHighlight")]
    [InlineData("Reject", false, "ElementHighlight")]
    [InlineData("Try", true, "ElementHighlight")]
    [InlineData("Reject", true, "Rejected")]
    [InlineData("Accept", true, "ElementHighlight")]
    public void Frame_TheFocusIsHighlighted_OrRejectedWhenAnIncrementalRunRejectsIt(string eventName, bool incremental, string state) {
        var ev = Enum.Parse<StepEvent>(eventName);
        var draft = Rows(2, 2);
        draft.MarkRow(1, PictureState.Solution);
        draft.FocusRow(1);
        draft.FocusCell(0, 1);
        var frame = TableFrames.Frame(Trace, draft, ev, "x", incremental: incremental);
        Assert.Equal(["r1", "r0c1"], frame.Focus);
        Assert.Equal($"r1:{state}", States(frame.Rows));
        Assert.Equal($"r0c1:{state}", States(frame.Cells));
    }

    [Fact]
    public void Frame_ARowOfTheWrongWidth_OrTooManyRows_IsAProgrammingError() {
        var narrow = new TableDraft();
        narrow.AddRow("only one");
        Assert.Throws<InvalidOperationException>(() => TableFrames.Frame(Trace, narrow, StepEvent.Try, "x"));
        Assert.Throws<InvalidOperationException>(() => TableFrames.Frame(Grid, Rows(4, 3), StepEvent.Try, "x"));
    }

    // ---- Build ----

    private static SolverStep<TableCells> Step(StepEvent ev, string caption, bool? ok = null) =>
        new(new TableCells([], [], []), ev, [], caption) { Ok = ok };

    // A draft per step that shows one more row each time and highlights the newest.
    private static IReadOnlyList<TableDraft> Growing(IReadOnlyList<SolverStep<TableCells>> steps) =>
        steps.Select((_, i) => {
            var d = Rows(i + 1, 2);
            d.FocusRow(i);
            return d;
        }).ToList();

    private static TableDraft Blank() => new();

    [Fact]
    public void Build_OneFramePerStep_AndThePayloadHasEveryRowAnyFrameShows() {
        var run = new SolveRun("x", [Step(StepEvent.Try, "a"), Step(StepEvent.Try, "b"), Step(StepEvent.Done, "c", true)], typeof(TableCells));
        var response = TableFrames.Build<TableCells>(Trace, run, Growing, Blank, _ => null, _ => throw new InvalidOperationException("not read"));
        Assert.Equal("Table", response.Type);
        var frames = response.Frames.Cast<TableFrame>().ToList();
        Assert.Equal(["a", "b", "c"], frames.Select(f => f.Caption));
        Assert.Equal([1, 2, 3], frames.Select(f => f.Values.Count));
        Assert.True(frames[^1].Ok);
        Assert.Equal(3, ((TablePayload)response.Payload).Rows.Count);
    }

    // The payload carries the blank: the base picture a frontend draws before Run. Its cells' text only (no states, focus,
    // changed cells or arrows, whatever the draft marks), and the payload lists every row it uses.
    [Fact]
    public void Build_ThePayloadCarriesTheBlank_CellTextOnly_AndListsItsRows() {
        var run = new SolveRun("x", [Step(StepEvent.Try, "a"), Step(StepEvent.Done, "b", true)], typeof(TableCells));
        var blank = new TableDraft();
        blank.AddRow("0", null);
        blank.AddRow("1", "?");
        blank.AddRow("2", null);
        blank.MarkRow(1, PictureState.Solution);
        blank.MarkCell(0, 0, PictureState.Rejected);
        blank.MarkChanged(1, 1);
        blank.FocusRow(2);
        var response = TableFrames.Build<TableCells>(Trace, run, Growing, () => blank, _ => null, _ => throw new InvalidOperationException("not read"));
        var payload = (TablePayload)response.Payload;
        Assert.Equal(3, payload.Rows.Count);
        Assert.Equal(
            "{\"kind\":\"table\",\"shape\":\"trace\",\"columns\":[{\"id\":\"c0\",\"label\":\"Step\",\"key\":\"step\"},{\"id\":\"c1\",\"label\":\"To\",\"key\":\"to\"}],"
            + "\"rows\":[{\"id\":\"r0\"},{\"id\":\"r1\"},{\"id\":\"r2\"}],"
            + "\"blank\":{\"values\":[[\"0\",null],[\"1\",\"?\"],[\"2\",null]]}}",
            Compact(payload));

        // A grid's blank lists its rows too, and a blank wider than the table is refused like any frame.
        var grid = (TablePayload)TableFrames.Build<TableCells>(Grid, new SolveRun("x", [], typeof(TableCells)), Growing, () => Rows(3, 3), _ => null, _ => false).Payload;
        Assert.Equal([["0.0", "0.1", "0.2"], ["1.0", "1.1", "1.2"], ["2.0", "2.1", "2.2"]], grid.Blank!.Values.Select(r => r.ToArray()));
        Assert.Throws<InvalidOperationException>(() => TableFrames.Build<TableCells>(Grid, new SolveRun("x", [], typeof(TableCells)), Growing, () => Rows(1, 4), _ => null, _ => false));
    }

    // A visualization whose start depends on the run (Start) gives the blank everywhere: the payload's and a stopped run's.
    [Fact]
    public void Build_AStartFromTheSteps_IsTheBlank_InThePayloadAndAStoppedRun() {
        var steps = new object[] { Step(StepEvent.Try, "a"), Step(StepEvent.Done, "Stopped after 0.05 s and 2 steps; the solver had not finished.", false) };
        var run = new SolveRun(SolveRun.TimedOutAnswer, steps, typeof(TableCells)) { TimedOut = true };
        var start = new TableDraft();
        start.AddRow("0", "start");
        int seen = -1;
        var response = TableFrames.Build<TableCells>(Trace, run, Growing, () => throw new InvalidOperationException("not read"),
            _ => null, _ => true, s => { seen = s.Count; return start; });
        Assert.Equal(1, seen);
        Assert.Equal([["0", "start"]], ((TablePayload)response.Payload).Blank!.Values.Select(r => r.ToArray()));
        Assert.Equal([["0", "start"]], response.Frames.Cast<TableFrame>().Last().Values.Select(r => r.ToArray()));

        // A start of null falls back to the blank.
        var fallback = TableFrames.Build<TableCells>(Trace, new SolveRun("x", [], typeof(TableCells)), Growing, () => Rows(1, 2), _ => null, _ => false, _ => null);
        Assert.Equal([["0.0", "0.1"]], ((TablePayload)fallback.Payload).Blank!.Values.Select(r => r.ToArray()));
    }

    [Fact]
    public void Build_StepsItCannotDraw_EndInADoneFrameFromTheAnswer() {
        var legacy = new SolveRun("answer", ["legacy step"], null);
        var solved = Rows(1, 2);
        solved.MarkRow(0, PictureState.Solution);
        var response = TableFrames.Build<TableCells>(Trace, legacy, Growing, Blank, a => a == "answer" ? solved : null, _ => true);
        var done = Assert.Single(response.Frames.Cast<TableFrame>());
        Assert.Equal((StepEvent.Done, "The answer is answer.", true), (done.Event, done.Caption, done.Ok));
        Assert.Equal("r0:Solution", States(done.Rows));

        var wrong = TableFrames.Build<TableCells>(Trace, legacy, Growing, () => Rows(1, 2), _ => solved, _ => false);
        var notOk = Assert.Single(wrong.Frames.Cast<TableFrame>());
        Assert.Equal(("No answer was found.", false), (notOk.Caption, notOk.Ok));
        Assert.Empty(notOk.Rows);
    }

    [Fact]
    public void Build_AStoppedRun_EndsWithTheBlankTable_NotOk_AndNeverReadsItsAnswer() {
        var steps = new object[] { Step(StepEvent.Try, "a"), Step(StepEvent.Try, "b"), Step(StepEvent.Done, "Stopped after 0.05 s and 3 steps; the solver had not finished.", false) };
        var run = new SolveRun(SolveRun.TimedOutAnswer, steps, typeof(TableCells)) { TimedOut = true };
        int drawn = -1;
        var blank = new TableDraft();
        blank.AddRow("instance", null);
        var response = TableFrames.Build<TableCells>(Trace, run, s => { drawn = s.Count; return Growing(s); }, () => blank,
            _ => throw new InvalidOperationException("not read"), _ => throw new InvalidOperationException("not read"));
        Assert.Equal(2, drawn);
        var frames = response.Frames.Cast<TableFrame>().ToList();
        Assert.Equal(3, frames.Count);
        var done = frames[^1];
        Assert.Equal((StepEvent.Done, false), (done.Event, done.Ok));
        Assert.StartsWith("Stopped after", done.Caption);
        Assert.Equal([["instance", null]], done.Values.Select(r => r.ToArray()));
        Assert.Empty(done.Rows);
        Assert.Empty(done.Cells);
        Assert.Empty(done.Focus);
        Assert.Empty(done.Changed);

        // Stopped before any step was kept: the blank Done alone, with the shared caption.
        var nothing = TableFrames.Build<TableCells>(Grid, new SolveRun(SolveRun.TimedOutAnswer, [], typeof(TableCells)) { TimedOut = true },
            Growing, () => new TableDraft(), _ => null, _ => true);
        var only = Assert.Single(nothing.Frames.Cast<TableFrame>());
        Assert.Equal((GraphSubsetFrames.StoppedCaption, false), (only.Caption, only.Ok));
        Assert.Empty(only.Arrows!);
    }
}
