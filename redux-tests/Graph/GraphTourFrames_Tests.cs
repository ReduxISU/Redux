using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Unit tests of the state derivation for the Graph picture's tour shape (Hamiltonian Cycle, Directed Hamiltonian Cycle,
// Traveling Salesperson): (graph, rule, partial Tour, step event, focus) -> named states, phantoms and the order
// annotation. No endpoint, no solver.
public class GraphTourFrames_Tests {
    // A square 1-2-3-4 with the diagonal 1-3 and no 2-4:
    //     1 --- 2
    //     | \   |
    //     4 --- 3     (edges 1-2, 2-3, 3-4, 4-1, 3-1; listed with mixed endpoint order on purpose)
    private static readonly string[] Nodes = ["1", "2", "3", "4"];
    private static readonly GraphTourEdge[] Edges = [new("1", "2"), new("3", "2"), new("3", "4"), new("4", "1"), new("3", "1")];

    private static GraphTourSpec Undirected(TourRule rule = TourRule.HamiltonianCycle) => new(Nodes, Edges, false, null, rule);
    private static GraphTourSpec Directed() => new(Nodes, [new("1", "2"), new("2", "3"), new("3", "4"), new("4", "1"), new("2", "1")], true, null, TourRule.HamiltonianCycle);

    private static Tour Of(params string[] order) => new(order);

    private static PictureState? State(GraphFrame f, string node) => f.Nodes.TryGetValue(node, out var s) ? s.State : null;
    private static PictureState? EdgeState(GraphFrame f, string edge) => f.Edges.TryGetValue(edge, out var s) ? s.State : null;

    // ---- payload ----

    [Theory]
    [InlineData("HamiltonianCycle", "hamiltonianCycle")]
    [InlineData("TravelingSalesperson", "tsp")]
    public void Payload_IsTheTourShape_OnACircle_WithCanonicalUndirectedEdges(string rule, string name) {
        var p = GraphTourFrames.Payload(Undirected(Enum.Parse<TourRule>(rule)));
        Assert.Equal("graph", p.Kind);
        Assert.Equal("undirected", p.Subtype);
        Assert.Equal("circle", p.Layout);
        Assert.Equal("tour", p.Shape);
        Assert.Equal(name, p.Rule);
        Assert.Equal(name, GraphTourFrames.RuleName(Enum.Parse<TourRule>(rule)));
        Assert.Equal(["1-2", "2-3", "3-4", "1-4", "1-3"], p.Edges.Select(e => e.Id));
        Assert.Equal([("1", "2"), ("2", "3"), ("3", "4"), ("1", "4"), ("1", "3")], p.Edges.Select(e => (e.A, e.B)));
        string json = JsonSerializer.Serialize(p);
        Assert.DoesNotContain("\"k\"", json);
        Assert.DoesNotContain("weight", json);
        Assert.DoesNotContain("\"state\"", json);
    }

    [Fact]
    public void Payload_Tsp_CarriesWeightsAndK() {
        var spec = new GraphTourSpec(["a", "b", "c"], [new("a", "b", 790), new("c", "b", 5), new("c", "a", 2.5)], false, 8000, TourRule.TravelingSalesperson);
        var p = GraphTourFrames.Payload(spec);
        Assert.Equal(8000, p.K);
        Assert.Equal([790, 5, 2.5], p.Edges.Select(e => e.Weight!.Value));
        Assert.Equal(["a-b", "b-c", "a-c"], p.Edges.Select(e => e.Id));
        string json = JsonSerializer.Serialize(p);
        Assert.Contains("\"weight\":790", json);
        Assert.Contains("\"k\":8000", json);
    }

    [Fact]
    public void Payload_Directed_SaysSo_AndKeepsTheDirectionInTheIdAndEnds() {
        var p = GraphTourFrames.Payload(Directed());
        Assert.Equal("directed", p.Subtype);
        Assert.Equal(["1>2", "2>3", "3>4", "4>1", "2>1"], p.Edges.Select(e => e.Id));   // 1>2 and 2>1 are two edges
        Assert.Equal([("1", "2"), ("2", "3"), ("3", "4"), ("4", "1"), ("2", "1")], p.Edges.Select(e => (e.A, e.B)));   // a is the tail, b the head
    }

    [Fact]
    public void Payload_DropsLoopsAndDuplicates_ButNotTheReverseOfADirectedEdge() {
        var undirected = new GraphTourSpec(["a", "b"], [new("a", "b"), new("b", "a"), new("a", "a")], false, null, TourRule.HamiltonianCycle);
        Assert.Equal("a-b", Assert.Single(GraphTourFrames.Payload(undirected).Edges).Id);
        var directed = new GraphTourSpec(["a", "b"], [new("a", "b"), new("b", "a"), new("a", "b"), new("b", "b")], true, null, TourRule.HamiltonianCycle);
        Assert.Equal(["a>b", "b>a"], GraphTourFrames.Payload(directed).Edges.Select(e => e.Id));
    }

    [Fact]
    public void EdgeId_IsSortedWhenUndirected_AndTailFirstWhenDirected() {
        Assert.Equal("1-2", GraphTourFrames.EdgeId(false, "2", "1"));
        Assert.Equal("2>1", GraphTourFrames.EdgeId(true, "2", "1"));
    }

    // ---- frames ----

    [Fact]
    public void APathInProgress_HasItsEdgesSolution_ItsNodesCovered_AndTheOrderAnnotation() {
        var f = GraphTourFrames.Frame(Undirected(), Of("1", "2", "3"), StepEvent.Accept, ["3", "2-3"], "x");
        Assert.Equal(["1", "2", "3"], f.Order);
        Assert.Equal(PictureState.Covered, State(f, "1"));
        Assert.Equal(PictureState.Covered, State(f, "2"));
        Assert.Equal(PictureState.ElementHighlight, State(f, "3"));   // the focus shows over the ordinary state
        Assert.Null(State(f, "4"));
        Assert.Equal(PictureState.Solution, EdgeState(f, "1-2"));
        Assert.Equal(PictureState.ElementHighlight, EdgeState(f, "2-3"));
        Assert.Equal(["1-2", "2-3"], f.Edges.Keys.Order());
        Assert.Empty(f.Phantoms);
        Assert.Null(f.Ok);
    }

    [Fact]
    public void AClosedTour_ReadsEdgeOrderEitherWay_AndTheClosingEdgeIsSolution() {
        var f = GraphTourFrames.Frame(Undirected(), Of("1", "2", "3", "4", "1"), StepEvent.Accept, [], "x");
        Assert.Equal(["1", "2", "3", "4", "1"], f.Order);   // a closed tour repeats its start, like the certificate
        Assert.Equal(["1-2", "1-4", "2-3", "3-4"], f.Edges.Keys.Order());
        Assert.All(f.Edges.Values, s => Assert.Equal(PictureState.Solution, s.State));
        Assert.Equal(4, f.Nodes.Count);
        Assert.All(f.Nodes.Values, s => Assert.Equal(PictureState.Covered, s.State));
        Assert.Empty(f.Phantoms);
    }

    [Fact]
    public void ASolvedTour_HasItsNodesSolution_OnlyWhenDoneAndOk() {
        var done = GraphTourFrames.Frame(Undirected(), Of("1", "2", "3", "4", "1"), StepEvent.Done, [], "x", true);
        Assert.All(done.Nodes.Values, s => Assert.Equal(PictureState.Solution, s.State));
        Assert.True(done.Ok);
        var notOk = GraphTourFrames.Frame(Undirected(), Of("1", "2", "3", "4", "1"), StepEvent.Done, [], "x", false);
        Assert.All(notOk.Nodes.Values, s => Assert.Equal(PictureState.Covered, s.State));
        var notDone = GraphTourFrames.Frame(Undirected(), Of("1", "2", "3", "4", "1"), StepEvent.Accept, [], "x", true);
        Assert.All(notDone.Nodes.Values, s => Assert.Equal(PictureState.Covered, s.State));
    }

    [Fact]
    public void ANoAnswerDoneFrame_IsEmpty_ButStillHasAnEmptyOrder() {
        var f = GraphTourFrames.Frame(Undirected(), Of(), StepEvent.Done, [], "x", false);
        Assert.NotNull(f.Order);
        Assert.Empty(f.Order);
        Assert.Empty(f.Nodes);
        Assert.Empty(f.Edges);
        Assert.Empty(f.Phantoms);
        Assert.False(f.Ok);
    }

    [Fact]
    public void AStepThatIsNotAnEdge_IsARejectedPhantom_NotAnEdgeState() {
        // 2 and 4 are not joined.
        var f = GraphTourFrames.Frame(Undirected(), Of("1", "2", "4"), StepEvent.Reject, ["4"], "x");
        var phantom = Assert.Single(f.Phantoms);
        Assert.Equal(("2", "4", PictureState.Rejected), (phantom.A, phantom.B, phantom.State));
        Assert.Equal(["1-2"], f.Edges.Keys);
        Assert.Equal(PictureState.Rejected, State(f, "4"));   // in a Reject step the focus is what failed
        Assert.Equal(PictureState.Covered, State(f, "2"));
    }

    [Fact]
    public void AClosingEdgeTheGraphLacks_IsAPhantomToo() {
        var spec = new GraphTourSpec(["1", "2", "3"], [new("1", "2"), new("2", "3")], false, null, TourRule.HamiltonianCycle);
        var f = GraphTourFrames.Frame(spec, Of("1", "2", "3", "1"), StepEvent.Reject, ["3", "1"], "x");
        var phantom = Assert.Single(f.Phantoms);
        Assert.Equal(("3", "1"), (phantom.A, phantom.B));
        Assert.Equal(["1-2", "2-3"], f.Edges.Keys.Order());
    }

    [Fact]
    public void ARejectStepsFocusIsRejected_ATrySteps_IsHighlighted_AndRejectedBeatsHighlight() {
        var reject = GraphTourFrames.Frame(Undirected(), Of("1", "2"), StepEvent.Reject, ["2", "1-2"], "x");
        Assert.Equal(PictureState.Rejected, State(reject, "2"));
        Assert.Equal(PictureState.Rejected, EdgeState(reject, "1-2"));   // the whole closed-off step reads as failed
        var @try = GraphTourFrames.Frame(Undirected(), Of("1"), StepEvent.Try, ["1"], "x");
        Assert.Equal(PictureState.ElementHighlight, State(@try, "1"));
        Assert.Empty(@try.Edges);
    }

    [Fact]
    public void Directed_AStepAgainstAnEdgesDirection_IsAPhantom_AndTheEdgeItself_IsLeftPlain() {
        // Directed edges: 1>2, 2>3, 3>4, 4>1, 2>1. Walking 3 -> 2 goes against 2>3.
        var f = GraphTourFrames.Frame(Directed(), Of("1", "2", "3", "2"), StepEvent.Reject, ["2"], "x");
        Assert.Equal(["1>2", "2>3"], f.Edges.Keys.Order());
        var phantom = Assert.Single(f.Phantoms);
        Assert.Equal(("3", "2"), (phantom.A, phantom.B));   // drawn from the node walked from, to the node walked to
    }

    [Fact]
    public void Directed_TheTwoDirectionsOfAPairAreSeparateEdges() {
        var forward = GraphTourFrames.Frame(Directed(), Of("1", "2"), StepEvent.Accept, [], "x");
        Assert.Equal(["1>2"], forward.Edges.Keys);
        var back = GraphTourFrames.Frame(Directed(), Of("2", "1"), StepEvent.Accept, [], "x");
        Assert.Equal(["2>1"], back.Edges.Keys);
        Assert.Empty(back.Phantoms);
        // 4 -> 3 is not an edge (only 3>4 is).
        var against = GraphTourFrames.Frame(Directed(), Of("4", "3"), StepEvent.Reject, [], "x");
        Assert.Empty(against.Edges);
        Assert.Single(against.Phantoms);
    }

    [Fact]
    public void Directed_AFullCycle_IsAllSolution() {
        var f = GraphTourFrames.Frame(Directed(), Of("1", "2", "3", "4", "1"), StepEvent.Done, [], "x", true);
        Assert.Equal(["1>2", "2>3", "3>4", "4>1"], f.Edges.Keys.Order());
        Assert.All(f.Edges.Values, s => Assert.Equal(PictureState.Solution, s.State));
        Assert.Empty(f.Phantoms);
    }

    [Fact]
    public void NamesThatAreNotNodes_AreDropped_AndARepeatedMissingStepIsDrawnOnce() {
        var f = GraphTourFrames.Frame(Undirected(), Of("timeout", "1"), StepEvent.Done, [], "x", false);
        Assert.Equal(["1"], f.Order);
        var twice = GraphTourFrames.Frame(Undirected(), Of("2", "4", "2", "4"), StepEvent.Reject, [], "x");
        Assert.Single(twice.Phantoms);
    }

    [Fact]
    public void TheFrameSerializes_WithOrder_AndOmitsGroupsAndOk() {
        string json = JsonSerializer.Serialize(GraphTourFrames.Frame(Undirected(), Of("1", "2"), StepEvent.Accept, [], "x"));
        Assert.Contains("\"order\":[\"1\",\"2\"]", json);
        Assert.DoesNotContain("\"groups\"", json);
        Assert.DoesNotContain("\"ok\"", json);
    }

    [Fact]
    public void OtherShapesDoNotGetAnOrder() {
        var f = GraphSubsetFrames.Frame(new GraphSubsetSpec(Nodes, [new("1", "2")], 2, SubsetRule.Clique), new NodeSet(["1"]), StepEvent.Accept, [], "x");
        Assert.Null(f.Order);
        Assert.DoesNotContain("order", JsonSerializer.Serialize(f));
    }

    // ---- reading answers ----

    [Theory]
    [InlineData("{1,2,4,5,3,1}", new[] { "1", "2", "4", "5", "3", "1" })]
    [InlineData("{New York,Chicago,Miami,New York}", new[] { "New York", "Chicago", "Miami", "New York" })]
    [InlineData("1, 2 ,3", new[] { "1", "2", "3" })]
    [InlineData("{}", new string[0])]
    [InlineData("", new string[0])]
    public void ParseTour_ReadsTheCertificateInOrder_KeepingTheClosingRepeat(string answer, string[] expected) {
        Assert.Equal(expected, GraphTourFrames.ParseTour(answer).Order);
    }

    [Fact]
    public void Arrows_JoinsWithArrows() => Assert.Equal("1 → 4 → 3", GraphTourFrames.Arrows(["1", "4", "3"]));

    // ---- the whole response ----

    [Fact]
    public void Build_AddsASolvedFrame_WhenTheSolverRecordedNoSteps_AndOnlyWhenTheAnswerVerifies() {
        var run = new SolveRun("{1,2,3,4,1}", [], typeof(Tour));
        var good = GraphTourFrames.Build(Undirected(), run, GraphTourFrames.ParseTour, _ => true);
        var frame = Assert.IsType<GraphFrame>(Assert.Single(good.Frames));
        Assert.Equal(StepEvent.Done, frame.Event);
        Assert.True(frame.Ok);
        Assert.Equal(["1", "2", "3", "4", "1"], frame.Order);
        Assert.Equal("The tour is 1 → 2 → 3 → 4 → 1.", frame.Caption);

        var bad = GraphTourFrames.Build(Undirected(), new SolveRun("{}", [], typeof(Tour)), GraphTourFrames.ParseTour, _ => false);
        var none = Assert.IsType<GraphFrame>(Assert.Single(bad.Frames));
        Assert.False(none.Ok);
        Assert.Empty(none.Order!);
        Assert.Equal("No answer was found.", none.Caption);
    }

    [Fact]
    public void Build_UsesTheSteps_AndDoesNotAddASecondDone() {
        SolverStep<Tour>[] steps = [
            new(Of("1"), StepEvent.Try, ["1"], "start"),
            new(Of("1", "2"), StepEvent.Accept, ["2"], "go"),
            new(Of("1", "2"), StepEvent.Done, [], "done") { Ok = false },
        ];
        var response = GraphTourFrames.Build(Undirected(), new SolveRun("{}", steps, typeof(Tour)), GraphTourFrames.ParseTour, _ => true);
        Assert.Equal(3, response.Frames.Count);
        Assert.Equal("Graph", response.Type);
        Assert.Equal(["Try", "Accept", "Done"], response.Frames.Cast<GraphFrame>().Select(f => f.Event.ToString()));
    }

    // ---- runs stopped by the time limit ----

    [Fact]
    public void Build_StoppedRun_EndsWithABlankNotOkDone_WithAnEmptyOrder() {
        object[] steps = [
            new SolverStep<Tour>(Of("1", "2", "3"), StepEvent.Accept, ["2>3"], "Go 2 → 3."),
            new SolverStep<Tour>(Of("1", "2", "3"), StepEvent.Done, [], "Stopped after 0.05 s and 2 steps; the solver had not finished.") { Ok = false },
        ];
        var run = new SolveRun(SolveRun.TimedOutAnswer, steps, typeof(Tour)) { TimedOut = true };
        var response = GraphTourFrames.Build(Directed(), run,
            _ => throw new InvalidOperationException("not read"), _ => throw new InvalidOperationException("not verified"));
        var done = Assert.IsType<GraphFrame>(response.Frames[^1]);
        Assert.Equal(StepEvent.Done, done.Event);
        Assert.False(done.Ok);
        Assert.StartsWith("Stopped after", done.Caption);
        Assert.Empty(done.Nodes);
        Assert.DoesNotContain(done.Edges.Values, s => s.State == PictureState.Solution);   // the path so far is not the tour
        Assert.Empty(done.Order!);

        var none = new SolveRun(SolveRun.TimedOutAnswer, [], null) { TimedOut = true };
        var only = Assert.IsType<GraphFrame>(Assert.Single(GraphTourFrames.Build(Undirected(), none,
            _ => throw new InvalidOperationException("not read"), _ => true).Frames));
        Assert.Equal(GraphSubsetFrames.StoppedCaption, only.Caption);
        Assert.False(only.Ok);
        Assert.Empty(only.Order!);
    }
}
