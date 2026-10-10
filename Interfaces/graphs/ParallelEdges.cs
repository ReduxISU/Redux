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
/// E is a set, so an edge written twice is one edge: the parser drops an exact copy (the same ends in the same order with
/// the same weight), and <see cref="Distinct"/> also drops a copy whose ends are written the other way round in an
/// undirected graph. Only edges that differ in weight are parallel edges, and only those are merged.
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

    /// <summary>
    /// The edges with repeats dropped, keeping the first copy: in an undirected graph ({1,2},5) and ({2,1},5) are the
    /// same edge. Edges with different weights are kept, since those are parallel edges.
    /// </summary>
    public static List<(string source, string target, int weight)> Distinct(
        IEnumerable<(string source, string target, int weight)> edges, bool directed = false) {
        var seen = new HashSet<(string, string, int)>();
        return edges.Where(edge => {
            var (a, b) = Key(edge, directed);
            return seen.Add((a, b, edge.weight));
        }).ToList();
    }

    private static (string, string) Key((string source, string target, int weight) edge, bool directed) =>
        directed || string.CompareOrdinal(edge.source, edge.target) <= 0
            ? (edge.source, edge.target)
            : (edge.target, edge.source);

    private static List<(string source, string target, int weight)> Merge(
        IEnumerable<(string source, string target, int weight)> edges, bool directed, Func<int, int, int> combine) {
        var index = new Dictionary<(string, string), int>();
        var merged = new List<(string source, string target, int weight)>();
        foreach (var edge in Distinct(edges, directed)) {
            var key = Key(edge, directed);
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
