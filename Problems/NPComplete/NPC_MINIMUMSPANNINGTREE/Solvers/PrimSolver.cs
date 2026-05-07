using System.Collections.Generic;
using System.Linq;
using API.Interfaces;
using API.Problems.NPComplete.NPC_MINIMUMSPANNINGTREE;

namespace API.Problems.NPComplete.NPC_MINIMUMSPANNINGTREE.Solvers;

class PrimSolver : ISolver<MINIMUMSPANNINGTREE>
{
    public string solverName { get; } = "Prim's Algorithm";
    public string solverDefinition { get; } = "Finds a minimum spanning tree by repeatedly adding the lowest-weight edge that connects the growing tree to a new vertex.";
    public string source { get; } = "Prim, R. C. \"Shortest connection networks and some generalizations.\" Bell System Technical Journal 36, no. 6 (1957): 1389-1401.";
    public string sourceLink { get; } = "https://doi.org/10.1002/j.1538-7305.1957.tb01515.x";
    public string[] contributors { get; } = { "Val Kimbrough" };
    public bool timerHasExpired { get; set; }

    public string solve(MINIMUMSPANNINGTREE problem)
    {
        // Sort once so equal-weight instances still produce deterministic certificates.
        List<string> nodes = problem.graph.Nodes.ToList().Select(n => n.ToString()).Distinct().OrderBy(n => n).ToList();
        var edges = KruskalSolver.ExtractEdges(problem.graph);

        if (nodes.Count == 0)
            return "{}";

        var adjacency = BuildAdjacency(nodes, edges);
        var visited = new HashSet<string> { nodes[0] };
        var selected = new List<(string u, string v, int weight)>();

        while (selected.Count < nodes.Count - 1)
        {
            if (timerHasExpired)
                return "{}";

            var candidateEdges = visited
                .SelectMany(node => adjacency[node])
                .Where(edge => !visited.Contains(edge.to))
                .ToList();

            if (candidateEdges.Count == 0)
                return "{}";

            // Tie-break lexicographically so the API returns stable output when multiple MSTs exist.
            var nextEdge = candidateEdges
                .OrderBy(edge => edge.weight)
                .ThenBy(edge => KruskalSolver.CanonicalKey(edge.from, edge.to))
                .ThenBy(edge => edge.to)
                .First();

            selected.Add((nextEdge.from, nextEdge.to, nextEdge.weight));
            visited.Add(nextEdge.to);
        }

        return KruskalSolver.EdgeListToCertificate(selected);
    }

    private static Dictionary<string, List<(string from, string to, int weight)>> BuildAdjacency(
        List<string> nodes,
        List<(string u, string v, int weight)> edges)
    {
        var adjacency = nodes.ToDictionary(node => node, _ => new List<(string from, string to, int weight)>());

        foreach (var edge in edges)
        {
            if (edge.u == edge.v || !adjacency.ContainsKey(edge.u) || !adjacency.ContainsKey(edge.v))
                continue;

            // Store each undirected edge in both directions so frontier scans stay local to visited nodes.
            adjacency[edge.u].Add((edge.u, edge.v, edge.weight));
            adjacency[edge.v].Add((edge.v, edge.u, edge.weight));
        }

        return adjacency;
    }
}
