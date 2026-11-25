using System.Runtime.CompilerServices;

namespace API.Interfaces.Steps;

/// <summary>
/// Records the steps of a solver that branches, and builds their search-tree nodes (<see cref="SolverStep{TPartial}.Tree"/>)
/// from them. The solver says, once, how to show its states: the converters given to the constructor turn a state, or a
/// state and a choice made from it, into the partial answer, the focus, a child's label and the node's note. Each step is then
/// one call that names the event, what it is about, and why: <c>tree.Reject(state, value, $"x2 = False fails: ...")</c>.
/// <para>
/// A step is about a state (<typeparamref name="TState"/>, told apart by reference) or about a choice from one
/// (<typeparamref name="TChoice"/>: a value, the next city). A state's step is about its node; a choice's step is about the
/// child the choice makes under its state, the one the last step about the same choice made if there is one, else a new one.
/// The event gives the node's state: a Try explores it, a Reject prunes it (<c>outcome</c> says otherwise, such as Failed), and
/// an Accept queues it as the state given in <c>queued</c>, or makes it the solution. A node's note is this step's alone:
/// given as <c>note</c>, or from the converter for the event.
/// </para>
/// <para>
/// <see cref="Root"/> makes node 0, queued, and the next step reported carries it before its own node; a step about the root
/// while it is still waiting shows it as Root made it. A best-first solver calls <see cref="Expand"/> for each state it takes
/// off its queue: it records a Backtrack only when the state is not one of the children queued since the last expansion.
/// <see cref="Done"/> with ok names the answer's path, root first: every node above the last solution explored, then the
/// solution as its Accept left it (past the cap, the steps of its ancestors may not have been kept). A starting answer
/// (<see cref="Accept(KeptText, KeptText, Func{TPartial}, Func{string[]}, KeptText)"/>) was not reached by the search, so it
/// is named alone.
/// </para>
/// <para>
/// Costs nothing when the recorder is <see cref="StepRecorder{TPartial}.Off"/>: no converter is called, no text formatted, no
/// node made. A <see cref="StepRecorder{TPartial}.DeadlineOnly"/> recorder still counts every step, so the tree remembers which
/// states were just queued (to tell a Backtrack) and nothing else. Recording, it keeps a node for every state and choice
/// reported, past the cap too, since a later step (or the Done) can name it; it calls the partial, focus and note converters
/// and formats the text only for steps the recorder keeps, as <see cref="StepRecorder{TPartial}"/> builds its lambdas.
/// </para>
/// </summary>
sealed class SearchSteps<TState, TChoice, TPartial> : StepsCore<TPartial>, IBacktrackGate where TState : class {
    // A node as the tree keeps it; a patch adds the state and note of one step.
    private sealed class Node(int id, Node? parent, string label, bool leaf) {
        public int Id { get; } = id;
        public Node? Parent { get; } = parent;
        public string Label { get; } = label;
        // A starting answer: the Done names it alone.
        public bool Leaf { get; } = leaf;
    }

    private readonly Func<TState, TPartial> _partial;
    private readonly Func<TState, string[]> _focus;
    private readonly Func<TState, TChoice, TPartial> _childPartial;
    private readonly Func<TState, TChoice, string> _childLabel;
    private readonly Func<TState, TChoice, string[]>? _childFocus;
    private readonly Func<TState, TChoice, string[]>? _rejectFocus;
    private readonly Func<TState, TChoice, string?>? _rejectNote;
    private readonly Func<TState, string?>? _queuedNote;
    private readonly Func<TState, string?>? _solvedNote;

    // Each state's node, by reference; weak, so a state the solver has dropped is not kept alive.
    private readonly ConditionalWeakTable<TState, Node> _nodes = new();
    // The states queued since the last expansion, to tell a Backtrack from going on with a child just made.
    private readonly List<TState> _justQueued = new();
    private int _nextId;
    private Node? _root;
    private SearchNode? _pendingRoot;
    // The child the last choice made, and the choice, so the next step about the same choice is about it too.
    private Node? _newestChild;
    private TState? _newestFrom;
    private TChoice _newestChoice = default!;
    // The last solution accepted, and its patch.
    private Node? _answer;
    private SearchNode? _answerPatch;

    /// <param name="rec">The recorder the steps go to.</param>
    /// <param name="partial">A state's partial answer.</param>
    /// <param name="focus">The ids a step about a state is about.</param>
    /// <param name="childPartial">The partial answer once a choice is made from a state.</param>
    /// <param name="childLabel">The label of the child a choice makes (<c>x2=F</c>, <c>Denver</c>).</param>
    /// <param name="childFocus">The ids a step about a choice is about; by default, its state's.</param>
    /// <param name="rejectFocus">The ids a Reject of a choice is about (the clause it falsifies, say); by default, the choice's.</param>
    /// <param name="rejectNote">A rejected choice's note (<c>C2 false</c>); none by default.</param>
    /// <param name="queuedNote">A queued state's note (<c>2 clauses left</c>, <c>lb 4,525</c>); none by default.</param>
    /// <param name="solvedNote">A solution's note, from the state it reaches (<c>x3 free</c>); none by default.</param>
    public SearchSteps(StepRecorder<TPartial> rec,
                      Func<TState, TPartial> partial,
                      Func<TState, string[]> focus,
                      Func<TState, TChoice, TPartial> childPartial,
                      Func<TState, TChoice, string> childLabel,
                      Func<TState, TChoice, string[]>? childFocus = null,
                      Func<TState, TChoice, string[]>? rejectFocus = null,
                      Func<TState, TChoice, string?>? rejectNote = null,
                      Func<TState, string?>? queuedNote = null,
                      Func<TState, string?>? solvedNote = null) : base(rec) {
        _partial = partial;
        _focus = focus;
        _childPartial = childPartial;
        _childLabel = childLabel;
        _childFocus = childFocus;
        _rejectFocus = rejectFocus;
        _rejectNote = rejectNote;
        _queuedNote = queuedNote;
        _solvedNote = solvedNote;
    }

    bool IBacktrackGate.ShowsBacktrackTo(object state) => ((IStepGate)Rec).ShowsNextStep && IsJump((TState)state);

    /// <summary>
    /// Whether the state expanded last queued no child: so going on from another is backing out of a dead end. Read it in the
    /// caption of <see cref="Expand"/>, where it says what the expansion before this one found.
    /// </summary>
    public bool DeadEnd => _justQueued.Count == 0;

    private bool IsJump(TState state) => !_justQueued.Contains(state);

    /// <summary>The root of the search, node 0, queued. It records no step: the next step reported carries it.</summary>
    public void Root(TState state, [InterpolatedStringHandlerArgument("")] KeptText label, [InterpolatedStringHandlerArgument("")] KeptText note = default) {
        if (!Rec.CountsSteps) return;
        _justQueued.Add(state);
        if (!Recording) return;
        _root = new Node(_nextId++, null, label.ToStringAndClear()!, leaf: false);
        _nodes.AddOrUpdate(state, _root);
        _pendingRoot = Patch(_root, SearchNodeState.Queued, note.ToStringAndClear());
    }

    /// <summary>
    /// The search takes <paramref name="state"/> off its queue to expand it. Records a Backtrack to its node (explored) when it is
    /// not one of the children queued since the last expansion; the caption is formatted only then.
    /// </summary>
    public void Expand(TState state, [InterpolatedStringHandlerArgument("", "state")] ShownText why) {
        if (!Rec.CountsSteps) return;
        bool jump = IsJump(state);
        _justQueued.Clear();
        if (jump) StateStep(StepEvent.Backtrack, state, why.ToStringAndClear(), SearchNodeState.Explored, null, null, null);
    }

    /// <summary>A Try of a state: the search explores its node.</summary>
    public void Try(TState state, [InterpolatedStringHandlerArgument("")] ShownText why) {
        if (!Rec.CountsSteps) return;
        _justQueued.Clear();
        StateStep(StepEvent.Try, state, why.ToStringAndClear(), SearchNodeState.Explored, null, null, null);
    }

    /// <summary>A Try of a choice: a new child under <paramref name="from"/>, explored.</summary>
    public void Try(TState from, TChoice choice, [InterpolatedStringHandlerArgument("")] ShownText why) {
        if (!Rec.CountsSteps) return;
        ChoiceStep(StepEvent.Try, from, choice, why.ToStringAndClear(), SearchNodeState.Explored, null, newChild: true);
    }

    /// <summary>
    /// An Accept of a state: its node is the solution (the best so far, for a branch and bound). <paramref name="partial"/> and
    /// <paramref name="focus"/> replace the converters for a step that shows the state another way (a closed tour).
    /// </summary>
    public void Accept(TState state, [InterpolatedStringHandlerArgument("")] KeptText why, [InterpolatedStringHandlerArgument("")] KeptText note = default,
                       Func<TPartial>? partial = null, Func<string[]>? focus = null) {
        if (!Rec.CountsSteps) return;
        StateStep(StepEvent.Accept, state, why.ToStringAndClear(), SearchNodeState.Solution, note.ToStringAndClear(), partial, focus);
    }

    /// <summary>
    /// An Accept of a choice: its child is queued as the state <paramref name="queued"/>, or is the solution, reaching the state
    /// <paramref name="solved"/> (for the solved note). The note is <paramref name="note"/>, else the queued or solved note.
    /// </summary>
    public void Accept(TState from, TChoice choice, [InterpolatedStringHandlerArgument("")] KeptText why, TState? queued = null, TState? solved = null,
                       [InterpolatedStringHandlerArgument("")] KeptText note = default) {
        if (!Rec.CountsSteps) return;
        if (queued != null) _justQueued.Add(queued);
        string? text = note.ToStringAndClear();
        if (Recording && text is null)
            text = queued != null ? _queuedNote?.Invoke(queued) : solved != null ? _solvedNote?.Invoke(solved) : null;
        Node? node = ChoiceStep(StepEvent.Accept, from, choice, why.ToStringAndClear(), queued != null ? SearchNodeState.Queued : SearchNodeState.Solution, text, newChild: false);
        if (queued != null && node != null) _nodes.AddOrUpdate(queued, node);
    }

    /// <summary>
    /// An Accept of a starting answer that is no state of the search (a greedy tour): a leaf under the root, the solution so
    /// far, labelled <paramref name="leaf"/>. It has no state to convert, so it says its own partial and focus.
    /// </summary>
    public void Accept([InterpolatedStringHandlerArgument("")] KeptText why, [InterpolatedStringHandlerArgument("")] KeptText leaf,
                       Func<TPartial> partial, Func<string[]> focus, [InterpolatedStringHandlerArgument("")] KeptText note = default) {
        if (!Rec.CountsSteps) return;
        Node? node = null;
        string? text = note.ToStringAndClear();
        if (Recording) {
            node = new Node(_nextId++, _root, leaf.ToStringAndClear()!, leaf: true);
            SetAnswer(node, text);
        }
        if (Rec.Builds(StepEvent.Accept)) Build(StepEvent.Accept, why.ToStringAndClear(), node, SearchNodeState.Solution, text, partial(), focus());
        else Skip(StepEvent.Accept);
    }

    /// <summary>
    /// A Reject of a state: its node is pruned, or takes <paramref name="outcome"/>. <paramref name="partial"/> and
    /// <paramref name="focus"/> replace the converters for a step that shows the state another way (a closed tour).
    /// </summary>
    public void Reject(TState state, [InterpolatedStringHandlerArgument("")] ShownText why, [InterpolatedStringHandlerArgument("")] ShownText note = default,
                       SearchNodeState outcome = SearchNodeState.Pruned, Func<TPartial>? partial = null, Func<string[]>? focus = null) {
        if (!Rec.CountsSteps) return;
        StateStep(StepEvent.Reject, state, why.ToStringAndClear(), outcome, note.ToStringAndClear(), partial, focus);
    }

    /// <summary>A Reject of a choice: its child is pruned. The note is <paramref name="note"/>, else the reject note.</summary>
    public void Reject(TState from, TChoice choice, [InterpolatedStringHandlerArgument("")] ShownText why, [InterpolatedStringHandlerArgument("")] ShownText note = default) {
        if (!Rec.CountsSteps) return;
        string? text = note.ToStringAndClear();
        if (text is null && _rejectNote != null && Rec.Builds(StepEvent.Reject)) text = _rejectNote(from, choice);
        ChoiceStep(StepEvent.Reject, from, choice, why.ToStringAndClear(), SearchNodeState.Pruned, text, newChild: false);
    }

    /// <summary>
    /// The final step (<see cref="StepRecorder{TPartial}.Done(TPartial, bool, string, Func{SearchNode[]}?)"/>). With
    /// <paramref name="ok"/>, it names the path to the last solution accepted, root first; else no nodes.
    /// </summary>
    public void Done(TPartial answer, bool ok, [InterpolatedStringHandlerArgument("")] KeptText why) {
        if (!Recording) return;
        SearchNode[]? path = ok && _answer != null ? AnswerPath() : null;
        Rec.Done(answer, ok, why.ToStringAndClear()!, path is null ? null : () => path);
    }

    // A step about a state's node.
    private void StateStep(StepEvent kind, TState state, string? why, SearchNodeState nodeState, string? note,
                           Func<TPartial>? partial, Func<string[]>? focus) {
        Node? node = null;
        if (Recording) {
            node = NodeOf(state);
            if (nodeState == SearchNodeState.Solution) SetAnswer(node, note);
        }
        if (Rec.Builds(kind)) Build(kind, why, node, nodeState, note, partial != null ? partial() : _partial(state), focus != null ? focus() : _focus(state));
        else Skip(kind);
    }

    // A step about the child a choice makes: a new one for a Try, else the one the last step about the same choice made.
    private Node? ChoiceStep(StepEvent kind, TState from, TChoice choice, string? why, SearchNodeState nodeState, string? note, bool newChild) {
        Node? node = null;
        if (Recording) {
            bool same = !newChild && _newestChild != null && ReferenceEquals(_newestFrom, from) && EqualityComparer<TChoice>.Default.Equals(_newestChoice, choice);
            if (same) node = _newestChild!;
            else {
                node = new Node(_nextId++, NodeOf(from), _childLabel(from, choice), leaf: false);
                (_newestChild, _newestFrom, _newestChoice) = (node, from, choice);
            }
            if (nodeState == SearchNodeState.Solution) SetAnswer(node, note);
        }
        if (Rec.Builds(kind)) {
            var focus = kind == StepEvent.Reject && _rejectFocus != null ? _rejectFocus(from, choice)
                : _childFocus != null ? _childFocus(from, choice)
                : _focus(from);
            Build(kind, why, node, nodeState, note, _childPartial(from, choice), focus);
        } else {
            Skip(kind);
        }
        return node;
    }

    private void SetAnswer(Node node, string? note) {
        _answer = node;
        _answerPatch = Patch(node, SearchNodeState.Solution, note);
    }

    // Hands a built step to the recorder, with its node's patch (after the root, while it waits).
    private void Build(StepEvent kind, string? why, Node? node, SearchNodeState nodeState, string? note, TPartial partial, string[] focus) {
        SearchNode? patch = node is null ? null : Patch(node, nodeState, note);
        SearchNode[]? tree;
        if (_pendingRoot is null) tree = patch is null ? null : [patch];
        else if (patch is null || patch.NodeId == _pendingRoot.NodeId) tree = [_pendingRoot];
        else tree = [_pendingRoot, patch];
        _pendingRoot = null;
        Add(kind, partial, why, focus, tree);
    }

    // A step the recorder will not build takes the waiting root with it, as a built one would.
    protected override void Skip(StepEvent kind) {
        _pendingRoot = null;
        base.Skip(kind);
    }

    private SearchNode[] AnswerPath() {
        if (_answer!.Leaf) return [_answerPatch!];
        var path = new List<SearchNode> { _answerPatch! };
        for (Node? up = _answer.Parent; up != null; up = up.Parent)
            path.Insert(0, Patch(up, SearchNodeState.Explored, null));
        return [.. path];
    }

    private Node NodeOf(TState state) =>
        _nodes.TryGetValue(state, out var node) ? node : throw new InvalidOperationException("This state has no node: give it to Root, or to an Accept as queued, first.");

    private static SearchNode Patch(Node node, SearchNodeState state, string? note) =>
        new(node.Id, node.Parent?.Id, node.Label, state) { Bound = string.IsNullOrEmpty(note) ? null : note };
}
