using System.Text.Json.Serialization;

namespace API.Interfaces.Steps;

// Typed answer shapes for SolverStep<TPartial>. Each is immutable with value equality, so a replay test
// can compare the last step's Partial with the answer. Sets are stored sorted and de-duplicated so that
// equal sets compare equal whatever order they were built in.

static class ShapeUtil {
    public static string[] SortedSet(IEnumerable<string> items) =>
        items.Distinct(StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToArray();

    public static int HashOf(IEnumerable<string> items) {
        var h = new HashCode();
        foreach (var s in items) h.Add(s, StringComparer.Ordinal);
        return h.ToHashCode();
    }
}

/// <summary>A set of node ids.</summary>
sealed class NodeSet : IEquatable<NodeSet> {
    [JsonPropertyName("nodes")] public string[] Nodes { get; }
    public NodeSet(IEnumerable<string> nodes) { Nodes = ShapeUtil.SortedSet(nodes); }
    public bool Equals(NodeSet? other) => other != null && Nodes.SequenceEqual(other.Nodes, StringComparer.Ordinal);
    public override bool Equals(object? obj) => Equals(obj as NodeSet);
    public override int GetHashCode() => ShapeUtil.HashOf(Nodes);
}

/// <summary>A set of edge keys.</summary>
sealed class EdgeSet : IEquatable<EdgeSet> {
    [JsonPropertyName("edges")] public string[] Edges { get; }
    public EdgeSet(IEnumerable<string> edges) { Edges = ShapeUtil.SortedSet(edges); }
    public bool Equals(EdgeSet? other) => other != null && Edges.SequenceEqual(other.Edges, StringComparer.Ordinal);
    public override bool Equals(object? obj) => Equals(obj as EdgeSet);
    public override int GetHashCode() => ShapeUtil.HashOf(Edges);
}

/// <summary>A grouping of nodes: node id to group index (colorings, partitions, cuts).</summary>
sealed class Groups : IEquatable<Groups> {
    [JsonPropertyName("groups")] public IReadOnlyDictionary<string, int> Of { get; }
    public Groups(IEnumerable<KeyValuePair<string, int>> groups) {
        Of = groups.OrderBy(p => p.Key, StringComparer.Ordinal).ToDictionary(p => p.Key, p => p.Value);
    }
    public bool Equals(Groups? other) =>
        other != null && Of.Count == other.Of.Count && Of.All(p => other.Of.TryGetValue(p.Key, out var v) && v == p.Value);
    public override bool Equals(object? obj) => Equals(obj as Groups);
    public override int GetHashCode() => ShapeUtil.HashOf(Of.Select(p => $"{p.Key}={p.Value}"));
}

/// <summary>An ordered list of node ids (a tour or path).</summary>
sealed class Tour : IEquatable<Tour> {
    [JsonPropertyName("order")] public string[] Order { get; }
    public Tour(IEnumerable<string> order) { Order = order.ToArray(); }
    public bool Equals(Tour? other) => other != null && Order.SequenceEqual(other.Order, StringComparer.Ordinal);
    public override bool Equals(object? obj) => Equals(obj as Tour);
    public override int GetHashCode() => ShapeUtil.HashOf(Order);
}

/// <summary>A variable-to-value assignment, values as strings.</summary>
sealed class Assignment : IEquatable<Assignment> {
    [JsonPropertyName("values")] public IReadOnlyDictionary<string, string> Values { get; }
    public Assignment(IEnumerable<KeyValuePair<string, string>> values) {
        Values = values.OrderBy(p => p.Key, StringComparer.Ordinal).ToDictionary(p => p.Key, p => p.Value);
    }
    public bool Equals(Assignment? other) =>
        other != null && Values.Count == other.Values.Count
            && Values.All(p => other.Values.TryGetValue(p.Key, out var v) && v == p.Value);
    public override bool Equals(object? obj) => Equals(obj as Assignment);
    public override int GetHashCode() => ShapeUtil.HashOf(Values.Select(p => $"{p.Key}={p.Value}"));
}

/// <summary>The sets (by index or id) chosen so far, as in set cover.</summary>
sealed class ChosenSets : IEquatable<ChosenSets> {
    [JsonPropertyName("sets")] public string[] Sets { get; }
    public ChosenSets(IEnumerable<string> sets) { Sets = ShapeUtil.SortedSet(sets); }
    public bool Equals(ChosenSets? other) => other != null && Sets.SequenceEqual(other.Sets, StringComparer.Ordinal);
    public override bool Equals(object? obj) => Equals(obj as ChosenSets);
    public override int GetHashCode() => ShapeUtil.HashOf(Sets);
}

/// <summary>The automaton states the run is in, and how many input symbols have been consumed.</summary>
sealed class ActiveStates : IEquatable<ActiveStates> {
    [JsonPropertyName("states")] public string[] States { get; }
    [JsonPropertyName("position")] public int Position { get; }
    public ActiveStates(IEnumerable<string> states, int position = 0) {
        States = ShapeUtil.SortedSet(states);
        Position = position;
    }
    public bool Equals(ActiveStates? other) =>
        other != null && Position == other.Position && States.SequenceEqual(other.States, StringComparer.Ordinal);
    public override bool Equals(object? obj) => Equals(obj as ActiveStates);
    public override int GetHashCode() => HashCode.Combine(ShapeUtil.HashOf(States), Position);
}

/// <summary>
/// What one construction step added to the target while a reduction builds it. Unlike the answer shapes
/// above this is a DELTA, not the whole target so far: a target can have quadratically many edges, and
/// repeating them in every step would bloat the response. Replaying the steps in order (adding each step's
/// <see cref="Nodes"/> and <see cref="Edges"/> to what came before) reproduces the target exactly, and an
/// edge's two ends are always added by its own step or an earlier one.
/// Used as <c>SolverStep&lt;BuildDelta&gt;</c>: the step's <c>Focus</c> holds the TARGET node ids being looked at,
/// <see cref="SourceFocus"/> the SOURCE ids they came from. Sides are told apart by field, so ids carry no
/// "s:"/"t:" prefix. Source ids are the stable ids of the Gadget payload (graph node names; formulas "c{i}",
/// "c{i}-{j}", "v:{name}"). Target nodes are named as the target's picture names them; target edges are
/// <see cref="BuildEdge"/>s, which carry their two ends, so no client ever splits an id.
/// <para>
/// "Nodes" are the target picture's pieces that stand on their own, "edges" the pieces drawn between two of them. What they
/// are depends on the target's picture: a Graph's nodes and edges; a Grouped Graph's clauses (<c>c{i}</c>), literals
/// (<c>c{i}-{j}</c>) and variables (<c>v:{name}</c>) as nodes, and its complement and clause edges (<c>a~b</c>) as edges; a
/// Tripartite Matching's elements (<c>x{i}</c>, <c>y{i}</c>, <c>z{i}</c>) and triples (<c>T{i}</c>) as nodes, since a triple
/// runs through three elements the payload already names; a Table's rows (<c>r{i}</c>) and columns (<c>c{j}</c>) as nodes,
/// whose cells appear with them. Whatever the picture, every piece of its payload is added by exactly one step.
/// </para>
/// </summary>
sealed record BuildDelta {
    /// <summary>Source ids this step was built from.</summary>
    [JsonPropertyName("sourceFocus")] public string[] SourceFocus { get; init; } = Array.Empty<string>();
    /// <summary>Target node ids added by this step.</summary>
    [JsonPropertyName("nodes")] public string[] Nodes { get; init; } = Array.Empty<string>();
    /// <summary>Target edges added by this step, sorted by id.</summary>
    [JsonPropertyName("edges")] public BuildEdge[] Edges { get; init; } = Array.Empty<BuildEdge>();
    /// <summary>Pairs of target nodes the rule considered and left without an edge (shown as "no edge here"), sorted by id.</summary>
    [JsonPropertyName("skipped")] public BuildEdge[] Skipped { get; init; } = Array.Empty<BuildEdge>();
    /// <summary>Indices into the reduction's gadget list of the gadgets this step builds.</summary>
    [JsonPropertyName("gadgets")] public int[] Gadgets { get; init; } = Array.Empty<int>();
    /// <summary>The target problem's bound (K) once the reduction has fixed it; omitted from JSON before that.</summary>
    [JsonPropertyName("bound")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Bound { get; init; }
}

/// <summary>
/// An undirected edge of a construction step: <see cref="Id"/> is the id the target's Graph picture gives the same edge
/// (<c>"a-b"</c> with a &lt; b by ordinal comparison, separators in a name escaped), and <see cref="A"/> / <see cref="B"/>
/// are its two ends, in that order. The id is a label, never something to split.
/// </summary>
sealed record BuildEdge {
    /// <summary>The edge's id in the target's picture.</summary>
    [JsonPropertyName("id")] public string Id { get; }
    /// <summary>The end that comes first by ordinal comparison.</summary>
    [JsonPropertyName("a")] public string A { get; }
    /// <summary>The other end.</summary>
    [JsonPropertyName("b")] public string B { get; }

    /// <summary>The edge between <paramref name="u"/> and <paramref name="v"/>, in either order.</summary>
    public BuildEdge(string u, string v) {
        (A, B) = string.CompareOrdinal(u, v) <= 0 ? (u, v) : (v, u);
        Id = API.Interfaces.Graphs.GraphSubsetFrames.EdgeId(A, B);
    }

    /// <summary>
    /// An edge of a picture that writes its edge ids its own way (the Grouped Graph's <c>c0-1~c2-0</c>): the id and the two
    /// ends exactly as that picture gives them.
    /// </summary>
    public BuildEdge(string id, string a, string b) {
        Id = id;
        A = a;
        B = b;
    }

    /// <summary>Edges sorted by id, so a step lists them the same way every time.</summary>
    public static BuildEdge[] Sorted(IEnumerable<BuildEdge> edges) => edges.OrderBy(e => e.Id, StringComparer.Ordinal).ToArray();
}

/// <summary>
/// The triples a 3-Dimensional Matching solver has chosen so far, by id. Triples are named by their position in M,
/// <c>T{i+1}</c> for the triple at 0-based index i (see <c>TripartiteFrames.TripleId</c>), never by their names, so a triple
/// listed twice stays two triples and no id needs escaping. A set: stored sorted and de-duplicated. The answer is the last
/// one: the matching the solver returns, or no triples when there is none.
/// </summary>
sealed class ChosenTriples : IEquatable<ChosenTriples> {
    [JsonPropertyName("triples")] public string[] Triples { get; }
    public ChosenTriples(IEnumerable<string> triples) { Triples = ShapeUtil.SortedSet(triples); }
    public bool Equals(ChosenTriples? other) => other != null && Triples.SequenceEqual(other.Triples, StringComparer.Ordinal);
    public override bool Equals(object? obj) => Equals(obj as ChosenTriples);
    public override int GetHashCode() => ShapeUtil.HashOf(Triples);
}

/// <summary>
/// A table a dynamic-programming solver fills (Edit Distance's grid), as far as it has got. Cells are named by position,
/// <c>r{i}c{j}</c> for row i and column j (0-based, see <c>TableFrames.CellId</c>), never by their contents, so no id
/// needs escaping.
/// <list type="bullet">
/// <item><see cref="Values"/>: the text of every cell filled so far; a cell with no entry is still empty.</item>
/// <item><see cref="Reads"/>: the cells the latest fill read to work out its value, the one whose value it took first.
/// Empty for a step that fills nothing, or that fills cells without reading others (a base row).</item>
/// <item><see cref="Path"/>: the cells the trace-back has taken, in the order it took them (from the answer's cell back
/// toward the start). Empty until the trace-back begins.</item>
/// </list>
/// The answer is the last one: the filled table and the path through it that spells out the answer.
/// </summary>
sealed class TableCells : IEquatable<TableCells> {
    [JsonPropertyName("values")] public IReadOnlyDictionary<string, string> Values { get; }
    [JsonPropertyName("reads")] public string[] Reads { get; }
    [JsonPropertyName("path")] public string[] Path { get; }
    public TableCells(IEnumerable<KeyValuePair<string, string>> values, IEnumerable<string> reads, IEnumerable<string> path) {
        Values = values.OrderBy(p => p.Key, StringComparer.Ordinal).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        Reads = reads.ToArray();
        Path = path.ToArray();
    }
    public bool Equals(TableCells? other) =>
        other != null && Reads.SequenceEqual(other.Reads, StringComparer.Ordinal) && Path.SequenceEqual(other.Path, StringComparer.Ordinal)
            && Values.Count == other.Values.Count && Values.All(p => other.Values.TryGetValue(p.Key, out var v) && v == p.Value);
    public override bool Equals(object? obj) => Equals(obj as TableCells);
    public override int GetHashCode() =>
        HashCode.Combine(ShapeUtil.HashOf(Reads), ShapeUtil.HashOf(Path), ShapeUtil.HashOf(Values.Select(p => $"{p.Key}={p.Value}")));
}

/// <summary>Empty answers of each shape.</summary>
static class Shapes {
    /// <summary>
    /// The empty answer of <paramref name="shape"/> (no nodes, no edges, no groups, ...), or null when the shape has no
    /// answer-like empty value: <see cref="ActiveStates"/> is where an automaton run is, not a candidate answer, and a
    /// shape not listed here is unknown.
    /// </summary>
    public static object? EmptyAnswer(Type shape) =>
        shape == typeof(NodeSet) ? new NodeSet([])
        : shape == typeof(EdgeSet) ? new EdgeSet([])
        : shape == typeof(Groups) ? new Groups([])
        : shape == typeof(Tour) ? new Tour([])
        : shape == typeof(Assignment) ? new Assignment([])
        : shape == typeof(ChosenSets) ? new ChosenSets([])
        : shape == typeof(ChosenTriples) ? new ChosenTriples([])
        : shape == typeof(TableCells) ? new TableCells([], [], [])
        : null;
}
