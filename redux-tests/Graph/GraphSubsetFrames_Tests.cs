using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Unit tests of the state derivation for the Graph picture's subset rules: (graph, rule, partial NodeSet,
// step event, focus) -> named states. No endpoint, no solver.
public class GraphSubsetFrames_Tests {
    // a-b, b-c, c-d, a-c, and an isolated e:
    //     a --- b
    //      \   /
    //        c --- d        e
    private static GraphSubsetSpec Spec(SubsetRule rule, int? k = null) => new(
        ["a", "b", "c", "d", "e"],
        [new("a", "b"), new("c", "b"), new("c", "d"), new("a", "c")],   // note c,b: reversed on purpose
        k, rule);

    private static NodeSet Set(params string[] nodes) => new(nodes);

    private static PictureState? State(GraphFrame f, string node) => f.Nodes.TryGetValue(node, out var s) ? s.State : null;
    private static PictureState? EdgeState(GraphFrame f, string edge) => f.Edges.TryGetValue(edge, out var s) ? s.State : null;

    // ---- payload ----

    [Fact]
    public void Payload_HasCanonicalEdgeIdsAndNoColors() {
        var p = GraphSubsetFrames.Payload(Spec(SubsetRule.VertexCover, 2));
        Assert.Equal(["a-b", "b-c", "c-d", "a-c"], p.Edges.Select(e => e.Id));
        Assert.Equal(["b", "c"], p.Edges.Select(e => e.B).Take(2));     // "c,b" was swapped to a < b
        Assert.Equal(2, p.K);
        string json = JsonSerializer.Serialize(p);
        Assert.Contains("\"rule\":\"vertexCover\"", json);
        Assert.DoesNotContain("color", json);
        Assert.DoesNotContain("weight", json);
    }

    [Fact]
    public void Payload_DropsLoopsAndDuplicateEdges() {
        var spec = new GraphSubsetSpec(["a", "b"], [new("a", "b"), new("b", "a"), new("a", "a")], null, SubsetRule.MinVertexCover);
        Assert.Equal(["a-b"], GraphSubsetFrames.Payload(spec).Edges.Select(e => e.Id));
        Assert.DoesNotContain("\"k\"", JsonSerializer.Serialize(GraphSubsetFrames.Payload(spec)));
    }

    [Theory]
    [InlineData("Clique", "clique")]
    [InlineData("VertexCover", "vertexCover")]
    [InlineData("MinVertexCover", "minVertexCover")]
    [InlineData("IndependentSet", "independentSet")]
    [InlineData("DominatingSet", "dominatingSet")]
    public void RuleNames_AreTheContractsSpelling(string rule, string name) =>
        Assert.Equal(name, GraphSubsetFrames.RuleName(Enum.Parse<SubsetRule>(rule)));

    // ---- clique ----

    [Fact]
    public void Clique_ChosenNodesAreSolution_JoinedPairsSolutionEdges_NoPhantoms() {
        var f = GraphSubsetFrames.Frame(Spec(SubsetRule.Clique), Set("a", "b", "c"), StepEvent.Done, [], "x", true);
        Assert.All(new[] { "a", "b", "c" }, n => Assert.Equal(PictureState.Solution, State(f, n)));
        Assert.Null(State(f, "d"));
        Assert.Equal(PictureState.Solution, EdgeState(f, "a-b"));
        Assert.Equal(PictureState.Solution, EdgeState(f, "b-c"));
        Assert.Equal(PictureState.Solution, EdgeState(f, "a-c"));
        Assert.Null(EdgeState(f, "c-d"));
        Assert.Empty(f.Phantoms);
        Assert.True(f.Ok);
    }

    [Fact]
    public void Clique_UnjoinedPairsBecomeRejectedPhantoms_EvenWhileInProgress() {
        foreach (var ev in new[] { StepEvent.Try, StepEvent.Reject, StepEvent.Accept, StepEvent.Done }) {
            var f = GraphSubsetFrames.Frame(Spec(SubsetRule.Clique), Set("a", "b", "d"), ev, [], "x");
            // a-d and b-d are not edges.
            Assert.Equal([("a", "d"), ("b", "d")], f.Phantoms.Select(p => (p.A, p.B)));
            Assert.All(f.Phantoms, p => Assert.Equal(PictureState.Rejected, p.State));
            // A candidate only being tried is highlighted; a set taken mid-run is Committed; the answer is Solution (#886).
            var picked = ev switch {
                StepEvent.Try or StepEvent.Reject => PictureState.ElementHighlight,
                StepEvent.Accept => PictureState.Committed,
                _ => PictureState.Solution,
            };
            Assert.Equal(picked, EdgeState(f, "a-b"));
        }
    }

    // ---- vertex cover (and minimum) ----

    [Theory]
    [InlineData("VertexCover")]
    [InlineData("MinVertexCover")]
    public void VertexCover_TouchedEdgesAreCovered(string ruleName) {
        var rule = Enum.Parse<SubsetRule>(ruleName);
        var f = GraphSubsetFrames.Frame(Spec(rule), Set("a", "c"), StepEvent.Done, [], "x", true);
        Assert.Equal(PictureState.Solution, State(f, "a"));
        Assert.Equal(PictureState.Solution, State(f, "c"));
        Assert.All(new[] { "a-b", "b-c", "c-d", "a-c" }, e => Assert.Equal(PictureState.Covered, EdgeState(f, e)));
    }

    [Theory]
    [InlineData("VertexCover")]
    [InlineData("MinVertexCover")]
    public void VertexCover_UncoveredEdgeIsRejectedOnDone_ButNotWhileBuilding(string ruleName) {
        var rule = Enum.Parse<SubsetRule>(ruleName);
        var done = GraphSubsetFrames.Frame(Spec(rule), Set("a"), StepEvent.Done, [], "x", false);
        Assert.Equal(PictureState.Covered, EdgeState(done, "a-b"));
        Assert.Equal(PictureState.Rejected, EdgeState(done, "c-d"));
        Assert.Equal(PictureState.Rejected, EdgeState(done, "b-c"));
        Assert.False(done.Ok);

        foreach (var building in new[] { StepEvent.Accept, StepEvent.Backtrack }) {
            var f = GraphSubsetFrames.Frame(Spec(rule), Set("a"), building, [], "x");
            Assert.Equal(PictureState.Covered, EdgeState(f, "a-b"));
            Assert.Null(EdgeState(f, "c-d"));   // not failed yet
        }
        // A complete candidate that fails is judged in Try and Reject too.
        Assert.Equal(PictureState.Rejected, EdgeState(GraphSubsetFrames.Frame(Spec(rule), Set("a"), StepEvent.Reject, [], "x"), "c-d"));
        Assert.Equal(PictureState.Rejected, EdgeState(GraphSubsetFrames.Frame(Spec(rule), Set("a"), StepEvent.Try, [], "x"), "c-d"));
    }

    // ---- independent set ----

    [Fact]
    public void IndependentSet_NeighboursOfChosenAreBlocked_NotChosenOnes() {
        var f = GraphSubsetFrames.Frame(Spec(SubsetRule.IndependentSet, 2), Set("a", "d"), StepEvent.Done, [], "x", true);
        Assert.Equal(PictureState.Solution, State(f, "a"));
        Assert.Equal(PictureState.Solution, State(f, "d"));
        Assert.Equal(PictureState.Blocked, State(f, "b"));   // neighbour of a
        Assert.Equal(PictureState.Blocked, State(f, "c"));   // neighbour of a and d
        Assert.Null(State(f, "e"));                            // untouched isolated node
        Assert.Empty(f.Edges);                                 // no edge inside the set
    }

    [Fact]
    public void IndependentSet_EdgesInsideTheSetAreRejected_InEveryEvent() {
        foreach (var ev in new[] { StepEvent.Try, StepEvent.Accept, StepEvent.Done }) {
            var f = GraphSubsetFrames.Frame(Spec(SubsetRule.IndependentSet), Set("a", "b"), ev, [], "x");
            Assert.Equal(PictureState.Rejected, EdgeState(f, "a-b"));
            var picked = ev switch {
                StepEvent.Try => PictureState.ElementHighlight,
                StepEvent.Accept => PictureState.Committed,
                _ => PictureState.Solution,
            };
            Assert.Equal(picked, State(f, "a"));   // chosen beats Blocked
            Assert.Equal(picked, State(f, "b"));
            Assert.Equal(PictureState.Blocked, State(f, "c"));
        }
    }

    // ---- dominating set ----

    [Fact]
    public void DominatingSet_DominatedNodesAreCovered_BoundaryEdgesCovered() {
        var f = GraphSubsetFrames.Frame(Spec(SubsetRule.DominatingSet, 2), Set("c", "e"), StepEvent.Done, [], "x", true);
        Assert.Equal(PictureState.Solution, State(f, "c"));
        Assert.Equal(PictureState.Solution, State(f, "e"));
        Assert.All(new[] { "a", "b", "d" }, n => Assert.Equal(PictureState.Covered, State(f, n)));
        Assert.All(new[] { "b-c", "c-d", "a-c" }, e => Assert.Equal(PictureState.Covered, EdgeState(f, e)));
        Assert.Null(EdgeState(f, "a-b"));   // both ends dominated, neither chosen: not touched by the set
    }

    [Fact]
    public void DominatingSet_UndominatedIsRejectedUnlessInProgressOrNothingChosen() {
        var done = GraphSubsetFrames.Frame(Spec(SubsetRule.DominatingSet), Set("d"), StepEvent.Done, [], "x", false);
        Assert.Equal(PictureState.Covered, State(done, "c"));
        Assert.Equal(PictureState.Rejected, State(done, "a"));
        Assert.Equal(PictureState.Rejected, State(done, "b"));
        Assert.Equal(PictureState.Rejected, State(done, "e"));

        var building = GraphSubsetFrames.Frame(Spec(SubsetRule.DominatingSet), Set("d"), StepEvent.Accept, [], "x");
        Assert.Null(State(building, "a"));
        Assert.Equal(PictureState.Covered, State(building, "c"));

        var empty = GraphSubsetFrames.Frame(Spec(SubsetRule.DominatingSet), Set(), StepEvent.Done, [], "x", false);
        Assert.Empty(empty.Nodes);
    }

    // ---- precedence and focus ----

    [Fact]
    public void Focus_ShowsOverSolutionAndCovered_ButNeverOverRejected() {
        // Vertex cover {a}: node a Solution, edge a-b Covered, edge c-d Rejected on Done.
        var f = GraphSubsetFrames.Frame(Spec(SubsetRule.VertexCover), Set("a"), StepEvent.Done, ["a", "a-b", "c-d", "d"], "x");
        Assert.Equal(PictureState.ElementHighlight, State(f, "a"));        // Solution < ElementHighlight
        Assert.Equal(PictureState.ElementHighlight, State(f, "d"));        // was Background
        Assert.Equal(PictureState.ElementHighlight, EdgeState(f, "a-b"));  // Covered < ElementHighlight
        Assert.Equal(PictureState.Rejected, EdgeState(f, "c-d"));          // Rejected is never hidden
        Assert.Equal(["a", "a-b", "c-d", "d"], f.Focus);
    }

    [Fact]
    public void Focus_OnAnUnknownId_IsIgnored_ButKept() {
        var f = GraphSubsetFrames.Frame(Spec(SubsetRule.Clique), Set(), StepEvent.Try, ["zzz"], "x");
        Assert.Empty(f.Nodes);
        Assert.Empty(f.Edges);
    }

    [Fact]
    public void ChosenIdsNotInTheGraph_AreIgnored() {
        var f = GraphSubsetFrames.Frame(Spec(SubsetRule.Clique), Set("a", "zzz"), StepEvent.Try, [], "x");
        Assert.Equal(["a"], f.Nodes.Keys);
        Assert.Empty(f.Phantoms);
    }

    [Fact]
    public void Frame_OmitsBackgroundEntries_AndSerializesStateNames() {
        var f = GraphSubsetFrames.Frame(Spec(SubsetRule.Clique), Set("a", "b"), StepEvent.Accept, ["b"], "Add b.");
        Assert.DoesNotContain(PictureState.Background, f.Nodes.Values.Select(v => v.State));
        string json = JsonSerializer.Serialize(f);
        Assert.Equal("{\"event\":\"Accept\",\"caption\":\"Add b.\",\"focus\":[\"b\"],"
            + "\"nodes\":{\"a\":{\"state\":\"Committed\"},\"b\":{\"state\":\"ElementHighlight\"}},"
            + "\"edges\":{\"a-b\":{\"state\":\"Committed\"}},\"phantoms\":[]}", json);   // no "ok" before Done
    }

    // ---- the whole response ----

    [Fact]
    public void Build_AddsASolvedFrameWhenTheSolverRecordedNoSteps() {
        var run = new SolveRun("{a,c}", [], null);
        var response = GraphSubsetFrames.Build(Spec(SubsetRule.VertexCover, 2), run,
            ans => new NodeSet(ans.Trim('{', '}').Split(',')), _ => true);
        Assert.Equal("Graph", response.Type);
        var frame = Assert.IsType<GraphFrame>(Assert.Single(response.Frames));
        Assert.Equal(StepEvent.Done, frame.Event);
        Assert.True(frame.Ok);
        Assert.Equal(PictureState.Solution, State(frame, "a"));
    }

    [Fact]
    public void Build_DoesNotDuplicateATrailingDoneStep_AndDropsStepsOfAnotherShape() {
        object[] typed = [
            new SolverStep<NodeSet>(Set("a"), StepEvent.Accept, ["a"], "Take a."),
            new SolverStep<NodeSet>(Set("a", "c"), StepEvent.Done, [], "Done.") { Ok = true },
        ];
        var run = new SolveRun("{a,c}", typed, typeof(NodeSet));
        var response = GraphSubsetFrames.Build(Spec(SubsetRule.VertexCover), run, _ => throw new InvalidOperationException("not needed"), _ => throw new InvalidOperationException("not needed"));
        Assert.Equal(2, response.Frames.Count);
        Assert.Equal("Take a.", ((GraphFrame)response.Frames[0]).Caption);

        // Steps of another shape cannot be drawn: only the solved frame, built from the answer, remains.
        var other = new SolveRun("{a,c}", [new SolverStep<ActiveStates>(new ActiveStates(["1"]), StepEvent.Done, [], "x") { Ok = true }], typeof(ActiveStates));
        var response2 = GraphSubsetFrames.Build(Spec(SubsetRule.VertexCover), other, ans => Set("a", "c"), _ => false);
        var only = Assert.IsType<GraphFrame>(Assert.Single(response2.Frames));
        Assert.False(only.Ok);
    }

    [Fact]
    public void Build_EmptyAnswerSaysNoAnswerWasFound() {
        var response = GraphSubsetFrames.Build(Spec(SubsetRule.Clique), new SolveRun("{}", [], null), _ => Set(), _ => false);
        var frame = (GraphFrame)Assert.Single(response.Frames);
        Assert.Equal("No answer was found.", frame.Caption);
        Assert.False(frame.Ok);
    }

    // ---- edge ids ----

    [Fact]
    public void EdgeIds_StayPlainForOrdinaryNames_AndAreUniqueWhenNamesContainSeparators() {
        Assert.Equal("a-b", GraphSubsetFrames.EdgeId("b", "a"));
        // Without escaping, both of these pairs would be "n-1-n-2".
        Assert.Equal(@"n\-1-n\-2", GraphSubsetFrames.EdgeId("n-1", "n-2"));
        Assert.Equal(@"1\-n\-2-n", GraphSubsetFrames.EdgeId("n", "1-n-2"));
        Assert.Equal(@"a\>b-a\\b", GraphSubsetFrames.EdgeId(@"a\b", "a>b"));   // "a>b" < "a\b"; a backslash is doubled

        var spec = new GraphSubsetSpec(["n-1", "n-2", "n", "1-n-2", "a", "b-c", "a-b", "c"],
            [new("n-1", "n-2"), new("n", "1-n-2"), new("a", "b-c"), new("a-b", "c"), new("n-2", "n-1")], 2, SubsetRule.Clique);
        var p = GraphSubsetFrames.Payload(spec);
        // Four distinct edges (the reversed n-2,n-1 is the first one again), each with its own id and its two ends given
        // explicitly, so a renderer never splits an id.
        Assert.Equal(4, p.Edges.Count);
        Assert.Equal(4, p.Edges.Select(e => e.Id).Distinct().Count());
        Assert.Equal([("n-1", "n-2"), ("1-n-2", "n"), ("a", "b-c"), ("a-b", "c")], p.Edges.Select(e => (e.A, e.B)));
        Assert.All(p.Edges, e => Assert.Equal(GraphSubsetFrames.EdgeId(e.A, e.B), e.Id));

        // A frame marks exactly the edge its set joins, not the other one that would have shared its id.
        var f = GraphSubsetFrames.Frame(spec, Set("n", "1-n-2"), StepEvent.Accept, [], "x");
        Assert.Equal([GraphSubsetFrames.EdgeId("n", "1-n-2")], f.Edges.Keys);
    }

    // ---- candidates being tried are highlighted, not drawn as the answer ----

    [Theory]
    [InlineData("Try")]
    [InlineData("Reject")]
    public void Frame_ACandidateBeingTried_IsElementHighlight_NeverSolution(string ev) {
        // {a,b,c} is a clique: on Done and an Accept that ends the run it is drawn as the answer, but a Try or Reject only
        // looks at it.
        var f = GraphSubsetFrames.Frame(Spec(SubsetRule.Clique), Set("a", "b", "c"), Enum.Parse<StepEvent>(ev), [], "x");
        Assert.DoesNotContain(f.Nodes.Values, s => s.State == PictureState.Solution);
        Assert.DoesNotContain(f.Edges.Values, s => s.State == PictureState.Solution);
        Assert.Equal(PictureState.ElementHighlight, State(f, "a"));
        Assert.Equal(PictureState.ElementHighlight, EdgeState(f, "a-b"));
        foreach (var answer in new[] { StepEvent.Accept, StepEvent.Done }) {
            var g = GraphSubsetFrames.Frame(Spec(SubsetRule.Clique), Set("a", "b", "c"), answer, [], "x", answer == StepEvent.Done ? true : null,
                endsRun: answer == StepEvent.Accept);
            Assert.Equal(PictureState.Solution, State(g, "a"));
            Assert.Equal(PictureState.Solution, EdgeState(g, "a-b"));
        }
    }

    // ---- Committed: kept so far, not confirmed (#886) ----

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Picked_IsSolutionOnlyForTheConfirmedAnswer(bool incremental) {
        Assert.Equal(PictureState.Solution, GraphSubsetFrames.Picked(StepEvent.Done, incremental));
        Assert.Equal(PictureState.Solution, GraphSubsetFrames.Picked(StepEvent.Accept, incremental, endsRun: true));
        Assert.Equal(PictureState.Committed, GraphSubsetFrames.Picked(StepEvent.Accept, incremental));
        Assert.Equal(PictureState.Committed, GraphSubsetFrames.Picked(StepEvent.Backtrack, incremental));
        var tried = incremental ? PictureState.Committed : PictureState.ElementHighlight;
        Assert.Equal(tried, GraphSubsetFrames.Picked(StepEvent.Try, incremental));
        Assert.Equal(tried, GraphSubsetFrames.Picked(StepEvent.Reject, incremental));
    }

    [Fact]
    public void Build_AnAcceptIsSolutionOnlyWhenACorrectDoneFollowsIt() {
        // A best so far accepted mid-run is kept but not confirmed; the Accept right before a correct Done is the answer.
        var steps = new List<object> {
            new SolverStep<NodeSet>(Set("a"), StepEvent.Accept, [], "best so far"),
            new SolverStep<NodeSet>(Set("a", "b"), StepEvent.Accept, [], "better"),
            new SolverStep<NodeSet>(Set("a", "b"), StepEvent.Done, [], "done") { Ok = true },
        };
        var frames = GraphSubsetFrames.Build(Spec(SubsetRule.Clique), new SolveRun("{a,b}", steps, typeof(NodeSet)), _ => Set(), _ => true).Frames;
        Assert.Equal(PictureState.Committed, State((GraphFrame)frames[0], "a"));
        Assert.Equal(PictureState.Solution, State((GraphFrame)frames[1], "a"));
        Assert.Equal(PictureState.Solution, EdgeState((GraphFrame)frames[1], "a-b"));
        Assert.Equal(PictureState.Solution, State((GraphFrame)frames[2], "a"));
        // An Accept followed by a Done that is not correct is not the answer.
        steps[2] = new SolverStep<NodeSet>(Set("a", "b"), StepEvent.Done, [], "done") { Ok = false };
        frames = GraphSubsetFrames.Build(Spec(SubsetRule.Clique), new SolveRun("{a,b}", steps, typeof(NodeSet)), _ => Set(), _ => false).Frames;
        Assert.Equal(PictureState.Committed, State((GraphFrame)frames[1], "a"));
    }

    // ---- incremental runs: the committed set stays Committed, only the piece under test is marked ----

    [Fact]
    public void Incremental_Reject_KeepsTheCommittedSetCommitted_AndRejectsOnlyThePieceUnderTest() {
        // A backtracking clique search holds {a,b} and skips d: {a,b} is still the clique so far.
        var f = GraphSubsetFrames.Frame(Spec(SubsetRule.Clique), Set("a", "b"), StepEvent.Reject, ["d"], "x", incremental: true);
        Assert.Equal(PictureState.Committed, State(f, "a"));
        Assert.Equal(PictureState.Committed, State(f, "b"));
        Assert.Equal(PictureState.Committed, EdgeState(f, "a-b"));
        Assert.Equal(PictureState.Rejected, State(f, "d"));
        Assert.Null(State(f, "c"));
    }

    [Fact]
    public void Incremental_Try_HighlightsOnlyThePieceUnderTest_AndJudgesNothingYet() {
        // A search holds the cover {a} and looks at c next: {a} is Committed, c is highlighted, and the edge c-d that
        // nothing covers yet is not a failure, because the cover is still being built.
        var f = GraphSubsetFrames.Frame(Spec(SubsetRule.VertexCover, 2), Set("a"), StepEvent.Try, ["c"], "x", incremental: true);
        Assert.Equal(PictureState.Committed, State(f, "a"));
        Assert.Equal(PictureState.ElementHighlight, State(f, "c"));
        Assert.Null(EdgeState(f, "c-d"));
        Assert.Equal(PictureState.Covered, EdgeState(f, "a-b"));
    }

    [Fact]
    public void Incremental_RejectOfTheCommittedSet_StillJudgesIt() {
        // Out of budget with {a}: the set is still the one built so far (Committed), and what it leaves uncovered failed.
        var f = GraphSubsetFrames.Frame(Spec(SubsetRule.VertexCover, 1), Set("a"), StepEvent.Reject, [], "x", incremental: true);
        Assert.Equal(PictureState.Committed, State(f, "a"));
        Assert.Equal(PictureState.Rejected, EdgeState(f, "c-d"));
        Assert.Equal(PictureState.Rejected, EdgeState(f, "b-c"));
    }

    [Fact]
    public void Build_ReadsIncrementalFromTheRun() {
        var steps = new List<object> {
            new SolverStep<NodeSet>(Set("a", "b"), StepEvent.Reject, ["d"], "skip d"),
            new SolverStep<NodeSet>(Set("a", "b", "c"), StepEvent.Done, [], "done") { Ok = true },
        };
        GraphFrame First(bool incremental) => (GraphFrame)GraphSubsetFrames.Build(Spec(SubsetRule.Clique, 3),
            new SolveRun("{a,b,c}", steps, typeof(NodeSet)) { Incremental = incremental }, _ => Set(), _ => true).Frames[0];
        Assert.Equal(PictureState.Committed, State(First(true), "a"));
        Assert.Equal(PictureState.Rejected, State(First(true), "d"));
        // The same step from a run that judges whole candidates is a candidate being tried.
        Assert.Equal(PictureState.ElementHighlight, State(First(false), "a"));
        Assert.Equal(PictureState.ElementHighlight, State(First(false), "d"));
    }

    // ---- runs stopped by the time limit ----

    [Fact]
    public void Build_StoppedRun_EndsWithABlankNotOkDone_EvenIfItsDoneStepCarriesACandidate() {
        // A Done step that (as before the recorder fix) carries the last rejected candidate: the picture must not draw it.
        object[] steps = [
            new SolverStep<NodeSet>(Set("a", "b"), StepEvent.Reject, ["a", "b"], "Try {a,b}: edge {c,d} isn't covered."),
            new SolverStep<NodeSet>(Set("a", "b"), StepEvent.Done, [], "Stopped after 0.05 s and 9 steps; the solver had not finished.") { Ok = false },
        ];
        var run = new SolveRun(SolveRun.TimedOutAnswer, steps, typeof(NodeSet)) { TimedOut = true };
        var response = GraphSubsetFrames.Build(Spec(SubsetRule.VertexCover, 2), run,
            _ => throw new InvalidOperationException("a stopped run's answer must not be read"), _ => throw new InvalidOperationException("nor verified"));
        Assert.Equal(2, response.Frames.Count);
        var done = Assert.IsType<GraphFrame>(response.Frames[^1]);
        Assert.Equal(StepEvent.Done, done.Event);
        Assert.False(done.Ok);
        Assert.Equal("Stopped after 0.05 s and 9 steps; the solver had not finished.", done.Caption);
        Assert.Empty(done.Nodes);   // in particular, no node is Solution
        Assert.Empty(done.Edges);   // and no edge is Solution or Rejected (an empty cover is not a failed candidate)
        Assert.Empty(done.Phantoms);
    }

    [Fact]
    public void Build_StoppedRun_FromTheRecorder_HasNoSolutionInItsDoneFrame() {
        var run = SolveRun.Record<NodeSet>(true, TimeSpan.FromMilliseconds(5), rec => {
            while (true) rec.Reject(() => Set("a", "c"), () => "Try {a,c}.", "a", "c");
        });
        Assert.True(run.TimedOut);
        var response = GraphSubsetFrames.Build(Spec(SubsetRule.Clique, 2), run,
            _ => throw new InvalidOperationException("not read"), _ => throw new InvalidOperationException("not verified"));
        var done = Assert.IsType<GraphFrame>(response.Frames[^1]);
        Assert.Equal(StepEvent.Done, done.Event);
        Assert.False(done.Ok);
        Assert.StartsWith("Stopped after ", done.Caption);
        Assert.DoesNotContain(done.Nodes.Values, s => s.State == PictureState.Solution);
        Assert.DoesNotContain(done.Edges.Values, s => s.State == PictureState.Solution);
        Assert.Single(response.Frames, f => ((GraphFrame)f).Event == StepEvent.Done);
    }

    [Fact]
    public void Build_StoppedRun_WithStepsOfAnotherShape_NeverReadsTheStoppedAnswerAsNodes() {
        object[] steps = [new SolverStep<ActiveStates>(new ActiveStates(["1"]), StepEvent.Done, [], "Stopped.") { Ok = false }];
        var run = new SolveRun(SolveRun.TimedOutAnswer, steps, typeof(ActiveStates)) { TimedOut = true };
        bool read = false;
        var response = GraphSubsetFrames.Build(Spec(SubsetRule.Clique), run, ans => { read = true; return Set(ans); }, _ => true);
        Assert.False(read);
        var done = Assert.IsType<GraphFrame>(Assert.Single(response.Frames));
        Assert.Equal(StepEvent.Done, done.Event);
        Assert.False(done.Ok);
        Assert.Equal(GraphSubsetFrames.StoppedCaption, done.Caption);
        Assert.Empty(done.Nodes);
        Assert.Empty(done.Edges);
    }

    [Fact]
    public void Build_StoppedRun_WithNoSteps_EndsWithTheStoppedFrame() {
        var run = new SolveRun(SolveRun.TimedOutAnswer, [], null) { TimedOut = true };
        var response = GraphSubsetFrames.Build(Spec(SubsetRule.VertexCover), run, _ => Set("a", "b", "c", "d"), _ => true);
        var done = Assert.IsType<GraphFrame>(Assert.Single(response.Frames));
        Assert.False(done.Ok);
        Assert.Equal(GraphSubsetFrames.StoppedCaption, done.Caption);
        Assert.Empty(done.Nodes);
        Assert.Equal("{\"event\":\"Done\",\"caption\":\"" + GraphSubsetFrames.StoppedCaption + "\",\"focus\":[],\"nodes\":{},\"edges\":{},\"phantoms\":[],\"ok\":false}",
            JsonSerializer.Serialize(done));
    }

    // ---- reasons used in solver captions ----

    [Fact]
    public void Why_NamesTheFirstProblem_OrIsEmpty() {
        var s = Spec(SubsetRule.Clique);
        Assert.Equal("a and d aren't joined", GraphSubsetFrames.Why(SubsetRule.Clique, s.Nodes, s.Edges, ["d", "a", "c"]));
        Assert.Equal("", GraphSubsetFrames.Why(SubsetRule.Clique, s.Nodes, s.Edges, ["a", "b", "c"]));
        Assert.Equal("a and b are joined", GraphSubsetFrames.Why(SubsetRule.IndependentSet, s.Nodes, s.Edges, ["a", "b", "d"]));
        Assert.Equal("", GraphSubsetFrames.Why(SubsetRule.IndependentSet, s.Nodes, s.Edges, ["a", "d"]));
        Assert.Equal("edge {c,d} isn't covered", GraphSubsetFrames.Why(SubsetRule.VertexCover, s.Nodes, s.Edges, ["a", "b"]));
        Assert.Equal("", GraphSubsetFrames.Why(SubsetRule.MinVertexCover, s.Nodes, s.Edges, ["a", "c"]));
        Assert.Equal("e isn't dominated", GraphSubsetFrames.Why(SubsetRule.DominatingSet, s.Nodes, s.Edges, ["a", "c"]));
        Assert.Equal("", GraphSubsetFrames.Why(SubsetRule.DominatingSet, s.Nodes, s.Edges, ["c", "e"]));
    }
}
