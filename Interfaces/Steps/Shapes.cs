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
/// A shortest-path search so far (Dijkstra): the best distance known to each node reached from the source, the node each
/// was reached from, and the nodes whose distance is settled for good. A node that has not been reached has no entry in
/// <see cref="Dist"/> (its distance is ∞). The answer is the last one: the shortest-path tree is <see cref="Pred"/>.
/// </summary>
sealed class Distances : IEquatable<Distances> {
    [JsonPropertyName("dist")] public IReadOnlyDictionary<string, int> Dist { get; }
    [JsonPropertyName("pred")] public IReadOnlyDictionary<string, string> Pred { get; }
    [JsonPropertyName("settled")] public string[] Settled { get; }
    public Distances(IEnumerable<KeyValuePair<string, int>> dist, IEnumerable<KeyValuePair<string, string>> pred, IEnumerable<string> settled) {
        Dist = dist.OrderBy(p => p.Key, StringComparer.Ordinal).ToDictionary(p => p.Key, p => p.Value);
        Pred = pred.OrderBy(p => p.Key, StringComparer.Ordinal).ToDictionary(p => p.Key, p => p.Value);
        Settled = ShapeUtil.SortedSet(settled);
    }
    public bool Equals(Distances? other) =>
        other != null && Settled.SequenceEqual(other.Settled, StringComparer.Ordinal)
            && Dist.Count == other.Dist.Count && Dist.All(p => other.Dist.TryGetValue(p.Key, out var d) && d == p.Value)
            && Pred.Count == other.Pred.Count && Pred.All(p => other.Pred.TryGetValue(p.Key, out var u) && u == p.Value);
    public override bool Equals(object? obj) => Equals(obj as Distances);
    public override int GetHashCode() =>
        HashCode.Combine(ShapeUtil.HashOf(Settled), ShapeUtil.HashOf(Dist.Select(p => $"{p.Key}={p.Value}")), ShapeUtil.HashOf(Pred.Select(p => $"{p.Key}={p.Value}")));
}

/// <summary>
/// Strongly connected components found so far, with the depth-first bookkeeping that finds them (Kosaraju): node id to
/// component index (0-based, in the order the components were found), the nodes visited by the first pass, and the order
/// in which that pass finished them (the n-th finished node has finish number n).
/// </summary>
sealed class Components : IEquatable<Components> {
    [JsonPropertyName("groups")] public IReadOnlyDictionary<string, int> Of { get; }
    [JsonPropertyName("visited")] public string[] Visited { get; }
    [JsonPropertyName("finished")] public string[] Finished { get; }
    public Components(IEnumerable<KeyValuePair<string, int>> groups, IEnumerable<string> visited, IEnumerable<string> finished) {
        Of = groups.OrderBy(p => p.Key, StringComparer.Ordinal).ToDictionary(p => p.Key, p => p.Value);
        Visited = ShapeUtil.SortedSet(visited);
        Finished = finished.ToArray();
    }
    public bool Equals(Components? other) =>
        other != null && Visited.SequenceEqual(other.Visited, StringComparer.Ordinal) && Finished.SequenceEqual(other.Finished, StringComparer.Ordinal)
            && Of.Count == other.Of.Count && Of.All(p => other.Of.TryGetValue(p.Key, out var g) && g == p.Value);
    public override bool Equals(object? obj) => Equals(obj as Components);
    public override int GetHashCode() =>
        HashCode.Combine(ShapeUtil.HashOf(Visited), ShapeUtil.HashOf(Finished), ShapeUtil.HashOf(Of.Select(p => $"{p.Key}={p.Value}")));
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

/// <summary>
/// Which container each item is in (packing problems: Partition, Subset Sum, Knapsack, Bin Packing). Items and containers
/// are named by position, never by their numbers, so two items of the same size stay two items: <c>Assign[i]</c> is the
/// 0-based container of item <c>i</c> (instance order), or -1 when item <c>i</c> is left out. An item past the end of
/// <c>Assign</c> is left out too, so trailing -1s are dropped (any negative number is read as -1) and equal placements
/// compare equal however they were written.
/// </summary>
sealed class Placement : IEquatable<Placement> {
    [JsonPropertyName("assign")] public int[] Assign { get; }
    public Placement(IEnumerable<int> assign) {
        var a = assign.Select(c => c < 0 ? -1 : c).ToList();
        while (a.Count > 0 && a[^1] == -1) a.RemoveAt(a.Count - 1);
        Assign = a.ToArray();
    }
    /// <summary>The container of item <paramref name="item"/>, or -1 when it is left out.</summary>
    public int Of(int item) => item >= 0 && item < Assign.Length ? Assign[item] : -1;
    public bool Equals(Placement? other) => other != null && Assign.SequenceEqual(other.Assign);
    public override bool Equals(object? obj) => Equals(obj as Placement);
    public override int GetHashCode() => ShapeUtil.HashOf(Assign.Select(c => c.ToString(System.Globalization.CultureInfo.InvariantCulture)));
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

/// <summary>
/// A square a board solver ruled out, and the square that rules it out: an N-Queens square skipped because a queen attacks
/// it, or a Sudoku clue that repeats another. Both are cell ids, <c>r{i}c{j}</c> (see <c>BoardFrames.CellId</c>).
/// </summary>
sealed record BoardConflict(
    [property: JsonPropertyName("cell")] string Cell,
    [property: JsonPropertyName("with")] string With);

/// <summary>
/// A board as a puzzle solver sees it (Sudoku, N-Queens), as far as it has got. Squares are named by position,
/// <c>r{i}c{j}</c> for row i and column j (0-based, see <c>BoardFrames.CellId</c>), never by their contents, so no id
/// needs escaping.
/// <list type="bullet">
/// <item><see cref="Values"/>: what is on every square that holds something, the instance's clues included: a digit
/// (<c>"5"</c>), or a piece's mark (<c>"Q"</c> for a queen). A square with no entry is empty.</item>
/// <item><see cref="Conflicts"/>: the squares this step ruled out, each with the square that rules it out. Empty for most
/// steps.</item>
/// </list>
/// The answer is the last one: the finished board, with no conflicts.
/// </summary>
sealed class BoardCells : IEquatable<BoardCells> {
    [JsonPropertyName("values")] public IReadOnlyDictionary<string, string> Values { get; }
    [JsonPropertyName("conflicts")] public BoardConflict[] Conflicts { get; }
    public BoardCells(IEnumerable<KeyValuePair<string, string>> values, IEnumerable<BoardConflict> conflicts) {
        Values = values.OrderBy(p => p.Key, StringComparer.Ordinal).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        Conflicts = conflicts.ToArray();
    }
    public bool Equals(BoardCells? other) =>
        other != null && Conflicts.SequenceEqual(other.Conflicts)
            && Values.Count == other.Values.Count && Values.All(p => other.Values.TryGetValue(p.Key, out var v) && v == p.Value);
    public override bool Equals(object? obj) => Equals(obj as BoardCells);
    public override int GetHashCode() =>
        HashCode.Combine(ShapeUtil.HashOf(Conflicts.Select(c => $"{c.Cell}>{c.With}")), ShapeUtil.HashOf(Values.Select(p => $"{p.Key}={p.Value}")));
}

/// <summary>
/// The order a one-machine scheduler runs its jobs in (Job Sequencing), as far as it has got. Jobs are named by position,
/// <c>J{i+1}</c> for the job at 0-based index i (see <c>ScheduleFrames.JobId</c>), never by their numbers, so two jobs with
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
/// A convex hull as a divide-and-conquer solver builds it (Convex Hull), as far as it has got. Points are named by position,
/// <c>P{i+1}</c> for the point at 0-based index i of the instance (see <c>GeometryFrames.PointId</c>); a point listed twice is
/// named after its first copy. Every hull lists its corners counterclockwise.
/// <list type="bullet">
/// <item><see cref="Pieces"/>: the finished hulls waiting to be merged, left to right (not the two being merged).</item>
/// <item><see cref="Left"/> and <see cref="Right"/>: the two hulls being merged now; empty when no merge is under way.</item>
/// <item><see cref="Chain"/>: the chain of corners the merge has built so far, in the order it walked them (the bottom
/// edge left to right, then the top edge back); empty when no merge is under way.</item>
/// <item><see cref="Dropped"/>: the points found not to be corners so far (inside a hull, or in the middle of an edge). A
/// set.</item>
/// </list>
/// The answer is the last one: the hull as the only piece, and every other point dropped.
/// </summary>
sealed class HullBuild : IEquatable<HullBuild> {
    [JsonPropertyName("pieces")] public string[][] Pieces { get; }
    [JsonPropertyName("left")] public string[] Left { get; }
    [JsonPropertyName("right")] public string[] Right { get; }
    [JsonPropertyName("chain")] public string[] Chain { get; }
    [JsonPropertyName("dropped")] public string[] Dropped { get; }
    public HullBuild(IEnumerable<IEnumerable<string>> pieces, IEnumerable<string> left, IEnumerable<string> right, IEnumerable<string> chain, IEnumerable<string> dropped) {
        Pieces = pieces.Select(p => p.ToArray()).ToArray();
        Left = left.ToArray();
        Right = right.ToArray();
        Chain = chain.ToArray();
        Dropped = ShapeUtil.SortedSet(dropped);
    }
    public bool Equals(HullBuild? other) =>
        other != null && Pieces.Length == other.Pieces.Length && Pieces.Zip(other.Pieces).All(p => p.First.SequenceEqual(p.Second, StringComparer.Ordinal))
            && Left.SequenceEqual(other.Left, StringComparer.Ordinal) && Right.SequenceEqual(other.Right, StringComparer.Ordinal)
            && Chain.SequenceEqual(other.Chain, StringComparer.Ordinal) && Dropped.SequenceEqual(other.Dropped, StringComparer.Ordinal);
    public override bool Equals(object? obj) => Equals(obj as HullBuild);
    public override int GetHashCode() =>
        HashCode.Combine(ShapeUtil.HashOf(Pieces.Select(p => string.Join(",", p))), ShapeUtil.HashOf(Chain), ShapeUtil.HashOf(Dropped));
}

/// <summary>
/// One merge of Huffman's algorithm: the new node, the two trees it joins (left gets bit 0, right bit 1) and its weight. Leaves
/// are characters by position, <c>c{i+1}</c> for the i-th distinct character in character order; merged nodes are
/// <c>n{k}</c>, the k-th merge (see <c>CodeTreeFrames</c>).
/// </summary>
sealed record HuffmanMerge(
    [property: JsonPropertyName("node")] string Node,
    [property: JsonPropertyName("left")] string Left,
    [property: JsonPropertyName("right")] string Right,
    [property: JsonPropertyName("weight")] int Weight);

/// <summary>
/// A Huffman code as the greedy builds it (Lossless Data Compression), as far as it has got.
/// <list type="bullet">
/// <item><see cref="Merges"/>: the merges made so far, in order. The trees left are the characters and merged nodes that no
/// merge has used yet.</item>
/// <item><see cref="Codes"/>: each character's code (by its leaf id), once the tree is finished and read; empty until
/// then.</item>
/// </list>
/// The answer is the last one: every merge, and every character's code.
/// </summary>
sealed class HuffmanForest : IEquatable<HuffmanForest> {
    [JsonPropertyName("merges")] public HuffmanMerge[] Merges { get; }
    [JsonPropertyName("codes")] public IReadOnlyDictionary<string, string> Codes { get; }
    public HuffmanForest(IEnumerable<HuffmanMerge> merges, IEnumerable<KeyValuePair<string, string>> codes) {
        Merges = merges.ToArray();
        Codes = codes.OrderBy(p => p.Key, StringComparer.Ordinal).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
    }
    public bool Equals(HuffmanForest? other) =>
        other != null && Merges.SequenceEqual(other.Merges)
            && Codes.Count == other.Codes.Count && Codes.All(p => other.Codes.TryGetValue(p.Key, out var v) && v == p.Value);
    public override bool Equals(object? obj) => Equals(obj as HuffmanForest);
    public override int GetHashCode() => HashCode.Combine(Merges.Length, ShapeUtil.HashOf(Codes.Select(p => $"{p.Key}={p.Value}")));
}

/// <summary>One classical call to an oracle: the input x (its bits, read as a number) and the output f(x).</summary>
sealed record OracleQuery(
    [property: JsonPropertyName("input")] int Input,
    [property: JsonPropertyName("output")] long Output);

/// <summary>
/// How far a quantum-algorithm problem's run has got (Deutsch, Deutsch-Jozsa, Bernstein-Vazirani, Simon's Problem,
/// Unstructured Search, Prime Factorization), as the Quantum Circuit picture draws it (see <c>QuantumCircuitFrames</c>).
/// <list type="bullet">
/// <item><see cref="Applied"/>: how many columns of the problem's circuit have been applied (0: none). A classical solver
/// never applies any.</item>
/// <item><see cref="Queries"/>: the classical oracle calls made so far, in order.</item>
/// <item><see cref="Outcome"/>: the value read from the measured qubits (the first measured qubit is the most significant
/// bit), once it is known; null before that, and when the answer does not say which value was read.</item>
/// <item><see cref="Counts"/>: how often each outcome came up, when a quantum server reported it; empty otherwise.</item>
/// <item><see cref="Result"/>: the answer as the solver has it so far (<c>balanced</c>, <c>(1,0,1)</c>, <c>(2,2)</c>);
/// null until it has one. The last step's is the solver's answer.</item>
/// </list>
/// </summary>
sealed class QuantumProgress : IEquatable<QuantumProgress> {
    [JsonPropertyName("applied")] public int Applied { get; }
    [JsonPropertyName("queries")] public OracleQuery[] Queries { get; }
    [JsonPropertyName("outcome")] public long? Outcome { get; }
    [JsonPropertyName("counts")] public IReadOnlyDictionary<string, long> Counts { get; }
    [JsonPropertyName("result")] public string? Result { get; }

    public QuantumProgress(int applied = 0, IEnumerable<OracleQuery>? queries = null, long? outcome = null,
        IEnumerable<KeyValuePair<string, long>>? counts = null, string? result = null) {
        Applied = applied;
        Queries = (queries ?? []).ToArray();
        Outcome = outcome;
        Counts = (counts ?? []).OrderBy(p => p.Key, StringComparer.Ordinal).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        Result = result;
    }

    public bool Equals(QuantumProgress? other) =>
        other != null && Applied == other.Applied && Queries.SequenceEqual(other.Queries) && Outcome == other.Outcome
            && Counts.Count == other.Counts.Count && Counts.All(p => other.Counts.TryGetValue(p.Key, out var v) && v == p.Value)
            && Result == other.Result;
    public override bool Equals(object? obj) => Equals(obj as QuantumProgress);
    public override int GetHashCode() => HashCode.Combine(Applied, Queries.Length, Outcome, Counts.Count, Result);
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
        : shape == typeof(Distances) ? new Distances([], [], [])
        : shape == typeof(Components) ? new Components([], [], [])
        : shape == typeof(NetworkFlow) ? new NetworkFlow([], [], 0, [])
        : shape == typeof(Placement) ? new Placement([])
        : shape == typeof(TableCells) ? new TableCells([], [], [])
        : shape == typeof(BoardCells) ? new BoardCells([], [])
        : shape == typeof(JobOrder) ? new JobOrder([])
        : shape == typeof(PumpPlan) ? new PumpPlan([], [], null)
        : shape == typeof(ChosenTriples) ? new ChosenTriples([])
        : shape == typeof(HullBuild) ? new HullBuild([], [], [], [], [])
        : shape == typeof(HuffmanForest) ? new HuffmanForest([], [])
        : shape == typeof(QuantumProgress) ? new QuantumProgress()
        : null;
}
