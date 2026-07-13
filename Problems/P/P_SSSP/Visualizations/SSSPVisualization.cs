using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;
using API.Problems.P.P_SSSP.Solvers;

namespace API.Problems.P.P_SSSP.Visualizations;

class SSSPVisualization : IVisualization<SSSP>
{
    public string visualizationName { get; } = "Single Source Shortest Path Visualization";
    public string visualizationDefinition { get; } = "Visualizes the Single Source Shortest Path problem for non-negative weighted directed cyclic graphs using Dijkstra's algorithm";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Rajit Nilkar" };
    public string visualizationType => "Graph D3";
    public ISolver solver { get; } = new SSSPSolver();

    public SSSPVisualization() { }

    public API_JSON visualize(SSSP problem)
    {
        // For simplicity, we will just return a JSON representation of the graph
        // In a real implementation, this would be more complex and would include visual elements
        return problem.graph.ToAPIGraph();
    }
}