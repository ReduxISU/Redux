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
        keyword.Backtrack(() => Nodes("a"), $"drop b", "b");
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

    // Text that is not an interpolated string does not compile (CS9205), so a computed caption can never be built eagerly;
    // fixed text is written as a constant interpolated string.
    [Fact]
    public void ConstantInterpolatedStrings_AreTheFixedText() {
        var rec = new StepRecorder<NodeSet>();
        rec.Reject(() => Nodes("a"), $"already fixed");
        Assert.Equal("already fixed", rec.Steps[0].Caption);
    }

    // ---- SearchTree ----

    private sealed class State(string name) {
        public string Name { get; } = name;
        public override string ToString() => Name;
    }

    // A tree whose converters count their calls: a state shows as its name, a choice (an int) as "name+choice".
    private static SearchTree<State, int, NodeSet> TreeOn(StepRecorder<NodeSet> rec, Counter calls) => new(rec,
        partial: s => { calls.Count++; return Nodes(s.Name); },
        focus: s => { calls.Count++; return [s.Name]; },
        childPartial: (s, c) => { calls.Count++; return Nodes(s.Name, c.ToString()); },
        childLabel: (s, c) => $"{s.Name}+{c}",
        rejectFocus: (s, c) => { calls.Count++; return [s.Name, $"bad{c}"]; },
        rejectNote: (s, c) => { calls.Count++; return $"no {c}"; },
        queuedNote: s => { calls.Count++; return $"queued {s.Name}"; },
        solvedNote: s => { calls.Count++; return $"solved {s.Name}"; });

    private static IReadOnlyList<SearchNode> TreeOf(SolverStep<NodeSet> step) => step.Tree!;

    [Fact]
    public void Tree_ChoiceSteps_TakeTheirPartialFocusLabelAndNoteFromTheConverters() {
        var rec = new StepRecorder<NodeSet>();
        var tree = TreeOn(rec, new Counter());
        State root = new("r"), a = new("a");
        tree.Root(root, $"start");
        tree.Expand(root, $"never: the root is not a jump");
        tree.Try(root, 1, $"try 1");
        tree.Accept(root, 1, $"keep 1", queued: a);
        tree.Try(root, 2, $"try 2");
        tree.Reject(root, 2, $"2 fails");
        tree.Reject(root, 3, $"3 fails too", note: $"own note");

        Assert.Equal(new[] { StepEvent.Try, StepEvent.Accept, StepEvent.Try, StepEvent.Reject, StepEvent.Reject }, rec.Steps.Select(s => s.Event));
        Assert.Equal(Nodes("r", "1"), rec.Steps[0].Partial);
        Assert.Equal(["r"], rec.Steps[0].Focus);
        Assert.Equal([new SearchNode(0, null, "start", SearchNodeState.Queued), new SearchNode(1, 0, "r+1", SearchNodeState.Explored)], TreeOf(rec.Steps[0]));
        // The Accept is about the child the Try of the same choice made.
        Assert.Equal([new SearchNode(1, 0, "r+1", SearchNodeState.Queued) { Bound = "queued a" }], TreeOf(rec.Steps[1]));
        Assert.Equal([new SearchNode(2, 0, "r+2", SearchNodeState.Explored)], TreeOf(rec.Steps[2]));
        Assert.Equal(["r", "bad2"], rec.Steps[3].Focus);
        Assert.Equal([new SearchNode(2, 0, "r+2", SearchNodeState.Pruned) { Bound = "no 2" }], TreeOf(rec.Steps[3]));
        // A choice with no Try before it makes its own child; a given note wins over the converter.
        Assert.Equal([new SearchNode(3, 0, "r+3", SearchNodeState.Pruned) { Bound = "own note" }], TreeOf(rec.Steps[4]));
        // Children of a queued state go under its node.
        tree.Expand(a, $"never: a was just queued");
        tree.Try(a, 4, $"try 4");
        Assert.Equal(1, rec.Steps[^1].Tree![0].ParentId);
    }

    [Fact]
    public void Tree_Expand_RecordsABacktrackOnlyWhenTheSearchJumps() {
        var rec = new StepRecorder<NodeSet>();
        var tree = TreeOn(rec, new Counter());
        State root = new("root"), a = new("a"), b = new("b"), c = new("c");
        var captions = new Counter();
        tree.Root(root, $"start");
        tree.Expand(root, $"{captions.Hit()}");
        tree.Accept(root, 1, $"queue a", queued: a);
        tree.Accept(root, 2, $"queue b", queued: b);
        tree.Expand(a, $"{captions.Hit()}");    // a child just queued: no step
        tree.Accept(a, 3, $"queue c", queued: c);
        Assert.False(tree.DeadEnd);                // a queued c
        tree.Expand(c, $"{captions.Hit()}");    // c, just queued: no step
        tree.Reject(c, 4, $"4 fails");
        Assert.True(tree.DeadEnd);                 // c queued nothing
        tree.Expand(b, $"{(tree.DeadEnd ? "dead end, " : "")}back to {captions.Hit("b")}");

        Assert.Equal(1, captions.Count);
        var back = Assert.Single(rec.Steps, s => s.Event == StepEvent.Backtrack);
        Assert.Equal("dead end, back to b", back.Caption);
        Assert.Equal(Nodes("b"), back.Partial);
        Assert.Equal(["b"], back.Focus);
        Assert.Equal([new SearchNode(2, 0, "root+2", SearchNodeState.Explored)], back.Tree);
    }

    [Fact]
    public void Tree_StateSteps_TheWaitingRoot_AndOverrides() {
        var rec = new StepRecorder<NodeSet>();
        var tree = TreeOn(rec, new Counter());
        State root = new("A"), b = new("B");
        tree.Root(root, $"A", $"lb {4}");
        tree.Try(root, $"start at A");                         // the root while it waits: shown as Root made it
        tree.Try(root, $"expand A");
        tree.Accept(root, 1, $"queue B", queued: b);
        tree.Reject(b, $"no better", note: $"tour 9", outcome: SearchNodeState.Failed, partial: () => Nodes("A", "B", "A"), focus: () => ["B", "A"]);

        Assert.Equal([new SearchNode(0, null, "A", SearchNodeState.Queued) { Bound = "lb 4" }], TreeOf(rec.Steps[0]));
        Assert.Equal(Nodes("A"), rec.Steps[0].Partial);
        Assert.Equal([new SearchNode(0, null, "A", SearchNodeState.Explored)], TreeOf(rec.Steps[1]));
        Assert.Equal([new SearchNode(1, 0, "A+1", SearchNodeState.Failed) { Bound = "tour 9" }], TreeOf(rec.Steps[3]));
        Assert.Equal(Nodes("A", "B", "A"), rec.Steps[3].Partial);
        Assert.Equal(["B", "A"], rec.Steps[3].Focus);
    }

    [Fact]
    public void Tree_Done_NamesThePathToTheLastSolution_OrAStartingAnswerAlone() {
        var rec = new StepRecorder<NodeSet>(cap: 1);
        var tree = TreeOn(rec, new Counter());
        State root = new("r"), a = new("a"), done = new("done");
        tree.Root(root, $"start");
        tree.Accept($"greedy", leaf: $"greedy", partial: () => Nodes(), focus: () => [], note: $"tour 10");
        tree.Try(root, $"expand");
        tree.Accept(root, 1, $"queue a", queued: a);
        tree.Try(a, $"expand a");
        tree.Try(a, 2, $"try 2");
        tree.Accept(a, 2, $"solved", solved: done);           // past the cap
        tree.Done(Nodes("a", "2"), true, $"done");

        Assert.Equal([
            new SearchNode(0, null, "start", SearchNodeState.Explored),
            new SearchNode(2, 0, "r+1", SearchNodeState.Explored),
            new SearchNode(3, 2, "a+2", SearchNodeState.Solution) { Bound = "solved done" },
        ], rec.Steps[^1].Tree);

        var leafRec = new StepRecorder<NodeSet>();
        var leafTree = TreeOn(leafRec, new Counter());
        leafTree.Root(root, $"start");
        leafTree.Accept($"greedy", leaf: $"greedy", partial: () => Nodes(), focus: () => [], note: $"tour 10");
        leafTree.Try(root, $"expand");
        leafTree.Done(Nodes(), true, $"done");
        Assert.Equal([new SearchNode(1, 0, "greedy", SearchNodeState.Solution) { Bound = "tour 10" }], leafRec.Steps[^1].Tree);

        var failRec = new StepRecorder<NodeSet>();
        var failTree = TreeOn(failRec, new Counter());
        failTree.Root(root, $"start");
        failTree.Accept($"greedy", leaf: $"greedy", partial: () => Nodes(), focus: () => [], note: $"tour 10");
        failTree.Done(Nodes(), false, $"over K");
        Assert.Null(failRec.Steps[^1].Tree);
    }

    [Fact]
    public void Tree_Off_CallsNoConverter_AndFormatsNothing() {
        foreach (var rec in new[] { StepRecorder<NodeSet>.Off, StepRecorder<NodeSet>.DeadlineOnly(TimeSpan.FromMinutes(5)) }) {
            var c = new Counter();
            var tree = TreeOn(rec, c);
            State root = new("root"), a = new("a"), b = new("b");
            tree.Root(root, $"{c.Hit()}", $"{c.Hit()}");
            tree.Expand(root, $"{c.Hit()}");
            tree.Try(root, $"{c.Hit()}");
            tree.Try(root, 1, $"{c.Hit()}");
            tree.Accept(root, 1, $"{c.Hit()}", queued: a, note: $"{c.Hit()}");
            tree.Reject(root, 2, $"{c.Hit()}", note: $"{c.Hit()}");
            tree.Reject(root, 3, $"{c.Hit()}");
            tree.Accept(root, $"{c.Hit()}", note: $"{c.Hit()}", partial: () => { c.Count++; return Nodes(); }, focus: () => { c.Count++; return []; });
            tree.Accept($"{c.Hit()}", leaf: $"{c.Hit()}", partial: () => { c.Count++; return Nodes(); }, focus: () => { c.Count++; return []; });
            tree.Accept(a, 4, $"{c.Hit()}", solved: b);
            tree.Expand(b, $"{c.Hit()}");     // a jump: still nothing built
            tree.Done(Nodes(), true, $"{c.Hit()}");
            Assert.Equal(0, c.Count);
            Assert.Empty(rec.Steps);
        }
    }

    // Past the cap a Try or Reject calls no converter but the label (a later step or the Done may name its node); an Accept
    // is built in full, as the recorder's Accepts are.
    [Fact]
    public void Tree_PastTheCap_BuildsOnlyLabelsAndAccepts() {
        var rec = new StepRecorder<NodeSet>(cap: 1);
        var calls = new Counter();
        var tree = TreeOn(rec, calls);
        var shown = new Counter();
        var accepts = new Counter();
        State root = new("root"), a = new("a");
        tree.Root(root, $"start");
        tree.Try(root, $"kept");
        calls.Count = 0;
        tree.Try(root, 1, $"{shown.Hit()}");
        tree.Reject(root, 1, $"{shown.Hit()}", note: $"{shown.Hit()}");
        tree.Reject(root, 2, $"{shown.Hit()}");
        tree.Reject(root, $"{shown.Hit()}", note: $"{shown.Hit()}");
        tree.Expand(root, $"{shown.Hit()}");   // a jump, past the cap
        Assert.Equal(0, shown.Count);
        Assert.Equal(0, calls.Count);
        tree.Accept(root, 3, $"{accepts.Hit()}", queued: a);
        Assert.Equal(1, accepts.Count);
        Assert.Equal(3, calls.Count);           // childPartial, focus (the state's, by default) and queuedNote
    }
}
