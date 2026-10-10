using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Unit tests of the state derivation for the Graph picture's edge and cut shapes: (graph, rule, partial EdgeSet or
// Groups, step event, focus) -> named states. No endpoint, no solver.
public class GraphEdgeFrames_Tests {
    // a-b (1), b-c (2), c-d (1), a-c (3), and an isolated e; "c,b" is reversed on purpose:
    //     a --- b
    //      \   /
    //        c --- d        e
    private static GraphEdgeSpec Spec(EdgeRule rule, int? k = null, params string[] terminals) => new(
        ["a", "b", "c", "d", "e"],
        [new("a", "b", 1), new("c", "b", 2), new("c", "d", 1), new("a", "c", 3)],
        rule) { K = k, Terminals = terminals };

    private static EdgeSet Set(params string[] edges) => new(edges);
    private static Groups Split(params string[] sSide) => GraphEdgeFrames.Split(["a", "b", "c", "d", "e"], sSide);

    private static PictureState? State(GraphFrame f, string node) => f.Nodes.TryGetValue(node, out var s) ? s.State : null;
    private static PictureState? EdgeState(GraphFrame f, string edge) => f.Edges.TryGetValue(edge, out var s) ? s.State : null;

    // ---- payload ----

    [Fact]
    public void Payload_CarriesWeightsCanonicalIdsAndNoColors() {
        var p = GraphEdgeFrames.Payload(Spec(EdgeRule.SpanningTree));
        Assert.Equal(["a-b", "b-c", "c-d", "a-c"], p.Edges.Select(e => e.Id));
        Assert.Equal([1d, 2d, 1d, 3d], p.Edges.Select(e => e.Weight));
        Assert.Equal("edges", p.Shape);
        Assert.Equal("spanningTree", p.Rule);
        string json = JsonSerializer.Serialize(p);
        Assert.Contains("\"weight\":2", json);
        Assert.DoesNotContain("color", json);
        Assert.DoesNotContain("terminal", json);
        Assert.DoesNotContain("\"k\"", json);
    }

    [Fact]
    public void Payload_UnweightedProblemsCarryNoWeight() {
        var spec = new GraphEdgeSpec(["a", "b"], [new("a", "b")], EdgeRule.Cut) { K = 1 };
        string json = JsonSerializer.Serialize(GraphEdgeFrames.Payload(spec));
        Assert.DoesNotContain("weight", json);
        Assert.Contains("\"k\":1", json);
    }

    [Fact]
    public void Payload_MarksOnlyTheTerminals() {
        var p = GraphEdgeFrames.Payload(Spec(EdgeRule.SteinerTree, 3, "a", "d", "zz"));   // zz is not a node: ignored
        Assert.Equal(["a", "d"], p.Nodes.Where(n => n.Terminal == true).Select(n => n.Id));
        Assert.All(p.Nodes.Where(n => n.Terminal != true), n => Assert.Null(n.Terminal));
        Assert.Equal(2, JsonSerializer.Serialize(p).Split("\"terminal\":true").Length - 1);
    }

    [Fact]
    public void Payload_DropsLoops_AndKeepsTheLightestOfParallelEdges() {
        var spec = new GraphEdgeSpec(["a", "b"], [new("a", "b", 5), new("b", "a", 2), new("a", "a", 1), new("a", "b", 9)], EdgeRule.SpanningTree);
        var edges = GraphEdgeFrames.Payload(spec).Edges;
        Assert.Equal("a-b", Assert.Single(edges).Id);
        Assert.Equal(2d, edges[0].Weight);
    }

    [Theory]
    [InlineData("SpanningTree", "spanningTree", "edges")]
    [InlineData("SteinerTree", "steinerTree", "edges")]
    [InlineData("Cut", "cut", "cut")]
    [InlineData("WeightedCut", "weightedCut", "cut")]
    [InlineData("MaxCut", "maxCut", "cut")]
    [InlineData("MinCut", "minCut", "cut")]
    public void RuleAndShapeNames_AreTheContractsSpelling(string rule, string name, string shape) {
        Assert.Equal(name, GraphEdgeFrames.RuleName(Enum.Parse<EdgeRule>(rule)));
        Assert.Equal(shape, GraphEdgeFrames.ShapeName(Enum.Parse<EdgeRule>(rule)));
    }

    // ---- edges answer ----

    [Fact]
    public void Edges_ChosenAreSolution_TheirEndsCovered_TheRestBackground() {
        var f = GraphEdgeFrames.EdgeFrame(Spec(EdgeRule.SpanningTree), Set("a-b", "b-c"), StepEvent.Accept, [], "x");
        Assert.Equal(PictureState.Solution, EdgeState(f, "a-b"));
        Assert.Equal(PictureState.Solution, EdgeState(f, "b-c"));
        Assert.Null(EdgeState(f, "a-c"));
        Assert.Null(EdgeState(f, "c-d"));
        Assert.All(new[] { "a", "b", "c" }, n => Assert.Equal(PictureState.Covered, State(f, n)));
        Assert.Null(State(f, "d"));
        Assert.Null(State(f, "e"));
        Assert.Empty(f.Phantoms);
        Assert.Null(f.Groups);
    }

    [Fact]
    public void Edges_TheEdgeBeingTriedIsHighlighted_EvenOverASolutionEdge() {
        var f = GraphEdgeFrames.EdgeFrame(Spec(EdgeRule.SpanningTree), Set("a-b"), StepEvent.Try, ["a-c"], "x");
        Assert.Equal(PictureState.ElementHighlight, EdgeState(f, "a-c"));
        var added = GraphEdgeFrames.EdgeFrame(Spec(EdgeRule.SpanningTree), Set("a-b"), StepEvent.Accept, ["a-b"], "x");
        Assert.Equal(PictureState.ElementHighlight, EdgeState(added, "a-b"));
    }

    [Fact]
    public void Edges_AnEdgeThatWouldCloseACycle_IsRejectedInARejectStep() {
        var f = GraphEdgeFrames.EdgeFrame(Spec(EdgeRule.SpanningTree), Set("a-b", "b-c"), StepEvent.Reject, ["a-c"], "x");
        Assert.Equal(PictureState.Rejected, EdgeState(f, "a-c"));
        Assert.Equal(PictureState.Solution, EdgeState(f, "a-b"));
    }

    [Fact]
    public void Edges_AnUnreachedTerminalIsRejectedInARejectStep_AndNodeFocusIsHighlightedOtherwise() {
        var reject = GraphEdgeFrames.EdgeFrame(Spec(EdgeRule.SteinerTree, 3, "a", "d"), Set("a-b"), StepEvent.Reject, ["d"], "x");
        Assert.Equal(PictureState.Rejected, State(reject, "d"));
        var start = GraphEdgeFrames.EdgeFrame(Spec(EdgeRule.SpanningTree), Set(), StepEvent.Try, ["a"], "x");
        Assert.Equal(PictureState.ElementHighlight, State(start, "a"));
        Assert.Empty(start.Edges);
    }

    [Fact]
    public void Edges_IgnoreEdgeKeysThatAreNotInTheGraph() {
        var f = GraphEdgeFrames.EdgeFrame(Spec(EdgeRule.SpanningTree), Set("x-y", "b-c"), StepEvent.Done, [], "x", true);
        Assert.Equal(["b-c"], f.Edges.Keys);
        Assert.Equal(["b", "c"], f.Nodes.Keys);
        Assert.True(f.Ok);
    }

    [Fact]
    public void Edges_Serialization_OmitsGroupsAndBackground() {
        string json = JsonSerializer.Serialize(GraphEdgeFrames.EdgeFrame(Spec(EdgeRule.SpanningTree), Set("a-b"), StepEvent.Accept, ["a-b"], "x"), FramesResponse.JsonOptions);
        Assert.DoesNotContain("groups", json);
        Assert.DoesNotContain("Background", json);
        Assert.DoesNotContain("\"ok\"", json);
    }

    // ---- cut answer ----

    [Fact]
    public void Cut_EdgesBetweenTheSidesAreSolution_TheFrameCarriesTheGroups() {
        var f = GraphEdgeFrames.CutFrame(Spec(EdgeRule.MaxCut), Split("a", "d"), StepEvent.Accept, [], "x");
        Assert.Equal(["a-b", "a-c", "c-d"], f.Edges.Keys);
        Assert.All(f.Edges.Values, s => Assert.Equal(PictureState.Solution, s.State));
        Assert.Null(EdgeState(f, "b-c"));
        Assert.Equal(new Dictionary<string, int> { ["a"] = 0, ["b"] = 1, ["c"] = 1, ["d"] = 0, ["e"] = 1 }, f.Groups);
        Assert.Empty(f.Nodes);
    }

    [Fact]
    public void Cut_AnEmptySplitHasNoCutEdgesAndEmptyGroups() {
        var f = GraphEdgeFrames.CutFrame(Spec(EdgeRule.Cut, 2), new Groups([]), StepEvent.Done, [], "x", false);
        Assert.Empty(f.Edges);
        Assert.Empty(f.Groups!);
        Assert.False(f.Ok);
        Assert.Contains("\"groups\": {}", JsonSerializer.Serialize(f, FramesResponse.JsonOptions));
    }

    [Fact]
    public void Cut_FocusIsHighlighted_EvenInARejectStep() {
        var f = GraphEdgeFrames.CutFrame(Spec(EdgeRule.MinCut), Split("d"), StepEvent.Reject, ["d"], "x");
        Assert.Equal(PictureState.ElementHighlight, State(f, "d"));
    }

    [Fact]
    public void Cut_IgnoresNodesThatAreNotInTheGraph() {
        var f = GraphEdgeFrames.CutFrame(Spec(EdgeRule.Cut, 1), new Groups([new("a", 0), new("zz", 1)]), StepEvent.Try, [], "x");
        Assert.Equal(["a"], f.Groups!.Keys);
        Assert.Empty(f.Edges);
    }

    // ---- reading answers ----

    [Theory]
    [InlineData("{{1,2},{3,4}}", new[] { "1-2", "3-4" })]
    [InlineData("{{4,3},{2,1}}", new[] { "1-2", "3-4" })]
    [InlineData("{({2,1},5),({1,3},4)}", new[] { "1-2", "1-3" })]
    [InlineData("{{ a , b }}", new[] { "a-b" })]
    [InlineData("{}", new string[0])]
    [InlineData("nonsense", new string[0])]
    public void ParseEdges_ReadsPlainAndWeightedEdgeLists(string answer, string[] expected) =>
        Assert.Equal(expected, GraphEdgeFrames.ParseEdges(answer).Edges);

    [Fact]
    public void PartitionFromCutEdges_RecoversASplitWithExactlyThoseCrossings() {
        var spec = Spec(EdgeRule.Cut, 3);
        var groups = GraphEdgeFrames.PartitionFromCutEdges(spec, "{{a,b},{c,d},{a,c}}");
        var f = GraphEdgeFrames.CutFrame(spec, groups, StepEvent.Done, [], "x");
        Assert.Equal(["a-b", "a-c", "c-d"], f.Edges.Keys);
        Assert.Equal(groups.Of["a"], groups.Of["d"]);
        Assert.NotEqual(groups.Of["a"], groups.Of["b"]);
    }

    [Fact]
    public void PartitionFromCutEdges_GivesNoSplitWhenTheEdgesCannotBeACut() {
        // a-b, b-c and a-c cannot all cross one split (a triangle has an even number of crossings).
        Assert.Empty(GraphEdgeFrames.PartitionFromCutEdges(Spec(EdgeRule.Cut, 3), "{{a,b},{b,c},{a,c}}").Of);
        Assert.Empty(GraphEdgeFrames.PartitionFromCutEdges(Spec(EdgeRule.Cut, 3), "{}").Of);
    }

    [Fact]
    public void Crossing_CountsDistinctEdgesAndSumsWeights() {
        var edges = new GraphEdgeInput[] { new("a", "b", 4), new("b", "a", 6), new("b", "c", 2), new("c", "c", 9) };
        var (count, weight) = GraphEdgeFrames.Crossing(edges, new HashSet<string> { "a" });
        Assert.Equal(1, count);
        Assert.Equal(4d, weight);   // the lightest of the parallel a-b edges is the one drawn
    }

    // ---- runs stopped by the time limit ----

    [Fact]
    public void BuildEdges_StoppedRun_EndsWithABlankNotOkDone_AndNeverReadsTheAnswer() {
        object[] steps = [
            new SolverStep<EdgeSet>(Set("a-b", "c-d"), StepEvent.Reject, [], "Try edges a-b, c-d: the edges form more than one piece."),
            new SolverStep<EdgeSet>(Set("a-b", "c-d"), StepEvent.Done, [], "Stopped after 0.05 s and 2 steps; the solver had not finished.") { Ok = false },
        ];
        var run = new SolveRun(SolveRun.TimedOutAnswer, steps, typeof(EdgeSet)) { TimedOut = true };
        var response = GraphEdgeFrames.BuildEdges(Spec(EdgeRule.SteinerTree, 2, "a", "d"), run,
            _ => throw new InvalidOperationException("not read"), _ => throw new InvalidOperationException("not verified"));
        var done = Assert.IsType<GraphFrame>(response.Frames[^1]);
        Assert.Equal(StepEvent.Done, done.Event);
        Assert.False(done.Ok);
        Assert.StartsWith("Stopped after", done.Caption);
        Assert.DoesNotContain(done.Edges.Values, s => s.State == PictureState.Solution);
        Assert.Empty(done.Nodes);

        // Steps that cannot be drawn: the stopped frame is added, and the answer string is not read as edges.
        var other = new SolveRun(SolveRun.TimedOutAnswer, [], null) { TimedOut = true };
        var only = Assert.IsType<GraphFrame>(Assert.Single(GraphEdgeFrames.BuildEdges(Spec(EdgeRule.SpanningTree), other,
            _ => throw new InvalidOperationException("not read"), _ => true).Frames));
        Assert.Equal(GraphSubsetFrames.StoppedCaption, only.Caption);
        Assert.False(only.Ok);
        Assert.Empty(only.Edges);
    }

    [Fact]
    public void BuildCut_StoppedRun_EndsWithABlankNotOkDone_WithAnEmptySplit() {
        object[] steps = [
            new SolverStep<Groups>(Split("a", "b"), StepEvent.Reject, [], "Put {a,b} against {c,d,e}: 2 edges cross, not 3."),
            new SolverStep<Groups>(Split("a", "b"), StepEvent.Done, [], "Stopped after 0.05 s and 2 steps; the solver had not finished.") { Ok = false },
        ];
        var run = new SolveRun(SolveRun.TimedOutAnswer, steps, typeof(Groups)) { TimedOut = true };
        var response = GraphEdgeFrames.BuildCut(Spec(EdgeRule.Cut, 3), run,
            _ => throw new InvalidOperationException("not read"), _ => throw new InvalidOperationException("not verified"));
        var done = Assert.IsType<GraphFrame>(response.Frames[^1]);
        Assert.False(done.Ok);
        Assert.Empty(done.Edges);   // the last rejected split's crossing edges are not drawn as the cut
        Assert.NotNull(done.Groups);
        Assert.Empty(done.Groups);
        Assert.Equal(JsonSerializer.Serialize(response.Frames[0]).Contains("\"groups\""), JsonSerializer.Serialize(done).Contains("\"groups\":{}"));

        var none = new SolveRun(SolveRun.TimedOutAnswer, [], null) { TimedOut = true };
        var only = Assert.IsType<GraphFrame>(Assert.Single(GraphEdgeFrames.BuildCut(Spec(EdgeRule.Cut, 3), none,
            _ => throw new InvalidOperationException("not read"), _ => true).Frames));
        Assert.Equal(GraphSubsetFrames.StoppedCaption, only.Caption);
        Assert.Empty(only.Groups!);
    }
}
