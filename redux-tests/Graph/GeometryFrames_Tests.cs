using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Geometry;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Geometry picture's base on its own: payload, ids (repeated points included), the states of each frame, the Done frame,
// the answer-only Done frame, a stopped run and a capped one. The solver's steps and the endpoint are tested in
// GeometrySolvers_Tests and GeometryFrames_Endpoint_Tests.
public class GeometryFrames_Tests {
    // A square with a point inside (P5) and P6 a repeat of P1.
    private static readonly GeometrySpec Square = new([(0, 0), (4, 0), (4, 4), (0, 4), (2, 1), (0, 0)]);

    private static string Json(object value) => JsonSerializer.Serialize(value, FramesResponse.JsonOptions);

    private static string States(IReadOnlyDictionary<string, StateRef> map) => string.Join(" ", map.Select(p => $"{p.Key}:{p.Value.State}"));

    private static HullBuild H(string[][]? pieces = null, string[]? left = null, string[]? right = null, string[]? chain = null, string[]? dropped = null) =>
        new(pieces ?? [], left ?? [], right ?? [], chain ?? [], dropped ?? []);

    // ---- Ids and payload ----

    [Fact]
    public void Ids_ArePositions_AndARepeatedPointIsNamedAfterItsFirstCopy() {
        Assert.Equal(("P1", "P12"), (GeometryFrames.PointId(0), GeometryFrames.PointId(11)));
        Assert.Equal(["P1", "P2", "P3", "P4", "P5", "P1"], GeometryFrames.FirstOf(Square));
        Assert.Equal(["P1", "P2", "P3", "P4", "P5"], GeometryFrames.IdsByPoint(Square).Values);
        // 0 and -0 are one coordinate.
        Assert.Equal(["P1", "P1"], GeometryFrames.FirstOf(new GeometrySpec([(0, 0), (-0.0, -0.0)])));
    }

    [Fact]
    public void Payload_Json_HasThePointsToScale() {
        Assert.Equal(
            "{\"kind\":\"geometry\",\"points\":[{\"id\":\"P1\",\"x\":0,\"y\":0},{\"id\":\"P2\",\"x\":4,\"y\":0},{\"id\":\"P3\",\"x\":4,\"y\":4},"
            + "{\"id\":\"P4\",\"x\":0,\"y\":4},{\"id\":\"P5\",\"x\":2,\"y\":1},{\"id\":\"P6\",\"x\":0,\"y\":0}]}",
            JsonSerializer.Serialize(JsonDocument.Parse(Json(GeometryFrames.Payload(Square))).RootElement));
        Assert.Equal("{\"kind\":\"geometry\",\"points\":[{\"id\":\"P1\",\"x\":-0.25,\"y\":1E-07}]}",
            JsonSerializer.Serialize(JsonDocument.Parse(Json(GeometryFrames.Payload(new GeometrySpec([(-0.25, 1e-7)])))).RootElement));
    }

    // ---- Frames ----

    [Fact]
    public void Frame_Json_HasTheContractFieldsInOrder_AndOkOnlyOnDone() {
        var frame = GeometryFrames.Frame(Square, H(left: ["P1", "P5"], right: ["P2", "P3"], chain: ["P1"]), StepEvent.Try, ["P1"], "c", incremental: true);
        using var doc = JsonDocument.Parse(Json(frame));
        Assert.Equal(["event", "caption", "focus", "pieces", "chain", "hull", "points"], doc.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal("[{\"points\":[\"P1\",\"P5\"],\"role\":\"left\"},{\"points\":[\"P2\",\"P3\"],\"role\":\"right\"}]",
            JsonSerializer.Serialize(doc.RootElement.GetProperty("pieces")));
        var done = GeometryFrames.Frame(Square, H(), StepEvent.Done, [], "c", ok: true, incremental: true);
        Assert.True(JsonDocument.Parse(Json(done)).RootElement.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public void Frame_Incremental_KeepsHullsAndTheChainCommitted_TheTriedPointHighlighted_AndDroppedPointsRejected() {
        var frame = GeometryFrames.Frame(Square, H(pieces: [["P4"]], left: ["P1", "P5"], right: ["P2", "P3"], chain: ["P1", "P2"], dropped: []),
            StepEvent.Try, ["P2"], "Add P2 to the bottom edge.", incremental: true);
        Assert.Equal(["P4 piece", "P1,P5 left", "P2,P3 right"], frame.Pieces.Select(p => $"{string.Join(",", p.Points)} {p.Role}"));
        Assert.Equal(["P1", "P2"], frame.Chain);
        Assert.Empty(frame.Hull);
        // Mid-run the hulls and the chain are kept but not the answer yet, so they are Committed (#849, #886). P6 is P1
        // again, so it is drawn as P1 is.
        Assert.Equal("P1:Committed P2:ElementHighlight P3:Committed P4:Committed P5:Committed P6:Committed", States(frame.Points));
    }

    [Fact]
    public void Frame_IncrementalReject_MarksThePoppedPointRejected() {
        var frame = GeometryFrames.Frame(Square, H(left: ["P1", "P5"], right: ["P2", "P3"], chain: ["P1"]), StepEvent.Reject, ["P5"], "c", incremental: true);
        Assert.Equal("P1:Committed P2:Committed P3:Committed P5:Rejected P6:Committed", States(frame.Points));
    }

    [Fact]
    public void Frame_DroppedPoints_StayRejected() {
        var frame = GeometryFrames.Frame(Square, H(pieces: [["P1", "P2", "P3", "P4"]], dropped: ["P5"]), StepEvent.Try, [], "c", incremental: true);
        Assert.Equal("P1:Committed P2:Committed P3:Committed P4:Committed P5:Rejected P6:Committed", States(frame.Points));
    }

    [Fact]
    public void Frame_WholeCandidate_IsOnlyHighlightedWhileTried() {
        var tried = GeometryFrames.Frame(Square, H(pieces: [["P1", "P2", "P3", "P4"]]), StepEvent.Reject, [], "c");
        Assert.Equal("P1:ElementHighlight P2:ElementHighlight P3:ElementHighlight P4:ElementHighlight P6:ElementHighlight", States(tried.Points));
        var accepted = GeometryFrames.Frame(Square, H(pieces: [["P1", "P2", "P3", "P4"]]), StepEvent.Accept, [], "c");
        // An accepted candidate the run goes on from is not the answer yet (#849, #886).
        Assert.Equal("P1:Committed P2:Committed P3:Committed P4:Committed P6:Committed", States(accepted.Points));
    }

    [Fact]
    public void Frame_Done_DrawsTheHullInOrder_ItsCornersSolution_AndEveryOtherPointCovered() {
        var answer = H(pieces: [["P1", "P2", "P3", "P4"]], dropped: ["P5"]);
        var done = GeometryFrames.Frame(Square, answer, StepEvent.Done, [], "c", ok: true, incremental: true);
        Assert.Equal(["P1", "P2", "P3", "P4"], done.Hull);
        Assert.Empty(done.Pieces);
        Assert.Empty(done.Chain);
        Assert.Equal("P1:Solution P2:Solution P3:Solution P4:Solution P5:Covered P6:Solution", States(done.Points));
        // A claimed hull that is wrong failed; nothing else is judged.
        var wrong = GeometryFrames.Frame(Square, H(pieces: [["P1", "P2", "P3"]]), StepEvent.Done, [], "c", ok: false, incremental: true);
        Assert.Equal("P1:Rejected P2:Rejected P3:Rejected P6:Rejected", States(wrong.Points));
        // No answer: nothing drawn.
        var none = GeometryFrames.Frame(Square, H(), StepEvent.Done, [], "c", ok: false, incremental: true);
        Assert.Empty(none.Hull);
        Assert.Empty(none.Points);
    }

    [Fact]
    public void Frame_IdsThatAreNotPoints_AreIgnored() {
        var frame = GeometryFrames.Frame(Square, H(left: ["P1", "P9"], chain: ["P1", "Q"], dropped: ["P6", "x"]), StepEvent.Try, ["P7"], "c", incremental: true);
        Assert.Equal(["P1"], frame.Pieces.Single().Points);
        Assert.Equal(["P1"], frame.Chain);
        // P6 is not an id a solver names (its first copy is P1), so dropping it does nothing.
        Assert.Equal("P1:Committed P6:Committed", States(frame.Points));
    }

    [Fact]
    public void Precedence_RejectedBeatsHighlightBeatsSolution() {
        // The focus is on the chain (Solution) and was dropped (Rejected): Rejected shows.
        Assert.Equal("Rejected", GeometryFrames.Frame(Square, H(chain: ["P5"], dropped: ["P5"]), StepEvent.Try, ["P5"], "c", incremental: true).Points["P5"].State.ToString());
        Assert.Equal("ElementHighlight", GeometryFrames.Frame(Square, H(chain: ["P5"]), StepEvent.Try, ["P5"], "c", incremental: true).Points["P5"].State.ToString());
    }

    // ---- Answers ----

    [Fact]
    public void Answer_IsTheHullAsTheOnlyPiece_WithEveryOtherPointDropped() {
        Assert.Equal(H(pieces: [["P1", "P2", "P3", "P4"]], dropped: ["P5"]), GeometryFrames.Answer(Square, [(0, 0), (4, 0), (4, 4), (0, 4)]));
        Assert.Equal(H(), GeometryFrames.Answer(new GeometrySpec([]), []));
        // A corner that is not a point: no answer.
        Assert.Equal(H(), GeometryFrames.Answer(Square, [(0, 0), (9, 9), (4, 4)]));
    }

    // ---- Build ----

    private static SolveRun Run(bool timedOut, params SolverStep<HullBuild>[] steps) =>
        new(timedOut ? SolveRun.TimedOutAnswer : "((0,0), (4,0), (4,4), (0,4))", steps, typeof(HullBuild)) { Incremental = true, TimedOut = timedOut };

    [Fact]
    public void Build_AFramePerStep_EndingWithTheDoneStep() {
        var answer = H(pieces: [["P1", "P2", "P3", "P4"]], dropped: ["P5"]);
        var run = Run(false,
            new SolverStep<HullBuild>(H(pieces: [["P1", "P5"]]), StepEvent.Try, ["P1", "P5"], "Smallest piece: P1 and P5."),
            new SolverStep<HullBuild>(answer, StepEvent.Done, [], "Done.") { Ok = true });
        var response = GeometryFrames.Build(Square, run, _ => throw new InvalidOperationException("not read"), _ => throw new InvalidOperationException("not read"));
        Assert.Equal("Geometry", response.Type);
        var frames = response.Frames.Cast<GeometryFrame>().ToList();
        Assert.Equal([StepEvent.Try, StepEvent.Done], frames.Select(f => f.Event));
        Assert.Equal(["P1", "P2", "P3", "P4"], frames[1].Hull);
        Assert.True(frames[1].Ok);
    }

    [Fact]
    public void Build_WithoutDrawableSteps_AddsTheAnswer_OrABlankFrame() {
        var legacy = new SolveRun("((0,0), (4,0), (4,4), (0,4))", ["legacy"], null);
        var done = (GeometryFrame)Assert.Single(GeometryFrames.Build(Square, legacy, _ => GeometryFrames.Answer(Square, [(0, 0), (4, 0), (4, 4), (0, 4)]), _ => true).Frames);
        Assert.Equal((StepEvent.Done, true, "The answer is ((0,0), (4,0), (4,4), (0,4))."), (done.Event, done.Ok, done.Caption));
        Assert.Equal(["P1", "P2", "P3", "P4"], done.Hull);
        var blank = (GeometryFrame)Assert.Single(GeometryFrames.Build(Square, new SolveRun("nonsense", [], null), _ => throw new InvalidOperationException(), _ => false).Frames);
        Assert.Equal(("No answer was found.", false), (blank.Caption, blank.Ok));
        Assert.Empty(blank.Hull);
        Assert.Empty(blank.Points);
    }

    [Fact]
    public void Build_AStoppedRun_EndsBlank_NotOk_AndNeverReadsItsAnswer() {
        var run = Run(true,
            new SolverStep<HullBuild>(H(pieces: [["P1", "P5"]]), StepEvent.Try, ["P1", "P5"], "Smallest piece."),
            new SolverStep<HullBuild>(H(), StepEvent.Done, [], "Stopped after 0.05 s and 1 step; the solver had not finished.") { Ok = false });
        var frames = GeometryFrames.Build(Square, run, _ => throw new InvalidOperationException("read"), _ => throw new InvalidOperationException("read"))
            .Frames.Cast<GeometryFrame>().ToList();
        Assert.Equal(2, frames.Count);
        var done = frames[^1];
        Assert.Equal((StepEvent.Done, false), (done.Event, done.Ok));
        Assert.StartsWith("Stopped after", done.Caption);
        Assert.Empty(done.Pieces);
        Assert.Empty(done.Chain);
        Assert.Empty(done.Hull);
        Assert.Empty(done.Points);
        Assert.Empty(done.Focus);

        var other = new SolveRun(SolveRun.TimedOutAnswer, [], typeof(NodeSet)) { TimedOut = true };
        var added = (GeometryFrame)Assert.Single(GeometryFrames.Build(Square, other, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException()).Frames);
        Assert.Equal(GraphSubsetFrames.StoppedCaption, added.Caption);
        Assert.Equal(false, added.Ok);
    }

    [Fact]
    public void Build_ACappedRun_KeepsTheFirstSteps_ThenTheFinishedHull() {
        var rec = new StepRecorder<HullBuild>();
        for (int i = 0; i < 400; i++) {
            int k = i;
            rec.Try(() => H(chain: ["P1"]), () => $"Add {k}.", () => ["P1"]);
        }
        var answer = H(pieces: [["P1", "P2", "P3", "P4"]], dropped: ["P5"]);
        rec.Accept(() => answer, () => "The hull is P1, P2, P3 and P4.", () => []);
        rec.Done(answer, true, "Done.");
        var run = new SolveRun("((0,0), (4,0), (4,4), (0,4))", rec.Steps.Cast<object>().ToList(), typeof(HullBuild)) { Incremental = true };
        var frames = GeometryFrames.Build(Square, run, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException())
            .Frames.Cast<GeometryFrame>().ToList();
        Assert.Equal(StepRecorder<HullBuild>.DefaultCap + 2, frames.Count);
        Assert.Equal("Add 149.", frames[^3].Caption);
        Assert.Equal(StepEvent.Accept, frames[^2].Event);
        Assert.Equal("Done. (250 not shown)", frames[^1].Caption);
        Assert.Equal(["P1", "P2", "P3", "P4"], frames[^1].Hull);
    }

    // ---- The shape ----

    [Fact]
    public void HullBuild_HasValueEquality_AndAnEmptyAnswer() {
        Assert.Equal(H(pieces: [["P1", "P2"]], dropped: ["P5", "P3"]), H(pieces: [["P1", "P2"]], dropped: ["P3", "P5", "P3"]));
        Assert.NotEqual(H(pieces: [["P1", "P2"]]), H(pieces: [["P2", "P1"]]));
        Assert.NotEqual(H(left: ["P1"]), H(right: ["P1"]));
        Assert.Equal(H(), Shapes.EmptyAnswer(typeof(HullBuild)));
        Assert.Equal("{\"pieces\":[[\"P1\",\"P2\"]],\"left\":[],\"right\":[],\"chain\":[\"P1\"],\"dropped\":[\"P3\"]}",
            JsonSerializer.Serialize(H(pieces: [["P1", "P2"]], chain: ["P1"], dropped: ["P3"])));
    }
}
