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
/// One leg of an augmenting path: the edge (its picture id), whether the path runs along it (true) or against it, taking
/// back flow sent earlier (false), and how many units the leg moved.
/// </summary>
sealed record FlowArc(
    [property: JsonPropertyName("edge")] string Edge,
    [property: JsonPropertyName("forward")] bool Forward,
    [property: JsonPropertyName("units")] int Units);

/// <summary>
/// A flow so far (Ford-Fulkerson and its kin): the units on each edge that carries any (edge id to units), the augmenting
/// path just used (empty when none), the flow's value, and, once no path is left, the nodes the source can still reach
/// in the residual graph (the S side of the minimum cut; empty until then). Edge ids are the picture's
/// (<c>FlowFrames.ArcId</c>), node names as the instance writes them.
/// </summary>
sealed class NetworkFlow : IEquatable<NetworkFlow> {
    [JsonPropertyName("flow")] public IReadOnlyDictionary<string, int> Flow { get; }
    [JsonPropertyName("path")] public FlowArc[] Path { get; }
    [JsonPropertyName("value")] public int Value { get; }
    [JsonPropertyName("side")] public string[] Side { get; }
    public NetworkFlow(IEnumerable<KeyValuePair<string, int>> flow, IEnumerable<FlowArc> path, int value, IEnumerable<string> side) {
        Flow = flow.Where(p => p.Value != 0).OrderBy(p => p.Key, StringComparer.Ordinal).ToDictionary(p => p.Key, p => p.Value);
        Path = path.ToArray();
        Value = value;
        Side = ShapeUtil.SortedSet(side);
    }
    public bool Equals(NetworkFlow? other) =>
        other != null && Value == other.Value && Path.SequenceEqual(other.Path) && Side.SequenceEqual(other.Side, StringComparer.Ordinal)
            && Flow.Count == other.Flow.Count && Flow.All(p => other.Flow.TryGetValue(p.Key, out var u) && u == p.Value);
    public override bool Equals(object? obj) => Equals(obj as NetworkFlow);
    public override int GetHashCode() =>
        HashCode.Combine(Value, ShapeUtil.HashOf(Side), ShapeUtil.HashOf(Path.Select(a => $"{a.Edge}:{a.Forward}:{a.Units}")),
            ShapeUtil.HashOf(Flow.Select(p => $"{p.Key}={p.Value}")));
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
        : shape == typeof(NetworkFlow) ? new NetworkFlow([], [], 0, [])
        : null;
}
