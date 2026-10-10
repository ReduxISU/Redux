using System.Text.Json.Serialization;
using API.Interfaces.Steps;

namespace API.Interfaces.Graphs;

/// <summary>Whether a machine is a DFA (at most one move per state and symbol, no ε) or an NFA.</summary>
enum AutomatonKind { Dfa, Nfa }

/// <summary>One transition as the instance writes it: <c>(from, symbol, to)</c>. An ε-transition has the symbol <c>"ε"</c>.</summary>
sealed record AutomatonTransition(string From, string Symbol, string To);

/// <summary>
/// What an automaton problem hands the Automaton picture: the machine (Q, Σ, δ, q₀, F) and the input. No layout, no
/// colors, no ids; the base derives those.
/// </summary>
/// <param name="Kind">DFA or NFA. A DFA with a missing move gets a Garbage state; an NFA gets the path picker.</param>
/// <param name="States">The state names, in instance order.</param>
/// <param name="Alphabet">The input symbols, in instance order.</param>
/// <param name="Transitions">The transitions, in instance order.</param>
/// <param name="Start">The start state.</param>
/// <param name="Accepting">The accept states.</param>
/// <param name="Input">The input string, one symbol per character; <c>""</c> (or <c>"ε"</c>) is the empty input.</param>
sealed record AutomatonSpec(
    AutomatonKind Kind,
    IReadOnlyList<string> States,
    IReadOnlyList<string> Alphabet,
    IReadOnlyList<AutomatonTransition> Transitions,
    string Start,
    IReadOnlyList<string> Accepting,
    string Input);

// --- JSON shapes (explicit names so the contract does not depend on serializer naming options) ---

/// <summary>A state: its id (<c>q:{name}</c>), its label (the name) and its layer (moves from the start state).</summary>
sealed record AutomatonStateDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("layer")] int Layer);

/// <summary>
/// One arrow: every transition from <c>a</c> to <c>b</c>, merged. <c>label</c> joins the symbols with commas (<c>"a,b"</c>);
/// <c>symbols</c> lists them; <c>epsilon</c> is true when every symbol is ε (the arrow is dashed).
/// </summary>
sealed record AutomatonTransitionDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("a")] string A,
    [property: JsonPropertyName("b")] string B,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("symbols")] IReadOnlyList<string> Symbols,
    [property: JsonPropertyName("epsilon")] bool Epsilon);

/// <summary>A (state, symbol) pair with no move: it leads to the Garbage state.</summary>
sealed record AutomatonMissingMove(
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("symbol")] string Symbol);

/// <summary>
/// The DFA's Garbage state, drawn apart from the others with no arrows into it. <c>missing</c> lists the moves that lead
/// there.
/// </summary>
sealed record AutomatonGarbageDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("missing")] IReadOnlyList<AutomatonMissingMove> Missing);

/// <summary>
/// One run of an NFA, for the path picker: its own frames (start, one per move, then a Done frame whose <c>ok</c> says
/// whether this run accepts). <c>outcome</c> is <c>"accepts"</c>, <c>"rejects"</c> (the input is used up in a state that does
/// not accept) or <c>"dies"</c> (no move for the next symbol).
/// </summary>
sealed record AutomatonPathDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("accepted")] bool Accepted,
    [property: JsonPropertyName("outcome")] string Outcome,
    [property: JsonPropertyName("frames")] IReadOnlyList<AutomatonFrame> Frames);

/// <summary>The static part of an Automaton picture, sent once per response.</summary>
sealed record AutomatonPayload(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("layout")] string Layout,
    [property: JsonPropertyName("states")] IReadOnlyList<AutomatonStateDto> States,
    [property: JsonPropertyName("start")] string Start,
    [property: JsonPropertyName("accepting")] IReadOnlyList<string> Accepting,
    [property: JsonPropertyName("alphabet")] IReadOnlyList<string> Alphabet,
    [property: JsonPropertyName("transitions")] IReadOnlyList<AutomatonTransitionDto> Transitions,
    [property: JsonPropertyName("input")] IReadOnlyList<string> Input) {
    /// <summary>The Garbage state of a DFA with a missing move; absent otherwise.</summary>
    [JsonPropertyName("garbage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AutomatonGarbageDto? Garbage { get; init; }

    /// <summary>An NFA's runs, accepting ones first, for the path picker; absent for a DFA (it has one run).</summary>
    [JsonPropertyName("paths")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<AutomatonPathDto>? Paths { get; init; }

    /// <summary>True when the NFA has more runs than <see cref="AutomatonFrames.PathLimit"/> and only the first ones are listed.</summary>
    [JsonPropertyName("pathsTruncated")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? PathsTruncated { get; init; }
}

/// <summary>
/// One picture of the machine. <c>position</c> is how many input symbols have been read (the tape head sits on the next
/// one). <c>active</c> lists the ids of the states the machine is in (the Garbage id when a DFA fell into it).
/// <c>nodes</c> and <c>edges</c> list only states and transitions whose state is not <see cref="PictureState.Background"/>.
/// </summary>
sealed record AutomatonFrame(
    [property: JsonPropertyName("event")] StepEvent Event,
    [property: JsonPropertyName("caption")] string Caption,
    [property: JsonPropertyName("focus")] string[] Focus,
    [property: JsonPropertyName("position")] int Position,
    [property: JsonPropertyName("active")] IReadOnlyList<string> Active,
    [property: JsonPropertyName("nodes")] IReadOnlyDictionary<string, StateRef> Nodes,
    [property: JsonPropertyName("edges")] IReadOnlyDictionary<string, StateRef> Edges) {
    [JsonPropertyName("ok")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Ok { get; init; }
}

/// <summary>
/// Derives the Automaton picture (DFA, NFA) from (the machine, the input, the states one step is in, the step event,
/// focus). Nothing here knows about a particular problem or solver.
/// <para>
/// <b>Ids.</b> State <c>x</c> is <c>q:{x}</c>, with the name written by <see cref="EscapeName"/> (a <c>\</c> before every
/// <c>\</c>, <c>&gt;</c> and <c>:</c> in it). The arrow from <c>x</c> to <c>y</c> is <c>{id of x}&gt;{id of y}</c>
/// (<c>q:1&gt;q:2</c>): every transition between the same two states in the same direction is one arrow, labelled with
/// its symbols joined by commas (<c>a,b</c>). The Garbage state is <c>garbage</c>. A state id never holds a bare
/// <c>&gt;</c>, so an arrow id splits one way only, and no state, arrow or Garbage id can equal another; an id is still a
/// label, never something to split: every arrow gives its ends as <c>a</c> and <c>b</c>.
/// </para>
/// <para>
/// <b>Garbage.</b> A DFA in which some state has no move on some symbol gets a Garbage state (label <c>Garbage</c>, with
/// a <c>'</c> added while a state already has that name), drawn apart with no arrows into it. A DFA solver that falls into
/// it records a step whose partial has no states (<see cref="ActiveStates"/> is empty); the picture shows Garbage as the
/// active state.
/// </para>
/// <para>
/// <b>Steps.</b> A step's partial is <see cref="ActiveStates"/>: the states the machine is in and how many symbols it has
/// read. Its focus names state ids, arrow ids and the Garbage id: the arrow just taken, the state just reached, accepted
/// or stuck.
/// </para>
/// <para>
/// <b>States.</b> The active states are <c>ElementHighlight</c> while the machine runs. In an Accept step an active accept
/// state is <c>Solution</c>. The focus is <c>ElementHighlight</c>, or <c>Rejected</c> in a Reject step
/// (<see cref="GraphSubsetFrames.FocusMark"/>); a focused accept state in an Accept step is <c>Solution</c>. In the Done
/// step the active accept states are <c>Solution</c> when the machine accepts (<c>ok</c> true) and every active state is
/// <c>Rejected</c> when it does not; a state the run never reached on this input is <c>Untraveled</c>. Solution appears
/// only in Accept and Done steps. When several states apply to one element the order is
/// <c>Rejected &gt; ElementHighlight &gt; Solution &gt; Background</c> (<see cref="GraphSubsetFrames.Raise"/>).
/// </para>
/// </summary>
static class AutomatonFrames {
    public const string LayoutName = "layered";
    public const string GarbageId = "garbage";
    public const string Epsilon = "ε";
    /// <summary>The most NFA runs listed for the path picker; enough to pick from, and it keeps a branchy machine fast.</summary>
    public const int PathLimit = 40;

    // ---- Ids ----

    /// <summary><c>q:{name}</c>, the name written with <see cref="EscapeName"/>.</summary>
    public static string StateId(string name) => "q:" + EscapeName(name);

    /// <summary>The id of the arrow from state <paramref name="from"/> to state <paramref name="to"/>: <c>q:{from}&gt;q:{to}</c>.</summary>
    public static string TransitionId(string from, string to) => StateId(from) + ">" + StateId(to);

    /// <summary>
    /// A state name as it is written inside an id: a backslash before every <c>\</c>, <c>&gt;</c> and <c>:</c> (the
    /// separators of this picture's ids). Ordinary names (<c>1</c>, <c>q0</c>) are unchanged.
    /// </summary>
    public static string EscapeName(string name) =>
        name.IndexOfAny(['\\', '>', ':']) < 0 ? name : name.Replace(@"\", @"\\").Replace(">", @"\>").Replace(":", @"\:");

    public static string KindName(AutomatonKind kind) => kind switch {
        AutomatonKind.Dfa => "dfa",
        AutomatonKind.Nfa => "nfa",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>The input as symbols, with <c>"ε"</c> read as the empty input.</summary>
    public static string InputSymbols(string input) => input == Epsilon ? "" : input;

    // ---- The model the payload and the frames read ----

    private sealed class Model {
        public readonly AutomatonSpec Spec;
        public readonly List<string> States = new();
        public readonly HashSet<string> Accepting = new(StringComparer.Ordinal);
        public readonly Dictionary<string, int> Layer = new(StringComparer.Ordinal);
        /// <summary>Merged arrows in first-seen order: (from, to) and their symbols.</summary>
        public readonly List<(string From, string To, List<string> Symbols)> Arrows = new();
        public readonly List<AutomatonMissingMove> Missing = new();
        public readonly string? GarbageLabel;
        public readonly string Input;
        public readonly HashSet<string> NodeIds = new(StringComparer.Ordinal);
        public readonly HashSet<string> EdgeIds = new(StringComparer.Ordinal);
        /// <summary>Every state reached on this input (the union of the active sets of the whole run, ε moves included).</summary>
        public readonly HashSet<string> Reached = new(StringComparer.Ordinal);
        public bool FellIntoGarbage;

        public Model(AutomatonSpec spec) {
            Spec = spec;
            Input = InputSymbols(spec.Input);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var s in spec.States)
                if (seen.Add(s)) States.Add(s);
            // A state named only by a transition, the start or an accept state still gets a node.
            foreach (var s in spec.Transitions.SelectMany(t => new[] { t.From, t.To }).Append(spec.Start).Concat(spec.Accepting))
                if (seen.Add(s)) States.Add(s);
            foreach (var s in spec.Accepting) Accepting.Add(s);

            var index = new Dictionary<(string, string), int>();
            foreach (var t in spec.Transitions) {
                if (!index.TryGetValue((t.From, t.To), out int i)) {
                    i = Arrows.Count;
                    index[(t.From, t.To)] = i;
                    Arrows.Add((t.From, t.To, new List<string>()));
                }
                if (!Arrows[i].Symbols.Contains(t.Symbol)) Arrows[i].Symbols.Add(t.Symbol);
            }

            // Layers: moves from the start state (breadth first, ε included); states it cannot reach go one layer past the last.
            Layer[spec.Start] = 0;
            var queue = new Queue<string>([spec.Start]);
            while (queue.Count > 0) {
                string s = queue.Dequeue();
                foreach (var (from, to, _) in Arrows)
                    if (from == s && !Layer.ContainsKey(to)) {
                        Layer[to] = Layer[s] + 1;
                        queue.Enqueue(to);
                    }
            }
            int beyond = Layer.Values.Max() + 1;
            foreach (var s in States) Layer.TryAdd(s, beyond);

            if (spec.Kind == AutomatonKind.Dfa) {
                foreach (var s in States)
                    foreach (var a in spec.Alphabet)
                        if (!spec.Transitions.Any(t => t.From == s && t.Symbol == a)) Missing.Add(new AutomatonMissingMove(StateId(s), a));
                if (Missing.Count > 0) {
                    string label = "Garbage";
                    while (States.Contains(label)) label += "'";
                    GarbageLabel = label;
                    NodeIds.Add(GarbageId);
                }
            }
            foreach (var s in States) NodeIds.Add(StateId(s));
            foreach (var (from, to, _) in Arrows) EdgeIds.Add(TransitionId(from, to));

            // The states reached on this input, step by step as the whole machine runs it.
            var active = Closure([spec.Start]);
            Reached.UnionWith(active);
            foreach (char c in Input) {
                var next = Closure(active.SelectMany(s => Moves(s, c.ToString())));
                if (next.Count == 0) {
                    // A missing move falls into Garbage; a symbol outside the alphabet is not a move at all.
                    FellIntoGarbage = GarbageLabel != null && Spec.Alphabet.Contains(c.ToString());
                    break;
                }
                Reached.UnionWith(next);
                active = next;
            }
        }

        public IEnumerable<string> Moves(string state, string symbol) =>
            Spec.Transitions.Where(t => t.From == state && t.Symbol == symbol).Select(t => t.To);

        /// <summary>The states reachable from <paramref name="from"/> by ε moves alone, the states themselves included.</summary>
        public HashSet<string> Closure(IEnumerable<string> from) {
            var result = new HashSet<string>(from, StringComparer.Ordinal);
            var stack = new Stack<string>(result);
            while (stack.Count > 0)
                foreach (var to in Moves(stack.Pop(), Epsilon))
                    if (result.Add(to)) stack.Push(to);
            return result;
        }

        /// <summary>The ids of <paramref name="states"/>, in state order; an empty set of states in a DFA with Garbage is Garbage.</summary>
        public List<string> ActiveIds(IEnumerable<string> states, bool garbageWhenEmpty) {
            var set = new HashSet<string>(states, StringComparer.Ordinal);
            if (set.Count == 0 && garbageWhenEmpty && GarbageLabel != null) return [GarbageId];
            return States.Where(set.Contains).Select(StateId).ToList();
        }
    }

    // ---- The payload ----

    public static AutomatonPayload Payload(AutomatonSpec spec) => Payload(new Model(spec));

    private static AutomatonPayload Payload(Model m) {
        bool nfa = m.Spec.Kind == AutomatonKind.Nfa;
        var (paths, truncated) = nfa ? Paths(m) : (null, false);
        return new AutomatonPayload(KindName(m.Spec.Kind), LayoutName,
            m.States.Select(s => new AutomatonStateDto(StateId(s), s, m.Layer[s])).ToList(),
            StateId(m.Spec.Start),
            m.States.Where(m.Accepting.Contains).Select(StateId).ToList(),
            m.Spec.Alphabet.Distinct(StringComparer.Ordinal).ToList(),
            m.Arrows.Select(a => new AutomatonTransitionDto(TransitionId(a.From, a.To), StateId(a.From), StateId(a.To),
                string.Join(",", a.Symbols), a.Symbols, a.Symbols.All(x => x == Epsilon))).ToList(),
            m.Input.Select(c => c.ToString()).ToList()) {
            Garbage = m.GarbageLabel is { } label ? new AutomatonGarbageDto(GarbageId, label, m.Missing) : null,
            Paths = paths,
            PathsTruncated = nfa ? truncated : null,
        };
    }

    // ---- Frames ----

    /// <summary>
    /// The frame for one step: the machine is in <paramref name="states"/> (state names; none means Garbage in a DFA that
    /// has one) after reading <paramref name="position"/> symbols.
    /// </summary>
    public static AutomatonFrame Frame(AutomatonSpec spec, IEnumerable<string> states, int position, StepEvent ev, string[] focus, string caption, bool? ok = null) =>
        Frame(new Model(spec), states, position, ev, focus, caption, ok, null);

    private static AutomatonFrame Frame(Model m, IEnumerable<string> states, int position, StepEvent ev, string[] focus, string caption, bool? ok, ISet<string>? reached) {
        var stateList = states.ToList();
        var active = m.ActiveIds(stateList, garbageWhenEmpty: m.Spec.Kind == AutomatonKind.Dfa);
        var nodes = new Dictionary<string, PictureState>(StringComparer.Ordinal);
        var edges = new Dictionary<string, PictureState>(StringComparer.Ordinal);
        var acceptIds = new HashSet<string>(m.Accepting.Select(StateId), StringComparer.Ordinal);

        if (ev == StepEvent.Done) {
            foreach (var id in active) {
                if (ok == true) {
                    if (acceptIds.Contains(id)) GraphSubsetFrames.Raise(nodes, id, PictureState.Solution);
                } else {
                    GraphSubsetFrames.Raise(nodes, id, PictureState.Rejected);
                }
            }
            reached ??= m.Reached;
            foreach (var s in m.States)
                if (!reached.Contains(s)) GraphSubsetFrames.Raise(nodes, StateId(s), PictureState.Untraveled);
        } else {
            foreach (var id in active)
                GraphSubsetFrames.Raise(nodes, id, ev == StepEvent.Accept && acceptIds.Contains(id) ? PictureState.Solution : PictureState.ElementHighlight);
        }

        var mark = GraphSubsetFrames.FocusMark(ev);
        foreach (var id in focus) {
            if (m.NodeIds.Contains(id))
                GraphSubsetFrames.Raise(nodes, id, ev == StepEvent.Accept && acceptIds.Contains(id) ? PictureState.Solution : mark);
            else if (m.EdgeIds.Contains(id))
                GraphSubsetFrames.Raise(edges, id, mark);
        }

        return new AutomatonFrame(ev, caption, focus, position, active, GraphSubsetFrames.ToRefs(nodes), GraphSubsetFrames.ToRefs(edges)) { Ok = ok };
    }

    /// <summary>
    /// The frame that closes a run stopped by its time limit (<see cref="SolveRun.TimedOut"/>): a Done frame with ok = false
    /// in which nothing is active and every element is Background (the machine was not judged, so nothing is Rejected or
    /// Untraveled). <paramref name="position"/> keeps how far the solver had read. See <see cref="GraphSubsetFrames.StoppedFrame"/>.
    /// </summary>
    public static AutomatonFrame StoppedFrame(string caption, int position = 0) =>
        new(StepEvent.Done, caption, [], position, [], new SortedDictionary<string, StateRef>(StringComparer.Ordinal),
            new SortedDictionary<string, StateRef>(StringComparer.Ordinal)) { Ok = false };

    // ---- NFA runs, for the path picker ----

    private sealed record Move(string From, string Symbol, string To);

    /// <summary>
    /// The NFA's runs, read the way <c>NFASolver</c> reads them: a run that has used up the input in an accept state accepts
    /// (and may go on by ε moves into another run); a run with no move left that does not accept is rejected. No run
    /// visits the same state twice at the same input position. At most <see cref="PathLimit"/> runs, accepting ones first.
    /// </summary>
    private static (List<AutomatonPathDto> Paths, bool Truncated) Paths(Model m) {
        string input = m.Input;
        var found = new List<(List<Move> Moves, string End, int Pos, bool Accepted)>();
        bool truncated = false;
        var moves = new List<Move>();
        var seen = new HashSet<(string, int)> { (m.Spec.Start, 0) };

        void Walk(string state, int pos) {
            if (truncated) return;
            bool accepting = pos >= input.Length && m.Accepting.Contains(state);
            if (accepting) Record(state, pos, true);
            bool moved = false;
            var options = m.Spec.Transitions.Where(t => t.From == state && t.Symbol == Epsilon).Select(t => (t, pos))
                .Concat(pos < input.Length
                    ? m.Spec.Transitions.Where(t => t.From == state && t.Symbol == input[pos].ToString()).Select(t => (t, pos + 1))
                    : []);
            foreach (var (t, next) in options.ToList()) {
                if (truncated) return;
                if (!seen.Add((t.To, next))) continue;
                moved = true;
                moves.Add(new Move(t.From, t.Symbol, t.To));
                Walk(t.To, next);
                moves.RemoveAt(moves.Count - 1);
                seen.Remove((t.To, next));
            }
            if (!moved && !accepting) Record(state, pos, false);
        }

        void Record(string end, int pos, bool accepted) {
            if (found.Count >= PathLimit) {
                truncated = true;
                return;
            }
            found.Add((moves.ToList(), end, pos, accepted));
        }

        Walk(m.Spec.Start, 0);
        var ordered = found.Where(f => f.Accepted).Concat(found.Where(f => !f.Accepted)).ToList();
        var result = new List<AutomatonPathDto>();
        for (int i = 0; i < ordered.Count; i++) {
            var (pathMoves, end, pos, accepted) = ordered[i];
            bool finished = pos >= input.Length;
            string outcome = accepted ? "accepts" : finished ? "rejects" : "dies";
            string verdict = accepted ? "accepts" : finished ? "rejects" : $"dies at symbol {pos + 1}";
            string chain = string.Join(" ", new[] { m.Spec.Start }.Concat(pathMoves.Select(mv => (mv.Symbol == Epsilon ? "⇢ " : "→ ") + mv.To)));
            result.Add(new AutomatonPathDto($"p{i + 1}", $"Path {i + 1}: {chain}, {verdict}", accepted, outcome,
                PathFrames(m, pathMoves, end, pos, accepted)));
        }
        return (result, truncated);
    }

    private static List<AutomatonFrame> PathFrames(Model m, List<Move> moves, string end, int endPos, bool accepted) {
        var reached = new HashSet<string>(StringComparer.Ordinal) { m.Spec.Start };
        var frames = new List<AutomatonFrame> {
            Frame(m, [m.Spec.Start], 0, StepEvent.Try, [StateId(m.Spec.Start)], $"Start in {m.Spec.Start}.", null, null),
        };
        int pos = 0;
        foreach (var mv in moves) {
            reached.Add(mv.To);
            string caption;
            if (mv.Symbol == Epsilon) {
                caption = $"Take an ε-move from {mv.From} to {mv.To}.";
            } else {
                pos++;
                caption = $"Read '{mv.Symbol}': move from {mv.From} to {mv.To}.";
            }
            frames.Add(Frame(m, [mv.To], pos, StepEvent.Try, [TransitionId(mv.From, mv.To)], caption, null, null));
        }
        string done = accepted ? $"This path ends in {end}, an accept state. Accepted."
            : endPos >= m.Input.Length ? $"This path ends in {end}, which is not an accept state. Rejected."
            : $"No '{m.Input[endPos]}' move from {end}, so this path dies. Rejected.";
        frames.Add(Frame(m, [end], endPos, StepEvent.Done, [], done, accepted, reached));
        return frames;
    }

    // ---- The whole response ----

    /// <summary>
    /// The frame that closes a run whose steps cannot be drawn (they are of another shape, or have no Done step): the whole
    /// machine run on the input, accepted when an accept state is active at the end.
    /// </summary>
    private static AutomatonFrame SimulatedDone(Model m) {
        var active = m.Closure([m.Spec.Start]);
        int read = 0;
        foreach (char c in m.Input) {
            var next = m.Closure(active.SelectMany(s => m.Moves(s, c.ToString())));
            // Stuck: a DFA's missing move leaves no state (Garbage); a symbol outside the alphabet leaves the machine where it is.
            if (next.Count == 0) {
                if (m.FellIntoGarbage) active = next;
                break;
            }
            active = next;
            read++;
        }
        bool accepted = read == m.Input.Length && active.Any(m.Accepting.Contains);
        string caption = accepted ? "The machine accepts the input."
            : m.FellIntoGarbage ? "The machine falls into Garbage, so it rejects the input."
            : "The machine rejects the input.";
        return Frame(m, active, read, StepEvent.Done, [], caption, accepted, null);
    }

    /// <summary>
    /// The whole response: the payload, a frame per kept step, then a Done frame (the machine run on the input) unless the
    /// last step already is the Done step. A run stopped by its time limit ends with <see cref="StoppedFrame"/> instead, and
    /// its answer string (<see cref="SolveRun.TimedOutAnswer"/>) is never read.
    /// </summary>
    public static FramesResponse Build(AutomatonSpec spec, SolveRun run) {
        var m = new Model(spec);
        var frames = new List<object>();
        SolverStep<ActiveStates>? last = null;
        foreach (var step in run.StepsFor(typeof(ActiveStates)).OfType<SolverStep<ActiveStates>>()) {
            frames.Add(run.TimedOut && step.Event == StepEvent.Done
                ? StoppedFrame(step.Caption, step.Partial?.Position ?? 0)
                : Frame(m, step.Partial!.States, step.Partial.Position, step.Event, step.Focus, step.Caption, step.Ok, null));
            last = step;
        }
        if (run.TimedOut) {
            if (last is not { Event: StepEvent.Done }) frames.Add(StoppedFrame(GraphSubsetFrames.StoppedCaption, last?.Partial?.Position ?? 0));
        } else if (last is not { Event: StepEvent.Done }) {
            frames.Add(SimulatedDone(m));
        }
        return new FramesResponse(nameof(VisualizationType.Automaton), Payload(m), frames);
    }

    // ---- Captions, for solvers to call inside a recorder lambda so they cost nothing when recording is off ----

    /// <summary>"{1, 2}" for captions.</summary>
    public static string Braces(IEnumerable<string> states) => "{" + string.Join(", ", states) + "}";

    /// <summary>"4", "4 and 5", "3, 4 and 5".</summary>
    public static string List(IReadOnlyList<string> names) =>
        names.Count <= 1 ? string.Join("", names) : string.Join(", ", names.Take(names.Count - 1)) + " and " + names[^1];
}

/// <summary>
/// A visualization for an automaton problem drawn as the Automaton picture (DFA, NFA). The visualization declares only
/// what the problem is (<see cref="Describe"/>); the frames hook then comes for free. Implement members explicitly so
/// nothing new appears in /info. The class must also declare <c>Type? IVisualization.StepShape =&gt; typeof(ActiveStates)</c>.
/// </summary>
interface IAutomatonVisualization<TProblem> : IFramesVisualization where TProblem : IProblem {
    AutomatonSpec Describe(TProblem problem);

    FramesResponse IFramesVisualization.BuildFrames(string instance, SolveRun run) =>
        AutomatonFrames.Build(Describe(ParseGuard.CreateProblem<TProblem>(instance)), run);
}
