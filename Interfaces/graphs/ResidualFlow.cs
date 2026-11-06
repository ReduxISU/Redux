using API.Interfaces.Steps;

namespace API.Interfaces.Graphs;

/// <summary>
/// What one push along an augmenting path did, as the Flow Network picture reads it right after the push. A max-flow solver
/// that works on a residual graph gets it from <see cref="ResidualFlow.Push"/>, for its step and its why.
/// </summary>
/// <param name="Along">The path, with an arrow per hop: <c>s → a ⇠ b → t</c> (<c>⇠</c>: the hop only took back flow).</param>
/// <param name="Legs">The edges the push ran along, each with the units it moved and whether it ran forward.</param>
/// <param name="Focus">The ids the step is about: the source, then each hop's edges and the node it reaches.</param>
/// <param name="Back">The edges it ran backward over (<c>b → a</c>), taking back flow sent along them earlier.</param>
/// <param name="Full">The edges it filled (<c>a → b</c>).</param>
sealed record FlowPush(string Along, IReadOnlyList<FlowArc> Legs, string[] Focus, IReadOnlyList<string> Back, IReadOnlyList<string> Full) {
    /// <summary>No push: before the first one, and for a solve that records nothing.</summary>
    public static readonly FlowPush None = new("", [], [], [], []);
}

/// <summary>
/// Reads the flow off a max-flow solver's residual graph (<c>residual[u][v]</c>: the room left from u to v) for the Flow
/// Network picture. The residual graph merges parallel edges and both directions of a pair, so it holds only the net flow
/// from u to v: capacity(u,v) - residual[u][v]. That net flow is drawn on the edge it runs along (u to v when positive, v to
/// u when negative), which is a valid flow since 0 &lt;= net &lt;= capacity(u,v) and 0 &lt;= -net &lt;= capacity(v,u).
/// </summary>
sealed class ResidualFlow {
    // The summed capacity of every listed (from, to) pair, in listed order.
    private readonly Dictionary<(string From, string To), int> _capacity = new();

    /// <param name="edges">The network's edges as the instance lists them, parallel edges included.</param>
    public ResidualFlow(IEnumerable<(string From, string To, int Capacity)> edges) {
        foreach (var (from, to, capacity) in edges)
            _capacity[(from, to)] = _capacity.GetValueOrDefault((from, to)) + capacity;
    }

    private int Capacity(string from, string to) => _capacity.GetValueOrDefault((from, to));

    // The net flow from u to v.
    private int Net(Dictionary<string, Dictionary<string, int>> residual, string u, string v) =>
        Capacity(u, v) - residual[u].GetValueOrDefault(v);

    private int Units(Dictionary<string, Dictionary<string, int>> residual, string from, string to) =>
        from == to ? 0 : Math.Max(0, Net(residual, from, to));

    /// <summary>The flow on every edge as the residual graph holds it now, with the push's legs, the flow's value and the cut's S side.</summary>
    public NetworkFlow Snapshot(Dictionary<string, Dictionary<string, int>> residual, IEnumerable<FlowArc> legs, int value, IEnumerable<string> side) =>
        new(_capacity.Keys.Select(e => KeyValuePair.Create(FlowFrames.ArcId(e.From, e.To), Units(residual, e.From, e.To))), legs, value, side);

    /// <summary>How many edges run from the S side to the other side.</summary>
    public int EdgesLeaving(ISet<string> side) => _capacity.Keys.Count(e => side.Contains(e.From) && !side.Contains(e.To));

    /// <summary>
    /// What pushing <paramref name="pushed"/> units from <paramref name="s"/> to <paramref name="t"/> along the BFS tree
    /// <paramref name="parent"/> did, read after the push. Each hop x to y moved <c>pushed</c> units: first taking back what y
    /// had sent to x, then along x to y; the net flow from x to y is now <c>pushed</c> more than it was.
    /// </summary>
    public FlowPush Push(Dictionary<string, Dictionary<string, int>> residual, Dictionary<string, string> parent, string s, string t, int pushed) {
        var nodes = new List<string>();
        for (string node = t; node != s; node = parent[node]) nodes.Add(node);
        nodes.Add(s);
        nodes.Reverse();

        string along = s;
        var legs = new List<FlowArc>();
        var focus = new List<string> { FlowFrames.NodeId(s) };
        var back = new List<string>();
        var full = new List<string>();
        for (int i = 1; i < nodes.Count; i++) {
            string x = nodes[i - 1], y = nodes[i];
            int before = Net(residual, x, y) - pushed;
            int backward = Math.Min(pushed, Math.Max(0, -before));
            int ahead = pushed - backward;
            along += (ahead > 0 ? " → " : " ⇠ ") + y;
            if (backward > 0) {
                legs.Add(new FlowArc(FlowFrames.ArcId(y, x), false, backward));
                focus.Add(FlowFrames.ArcId(y, x));
                back.Add($"{y} → {x}");
            }
            if (ahead > 0) {
                legs.Add(new FlowArc(FlowFrames.ArcId(x, y), true, ahead));
                focus.Add(FlowFrames.ArcId(x, y));
            }
            focus.Add(FlowFrames.NodeId(y));
            if (ahead > 0 && Units(residual, x, y) == Capacity(x, y)) full.Add($"{x} → {y}");
        }
        return new FlowPush(along, legs, focus.ToArray(), back, full);
    }
}
