using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;
using API.Problems.NPComplete.NPC_MAXCUT.Solvers;

namespace API.Problems.NPComplete.NPC_MAXCUT.Visualizations;

class MaxCutVisualization : IVisualization<MAXCUT>
{
    public string visualizationName { get; } = "Max Cut Visualization";
    public string visualizationDefinition { get; } = "Displays a weighted undirected graph and highlights the edges belonging to the maximum cut, coloring S-side nodes and crossing edges.";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Max Gruenwoldt", "Michael Trosper" };
    public string visualizationType { get; } = "Graph D3";
    public ISolver solver { get; } = new MaxCutSolver();

    public MaxCutVisualization() { }

    public API_JSON visualize(MAXCUT problem) => problem.graph.ToAPIGraph();

    public API_JSON SolvedVisualization(MAXCUT problem, string solution)
    {
        if (string.IsNullOrWhiteSpace(solution) || solution.Trim() == "{}")
            return visualize(problem);

        API_GraphJSON apiGraph = problem.graph.ToAPIGraph();

        // solution is the S-side node set: {node1,node2,...}
        HashSet<string> sNodes = solution.Trim()
            .TrimStart('{').TrimEnd('}')
            .Split(',')
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToHashSet();

        // Color edges crossing the cut (one endpoint in S, one in T).
        foreach (var link in apiGraph.links)
        {
            bool srcInS = sNodes.Contains(link.source);
            bool dstInS = sNodes.Contains(link.target);
            if (srcInS != dstInS)
            {
                link.color = "Solution";
                link.dashed = "True";
            }
        }

        // Color S-side nodes.
        foreach (var node in apiGraph.nodes)
            if (sNodes.Contains(node.name))
                node.color = "Solution";

        return apiGraph;
    }
}
