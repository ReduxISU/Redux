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

    // A brute-force-like run: 500 failed tries, the answer found at the 400th, then Done.
    [Fact]
    public void PastTheCap_TheWinningAcceptIsKept_JustBeforeDone() {
        var rec = new StepRecorder<NodeSet>();
        for (int i = 1; i <= 500; i++) {
            int n = i;
            if (n == 400) rec.Accept(() => Nodes(n.ToString()), () => $"accept {n}");
            else rec.Reject(() => Nodes(n.ToString()), () => $"reject {n}");
        }
        rec.Done(Nodes("400"), true, "found");

        Assert.Equal(StepRecorder<NodeSet>.DefaultCap + 2, rec.Steps.Count);
        Assert.Equal(Enumerable.Range(1, 150).Select(n => $"reject {n}"), rec.Steps.Take(150).Select(s => s.Caption));
        Assert.Equal(StepEvent.Accept, rec.Steps[150].Event);
        Assert.Equal("accept 400", rec.Steps[150].Caption);
        Assert.Equal(Nodes("400"), rec.Steps[150].Partial);
        Assert.Equal(StepEvent.Done, rec.Steps[151].Event);
        // 500 reported decisions, 151 kept (150 + the Accept): 349 not shown.
        Assert.Equal("found (349 not shown)", rec.Steps[151].Caption);
        Assert.Equal(349, rec.Hidden);
        Assert.Equal(501, rec.Total);
    }

    [Fact]
    public void PastTheCap_OnlyTheLastAcceptIsKept_InChronologicalOrder() {
        var rec = new StepRecorder<NodeSet>(cap: 2);
        rec.Try(() => Nodes("a"), () => "t1");
        rec.Accept(() => Nodes("a"), () => "a1");   // within the cap
        rec.Accept(() => Nodes("b"), () => "a2");   // past the cap, superseded
        rec.Reject(() => Nodes("c"), () => "r1");
        rec.Accept(() => Nodes("d"), () => "a3");   // past the cap, the last one
        rec.Reject(() => Nodes("e"), () => "r2");
        rec.Done(Nodes("d"), true, "done");

        Assert.Equal(new[] { "t1", "a1", "a3", "done (3 not shown)" }, rec.Steps.Select(s => s.Caption));
        Assert.True(rec.Steps.Count <= 2 + 2);
    }

    [Fact]
    public void PastTheCap_WithNoAccept_IsUnchanged() {
        var rec = new StepRecorder<NodeSet>(cap: 3);
        rec.Accept(() => Nodes("a"), () => "kept accept");   // an Accept inside the cap adds nothing at Done
        for (int i = 0; i < 9; i++) rec.Reject(() => Nodes("x"), () => "r");
        rec.Done(Nodes("a"), false, "finished");

        Assert.Equal(new[] { StepEvent.Accept, StepEvent.Reject, StepEvent.Reject, StepEvent.Done }, rec.Steps.Select(s => s.Event));
        Assert.Equal("finished (7 not shown)", rec.Steps[^1].Caption);
    }

    [Fact]
    public void UnderTheCap_IsUnchanged() {
        var rec = new StepRecorder<NodeSet>(cap: 10);
        rec.Reject(() => Nodes("a"), () => "r");
        rec.Accept(() => Nodes("b"), () => "a");
        rec.Reject(() => Nodes("c"), () => "r2");
        rec.Done(Nodes("b"), true, "done");
        Assert.Equal(new[] { "r", "a", "r2", "done" }, rec.Steps.Select(s => s.Caption));
        Assert.Equal(0, rec.Hidden);
    }

    // Past the cap only an Accept's lambdas run (once per Accept); every other step's lambdas never run.
    [Fact]
    public void PastTheCap_LambdasRunOnlyForAccepts() {
        var rec = new StepRecorder<NodeSet>(cap: 5);
        int rejectCalls = 0, acceptCalls = 0;
        for (int i = 0; i < 1000; i++) {
            if (i % 100 == 99)
                rec.Accept(() => { acceptCalls++; return Nodes("a"); }, () => { acceptCalls++; return "a"; });
            else
                rec.Reject(() => { rejectCalls++; return Nodes("r"); }, () => { rejectCalls++; return "r"; });
        }
        rec.Done(Nodes("a"), true, "done");

        Assert.Equal(5 * 2, rejectCalls);   // only the five kept Rejects
        Assert.Equal(10 * 2, acceptCalls);  // all ten Accepts are past the cap: one partial + one caption each
        Assert.Equal(7, rec.Steps.Count);
    }

    // The lambdas of an Accept past the cap are evaluated when it is reported, so a later change to the
    // solver's state does not leak into the kept step.
    [Fact]
    public void PastTheCap_KeptAccept_SeesTheStateWhenItWasReported() {
        var rec = new StepRecorder<NodeSet>(cap: 0);
        var chosen = new List<string> { "a" };
        rec.Accept(() => Nodes(chosen.ToArray()), () => $"accept {string.Join(",", chosen)}");
        chosen.Add("b");
        rec.Reject(() => Nodes(chosen.ToArray()), () => "r");
        rec.Done(Nodes("a"), true, "done");
        Assert.Equal(Nodes("a"), rec.Steps[0].Partial);
        Assert.Equal("accept a", rec.Steps[0].Caption);
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
