using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Unit tests of the Bipartite Graph picture: (universe, sets, pick side, rule, the picks of one step, event, focus) ->
// payload and named states. No endpoint, no solver.
public class BipartiteFrames_Tests {
    // Set Cover's default instance: U = {1,...,5}, S1 = {1,2,3}, S2 = {2,4}, S3 = {3,4}, S4 = {4,5}.
    private static readonly string[] U = ["1", "2", "3", "4", "5"];
    private static readonly IReadOnlyList<string>[] S = [["1", "2", "3"], ["2", "4"], ["3", "4"], ["4", "5"]];

    private static BipartiteSpec Cover(int? k = 3) => new(U, S, k, BipartitePick.Sets, BipartiteRule.AtLeastOnce);
    private static BipartiteSpec Exact() => new(U, S, null, BipartitePick.Sets, BipartiteRule.ExactlyOnce);
    private static BipartiteSpec Hitting() => new(U, S, null, BipartitePick.Elements, BipartiteRule.ExactlyOnce);

    private static PictureState? State(BipartiteFrame f, string id) => f.Nodes.TryGetValue(id, out var s) ? s.State : null;
    private static PictureState? EdgeState(BipartiteFrame f, string id) => f.Edges.TryGetValue(id, out var s) ? s.State : null;

    // ---- payload ----

    [Fact]
    public void Payload_ListsElementsSetsAndOneEdgePerMembership_WithTheEndsOnEveryEdge() {
        var p = BipartiteFrames.Payload(Cover());
        Assert.Equal("bipartiteGraph", p.Kind);
        Assert.Equal("columns", p.Layout);
        Assert.Equal("sets", p.PickSide);
        Assert.Equal("atLeastOnce", p.Rule);
        Assert.Equal(3, p.K);
        Assert.Equal(["e:1", "e:2", "e:3", "e:4", "e:5"], p.Elements.Select(e => e.Id));
        Assert.Equal(U, p.Elements.Select(e => e.Label));
        Assert.Equal(["S1", "S2", "S3", "S4"], p.Sets.Select(s => s.Id));
        Assert.Equal(["e:2", "e:4"], p.Sets[1].Members);
        Assert.Equal(9, p.Edges.Count);
        Assert.All(p.Edges, e => Assert.Equal($"{e.A}~{e.B}", e.Id));
        Assert.Equal(("e:4", "S4"), (p.Edges[^2].A, p.Edges[^2].B));
    }

    [Fact]
    public void Payload_Json_IsTheContract() {
        var spec = new BipartiteSpec(["1", "2"], [["1", "2"], ["2"]], null, BipartitePick.Elements, BipartiteRule.ExactlyOnce);
        Assert.Equal(
            "{\"kind\":\"bipartiteGraph\",\"layout\":\"columns\",\"pickSide\":\"elements\",\"rule\":\"exactlyOnce\"," +
            "\"elements\":[{\"id\":\"e:1\",\"label\":\"1\"},{\"id\":\"e:2\",\"label\":\"2\"}]," +
            "\"sets\":[{\"id\":\"S1\",\"label\":\"S1\",\"members\":[\"e:1\",\"e:2\"]},{\"id\":\"S2\",\"label\":\"S2\",\"members\":[\"e:2\"]}]," +
            "\"edges\":[{\"id\":\"e:1~S1\",\"a\":\"e:1\",\"b\":\"S1\"},{\"id\":\"e:2~S1\",\"a\":\"e:2\",\"b\":\"S1\"},{\"id\":\"e:2~S2\",\"a\":\"e:2\",\"b\":\"S2\"}]}",
            JsonSerializer.Serialize(BipartiteFrames.Payload(spec)));
    }

    [Theory]
    [InlineData("AtLeastOnce", "atLeastOnce")]
    [InlineData("ExactlyOnce", "exactlyOnce")]
    public void RuleNames_AreTheContractsSpelling(string rule, string name) =>
        Assert.Equal(name, BipartiteFrames.RuleName(Enum.Parse<BipartiteRule>(rule)));

    [Fact]
    public void Payload_DropsRepeatedMembers_AndKeepsTwoSetsWithTheSameElementsApart() {
        var spec = new BipartiteSpec(["a", "a", "b"], [["a", "a"], ["a"]], 1, BipartitePick.Sets, BipartiteRule.AtLeastOnce);
        var p = BipartiteFrames.Payload(spec);
        Assert.Equal(["e:a", "e:b"], p.Elements.Select(e => e.Id));
        Assert.Equal(["e:a~S1", "e:a~S2"], p.Edges.Select(e => e.Id));
    }

    [Fact]
    public void Ids_StayUnique_WhenElementNamesHoldTheSeparators_AndSuchNamesAreNotRefused() {
        // Unescaped, the element "a~S1" in S2 would give the edge "e:a~S1~S2"; escaped it is "e:a\~S1~S2", which no other
        // edge can be. An element named like a set ("S1") is "e:S1", never the set S1.
        string[] names = ["a", "a~S1", "S1", @"x\", "p:q", "e:1"];
        var spec = new BipartiteSpec(names, [names, ["a"]], null, BipartitePick.Sets, BipartiteRule.AtLeastOnce);
        var p = BipartiteFrames.Payload(spec);
        Assert.Equal(["e:a", @"e:a\~S1", "e:S1", @"e:x\\", @"e:p\:q", @"e:e\:1"], p.Elements.Select(e => e.Id));
        Assert.Equal(names, p.Elements.Select(e => e.Label));
        var ids = p.Elements.Select(e => e.Id).Concat(p.Sets.Select(s => s.Id)).Concat(p.Edges.Select(e => e.Id)).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.Contains(p.Edges, e => e.Id == @"e:a\~S1~S1" && e.A == @"e:a\~S1" && e.B == "S1");
    }

    // ---- whole candidates (a brute force) ----

    [Fact]
    public void WholeCandidate_Try_IsHighlighted_NotSolution_AndWhatItMissesIsRejected() {
        var f = BipartiteFrames.Frame(Cover(), ["S1", "S2"], StepEvent.Reject, ["S1", "S2"], "Try {S1,S2}: 5 isn't covered.");
        Assert.Equal(PictureState.ElementHighlight, State(f, "S1"));
        Assert.Equal(PictureState.ElementHighlight, State(f, "S2"));
        Assert.Equal(PictureState.ElementHighlight, EdgeState(f, "e:2~S2"));
        Assert.Null(State(f, "S3"));
        Assert.Null(EdgeState(f, "e:3~S3"));
        Assert.Equal(PictureState.Covered, State(f, "e:4"));
        Assert.Equal(PictureState.Rejected, State(f, "e:5"));
        Assert.DoesNotContain(f.Nodes.Values, s => s.State is PictureState.Solution or PictureState.Blocked);
        Assert.Equal(2, f.Hits["e:2"]);
        Assert.False(f.Hits.ContainsKey("e:5"));
    }

    [Fact]
    public void WholeCandidate_UnderExactlyOnce_ADoubledElementIsRejected_AndNothingIsBlocked() {
        var f = BipartiteFrames.Frame(Exact(), ["S1", "S3"], StepEvent.Reject, ["S1", "S3"], "Try {S1,S3}.");
        Assert.Equal(PictureState.Rejected, State(f, "e:3"));      // in S1 and S3
        Assert.Equal(2, f.Hits["e:3"]);
        Assert.Equal(PictureState.Covered, State(f, "e:1"));
        Assert.Equal(PictureState.Rejected, State(f, "e:5"));      // in neither
        Assert.Null(State(f, "S2"));                                 // overlaps, but nothing is taken while a candidate is tried
    }

    [Fact]
    public void WholeCandidate_Accept_IsSolution_UnderTheFocusHighlight() {
        // The Accept that ends the run is the confirmed answer.
        var f = BipartiteFrames.Frame(Cover(), ["S1", "S4"], StepEvent.Accept, [], "Try {S1,S4}. It covers U.", endsRun: true);
        Assert.Equal(PictureState.Solution, State(f, "S1"));
        Assert.Equal(PictureState.Solution, EdgeState(f, "e:5~S4"));
        Assert.All(U, x => Assert.Equal(PictureState.Covered, State(f, "e:" + x)));
        // A brute force names the winning candidate as its focus, as Clique's does: the focus shows over Solution.
        var focused = BipartiteFrames.Frame(Cover(), ["S1", "S4"], StepEvent.Accept, ["S1", "S4"], "Try {S1,S4}. It covers U.");
        Assert.Equal(PictureState.ElementHighlight, State(focused, "S1"));
        // An Accept the run goes on from is kept, not confirmed (#886).
        var kept = BipartiteFrames.Frame(Cover(), ["S1", "S4"], StepEvent.Accept, [], "Try {S1,S4}. It covers U.");
        Assert.Equal(PictureState.Committed, State(kept, "S1"));
        Assert.Equal(PictureState.Committed, EdgeState(kept, "e:5~S4"));
    }

    // ---- incremental runs (greedy, Algorithm X) ----

    [Fact]
    public void Incremental_TheCommittedPicksStayCommitted_AndOnlyThePieceUnderTestIsHighlighted() {
        var f = BipartiteFrames.Frame(Exact(), ["S1", "S4"], StepEvent.Accept, ["S4"], "Try S4.", incremental: true);
        Assert.Equal(PictureState.Committed, State(f, "S1"));
        Assert.Equal(PictureState.Committed, EdgeState(f, "e:1~S1"));
        Assert.Equal(PictureState.ElementHighlight, State(f, "S4"));
        Assert.Equal(PictureState.ElementHighlight, EdgeState(f, "e:5~S4"));
        // S2 = {2,4} and S3 = {3,4} would double up on covered elements.
        Assert.Equal(PictureState.Blocked, State(f, "S2"));
        Assert.Equal(PictureState.Blocked, State(f, "S3"));
    }

    [Fact]
    public void Incremental_Try_DoesNotFailWhatIsNotCoveredYet() {
        var f = BipartiteFrames.Frame(Cover(), ["S1"], StepEvent.Try, ["S1"], "Pick S1.", incremental: true);
        Assert.Null(State(f, "e:4"));
        Assert.Null(State(f, "e:5"));
    }

    [Fact]
    public void Incremental_Reject_MarksOnlyTheFocusRejected() {
        // Algorithm X: no set left can cover 5 without doubling up on 4.
        var f = BipartiteFrames.Frame(Exact(), ["S2"], StepEvent.Reject, ["e:5"], "No set left can cover 5.", incremental: true);
        Assert.Equal(PictureState.Rejected, State(f, "e:5"));
        Assert.Equal(PictureState.Committed, State(f, "S2"));
        Assert.Null(State(f, "e:1"));                  // not covered, but not failed: the search goes on
        Assert.Equal(PictureState.Blocked, State(f, "S4"));

        // A set in the focus of a Reject (over budget) fails with its edges.
        var over = BipartiteFrames.Frame(Cover(1), ["S1", "S2"], StepEvent.Reject, ["S2"], "That is 2 sets, more than K = 1.", incremental: true);
        Assert.Equal(PictureState.Rejected, State(over, "S2"));
        Assert.Equal(PictureState.Rejected, EdgeState(over, "e:4~S2"));
        Assert.Equal(PictureState.Committed, State(over, "S1"));
    }

    // ---- precedence ----

    [Fact]
    public void Precedence_RejectedBeatsHighlight_HighlightBeatsCommittedAndCovered() {
        // e:3 is doubled (Rejected) and in the focus: it stays Rejected. e:1 is covered and in the focus: highlighted.
        var f = BipartiteFrames.Frame(Exact(), ["S1", "S3"], StepEvent.Accept, ["e:3", "e:1", "S1"], "...", incremental: true);
        Assert.Equal(PictureState.Rejected, State(f, "e:3"));
        Assert.Equal(PictureState.ElementHighlight, State(f, "e:1"));
        Assert.Equal(PictureState.ElementHighlight, State(f, "S1"));
        Assert.Equal(PictureState.Committed, State(f, "S3"));
    }

    // ---- the answer ----

    [Fact]
    public void Done_WithAnAnswer_IsSolutionAndCovered_AndBlocksOverlapsUnderExactlyOnce() {
        var spec = new BipartiteSpec(["1", "2", "3", "4"], [["1", "2", "3"], ["2", "3"], ["4", "1"]], null, BipartitePick.Sets, BipartiteRule.ExactlyOnce);
        var f = BipartiteFrames.Frame(spec, ["S2", "S3"], StepEvent.Done, [], "Done.", ok: true);
        Assert.Equal(PictureState.Solution, State(f, "S2"));
        Assert.Equal(PictureState.Solution, State(f, "S3"));
        Assert.Equal(PictureState.Blocked, State(f, "S1"));
        Assert.All(["e:1", "e:2", "e:3", "e:4"], x => Assert.Equal(PictureState.Covered, State(f, x)));
        Assert.True(f.Ok);
    }

    [Fact]
    public void Done_WithAnIncompleteAnswer_RejectsWhatIsNotCovered() {
        var f = BipartiteFrames.Frame(Cover(), ["S1"], StepEvent.Done, [], "Done.", ok: false, incremental: true);
        Assert.Equal(PictureState.Rejected, State(f, "e:4"));
        Assert.Equal(PictureState.Rejected, State(f, "e:5"));
        // A run that ends without an answer never shows green: the pick is Rejected, with its edges (#886).
        Assert.Equal(PictureState.Rejected, State(f, "S1"));
        Assert.NotEmpty(f.Edges);
        Assert.All(f.Edges.Values, s => Assert.Equal(PictureState.Rejected, s.State));
    }

    [Fact]
    public void Done_WithNoAnswer_JudgesNothing() {
        var f = BipartiteFrames.Frame(Cover(), [], StepEvent.Done, [], "No cover exists.", ok: false);
        Assert.Empty(f.Nodes);
        Assert.Empty(f.Edges);
        Assert.Empty(f.Hits);
    }

    [Fact]
    public void PickingElements_HitsSets_AndDrawsTheElementsPicked() {
        // Hitting Set picks elements: {1,2} hits S1 twice, S2 once, S3 not at all, S4 not at all.
        var partial = new NodeSet(["1", "2"]);
        var f = BipartiteFrames.Frame(Hitting(), BipartiteFrames.PickIds(partial), StepEvent.Reject, ["e:1", "e:2"], "Try {1,2}.");
        Assert.Equal(PictureState.ElementHighlight, State(f, "e:1"));
        Assert.Equal(PictureState.ElementHighlight, EdgeState(f, "e:2~S2"));
        Assert.Equal(PictureState.Rejected, State(f, "S1"));
        Assert.Equal(2, f.Hits["S1"]);
        Assert.Equal(PictureState.Covered, State(f, "S2"));
        Assert.Equal(PictureState.Rejected, State(f, "S3"));
        Assert.Equal(PictureState.Rejected, State(f, "S4"));
        Assert.Null(State(f, "e:3"));
    }

    [Fact]
    public void Frame_Json_HasOnlyTheContractFields() {
        var f = BipartiteFrames.Frame(Cover(), ["S4"], StepEvent.Accept, ["S4"], "Pick S4.", incremental: true);
        Assert.Equal(
            "{\"event\":\"Accept\",\"caption\":\"Pick S4.\",\"focus\":[\"S4\"]," +
            "\"nodes\":{\"S4\":{\"state\":\"ElementHighlight\"},\"e:4\":{\"state\":\"Covered\"},\"e:5\":{\"state\":\"Covered\"}}," +
            "\"edges\":{\"e:4~S4\":{\"state\":\"ElementHighlight\"},\"e:5~S4\":{\"state\":\"ElementHighlight\"}}," +
            "\"hits\":{\"e:4\":1,\"e:5\":1}}",
            JsonSerializer.Serialize(f));
        var done = BipartiteFrames.Frame(Cover(), [], StepEvent.Done, [], "None.", ok: false);
        Assert.EndsWith(",\"ok\":false}", JsonSerializer.Serialize(done));
    }

    // ---- reading answers ----

    [Fact]
    public void ParseAnswer_MatchesSetsByTheirElements_EachSetOnce() {
        var spec = new BipartiteSpec(["1", "2", "3"], [["1"], ["2", "3"], ["1"]], null, BipartitePick.Sets, BipartiteRule.AtLeastOnce);
        Assert.Equal(["S2", "S1"], BipartiteFrames.ParseAnswer(spec, "{{3,2},{1}}")!);
        Assert.Equal(["S1"], BipartiteFrames.ParseAnswer(spec, "{{1},{1}}")!);          // read as a set, as the verifiers do
        Assert.Empty(BipartiteFrames.ParseAnswer(spec, "{}")!);
        Assert.Null(BipartiteFrames.ParseAnswer(spec, "{{1,2}}"));                    // no such set
        Assert.Null(BipartiteFrames.ParseAnswer(spec, ""));
    }

    [Fact]
    public void ParseAnswer_OfAnElementPick_GivesElementIds() {
        Assert.Equal(["e:1", "e:4"], BipartiteFrames.ParseAnswer(Hitting(), "{1,4}")!);
        Assert.Empty(BipartiteFrames.ParseAnswer(Hitting(), "{}")!);
    }

    [Fact]
    public void Why_NamesWhatIsMissedAndWhatIsDoubled() {
        Assert.Equal("5 isn't covered", BipartiteFrames.Why(Cover(), ["S1", "S2"]));
        Assert.Equal("", BipartiteFrames.Why(Cover(), ["S1", "S4"]));
        Assert.Equal("5 isn't covered; 3 is covered more than once", BipartiteFrames.Why(Exact(), ["S1", "S3"]));
        Assert.Equal("4 and 5 aren't covered", BipartiteFrames.Why(Exact(), ["S1"]));
        Assert.Equal("S3 and S4 aren't hit; S1 is hit more than once", BipartiteFrames.Why(Hitting(), ["e:1", "e:2"]));
    }

    // ---- the whole response ----

    private static SolveRun Run(bool incremental, params SolverStep<ChosenSets>[] steps) =>
        new("{{1,2,3},{4,5}}", steps.Cast<object>().ToList(), typeof(ChosenSets)) { Incremental = incremental };

    [Fact]
    public void Build_DrawsEachStep_AndAddsNoSolvedFrameAfterADoneStep() {
        var run = Run(true,
            new SolverStep<ChosenSets>(new ChosenSets(["S1"]), StepEvent.Accept, ["S1"], "Pick S1."),
            new SolverStep<ChosenSets>(new ChosenSets(["S1", "S4"]), StepEvent.Accept, ["S4"], "Pick S4."),
            new SolverStep<ChosenSets>(new ChosenSets(["S1", "S4"]), StepEvent.Done, [], "Covered.") { Ok = true });
        var response = BipartiteFrames.Build(Cover(), run, _ => throw new InvalidOperationException("not read"), _ => throw new InvalidOperationException("not verified"));
        Assert.Equal("BipartiteGraph", response.Type);
        Assert.IsType<BipartitePayload>(response.Payload);
        Assert.Equal(3, response.Frames.Count);
        var first = Assert.IsType<BipartiteFrame>(response.Frames[0]);
        Assert.Equal(PictureState.ElementHighlight, State(first, "S1"));    // incremental: the focus shows over Solution
        var last = Assert.IsType<BipartiteFrame>(response.Frames[^1]);
        Assert.True(last.Ok);
        Assert.Equal(PictureState.Solution, State(last, "S4"));
    }

    [Fact]
    public void Build_WithoutADoneStep_AddsTheSolvedFrameFromTheAnswer() {
        var run = new SolveRun("{{1,2,3},{4,5}}", [], null);
        var response = BipartiteFrames.Build(Cover(), run, a => BipartiteFrames.ParseAnswer(Cover(), a), _ => true);
        var done = Assert.IsType<BipartiteFrame>(Assert.Single(response.Frames));
        Assert.Equal(StepEvent.Done, done.Event);
        Assert.True(done.Ok);
        Assert.Equal("The answer is {S1,S4}.", done.Caption);
        Assert.Equal(PictureState.Solution, State(done, "S4"));
    }

    [Theory]
    [InlineData("{}", false, "No answer was found.")]
    [InlineData("{}", true, "The answer is {}: nothing needs to be picked.")]
    [InlineData("Something went wrong", false, "The solver's answer could not be read, so there is nothing to draw.")]
    public void Build_SolvedFrameCaption_SaysWhatTheAnswerIs(string answer, bool ok, string caption) {
        var response = BipartiteFrames.Build(Cover(), new SolveRun(answer, [], null), a => BipartiteFrames.ParseAnswer(Cover(), a), _ => ok);
        Assert.Equal(caption, Assert.IsType<BipartiteFrame>(Assert.Single(response.Frames)).Caption);
    }

    [Fact]
    public void Build_StepsOfAnotherShape_AreNotDrawn() {
        object[] steps = [new SolverStep<NodeSet>(new NodeSet(["1"]), StepEvent.Done, [], "Done.") { Ok = true }];
        var response = BipartiteFrames.Build(Cover(), new SolveRun("{{1,2,3},{4,5}}", steps, typeof(NodeSet)), a => BipartiteFrames.ParseAnswer(Cover(), a), _ => true);
        var done = Assert.IsType<BipartiteFrame>(Assert.Single(response.Frames));
        Assert.Equal("The answer is {S1,S4}.", done.Caption);
    }

    // ---- runs stopped by the time limit ----

    [Fact]
    public void Build_StoppedRun_EndsWithABlankNotOkDone_EvenIfItsDoneStepCarriesACandidate() {
        object[] steps = [
            new SolverStep<ChosenSets>(new ChosenSets(["S1", "S2"]), StepEvent.Reject, ["S1", "S2"], "Try {S1,S2}: 5 isn't covered."),
            new SolverStep<ChosenSets>(new ChosenSets(["S1", "S2"]), StepEvent.Done, [], "Stopped after 0.05 s and 9 steps; the solver had not finished.") { Ok = false },
        ];
        var run = new SolveRun(SolveRun.TimedOutAnswer, steps, typeof(ChosenSets)) { TimedOut = true };
        var response = BipartiteFrames.Build(Cover(), run,
            _ => throw new InvalidOperationException("a stopped run's answer must not be read"), _ => throw new InvalidOperationException("nor verified"));
        Assert.Equal(2, response.Frames.Count);
        var done = Assert.IsType<BipartiteFrame>(response.Frames[^1]);
        Assert.Equal(StepEvent.Done, done.Event);
        Assert.False(done.Ok);
        Assert.Equal("Stopped after 0.05 s and 9 steps; the solver had not finished.", done.Caption);
        Assert.Empty(done.Nodes);
        Assert.Empty(done.Edges);
        Assert.Empty(done.Hits);
    }

    [Fact]
    public void Build_StoppedRun_FromTheRecorder_HasNoSolutionInItsDoneFrame() {
        var run = SolveRun.Record<ChosenSets>(true, TimeSpan.FromMilliseconds(5), rec => {
            while (true) rec.Reject(() => new ChosenSets(["S1", "S4"]), () => "Try {S1,S4}.", "S1", "S4");
        });
        Assert.True(run.TimedOut);
        var response = BipartiteFrames.Build(Cover(), run, _ => throw new InvalidOperationException("not read"), _ => throw new InvalidOperationException("not verified"));
        var done = Assert.IsType<BipartiteFrame>(response.Frames[^1]);
        Assert.False(done.Ok);
        Assert.StartsWith("Stopped after ", done.Caption);
        Assert.Empty(done.Nodes);
        Assert.Single(response.Frames, f => ((BipartiteFrame)f).Event == StepEvent.Done);
    }

    [Fact]
    public void Build_StoppedRun_WithNoSteps_EndsWithTheStoppedFrame() {
        var run = new SolveRun(SolveRun.TimedOutAnswer, [], null) { TimedOut = true };
        var response = BipartiteFrames.Build(Cover(), run, _ => ["S1"], _ => true);
        var done = Assert.IsType<BipartiteFrame>(Assert.Single(response.Frames));
        Assert.Equal("{\"event\":\"Done\",\"caption\":\"" + GraphSubsetFrames.StoppedCaption + "\",\"focus\":[],\"nodes\":{},\"edges\":{},\"hits\":{},\"ok\":false}",
            JsonSerializer.Serialize(done));
    }
}
