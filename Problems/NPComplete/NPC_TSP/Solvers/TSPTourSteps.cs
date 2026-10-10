using System.Globalization;
using API.Interfaces.Graphs;

namespace API.Problems.NPComplete.NPC_TSP.Solvers;

/// <summary>
/// Caption and focus helpers shared by the three Traveling Salesperson solvers. Everything here scans the edge list, so
/// call it only inside a recorder lambda (which runs only while recording) and never on a solver's hot path.
/// </summary>
static class TSPTourSteps {
    /// <summary>The weight of the edge between two cities (either direction; the first listed wins, as in the verifier), or null if there is none.</summary>
    public static int? Weight(TSP tsp, string a, string b) {
        foreach (var e in tsp.edges)
            if (e.source == a && e.target == b) return e.weight;
        foreach (var e in tsp.edges)
            if (e.source == b && e.target == a) return e.weight;
        return null;
    }

    /// <summary>The first step of a closed tour (<paramref name="closed"/> ends where it starts) that is not an edge, or null.</summary>
    public static (string From, string To)? FirstMissingHop(TSP tsp, IReadOnlyList<string> closed) {
        for (int i = 0; i + 1 < closed.Count; i++)
            if (Weight(tsp, closed[i], closed[i + 1]) == null) return (closed[i], closed[i + 1]);
        return null;
    }

    /// <summary>The total weight of a path's steps, or null if one of them is not an edge.</summary>
    public static int? Cost(TSP tsp, IReadOnlyList<string> path) {
        int total = 0;
        for (int i = 0; i + 1 < path.Count; i++) {
            int? w = Weight(tsp, path[i], path[i + 1]);
            if (w == null) return null;
            total += w.Value;
        }
        return total;
    }

    /// <summary>The ids of the edges a path walks (those that exist), to show a whole rejected tour as the thing that failed.</summary>
    public static string[] EdgeIds(TSP tsp, IReadOnlyList<string> path) {
        var ids = new List<string>();
        for (int i = 0; i + 1 < path.Count; i++)
            if (Weight(tsp, path[i], path[i + 1]) != null) ids.Add(GraphTourFrames.EdgeId(false, path[i], path[i + 1]));
        return ids.ToArray();
    }

    /// <summary>A number for a caption: whole numbers without a decimal point, whatever the culture.</summary>
    public static string Num(double value) => value.ToString(CultureInfo.InvariantCulture);
}
