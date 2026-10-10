using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;
using API.Problems.NPHard.NPH_MINIMUMTSP.Solvers;

namespace API.Problems.NPHard.NPH_MINIMUMTSP.Visualizations;

class MinimumTSPDefaultVisualization : IVisualization<MINIMUMTSP, API_GraphJSON>, IGraphTourVisualization<MINIMUMTSP> {

    // --- Fields ---
    public string visualizationName { get; } = "Minimum Traveling Salesperson Visualization";
    public string visualizationDefinition { get; } = "This is a default visualization for Minimum Traveling Salesperson";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.GraphD3;
    public ISolver solver { get; } = new BruteForceMinimumTSP();

    // --- Methods Including Constructors ---
    public MinimumTSPDefaultVisualization() {

    }
    public API_GraphJSON visualize(MINIMUMTSP problem) {
        return problem.graph.ToAPIGraph();
    }

    // The tour's cities and hops are Solution; with no tour ({}), the plain graph.
    public API_JSON SolvedVisualization(MINIMUMTSP problem, string solution) {
        if (string.IsNullOrWhiteSpace(solution) || solution == "{}")
            return problem.graph.ToAPIGraph();

        List<string> solutionNodes = GraphParser.parseNodeListWithStringFunctions(solution)
            .Where(n => !string.IsNullOrWhiteSpace(n)).ToList();

        if (solutionNodes.Count == 0)
            return problem.graph.ToAPIGraph();

        API_GraphJSON apiGraph = problem.graph.ToAPIGraph();
        for (int i = 0; i < apiGraph.nodes.Count; i++)
            apiGraph.nodes[i].color = "Solution";

        for (int i = 0; i < solutionNodes.Count - 1; i++) {
            var from = solutionNodes[i];
            var to = solutionNodes[i + 1];

            var link = apiGraph.links.FirstOrDefault(l =>
                (l.source == from && l.target == to) ||
                (l.source == to && l.target == from)
            );

            if (link != null)
                link.color = "Solution";
        }
        return apiGraph;
    }

    // --- Graph picture (format=frames only; the list-format methods above are unchanged) ---
    Type? IVisualization.StepShape => typeof(Tour);

    // Traveling Salesperson's tour picture, weighted, with no K.
    GraphTourSpec IGraphTourVisualization<MINIMUMTSP>.Describe(MINIMUMTSP problem) =>
        new(problem.nodes, problem.listedEdges.Select(e => new GraphTourEdge(e.source, e.target, e.weight)).ToList(), false, null, TourRule.MinimumTravelingSalesperson);
}
