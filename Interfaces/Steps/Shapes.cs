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
/// The order a one-machine scheduler runs its jobs in (Job Sequencing), as far as it has got. Jobs are named by position,
/// <c>J{i}</c> for the job at 0-based index i (see <c>ScheduleFrames.JobId</c>), never by their numbers, so two jobs with
/// the same time, deadline and penalty stay two jobs. <see cref="Order"/> lists job ids in the order they run, from time 0;
/// a job not listed is not placed. The answer is the last one: every job, in the order the solver returns.
/// </summary>
sealed class JobOrder : IEquatable<JobOrder> {
    [JsonPropertyName("order")] public string[] Order { get; }
    public JobOrder(IEnumerable<string> order) { Order = order.ToArray(); }
    public bool Equals(JobOrder? other) => other != null && Order.SequenceEqual(other.Order, StringComparer.Ordinal);
    public override bool Equals(object? obj) => Equals(obj as JobOrder);
    public override int GetHashCode() => ShapeUtil.HashOf(Order);
}

/// <summary>
/// One hour of a dynamic program's forward pass over a tank (Pump Scheduling): after that hour, how many (tank level, pumps
/// on) states it can still reach, the lowest and highest of their tank levels in gallons, and the best score among them (the
/// cheapest cost so far for Cost Minimization, the most water stored so far, in gallon-hours, for Emergency Resilience).
/// </summary>
sealed record ReachedHour(
    [property: JsonPropertyName("states")] int States,
    [property: JsonPropertyName("low")] double Low,
    [property: JsonPropertyName("high")] double High,
    [property: JsonPropertyName("best")] double Best);

/// <summary>
/// A pump schedule as a dynamic program builds it (Pump Scheduling), as far as it has got. Pumps are named by position,
/// <c>P{i+1}</c> (see <c>ScheduleFrames.PumpId</c>), never by their names, so two pumps with one name stay two pumps.
/// <list type="bullet">
/// <item><see cref="Reached"/>: one entry per hour the forward pass has finished, hour 0 first; it stops early at an hour
/// no state survives.</item>
/// <item><see cref="On"/>: the schedule committed so far: one entry per hour (24 once the trace back has run), each the ids
/// of the pumps on that hour, in pump order. Empty until then.</item>
/// <item><see cref="Budget"/>: the budget the schedule must fit (Emergency Resilience, once the solver has fixed it); null
/// for Cost Minimization.</item>
/// </list>
/// The answer is the last one: the schedule the solver returns, and the forward pass that found it.
/// </summary>
sealed class PumpPlan : IEquatable<PumpPlan> {
    [JsonPropertyName("reached")] public ReachedHour[] Reached { get; }
    [JsonPropertyName("on")] public string[][] On { get; }
    [JsonPropertyName("budget")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Budget { get; }
    public PumpPlan(IEnumerable<ReachedHour> reached, IEnumerable<IEnumerable<string>> on, double? budget) {
        Reached = reached.ToArray();
        On = on.Select(hour => hour.ToArray()).ToArray();
        Budget = budget;
    }
    public bool Equals(PumpPlan? other) =>
        other != null && Budget == other.Budget && Reached.SequenceEqual(other.Reached)
            && On.Length == other.On.Length && On.Zip(other.On).All(p => p.First.SequenceEqual(p.Second, StringComparer.Ordinal));
    public override bool Equals(object? obj) => Equals(obj as PumpPlan);
    public override int GetHashCode() =>
        HashCode.Combine(Budget, Reached.Length, ShapeUtil.HashOf(On.Select(hour => string.Join(",", hour))));
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
        : shape == typeof(JobOrder) ? new JobOrder([])
        : shape == typeof(PumpPlan) ? new PumpPlan([], [], null)
        : null;
}
