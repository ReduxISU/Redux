using API.Interfaces;
using API.Interfaces.JSON_Objects.Graphs;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_TOPOLOGICALSORT.Solvers;

namespace API.Problems.NPComplete.NPC_TOPOLOGICALSORT.Visualizations;

class TopologicalSortDefaultVisualization : IVisualization<TOPOLOGICALSORT>
{
    // --- Fields ---
    public string visualizationName { get; } = "Topological Sort Visualization";
    public string visualizationDefinition { get; } = "Animates the graph by topological rank: all source nodes (in-degree zero) highlight first, followed by their successors in waves. This mirrors how Kahn's Algorithm peels off layers of zero-in-degree nodes during execution. Every edge is highlighted because, in a valid topological ordering, every directed edge points forward.";
    public string source { get; } = "Kahn, A. B. (1962). Topological sorting of large networks. Communications of the ACM, 5(11), 558-562.";
    public string[] contributors { get; } = { "Pravesh Aryal", "Koras Koirala", "Dinesh Khanal" };
    public string visualizationType { get; } = "Graph D3";
    public ISolver solver { get; } = new KahnsAlgorithm();

    // --- Constructors ---
    public TopologicalSortDefaultVisualization() { }

    // --- Methods ---
    public API_JSON visualize(TOPOLOGICALSORT problem)
    {
        return problem.graph.ToAPIGraph();
    }

    public API_JSON SolvedVisualization(TOPOLOGICALSORT problem, string solution)
    {
        API_GraphJSON apiGraph = problem.graph.ToAPIGraph();

        // No valid topological ordering (cycle in graph): return graph un-highlighted.
        if (string.IsNullOrWhiteSpace(solution) || solution.Trim() == "{}")
            return apiGraph;

        // Compute the topological rank of every node.
        // rank(v) = length of longest path from any source node to v.
        // All rank-0 nodes (in-degree zero) animate first, rank-1 next, etc.
        Dictionary<string, int> rank = ComputeRanks(problem);
        int maxRank = rank.Values.Count == 0 ? 0 : rank.Values.Max();

        // Spread the animation evenly across a fixed total duration.
        const int totalDurationMs = 5000;
        int delayPerRank = maxRank == 0 ? 0 : totalDurationMs / (maxRank + 1);

        // Highlight every node, timed by its topological rank.
        foreach (var node in apiGraph.nodes)
        {
            int r = rank.TryGetValue(node.name, out var value) ? value : 0;
            node.color = "Solution";
            node.delay = (r * delayPerRank).ToString();
        }

        // Highlight every edge. An edge (u -> v) appears one rank-step after u.
        foreach (var link in apiGraph.links)
        {
            int sourceRank = rank.TryGetValue(link.source, out var value) ? value : 0;
            link.color = "Solution";
            link.delay = ((sourceRank + 1) * delayPerRank).ToString();
        }

        return apiGraph;
    }

    private static Dictionary<string, int> ComputeRanks(TOPOLOGICALSORT problem)
    {
        Dictionary<string, int> rank = new Dictionary<string, int>();
        Dictionary<string, int> inDegree = new Dictionary<string, int>();

        foreach (var node in problem.nodes)
        {
            rank[node] = 0;
            inDegree[node] = 0;
        }

        foreach (var edge in problem.edges)
        {
            if (inDegree.ContainsKey(edge.Value))
                inDegree[edge.Value]++;
        }

        Queue<string> queue = new Queue<string>();
        foreach (var node in problem.nodes)
        {
            if (inDegree[node] == 0)
                queue.Enqueue(node);
        }

        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            foreach (var edge in problem.edges)
            {
                if (edge.Key != current) continue;
                rank[edge.Value] = Math.Max(rank[edge.Value], rank[current] + 1);
                inDegree[edge.Value]--;
                if (inDegree[edge.Value] == 0)
                    queue.Enqueue(edge.Value);
            }
        }

        return rank;
    }
}
