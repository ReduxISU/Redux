using System.Text.Json;
using API.Interfaces;
using API.Interfaces.SearchTree;
using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Unit tests of the Search Tree picture: recorded steps with SearchNode patches -> the drawn tree, its states, folding and
// ids. No endpoint, no solver: runs are built by hand. The partial answer is never read, so these use NodeSet steps.
public class SearchTreeFrames_Tests {
    private static readonly SearchTreeSpec Spec = new("Test");

    private static SearchNode N(int id, int? parent, string label, SearchNodeState state, string? bound = null) =>
        new(id, parent, label, state) { Bound = bound };

    private static SolverStep<NodeSet> Step(StepEvent ev, string caption, params SearchNode[] tree) =>
        new(new NodeSet([]), ev, [], caption) { Tree = tree.Length == 0 ? null : tree };

    private static SolverStep<NodeSet> Done(bool ok, string caption = "Done.") =>
        new(new NodeSet([]), StepEvent.Done, [], caption) { Ok = ok };

    private static List<SearchTreeFrame> Frames(SolveRun run, Func<string, bool>? verify = null) {
        var response = SearchTreeFrames.Build<NodeSet>(Spec, "instance", run, verify ?? (_ => throw new InvalidOperationException("The answer must not be read.")));
        Assert.Equal("SearchTree", response.Type);
        return response.Frames.Cast<SearchTreeFrame>().ToList();
    }

    private static SolveRun Run(params SolverStep<NodeSet>[] steps) => new("answer", steps.Cast<object>().ToList(), typeof(NodeSet));

    private static Dictionary<string, SearchTreeNodeDto> ById(SearchTreeFrame f) => f.Nodes.ToDictionary(n => n.Id);

    private const SearchNodeState Q = SearchNodeState.Queued, E = SearchNodeState.Explored, P = SearchNodeState.Pruned,
        F = SearchNodeState.Failed, S = SearchNodeState.Solution;

    // A depth-first run: root, a=1 (pruned), a=2 then b=1 (fails, backs out), b=2 solution.
    private static SolveRun DepthFirst() => Run(
        Step(StepEvent.Try, "Try a=1.", N(0, null, "start", E), N(1, 0, "a=1", E)),
        Step(StepEvent.Reject, "a=1 clashes.", N(1, 0, "a=1", P, "clash")),
        Step(StepEvent.Try, "Try a=2.", N(2, 0, "a=2", E)),
        Step(StepEvent.Try, "Try b=1.", N(3, 2, "b=1", E)),
        Step(StepEvent.Reject, "Nothing works under b=1.", N(3, 2, "b=1", F)),
        Step(StepEvent.Try, "Try b=2.", N(4, 2, "b=2", S)),
        Done(true));

    // ---- payload and shape ----

    [Fact]
    public void Payload_NamesTheProblemAndInstance() {
        var p = SearchTreeFrames.Payload(new SearchTreeSpec("3SAT"), "(x1)");
        Assert.Equal("{\"kind\":\"searchTree\",\"problem\":\"3SAT\",\"instance\":\"(x1)\",\"maxLeaves\":12}", JsonSerializer.Serialize(p));
    }

    [Fact]
    public void Frame_Json_HasTheContractsFields_AndOmitsUnsetOptionalOnes() {
        var frames = Frames(DepthFirst());
        var json = JsonDocument.Parse(JsonSerializer.Serialize<object>(frames[1], FramesResponse.JsonOptions)).RootElement;
        Assert.Equal(["event", "caption", "focus", "nodes", "total", "cut"], json.EnumerateObject().Select(p => p.Name));
        var nodes = json.GetProperty("nodes").EnumerateArray().ToList();
        Assert.Equal(["id", "parent", "label", "kind", "state"], nodes[0].EnumerateObject().Select(p => p.Name));
        Assert.Equal(JsonValueKind.Null, nodes[0].GetProperty("parent").ValueKind);
        Assert.Equal(["id", "parent", "label", "kind", "state", "bound"], nodes[1].EnumerateObject().Select(p => p.Name));
        Assert.Equal("Rejected", nodes[1].GetProperty("state").GetString());
        Assert.Equal("pruned", nodes[1].GetProperty("kind").GetString());
        var done = JsonDocument.Parse(JsonSerializer.Serialize<object>(frames[^1], FramesResponse.JsonOptions)).RootElement;
        Assert.True(done.GetProperty("ok").GetBoolean());
    }

    // ---- building the tree ----

    [Fact]
    public void Tree_GrowsStepByStep_ParentsBeforeChildren_InTheOrderTheyWereMade() {
        var frames = Frames(DepthFirst());
        Assert.Equal(7, frames.Count);
        Assert.Equal([("n0", null), ("n1", "n0")], frames[0].Nodes.Select(n => (n.Id, n.Parent)));
        Assert.Equal(["n0", "n1", "n2", "n3", "n4"], frames[5].Nodes.Select(n => n.Id));
        Assert.Equal(["start", "a=1", "a=2", "b=1", "b=2"], frames[5].Nodes.Select(n => n.Label));
        Assert.Equal([null, "n0", "n0", "n2", "n2"], frames[5].Nodes.Select(n => n.Parent));
        Assert.Equal([2, 2, 3, 4, 4, 5, 5], frames.Select(f => f.Total));
        Assert.Equal([0, 1, 1, 1, 1, 1, 1], frames.Select(f => f.Cut));
        Assert.Equal(["Try a=1.", "a=1 clashes.", "Try a=2.", "Try b=1.", "Nothing works under b=1.", "Try b=2.", "Done."], frames.Select(f => f.Caption));
    }

    [Fact]
    public void States_FocusIsHighlighted_RejectsAreRejected_ExploredIsCovered() {
        var frames = Frames(DepthFirst());
        // Try: the node being worked on is ElementHighlight, the root it came from Covered.
        Assert.Equal(["n1"], frames[0].Focus);
        Assert.Equal(PictureState.ElementHighlight, ById(frames[0])["n1"].State);
        Assert.Equal(PictureState.Covered, ById(frames[0])["n0"].State);
        // Reject: the cut node is Rejected and keeps its bound note; kind tells dashed (pruned) from solid (failed).
        Assert.Equal(PictureState.Rejected, ById(frames[1])["n1"].State);
        Assert.Equal("clash", ById(frames[1])["n1"].Bound);
        Assert.Equal("failed", ById(frames[4])["n3"].Kind);
        Assert.Equal(PictureState.Rejected, ById(frames[4])["n3"].State);
        Assert.Equal("pruned", ById(frames[4])["n1"].Kind);
        // Once the focus moves on, a node keeps the state of its kind.
        Assert.Equal(PictureState.Covered, ById(frames[3])["n2"].State);
        Assert.Equal(PictureState.Rejected, ById(frames[3])["n1"].State);
        // No order badges in a depth-first run.
        Assert.All(frames.SelectMany(f => f.Nodes), n => Assert.Null(n.Order));
    }

    [Fact]
    public void AnswerPath_IsSolution_OnlyInTheDoneFrameWithOkTrue() {
        var frames = Frames(DepthFirst());
        // The Try that makes the solution node does not claim it yet.
        Assert.DoesNotContain(frames[5].Nodes, n => n.State == PictureState.Solution);
        Assert.Equal("solution", ById(frames[5])["n4"].Kind);
        var done = frames[^1];
        Assert.Empty(done.Focus);
        Assert.True(done.Ok);
        Assert.Equal(["n0", "n2", "n4"], done.Nodes.Where(n => n.State == PictureState.Solution).Select(n => n.Id));
        Assert.Equal(PictureState.Rejected, ById(done)["n1"].State);
        Assert.Equal(PictureState.Rejected, ById(done)["n3"].State);

        var failed = Frames(Run(
            Step(StepEvent.Try, "Try.", N(0, null, "start", E), N(1, 0, "a=1", S)),
            Done(false)));
        Assert.DoesNotContain(failed[^1].Nodes, n => n.State == PictureState.Solution);
        Assert.False(failed[^1].Ok);
    }

    // A best-first run: the root is queued, both children are queued, the second is expanded first (after a Backtrack),
    // a third node is pruned when it leaves the queue, and two complete answers arrive, the second better.
    private static SolveRun BestFirst() => Run(
        Step(StepEvent.Try, "Start.", N(0, null, "A", Q, "lb 10")),
        Step(StepEvent.Accept, "Queue B.", N(1, 0, "B", Q, "lb 12")),
        Step(StepEvent.Accept, "Queue C.", N(2, 0, "C", Q, "lb 11")),
        Step(StepEvent.Backtrack, "Expand C.", N(2, 0, "C", E)),
        Step(StepEvent.Accept, "Close a tour under C.", N(3, 2, "D", S, "tour 20")),
        Step(StepEvent.Accept, "Close a better tour under B.", N(4, 1, "E", S, "tour 15")),
        Step(StepEvent.Reject, "Drop nothing.", N(4, 1, "E", S)),
        Done(true));

    [Fact]
    public void BestFirst_QueuedNodesAreUntraveled_AndGetOrderBadgesWhenTheyLeaveTheQueue() {
        var frames = Frames(BestFirst());
        Assert.Equal("queued", ById(frames[0])["n0"].Kind);
        Assert.Equal(PictureState.ElementHighlight, ById(frames[0])["n0"].State);
        // Getting a child takes the root off the queue: it is explored, and first out.
        Assert.Equal("explored", ById(frames[1])["n0"].Kind);
        Assert.Equal(1, ById(frames[1])["n0"].Order);
        Assert.Equal(PictureState.Untraveled, ById(frames[2])["n1"].State);
        Assert.Null(ById(frames[2])["n1"].Order);
        // The Backtrack to C takes it off the queue second; B (queued since frame 1) leaves third when it gets a child.
        Assert.Equal(PictureState.ElementHighlight, ById(frames[3])["n2"].State);
        Assert.Equal(2, ById(frames[3])["n2"].Order);
        Assert.Equal(3, ById(frames[5])["n1"].Order);
        Assert.Equal("lb 11", ById(frames[3])["n2"].Bound);
    }

    [Fact]
    public void BestFirst_TheAcceptThatReachesAnAnswerShowsItsPath_AndABetterAnswerDemotesTheOldOne() {
        var frames = Frames(BestFirst());
        // The Accept that closes the first tour: its path is Solution, the node itself is in focus.
        Assert.Equal(PictureState.ElementHighlight, ById(frames[4])["n3"].State);
        Assert.Equal(PictureState.Solution, ById(frames[4])["n2"].State);
        Assert.Equal(PictureState.Solution, ById(frames[4])["n0"].State);
        // The better tour takes over; the old one is just explored now.
        Assert.Equal("explored", ById(frames[5])["n3"].Kind);
        Assert.Equal(PictureState.Covered, ById(frames[5])["n3"].State);
        Assert.Equal(["n0", "n1"], frames[5].Nodes.Where(n => n.State == PictureState.Solution).Select(n => n.Id));
        // A later step about the solution node is not an Accept: no path.
        Assert.DoesNotContain(frames[6].Nodes, n => n.State == PictureState.Solution);
        Assert.Equal(["n0", "n1", "n4"], frames[^1].Nodes.Where(n => n.State == PictureState.Solution).Select(n => n.Id));
    }

    // ---- steps without patches, missing parents, stopped runs ----

    [Fact]
    public void StepsWithoutPatches_KeepTheTree_AndHaveNoFocus() {
        var frames = Frames(Run(
            Step(StepEvent.Accept, "A note before the search."),
            Step(StepEvent.Try, "Try a.", N(0, null, "start", E), N(1, 0, "a", E)),
            Step(StepEvent.Reject, "A note with no node."),
            Done(false)));
        Assert.Empty(frames[0].Nodes);
        Assert.Empty(frames[0].Focus);
        Assert.Equal(frames[1].Nodes.Select(n => n.Id), frames[2].Nodes.Select(n => n.Id));
        Assert.Empty(frames[2].Focus);
        Assert.Equal(PictureState.Covered, ById(frames[2])["n1"].State);
    }

    [Fact]
    public void ASolverThatRecordsNoTree_GetsEmptyFrames_AndTheSolvedFrameFromTheVerifier() {
        var run = new SolveRun("{a}", new List<object> { new SolverStep<NodeSet>(new NodeSet(["a"]), StepEvent.Try, ["a"], "Try {a}.") }, typeof(NodeSet));
        var frames = Frames(run, answer => answer == "{a}");
        Assert.Equal(2, frames.Count);
        Assert.All(frames, f => Assert.Empty(f.Nodes));
        Assert.Equal(StepEvent.Done, frames[^1].Event);
        Assert.True(frames[^1].Ok);
        Assert.Equal("The answer is {a}.", frames[^1].Caption);
        // Steps of another shape cannot be drawn: just the solved frame.
        var other = new SolveRun("{}", new List<object> { new SolverStep<Tour>(new Tour([]), StepEvent.Try, [], "x") { Tree = [N(0, null, "s", E)] } }, typeof(Tour));
        var only = Frames(other, _ => false);
        Assert.Equal("No answer was found.", Assert.Single(only).Caption);
        Assert.Empty(only[0].Nodes);
    }

    [Fact]
    public void APatchWhoseParentWasNeverSeen_HangsUnderAHiddenStandInUnderTheRoot() {
        var frames = Frames(Run(
            Step(StepEvent.Try, "Try a.", N(0, null, "start", E), N(1, 0, "a", E)),
            Step(StepEvent.Accept, "Found it, far down.", N(9, 7, "z", S)),
            Done(true)));
        var f = frames[1];
        Assert.Equal([("n0", null), ("n1", "n0"), ("n7", "n0"), ("n9", "n7")], f.Nodes.Select(n => (n.Id, n.Parent)));
        Assert.Equal("hidden", ById(f)["n7"].Kind);
        Assert.Equal("…", ById(f)["n7"].Label);
        Assert.Equal(PictureState.Solution, ById(f)["n7"].State);
        Assert.Equal(["n0", "n7", "n9"], frames[^1].Nodes.Where(n => n.State == PictureState.Solution).Select(n => n.Id));
    }

    [Fact]
    public void StoppedRun_EndsWithTheTreeSoFar_NotOk_NothingHighlighted_AndTheAnswerIsNeverRead() {
        var steps = new List<object> {
            Step(StepEvent.Try, "Try a.", N(0, null, "start", E), N(1, 0, "a", S)),
            Step(StepEvent.Accept, "Keep a.", N(1, 0, "a", S)),
            new SolverStep<NodeSet>(new NodeSet([]), StepEvent.Done, [], "Stopped after 0.05 s and 2 steps; the solver had not finished.") { Ok = false },
        };
        var frames = Frames(new SolveRun(SolveRun.TimedOutAnswer, steps, typeof(NodeSet)) { TimedOut = true });
        var done = frames[^1];
        Assert.Equal(3, frames.Count);
        Assert.False(done.Ok);
        Assert.Empty(done.Focus);
        Assert.Equal(["n0", "n1"], done.Nodes.Select(n => n.Id));
        Assert.DoesNotContain(done.Nodes, n => n.State is PictureState.Solution or PictureState.ElementHighlight);
        Assert.StartsWith("Stopped after", done.Caption);

        // Stopped before any step of this shape was kept: one Done frame, the shared caption, no tree.
        var none = Frames(new SolveRun(SolveRun.TimedOutAnswer, new List<object>(), typeof(NodeSet)) { TimedOut = true });
        Assert.False(Assert.Single(none).Ok);
        Assert.Empty(none[0].Nodes);
        Assert.Equal(API.Interfaces.Graphs.GraphSubsetFrames.StoppedCaption, none[0].Caption);
    }

    // ---- folding ----

    // A root with `branches` explored children, each with `width` failed leaves, then a last child with `cuts` pruned leaves
    // and the focus.
    private static List<SolverStep<NodeSet>> Wide(int branches, int width, int cuts) {
        var steps = new List<SolverStep<NodeSet>> { Step(StepEvent.Try, "Start.", N(0, null, "start", E)) };
        int id = 1;
        for (int b = 0; b < branches; b++) {
            int branch = id++;
            steps.Add(Step(StepEvent.Try, $"Branch {b}.", N(branch, 0, $"b{b}", E)));
            for (int w = 0; w < width; w++) steps.Add(Step(StepEvent.Reject, $"Leaf {w}.", N(id++, branch, $"l{w}", F)));
            steps.Add(Step(StepEvent.Reject, $"Back out of {b}.", N(branch, 0, $"b{b}", F)));
        }
        int last = id++;
        steps.Add(Step(StepEvent.Try, "Last branch.", N(last, 0, "last", E)));
        for (int c = 0; c < cuts; c++) steps.Add(Step(StepEvent.Reject, $"Cut {c}.", N(id++, last, $"c{c}", P)));
        steps.Add(Step(StepEvent.Try, "Go on.", N(id, last, "go", E)));
        return steps;
    }

    private static int Leaves(SearchTreeFrame f) => f.Nodes.Count(n => !f.Nodes.Any(k => k.Parent == n.Id));

    [Fact]
    public void Folding_UpTo12Leaves_DrawsEveryNode() {
        var f = Frames(Run([.. Wide(2, 3, 5)]), _ => false)[^2];   // 6 failed leaves + 5 cut + go = 12 leaves
        Assert.Equal(12, Leaves(f));
        Assert.DoesNotContain(f.Nodes, n => n.Kind is "more" or "cut");
        Assert.Equal(f.Total, f.Nodes.Count);
    }

    [Fact]
    public void Folding_PastTheLimit_GroupsCutLeaves_ThenFoldsFinishedBranches_KeepingTheFocusInView() {
        var frames = Frames(Run([.. Wide(4, 4, 5)]), _ => false);   // 16 failed leaves + 5 cut + go = 22 leaves
        var f = frames[^2];
        Assert.True(Leaves(f) <= SearchTreeFrames.MaxLeaves, $"{Leaves(f)} leaves");
        var cut = Assert.Single(f.Nodes, n => n.Kind == "cut");
        Assert.Equal(("n21.cut", "n21", "5 cut", 5, PictureState.Rejected), (cut.Id, cut.Parent, cut.Label, cut.Hidden, cut.State));
        var folds = f.Nodes.Where(n => n.Kind == "more").ToList();
        Assert.NotEmpty(folds);
        Assert.All(folds, m => {
            Assert.Equal(m.Parent + ".more", m.Id);
            Assert.Equal("+4 more", m.Label);
            Assert.Equal(4, m.Hidden);
            Assert.Equal(PictureState.Background, m.State);
        });
        // The node in focus and its parents are drawn.
        Assert.Equal(["n27"], f.Focus);
        Assert.Contains(f.Nodes, n => n.Id == "n27" && n.State == PictureState.ElementHighlight);
        Assert.Contains(f.Nodes, n => n.Id == "n21");
        // Hidden counts and drawn nodes add up to every node made.
        Assert.Equal(f.Total, f.Nodes.Count(n => n.Kind is not ("more" or "cut")) + f.Nodes.Sum(n => n.Hidden ?? 0));
        Assert.Equal(28, f.Total);
        Assert.Equal(5, f.Cut);
    }

    [Fact]
    public void Folding_IdsAreTheSameInEveryFrame() {
        var frames = Frames(Run([.. Wide(4, 4, 5)]), _ => false);
        var seen = new Dictionary<string, string>();
        foreach (var n in frames.SelectMany(f => f.Nodes)) {
            // The same id always names the same node (same parent), so a renderer can animate between frames.
            if (seen.TryGetValue(n.Id, out var parent)) Assert.Equal(parent, n.Parent ?? "");
            else seen[n.Id] = n.Parent ?? "";
        }
        Assert.All(frames, f => Assert.Equal(f.Nodes.Count, f.Nodes.Select(n => n.Id).Distinct().Count()));
        Assert.All(frames, f => Assert.All(f.Nodes.Where(n => n.Parent != null), n => Assert.Contains(f.Nodes, p => p.Id == n.Parent)));
    }

    [Fact]
    public void Folding_NeverFoldsQueuedNodesOrTheAnswersPath() {
        var steps = new List<SolverStep<NodeSet>> { Step(StepEvent.Try, "Start.", N(0, null, "start", Q)) };
        for (int i = 1; i <= 20; i++) steps.Add(Step(StepEvent.Accept, $"Queue {i}.", N(i, 0, $"q{i}", Q)));
        steps.Add(Step(StepEvent.Accept, "Answer.", N(21, 20, "end", S)));
        steps.Add(Done(true));
        var frames = Frames(Run([.. steps]));
        Assert.All(frames, f => Assert.DoesNotContain(f.Nodes, n => n.Kind is "more" or "cut"));
        Assert.Equal(["n0", "n20", "n21"], frames[^1].Nodes.Where(n => n.State == PictureState.Solution).Select(n => n.Id));
    }
}
