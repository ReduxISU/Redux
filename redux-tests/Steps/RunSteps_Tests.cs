using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// RunSteps, the steps of a solver that does not branch: the converters set once say how a state shows, each call names the
// event, the state and why. What it records must match the recorder's own forms, and it must build nothing a recorder would
// not keep: no converter called and no text formatted when the recorder is off, counts only, or the step is past the cap
// (an Accept past the cap is still built, since the recorder keeps the last one).
public class RunSteps_Tests {
    private static NodeSet Nodes(params string[] n) => new(n);

    // Counts its calls: a converter or a caption hole that ran shows in the count.
    private sealed class Counter {
        public int Count;
        public string Hit(string text = "x") { Count++; return text; }
    }

    // The state is a candidate set of names, with the name a step is about (or null).
    private static RunSteps<(string[] Names, string? About), NodeSet> StepsOn(StepRecorder<NodeSet> rec, Counter calls) => new(rec,
        partial: s => { calls.Count++; return Nodes(s.Names); },
        focus: s => { calls.Count++; return s.About is null ? [] : [s.About]; });

    [Fact]
    public void Off_CallsNoConverter_AndFormatsNothing() {
        var calls = new Counter();
        var text = new Counter();
        var steps = StepsOn(StepRecorder<NodeSet>.Off, calls);
        steps.Try((["a"], "a"), $"try {text.Hit()}");
        steps.Accept((["a"], "a"), $"accept {text.Hit()}", focus: () => { calls.Count++; return ["b"]; });
        steps.Reject((["a"], null), $"reject {text.Hit()}", partial: () => { calls.Count++; return Nodes("z"); });
        steps.Backtrack((["a"], null), $"back {text.Hit()}");
        steps.Done(Nodes("a"), true, $"done {text.Hit()}");
        Assert.Equal(0, calls.Count);
        Assert.Equal(0, text.Count);
    }

    [Fact]
    public void DeadlineOnly_CountsEveryStep_AndBuildsNothing() {
        var calls = new Counter();
        var text = new Counter();
        long now = 0;
        var rec = StepRecorder<NodeSet>.DeadlineOnlyForTest(TimeSpan.FromMilliseconds(3), () => now++);
        var steps = StepsOn(rec, calls);
        // The clock is read on every ClockCheckInterval-th step (the first included): the 4th read is past the limit.
        Assert.Throws<StepBudgetExceededException>(() => {
            for (int i = 0; i < 1000; i++) steps.Reject((["a"], null), $"reject {text.Hit()}");
        });
        Assert.Equal(0, calls.Count);
        Assert.Equal(0, text.Count);
        Assert.Empty(rec.Steps);
    }

    [Fact]
    public void Recording_KeepsWhatTheRecordersFormsKeep() {
        var calls = new Counter();
        var viaSteps = new StepRecorder<NodeSet>();
        var viaRec = new StepRecorder<NodeSet>();
        var steps = StepsOn(viaSteps, calls);
        int n = 3;
        steps.Try((["a"], "a"), $"pick a ({n})");
        viaRec.Try(() => Nodes("a"), () => $"pick a ({n})", "a");
        steps.Reject((["a", "b"], "b"), $"b clashes", focus: () => ["b", "a"]);
        viaRec.Reject(() => Nodes("a", "b"), () => "b clashes", "b", "a");
        steps.Backtrack((["a"], null), $"drop b", partial: () => Nodes("a", "-"));
        viaRec.Backtrack(() => Nodes("a", "-"), () => "drop b");
        steps.Accept((["a"], "a"), $"keep a{n,3:D2}");
        viaRec.Accept(() => Nodes("a"), () => $"keep a{n,3:D2}", "a");
        steps.Done(Nodes("a"), true, $"finished");
        viaRec.Done(Nodes("a"), true, "finished");

        Assert.Equal(viaRec.Steps.Count, viaSteps.Steps.Count);
        for (int i = 0; i < viaRec.Steps.Count; i++) {
            Assert.Equal(viaRec.Steps[i] with { Focus = [] }, viaSteps.Steps[i] with { Focus = [] });
            Assert.Equal(viaRec.Steps[i].Focus, viaSteps.Steps[i].Focus);
        }
        Assert.Equal("keep a 03", viaSteps.Steps[3].Caption);
        // An override replaces its converter: the Reject called no focus converter, the Backtrack no partial one.
        Assert.Equal(6, calls.Count);
    }

    // Past the cap nothing of a Try, Reject or Backtrack is built; an Accept is, and the last one is kept before the Done.
    [Fact]
    public void PastTheCap_BuildsOnlyAccepts_AndKeepsTheLastOne() {
        var calls = new Counter();
        var rejects = new Counter();
        var accepts = new Counter();
        var rec = new StepRecorder<NodeSet>(cap: 5);
        var steps = StepsOn(rec, calls);
        for (int i = 0; i < 1000; i++) {
            if (i % 100 == 99) steps.Accept(([$"a{i}"], null), $"accept {accepts.Hit(i.ToString())}");
            else steps.Reject((["r"], null), $"reject {rejects.Hit()}");
        }
        steps.Done(Nodes("a999"), true, $"done");
        Assert.Equal(5, rejects.Count);
        Assert.Equal(10, accepts.Count);
        Assert.Equal(5 * 2 + 10 * 2, calls.Count);
        Assert.Equal(7, rec.Steps.Count);
        Assert.Equal("accept 999", rec.Steps[5].Caption);
        Assert.Equal(StepEvent.Done, rec.Steps[6].Event);
        Assert.Equal(1001, rec.Total);
    }

    // Update, a step with no verdict, records exactly what a Try records: the same event, partial, focus and caption.
    [Fact]
    public void Update_RecordsWhatATryRecords() {
        var calls = new Counter();
        var viaUpdate = new StepRecorder<NodeSet>();
        var viaTry = new StepRecorder<NodeSet>();
        int cell = 4;
        StepsOn(viaUpdate, calls).Update((["a"], "a"), $"fill cell {cell}");
        StepsOn(viaTry, calls).Try((["a"], "a"), $"fill cell {cell}");
        StepsOn(viaUpdate, calls).Update((["a"], null), $"read b", focus: () => ["b"], partial: () => Nodes("b"));
        StepsOn(viaTry, calls).Try((["a"], null), $"read b", focus: () => ["b"], partial: () => Nodes("b"));

        Assert.Equal(StepEvent.Try, viaUpdate.Steps[0].Event);
        Assert.Equal(viaTry.Steps.Count, viaUpdate.Steps.Count);
        for (int i = 0; i < viaTry.Steps.Count; i++) {
            Assert.Equal(viaTry.Steps[i] with { Focus = [] }, viaUpdate.Steps[i] with { Focus = [] });
            Assert.Equal(viaTry.Steps[i].Focus, viaUpdate.Steps[i].Focus);
        }
        Assert.Equal("fill cell 4", viaUpdate.Steps[0].Caption);
        Assert.Equal(["b"], viaUpdate.Steps[1].Focus);
    }

    // Like a Try, an Update builds nothing when the recorder is off or past the cap, and a counting recorder counts it.
    [Fact]
    public void Update_BuildsNothingOffOrPastTheCap() {
        var calls = new Counter();
        var text = new Counter();
        StepsOn(StepRecorder<NodeSet>.Off, calls).Update((["a"], "a"), $"update {text.Hit()}");
        Assert.Equal(0, calls.Count);
        Assert.Equal(0, text.Count);

        var rec = new StepRecorder<NodeSet>(cap: 3);
        var steps = StepsOn(rec, calls);
        for (int i = 0; i < 100; i++) steps.Update((["a"], null), $"update {text.Hit()}");
        steps.Done(Nodes("a"), true, $"done");
        Assert.Equal(3, text.Count);
        Assert.Equal(3 * 2, calls.Count);
        Assert.Equal(4, rec.Steps.Count);
        Assert.Equal(101, rec.Total);
        Assert.Equal("done (97 not shown)", rec.Steps[3].Caption);

        long now = 0;
        var deadline = StepRecorder<NodeSet>.DeadlineOnlyForTest(TimeSpan.FromMilliseconds(3), () => now++);
        var counted = StepsOn(deadline, calls);
        Assert.Throws<StepBudgetExceededException>(() => {
            for (int i = 0; i < 1000; i++) counted.Update((["a"], null), $"update {text.Hit()}");
        });
        Assert.Equal(3, text.Count);
        Assert.Empty(deadline.Steps);
    }

    // A search that keeps its own tree gives each step's tree nodes through the tree converter, and the answer's path
    // through the Done; neither is called when the recorder is off or the step is past the cap.
    [Fact]
    public void TreeConverter_GivesEachStepItsNodes_AndOnlyWhenBuilt() {
        var calls = new Counter();
        RunSteps<int, NodeSet> On(StepRecorder<NodeSet> rec) => new(rec,
            partial: id => Nodes($"n{id}"),
            tree: id => { calls.Count++; return [new SearchNode(id, id == 0 ? null : 0, $"n{id}", SearchNodeState.Explored)]; });

        var off = On(StepRecorder<NodeSet>.Off);
        off.Reject(1, $"reject");
        off.Done(Nodes(), true, $"done", tree: () => { calls.Count++; return []; });
        Assert.Equal(0, calls.Count);

        var rec = new StepRecorder<NodeSet>(cap: 1);
        var steps = On(rec);
        steps.Try(0, $"start");
        steps.Reject(1, $"past the cap");
        steps.Done(Nodes("n0"), true, $"done", tree: () => [new SearchNode(0, null, "n0", SearchNodeState.Solution)]);
        Assert.Equal(1, calls.Count);
        Assert.Equal("n0", Assert.Single(rec.Steps[0].Tree!).Label);
        Assert.Equal(SearchNodeState.Solution, Assert.Single(rec.Steps[^1].Tree!).State);
        // A step with no tree converter, and a Done with no path, carry no nodes.
        var plain = new StepRecorder<NodeSet>();
        StepsOn(plain, calls).Try((["a"], null), $"try");
        StepsOn(plain, calls).Done(Nodes("a"), true, $"done");
        Assert.Null(plain.Steps[0].Tree);
        Assert.Null(plain.Steps[1].Tree);
    }

    [Fact]
    public void NoFocusConverter_GivesNoFocus() {
        var rec = new StepRecorder<NodeSet>();
        var steps = new RunSteps<string, NodeSet>(rec, partial: s => Nodes(s));
        steps.Try("a", $"try a");
        Assert.Empty(rec.Steps[0].Focus);
        Assert.Equal(Nodes("a"), rec.Steps[0].Partial);
    }
}
