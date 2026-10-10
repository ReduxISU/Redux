using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Unit tests of the state derivation for the Graph picture's groups shape (Graph Coloring, Clique Cover):
// (graph, rule, partial Groups, step event, focus) -> named states and the groups annotation. No endpoint, no solver.
public class GraphGroupFrames_Tests {
    // A triangle a-b-c with a tail c-d, and an isolated e; "c,b" is reversed on purpose:
    //     a --- b
    //      \   /
    //        c --- d        e
    private static readonly string[] Nodes = ["a", "b", "c", "d", "e"];
    private static readonly KeyValuePair<string, string>[] Edges = [new("a", "b"), new("c", "b"), new("c", "d"), new("a", "c")];

    private static GraphGroupSpec Spec(GroupRule rule, int k = 3) => new(Nodes, Edges, k, rule);

    private static Groups Of(params (string Node, int Group)[] groups) => new(groups.Select(g => new KeyValuePair<string, int>(g.Node, g.Group)));

    private static PictureState? State(GraphFrame f, string node) => f.Nodes.TryGetValue(node, out var s) ? s.State : null;
    private static PictureState? EdgeState(GraphFrame f, string edge) => f.Edges.TryGetValue(edge, out var s) ? s.State : null;

    // ---- payload ----

    [Theory]
    [InlineData("Coloring", "coloring")]
    [InlineData("CliqueCover", "cliqueCover")]
    public void Payload_IsTheGroupsShape_WithKAndCanonicalEdges_AndNoColors(string rule, string name) {
        var p = GraphGroupFrames.Payload(Spec(Enum.Parse<GroupRule>(rule), 4));
        Assert.Equal("groups", p.Shape);
        Assert.Equal(name, p.Rule);
        Assert.Equal(name, GraphGroupFrames.RuleName(Enum.Parse<GroupRule>(rule)));
        Assert.Equal(4, p.K);
        Assert.Equal(["a-b", "b-c", "c-d", "a-c"], p.Edges.Select(e => e.Id));
        Assert.Equal(Nodes, p.Nodes.Select(n => n.Id));
        string json = JsonSerializer.Serialize(p);
        Assert.Contains("\"k\":4", json);
        Assert.DoesNotContain("\"color\"", json);
        Assert.DoesNotContain("weight", json);
    }

    [Fact]
    public void Payload_DropsLoopsAndDuplicateEdges() {
        var spec = new GraphGroupSpec(["a", "b"], [new("a", "b"), new("b", "a"), new("a", "a")], 2, GroupRule.Coloring);
        Assert.Equal("a-b", Assert.Single(GraphGroupFrames.Payload(spec).Edges).Id);
    }

    // ---- coloring ----

    [Fact]
    public void Coloring_AnEdgeWhoseEndsShareAColorIsRejected_OtherEdgesAreBackground() {
        var f = GraphGroupFrames.Frame(Spec(GroupRule.Coloring), Of(("a", 0), ("b", 0), ("c", 1)), StepEvent.Reject, [], "x");
        Assert.Equal(["a-b"], f.Edges.Keys);
        Assert.Equal(PictureState.Rejected, EdgeState(f, "a-b"));
        Assert.Empty(f.Nodes);   // group colors are the meaning of a node; the clash is shown on the edge
        Assert.Empty(f.Phantoms);
        Assert.Equal(new Dictionary<string, int> { ["a"] = 0, ["b"] = 0, ["c"] = 1 }, f.Groups);
    }

    [Fact]
    public void Coloring_AProperColoringHasNoStatesAtAll_EvenWhenSolved() {
        var f = GraphGroupFrames.Frame(Spec(GroupRule.Coloring), Of(("a", 0), ("b", 1), ("c", 2), ("d", 0), ("e", 0)), StepEvent.Done, [], "x", true);
        Assert.Empty(f.Nodes);   // never Solution: the group colors are the answer
        Assert.Empty(f.Edges);
        Assert.Equal(5, f.Groups!.Count);
        Assert.True(f.Ok);
    }

    [Fact]
    public void Coloring_ANodeWithNoColorYetIsLeftOutOfGroups_AndClashesNeedBothEndsColored() {
        var f = GraphGroupFrames.Frame(Spec(GroupRule.Coloring), Of(("a", 0)), StepEvent.Accept, ["a"], "x");
        Assert.Equal(["a"], f.Groups!.Keys);
        Assert.Empty(f.Edges);
        Assert.Equal(PictureState.ElementHighlight, State(f, "a"));
    }

    [Fact]
    public void Coloring_TheNodeBeingColoredIsHighlighted_AndWhatFailedIsRejectedInARejectStep() {
        var coloring = Of(("a", 0), ("b", 0));
        var tried = GraphGroupFrames.Frame(Spec(GroupRule.Coloring), coloring, StepEvent.Try, ["b"], "x");
        Assert.Equal(PictureState.ElementHighlight, State(tried, "b"));
        var rejected = GraphGroupFrames.Frame(Spec(GroupRule.Coloring), coloring, StepEvent.Reject, ["a", "b"], "x");
        Assert.Equal(PictureState.Rejected, State(rejected, "a"));
        Assert.Equal(PictureState.Rejected, State(rejected, "b"));
        Assert.Equal(PictureState.Rejected, EdgeState(rejected, "a-b"));
    }

    [Fact]
    public void Coloring_ANodeColoredBeyondKIsRejected_ItsGroupIsStillReported() {
        var f = GraphGroupFrames.Frame(Spec(GroupRule.Coloring, 2), Of(("a", 0), ("b", 1), ("c", 2)), StepEvent.Reject, ["c"], "x");
        Assert.Equal(PictureState.Rejected, State(f, "c"));
        Assert.Null(State(f, "a"));
        Assert.Equal(2, f.Groups!["c"]);
    }

    [Fact]
    public void Coloring_LoopsAreNeverAClash() {
        var spec = new GraphGroupSpec(["a", "b"], [new("a", "a"), new("a", "b")], 2, GroupRule.Coloring);
        var f = GraphGroupFrames.Frame(spec, Of(("a", 0), ("b", 1)), StepEvent.Done, [], "x", true);
        Assert.Empty(f.Edges);
    }

    // ---- clique cover ----

    [Fact]
    public void CliqueCover_EdgesInsideAGroupAreSolution_EdgesBetweenGroupsAreBackground() {
        var f = GraphGroupFrames.Frame(Spec(GroupRule.CliqueCover), Of(("a", 0), ("b", 0), ("c", 0), ("d", 1), ("e", 2)), StepEvent.Done, [], "x", true);
        Assert.Equal(["a-b", "a-c", "b-c"], f.Edges.Keys);
        Assert.All(f.Edges.Values, s => Assert.Equal(PictureState.Solution, s.State));
        Assert.Null(EdgeState(f, "c-d"));
        Assert.Empty(f.Phantoms);
        Assert.Empty(f.Nodes);
    }

    [Fact]
    public void CliqueCover_AMissingEdgeInsideAGroupIsARejectedPhantom() {
        var f = GraphGroupFrames.Frame(Spec(GroupRule.CliqueCover), Of(("a", 0), ("d", 0), ("b", 1)), StepEvent.Reject, ["a", "d"], "x");
        var phantom = Assert.Single(f.Phantoms);
        Assert.Equal(("a", "d", PictureState.Rejected), (phantom.A, phantom.B, phantom.State));
        Assert.DoesNotContain(f.Edges, e => e.Value.State == PictureState.Solution);   // a-d is not an edge; b is alone
    }

    [Fact]
    public void CliqueCover_ANodeInAGroupBeyondKIsRejected() {
        var f = GraphGroupFrames.Frame(Spec(GroupRule.CliqueCover, 1), Of(("a", 0), ("b", 1)), StepEvent.Accept, [], "x");
        Assert.Equal(PictureState.Rejected, State(f, "b"));
        Assert.Null(State(f, "a"));
    }

    // ---- common ----

    [Fact]
    public void Frames_IgnoreNodesAndEdgesThatAreNotInTheGraph() {
        var f = GraphGroupFrames.Frame(Spec(GroupRule.Coloring), Of(("a", 0), ("zz", 1)), StepEvent.Try, ["zz", "zz-a"], "x");
        Assert.Equal(["a"], f.Groups!.Keys);
        Assert.Empty(f.Nodes);
        Assert.Empty(f.Edges);
    }

    [Fact]
    public void Serialization_AlwaysCarriesGroups_AndOmitsBackgroundAndAMissingOk() {
        string json = JsonSerializer.Serialize(GraphGroupFrames.Frame(Spec(GroupRule.Coloring), Of(("a", 0)), StepEvent.Accept, [], "x"), FramesResponse.JsonOptions);
        Assert.Contains("\"groups\"", json);
        Assert.Contains("\"a\": 0", json);
        Assert.DoesNotContain("Background", json);
        Assert.DoesNotContain("\"ok\"", json);
        string empty = JsonSerializer.Serialize(GraphGroupFrames.Frame(Spec(GroupRule.Coloring), new Groups([]), StepEvent.Done, [], "x", false), FramesResponse.JsonOptions);
        Assert.Contains("\"groups\": {}", empty);
    }

    // ---- reading answers and captions ----

    [Theory]
    [InlineData("{{a,d},{b},{c}}", "a=0,b=1,c=2,d=0")]
    [InlineData("{{4,5},{1,2,3},{}}", "1=1,2=1,3=1,4=0,5=0")]
    [InlineData("{1,2},{3}", "1=0,2=0,3=1")]
    [InlineData("{{ a , b }}", "a=0,b=0")]
    [InlineData("{}", "")]
    [InlineData("nonsense", "")]
    public void ParseClasses_TheNthClassIsGroupN(string answer, string expected) {
        var groups = GraphGroupFrames.ParseClasses(answer);
        Assert.Equal(expected, string.Join(",", groups.Of.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}={p.Value}")));
    }

    [Fact]
    public void Classes_KeepsEmptyClassesSoAPositionIsAnIndex() {
        var of = new Dictionary<string, int> { ["b"] = 2, ["a"] = 0, ["c"] = 0 };
        Assert.Equal("{a,c},{},{b}", GraphGroupFrames.Classes(of));
        Assert.Equal("{a,c},{},{b},{}", GraphGroupFrames.Classes(of, 4));
        Assert.Equal("", GraphGroupFrames.Classes(new Dictionary<string, int>()));
    }

    [Fact]
    public void And_JoinsInWords() {
        Assert.Equal("", GraphGroupFrames.And([]));
        Assert.Equal("1", GraphGroupFrames.And(["1"]));
        Assert.Equal("1 and 3", GraphGroupFrames.And(["1", "3"]));
        Assert.Equal("1, 2 and 3", GraphGroupFrames.And(["1", "2", "3"]));
    }

    [Fact]
    public void Why_NamesAClash_AMissingEdge_OrAnUncoloredNode() {
        var clash = GraphGroupFrames.Why(GroupRule.Coloring, Nodes, Edges, Of(("a", 1), ("b", 1), ("c", 0), ("d", 0), ("e", 0)).Of);
        Assert.Equal("a and b are both color 1", clash!.Reason);
        Assert.Equal(["a", "b"], clash.Focus);
        var missing = GraphGroupFrames.Why(GroupRule.CliqueCover, Nodes, Edges, Of(("a", 0), ("d", 0), ("b", 1), ("c", 2), ("e", 3)).Of);
        Assert.Equal("a and d are in one group but aren't joined", missing!.Reason);
        var uncolored = GraphGroupFrames.Why(GroupRule.Coloring, Nodes, Edges, Of(("a", 0), ("b", 1), ("c", 2), ("d", 0)).Of);
        Assert.Equal("e has no color", uncolored!.Reason);
        Assert.Equal(["e"], uncolored.Focus);
        Assert.Equal("e is in no group", GraphGroupFrames.Why(GroupRule.CliqueCover, Nodes, Edges, Of(("a", 0), ("b", 0), ("c", 0), ("d", 1)).Of)!.Reason);
        Assert.Null(GraphGroupFrames.Why(GroupRule.Coloring, Nodes, Edges, Of(("a", 0), ("b", 1), ("c", 2), ("d", 0), ("e", 0)).Of));
    }

    // ---- the whole response ----

    [Fact]
    public void Build_WithoutSteps_DrawsTheSolvedFrameFromTheAnswerAndTheVerifier() {
        var run = new SolveRun("{{a,d},{b},{c},{e}}", [], null);
        var response = GraphGroupFrames.Build(Spec(GroupRule.Coloring, 4), run, GraphGroupFrames.ParseClasses, answer => answer.Length > 0);
        Assert.Equal("Graph", response.Type);
        var frame = Assert.IsType<GraphFrame>(Assert.Single(response.Frames));
        Assert.Equal(StepEvent.Done, frame.Event);
        Assert.True(frame.Ok);
        Assert.Equal(5, frame.Groups!.Count);
        Assert.Equal("The answer is {a,d},{b},{c},{e}.", frame.Caption);
    }

    [Fact]
    public void Build_AnEmptyAnswerIsAnEmptyNotOkPicture() {
        var response = GraphGroupFrames.Build(Spec(GroupRule.Coloring), new SolveRun("{}", [], null), GraphGroupFrames.ParseClasses, _ => false);
        var frame = Assert.IsType<GraphFrame>(Assert.Single(response.Frames));
        Assert.Empty(frame.Groups!);
        Assert.False(frame.Ok);
        Assert.Equal("No answer was found.", frame.Caption);
    }

    // ---- runs stopped by the time limit ----

    [Fact]
    public void Build_StoppedRun_EndsWithABlankNotOkDone_WithEmptyGroups() {
        object[] steps = [
            new SolverStep<Groups>(Of(("a", 0), ("b", 0), ("c", 0)), StepEvent.Reject, ["a", "b"], "Try {a,b,c}: a and b share color 0."),
            new SolverStep<Groups>(Of(("a", 0), ("b", 0), ("c", 0)), StepEvent.Done, [], "Stopped after 0.05 s and 2 steps; the solver had not finished.") { Ok = false },
        ];
        var run = new SolveRun(SolveRun.TimedOutAnswer, steps, typeof(Groups)) { TimedOut = true };
        var response = GraphGroupFrames.Build(Spec(GroupRule.CliqueCover), run,
            _ => throw new InvalidOperationException("not read"), _ => throw new InvalidOperationException("not verified"));
        var done = Assert.IsType<GraphFrame>(response.Frames[^1]);
        Assert.Equal(StepEvent.Done, done.Event);
        Assert.False(done.Ok);
        Assert.StartsWith("Stopped after", done.Caption);
        Assert.Empty(done.Nodes);
        Assert.DoesNotContain(done.Edges.Values, s => s.State == PictureState.Solution);   // no clique drawn as the cover
        Assert.Empty(done.Phantoms);
        Assert.Empty(done.Groups!);

        var none = new SolveRun(SolveRun.TimedOutAnswer, [], null) { TimedOut = true };
        var only = Assert.IsType<GraphFrame>(Assert.Single(GraphGroupFrames.Build(Spec(GroupRule.Coloring), none,
            _ => throw new InvalidOperationException("not read"), _ => true).Frames));
        Assert.Equal(GraphSubsetFrames.StoppedCaption, only.Caption);
        Assert.False(only.Ok);
    }

    // ---- candidates being tried are highlighted, not drawn as the answer; edge ids ----

    [Theory]
    [InlineData("Try")]
    [InlineData("Reject")]
    public void CliqueCover_AGroupingBeingTried_HighlightsItsCliques_NeverSolution(string ev) {
        var f = GraphGroupFrames.Frame(Spec(GroupRule.CliqueCover), Of(("a", 0), ("b", 0), ("c", 0), ("d", 1), ("e", 2)), Enum.Parse<StepEvent>(ev), [], "x");
        Assert.Equal(PictureState.ElementHighlight, EdgeState(f, "a-b"));
        Assert.DoesNotContain(f.Edges.Values.Concat(f.Nodes.Values), s => s.State == PictureState.Solution);
    }

    // ---- incremental runs: the grouping so far stays Solution, only the piece under test is marked ----

    [Fact]
    public void CliqueCover_Incremental_Reject_KeepsTheCliquesSoFarSolution_AndRejectsOnlyTheNodeUnderTest() {
        // A search that has put a, b and c in one clique and fails to place d: the clique stays drawn solid.
        var f = GraphGroupFrames.Frame(Spec(GroupRule.CliqueCover), Of(("a", 0), ("b", 0), ("c", 0)), StepEvent.Reject, ["d"], "x", incremental: true);
        Assert.Equal(PictureState.Solution, EdgeState(f, "a-b"));
        Assert.Equal(PictureState.Solution, EdgeState(f, "b-c"));
        Assert.Equal(PictureState.Solution, EdgeState(f, "a-c"));
        Assert.Equal(PictureState.Rejected, State(f, "d"));
        var tried = GraphGroupFrames.Frame(Spec(GroupRule.CliqueCover), Of(("a", 0), ("b", 0), ("c", 0), ("d", 1)), StepEvent.Try, ["d"], "x", incremental: true);
        Assert.Equal(PictureState.Solution, EdgeState(tried, "a-b"));
        Assert.Equal(PictureState.ElementHighlight, State(tried, "d"));
    }

    [Fact]
    public void Build_ReadsIncrementalFromTheRun() {
        var steps = new List<object> { new SolverStep<Groups>(Of(("a", 0), ("b", 0)), StepEvent.Reject, ["c"], "x") };
        GraphFrame First(bool incremental) => (GraphFrame)GraphGroupFrames.Build(Spec(GroupRule.CliqueCover),
            new SolveRun("{}", steps, typeof(Groups)) { Incremental = incremental }, _ => Of(), _ => false).Frames[0];
        Assert.Equal(PictureState.Solution, EdgeState(First(true), "a-b"));
        Assert.Equal(PictureState.ElementHighlight, EdgeState(First(false), "a-b"));
        Assert.Equal(PictureState.Rejected, State(First(true), "c"));
    }

    [Fact]
    public void Payload_NodeNamesWithSeparators_GetUniqueIds() {
        var spec = new GraphGroupSpec(["n-1", "n-2", "n", "1-n-2"], [new("n-1", "n-2"), new("n", "1-n-2")], 2, GroupRule.Coloring);
        Assert.Equal([@"n\-1-n\-2", @"1\-n\-2-n"], GraphGroupFrames.Payload(spec).Edges.Select(e => e.Id));
        // A clash marks the edge whose ends share a color, not the other one that would have shared its id.
        var f = GraphGroupFrames.Frame(spec, Of(("n", 0), ("1-n-2", 0), ("n-1", 0), ("n-2", 1)), StepEvent.Done, [], "x", false);
        Assert.Equal([@"1\-n\-2-n"], f.Edges.Keys);
    }
}
