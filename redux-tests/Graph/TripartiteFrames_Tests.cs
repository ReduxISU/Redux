using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Interfaces.Tripartite;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Tripartite Matching picture's base on its own: payload, ids, the states of each frame, the answer-only Done frame, a
// stopped run and a capped one. The solver's steps and the endpoint are tested in TripartiteSolvers_Tests and
// TripartiteFrames_Endpoint_Tests.
public class TripartiteFrames_Tests {
    // X = {a, b}, Y = {c, d}, Z = {e, f}; T1 (a,c,e), T2 (a,d,f), T3 (b,d,f), T4 (b,c,e). The matchings are {T1, T3} and {T2, T4}.
    private static readonly TripartiteSpec Small = new(["a", "b"], ["c", "d"], ["e", "f"],
        [new(0, 0, 0), new(0, 1, 1), new(1, 1, 1), new(1, 0, 0)]);

    // The name 1 is in X, Y and Z, and T1 is listed twice (T1, T2).
    private static readonly TripartiteSpec Shared = new(["1", "2"], ["1", "2"], ["1", "2"],
        [new(0, 0, 0), new(0, 0, 0), new(1, 1, 1)]);

    private static string Json(object value) => JsonSerializer.Serialize(value, FramesResponse.JsonOptions);

    private static string States(IReadOnlyDictionary<string, StateRef> map) => string.Join(" ", map.Select(p => $"{p.Key}:{p.Value.State}"));

    private static ChosenTriples C(params string[] ids) => new(ids);

    // ---- Ids and payload ----

    [Fact]
    public void Ids_ArePositions() {
        Assert.Equal(("x1", "y3", "z12", "T1", "T10"), (TripartiteFrames.XId(0), TripartiteFrames.YId(2), TripartiteFrames.ZId(11), TripartiteFrames.TripleId(0), TripartiteFrames.TripleId(9)));
        Assert.Equal(["x2", "y1", "z2"], TripartiteFrames.ElementIds(new TripartiteTriple(1, 0, 1)));
        Assert.Equal(C("T1", "T3"), TripartiteFrames.Chosen([2, 0]));
    }

    [Fact]
    public void Payload_Json_HasTheContractFieldsInOrder() {
        string json = JsonSerializer.Serialize(JsonDocument.Parse(Json(TripartiteFrames.Payload(Small))).RootElement);
        Assert.Equal(
            "{\"kind\":\"tripartite\",\"x\":[{\"id\":\"x1\",\"name\":\"a\"},{\"id\":\"x2\",\"name\":\"b\"}],"
            + "\"y\":[{\"id\":\"y1\",\"name\":\"c\"},{\"id\":\"y2\",\"name\":\"d\"}],\"z\":[{\"id\":\"z1\",\"name\":\"e\"},{\"id\":\"z2\",\"name\":\"f\"}],"
            + "\"triples\":[{\"id\":\"T1\",\"x\":\"x1\",\"y\":\"y1\",\"z\":\"z1\"},{\"id\":\"T2\",\"x\":\"x1\",\"y\":\"y2\",\"z\":\"z2\"},"
            + "{\"id\":\"T3\",\"x\":\"x2\",\"y\":\"y2\",\"z\":\"z2\"},{\"id\":\"T4\",\"x\":\"x2\",\"y\":\"y1\",\"z\":\"z1\"}]}",
            json);
    }

    [Fact]
    public void Payload_ANameInEverySet_AndATripleListedTwice_StayDistinct() {
        var payload = TripartiteFrames.Payload(Shared);
        Assert.Equal(["x1", "x2", "y1", "y2", "z1", "z2"], payload.X.Concat(payload.Y).Concat(payload.Z).Select(e => e.Id));
        Assert.Equal(["T1 x1 y1 z1", "T2 x1 y1 z1", "T3 x2 y2 z2"], payload.Triples.Select(t => $"{t.Id} {t.X} {t.Y} {t.Z}"));
    }

    // ---- Frames ----

    [Fact]
    public void Frame_Json_HasTheContractFieldsInOrder_AndOkOnlyOnDone() {
        using var doc = JsonDocument.Parse(Json(TripartiteFrames.Frame(Small, C("T1", "T2"), StepEvent.Reject, ["x1"], "c")));
        Assert.Equal(["event", "caption", "focus", "chosen", "repeats", "elements", "triples"], doc.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal("{\"x1\":2}", JsonSerializer.Serialize(doc.RootElement.GetProperty("repeats")));
        Assert.Equal("{\"state\":\"Rejected\"}", JsonSerializer.Serialize(doc.RootElement.GetProperty("elements").GetProperty("x1")));
        using var done = JsonDocument.Parse(Json(TripartiteFrames.Frame(Small, C("T1", "T3"), StepEvent.Done, [], "c", ok: true)));
        Assert.True(done.RootElement.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public void Frame_WholeCandidate_IsHighlightedWhileTried_UsedNamesCovered_AndTheTriplesItRulesOutBlocked() {
        var frame = TripartiteFrames.Frame(Small, C("T1", "T3"), StepEvent.Try, [], "c");
        Assert.Equal(["T1", "T3"], frame.Chosen);
        Assert.Empty(frame.Repeats);
        Assert.Equal("x1:Covered x2:Covered y1:Covered y2:Covered z1:Covered z2:Covered", States(frame.Elements));
        // T2 shares a with T1 (and d, f with T3); T4 shares b with T3.
        Assert.Equal("T1:ElementHighlight T2:Blocked T3:ElementHighlight T4:Blocked", States(frame.Triples));
    }

    [Fact]
    public void Frame_WholeCandidateReject_MarksTheRepeatedNamesAndTheTriplesThatShareThemRejected() {
        // T1 and T2 both use a: a is used twice.
        var frame = TripartiteFrames.Frame(Small, C("T1", "T2"), StepEvent.Reject, ["x1"], "c");
        Assert.Equal(new Dictionary<string, int> { ["x1"] = 2 }, frame.Repeats);
        Assert.Equal("x1:Rejected y1:Covered y2:Covered z1:Covered z2:Covered", States(frame.Elements));
        Assert.Equal("T1:Rejected T2:Rejected T3:Blocked T4:Blocked", States(frame.Triples));
    }

    [Fact]
    public void Frame_IsSolutionOnlyInAnAcceptOrTheAnswer() {
        Assert.Equal("T1:Solution T2:Blocked T3:Solution T4:Blocked", States(TripartiteFrames.Frame(Small, C("T1", "T3"), StepEvent.Accept, [], "c").Triples));
        Assert.Equal("T1:Solution T2:Blocked T3:Solution T4:Blocked", States(TripartiteFrames.Frame(Small, C("T1", "T3"), StepEvent.Done, [], "c", ok: true).Triples));
        // A claimed answer that is wrong failed.
        Assert.Equal("T1:Rejected T2:Blocked T3:Rejected T4:Blocked", States(TripartiteFrames.Frame(Small, C("T1", "T3"), StepEvent.Done, [], "c", ok: false).Triples));
    }

    [Fact]
    public void Frame_Incremental_KeepsTheCommittedTriplesSolution_AndMarksOnlyTheFocus() {
        // T1 committed, T3 under test.
        var tried = TripartiteFrames.Frame(Small, C("T1", "T3"), StepEvent.Try, ["T3"], "c", incremental: true);
        Assert.Equal("T1:Solution T2:Blocked T3:ElementHighlight T4:Blocked", States(tried.Triples));
        // T2 shares a with the committed T1, so it is skipped: the focus failed.
        var skipped = TripartiteFrames.Frame(Small, C("T1"), StepEvent.Reject, ["T2"], "c", incremental: true);
        Assert.Equal("T1:Solution T2:Rejected T4:Blocked", States(skipped.Triples));
        Assert.Equal("x1:Covered y1:Covered z1:Covered", States(skipped.Elements));
    }

    [Fact]
    public void Frame_ATripleListedTwice_ChosenTwice_UsesItsNamesTwice() {
        var frame = TripartiteFrames.Frame(Shared, C("T1", "T2"), StepEvent.Reject, [], "c");
        Assert.Equal(new Dictionary<string, int> { ["x1"] = 2, ["y1"] = 2, ["z1"] = 2 }, frame.Repeats);
        Assert.Equal("T1:Rejected T2:Rejected", States(frame.Triples));
        // The same name in X, Y and Z is three elements: one use each.
        Assert.Equal("x1:Covered x2:Covered y1:Covered y2:Covered z1:Covered z2:Covered",
            States(TripartiteFrames.Frame(Shared, C("T1", "T3"), StepEvent.Accept, [], "c").Elements));
    }

    [Fact]
    public void Frame_IdsThatAreNotInTheInstance_AreIgnored() {
        var frame = TripartiteFrames.Frame(Small, C("T1", "T9", "x"), StepEvent.Try, ["T7", "x9", "q"], "c");
        Assert.Equal(["T1"], frame.Chosen);
        Assert.Equal("T1:ElementHighlight T2:Blocked T4:Blocked", States(frame.Triples));
        Assert.Equal("x1:Covered y1:Covered z1:Covered", States(frame.Elements));
    }

    [Fact]
    public void Precedence_RejectedBeatsHighlightBeatsSolutionBeatsCoveredBeatsBlocked() {
        // The focus is a used element (Covered) and a ruled-out triple (Blocked): highlighted over both.
        var frame = TripartiteFrames.Frame(Small, C("T1"), StepEvent.Try, ["x1", "T2"], "c", incremental: true);
        Assert.Equal("x1:ElementHighlight y1:Covered z1:Covered", States(frame.Elements));
        Assert.Equal("T1:Solution T2:ElementHighlight T4:Blocked", States(frame.Triples));
        // A repeated element in the focus stays Rejected.
        Assert.Equal("Rejected", TripartiteFrames.Frame(Small, C("T1", "T2"), StepEvent.Try, ["x1"], "c", incremental: true).Elements["x1"].State.ToString());
    }

    // ---- Answers ----

    [Theory]
    [InlineData("{{a,c,e},{b,d,f}}", "T1,T3")]
    [InlineData("{{b,c,e}}", "T4")]
    [InlineData("{}", "")]
    [InlineData("{{a,c,e},{q,d,f}}", "")]   // q is not in X
    [InlineData("{{a,c}}", "")]
    [InlineData("nonsense", "")]
    public void ParseMatching_NamesTriplesOfM(string answer, string ids) =>
        Assert.Equal(C(ids.Split(',', StringSplitOptions.RemoveEmptyEntries)), TripartiteFrames.ParseMatching(Small, answer));

    [Fact]
    public void ParseMatching_ATripleListedTwice_TakesItsCopiesInOrder() {
        Assert.Equal(C("T1", "T2"), TripartiteFrames.ParseMatching(Shared, "{{1,1,1},{1,1,1}}"));
        Assert.Equal(C("T1", "T3"), TripartiteFrames.ParseMatching(Shared, "{{1,1,1},{2,2,2}}"));
    }

    [Fact]
    public void ElementName_SaysTheSet_OnlyWhenTheNameIsInMoreThanOne() {
        Assert.Equal(("a", "f"), (TripartiteFrames.ElementName(Small, "x1"), TripartiteFrames.ElementName(Small, "z2")));
        Assert.Equal(("1 in X", "2 in Z"), (TripartiteFrames.ElementName(Shared, "x1"), TripartiteFrames.ElementName(Shared, "z2")));
        Assert.Equal("T2 (a, d, f)", TripartiteFrames.TripleLabel(Small, 1));
    }

    // ---- Build ----

    private static SolveRun Run(bool timedOut, params SolverStep<ChosenTriples>[] steps) =>
        new(timedOut ? SolveRun.TimedOutAnswer : "{{a,c,e},{b,d,f}}", steps, typeof(ChosenTriples)) { TimedOut = timedOut };

    [Fact]
    public void Build_AFramePerStep_EndingWithTheDoneStep() {
        var run = Run(false,
            new SolverStep<ChosenTriples>(C("T1", "T2"), StepEvent.Reject, ["x1"], "Try T1 and T2: a is used twice."),
            new SolverStep<ChosenTriples>(C("T1", "T3"), StepEvent.Accept, [], "Try T1 and T3."),
            new SolverStep<ChosenTriples>(C("T1", "T3"), StepEvent.Done, [], "Found.") { Ok = true });
        var response = TripartiteFrames.Build(Small, run, _ => throw new InvalidOperationException("not read"), _ => throw new InvalidOperationException("not read"));
        Assert.Equal("TripartiteMatching", response.Type);
        var frames = response.Frames.Cast<TripartiteFrame>().ToList();
        Assert.Equal([StepEvent.Reject, StepEvent.Accept, StepEvent.Done], frames.Select(f => f.Event));
        Assert.Equal("T1:Solution T2:Blocked T3:Solution T4:Blocked", States(frames[^1].Triples));
        Assert.True(frames[^1].Ok);
    }

    [Fact]
    public void Build_WithoutDrawableSteps_AddsTheAnswer_OrABlankFrame() {
        var legacy = new SolveRun("{{a,c,e},{b,d,f}}", ["legacy"], null);
        var done = (TripartiteFrame)Assert.Single(TripartiteFrames.Build(Small, legacy, a => TripartiteFrames.ParseMatching(Small, a), _ => true).Frames);
        Assert.Equal((StepEvent.Done, true, "The answer is {{a,c,e},{b,d,f}}."), (done.Event, done.Ok, done.Caption));
        Assert.Equal(["T1", "T3"], done.Chosen);
        var blank = (TripartiteFrame)Assert.Single(TripartiteFrames.Build(Small, new SolveRun("{}", [], null), _ => throw new InvalidOperationException(), _ => false).Frames);
        Assert.Equal(("No answer was found.", false), (blank.Caption, blank.Ok));
        Assert.Empty(blank.Chosen);
        Assert.Empty(blank.Elements);
        Assert.Empty(blank.Triples);
    }

    [Fact]
    public void Build_AStoppedRun_EndsBlank_NotOk_AndNeverReadsItsAnswer() {
        var run = Run(true,
            new SolverStep<ChosenTriples>(C("T1", "T2"), StepEvent.Reject, ["x1"], "Try T1 and T2."),
            new SolverStep<ChosenTriples>(C(), StepEvent.Done, [], "Stopped after 0.05 s and 1 step; the solver had not finished.") { Ok = false });
        var frames = TripartiteFrames.Build(Small, run, _ => throw new InvalidOperationException("read"), _ => throw new InvalidOperationException("read"))
            .Frames.Cast<TripartiteFrame>().ToList();
        Assert.Equal(2, frames.Count);
        var done = frames[^1];
        Assert.Equal((StepEvent.Done, false), (done.Event, done.Ok));
        Assert.StartsWith("Stopped after", done.Caption);
        Assert.Empty(done.Chosen);
        Assert.Empty(done.Repeats);
        Assert.Empty(done.Elements);
        Assert.Empty(done.Triples);
        Assert.Empty(done.Focus);

        // Steps of another shape cannot be drawn: the stopped frame is added, with the generic caption.
        var other = new SolveRun(SolveRun.TimedOutAnswer, [], typeof(NodeSet)) { TimedOut = true };
        var added = (TripartiteFrame)Assert.Single(TripartiteFrames.Build(Small, other, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException()).Frames);
        Assert.Equal(GraphSubsetFrames.StoppedCaption, added.Caption);
        Assert.Equal(false, added.Ok);
    }

    [Fact]
    public void Build_ACappedRun_KeepsTheFirstTries_ThenTheAcceptedMatching() {
        // 400 failed tries, then the accepted matching: 150 tries are kept, then the Accept and the Done step.
        var rec = new StepRecorder<ChosenTriples>();
        for (int i = 0; i < 400; i++) {
            int k = i;
            rec.Reject(() => C("T1", "T2"), () => $"Try {k}.", () => ["x1"]);
        }
        rec.Accept(() => C("T2", "T4"), () => "Try T2 and T4.", () => []);
        rec.Done(C("T2", "T4"), true, "Found.");
        var run = new SolveRun("{{a,d,f},{b,c,e}}", rec.Steps.Cast<object>().ToList(), typeof(ChosenTriples));
        var frames = TripartiteFrames.Build(Small, run, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException())
            .Frames.Cast<TripartiteFrame>().ToList();
        Assert.Equal(StepRecorder<ChosenTriples>.DefaultCap + 2, frames.Count);
        Assert.Equal("Try 149.", frames[^3].Caption);
        Assert.Equal(StepEvent.Accept, frames[^2].Event);
        Assert.Equal("Found. (250 not shown)", frames[^1].Caption);
        Assert.Equal("T1:Blocked T2:Solution T3:Blocked T4:Solution", States(frames[^1].Triples));
    }

    // ---- The shape ----

    [Fact]
    public void ChosenTriples_IsASet_WithValueEquality_AndAnEmptyAnswer() {
        Assert.Equal(C("T3", "T1"), C("T1", "T3", "T1"));
        Assert.NotEqual(C("T1"), C("T1", "T2"));
        Assert.Equal(C(), Shapes.EmptyAnswer(typeof(ChosenTriples)));
        Assert.Equal("{\"triples\":[\"T1\",\"T10\",\"T2\"]}", JsonSerializer.Serialize(C("T2", "T10", "T1")));
    }
}
