using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The keyword-first forms of StepRecorder (rec.Reject(() => partial, $"caption", ids)) and the SearchTree built on them. What
// they record must match the lambda forms, and they must build nothing a recorder would not keep: no caption formatted, no
// partial or focus built, no node made, when the recorder is off or the step is past the cap (an Accept past the cap is still
// built, as before, since the recorder keeps the last one).
public class KeywordSteps_Tests {
    private static NodeSet Nodes(params string[] n) => new(n);

    // A hole that counts its evaluations: a caption that was formatted shows in the count.
    private sealed class Counter {
        public int Count;
        public string Hit(string text = "x") { Count++; return text; }
    }

    [Fact]
    public void Off_FormatsNoCaption_AndBuildsNothing() {
        var c = new Counter();
        var rec = StepRecorder<NodeSet>.Off;
        rec.Try(() => { c.Count++; return Nodes("a"); }, $"try {c.Hit()}", c.Hit());
        rec.Accept(() => { c.Count++; return Nodes("a"); }, $"accept {c.Hit()}", () => { c.Count++; return ["a"]; });
        rec.Reject(() => { c.Count++; return Nodes("a"); }, $"reject {c.Hit()}");
        rec.Backtrack(() => { c.Count++; return Nodes("a"); }, $"back {c.Hit()}", () => { c.Count++; return ["a"]; });
        // The focus given as plain ids is evaluated at the call (here, one Hit), like any argument; nothing else is.
        Assert.Equal(1, c.Count);
        Assert.Empty(rec.Steps);
        Assert.Equal(0, rec.Total);
    }

    [Fact]
    public void DeadlineOnly_FormatsNoCaption_ButCountsEveryStep() {
        var c = new Counter();
        var rec = StepRecorder<NodeSet>.DeadlineOnly(TimeSpan.FromMinutes(5));
        for (int i = 0; i < 100; i++)
            rec.Reject(() => { c.Count++; return Nodes("a"); }, $"reject {c.Hit()}");
        Assert.Equal(0, c.Count);
        Assert.Empty(rec.Steps);
    }

    [Fact]
    public void Recording_KeepsWhatTheLambdaFormsKeep() {
        var keyword = new StepRecorder<NodeSet>();
        var lambdas = new StepRecorder<NodeSet>();
        int n = 3;
        keyword.Try(() => Nodes("a"), $"pick a ({n})", "a");
        lambdas.Try(() => Nodes("a"), () => $"pick a ({n})", "a");
        keyword.Reject(() => Nodes("a", "b"), $"b clashes", () => ["b", "a"]);
        lambdas.Reject(() => Nodes("a", "b"), () => "b clashes", "b", "a");
        keyword.Backtrack(() => Nodes("a"), "drop b", "b");
        lambdas.Backtrack(() => Nodes("a"), () => "drop b", "b");
        keyword.Accept(() => Nodes("a"), $"keep a{n,3:D2}");
        lambdas.Accept(() => Nodes("a"), () => $"keep a{n,3:D2}");
        keyword.Done(Nodes("a"), true, "finished");
        lambdas.Done(Nodes("a"), true, "finished");

        Assert.Equal(lambdas.Steps.Count, keyword.Steps.Count);
        for (int i = 0; i < lambdas.Steps.Count; i++) {
            Assert.Equal(lambdas.Steps[i] with { Focus = [] }, keyword.Steps[i] with { Focus = [] });
            Assert.Equal(lambdas.Steps[i].Focus, keyword.Steps[i].Focus);
        }
        Assert.Equal("keep a 03", keyword.Steps[3].Caption);
    }

    // As with the lambdas (PastTheCap_LambdasRunOnlyForAccepts): past the cap nothing of a Try, Reject or Backtrack is built,
    // and an Accept is built in full, so the last one can be kept.
    [Fact]
    public void PastTheCap_FormatsOnlyAccepts() {
        var rec = new StepRecorder<NodeSet>(cap: 5);
        var rejects = new Counter();
        var accepts = new Counter();
        for (int i = 0; i < 1000; i++) {
            int k = i;
            if (i % 100 == 99)
                rec.Accept(() => { accepts.Count++; return Nodes("a"); }, $"accept {accepts.Hit(k.ToString())}", () => { accepts.Count++; return ["a"]; });
            else if (i % 2 == 0)
                rec.Reject(() => { rejects.Count++; return Nodes("r"); }, $"reject {rejects.Hit()}", () => { rejects.Count++; return ["r"]; });
            else
                rec.Backtrack(() => { rejects.Count++; return Nodes("r"); }, $"back {rejects.Hit()}");
        }
        rec.Done(Nodes("a"), true, "done");

        // The five kept steps: three Rejects (partial, caption, focus) and two Backtracks (partial, caption).
        Assert.Equal(3 * 3 + 2 * 2, rejects.Count);
        // All ten Accepts are past the cap: partial, caption and focus each.
        Assert.Equal(10 * 3, accepts.Count);
        Assert.Equal(7, rec.Steps.Count);
        Assert.Equal("accept 999", rec.Steps[5].Caption);
    }

    [Fact]
    public void PlainStrings_ConvertToo() {
        var rec = new StepRecorder<NodeSet>();
        string caption = "already built";
        rec.Reject(() => Nodes("a"), caption);
        Assert.Equal("already built", rec.Steps[0].Caption);
    }

    // ---- SearchTree ----

    private sealed class State(string name) {
        public string Name { get; } = name;
        public override string ToString() => Name;
    }

    private static IReadOnlyList<SearchNode> TreeOf(SolverStep<NodeSet> step) => step.Tree!;

    [Fact]
    public void Tree_RootIsCarriedByTheFirstStep_AndChildrenGoUnderTheStateExpanded() {
        var rec = new StepRecorder<NodeSet>();
        var tree = new SearchTree<State, NodeSet>(rec);
        State root = new("root"), a = new("a");
        tree.Root(root, "start");
        tree.Expand(root, () => Nodes(), $"never: the root is not a jump");
        tree.Try(() => Nodes("x"), $"try x", child: "x=T");
        tree.Accept(() => Nodes("x"), $"keep x", queued: a, bound: $"{2} left");
        tree.Try(() => Nodes("x"), "try x=F", child: "x=F");
        tree.Reject(() => Nodes("x"), "x=F fails", bound: "C1 false");

        Assert.Equal(new[] { StepEvent.Try, StepEvent.Accept, StepEvent.Try, StepEvent.Reject }, rec.Steps.Select(s => s.Event));
        Assert.Equal([new SearchNode(0, null, "start", SearchNodeState.Queued), new SearchNode(1, 0, "x=T", SearchNodeState.Explored)], TreeOf(rec.Steps[0]));
        Assert.Equal([new SearchNode(1, 0, "x=T", SearchNodeState.Queued) { Bound = "2 left" }], TreeOf(rec.Steps[1]));
        Assert.Equal([new SearchNode(2, 0, "x=F", SearchNodeState.Explored)], TreeOf(rec.Steps[2]));
        Assert.Equal([new SearchNode(2, 0, "x=F", SearchNodeState.Pruned) { Bound = "C1 false" }], TreeOf(rec.Steps[3]));
    }

    [Fact]
    public void Tree_Expand_RecordsABacktrackOnlyWhenTheSearchJumps() {
        var rec = new StepRecorder<NodeSet>();
        var tree = new SearchTree<State, NodeSet>(rec);
        State root = new("root"), a = new("a"), b = new("b"), c = new("c");
        var captions = new Counter();
        tree.Root(root, "start");
        tree.Expand(root, () => Nodes(), $"{captions.Hit()}");
        tree.Try(() => Nodes("a"), "try a", child: "a");
        tree.Accept(() => Nodes("a"), "queue a", queued: a);
        tree.Try(() => Nodes("b"), "try b", child: "b");
        tree.Accept(() => Nodes("b"), "queue b", queued: b);
        tree.Expand(a, () => Nodes("a"), $"{captions.Hit()}");    // a child just queued: no step
        tree.Try(() => Nodes("c"), "try c", child: "c");
        tree.Accept(() => Nodes("c"), "queue c", queued: c);
        Assert.False(tree.DeadEnd);                                // a queued c
        tree.Expand(c, () => Nodes("c"), $"{captions.Hit()}");    // c, just queued: no step
        tree.Try(() => Nodes("e"), "try e", child: "e");
        tree.Reject(() => Nodes("e"), "e fails");
        Assert.True(tree.DeadEnd);                                 // c queued nothing
        tree.Expand(b, () => Nodes("b"), $"{(tree.DeadEnd ? "dead end, " : "")}back to {captions.Hit("b")}", () => ["b"]);
        tree.Try(() => Nodes("d"), "try d", child: "d");

        Assert.Equal(1, captions.Count);
        var back = Assert.Single(rec.Steps, s => s.Event == StepEvent.Backtrack);
        Assert.Equal("dead end, back to b", back.Caption);
        Assert.Equal(["b"], back.Focus);
        Assert.Equal([new SearchNode(2, 0, "b", SearchNodeState.Explored)], back.Tree);
        // The child tried after the Backtrack goes under b.
        Assert.Equal(2, rec.Steps.First(s => s.Caption == "try d").Tree![0].ParentId);
    }

    [Fact]
    public void Tree_AtAState_AndAStateOverride() {
        var rec = new StepRecorder<NodeSet>();
        var tree = new SearchTree<State, NodeSet>(rec);
        State root = new("root"), a = new("a");
        tree.Root(root, "A", $"lb {4}");
        tree.Try(() => Nodes("A"), "start at A");                            // no node of its own: just the root
        tree.Try(() => Nodes("A"), "expand A", at: root);
        tree.Accept(() => Nodes("A", "B"), "queue B", child: "B", queued: a, bound: "lb 5");
        tree.Reject(() => Nodes("A", "C"), "no edge", child: "C", bound: "no edge");
        tree.Reject(() => Nodes("A", "B"), "no better", at: a, bound: "tour 9", state: SearchNodeState.Failed);

        Assert.Equal([new SearchNode(0, null, "A", SearchNodeState.Queued) { Bound = "lb 4" }], TreeOf(rec.Steps[0]));
        Assert.Equal([new SearchNode(0, null, "A", SearchNodeState.Explored)], TreeOf(rec.Steps[1]));
        Assert.Equal([new SearchNode(1, 0, "B", SearchNodeState.Queued) { Bound = "lb 5" }], TreeOf(rec.Steps[2]));
        Assert.Equal([new SearchNode(2, 0, "C", SearchNodeState.Pruned) { Bound = "no edge" }], TreeOf(rec.Steps[3]));
        Assert.Equal([new SearchNode(1, 0, "B", SearchNodeState.Failed) { Bound = "tour 9" }], TreeOf(rec.Steps[4]));
    }

    [Fact]
    public void Tree_Done_NamesThePathToTheLastSolution_OrALeafSolutionAlone() {
        var rec = new StepRecorder<NodeSet>(cap: 1);
        var tree = new SearchTree<State, NodeSet>(rec);
        State root = new("root"), a = new("a");
        tree.Root(root, "start");
        tree.Accept(() => Nodes(), "greedy", child: "greedy", bound: "tour 10");   // a leaf solution
        tree.Try(() => Nodes(), "expand", at: root);
        tree.Accept(() => Nodes("a"), "queue a", child: "a", queued: a);
        tree.Try(() => Nodes("a"), "expand a", at: a);
        tree.Try(() => Nodes("a", "b"), "try b", child: "b");
        tree.Accept(() => Nodes("a", "b"), "solved", bound: "tour 7");          // past the cap
        tree.Done(Nodes("a", "b"), true, $"done");

        Assert.Equal([
            new SearchNode(0, null, "start", SearchNodeState.Explored),
            new SearchNode(2, 0, "a", SearchNodeState.Explored),
            new SearchNode(3, 2, "b", SearchNodeState.Solution) { Bound = "tour 7" },
        ], rec.Steps[^1].Tree);

        var leafRec = new StepRecorder<NodeSet>();
        var leafTree = new SearchTree<State, NodeSet>(leafRec);
        leafTree.Root(root, "start");
        leafTree.Accept(() => Nodes(), "greedy", child: "greedy", bound: "tour 10");
        leafTree.Try(() => Nodes(), "expand", at: root);
        leafTree.Done(Nodes(), true, "done");
        Assert.Equal([new SearchNode(1, 0, "greedy", SearchNodeState.Solution) { Bound = "tour 10" }], leafRec.Steps[^1].Tree);

        var failRec = new StepRecorder<NodeSet>();
        var failTree = new SearchTree<State, NodeSet>(failRec);
        failTree.Root(root, "start");
        failTree.Accept(() => Nodes(), "greedy", child: "greedy", bound: "tour 10");
        failTree.Done(Nodes(), false, "over K");
        Assert.Null(failRec.Steps[^1].Tree);
    }

    [Fact]
    public void Tree_Off_MakesNoNode_AndFormatsNothing() {
        foreach (var rec in new[] { StepRecorder<NodeSet>.Off, StepRecorder<NodeSet>.DeadlineOnly(TimeSpan.FromMinutes(5)) }) {
            var c = new Counter();
            var tree = new SearchTree<State, NodeSet>(rec);
            State root = new("root"), a = new("a"), b = new("b");
            tree.Root(root, $"{c.Hit()}", $"{c.Hit()}");
            tree.Expand(root, () => { c.Count++; return Nodes(); }, $"{c.Hit()}", () => { c.Count++; return []; });
            tree.Try(() => { c.Count++; return Nodes(); }, $"{c.Hit()}", () => { c.Count++; return []; }, child: $"{c.Hit()}");
            tree.Accept(() => { c.Count++; return Nodes(); }, $"{c.Hit()}", () => { c.Count++; return []; }, queued: a, bound: $"{c.Hit()}");
            tree.Reject(() => { c.Count++; return Nodes(); }, $"{c.Hit()}", () => { c.Count++; return []; }, child: $"{c.Hit()}", bound: $"{c.Hit()}");
            tree.Expand(b, () => { c.Count++; return Nodes(); }, $"{c.Hit()}");     // a jump: still nothing built
            tree.Done(Nodes(), true, $"{c.Hit()}");
            Assert.Equal(0, c.Count);
            Assert.Empty(rec.Steps);
        }
    }

    // Past the cap a Try or Reject builds nothing but its node (the label, which a later step or the Done may name); an
    // Accept is built in full, as the recorder's Accepts are.
    [Fact]
    public void Tree_PastTheCap_FormatsOnlyLabelsAndAccepts() {
        var rec = new StepRecorder<NodeSet>(cap: 1);
        var tree = new SearchTree<State, NodeSet>(rec);
        var shown = new Counter();
        var labels = new Counter();
        var accepts = new Counter();
        State root = new("root"), a = new("a");
        tree.Root(root, "start");
        tree.Try(() => Nodes(), "kept", at: root);
        tree.Try(() => { shown.Count++; return Nodes(); }, $"{shown.Hit()}", () => { shown.Count++; return []; }, child: $"{labels.Hit("a")}");
        tree.Reject(() => { shown.Count++; return Nodes(); }, $"{shown.Hit()}", () => { shown.Count++; return []; }, bound: $"{shown.Hit()}");
        tree.Accept(() => { accepts.Count++; return Nodes(); }, $"{accepts.Hit()}", () => { accepts.Count++; return []; }, child: $"{labels.Hit("b")}", queued: a, bound: $"{accepts.Hit()}");
        tree.Expand(root, () => { shown.Count++; return Nodes(); }, $"{shown.Hit()}");   // a jump, past the cap
        Assert.Equal(0, shown.Count);
        Assert.Equal(2, labels.Count);
        Assert.Equal(4, accepts.Count);
    }
}
