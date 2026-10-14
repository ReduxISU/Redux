using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

public class StepRecorder_Tests {
    private static NodeSet Nodes(params string[] n) => new(n);

    [Fact]
    public void Off_RecordsNothing_AndNeverInvokesLambdas() {
        int calls = 0;
        var rec = StepRecorder<NodeSet>.Off;
        rec.Try(() => { calls++; return Nodes("a"); }, () => { calls++; return "x"; }, "a");
        rec.Accept(() => { calls++; return Nodes("a"); }, () => { calls++; return "x"; });
        rec.Reject(() => { calls++; return Nodes("a"); }, () => { calls++; return "x"; });
        rec.Backtrack(() => { calls++; return Nodes("a"); }, () => { calls++; return "x"; });
        rec.Done(Nodes("a"), true, "done");
        Assert.Equal(0, calls);
        Assert.Empty(rec.Steps);
        Assert.Equal(0, rec.Total);
        Assert.Equal(0, rec.Hidden);
    }

    [Fact]
    public void Recording_KeepsStepsInOrder_WithEventsFocusAndCaptions() {
        var rec = new StepRecorder<NodeSet>();
        rec.Try(() => Nodes("a"), () => "pick a", "a");
        rec.Reject(() => Nodes("a", "b"), () => "b clashes", "b", "a");
        rec.Backtrack(() => Nodes("a"), () => "drop b", "b");
        rec.Accept(() => Nodes("a"), () => "keep a");
        rec.Done(Nodes("a"), true, "finished");

        Assert.Equal(new[] { StepEvent.Try, StepEvent.Reject, StepEvent.Backtrack, StepEvent.Accept, StepEvent.Done },
            rec.Steps.Select(s => s.Event));
        Assert.Equal(new[] { "b", "a" }, rec.Steps[1].Focus);
        Assert.Equal("pick a", rec.Steps[0].Caption);
        Assert.Null(rec.Steps[0].Ok);
        Assert.True(rec.Steps[4].Ok);
        Assert.Equal("finished", rec.Steps[4].Caption);
        Assert.Equal(5, rec.Total);
        Assert.Equal(0, rec.Hidden);
    }

    [Fact]
    public void Cap_KeepsFirstN_CountsRest_AndDoneIsAlwaysKept() {
        var rec = new StepRecorder<NodeSet>(cap: 3);
        int partialCalls = 0, captionCalls = 0;
        for (int i = 0; i < 10; i++) {
            int n = i;
            rec.Try(() => { partialCalls++; return Nodes(n.ToString()); }, () => { captionCalls++; return $"step {n}"; });
        }
        rec.Done(Nodes("end"), false, "finished");

        // The lambdas ran only for the three kept steps.
        Assert.Equal(3, partialCalls);
        Assert.Equal(3, captionCalls);
        Assert.Equal(4, rec.Steps.Count);
        Assert.Equal(new[] { "step 0", "step 1", "step 2" }, rec.Steps.Take(3).Select(s => s.Caption));
        Assert.Equal(StepEvent.Done, rec.Steps[^1].Event);
        Assert.False(rec.Steps[^1].Ok);
        Assert.Equal("finished (7 not shown)", rec.Steps[^1].Caption);
        Assert.Equal(11, rec.Total);
        Assert.Equal(7, rec.Hidden);
    }

    [Fact]
    public void Done_WithNothingHidden_HasNoSuffix() {
        var rec = new StepRecorder<NodeSet>(cap: 2);
        rec.Try(() => Nodes("a"), () => "one");
        rec.Try(() => Nodes("b"), () => "two");
        rec.Done(Nodes("b"), true, "finished");
        Assert.Equal("finished", rec.Steps[^1].Caption);
        Assert.Equal(0, rec.Hidden);
    }

    [Fact]
    public void DefaultCap_Is150() {
        var rec = new StepRecorder<NodeSet>();
        for (int i = 0; i < 200; i++) rec.Try(() => Nodes("a"), () => "s");
        Assert.Equal(150, rec.Steps.Count);
        Assert.Equal(50, rec.Hidden);
    }

    [Fact]
    public void Shapes_HaveValueEquality_IndependentOfOrder() {
        Assert.Equal(Nodes("b", "a", "a"), Nodes("a", "b"));
        Assert.NotEqual(Nodes("a"), Nodes("a", "b"));
        Assert.Equal(new ActiveStates(["x", "y"], 2), new ActiveStates(["y", "x"], 2));
        Assert.NotEqual(new ActiveStates(["x"], 1), new ActiveStates(["x"], 2));
        Assert.Equal(new Tour(["a", "b"]), new Tour(["a", "b"]));
        Assert.NotEqual(new Tour(["a", "b"]), new Tour(["b", "a"]));
        Assert.Equal(new Groups([new("a", 0), new("b", 1)]), new Groups([new("b", 1), new("a", 0)]));
        Assert.Equal(new Assignment([new("x", "T")]), new Assignment([new("x", "T")]));
        Assert.Equal(new EdgeSet(["a-b"]), new EdgeSet(["a-b"]));
        Assert.Equal(new ChosenSets(["1", "2"]), new ChosenSets(["2", "1"]));
    }
}
