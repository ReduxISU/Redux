using System.Text.Json.Serialization;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;

namespace API.Interfaces.SearchTree;

/// <summary>
/// What a problem hands the Search Tree picture: its name as a caption shows it (<c>3SAT</c>). The tree itself comes from
/// the solver's steps (<see cref="SolverStep{TPartial}.Tree"/>), never from the instance.
/// </summary>
sealed record SearchTreeSpec(string Problem);

// --- JSON shapes (explicit names so the contract does not depend on serializer naming options) ---

/// <summary>The static part of a Search Tree picture, sent once per response.</summary>
/// <param name="Kind">Always <c>searchTree</c>.</param>
/// <param name="Problem">The problem's name.</param>
/// <param name="Instance">The instance as given.</param>
/// <param name="MaxLeaves">About how many leaves a frame shows before it folds finished branches.</param>
sealed record SearchTreePayload(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("problem")] string Problem,
    [property: JsonPropertyName("instance")] string Instance,
    [property: JsonPropertyName("maxLeaves")] int MaxLeaves);

/// <summary>
/// One drawn node of a frame's tree.
/// <list type="bullet">
/// <item><c>id</c>: <c>n{nodeId}</c> for a node of the search; <c>n{nodeId}.more</c> for the fold that stands for the
/// finished subtree under that node; <c>n{nodeId}.cut</c> for the node that stands for that node's cut-off children.</item>
/// <item><c>parent</c>: the parent's id; null for the root.</item>
/// <item><c>kind</c>: what the search did with it (<c>queued</c>, <c>explored</c>, <c>pruned</c>, <c>failed</c>,
/// <c>solution</c>, <c>skipped</c>), or <c>more</c> / <c>cut</c> for a fold, or <c>hidden</c> for a node whose own step was not
/// kept (past the step cap). <c>pruned</c> and <c>cut</c> are drawn dashed, <c>failed</c> solid.</item>
/// <item><c>state</c>: its named state in this frame.</item>
/// <item><c>bound</c>: the solver's note on the bound or cost that decided it.</item>
/// <item><c>order</c>: for a best-first solver, when the node left the queue (1 for the first).</item>
/// <item><c>hidden</c>: for a fold, how many nodes it stands for.</item>
/// </list>
/// </summary>
sealed record SearchTreeNodeDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("parent")] string? Parent,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("state")] PictureState State) {
    [JsonPropertyName("bound")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Bound { get; init; }

    [JsonPropertyName("order")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Order { get; init; }

    [JsonPropertyName("hidden")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Hidden { get; init; }
}

/// <summary>
/// One picture of the tree.
/// <list type="bullet">
/// <item><c>focus</c>: the id of the node the step is about, or empty.</item>
/// <item><c>nodes</c>: the drawn tree after the step, parents before children, children in the order they were made.</item>
/// <item><c>total</c>: how many nodes the search has made so far (folded ones included); <c>cut</c>: how many of them it cut
/// off without exploring.</item>
/// </list>
/// </summary>
sealed record SearchTreeFrame(
    [property: JsonPropertyName("event")] StepEvent Event,
    [property: JsonPropertyName("caption")] string Caption,
    [property: JsonPropertyName("focus")] string[] Focus,
    [property: JsonPropertyName("nodes")] IReadOnlyList<SearchTreeNodeDto> Nodes,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("cut")] int Cut) {
    /// <summary>The solver step this frame was drawn from (see <see cref="FramesResponse"/>, #895); null for an added frame. Always written.</summary>
    [JsonPropertyName("step")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int? Step { get; init; }

    [JsonPropertyName("ok")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Ok { get; init; }
}

/// <summary>
/// Builds the Search Tree picture, a second view of a backtracking or branch-and-bound solver: the tree of choices it made,
/// growing step by step. The tree is read from the <see cref="SearchNode"/> patches its steps carry
/// (<see cref="SolverStep{TPartial}.Tree"/>); the step's partial answer is never read, so any step shape works. Nothing here
/// knows about a particular solver.
/// <para>
/// <b>Building the tree.</b> A patch whose node id is new creates the node under its parent (children keep the order they were
/// made); a patch for a node that exists updates its state and bound. A node that gets a child while <c>queued</c> has been
/// taken off the queue: it becomes <c>explored</c>. A node that leaves the queue (to explored, solution or failed) gets the next
/// order number, so best-first solvers get order badges and depth-first ones (which never queue) get none. A new
/// <c>solution</c> demotes the previous one to <c>explored</c> (a branch and bound's best tour so far). A patch whose parent was
/// never seen (its step fell past the recorder's cap) hangs under a <c>hidden</c> stand-in for that parent, under the root.
/// </para>
/// <para>
/// <b>States</b> (precedence <c>Rejected &gt; ElementHighlight &gt; Solution &gt; Committed &gt; Covered &gt; Background</c>; see
/// <see cref="GraphSubsetFrames.Raise"/>):
/// </para>
/// <list type="bullet">
/// <item>explored and solution nodes are <c>Covered</c>; pruned and failed ones <c>Rejected</c> (<c>kind</c> tells dashed from
/// solid); queued and skipped ones <c>Untraveled</c>; folds of finished branches and hidden stand-ins <c>Background</c>, folds of
/// cut-off children <c>Rejected</c>.</item>
/// <item>The node the step is about (its last patch) is marked as <see cref="GraphSubsetFrames.FocusMark"/> says:
/// <c>ElementHighlight</c>, or <c>Rejected</c> in a Reject.</item>
/// <item>The answer's path, from the root to the latest solution node, is marked only in the Accept that reaches it and in a
/// Done with ok = true: <c>Solution</c> in the Done and in an Accept that ends the run, and <c>Committed</c> in an Accept the
/// run goes on from (a best so far a branch and bound may still beat; #886). A Done step may carry patches too: a solver whose answer was found past the recorder's cap
/// names the answer's path there, so the tree ends on it.</item>
/// <item>A run stopped by its time limit ends with the tree so far in a Done frame with ok = false: nothing highlighted, no
/// answer path. Its answer string is never read.</item>
/// </list>
/// <para>
/// <b>Folding.</b> Past <see cref="MaxLeaves"/> leaves, the cut-off leaves under one node (two or more) share one
/// <c>n{id}.cut</c> node ("3 cut"); while there are still too many leaves, the finished subtree with the most leaves (never
/// one holding the node in focus, a queued node or the answer's path) folds into one <c>n{id}.more</c> node ("+7 more"). Ids
/// come from the solver's node ids, so they are the same in every frame.
/// </para>
/// </summary>
static class SearchTreeFrames {
    public const string Kind = "searchTree";

    /// <summary>About how many leaves a frame draws before it folds.</summary>
    public const int MaxLeaves = 12;

    /// <summary>The id of a search node.</summary>
    public static string NodeId(int nodeId) => $"n{nodeId}";

    /// <summary>The id of the fold that stands for the finished subtree under a node.</summary>
    public static string MoreId(int nodeId) => $"n{nodeId}.more";

    /// <summary>The id of the node that stands for a node's cut-off children.</summary>
    public static string CutId(int nodeId) => $"n{nodeId}.cut";

    /// <summary>The <c>kind</c> of a search node: its state's name.</summary>
    public static string KindOf(SearchNodeState state) => state switch {
        SearchNodeState.Queued => "queued",
        SearchNodeState.Explored => "explored",
        SearchNodeState.Pruned => "pruned",
        SearchNodeState.Failed => "failed",
        SearchNodeState.Solution => "solution",
        SearchNodeState.Skipped => "skipped",
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };

    /// <summary>The state a search node is drawn in when nothing else applies.</summary>
    public static PictureState StateOf(SearchNodeState state) => state switch {
        SearchNodeState.Explored or SearchNodeState.Solution => PictureState.Covered,
        SearchNodeState.Pruned or SearchNodeState.Failed => PictureState.Rejected,
        _ => PictureState.Untraveled,
    };

    public static SearchTreePayload Payload(SearchTreeSpec spec, string instance) => new(Kind, spec.Problem, instance, MaxLeaves);

    // ---- The tree as the steps build it ----

    private sealed class Node {
        public required int Id;
        public required string Label;
        public Node? Parent;
        public SearchNodeState State;
        public string? Bound;
        public int? Order;
        public bool Hidden;
        public readonly List<Node> Kids = new();
    }

    /// <summary>The search tree as the patches so far have built it.</summary>
    internal sealed class Tree {
        private readonly Dictionary<int, Node> _nodes = new();
        private readonly List<Node> _roots = new();
        private int _order;
        private Node? _solution;

        /// <summary>How many nodes there are (hidden stand-ins included).</summary>
        public int Count => _nodes.Count;

        /// <summary>How many nodes are cut off without being explored.</summary>
        public int Cut => _nodes.Values.Count(n => n.State == SearchNodeState.Pruned);

        /// <summary>The latest solution node, if any.</summary>
        public int? Solution => _solution?.Id;

        public void Apply(SearchNode patch) {
            if (_nodes.TryGetValue(patch.NodeId, out var node)) {
                if (node.Hidden) {
                    node.Hidden = false;
                    node.Label = patch.Label;
                }
            } else {
                node = new Node { Id = patch.NodeId, Label = patch.Label, State = patch.State };
                _nodes[node.Id] = node;
                if (patch.ParentId is not int parentId) {
                    _roots.Add(node);
                } else {
                    if (!_nodes.TryGetValue(parentId, out var parent)) {
                        // The parent's own step was not kept: stand in for it under the root.
                        parent = new Node { Id = parentId, Label = "…", State = SearchNodeState.Explored, Hidden = true };
                        _nodes[parentId] = parent;
                        if (_roots.Count > 0) Attach(_roots[0], parent);
                        else _roots.Add(parent);
                    }
                    Attach(parent, node);
                }
            }
            SetState(node, patch.State);
            if (patch.Bound != null) node.Bound = patch.Bound;
        }

        private void Attach(Node parent, Node child) {
            child.Parent = parent;
            parent.Kids.Add(child);
            // A queued node that gets a child was taken off the queue and expanded.
            if (parent.State == SearchNodeState.Queued) SetState(parent, SearchNodeState.Explored);
        }

        private void SetState(Node node, SearchNodeState state) {
            if (node.State == SearchNodeState.Queued && state is SearchNodeState.Explored or SearchNodeState.Solution or SearchNodeState.Failed && node.Order == null)
                node.Order = ++_order;
            node.State = state;
            if (state == SearchNodeState.Solution) {
                if (_solution != null && _solution != node && _solution.State == SearchNodeState.Solution) _solution.State = SearchNodeState.Explored;
                _solution = node;
            } else if (_solution == node) {
                _solution = null;
            }
        }

        /// <summary>
        /// The drawn tree: folded past <see cref="MaxLeaves"/>, states as the class summary says.
        /// </summary>
        /// <param name="focus">The node the step is about, or null.</param>
        /// <param name="mark">The focus's state.</param>
        /// <param name="answer">The node whose path from the root is the answer's, or null.</param>
        /// <param name="answerState">The state of that path: Solution once confirmed, Committed while only kept so far.</param>
        public List<SearchTreeNodeDto> Draw(int? focus, PictureState mark, int? answer, PictureState answerState = PictureState.Solution) {
            Node? focusNode = focus is int f && _nodes.TryGetValue(f, out var fn) ? fn : null;
            var path = new HashSet<Node>();
            for (var n = answer is int a && _nodes.TryGetValue(a, out var an) ? an : null; n != null; n = n.Parent) path.Add(n);

            // Nodes that stay in view: the focus, queued nodes, the answer's path, and everything above them.
            var open = new HashSet<Node>(_roots);
            foreach (var n in _nodes.Values)
                if (n == focusNode || n.State == SearchNodeState.Queued || path.Contains(n))
                    for (var up = n; up != null && open.Add(up); up = up.Parent) { }

            // Sizes and leaf counts, bottom-up. With grouping, a node's cut-off leaves count as one.
            var size = new Dictionary<Node, int>();
            var leaves = new Dictionary<Node, int>();
            bool CutLeaf(Node k) => k.Kids.Count == 0 && k.State == SearchNodeState.Pruned && k != focusNode;
            List<Node> Cuts(Node n, bool group) {
                if (!group) return [];
                var cuts = n.Kids.Where(CutLeaf).ToList();
                return cuts.Count > 1 ? cuts : [];
            }
            void Measure(Node n, bool group) {
                int s = 1, l = 0;
                var cuts = Cuts(n, group);
                if (cuts.Count > 0) l++;
                foreach (var k in n.Kids) {
                    Measure(k, group);
                    s += size[k];
                    if (!cuts.Contains(k)) l += leaves[k];
                }
                size[n] = s;
                leaves[n] = n.Kids.Count == 0 ? 1 : l;
            }
            foreach (var r in _roots) Measure(r, false);
            bool group = _roots.Sum(r => leaves[r]) > MaxLeaves;
            if (group) foreach (var r in _roots) Measure(r, true);
            int total = _roots.Sum(r => leaves[r]);

            // Fold finished subtrees, most leaves first (an ancestor always has at least its descendants' leaves, so it comes
            // first and its descendants are then skipped), until the tree is narrow enough.
            var folded = new HashSet<Node>();
            if (total > MaxLeaves) {
                var candidates = _nodes.Values.Where(n => !open.Contains(n) && n.Kids.Count > 0 && leaves[n] > 1)
                    .OrderByDescending(n => leaves[n]).ThenByDescending(n => size[n]).ThenBy(n => n.Id);
                foreach (var n in candidates) {
                    if (total <= MaxLeaves) break;
                    bool under = false;
                    for (var up = n.Parent; up != null && !under; up = up.Parent) under = folded.Contains(up);
                    if (under) continue;
                    folded.Add(n);
                    total -= leaves[n] - 1;
                }
            }

            var drawn = new List<SearchTreeNodeDto>();
            var states = new Dictionary<string, PictureState>(StringComparer.Ordinal);
            void Emit(Node n) {
                string id = NodeId(n.Id);
                GraphSubsetFrames.Raise(states, id, n.Hidden ? PictureState.Background : StateOf(n.State));
                if (path.Contains(n)) GraphSubsetFrames.Raise(states, id, answerState);
                if (n == focusNode) GraphSubsetFrames.Raise(states, id, mark);
                drawn.Add(new SearchTreeNodeDto(id, n.Parent is { } p ? NodeId(p.Id) : null, n.Label, n.Hidden ? "hidden" : KindOf(n.State),
                    states[id]) { Bound = n.Bound, Order = n.Order });
                if (folded.Contains(n)) {
                    drawn.Add(new SearchTreeNodeDto(MoreId(n.Id), id, $"+{size[n] - 1} more", "more", PictureState.Background) { Hidden = size[n] - 1 });
                    return;
                }
                var cuts = Cuts(n, group);
                bool placed = false;
                foreach (var k in n.Kids) {
                    if (cuts.Contains(k)) {
                        if (!placed) drawn.Add(new SearchTreeNodeDto(CutId(n.Id), id, $"{cuts.Count} cut", "cut", PictureState.Rejected) { Hidden = cuts.Count });
                        placed = true;
                        continue;
                    }
                    Emit(k);
                }
            }
            foreach (var r in _roots) Emit(r);
            return drawn;
        }
    }

    // ---- Frames ----

    /// <summary>
    /// The frame for one step, after its patches are applied to <paramref name="tree"/>. <paramref name="stopped"/> marks the
    /// Done step of a run stopped by its time limit; <paramref name="endsRun"/> says whether the step is an Accept that ends the
    /// run (<see cref="GraphSubsetFrames.EndsRun"/>), so the path it reaches is the confirmed answer.
    /// </summary>
    internal static SearchTreeFrame Frame(Tree tree, StepEvent ev, IReadOnlyList<SearchNode>? patches, string caption, bool? ok, bool stopped = false, bool endsRun = false) {
        foreach (var p in patches ?? []) tree.Apply(p);
        int? focus = ev != StepEvent.Done && patches is { Count: > 0 } ? patches[^1].NodeId : null;
        int? answer = ev switch {
            StepEvent.Accept when focus is int f && f == tree.Solution => f,
            StepEvent.Done when ok == true && !stopped => tree.Solution,
            _ => null,
        };
        // The path is the answer once confirmed; an Accept the run goes on from only keeps it so far (#886).
        var nodes = tree.Draw(focus, GraphSubsetFrames.FocusMark(ev), answer, ev == StepEvent.Accept && !endsRun ? PictureState.Committed : PictureState.Solution);
        return new SearchTreeFrame(ev, caption, focus is int id ? [NodeId(id)] : [], nodes, tree.Count, tree.Cut) { Ok = ok };
    }

    /// <summary>
    /// The whole response: the payload, a frame per kept step, then a solved frame unless the last step already is the Done
    /// step (the tree so far, ok from the verifier). A run stopped by its time limit ends with the tree so far in a Done frame
    /// with ok = false, and its answer string (<see cref="SolveRun.TimedOutAnswer"/>) is never read.
    /// </summary>
    public static FramesResponse Build<TPartial>(SearchTreeSpec spec, string instance, SolveRun run, Func<string, bool> verifyAnswer) {
        var tree = new Tree();
        var frames = new List<object>();
        SolverStep<TPartial>? last = null;
        foreach (var (step, endsRun, index) in GraphSubsetFrames.DrawnSteps<TPartial>(run)) {
            bool stopped = run.TimedOut && step.Event == StepEvent.Done;
            frames.Add(Frame(tree, step.Event, step.Tree, step.Caption, stopped ? false : step.Ok, stopped, endsRun && !run.TimedOut) with { Step = index });
            last = step;
        }
        if (run.TimedOut) {
            if (last is not { Event: StepEvent.Done }) frames.Add(Frame(tree, StepEvent.Done, null, GraphSubsetFrames.StoppedCaption, false, stopped: true));
        } else if (last is not { Event: StepEvent.Done }) {
            bool ok = verifyAnswer(run.Answer);
            frames.Add(Frame(tree, StepEvent.Done, null, ok ? $"The answer is {run.Answer}." : "No answer was found.", ok));
        }
        return new FramesResponse(nameof(VisualizationType.SearchTree), Payload(spec, instance), frames);
    }
}

/// <summary>
/// A second visualization that draws a backtracking or branch-and-bound solver's run as the Search Tree picture. The class
/// declares only the problem's name (<see cref="Describe"/>); the frames hook then comes for free. Implement members
/// explicitly so nothing new appears in /info. The class must also declare <c>Type? IVisualization.StepShape =&gt;
/// typeof(TPartial)</c>, the shape of the solver's steps; the tree is read from their <see cref="SolverStep{TPartial}.Tree"/>.
/// </summary>
interface ISearchTreeVisualization<TProblem, TPartial> : IFramesVisualization where TProblem : IProblem {
    SearchTreeSpec Describe(TProblem problem);

    /// <summary>
    /// A search tree is the search itself: a run whose steps it cannot draw leaves it nothing but an empty tree, so
    /// <c>visualizeRun</c> lists it as <c>shape_mismatch</c> instead of drawing the answer alone (#895).
    /// </summary>
    bool IFramesVisualization.DrawsAnswerAlone() => false;

    FramesResponse IFramesVisualization.BuildFrames(string instance, SolveRun run) {
        TProblem problem = ParseGuard.CreateProblem<TProblem>(instance);
        return SearchTreeFrames.Build<TPartial>(Describe(problem), instance, run, answer => {
            // The problem's own verifier decides; a certificate it cannot read is not correct.
            try { return problem.defaultVerifier.verify(instance, answer); } catch { return false; }
        });
    }
}
