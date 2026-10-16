using System.Globalization;
using System.Text.Json.Serialization;
using API.Interfaces.Steps;

namespace API.Interfaces.Graphs;

/// <summary>One edge as the instance lists it: from, to, and its capacity.</summary>
sealed record FlowEdgeInput(string From, string To, int Capacity);

/// <summary>
/// What a flow problem hands the Flow Network picture: its nodes, its capacitated directed edges, the source and the sink.
/// No layout, no colors, no ids; the base derives those.
/// </summary>
/// <param name="Nodes">Node names, in instance order.</param>
/// <param name="Edges">Edges as listed, parallel edges included (see <see cref="FlowFrames"/> for how they merge).</param>
/// <param name="Source">The source s.</param>
/// <param name="Sink">The sink t.</param>
sealed record FlowSpec(IReadOnlyList<string> Nodes, IReadOnlyList<FlowEdgeInput> Edges, string Source, string Sink);

// --- JSON shapes (explicit names so the contract does not depend on serializer naming options) ---

/// <summary>A node: its id, its label (the name), its layer (column, source first, sink last) and its row in the column.</summary>
sealed record FlowNodeDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("layer")] int Layer,
    [property: JsonPropertyName("row")] int Row);

/// <summary>
/// A directed edge from <c>a</c> to <c>b</c>. <c>capacity</c> is the capacity that counts: the sum of the capacities of
/// the parallel edges it stands for. <c>label</c>, present only when two or more edges were listed from <c>a</c> to
/// <c>b</c>, lists each listed capacity in the order listed (<c>"2,5"</c>): draw <c>label</c> if present, else
/// <c>capacity</c>.
/// </summary>
sealed record FlowEdgeDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("a")] string A,
    [property: JsonPropertyName("b")] string B,
    [property: JsonPropertyName("capacity")] int Capacity) {
    [JsonPropertyName("label")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Label { get; init; }
}

/// <summary>The static part of a Flow Network picture, sent once per response.</summary>
sealed record FlowPayload(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("layout")] string Layout,
    [property: JsonPropertyName("rule")] string Rule,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("sink")] string Sink,
    [property: JsonPropertyName("nodes")] IReadOnlyList<FlowNodeDto> Nodes,
    [property: JsonPropertyName("edges")] IReadOnlyList<FlowEdgeDto> Edges,
    [property: JsonPropertyName("layers")] IReadOnlyList<IReadOnlyList<string>> Layers);

/// <summary>
/// One picture of the network. <c>nodes</c> and <c>edges</c> list only elements whose state is not
/// <see cref="PictureState.Background"/>. <c>flow</c> (every edge id to the units it carries), <c>value</c> (the flow's
/// value) and <c>path</c> (the legs of the augmenting path this step used, empty when none) are in every frame.
/// <c>groups</c> (every node id to 0 on the S side, 1 on the T side) and <c>cut</c> (the ids of the edges from S to T)
/// are only in a Done frame that found the cut.
/// </summary>
sealed record FlowFrame(
    [property: JsonPropertyName("event")] StepEvent Event,
    [property: JsonPropertyName("caption")] string Caption,
    [property: JsonPropertyName("focus")] string[] Focus,
    [property: JsonPropertyName("nodes")] IReadOnlyDictionary<string, StateRef> Nodes,
    [property: JsonPropertyName("edges")] IReadOnlyDictionary<string, StateRef> Edges,
    [property: JsonPropertyName("flow")] IReadOnlyDictionary<string, int> Flow,
    [property: JsonPropertyName("value")] int Value,
    [property: JsonPropertyName("path")] IReadOnlyList<FlowArc> Path) {
    /// <summary>The solver step this frame was drawn from (see <see cref="FramesResponse"/>, #895); null for an added frame. Always written.</summary>
    [JsonPropertyName("step")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int? Step { get; init; }

    [JsonPropertyName("groups")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, int>? Groups { get; init; }

    [JsonPropertyName("cut")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Cut { get; init; }

    [JsonPropertyName("ok")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Ok { get; init; }
}

/// <summary>
/// Derives the Flow Network picture from (network, a step's <see cref="NetworkFlow"/>, step event, focus). Nothing here
/// knows about a particular solver.
/// <para>
/// <b>Ids.</b> A node's id is its name written with <see cref="GraphSubsetFrames.EscapeName"/> (a <c>\</c> before every
/// <c>\</c>, <c>-</c> and <c>&gt;</c>; ordinary names are unchanged). The edge from <c>u</c> to <c>v</c> is
/// <c>{id of u}&gt;{id of v}</c> (<c>1&gt;2</c>), so <c>1&gt;2</c> and <c>2&gt;1</c> are two edges. A node id never holds
/// a bare <c>&gt;</c> and an edge id always does, so no two ids are equal; an id is still a label, never something to
/// split: every edge gives its ends as <c>a</c> and <c>b</c>.
/// </para>
/// <para>
/// <b>Parallel edges</b> (#769): edges listed more than once from the same node to the same node are one edge whose
/// capacity is their sum, as the solver adds them; its <c>label</c> lists every listed capacity in the order listed. An
/// edge listed twice with the same capacity is one edge (the instance's edges are a set). Edges in opposite directions
/// stay two edges.
/// </para>
/// <para>
/// <b>Layout</b> (as <c>mockups/flow.js</c>): the source in the first column, the sink in the last, every other node in
/// the column of its hop distance from the source along the edges (not passing through the sink); nodes the source cannot
/// reach share the last column before the sink. Rows follow instance order.
/// </para>
/// <para>
/// <b>States</b> (precedence <c>Rejected &gt; ElementHighlight &gt; Solution &gt; Covered &gt; Blocked &gt; Background</c>,
/// with <c>Untraveled</c> only where nothing else applies; see <see cref="GraphSubsetFrames.Raise"/>):
/// </para>
/// <list type="bullet">
/// <item>While pushing flow: an edge that is full (its flow equals its capacity, which is above 0) is <c>Covered</c>. How
/// much an edge carries is the <c>flow</c> annotation, not a state.</item>
/// <item>The focus (the augmenting path's nodes and edges) is <c>ElementHighlight</c>, or <c>Rejected</c> in a Reject
/// step of an incremental run (<see cref="GraphSubsetFrames.FocusMark"/>).</item>
/// <item>In the Done frame: the cut edges (from S to T) are <c>Solution</c>, the edges from T back to S (they carry
/// nothing) <c>Untraveled</c>; <c>groups</c> colors the two sides, so nodes have no state of their own.</item>
/// <item>A run stopped by its time limit ends with a Done frame, ok = false, every element <c>Background</c> and no
/// flow (<see cref="StoppedFrame"/>).</item>
/// </list>
/// </summary>
static class FlowFrames {
    public const string Kind = "flowNetwork";
    public const string LayoutName = "sourceToSink";
    public const string RuleName = "minimumSTCut";

    // ---- Ids ----

    /// <summary>A node's id: its name with the separators escaped (<see cref="GraphSubsetFrames.EscapeName"/>).</summary>
    public static string NodeId(string name) => GraphSubsetFrames.EscapeName(name);

    /// <summary>The id of the edge from <paramref name="from"/> to <paramref name="to"/>: <c>{from}&gt;{to}</c>.</summary>
    public static string ArcId(string from, string to) => NodeId(from) + ">" + NodeId(to);

    // ---- The model the payload and the frames read ----

    private sealed record Edge(string Id, string A, string B, int Capacity, string? Label);

    private sealed class Model {
        public readonly FlowSpec Spec;
        public readonly List<string> Nodes = new();
        public readonly List<Edge> Edges = new();
        public readonly Dictionary<string, Edge> EdgeById = new(StringComparer.Ordinal);
        public readonly HashSet<string> NodeIds = new(StringComparer.Ordinal);
        public readonly Dictionary<string, int> Layer = new(StringComparer.Ordinal);
        public readonly Dictionary<string, int> Row = new(StringComparer.Ordinal);
        public readonly List<List<string>> Layers = new();

        public Model(FlowSpec spec) {
            Spec = spec;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var n in spec.Nodes) if (seen.Add(n)) Nodes.Add(n);
            // A node named only by an edge, the source or the sink still gets a node.
            foreach (var n in spec.Edges.SelectMany(e => new[] { e.From, e.To }).Concat([spec.Source, spec.Sink]))
                if (seen.Add(n)) Nodes.Add(n);
            foreach (var n in Nodes) NodeIds.Add(NodeId(n));

            var listed = new Dictionary<string, (string A, string B, List<int> Capacities)>(StringComparer.Ordinal);
            var order = new List<string>();
            foreach (var e in spec.Edges) {
                string id = ArcId(e.From, e.To);
                if (!listed.TryGetValue(id, out var entry)) {
                    entry = (e.From, e.To, new List<int>());
                    listed[id] = entry;
                    order.Add(id);
                }
                entry.Capacities.Add(e.Capacity);
            }
            foreach (var id in order) {
                var (a, b, capacities) = listed[id];
                var edge = new Edge(id, a, b, capacities.Sum(),
                    capacities.Count < 2 ? null : string.Join(",", capacities.Select(c => c.ToString(CultureInfo.InvariantCulture))));
                Edges.Add(edge);
                EdgeById[id] = edge;
            }
            Lay();
        }

        private void Lay() {
            string s = Spec.Source, t = Spec.Sink;
            var dist = new Dictionary<string, int>(StringComparer.Ordinal) { [s] = 0 };
            var queue = new Queue<string>([s]);
            while (queue.Count > 0) {
                string x = queue.Dequeue();
                foreach (var e in Edges)
                    if (e.A == x && e.B != t && !dist.ContainsKey(e.B)) {
                        dist[e.B] = dist[x] + 1;
                        queue.Enqueue(e.B);
                    }
            }
            int last = Nodes.Where(n => n != t && dist.ContainsKey(n)).Select(n => dist[n]).DefaultIfEmpty(0).Max();
            var unreached = Nodes.Where(n => n != t && !dist.ContainsKey(n)).ToList();
            if (unreached.Count > 0) {
                last = Math.Max(last, 1);
                foreach (var n in unreached) dist[n] = last;
            }
            // The sink is last, even when the source is the sink's only other node (a source equal to the sink is refused by
            // the problem, but the layout must not depend on that).
            if (t != s) dist[t] = last + 1;
            int columns = dist.Values.Max() + 1;
            for (int c = 0; c < columns; c++) Layers.Add(Nodes.Where(n => dist[n] == c).ToList());
            foreach (var layer in Layers)
                for (int i = 0; i < layer.Count; i++) {
                    Layer[layer[i]] = dist[layer[i]];
                    Row[layer[i]] = i;
                }
        }

        /// <summary>
        /// A maximum flow by Edmonds-Karp over the merged edges, as units per edge id, with the nodes the source can still
        /// reach. Used only to draw an answer whose steps cannot be drawn.
        /// </summary>
        public (Dictionary<string, int> Flow, HashSet<string> Reach) MaxFlow() {
            var flow = Edges.ToDictionary(e => e.Id, _ => 0, StringComparer.Ordinal);
            // A residual move: along an edge (room = capacity - flow) or against it (room = flow).
            IEnumerable<(Edge Edge, bool Forward, string To, int Room)> Moves(string x) {
                foreach (var e in Edges) {
                    if (e.A == x && e.Capacity - flow[e.Id] > 0) yield return (e, true, e.B, e.Capacity - flow[e.Id]);
                    if (e.B == x && flow[e.Id] > 0) yield return (e, false, e.A, flow[e.Id]);
                }
            }
            HashSet<string> Search(Dictionary<string, (Edge Edge, bool Forward, string From)> prev) {
                var reach = new HashSet<string>(StringComparer.Ordinal) { Spec.Source };
                var queue = new Queue<string>([Spec.Source]);
                while (queue.Count > 0) {
                    string x = queue.Dequeue();
                    foreach (var (edge, forward, to, _) in Moves(x))
                        if (reach.Add(to)) {
                            prev[to] = (edge, forward, x);
                            queue.Enqueue(to);
                        }
                }
                return reach;
            }
            while (true) {
                var prev = new Dictionary<string, (Edge Edge, bool Forward, string From)>(StringComparer.Ordinal);
                var reach = Search(prev);
                if (!reach.Contains(Spec.Sink) || Spec.Sink == Spec.Source) return (flow, reach);
                var legs = new List<(Edge Edge, bool Forward)>();
                for (string x = Spec.Sink; x != Spec.Source; x = prev[x].From) legs.Add((prev[x].Edge, prev[x].Forward));
                int push = legs.Min(l => l.Forward ? l.Edge.Capacity - flow[l.Edge.Id] : flow[l.Edge.Id]);
                foreach (var (edge, forward) in legs) flow[edge.Id] += forward ? push : -push;
            }
        }
    }

    // ---- The payload ----

    public static FlowPayload Payload(FlowSpec spec) => Payload(new Model(spec));

    private static FlowPayload Payload(Model m) =>
        new(Kind, LayoutName, RuleName, NodeId(m.Spec.Source), NodeId(m.Spec.Sink),
            m.Nodes.Select(n => new FlowNodeDto(NodeId(n), n, m.Layer[n], m.Row[n])).ToList(),
            m.Edges.Select(e => new FlowEdgeDto(e.Id, NodeId(e.A), NodeId(e.B), e.Capacity) { Label = e.Label }).ToList(),
            m.Layers.Select(l => (IReadOnlyList<string>)l.Select(NodeId).ToList()).ToList());

    // ---- Frames ----

    /// <summary>
    /// The frame for one step. <paramref name="partial"/> is the flow after the step: units keyed by edge id, the path's
    /// legs by edge id, the S side (set once no path is left) by node name. <paramref name="incremental"/> says whether the
    /// step comes from an incremental run (<see cref="SolveRun.Incremental"/>).
    /// </summary>
    public static FlowFrame Frame(FlowSpec spec, NetworkFlow partial, StepEvent ev, string[] focus, string caption, bool? ok = null, bool incremental = false) =>
        Frame(new Model(spec), partial, ev, focus, caption, ok, incremental);

    private static FlowFrame Frame(Model m, NetworkFlow partial, StepEvent ev, string[] focus, string caption, bool? ok, bool incremental) {
        var nodes = new Dictionary<string, PictureState>(StringComparer.Ordinal);
        var edges = new Dictionary<string, PictureState>(StringComparer.Ordinal);
        var flow = FlowOf(m, partial);
        var path = partial.Path.Where(a => m.EdgeById.ContainsKey(a.Edge)).ToList();
        SortedDictionary<string, int>? groups = null;
        List<string>? cut = null;

        if (ev == StepEvent.Done && ok == true) {
            // The cut: S is what the source can still reach. Edges from S to T are the cut, edges from T to S carry nothing.
            var side = new HashSet<string>(partial.Side, StringComparer.Ordinal);
            groups = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (var n in m.Nodes) groups[NodeId(n)] = side.Contains(n) ? 0 : 1;
            cut = new List<string>();
            foreach (var e in m.Edges) {
                bool a = side.Contains(e.A), b = side.Contains(e.B);
                if (a && !b) {
                    cut.Add(e.Id);
                    GraphSubsetFrames.Raise(edges, e.Id, PictureState.Solution);
                } else if (!a && b) {
                    GraphSubsetFrames.Raise(edges, e.Id, PictureState.Untraveled);
                }
            }
        } else if (ev != StepEvent.Done) {
            foreach (var e in m.Edges)
                if (e.Capacity > 0 && flow[e.Id] == e.Capacity) GraphSubsetFrames.Raise(edges, e.Id, PictureState.Covered);
        }

        // The path pushed along is the piece under test of an incremental run: highlighted, Rejected only in a Reject.
        var mark = incremental ? GraphSubsetFrames.FocusMark(ev) : PictureState.ElementHighlight;
        foreach (var id in focus) {
            if (m.NodeIds.Contains(id)) GraphSubsetFrames.Raise(nodes, id, mark);
            else if (m.EdgeById.ContainsKey(id)) GraphSubsetFrames.Raise(edges, id, mark);
        }

        return new FlowFrame(ev, caption, focus, GraphSubsetFrames.ToRefs(nodes), GraphSubsetFrames.ToRefs(edges), flow, partial.Value, path) {
            Groups = groups,
            Cut = cut,
            Ok = ok,
        };
    }

    // Every edge's units, 0 where the partial names none; ids the picture does not have are dropped.
    private static SortedDictionary<string, int> FlowOf(Model m, NetworkFlow partial) {
        var flow = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var e in m.Edges) flow[e.Id] = partial.Flow.TryGetValue(e.Id, out int units) ? units : 0;
        return flow;
    }

    /// <summary>
    /// The frame that closes a run stopped by its time limit (<see cref="SolveRun.TimedOut"/>): a Done frame with ok = false
    /// in which every element is Background, with no flow and no path (see <see cref="GraphSubsetFrames.StoppedFrame"/>). The
    /// solver had not finished, so nothing is an answer or judged.
    /// </summary>
    public static FlowFrame StoppedFrame(FlowSpec spec, string caption) {
        var m = new Model(spec);
        return new FlowFrame(StepEvent.Done, caption, [], new SortedDictionary<string, StateRef>(StringComparer.Ordinal),
            new SortedDictionary<string, StateRef>(StringComparer.Ordinal), FlowOf(m, new NetworkFlow([], [], 0, [])), 0, []) { Ok = false };
    }

    // ---- Reading answers (only for a run whose steps cannot be drawn) ----

    /// <summary>
    /// A Minimum S-T Cut answer (the S side, <c>{1,2}</c>) as a final flow: a maximum flow (the base's own Edmonds-Karp),
    /// with the S side the answer names. Anything unreadable is the empty side.
    /// </summary>
    public static NetworkFlow ParseAnswer(FlowSpec spec, string answer) {
        var m = new Model(spec);
        var (flow, _) = m.MaxFlow();
        int value = m.Edges.Where(e => e.A == spec.Source).Sum(e => flow[e.Id]) - m.Edges.Where(e => e.B == spec.Source).Sum(e => flow[e.Id]);
        var side = answer.Replace("{", "").Replace("}", "").Split(',').Select(x => x.Trim()).Where(x => x.Length > 0);
        return new NetworkFlow(flow, [], value, side);
    }

    // ---- The whole response ----

    /// <summary>
    /// The whole response: the payload, a frame per kept step, then a solved frame built from the answer unless the last
    /// step already is the Done step. A run stopped by its time limit ends with <see cref="StoppedFrame"/> instead, and its
    /// answer string (<see cref="SolveRun.TimedOutAnswer"/>) is never read.
    /// </summary>
    public static FramesResponse Build(FlowSpec spec, SolveRun run, Func<string, bool> verifyAnswer) {
        var m = new Model(spec);
        var frames = new List<object>();
        SolverStep<NetworkFlow>? last = null;
        foreach (var (step, index) in GraphSubsetFrames.Indexed<NetworkFlow>(run)) {
            frames.Add((run.TimedOut && step.Event == StepEvent.Done
                ? StoppedFrame(spec, step.Caption)
                : Frame(m, step.Partial, step.Event, step.Focus, step.Caption, step.Ok, run.Incremental)) with { Step = index });
            last = step;
        }
        if (run.TimedOut) {
            if (last is not { Event: StepEvent.Done }) frames.Add(StoppedFrame(spec, GraphSubsetFrames.StoppedCaption));
        } else if (last is not { Event: StepEvent.Done }) {
            bool ok = verifyAnswer(run.Answer);
            var answer = ParseAnswer(spec, run.Answer);
            frames.Add(Frame(m, ok ? answer : new NetworkFlow(answer.Flow, [], answer.Value, []), StepEvent.Done, [],
                ok ? $"The answer is {run.Answer}." : "No answer was found.", ok, false));
        }
        return new FramesResponse(nameof(VisualizationType.FlowNetwork), Payload(m), frames);
    }

    // ---- Captions, for solvers to call inside a recorder lambda so they cost nothing when recording is off ----

    /// <summary>"{a,b,c}" for captions.</summary>
    public static string Braces(IEnumerable<string> names) => "{" + string.Join(",", names) + "}";

    /// <summary>"x", "x and y", "x, y and z".</summary>
    public static string List(IReadOnlyList<string> items) =>
        items.Count <= 1 ? string.Join("", items) : string.Join(", ", items.Take(items.Count - 1)) + " and " + items[^1];
}

/// <summary>
/// A visualization for a flow problem drawn as the Flow Network picture. The visualization declares only what the problem
/// is (<see cref="Describe"/>); the frames hook then comes for free. Implement members explicitly so nothing new appears in
/// /info. The class must also declare <c>Type? IVisualization.StepShape =&gt; typeof(NetworkFlow)</c>.
/// </summary>
interface IFlowVisualization<TProblem> : IFramesVisualization where TProblem : IProblem {
    FlowSpec Describe(TProblem problem);

    FramesResponse IFramesVisualization.BuildFrames(string instance, SolveRun run) {
        TProblem problem = ParseGuard.CreateProblem<TProblem>(instance);
        return FlowFrames.Build(Describe(problem), run, answer => {
            // The problem's own verifier decides; a certificate it cannot read is not correct.
            try { return problem.defaultVerifier.verify(instance, answer); } catch { return false; }
        });
    }
}
