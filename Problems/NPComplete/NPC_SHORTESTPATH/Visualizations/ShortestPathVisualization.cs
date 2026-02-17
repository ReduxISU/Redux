using System;
using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_SHORTESTPATH;

namespace API.Problems.NPComplete.NPC_SHORTESTPATH.Visualizations;

public class ShortestPathVisualization : IVisualization<SHORTESTPATH>
{
    public string visualizationName { get; } = "Dijkstra Visualization";
    public string visualizationDefinition { get; } = "Visualizes Dijkstra's algorithm";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Rajit Nilkar" };

    // Add the missing property to implement the interface
    public string visualizationType => typeof(SHORTESTPATH);

    // Implement the required method from IVisualization<SHORTESTPATH>
    public API_JSON visualize(SHORTESTPATH problem)
    {
        // For simplicity, we will just return a JSON representation of the graph
        // In a real implementation, this would be more complex and would include visual elements
        return new API_Graph(problem.nodes, problem.edges);
    }
}
