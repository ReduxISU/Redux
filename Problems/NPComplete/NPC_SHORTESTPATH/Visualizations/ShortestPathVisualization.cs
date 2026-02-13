using System;
using API.Interfaces;
using API.Problems.NPComplete.NPC_SHORTESTPATH;

namespace API.Problems.NPComplete.NPC_SHORTESTPATH.Visualizations;

public class ShortestPathVisualization : IVisualization<SHORTESTPATH>
{
    public string visualizationName { get; } = "Dijkstra Visualization";
    public string visualizationDefinition { get; } = "Visualizes Dijkstra's algorithm";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Rajit Nilkar" };

    // Implement the required method from IVisualization<SHORTESTPATH>
    public string visualize(SHORTESTPATH problem)
    {
        // TODO: Implement visualization logic using the problem instance
        return problem.ToString();
    }

    public string visualize(string problem, string solution)
    {
        // TODO: Implement visualization
        return solution;
    }
}
