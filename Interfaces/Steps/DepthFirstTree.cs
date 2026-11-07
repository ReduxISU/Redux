namespace API.Interfaces.Steps;

/// <summary>
/// The search tree of a depth-first search that records its steps through <see cref="RunSteps{TState, TPartial}"/>, for the
/// Search Tree picture. The search goes down one branch at a time, so the tree only needs to know the node at each depth of
/// the branch it is on: the root ("start") at depth 0, the first choice at depth 1, the choice made under it at depth 2, and
/// so on. A node's parent is the node one depth up.
/// <para>
/// How a search uses it:
/// </para>
/// <list type="number">
/// <item>Make one, sized by the deepest node the search can reach.</item>
/// <item>Call <see cref="Branch"/> each time the search makes a choice (or finds a dead end) at a depth: that is a new node,
/// a child of the node one depth up.</item>
/// <item>In the <c>tree:</c> converter given to RunSteps, return <see cref="Step"/> for the node the step is about. The root
/// goes out with the first step kept.</item>
/// <item>A search that never records taking a choice back (it just tries the next one) tells <see cref="Failed"/> about a
/// choice that led nowhere, so the next step kept marks it failed.</item>
/// <item>The Done names <see cref="AnswerPath"/>, or <see cref="NoAnswer"/>.</item>
/// </list>
/// <para>
/// It keeps plain ints (one node id per depth, and the next id), so solving without steps makes nothing for the tree but
/// one array. The nodes themselves are made only in the <c>tree:</c> converter and the Done, which run only for a step that
/// is kept.
/// </para>
/// <para>
/// A search that carries counters of its own through the recursion can derive its search object from this class, so the
/// tree and the counters are one object.
/// </para>
/// </summary>
class DepthFirstTree {
    /// <summary>The root, node 0, labelled <c>start</c>.</summary>
    public static readonly SearchNode Root = new(0, null, "start", SearchNodeState.Explored);

    // The node id at each depth of the branch the search is on (the root's, 0, at depth 0), and the id the next node gets.
    private readonly int[] _ids;
    private int _next = 1;
    // Whether a kept step has sent the root yet, and the newest node a kept step has sent. Ids only grow, so every node up to
    // it was sent.
    private bool _rootSent;
    private int _sent;
    // The choices that failed since the last step kept (see Failed), made at the first one, so a solve without steps never
    // makes it.
    private List<SearchNode>? _failed;
    // Each node a kept step cut off (pruned), by id, with the note it was cut with. Made at the first one, so a solve
    // without steps never makes it.
    private Dictionary<int, string?>? _cut;

    /// <param name="deepest">The deepest node the search can reach (the root is at depth 0).</param>
    public DepthFirstTree(int deepest) {
        _ids = new int[deepest + 1];
    }

    /// <summary>A new node at <paramref name="depth"/> (1 or more): a child of the node one depth up.</summary>
    public void Branch(int depth) => _ids[depth] = _next++;

    /// <summary>
    /// The patches a kept step sends, for the node at <paramref name="depth"/> on the current branch (the root, for 0): the root
    /// with the first step kept, then the choices failed since the last step kept, then the node itself, with its label, its
    /// state after the step and a short note (or none).
    /// <para>
    /// A node cut off (pruned) stays cut off: when a later step sends it failed or pruned again (say, when the search backs
    /// out of it), it is sent pruned, with the note it was cut with unless the step gives another.
    /// </para>
    /// </summary>
    public SearchNode[] Step(int depth, string label, SearchNodeState state, string? note = null) {
        var node = Node(depth, label, state, note);
        SearchNode[] patches = [.. _rootSent || depth == 0 ? [] : new[] { Root }, .. _failed ?? [], node];
        _rootSent = true;
        _sent = node.NodeId;
        _failed?.Clear();
        return patches;
    }

    /// <summary>
    /// Whether the node at <paramref name="depth"/> was sent by a kept step. Check it before <see cref="Failed"/>, so the label
    /// is never written when no step was kept (when solving without steps, none is).
    /// </summary>
    public bool Sent(int depth) => _ids[depth] <= _sent;

    /// <summary>
    /// For a search that never records taking a choice back: the choice at <paramref name="depth"/> led nowhere. It is held
    /// back and marked failed by the next step kept, or the Done.
    /// </summary>
    public void Failed(int depth, string label) => (_failed ??= new()).Add(Node(depth, label, SearchNodeState.Failed, null));

    /// <summary>
    /// The Done's patches for an answer: the choices failed since the last step kept, then the answer's path, root first,
    /// through the choice at each depth (<paramref name="labels"/>, from depth 1). The last choice is the solution; with no
    /// choice at all, the root is.
    /// </summary>
    public SearchNode[] AnswerPath(IReadOnlyList<string> labels) {
        var path = new List<SearchNode>(_failed ?? []) { labels.Count == 0 ? Root with { State = SearchNodeState.Solution } : Root };
        for (int d = 1; d <= labels.Count; d++)
            path.Add(Node(d, labels[d - 1], d == labels.Count ? SearchNodeState.Solution : SearchNodeState.Explored, null));
        return [.. path];
    }

    /// <summary>
    /// The Done's patches when there is no answer: the choices failed since the last step kept, and the root, failed (or
    /// still pruned, when a kept step cut the root itself off).
    /// </summary>
    public SearchNode[] NoAnswer() => [.. _failed ?? [], Node(0, "", SearchNodeState.Failed, null)];

    // The node at depth d of the current branch (the root, for 0), in the state the step gives it, unless it was cut off.
    private SearchNode Node(int d, string label, SearchNodeState state, string? note) {
        int id = _ids[d];
        if (state is SearchNodeState.Failed or SearchNodeState.Pruned && _cut != null && _cut.TryGetValue(id, out var cutNote)) {
            state = SearchNodeState.Pruned;
            note ??= cutNote;
        } else if (state == SearchNodeState.Pruned) {
            (_cut ??= new())[id] = note;
        }
        return d == 0 ? Root with { State = state, Bound = note } : new(id, _ids[d - 1], label, state) { Bound = note };
    }
}
