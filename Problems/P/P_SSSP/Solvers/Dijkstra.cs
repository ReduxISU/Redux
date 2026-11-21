using API.Interfaces.Steps;

namespace API.Problems.P.P_SSSP.Solvers;

/// <summary>What the SSSP and SPSP Dijkstra solvers share for their recorded steps.</summary>
static class Dijkstra {
    /// <summary>The search so far: distances of the reached nodes, their predecessors, and the settled nodes.</summary>
    public static Distances Partial(Dictionary<string, int> dist, Dictionary<string, string?> prev, IEnumerable<string> settled) =>
        new(dist.Where(p => p.Value != int.MaxValue), prev.Where(p => p.Value != null).Select(p => new KeyValuePair<string, string>(p.Key, p.Value!)), settled);

    /// <summary>A distance for a caption: the number, or ∞.</summary>
    public static string Show(int distance) => distance == int.MaxValue ? "∞" : distance.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>"Settle 3 at distance 2. Shorter routes found: 2: 4 → 3." or "... No shorter routes from here."</summary>
    public static string SettleCaption(string node, int distance, IReadOnlyList<string> shorter) =>
        $"Settle {node} at distance {Show(distance)}." + (shorter.Count > 0 ? $" Shorter routes found: {string.Join(", ", shorter)}." : " No shorter routes from here.");
}
