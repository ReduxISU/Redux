using System;
using System.Collections.Generic;
using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;
using API.Problems.NPComplete.NPC_SHORTESTPATH.Solvers;

namespace API.Problems.NPComplete.NPC_SHORTESTPATH.Visualizations;

class ShortestPathVisualization : IVisualization<SHORTESTPATH>
{
    public string visualizationName { get; } = "Dijkstra Visualization";
    public string visualizationDefinition { get; } = "Visualizes Dijkstra's algorithm";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Rajit Nilkar", "Scott Barfuss" };
    public VisualizationType visualizationType => VisualizationType.GraphD3;
    public ISolver solver { get; } = new DijkstraSolver();

    public ShortestPathVisualization() { }

    public API_JSON visualize(SHORTESTPATH problem)
    {
        // For simplicity, we will just return a JSON representation of the graph
        // In a real implementation, this would be more complex and would include visual elements
        return problem.graph.ToAPIGraph();
    }

    // SolvedVisualization: takes a problem instance and a solution certificate,
    // and returns a visualization of the problem instance with the solution highlighted
    public API_JSON SolvedVisualization(SHORTESTPATH problem, string solution)
    {
        if (string.IsNullOrWhiteSpace(solution) || solution.Trim() == "{}")
            // No path found, return graph with no highlights
            return visualize(problem);
        
        List<string> path;
        try
        {
            // Parse the solution as a path
            path = GraphParser.parseNodeListWithStringFunctions(solution);
        }
        catch
        {
            // Invalid solution format, return graph with no highlights
            return visualize(problem);
        }

        API_GraphJSON graph = problem.graph.ToAPIGraph(); // Convert to API graph format

        var pathNodes = new HashSet<string>(path);
        for (int i = 0; i < graph.nodes.Count; i++)
            graph.nodes[i].color = pathNodes.Contains(graph.nodes[i].name)
            ? "Solution" : "Background";
        
        var pathEdges = new HashSet<(string u, string v)>();
        for (int i = 0; i < path.Count - 1; i++)
            pathEdges.Add((path[i], path[i + 1]));

        for (int i = 0; i < graph.links.Count; i++)
        {
            var link = graph.links[i];
            bool isForwardPathEdge = pathEdges.Contains((link.source, link.target));
            bool isReversePathEdge = !problem.isDirected && pathEdges.Contains((link.target, link.source));

            if (isForwardPathEdge || isReversePathEdge)
                link.color = "Solution";
            else
                link.color = "Background";
        }
        return graph;
    }

    public List<API_JSON> stepsVisualization(SHORTESTPATH problem, List<string> steps)
    {
        // No step-by-step visualization yet?
        return new List<API_JSON>();
    }
}
