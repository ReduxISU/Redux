namespace API.Interfaces.Graphs;

/// <summary>
/// The one rule for parallel edges in weighted graphs (#769): two or more edges that join the same pair of nodes
/// become one edge. Cut problems add the weights, since every parallel edge crosses the cut; path, tour and tree
/// problems keep the lightest, since a route only ever takes the cheapest way between two nodes. For a directed graph,
/// "the same pair" means the same direction: a-to-b and b-to-a stay two edges.
/// <para>
/// A problem applies the rule once, where it reads its instance, so every solver and verifier sees the same graph.
/// The merged edge keeps the position and the endpoint order of the first edge listed for its pair.
/// </para>
/// <para>
/// The instance grammar reads E as a set, so the parser already drops an edge written twice exactly as before (the same
/// ends in the same order with the same weight); only the edges it keeps are merged here.
/// </para>
/// </summary>
static class ParallelEdges {
    /// <summary>One edge per pair, weighing the lightest of the pair's edges.</summary>
    public static List<(string source, string target, int weight)> Lightest(
        IEnumerable<(string source, string target, int weight)> edges, bool directed = false) =>
        Merge(edges, directed, Math.Min);

    /// <summary>One edge per pair, weighing the sum of the pair's edges.</summary>
    public static List<(string source, string target, int weight)> Summed(
        IEnumerable<(string source, string target, int weight)> edges, bool directed = false) =>
        Merge(edges, directed, (a, b) => a + b);

    private static List<(string source, string target, int weight)> Merge(
        IEnumerable<(string source, string target, int weight)> edges, bool directed, Func<int, int, int> combine) {
        var index = new Dictionary<(string, string), int>();
        var merged = new List<(string source, string target, int weight)>();
        foreach (var edge in edges) {
            var key = directed || string.CompareOrdinal(edge.source, edge.target) <= 0
                ? (edge.source, edge.target)
                : (edge.target, edge.source);
            if (index.TryGetValue(key, out int at)) {
                merged[at] = (merged[at].source, merged[at].target, combine(merged[at].weight, edge.weight));
            } else {
                index[key] = merged.Count;
                merged.Add(edge);
            }
        }
        return merged;
    }
}
