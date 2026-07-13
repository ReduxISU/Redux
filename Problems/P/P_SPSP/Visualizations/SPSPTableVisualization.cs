using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;
using API.Problems.P.P_SPSP;
using API.Problems.P.P_SPSP.Solvers;

namespace API.Problems.P.P_SPSP.Visualizations;

class SPSPTableVisualization : IVisualization<SPSP>
{
    public string visualizationName { get; } = "Single Pair Shortest Path Table Visualization";
    public string visualizationDefinition { get; } = "Displays a step-by-step table of Dijkstra's algorithm execution, showing each vertex's known status, current cost, and path at each stage of the algorithm.";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Rajit Nilkar" };
    public string visualizationType => "SSSP Table";
    public ISolver solver { get; } = new SPSPSolver();

    public SPSPTableVisualization() { }

    public API_JSON visualize(SPSP problem)
    {
        var spspSolver = new SPSPSolver();
        var steps = spspSolver.GetTableSteps(problem);
        return steps.Count > 0 ? (API_JSON)steps[0] : new API_empty();
    }

    public API_JSON SolvedVisualization(SPSP problem, string solution)
    {
        var spspSolver = new SPSPSolver();
        var steps = spspSolver.GetTableSteps(problem);
        return steps.Count > 0 ? (API_JSON)steps[steps.Count - 1] : new API_empty();
    }

    public List<API_JSON> StepsVisualization(SPSP problem, List<Object> steps)
    {
        var spspSolver = new SPSPSolver();
        var tableSteps = spspSolver.GetTableSteps(problem);
        return tableSteps.Cast<API_JSON>().ToList();
    }
}