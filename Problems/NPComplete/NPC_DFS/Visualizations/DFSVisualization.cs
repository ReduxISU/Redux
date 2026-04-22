using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;

namespace API.Problems.NPComplete.NPC_DFS.Visualizations;

class DFSVisualization : IVisualization<DFS>
{
    public string visualizationName { get; } = "Depth-First Search Visualization";
    public string visualizationDefinition { get; } = "Visualizes the traversal path explored by depth-first search.";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Scott Barfuss" };
    public string visualizationType { get; } = "Graph D3";

    public DFSVisualization() { }

    public API_JSON visualize(DFS problem)
    {
        API_GraphJSON graph = problem.graph.ToAPIGraph();
        HighlightEndpoints(problem, graph);
        return graph;
    }

    public API_JSON SolvedVisualization(DFS problem, string solution)
    {
        if (string.IsNullOrWhiteSpace(solution) || solution.Trim() == "{}")
            return visualize(problem);

        List<string> path;
        try
        {
            path = GraphParser.parseNodeListWithStringFunctions(solution)
                .Select(node => node.Trim())
                .Where(node => node.Length > 0)
                .ToList();
        }
        catch
        {
            return visualize(problem);
        }

        API_GraphJSON graph = problem.graph.ToAPIGraph();
        HighlightEndpoints(problem, graph);

        var pathNodes = new HashSet<string>(path);
        for (int i = 0; i < graph.nodes.Count; i++)
            graph.nodes[i].color = pathNodes.Contains(graph.nodes[i].name)
                ? "Solution"
                : "Background";

        var pathEdges = new HashSet<(string from, string to)>();
        for (int i = 0; i < path.Count - 1; i++)
            pathEdges.Add((path[i], path[i + 1]));

        for (int i = 0; i < graph.links.Count; i++)
        {
            API_Link link = graph.links[i];
            bool isForwardPathEdge = pathEdges.Contains((link.source, link.target));
            bool isReversePathEdge = !problem.isDirected && pathEdges.Contains((link.target, link.source));

            link.color = (isForwardPathEdge || isReversePathEdge)
                ? "Solution"
                : "Background";
        }

        return graph;
    }

    public List<API_JSON> stepsVisualization(DFS problem, List<string> steps)
    {
        var result = new List<API_JSON>();

        foreach (string step in steps)
            result.Add(SolvedVisualization(problem, step));

        return result;
    }

    private static void HighlightEndpoints(DFS problem, API_GraphJSON graph)
    {
        foreach (API_Node_Programmable_Small node in graph.nodes)
        {
            if (node.name == problem.sourceNode)
                node.outline = "Green";

            if (node.name == problem.targetNode)
                node.outline = "Red";
        }
    }
}
