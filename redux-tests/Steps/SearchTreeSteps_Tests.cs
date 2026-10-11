using System.Text.Json;
using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The search-tree link a step can carry (SolverStep.Tree, SearchNode) and the recorder overloads that record it. A step
// without one must serialize exactly as before, so no other picture or client sees a difference.
public class SearchTreeSteps_Tests {
    private static JsonElement Serialize<T>(T value) =>
        JsonDocument.Parse(JsonSerializer.Serialize(value, ProblemProvider.VisualizeJsonOptions())).RootElement;

    [Fact]
    public void AStepWithoutATree_SerializesAsBefore() {
        var step = new SolverStep<NodeSet>(new NodeSet(["a"]), StepEvent.Try, ["a"], "Pick a.");
        Assert.Null(step.Tree);
        Assert.Equal(["partial", "event", "focus", "caption"], Serialize(step).EnumerateObject().Select(p => p.Name));
        var done = new SolverStep<NodeSet>(new NodeSet([]), StepEvent.Done, [], "Done.") { Ok = true };
        Assert.Equal(["partial", "event", "focus", "caption", "ok"], Serialize(done).EnumerateObject().Select(p => p.Name));
        Assert.DoesNotContain("tree", JsonSerializer.Serialize(done, ProblemProvider.VisualizeJsonOptions()));
    }

    [Fact]
    public void AStepWithATree_AddsItLast_WithLowerCaseStates_AndNoBoundUnlessSet() {
        var step = new SolverStep<Assignment>(new Assignment([]), StepEvent.Reject, [], "x1 = True fails.") {
            Tree = [new SearchNode(0, null, "start", SearchNodeState.Explored), new SearchNode(1, 0, "x1=T", SearchNodeState.Pruned) { Bound = "C2 false" }],
        };
        var json = Serialize(step);
        Assert.Equal(["partial", "event", "focus", "caption", "tree"], json.EnumerateObject().Select(p => p.Name));
        var nodes = json.GetProperty("tree").EnumerateArray().ToList();
        Assert.Equal(["nodeId", "parentId", "label", "state"], nodes[0].EnumerateObject().Select(p => p.Name));
        Assert.Equal(JsonValueKind.Null, nodes[0].GetProperty("parentId").ValueKind);
        Assert.Equal("explored", nodes[0].GetProperty("state").GetString());
        Assert.Equal(["nodeId", "parentId", "label", "state", "bound"], nodes[1].EnumerateObject().Select(p => p.Name));
        Assert.Equal(0, nodes[1].GetProperty("parentId").GetInt32());
        Assert.Equal("pruned", nodes[1].GetProperty("state").GetString());
        Assert.Equal("C2 false", nodes[1].GetProperty("bound").GetString());
    }

    [Theory]
    [InlineData("Queued", "queued")]
    [InlineData("Explored", "explored")]
    [InlineData("Pruned", "pruned")]
    [InlineData("Failed", "failed")]
    [InlineData("Solution", "solution")]
    [InlineData("Skipped", "skipped")]
    public void States_SerializeAsTheSpecsNames(string member, string name) {
        var state = Enum.Parse<SearchNodeState>(member);
        Assert.Equal($"\"{name}\"", JsonSerializer.Serialize(state));
        Assert.Equal(name, API.Interfaces.SearchTree.SearchTreeFrames.KindOf(state));
    }

    [Fact]
    public void Recorder_KeepsTheTreeOfEachKeptStep_InEveryEvent() {
        var rec = new StepRecorder<NodeSet>();
        SearchNode[] One(int id) => [new SearchNode(id, id == 0 ? null : 0, $"n{id}", SearchNodeState.Explored)];
        rec.Try(() => new NodeSet([]), () => "t", () => [], () => One(0));
        rec.Accept(() => new NodeSet([]), () => "a", () => [], () => One(1));
        rec.Reject(() => new NodeSet([]), () => "r", () => [], () => One(2));
        rec.Backtrack(() => new NodeSet([]), () => "b", () => [], () => One(3));
        rec.Try(() => new NodeSet([]), () => "plain", () => []);
        rec.Done(new NodeSet([]), true, "done");
        Assert.Equal([StepEvent.Try, StepEvent.Accept, StepEvent.Reject, StepEvent.Backtrack, StepEvent.Try, StepEvent.Done], rec.Steps.Select(s => s.Event));
        Assert.Equal([0, 1, 2, 3], rec.Steps.Take(4).Select(s => Assert.Single(s.Tree!).NodeId));
        Assert.Null(rec.Steps[4].Tree);
        Assert.Null(rec.Steps[5].Tree);
    }

    [Fact]
    public void Recorder_BuildsNoTreePastTheCap_ExceptForTheLastAccept_AndNoneWhenOff() {
        var rec = new StepRecorder<NodeSet>(cap: 1);
        int built = 0;
        SearchNode[] Tree(int id) { built++; return [new SearchNode(id, null, "x", SearchNodeState.Explored)]; }
        rec.Try(() => new NodeSet([]), () => "kept", () => [], () => Tree(0));
        rec.Try(() => new NodeSet([]), () => "past the cap", () => [], () => Tree(1));
        rec.Reject(() => new NodeSet([]), () => "past the cap", () => [], () => Tree(2));
        rec.Accept(() => new NodeSet([]), () => "the answer", () => [], () => Tree(3));
        rec.Done(new NodeSet([]), true, "done");
        Assert.Equal(2, built);
        Assert.Equal([0, 3], rec.Steps.Where(s => s.Tree != null).Select(s => s.Tree![0].NodeId));

        StepRecorder<NodeSet>.Off.Try(() => throw new InvalidOperationException(), () => throw new InvalidOperationException(),
            () => throw new InvalidOperationException(), () => throw new InvalidOperationException());
        Assert.Empty(StepRecorder<NodeSet>.Off.Steps);
    }

    [Fact]
    public void Done_CanCarryTheAnswersPath_BuiltOnlyWhenRecording() {
        var rec = new StepRecorder<NodeSet>(cap: 1);
        rec.Try(() => new NodeSet([]), () => "kept", () => []);
        rec.Accept(() => new NodeSet([]), () => "past the cap", () => []);
        rec.Done(new NodeSet([]), true, "done", () => [new SearchNode(0, null, "start", SearchNodeState.Explored), new SearchNode(3, 0, "x", SearchNodeState.Solution)]);
        var done = rec.Steps[^1];
        Assert.Equal(StepEvent.Done, done.Event);
        Assert.Equal([0, 3], done.Tree!.Select(n => n.NodeId));
        Assert.Equal([StepEvent.Try, StepEvent.Accept, StepEvent.Done], rec.Steps.Select(s => s.Event));

        StepRecorder<NodeSet>.Off.Done(new NodeSet([]), true, "done", () => throw new InvalidOperationException());
        Assert.Empty(StepRecorder<NodeSet>.Off.Steps);
    }
}
