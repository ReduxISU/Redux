using System.Runtime.CompilerServices;

namespace API.Interfaces.Steps;

/// <summary>
/// Records the steps of a solver that branches and builds their search-tree nodes (<see cref="SolverStep{TPartial}.Tree"/>)
/// from the keywords, so the solver names only what the tree cannot know: a node's label and bound, and which of its own
/// states (<typeparamref name="TState"/>, told apart by reference) a step is about.
/// <para>
/// How a step finds its node: <c>at: state</c> is that state's node; else <c>child: label</c> makes a new node under the
/// state being expanded (the last one given to <see cref="Expand"/> or to <see cref="Try"/> with <c>at</c>); else an Accept
/// or Reject is about the newest such child. The keyword gives the node's state: a Try explores it, a Reject prunes it
/// (<c>state:</c> says otherwise, such as Failed), and an Accept queues it as the state given in <c>queued:</c>, or with no
/// <c>queued</c> makes it the solution. A patch carries this step's bound only: a later step about the node gives its own.
/// </para>
/// <para>
/// <see cref="Root"/> makes node 0, queued, and the next step reported carries it before its own node. A best-first solver
/// calls <see cref="Expand"/> for each state it takes off its queue; it records a Backtrack only when the state is not one
/// of the children queued since the last expansion (the search jumped). <see cref="Done"/> with ok names the answer's path,
/// root first: every node above the last solution explored, then the solution as its Accept left it (past the cap, the steps
/// of its ancestors may not have been kept). A solution that entered the tree already solved (an Accept with <c>child</c>,
/// such as a starting tour) is not on a path the search took, so it is named alone.
/// </para>
/// <para>
/// Costs nothing when the recorder is <see cref="StepRecorder{TPartial}.Off"/>: no node is made and no text is formatted. A
/// <see cref="StepRecorder{TPartial}.DeadlineOnly"/> recorder still counts every step, so it remembers which states were just
/// queued (to tell a Backtrack) and nothing else. Recording, it keeps a node for every state and every child reported, past
/// the cap too, since a later step (or the Done) can name it; captions, bounds, partials and focus are built only for kept
/// steps, as <see cref="StepRecorder{TPartial}"/> builds them.
/// </para>
/// </summary>
sealed class SearchTree<TState, TPartial> : IStepGate, IBacktrackGate where TState : class {
    // A node as the tree keeps it; a patch adds the state and bound of one step.
    private sealed class Node(int id, Node? parent, string label, bool leaf) {
        public int Id { get; } = id;
        public Node? Parent { get; } = parent;
        public string Label { get; } = label;
        // Made already solved by an Accept with a label: the Done names it alone.
        public bool Leaf { get; } = leaf;
    }

    private readonly StepRecorder<TPartial> _rec;
    // Each state's node, by reference; weak, so a state the solver has dropped is not kept alive.
    private readonly ConditionalWeakTable<TState, Node> _nodes = new();
    // The states queued since the last expansion, to tell a Backtrack from going on with a child just made.
    private readonly List<TState> _justQueued = new();
    private int _nextId;
    private Node? _expanding;
    private Node? _newestChild;
    private SearchNode? _pendingRoot;
    // The last solution accepted, and its patch.
    private Node? _answer;
    private SearchNode? _answerPatch;

    public SearchTree(StepRecorder<TPartial> rec) {
        _rec = rec;
    }

    bool IStepGate.Recording => ((IStepGate)_rec).Recording;
    bool IStepGate.ShowsNextStep => ((IStepGate)_rec).ShowsNextStep;
    bool IBacktrackGate.ShowsBacktrackTo(object state) => ((IStepGate)_rec).ShowsNextStep && IsJump((TState)state);

    /// <summary>
    /// Whether the state expanded last queued no child: so going on from another is backing out of a dead end. Read it in the
    /// caption of <see cref="Expand"/>, where it says what the expansion before this one found.
    /// </summary>
    public bool DeadEnd => _justQueued.Count == 0;

    private bool Recording => ((IStepGate)_rec).Recording;

    private bool IsJump(TState state) => !_justQueued.Contains(state);

    /// <summary>The root of the search, node 0, queued. It records no step: the next step reported carries it.</summary>
    public void Root(TState state, [InterpolatedStringHandlerArgument("")] KeptText label, [InterpolatedStringHandlerArgument("")] KeptText bound = default) {
        if (!_rec.CountsSteps) return;
        _justQueued.Add(state);
        if (!Recording) return;
        var root = new Node(_nextId++, null, label.ToStringAndClear()!, leaf: false);
        _nodes.AddOrUpdate(state, root);
        _expanding = root;
        _pendingRoot = Patch(root, SearchNodeState.Queued, bound.ToStringAndClear());
    }

    /// <summary>
    /// The search takes <paramref name="state"/> off its queue to expand it: its children come next. Records a Backtrack to its
    /// node (explored) when it is not one of the children queued since the last expansion; the caption is formatted only then.
    /// </summary>
    public void Expand(TState state, Func<TPartial> partial, [InterpolatedStringHandlerArgument("", "state")] ShownText caption, Func<string[]>? focus = null) {
        if (!_rec.CountsSteps) return;
        bool jump = IsJump(state);
        _justQueued.Clear();
        Node? node = null;
        if (Recording) _expanding = node = NodeOf(state);
        if (jump) Add(StepEvent.Backtrack, partial, caption.ToStringAndClear(), focus, node, SearchNodeState.Explored, null);
    }

    /// <summary>
    /// A Try. With <c>at</c>, the search explores that state's node and expands it (its children come next); with <c>child</c>,
    /// it tries a new node under the state being expanded; with neither, the step has no node of its own.
    /// </summary>
    public void Try(Func<TPartial> partial, [InterpolatedStringHandlerArgument("")] ShownText caption, Func<string[]>? focus = null,
                    TState? at = null, [InterpolatedStringHandlerArgument("")] KeptText child = default) {
        if (!_rec.CountsSteps) return;
        if (at != null) _justQueued.Clear();
        Node? node = null;
        if (Recording) {
            node = at != null ? NodeOf(at) : NewChild(child.ToStringAndClear(), leaf: false);
            if (at != null) _expanding = node;
        }
        Add(StepEvent.Try, partial, caption.ToStringAndClear(), focus, node, SearchNodeState.Explored, null);
    }

    /// <summary>
    /// An Accept: the node (<c>at</c>, a new <c>child</c>, or the newest child) is queued as the state <c>queued</c>, or, with
    /// no <c>queued</c>, is the solution (the best so far, for a branch and bound).
    /// </summary>
    public void Accept(Func<TPartial> partial, [InterpolatedStringHandlerArgument("")] KeptText caption, Func<string[]>? focus = null,
                       TState? at = null, [InterpolatedStringHandlerArgument("")] KeptText child = default, TState? queued = null,
                       [InterpolatedStringHandlerArgument("")] KeptText bound = default) {
        if (!_rec.CountsSteps) return;
        if (queued != null) _justQueued.Add(queued);
        Node? node = null;
        SearchNodeState state = queued != null ? SearchNodeState.Queued : SearchNodeState.Solution;
        string? boundText = bound.ToStringAndClear();
        if (Recording) {
            node = at != null ? NodeOf(at) : NewChild(child.ToStringAndClear(), leaf: queued == null) ?? _newestChild;
            if (queued != null) _nodes.AddOrUpdate(queued, node!);
            else {
                _answer = node;
                _answerPatch = Patch(node!, state, boundText);
            }
        }
        Add(StepEvent.Accept, partial, caption.ToStringAndClear(), focus, node, state, boundText);
    }

    /// <summary>A Reject: the node (<c>at</c>, a new <c>child</c>, or the newest child) is pruned, or takes <c>state</c>.</summary>
    public void Reject(Func<TPartial> partial, [InterpolatedStringHandlerArgument("")] ShownText caption, Func<string[]>? focus = null,
                       TState? at = null, [InterpolatedStringHandlerArgument("")] KeptText child = default,
                       [InterpolatedStringHandlerArgument("")] ShownText bound = default, SearchNodeState state = SearchNodeState.Pruned) {
        if (!_rec.CountsSteps) return;
        Node? node = null;
        if (Recording) node = at != null ? NodeOf(at) : NewChild(child.ToStringAndClear(), leaf: false) ?? _newestChild;
        Add(StepEvent.Reject, partial, caption.ToStringAndClear(), focus, node, state, bound.ToStringAndClear());
    }

    /// <summary>
    /// The final step (<see cref="StepRecorder{TPartial}.Done(TPartial, bool, string, Func{SearchNode[]}?)"/>). With
    /// <paramref name="ok"/>, it names the path to the last solution accepted, root first; else no nodes.
    /// </summary>
    public void Done(TPartial answer, bool ok, [InterpolatedStringHandlerArgument("")] KeptText caption) {
        if (!Recording) return;
        SearchNode[]? path = ok && _answer != null ? AnswerPath() : null;
        _rec.Done(answer, ok, caption.ToStringAndClear()!, path is null ? null : () => path);
    }

    private SearchNode[] AnswerPath() {
        if (_answer!.Leaf) return [_answerPatch!];
        var path = new List<SearchNode> { _answerPatch! };
        for (Node? up = _answer.Parent; up != null; up = up.Parent)
            path.Insert(0, Patch(up, SearchNodeState.Explored, null));
        return [.. path];
    }

    // Records the step, with its node's patch (after the root, the first time) when the recorder builds it.
    private void Add(StepEvent kind, Func<TPartial> partial, string? caption, Func<string[]>? focus, Node? node,
                     SearchNodeState state, string? bound) {
        SearchNode[]? tree = null;
        if (_rec.Builds(kind)) {
            SearchNode? patch = node is null ? null : Patch(node, state, bound);
            if (_pendingRoot is null) tree = patch is null ? null : [patch];
            else if (patch is null || patch.NodeId == _pendingRoot.NodeId) tree = [patch ?? _pendingRoot];
            else tree = [_pendingRoot, patch];
        }
        _pendingRoot = null;
        _rec.Add(kind, partial, caption, focus, tree);
    }

    private Node NodeOf(TState state) =>
        _nodes.TryGetValue(state, out var node) ? node : throw new InvalidOperationException("This state has no node: give it to Root, or to an Accept as queued, first.");

    // A new node under the state being expanded, or null when no label was given.
    private Node? NewChild(string? label, bool leaf) {
        if (label is null) return null;
        var node = new Node(_nextId++, _expanding, label, leaf);
        _newestChild = node;
        return node;
    }

    private static SearchNode Patch(Node node, SearchNodeState state, string? bound) =>
        new(node.Id, node.Parent?.Id, node.Label, state) { Bound = string.IsNullOrEmpty(bound) ? null : bound };
}
