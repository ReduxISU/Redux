using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Layered Graph base: ids, layout, parallel edges and the states of each shape, without any solver.
public class LayeredFrames_Tests {
    private static LayeredEdgeInput E(string a, string b, double? w = null) => new(a, b, w);

    private static LayeredSpec Spec(LayeredRule rule, string[] nodes, params LayeredEdgeInput[] edges) => new(nodes, edges, rule);

    // Topological Sort's default instance.
    private static readonly LayeredSpec Topo = Spec(LayeredRule.TopologicalSort, ["1", "2", "3", "4", "5", "6"],
        E("1", "2"), E("1", "3"), E("2", "4"), E("3", "4"), E("4", "5"), E("3", "6"));

    // The feedback-set default: cycles 1 -> 2 -> 3 -> 1 and 2 -> 3 -> 4 -> 5 -> 2.
    private static LayeredSpec Feedback(LayeredRule rule) => Spec(rule, ["1", "2", "3", "4", "5"],
        E("1", "2"), E("2", "3"), E("3", "1"), E("4", "5"), E("5", "2"), E("3", "4")) with { K = 1 };

    private static PictureState? Node(LayeredFrame f, string id) => f.Nodes.TryGetValue(id, out var s) ? s.State : null;
    private static PictureState? Edge(LayeredFrame f, string id) => f.Edges.TryGetValue(id, out var s) ? s.State : null;

    // ---- Payload: ids, layout, parallel edges ----

    [Fact]
    public void Payload_RanksADagByLongestPath_AndOrdersEachRankByItsNeighbors() {
        var p = LayeredFrames.Payload(Topo);
        Assert.Equal(("layeredGraph", "ranked", "order", "topologicalSort"), (p.Kind, p.Layout, p.Shape, p.Rule));
        Assert.True(p.Directed);
        Assert.False(p.Weighted);
        Assert.Equal([["1"], ["2", "3"], ["4", "6"], ["5"]], p.Layers);
        Assert.Equal([0, 1, 1, 2, 3, 2], p.Nodes.Select(n => n.Layer));
        Assert.Equal(1, p.Nodes.Single(n => n.Id == "6").Row);
        Assert.All(p.Edges, e => Assert.False(e.Back));
        Assert.Equal(["1>2", "1>3", "2>4", "3>4", "4>5", "3>6"], p.Edges.Select(e => e.Id));
        Assert.All(p.Edges, e => Assert.Equal($"{e.A}>{e.B}", e.Id));
    }

    [Fact]
    public void Payload_MarksTheEdgeThatClosesACycle_AsBack_AndASelfLoopToo() {
        var p = LayeredFrames.Payload(Spec(LayeredRule.TopologicalSort, ["1", "2", "3"], E("1", "2"), E("2", "3"), E("3", "1"), E("2", "2")));
        Assert.Equal(["3>1", "2>2"], p.Edges.Where(e => e.Back).Select(e => e.Id));
        Assert.Equal([["1"], ["2"], ["3"]], p.Layers);
    }

    [Fact]
    public void Payload_SearchesFromTheSourceFirst() {
        var spec = Spec(LayeredRule.SingleSourceShortestPath, ["1", "2", "3"], E("1", "2", 1), E("2", "3", 1)) with { Directed = false, Source = "3" };
        var p = LayeredFrames.Payload(spec);
        Assert.Equal([["3"], ["2"], ["1"]], p.Layers);
        Assert.False(p.Directed);
        Assert.Equal(["1-2", "2-3"], p.Edges.Select(e => e.Id));
        Assert.Equal("3", p.Source);
    }

    [Fact]
    public void Payload_NamesWithSeparators_AreEscapedNotRefused_AndEveryIdIsUnique() {
        var spec = Spec(LayeredRule.TopologicalSort, ["a", "a-b", "b>c", @"x\y"], E("a", "a-b"), E("a-b", "b>c"), E("b>c", @"x\y"));
        var p = LayeredFrames.Payload(spec);
        Assert.Equal(["a", @"a\-b", @"b\>c", @"x\\y"], p.Nodes.Select(n => n.Id));
        Assert.Equal(["a", "a-b", "b>c", @"x\y"], p.Nodes.Select(n => n.Label));
        Assert.Equal([@"a>a\-b", @"a\-b>b\>c", @"b\>c>x\\y"], p.Edges.Select(e => e.Id));
        var ids = p.Nodes.Select(n => n.Id).Concat(p.Edges.Select(e => e.Id)).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.Equal(@"a\-b", p.Edges[0].B);
    }

    [Fact]
    public void Payload_ParallelEdges_AreOneEdgePerDirection_WithTheLightestWeight_AndEveryWeightAsTheLabel() {
        var spec = Spec(LayeredRule.SingleSourceShortestPath, ["1", "2", "3"], E("1", "2", 5), E("1", "2", 2), E("2", "3", 1), E("2", "1", 7)) with { Source = "1" };
        var p = LayeredFrames.Payload(spec);
        Assert.True(p.Weighted);
        Assert.Equal(["1>2", "2>3", "2>1"], p.Edges.Select(e => e.Id));
        Assert.Equal([2.0, 1.0, 7.0], p.Edges.Select(e => e.Weight!.Value));
        Assert.Equal(["5,2", null, null], p.Edges.Select(e => e.Label));

        // In an undirected graph both orders are the same edge.
        var undirected = LayeredFrames.Payload(spec with { Directed = false });
        Assert.Equal(["1-2", "2-3"], undirected.Edges.Select(e => e.Id));
        Assert.Equal("5,2,7", undirected.Edges[0].Label);
        Assert.Equal(2.0, undirected.Edges[0].Weight);
    }

    [Fact]
    public void Payload_SerializesTheContractFields() {
        var spec = Spec(LayeredRule.SinglePairShortestPath, ["1", "2"], E("1", "2", 3), E("1", "2", 4)) with { Source = "1", Target = "2" };
        var json = JsonSerializer.Serialize(LayeredFrames.Payload(spec), FramesResponse.JsonOptions);
        var root = JsonDocument.Parse(json).RootElement;
        Assert.Equal(["kind", "layout", "shape", "rule", "directed", "weighted", "nodes", "edges", "layers", "source", "target"], root.EnumerateObject().Select(p => p.Name));
        Assert.Equal(["id", "label", "layer", "row"], root.GetProperty("nodes")[0].EnumerateObject().Select(p => p.Name));
        Assert.Equal(["id", "a", "b", "back", "weight", "label"], root.GetProperty("edges")[0].EnumerateObject().Select(p => p.Name));
        Assert.Equal("3,4", root.GetProperty("edges")[0].GetProperty("label").GetString());
    }

    // ---- Order ----

    [Fact]
    public void Order_PlacedNodesAreCommitted_ReadyNodesCovered_TheNodeJustPlacedHighlighted() {
        // Placed so far, not confirmed: Committed until the run ends on the order (#886).
        var f = LayeredFrames.Frame(Topo, new Tour(["1", "2"]), StepEvent.Accept, ["2"], "Place 2.", incremental: true);
        Assert.Equal(["1", "2"], f.Order);
        Assert.Equal(PictureState.Committed, Node(f, "1"));
        Assert.Equal(PictureState.ElementHighlight, Node(f, "2"));
        Assert.Equal(PictureState.Covered, Node(f, "3"));
        Assert.Null(Node(f, "4")); // still waits for 3
        Assert.Equal(PictureState.Committed, Edge(f, "1>2"));
        Assert.Equal(PictureState.Covered, Edge(f, "1>3"));
        Assert.Equal(PictureState.Covered, Edge(f, "2>4"));
    }

    [Fact]
    public void Order_ADoneThatFails_ShowsTheStuckNodesAndACycle() {
        var spec = Spec(LayeredRule.TopologicalSort, ["1", "2", "3", "4", "5"], E("1", "2"), E("2", "3"), E("3", "4"), E("4", "2"), E("3", "5"));
        var f = LayeredFrames.Frame(spec, new Tour([]), StepEvent.Done, [], "No order.", false, incremental: true);
        Assert.Empty(f.Order!);
        Assert.Null(Node(f, "1"));
        Assert.All(["2", "3", "4", "5"], n => Assert.Equal(PictureState.Rejected, Node(f, n)));
        Assert.Equal(["2>3", "3>4", "4>2"], f.Edges.Where(e => e.Value.State == PictureState.Rejected).Select(e => e.Key).Order());
        Assert.False(f.Ok);
    }

    [Fact]
    public void Order_ADoneThatSucceeds_IsAllSolution() {
        var f = LayeredFrames.Frame(Topo, new Tour(["1", "2", "3", "4", "6", "5"]), StepEvent.Done, [], "Done.", true, incremental: true);
        Assert.All(f.Nodes.Values, s => Assert.Equal(PictureState.Solution, s.State));
        Assert.Equal(6, f.Edges.Count);
        Assert.All(f.Edges.Values, s => Assert.Equal(PictureState.Solution, s.State));
    }

    // ---- Removed set ----

    [Fact]
    public void ArcSet_ATriedCandidateIsHighlighted_AndTheCycleThatSurvivesIsRejected() {
        var f = LayeredFrames.Frame(Feedback(LayeredRule.FeedbackArcSet), new EdgeSet(["1>2"]), StepEvent.Reject, ["1>2"], "Try.");
        Assert.Equal(PictureState.ElementHighlight, Edge(f, "1>2"));
        Assert.Equal(["2>3", "3>4", "4>5", "5>2"], f.Edges.Where(e => e.Value.State == PictureState.Rejected).Select(e => e.Key).Order());
        Assert.Empty(f.Nodes);
    }

    [Fact]
    public void ArcSet_TheAnswerIsSolution_AndNoCycleIsLeft() {
        foreach (var ev in new[] { StepEvent.Accept, StepEvent.Done }) {
            var f = LayeredFrames.Frame(Feedback(LayeredRule.FeedbackArcSet), new EdgeSet(["2>3"]), ev, [], "Found.", ev == StepEvent.Done ? true : null,
                endsRun: ev == StepEvent.Accept);
            Assert.Equal(PictureState.Solution, Edge(f, "2>3"));
            Assert.Single(f.Edges);
        }
        // An Accept the run goes on from is kept, not confirmed (#886).
        var kept = LayeredFrames.Frame(Feedback(LayeredRule.FeedbackArcSet), new EdgeSet(["2>3"]), StepEvent.Accept, [], "Found.");
        Assert.Equal(PictureState.Committed, Edge(kept, "2>3"));
    }

    [Fact]
    public void NodeSet_RemovedNodesTakeTheirEdges_AndTheSurvivingCycleIsRejected() {
        var f = LayeredFrames.Frame(Feedback(LayeredRule.FeedbackNodeSet), new NodeSet(["1"]), StepEvent.Reject, ["1"], "Try.");
        Assert.Equal(PictureState.ElementHighlight, Node(f, "1"));
        Assert.Equal(PictureState.Untraveled, Edge(f, "1>2"));
        Assert.Equal(PictureState.Untraveled, Edge(f, "3>1"));
        Assert.Equal(PictureState.Rejected, Edge(f, "2>3"));
        Assert.Equal(PictureState.Rejected, Edge(f, "5>2"));

        var done = LayeredFrames.Frame(Feedback(LayeredRule.FeedbackNodeSet), new NodeSet(["2"]), StepEvent.Done, [], "Found.", true);
        Assert.Equal(PictureState.Solution, Node(done, "2"));
        Assert.DoesNotContain(done.Edges.Values, s => s.State == PictureState.Rejected);
    }

    // ---- Groups ----

    [Fact]
    public void Groups_VisitedNodesAreCovered_GroupedNodesAreColoredNotStated_AndEdgesInsideAComponentAreSolution() {
        var spec = Spec(LayeredRule.StronglyConnectedComponents, ["1", "2", "3", "4", "5"],
            E("1", "2"), E("2", "3"), E("3", "1"), E("3", "4"), E("4", "5"), E("5", "4"));
        var pass1 = LayeredFrames.Frame(spec, new Components([], ["1", "2"], ["2"]), StepEvent.Try, ["1"], "Pass 1.", incremental: true);
        Assert.Equal(PictureState.ElementHighlight, Node(pass1, "1"));
        Assert.Equal(PictureState.Covered, Node(pass1, "2"));
        Assert.Equal(["2"], pass1.Finish);
        Assert.Empty(pass1.Groups!);
        // A component found mid-run is kept so far (Committed, #886).
        var found = LayeredFrames.Frame(spec, new Components(new Dictionary<string, int> { ["4"] = 0, ["5"] = 0 }, ["1", "2", "3", "4", "5"], ["5", "4"]),
            StepEvent.Accept, [], "Found {4,5}.", incremental: true);
        Assert.Equal(PictureState.Committed, Edge(found, "4>5"));

        var groups = new Dictionary<string, int> { ["1"] = 0, ["2"] = 0, ["3"] = 0, ["4"] = 1, ["5"] = 1 };
        var done = LayeredFrames.Frame(spec, new Components(groups, groups.Keys, ["5", "4", "3", "2", "1"]), StepEvent.Done, [], "Done.", true, incremental: true);
        Assert.Empty(done.Nodes);
        Assert.Equal(groups, done.Groups);
        Assert.Equal(["1>2", "2>3", "3>1", "4>5", "5>4"], done.Edges.Keys);
        Assert.All(done.Edges.Values, s => Assert.Equal(PictureState.Solution, s.State));
    }

    // ---- Distances ----

    private static readonly LayeredSpec Sssp = Spec(LayeredRule.SingleSourceShortestPath, ["1", "2", "3", "4"],
        E("1", "2", 4), E("1", "3", 2), E("3", "2", 1)) with { Source = "1" };

    [Fact]
    public void Distances_SettledIsSolution_ReachedIsCovered_EveryNodeHasADistance() {
        var d = new Distances(new Dictionary<string, int> { ["1"] = 0, ["2"] = 4, ["3"] = 2 }, new Dictionary<string, string> { ["2"] = "1", ["3"] = "1" }, ["1"]);
        var f = LayeredFrames.Frame(Sssp, d, StepEvent.Accept, ["1", "1>2", "1>3"], "Settle 1.", incremental: true);
        Assert.Equal(PictureState.ElementHighlight, Node(f, "1"));
        Assert.Equal(PictureState.Covered, Node(f, "2"));
        Assert.Equal(PictureState.ElementHighlight, Edge(f, "1>2"));
        Assert.Null(Node(f, "4"));
        // A settled node is kept so far until the run ends: Committed, not Solution (#886).
        var later = LayeredFrames.Frame(Sssp, new Distances(d.Dist, d.Pred, ["1", "3"]), StepEvent.Accept, ["3"], "Settle 3.", incremental: true);
        Assert.Equal(PictureState.Committed, Node(later, "1"));
        Assert.Equal(PictureState.Committed, Edge(later, "1>3"));
        Assert.Equal(new Dictionary<string, int?> { ["1"] = 0, ["2"] = 4, ["3"] = 2, ["4"] = null }, f.Dist);
        Assert.Equal(new Dictionary<string, string> { ["2"] = "1", ["3"] = "1" }, f.Pred);

        var json = JsonSerializer.Serialize(f, FramesResponse.JsonOptions);
        Assert.Contains("\"4\": null", json);
    }

    [Fact]
    public void Distances_SingleSourceDone_IsTheTree_WithUnreachedNodesUntraveled() {
        var d = new Distances(new Dictionary<string, int> { ["1"] = 0, ["2"] = 3, ["3"] = 2 }, new Dictionary<string, string> { ["2"] = "3", ["3"] = "1" }, ["1", "2", "3"]);
        var f = LayeredFrames.Frame(Sssp, d, StepEvent.Done, [], "Done.", true, incremental: true);
        Assert.Equal(PictureState.Solution, Node(f, "2"));
        Assert.Equal(PictureState.Untraveled, Node(f, "4"));
        Assert.Equal(PictureState.Solution, Edge(f, "3>2"));
        Assert.Equal(PictureState.Solution, Edge(f, "1>3"));
        Assert.Null(Edge(f, "1>2"));
    }

    [Fact]
    public void Distances_SinglePairDone_OnlyThePathIsSolution_AndAnUnreachedTargetIsRejected() {
        var spsp = Sssp with { Rule = LayeredRule.SinglePairShortestPath, Target = "2" };
        var d = new Distances(new Dictionary<string, int> { ["1"] = 0, ["2"] = 3, ["3"] = 2 }, new Dictionary<string, string> { ["2"] = "3", ["3"] = "1" }, ["1", "2", "3"]);
        var f = LayeredFrames.Frame(spsp, d, StepEvent.Done, [], "Done.", true, incremental: true);
        Assert.All(["1", "3", "2"], n => Assert.Equal(PictureState.Solution, Node(f, n)));
        Assert.Equal(PictureState.Solution, Edge(f, "3>2"));

        var lost = LayeredFrames.Frame(spsp with { Target = "4" }, d, StepEvent.Done, [], "Not reached.", false, incremental: true);
        Assert.Equal(PictureState.Rejected, Node(lost, "4"));
        Assert.Equal(PictureState.Covered, Node(lost, "1"));
        Assert.DoesNotContain(lost.Nodes.Values, s => s.State == PictureState.Solution);
    }

    // ---- The whole response ----

    [Fact]
    public void Build_AStoppedRun_EndsBlankAndNotOk_AndNeverReadsTheAnswer() {
        object[] steps = [
            new SolverStep<EdgeSet>(new EdgeSet(["1>2"]), StepEvent.Reject, ["1>2"], "Try removing {(1,2)}."),
            new SolverStep<EdgeSet>(new EdgeSet([]), StepEvent.Done, [], "Stopped after 0.05 s and 1 step; the solver had not finished.") { Ok = false },
        ];
        var run = new SolveRun(SolveRun.TimedOutAnswer, steps, typeof(EdgeSet)) { TimedOut = true };
        var response = LayeredFrames.Build(Feedback(LayeredRule.FeedbackArcSet), run, _ => throw new InvalidOperationException("read"), _ => throw new InvalidOperationException("verified"));
        Assert.Equal("LayeredGraph", response.Type);
        var done = Assert.IsType<LayeredFrame>(response.Frames[^1]);
        Assert.Equal((StepEvent.Done, false), (done.Event, done.Ok));
        Assert.Empty(done.Nodes);
        Assert.Empty(done.Edges);

        // Without its own Done step a stopped run still ends with one, with the shape's empty annotation.
        var bare = new SolveRun(SolveRun.TimedOutAnswer, [], typeof(Distances)) { TimedOut = true };
        var last = Assert.IsType<LayeredFrame>(Assert.Single(LayeredFrames.Build(Sssp, bare, _ => throw new InvalidOperationException(), _ => false).Frames));
        Assert.False(last.Ok);
        Assert.All(last.Dist!.Values, v => Assert.Null(v));
        Assert.Empty(last.Pred!);
    }

    [Fact]
    public void Build_StepsOfAnotherShape_EndWithTheAnswerReadFromItsString() {
        var run = new SolveRun("{(1,{1}),(2,{1,3,2}),(3,{1,3}),(4,{})}", [], null);
        var spec = Sssp;
        var response = LayeredFrames.Build(spec, run, a => LayeredFrames.ParseAnswer(spec, a), _ => true);
        var done = Assert.IsType<LayeredFrame>(Assert.Single(response.Frames));
        Assert.Equal(new Dictionary<string, int?> { ["1"] = 0, ["2"] = 3, ["3"] = 2, ["4"] = null }, done.Dist);
        Assert.Equal(PictureState.Untraveled, Node(done, "4"));
        Assert.True(done.Ok);
    }

    [Fact]
    public void ParseAnswer_ReadsEveryRulesAnswerFormat() {
        Assert.Equal(new Tour(["1", "2", "3"]), LayeredFrames.ParseAnswer(Topo, "(1,2,3)"));
        Assert.Equal(new Tour([]), LayeredFrames.ParseAnswer(Topo, "()"));
        Assert.Equal(new EdgeSet(["2>3", "4>1"]), LayeredFrames.ParseAnswer(Feedback(LayeredRule.FeedbackArcSet), "{(2,3),(4,1)}"));
        Assert.Equal(new NodeSet(["2"]), LayeredFrames.ParseAnswer(Feedback(LayeredRule.FeedbackNodeSet), "{2}"));
        var c = (Components)LayeredFrames.ParseAnswer(Spec(LayeredRule.StronglyConnectedComponents, ["1", "2", "3"]), "{{1,2},{3}}");
        Assert.Equal(new Dictionary<string, int> { ["1"] = 0, ["2"] = 0, ["3"] = 1 }, c.Of);
        var spsp = Sssp with { Rule = LayeredRule.SinglePairShortestPath, Target = "2" };
        Assert.Equal(new Distances(new Dictionary<string, int> { ["1"] = 0, ["3"] = 2, ["2"] = 3 }, new Dictionary<string, string> { ["3"] = "1", ["2"] = "3" }, ["1", "3", "2"]),
            LayeredFrames.ParseAnswer(spsp, "{1,3,2}"));
    }

    [Fact]
    public void SurvivingCycle_NamesACycleLeftAfterARemoval() {
        var cycle = LayeredFrames.SurvivingCycle(Feedback(LayeredRule.FeedbackArcSet));
        Assert.Equal("2 → 3 → 4 → 5 → 2", cycle(["1>2"], []));
        Assert.Equal("", cycle(["2>3"], []));
        Assert.Equal("1 → 2 → 3 → 1", cycle([], ["4"]));
    }
}
