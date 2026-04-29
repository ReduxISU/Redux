using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;
using API.Problems.NPComplete.NPC_MINIMUMSPANNINGTREE.Solvers;

namespace API.Problems.NPComplete.NPC_MINIMUMSPANNINGTREE.Visualizations;

class MinimumSpanningTreeVisualization : IVisualization<NPC_MINIMUMSPANNINGTREE>
{
    public string visualizationName { get; } = "Minimum Spanning Tree Visualization";
    public string visualizationDefinition { get; } = "Displays a weighted graph and highlights the edges selected for a minimum spanning tree.";
    public string source { get; } = "https://en.wikipedia.org/wiki/Minimum_spanning_tree";
    public string[] contributors { get; } = { "OpenAI" };
    public string visualizationType { get; } = "Graph D3";
    public ISolver solver { get; } = new KruskalSolver();

    public API_JSON visualize(NPC_MINIMUMSPANNINGTREE problem)
    {
        return problem.graph.ToAPIGraph();
    }

    public API_JSON SolvedVisualization(NPC_MINIMUMSPANNINGTREE problem, string solution)
    {
        if (string.IsNullOrWhiteSpace(solution) || solution.Trim() == "{}")
            return visualize(problem);

        List<KeyValuePair<string, string>> parsedEdges;
        try
        {
            parsedEdges = GraphParser.parseUndirectedEdgeListWithStringFunctions(solution);
        }
        catch
        {
            return visualize(problem);
        }

        var selectedKeys = new HashSet<string>();
        var selectedNodes = new HashSet<string>();

        foreach (var edge in parsedEdges)
        {
            selectedKeys.Add(KruskalSolver.CanonicalKey(edge.Key, edge.Value));
            selectedNodes.Add(edge.Key);
            selectedNodes.Add(edge.Value);
        }

        API_GraphJSON apiGraph = problem.graph.ToAPIGraph();

        foreach (var node in apiGraph.nodes)
            node.color = selectedNodes.Contains(node.name) ? "Solution" : "Background";

        foreach (var link in apiGraph.links)
        {
            string linkKey = KruskalSolver.CanonicalKey(link.source, link.target);
            link.color = selectedKeys.Contains(linkKey) ? "Solution" : "Background";
        }

        return apiGraph;
    }
}
