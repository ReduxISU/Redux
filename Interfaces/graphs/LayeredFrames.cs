using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using API.Interfaces.Steps;

namespace API.Interfaces.Graphs;

/// <summary>Which problem a Layered Graph picture draws: it decides the answer shape and how a step is judged.</summary>
enum LayeredRule {
    TopologicalSort,
    FeedbackArcSet,
    FeedbackNodeSet,
    StronglyConnectedComponents,
    SingleSourceShortestPath,
    SinglePairShortestPath,
}

/// <summary>One edge as the instance lists it: from, to, and its weight in a weighted graph.</summary>
sealed record LayeredEdgeInput(string From, string To, double? Weight = null);

/// <summary>
/// What a directed-graph problem hands the Layered Graph picture: its graph, the optional K, source and target, and its
/// rule. No layout, no colors, no ids; the base derives those.
/// </summary>
/// <param name="Nodes">Node names, in instance order.</param>
/// <param name="Edges">Edges as listed, parallel edges included (see <see cref="LayeredFrames"/> for how they merge).</param>
/// <param name="Rule">The problem.</param>
sealed record LayeredSpec(IReadOnlyList<string> Nodes, IReadOnlyList<LayeredEdgeInput> Edges, LayeredRule Rule) {
    /// <summary>False for a shortest-path instance written with undirected edges ({u,v}); every edge then goes both ways.</summary>
    public bool Directed { get; init; } = true;
    public int? K { get; init; }
    public string? Source { get; init; }
    public string? Target { get; init; }
}

// --- JSON shapes (explicit names so the contract does not depend on serializer naming options) ---

/// <summary>A node: its id, its label (the name), its layer (rank, left to right) and its row (place within the layer).</summary>
sealed record LayeredNodeDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("layer")] int Layer,
    [property: JsonPropertyName("row")] int Row);

/// <summary>
/// An edge from <c>a</c> to <c>b</c> (in an undirected graph, between them). <c>back</c> is true when it points backward in
/// the layout (it closes a cycle and is drawn as a curve under the rows). <c>weight</c> is the weight that counts (the
/// lightest of parallel edges); <c>label</c>, present only for parallel edges, lists every listed weight in the order
/// listed (<c>"2,5"</c>): draw <c>label</c> if present, else <c>weight</c>.
/// </summary>
sealed record LayeredEdgeDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("a")] string A,
    [property: JsonPropertyName("b")] string B,
    [property: JsonPropertyName("back")] bool Back) {
    [JsonPropertyName("weight")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Weight { get; init; }

    [JsonPropertyName("label")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Label { get; init; }
}

/// <summary>The static part of a Layered Graph picture, sent once per response.</summary>
sealed record LayeredPayload(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("layout")] string Layout,
    [property: JsonPropertyName("shape")] string Shape,
    [property: JsonPropertyName("rule")] string Rule,
    [property: JsonPropertyName("directed")] bool Directed,
    [property: JsonPropertyName("weighted")] bool Weighted,
    [property: JsonPropertyName("nodes")] IReadOnlyList<LayeredNodeDto> Nodes,
    [property: JsonPropertyName("edges")] IReadOnlyList<LayeredEdgeDto> Edges,
    [property: JsonPropertyName("layers")] IReadOnlyList<IReadOnlyList<string>> Layers) {
    [JsonPropertyName("k")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? K { get; init; }

    [JsonPropertyName("source")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Source { get; init; }

    [JsonPropertyName("target")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Target { get; init; }
}

/// <summary>
/// One picture of the graph. <c>nodes</c> and <c>edges</c> list only elements whose state is not
/// <see cref="PictureState.Background"/>. The annotations belong to one shape each and are present in every frame of that
/// shape: <c>order</c> (order shape: the placed nodes, in order, for the order badges), <c>groups</c> and <c>finish</c>
/// (groups shape: node id to component index; the first pass's finish order, for the finish numbers), <c>dist</c> and
/// <c>pred</c> (distances shape: every node's best known distance, null for ∞; the node each reached node was reached from).
/// </summary>
sealed record LayeredFrame(
    [property: JsonPropertyName("event")] StepEvent Event,
    [property: JsonPropertyName("caption")] string Caption,
    [property: JsonPropertyName("focus")] string[] Focus,
    [property: JsonPropertyName("nodes")] IReadOnlyDictionary<string, StateRef> Nodes,
    [property: JsonPropertyName("edges")] IReadOnlyDictionary<string, StateRef> Edges) {
    [JsonPropertyName("order")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Order { get; init; }

    [JsonPropertyName("groups")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, int>? Groups { get; init; }

    [JsonPropertyName("finish")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Finish { get; init; }

    [JsonPropertyName("dist")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, int?>? Dist { get; init; }

    [JsonPropertyName("pred")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Pred { get; init; }

    [JsonPropertyName("ok")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Ok { get; init; }
}

/// <summary>
/// Derives the Layered Graph picture from (graph, rule, a step's partial answer, step event, focus). Nothing here knows about
/// a particular solver.
/// <para>
/// <b>Ids.</b> A node's id is its name written with <see cref="GraphSubsetFrames.EscapeName"/> (a <c>\</c> before every
/// <c>\</c>, <c>-</c> and <c>&gt;</c>; ordinary names are unchanged). A directed edge from <c>u</c> to <c>v</c> is
/// <c>{id of u}&gt;{id of v}</c> (<c>1&gt;2</c>), so <c>1&gt;2</c> and <c>2&gt;1</c> are two edges; an undirected edge is
/// <see cref="GraphSubsetFrames.EdgeId"/> (<c>1-2</c>). A node id never holds a bare separator and an edge id always holds
/// one, so no two ids are equal; an id is still a label, never something to split: every edge gives its ends as <c>a</c>
/// and <c>b</c>.
/// </para>
/// <para>
/// <b>Parallel edges</b> (#769): edges listed more than once between the same two nodes (in the same direction, if directed)
/// are one edge whose weight is the lightest, since a path only ever takes the cheapest; its <c>label</c> lists every listed
/// weight in the order listed.
/// </para>
/// <para>
/// <b>Layout.</b> Break cycles by depth-first search (from the source first, then in node order): an edge into a node still
/// on the search stack is a back edge. Rank by the longest path over the other edges, then order each rank by the average
/// row of its neighbors in the rank before (two down-and-up sweeps). An undirected edge is oriented the way the search first
/// crosses it.
/// </para>
/// <para>
/// <b>States</b> (precedence <c>Rejected &gt; ElementHighlight &gt; Solution &gt; Committed &gt; Covered &gt; Blocked &gt; Background</c>,
/// with <c>Untraveled</c> only where nothing else applies; see <see cref="GraphSubsetFrames.Raise"/>). What a solver has
/// kept is <c>Committed</c> until the run ends on it, and <c>Solution</c> only in the confirmed answer (the Done, or an Accept
/// that ends the run; <see cref="GraphSubsetFrames.Picked"/>, #886). In a Done with ok = false (the run found nothing) what it
/// keeps is <c>Rejected</c> instead, never <c>Solution</c>:
/// </para>
/// <list type="bullet">
/// <item><b>Order</b> (Topological Sort): placed nodes are <c>Committed</c> (an incremental solver's committed order), nodes
/// ready to place (every edge into them comes from a placed node) <c>Covered</c>; an edge between two placed nodes is
/// <c>Committed</c> when it points forward and <c>Rejected</c> when it points backward, an edge out of a placed node to one not
/// placed yet <c>Covered</c>. A Done that is not ok shows the nodes that can never be placed and one cycle among them
/// <c>Rejected</c>.</item>
/// <item><b>Removed set</b> (Feedback Arc Set, edges; Feedback Node Set, nodes): what the candidate removes is
/// <c>ElementHighlight</c> while it is only tried (<see cref="GraphSubsetFrames.Picked"/>), <c>Committed</c> in an Accept the
/// run goes on from, and <c>Solution</c> in the answer (the frontend draws a removed edge dashed); edges that go with a removed node are <c>Untraveled</c>; a cycle
/// that survives the removal is <c>Rejected</c>, except in an Accept (a removal accepted leaves none, and the Reduction View
/// draws a pane at rest as an Accept).</item>
/// <item><b>Groups</b> (Strongly Connected Components): <c>groups</c> colors the nodes, so a node in a component has no
/// state of its own (as in the Graph picture's groups shape); a node the first pass has visited but no component holds yet is
/// <c>Covered</c>; an edge inside a component is <c>Committed</c>, and <c>Solution</c> in the answer.</item>
/// <item><b>Distances</b> (SSSP, SPSP): settled nodes and the edges they were settled through are <c>Committed</c> (their
/// cost is final, but the run is not over), and <c>Solution</c> in the answer; nodes
/// reached but not settled, and the edges they were reached through, <c>Covered</c>. In the Done step nodes never reached
/// are <c>Untraveled</c>; for a single pair only the path is <c>Solution</c> (other settled nodes are <c>Covered</c>), and an
/// unreached target is <c>Rejected</c>.</item>
/// <item>The focus is <c>ElementHighlight</c>, or <c>Rejected</c> in a Reject step of an incremental run
/// (<see cref="GraphSubsetFrames.FocusMark"/>). A whole candidate's focus (a brute force) is only highlighted.</item>
/// </list>
/// </summary>
static class LayeredFrames {
    public const string Kind = "layeredGraph";
    public const string LayoutName = "ranked";

    // ---- Names ----

    public static string RuleName(LayeredRule rule) => rule switch {
        LayeredRule.TopologicalSort => "topologicalSort",
        LayeredRule.FeedbackArcSet => "feedbackArcSet",
        LayeredRule.FeedbackNodeSet => "feedbackNodeSet",
        LayeredRule.StronglyConnectedComponents => "stronglyConnectedComponents",
        LayeredRule.SingleSourceShortestPath => "singleSourceShortestPath",
        LayeredRule.SinglePairShortestPath => "singlePairShortestPath",
        _ => throw new ArgumentOutOfRangeException(nameof(rule)),
    };

    public static string ShapeName(LayeredRule rule) => rule switch {
        LayeredRule.TopologicalSort => "order",
        LayeredRule.FeedbackArcSet or LayeredRule.FeedbackNodeSet => "removedSet",
        LayeredRule.StronglyConnectedComponents => "groups",
        LayeredRule.SingleSourceShortestPath or LayeredRule.SinglePairShortestPath => "distances",
        _ => throw new ArgumentOutOfRangeException(nameof(rule)),
    };

    /// <summary>The step shape (<c>TPartial</c>) a solver of <paramref name="rule"/> records.</summary>
    public static Type StepShape(LayeredRule rule) => rule switch {
        LayeredRule.TopologicalSort => typeof(Tour),
        LayeredRule.FeedbackArcSet => typeof(EdgeSet),
        LayeredRule.FeedbackNodeSet => typeof(NodeSet),
        LayeredRule.StronglyConnectedComponents => typeof(Components),
        LayeredRule.SingleSourceShortestPath or LayeredRule.SinglePairShortestPath => typeof(Distances),
        _ => throw new ArgumentOutOfRangeException(nameof(rule)),
    };

    // ---- Ids ----

    /// <summary>A node's id: its name with the separators escaped (<see cref="GraphSubsetFrames.EscapeName"/>).</summary>
    public static string NodeId(string name) => GraphSubsetFrames.EscapeName(name);

    /// <summary>The id of the directed edge from <paramref name="from"/> to <paramref name="to"/>: <c>{from}&gt;{to}</c>.</summary>
    public static string ArcId(string from, string to) => NodeId(from) + ">" + NodeId(to);

    /// <summary>The id of the edge between two nodes: <see cref="ArcId"/> when directed, else <see cref="GraphSubsetFrames.EdgeId"/>.</summary>
    public static string EdgeId(bool directed, string from, string to) => directed ? ArcId(from, to) : GraphSubsetFrames.EdgeId(from, to);

    /// <summary>The arc from <paramref name="from"/> to <paramref name="to"/> as a construction step adds it: its id and its two ends' ids, tail first.</summary>
    public static BuildEdge BuildArc(string from, string to) => new(ArcId(from, to), NodeId(from), NodeId(to));

    // ---- The model the payload and the frames read ----

    private sealed record Edge(string Id, string A, string B, double? Weight, string? Label);

    private sealed class Model {
        public readonly LayeredSpec Spec;
        public readonly List<string> Nodes = new();
        public readonly List<Edge> Edges = new();
        public readonly Dictionary<string, Edge> EdgeById = new(StringComparer.Ordinal);
        /// <summary>The edges leaving each node, in listed order (an undirected edge leaves both of its ends).</summary>
        public readonly Dictionary<string, List<(Edge Edge, string To)>> Out = new(StringComparer.Ordinal);
        public readonly Dictionary<string, int> Layer = new(StringComparer.Ordinal);
        public readonly Dictionary<string, int> Row = new(StringComparer.Ordinal);
        public readonly List<List<string>> Layers = new();
        public readonly HashSet<string> Back = new(StringComparer.Ordinal);
        public readonly HashSet<string> NodeIds = new(StringComparer.Ordinal);

        public Model(LayeredSpec spec) {
            Spec = spec;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var n in spec.Nodes) if (seen.Add(n)) Nodes.Add(n);
            // A node named only by an edge, the source or the target still gets a node.
            foreach (var n in spec.Edges.SelectMany(e => new[] { e.From, e.To }).Concat(new[] { spec.Source, spec.Target }.OfType<string>()))
                if (seen.Add(n)) Nodes.Add(n);
            foreach (var n in Nodes) {
                Out[n] = new();
                NodeIds.Add(NodeId(n));
            }

            var listed = new Dictionary<string, (string A, string B, List<double?> Weights)>(StringComparer.Ordinal);
            var order = new List<string>();
            foreach (var e in spec.Edges) {
                string id = EdgeId(spec.Directed, e.From, e.To);
                if (!listed.TryGetValue(id, out var entry)) {
                    entry = (e.From, e.To, new List<double?>());
                    listed[id] = entry;
                    order.Add(id);
                }
                entry.Weights.Add(e.Weight);
            }
            foreach (var id in order) {
                var (a, b, weights) = listed[id];
                var edge = new Edge(id, a, b, Lightest(weights), WeightLabel(weights));
                Edges.Add(edge);
                EdgeById[id] = edge;
                Out[a].Add((edge, b));
                if (!spec.Directed && a != b) Out[b].Add((edge, a));
            }
            Lay();
        }

        private static double? Lightest(List<double?> weights) => weights.Any(w => w is null) ? null : weights.Min();

        private static string? WeightLabel(List<double?> weights) =>
            weights.Count < 2 || weights.Any(w => w is null)
                ? null
                : string.Join(",", weights.Select(w => w!.Value.ToString(CultureInfo.InvariantCulture)));

        private void Lay() {
            // 1. Break cycles: an edge into a node still on the search stack is a back edge. An undirected edge is oriented
            // the way the search first crosses it, and never crossed again.
            var oriented = new Dictionary<string, (string From, string To)>(StringComparer.Ordinal);
            var state = new Dictionary<string, int>(StringComparer.Ordinal); // 1 on the stack, 2 done
            var starts = Spec.Source is { } s ? new[] { s }.Concat(Nodes.Where(n => n != s)) : Nodes;
            foreach (var start in starts) {
                if (state.ContainsKey(start)) continue;
                var stack = new Stack<(string Node, int Next)>();
                stack.Push((start, 0));
                state[start] = 1;
                while (stack.Count > 0) {
                    var (node, next) = stack.Pop();
                    var outs = Out[node];
                    if (next >= outs.Count) {
                        state[node] = 2;
                        continue;
                    }
                    stack.Push((node, next + 1));
                    var (edge, to) = outs[next];
                    if (oriented.ContainsKey(edge.Id)) continue;
                    oriented[edge.Id] = (node, to);
                    if (state.TryGetValue(to, out int st)) {
                        if (st == 1) Back.Add(edge.Id);
                    } else {
                        state[to] = 1;
                        stack.Push((to, 0));
                    }
                }
            }

            // 2. Rank by the longest path over the edges that point forward.
            var forward = Edges.Where(e => !Back.Contains(e.Id) && e.A != e.B).Select(e => oriented[e.Id]).ToList();
            var indegree = Nodes.ToDictionary(n => n, _ => 0, StringComparer.Ordinal);
            var succ = Nodes.ToDictionary(n => n, _ => new List<string>(), StringComparer.Ordinal);
            var pred = Nodes.ToDictionary(n => n, _ => new List<string>(), StringComparer.Ordinal);
            foreach (var (from, to) in forward) {
                indegree[to]++;
                succ[from].Add(to);
                pred[to].Add(from);
            }
            foreach (var n in Nodes) Layer[n] = 0;
            var queue = new Queue<string>(Nodes.Where(n => indegree[n] == 0));
            while (queue.Count > 0) {
                string n = queue.Dequeue();
                foreach (var m in succ[n]) {
                    Layer[m] = Math.Max(Layer[m], Layer[n] + 1);
                    if (--indegree[m] == 0) queue.Enqueue(m);
                }
            }
            int ranks = Nodes.Count == 0 ? 0 : Layer.Values.Max() + 1;
            for (int r = 0; r < ranks; r++) Layers.Add(Nodes.Where(n => Layer[n] == r).ToList());

            // 3. Order each rank by the average row of its neighbors in the rank before (down) or after (up).
            void Reindex() {
                foreach (var layer in Layers)
                    for (int i = 0; i < layer.Count; i++) Row[layer[i]] = i;
            }
            Reindex();
            double Bary(string n, IEnumerable<string> neighbours, int rank) {
                var near = neighbours.Where(m => Layer[m] == rank).ToList();
                return near.Count > 0 ? near.Average(m => (double)Row[m]) : Row[n];
            }
            for (int sweep = 0; sweep < 2; sweep++) {
                for (int r = 1; r < ranks; r++) {
                    var b = Layers[r].ToDictionary(n => n, n => Bary(n, pred[n], r - 1), StringComparer.Ordinal);
                    Layers[r] = Layers[r].OrderBy(n => b[n]).ToList();
                    Reindex();
                }
                for (int r = ranks - 2; r >= 0; r--) {
                    var b = Layers[r].ToDictionary(n => n, n => Bary(n, succ[n], r + 1), StringComparer.Ordinal);
                    Layers[r] = Layers[r].OrderBy(n => b[n]).ToList();
                    Reindex();
                }
            }
        }

        /// <summary>
        /// One cycle in the graph left after removing <paramref name="goneEdges"/> (ids) and <paramref name="goneNodes"/>
        /// (names), as its edge ids in order, or null when there is none. Directed graphs only.
        /// </summary>
        public List<string>? FindCycle(ISet<string> goneEdges, ISet<string> goneNodes) {
            var state = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var start in Nodes) {
                if (goneNodes.Contains(start) || state.ContainsKey(start)) continue;
                // The search path: each node with the edge it was entered by and how many of its edges are done.
                var path = new List<(string Node, Edge? In, int Next)> { (start, null, 0) };
                state[start] = 1;
                while (path.Count > 0) {
                    var (node, inEdge, next) = path[^1];
                    var outs = Out[node];
                    if (next >= outs.Count) {
                        state[node] = 2;
                        path.RemoveAt(path.Count - 1);
                        continue;
                    }
                    path[^1] = (node, inEdge, next + 1);
                    var (edge, to) = outs[next];
                    if (goneEdges.Contains(edge.Id) || goneNodes.Contains(to)) continue;
                    if (state.TryGetValue(to, out int st)) {
                        if (st != 1) continue;
                        int at = path.FindIndex(p => p.Node == to);
                        var ring = path.Skip(at + 1).Select(p => p.In!.Id).ToList();
                        ring.Add(edge.Id);
                        return ring;
                    }
                    state[to] = 1;
                    path.Add((to, edge, 0));
                }
            }
            return null;
        }

        /// <summary>The nodes Kahn's algorithm can place: those not on, and not after, a cycle.</summary>
        public HashSet<string> Placeable() {
            var indegree = Nodes.ToDictionary(n => n, _ => 0, StringComparer.Ordinal);
            foreach (var e in Edges) indegree[e.B]++;
            var queue = new Queue<string>(Nodes.Where(n => indegree[n] == 0));
            var placed = new HashSet<string>(StringComparer.Ordinal);
            while (queue.Count > 0) {
                string n = queue.Dequeue();
                placed.Add(n);
                foreach (var (_, to) in Out[n])
                    if (--indegree[to] == 0) queue.Enqueue(to);
            }
            return placed;
        }

        public string CycleText(List<string> ring) =>
            string.Join(" → ", ring.Select(id => EdgeById[id].A).Append(EdgeById[ring[0]].A));
    }

    // ---- The payload ----

    public static LayeredPayload Payload(LayeredSpec spec) => Payload(new Model(spec));

    private static LayeredPayload Payload(Model m) {
        var spec = m.Spec;
        bool weighted = m.Edges.Count > 0 && m.Edges.All(e => e.Weight != null);
        return new LayeredPayload(Kind, LayoutName, ShapeName(spec.Rule), RuleName(spec.Rule), spec.Directed, weighted,
            m.Nodes.Select(n => new LayeredNodeDto(NodeId(n), n, m.Layer[n], m.Row[n])).ToList(),
            m.Edges.Select(e => new LayeredEdgeDto(e.Id, NodeId(e.A), NodeId(e.B), m.Back.Contains(e.Id)) { Weight = e.Weight, Label = e.Label }).ToList(),
            m.Layers.Select(l => (IReadOnlyList<string>)l.Select(NodeId).ToList()).ToList()) {
            K = spec.K,
            Source = spec.Source is { } s ? NodeId(s) : null,
            Target = spec.Target is { } t ? NodeId(t) : null,
        };
    }

    // ---- Frames ----

    /// <summary>
    /// The frame for one step. <paramref name="partial"/> is the step's partial answer, of the rule's shape
    /// (<see cref="StepShape"/>): <see cref="Tour"/> of node names, <see cref="EdgeSet"/> of edge ids, <see cref="NodeSet"/>
    /// of node names, <see cref="Components"/> or <see cref="Distances"/> keyed by node name. <paramref name="incremental"/>
    /// says whether the step comes from an incremental run (<see cref="SolveRun.Incremental"/>); <paramref name="endsRun"/>
    /// whether it is an Accept that ends the run (<see cref="GraphSubsetFrames.EndsRun"/>).
    /// </summary>
    public static LayeredFrame Frame(LayeredSpec spec, object partial, StepEvent ev, string[] focus, string caption, bool? ok = null, bool incremental = false, bool endsRun = false) =>
        Frame(new Model(spec), partial, ev, focus, caption, ok, incremental, endsRun);

    private static LayeredFrame Frame(Model m, object partial, StepEvent ev, string[] focus, string caption, bool? ok, bool incremental, bool endsRun) {
        var nodes = new Dictionary<string, PictureState>(StringComparer.Ordinal);
        var edges = new Dictionary<string, PictureState>(StringComparer.Ordinal);
        var known = new HashSet<string>(m.Nodes, StringComparer.Ordinal);
        // What the step keeps: Committed until the run ends on it, Solution in the answer, Rejected in a Done that found
        // nothing (#886).
        var picked = GraphSubsetFrames.Picked(ev, incremental, endsRun, ok == true);
        LayeredFrame frame;

        switch (partial) {
            case Tour tour:
                frame = OrderFrame(m, tour.Order.Where(known.Contains).Distinct(StringComparer.Ordinal).ToList(), ev, ok, picked, nodes, edges);
                break;
            case EdgeSet removed:
                frame = ArcSetFrame(m, removed, ev, picked, edges);
                break;
            case NodeSet removed:
                frame = NodeSetFrame(m, removed, ev, picked, known, nodes, edges);
                break;
            case Components components:
                frame = GroupsFrame(m, components, picked, known, nodes, edges);
                break;
            case Distances distances:
                frame = DistancesFrame(m, distances, ev, ok, picked, known, nodes, edges);
                break;
            default:
                throw new ArgumentException($"A Layered Graph picture cannot draw a {partial.GetType().Name}.", nameof(partial));
        }

        // A whole candidate's focus is only highlighted; an incremental run's focus is the piece under test, Rejected when
        // the step rejects it.
        var mark = incremental ? GraphSubsetFrames.FocusMark(ev) : PictureState.ElementHighlight;
        foreach (var id in focus) {
            if (m.NodeIds.Contains(id)) GraphSubsetFrames.Raise(nodes, id, mark);
            else if (m.EdgeById.ContainsKey(id)) GraphSubsetFrames.Raise(edges, id, mark);
        }

        return frame with {
            Event = ev,
            Caption = caption,
            Focus = focus,
            Nodes = GraphSubsetFrames.ToRefs(nodes),
            Edges = GraphSubsetFrames.ToRefs(edges),
            Ok = ok,
        };
    }

    private static LayeredFrame Blank(StepEvent ev, string caption, string[] focus) =>
        new(ev, caption, focus, new SortedDictionary<string, StateRef>(StringComparer.Ordinal), new SortedDictionary<string, StateRef>(StringComparer.Ordinal));

    // A cycle that survives the removal is marked unless the step is an Accept: a solver accepts only a removal that leaves no
    // cycle, and the Reduction View draws a pane at rest (nothing removed, nothing judged) as an Accept (#694).
    private static LayeredFrame ArcSetFrame(Model m, EdgeSet removed, StepEvent ev, PictureState picked, Dictionary<string, PictureState> edges) {
        var gone = new HashSet<string>(removed.Edges.Where(m.EdgeById.ContainsKey), StringComparer.Ordinal);
        foreach (var id in gone) GraphSubsetFrames.Raise(edges, id, picked);
        if (ev != StepEvent.Accept && m.FindCycle(gone, new HashSet<string>()) is { } ring)
            foreach (var id in ring) GraphSubsetFrames.Raise(edges, id, PictureState.Rejected);
        return Blank(StepEvent.Try, "", []);
    }

    private static LayeredFrame NodeSetFrame(Model m, NodeSet removed, StepEvent ev, PictureState picked, HashSet<string> known,
        Dictionary<string, PictureState> nodes, Dictionary<string, PictureState> edges) {
        var gone = new HashSet<string>(removed.Nodes.Where(known.Contains), StringComparer.Ordinal);
        foreach (var n in gone) GraphSubsetFrames.Raise(nodes, NodeId(n), picked);
        if (ev != StepEvent.Accept && m.FindCycle(new HashSet<string>(), gone) is { } ring)
            foreach (var id in ring) GraphSubsetFrames.Raise(edges, id, PictureState.Rejected);
        foreach (var e in m.Edges)
            if (gone.Contains(e.A) || gone.Contains(e.B)) GraphSubsetFrames.Raise(edges, e.Id, PictureState.Untraveled);
        return Blank(StepEvent.Try, "", []);
    }

    private static LayeredFrame OrderFrame(Model m, List<string> placed, StepEvent ev, bool? ok, PictureState picked,
        Dictionary<string, PictureState> nodes, Dictionary<string, PictureState> edges) {
        if (ev == StepEvent.Done && ok != true && placed.Count < m.Nodes.Count) {
            // No order exists: the nodes Kahn's algorithm can never place, and one cycle among them.
            var placeable = m.Placeable();
            foreach (var n in m.Nodes)
                if (!placeable.Contains(n)) GraphSubsetFrames.Raise(nodes, NodeId(n), PictureState.Rejected);
            if (m.FindCycle(new HashSet<string>(), placeable) is { } ring)
                foreach (var id in ring) GraphSubsetFrames.Raise(edges, id, PictureState.Rejected);
        }
        var position = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < placed.Count; i++) position[placed[i]] = i;
        foreach (var n in placed) GraphSubsetFrames.Raise(nodes, NodeId(n), picked);
        // Ready to place: every edge into it comes from a placed node. A failed Done has nothing ready (it is stuck).
        if (!(ev == StepEvent.Done && ok != true))
            foreach (var n in m.Nodes)
                if (!position.ContainsKey(n) && m.Edges.Where(e => e.B == n).All(e => position.ContainsKey(e.A)))
                    GraphSubsetFrames.Raise(nodes, NodeId(n), PictureState.Covered);
        foreach (var e in m.Edges) {
            bool a = position.TryGetValue(e.A, out int pa), b = position.TryGetValue(e.B, out int pb);
            if (a && b) GraphSubsetFrames.Raise(edges, e.Id, pa < pb ? picked : PictureState.Rejected);
            else if (a) GraphSubsetFrames.Raise(edges, e.Id, PictureState.Covered);
        }
        return Blank(ev, "", []) with { Order = placed.Select(NodeId).ToList() };
    }

    private static LayeredFrame GroupsFrame(Model m, Components c, PictureState picked, HashSet<string> known,
        Dictionary<string, PictureState> nodes, Dictionary<string, PictureState> edges) {
        var groups = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var of = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (n, g) in c.Of)
            if (known.Contains(n)) {
                groups[NodeId(n)] = g;
                of[n] = g;
            }
        foreach (var n in c.Visited)
            if (known.Contains(n) && !of.ContainsKey(n)) GraphSubsetFrames.Raise(nodes, NodeId(n), PictureState.Covered);
        foreach (var e in m.Edges)
            if (of.TryGetValue(e.A, out int ga) && of.TryGetValue(e.B, out int gb) && ga == gb)
                GraphSubsetFrames.Raise(edges, e.Id, picked);
        return Blank(StepEvent.Try, "", []) with {
            Groups = groups,
            Finish = c.Finished.Where(known.Contains).Select(NodeId).ToList(),
        };
    }

    private static LayeredFrame DistancesFrame(Model m, Distances d, StepEvent ev, bool? ok, PictureState picked, HashSet<string> known,
        Dictionary<string, PictureState> nodes, Dictionary<string, PictureState> edges) {
        bool directed = m.Spec.Directed;
        var settled = new HashSet<string>(d.Settled.Where(known.Contains), StringComparer.Ordinal);
        bool done = ev == StepEvent.Done;
        bool pair = m.Spec.Rule == LayeredRule.SinglePairShortestPath;

        string? TreeEdge(string v) =>
            d.Pred.TryGetValue(v, out var u) && known.Contains(u) && m.EdgeById.ContainsKey(EdgeId(directed, u, v)) ? EdgeId(directed, u, v) : null;

        if (done && pair) {
            // The answer is the path to the target; the rest of what was settled is only known.
            var path = new List<string>();
            if (ok == true && m.Spec.Target is { } t) {
                for (string? x = t; x != null && path.Count <= m.Nodes.Count; x = d.Pred.TryGetValue(x, out var u) ? u : null) {
                    path.Add(x);
                    if (x == m.Spec.Source) break;
                }
            }
            var onPath = new HashSet<string>(path, StringComparer.Ordinal);
            foreach (var n in path) {
                GraphSubsetFrames.Raise(nodes, NodeId(n), PictureState.Solution);
                if (TreeEdge(n) is { } id && onPath.Contains(d.Pred[n])) GraphSubsetFrames.Raise(edges, id, PictureState.Solution);
            }
            foreach (var n in settled) GraphSubsetFrames.Raise(nodes, NodeId(n), PictureState.Covered);
            if (ok != true && m.Spec.Target is { } target) GraphSubsetFrames.Raise(nodes, NodeId(target), PictureState.Rejected);
        } else {
            foreach (var n in m.Nodes) {
                bool reached = d.Dist.ContainsKey(n);
                if (!reached) continue;
                var state = settled.Contains(n) ? picked : PictureState.Covered;
                GraphSubsetFrames.Raise(nodes, NodeId(n), state);
                if (TreeEdge(n) is { } id) GraphSubsetFrames.Raise(edges, id, state);
            }
        }
        if (done)
            foreach (var n in m.Nodes)
                if (!d.Dist.ContainsKey(n)) GraphSubsetFrames.Raise(nodes, NodeId(n), PictureState.Untraveled);

        var dist = new SortedDictionary<string, int?>(StringComparer.Ordinal);
        foreach (var n in m.Nodes) dist[NodeId(n)] = d.Dist.TryGetValue(n, out int v) ? v : null;
        var pred = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (v, u) in d.Pred)
            if (known.Contains(v) && known.Contains(u)) pred[NodeId(v)] = NodeId(u);
        return Blank(ev, "", []) with { Dist = dist, Pred = pred };
    }

    /// <summary>
    /// The frame that closes a run stopped by its time limit (<see cref="SolveRun.TimedOut"/>): a Done frame with ok = false
    /// in which every element is Background, carrying the shape's empty annotation (see
    /// <see cref="GraphSubsetFrames.StoppedFrame"/>). The solver had not finished, so nothing is an answer or judged.
    /// </summary>
    public static LayeredFrame StoppedFrame(LayeredSpec spec, string caption) {
        var frame = Blank(StepEvent.Done, caption, []) with { Ok = false };
        return spec.Rule switch {
            LayeredRule.TopologicalSort => frame with { Order = [] },
            LayeredRule.StronglyConnectedComponents => frame with { Groups = new SortedDictionary<string, int>(StringComparer.Ordinal), Finish = [] },
            LayeredRule.SingleSourceShortestPath or LayeredRule.SinglePairShortestPath => frame with {
                Dist = new SortedDictionary<string, int?>(new Model(spec).Nodes.ToDictionary(NodeId, _ => (int?)null), StringComparer.Ordinal),
                Pred = new SortedDictionary<string, string>(StringComparer.Ordinal),
            },
            _ => frame,
        };
    }

    // ---- Reading answers (only for a run whose steps cannot be drawn) ----

    private static readonly Regex Arc = new(@"\(\s*([^(),{}]+?)\s*,\s*([^(),{}]+?)\s*\)", RegexOptions.Compiled);
    private static readonly Regex Class = new(@"\{([^{}]*)\}", RegexOptions.Compiled);
    private static readonly Regex NodePath = new(@"\(\s*([^(),{}]+?)\s*,\s*\{([^{}]*)\}\s*\)", RegexOptions.Compiled);

    private static List<string> Names(string list) => list.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();

    /// <summary>A Feedback Arc Set answer (<c>{(2,3),(4,1)}</c>) as the picture's edge ids.</summary>
    public static EdgeSet ParseArcs(string answer) => new(Arc.Matches(answer).Select(x => ArcId(x.Groups[1].Value, x.Groups[2].Value)));

    /// <summary>
    /// A solver's answer string as the rule's partial answer, for the Done frame of a run whose steps cannot be drawn:
    /// an order <c>(1,2,3)</c>, removed edges <c>{(2,3)}</c> or nodes <c>{2}</c>, components <c>{{1,2},{3}}</c>, shortest
    /// paths <c>{(1,{1}),(2,{1,2}),(3,{})}</c> or a path <c>{1,3,5}</c>. Anything unreadable is the empty answer.
    /// </summary>
    public static object ParseAnswer(LayeredSpec spec, string answer) => spec.Rule switch {
        LayeredRule.TopologicalSort => new Tour(Names(answer.Trim().TrimStart('(').TrimEnd(')'))),
        LayeredRule.FeedbackArcSet => ParseArcs(answer),
        LayeredRule.FeedbackNodeSet => new NodeSet(Names(answer.Replace("{", "").Replace("}", ""))),
        LayeredRule.StronglyConnectedComponents => ParseComponents(answer),
        LayeredRule.SinglePairShortestPath => DistancesOf(spec, new[] { Names(answer.Replace("{", "").Replace("}", "")) }.Where(p => p.Count > 0)),
        _ => DistancesOf(spec, NodePath.Matches(answer).Select(x => Names(x.Groups[2].Value)).Where(p => p.Count > 0)),
    };

    /// <summary>A components answer (<c>{{1,2},{3}}</c>): the n-th class is component n.</summary>
    public static Components ParseComponents(string answer) {
        var of = new Dictionary<string, int>(StringComparer.Ordinal);
        var classes = Class.Matches(answer).Select(x => Names(x.Groups[1].Value)).Where(c => c.Count > 0).ToList();
        for (int g = 0; g < classes.Count; g++)
            foreach (var n in classes[g]) of[n] = g;
        return new Components(of, of.Keys, []);
    }

    /// <summary>Settled nodes, predecessors and distances along <paramref name="paths"/> (each from the source), by the lightest edge weights.</summary>
    public static Distances DistancesOf(LayeredSpec spec, IEnumerable<List<string>> paths) {
        var m = new Model(spec);
        var dist = new Dictionary<string, int>(StringComparer.Ordinal);
        var pred = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in paths) {
            int total = 0;
            dist[path[0]] = 0;
            for (int i = 1; i < path.Count; i++) {
                total += m.EdgeById.TryGetValue(EdgeId(spec.Directed, path[i - 1], path[i]), out var e) ? (int)(e.Weight ?? 1) : 0;
                dist[path[i]] = total;
                pred[path[i]] = path[i - 1];
            }
        }
        return new Distances(dist, pred, dist.Keys);
    }

    // ---- The whole response ----

    private static (object Partial, StepEvent Event, string[] Focus, string Caption, bool? Ok) Read(object step) => step switch {
        SolverStep<Tour> s => (s.Partial, s.Event, s.Focus, s.Caption, s.Ok),
        SolverStep<EdgeSet> s => (s.Partial, s.Event, s.Focus, s.Caption, s.Ok),
        SolverStep<NodeSet> s => (s.Partial, s.Event, s.Focus, s.Caption, s.Ok),
        SolverStep<Components> s => (s.Partial, s.Event, s.Focus, s.Caption, s.Ok),
        SolverStep<Distances> s => (s.Partial, s.Event, s.Focus, s.Caption, s.Ok),
        _ => throw new ArgumentException($"A Layered Graph picture cannot draw a {step.GetType().Name}.", nameof(step)),
    };

    /// <summary>
    /// The whole response: the payload, a frame per kept step, then a solved frame built from the answer unless the last
    /// step already is the Done step. A run stopped by its time limit ends with <see cref="StoppedFrame"/> instead, and its
    /// answer string (<see cref="SolveRun.TimedOutAnswer"/>) is never read.
    /// </summary>
    public static FramesResponse Build(LayeredSpec spec, SolveRun run, Func<string, object> parseAnswer, Func<string, bool> verifyAnswer) {
        var m = new Model(spec);
        var frames = new List<object>();
        StepEvent? last = null;
        var steps = run.StepsFor(StepShape(spec.Rule)).Select(Read).ToList();
        for (int i = 0; i < steps.Count; i++) {
            var (partial, ev, focus, caption, ok) = steps[i];
            bool endsRun = i + 1 < steps.Count && GraphSubsetFrames.EndsRun(ev, steps[i + 1].Event, steps[i + 1].Ok);
            frames.Add(run.TimedOut && ev == StepEvent.Done
                ? StoppedFrame(spec, caption)
                : Frame(m, partial, ev, focus, caption, ok, run.Incremental, endsRun));
            last = ev;
        }
        bool endsWithDone = last == StepEvent.Done;
        if (run.TimedOut) {
            if (!endsWithDone) frames.Add(StoppedFrame(spec, GraphSubsetFrames.StoppedCaption));
        } else if (!endsWithDone) {
            bool ok = verifyAnswer(run.Answer);
            frames.Add(Frame(m, parseAnswer(run.Answer), StepEvent.Done, [], ok ? $"The answer is {run.Answer}." : "No answer was found.", ok, false, false));
        }
        return new FramesResponse(nameof(VisualizationType.LayeredGraph), Payload(m), frames);
    }

    // ---- Captions, for solvers to call inside a recorder lambda so they cost nothing when recording is off ----

    /// <summary>"{a,b,c}" for captions.</summary>
    public static string Braces(IEnumerable<string> names) => "{" + string.Join(",", names) + "}";

    /// <summary>"4", "4 and 5", "3, 4 and 5".</summary>
    public static string List(IReadOnlyList<string> names) =>
        names.Count <= 1 ? string.Join("", names) : string.Join(", ", names.Take(names.Count - 1)) + " and " + names[^1];

    /// <summary>
    /// A cycle finder for solver captions: given the edges (ids) and nodes (names) a candidate removes, one cycle that
    /// survives as "1 → 2 → 3 → 1", or "" when none does. Build it once per solve; it costs nothing until called.
    /// </summary>
    public static Func<IEnumerable<string>, IEnumerable<string>, string> SurvivingCycle(LayeredSpec spec) {
        Model? m = null;
        return (goneEdges, goneNodes) => {
            m ??= new Model(spec);
            var ring = m.FindCycle(new HashSet<string>(goneEdges, StringComparer.Ordinal), new HashSet<string>(goneNodes, StringComparer.Ordinal));
            return ring == null ? "" : m.CycleText(ring);
        };
    }
}

/// <summary>
/// A visualization for a directed-graph problem drawn as the Layered Graph picture. The visualization declares only what
/// the problem is (<see cref="Describe"/>); the frames hook then comes for free. Implement members explicitly so nothing new
/// appears in /info. The class must also declare <c>Type? IVisualization.StepShape</c> as
/// <see cref="LayeredFrames.StepShape"/> of its rule.
/// </summary>
interface ILayeredVisualization<TProblem> : IFramesVisualization where TProblem : IProblem {
    LayeredSpec Describe(TProblem problem);

    FramesResponse IFramesVisualization.BuildFrames(string instance, SolveRun run) {
        TProblem problem = ParseGuard.CreateProblem<TProblem>(instance);
        var spec = Describe(problem);
        return LayeredFrames.Build(spec, run,
            answer => LayeredFrames.ParseAnswer(spec, answer),
            answer => {
                // The problem's own verifier decides; a certificate it cannot read is not correct.
                try { return problem.defaultVerifier.verify(instance, answer); } catch { return false; }
            });
    }
}
