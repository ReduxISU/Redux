using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Tables;
using API.Problems.P.P_SSSP.Solvers;
using SSSPTableStep = API.Problems.P.P_SSSP.Solvers.SSSPSolver.SSSPTableStep;

namespace API.Problems.P.P_SSSP.Visualizations;

class SSSPTableVisualization : IVisualization<SSSP> {
    public string visualizationName { get; } = "SSSP Table Visualization";
    public string visualizationDefinition { get; } = "Displays a step-by-step table visualization of Dijkstra's alogrithm execution, showing each vertex's cost/predecessor and reconstructed path at each stage";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Rajit Nilkar" };
    public VisualizationType visualizationType => VisualizationType.DynamicTable;
    public ISolver solver { get; } = new SSSPSolver();

    public SSSPTableVisualization() { }

    public API_JSON visualize(SSSP problem) {
        var solver = new SSSPSolver();
        var steps = solver.GetTableSteps(problem);
        return steps.Count > 0 ? TranslateToTableJSON((SSSPTableStep)steps[0]) : new API_empty();
    }

    public API_JSON SolvedVisualization(SSSP problem) {
        var solver = new SSSPSolver();
        var steps = solver.GetTableSteps(problem);
        return steps.Count > 0 ? TranslateToTableJSON((SSSPTableStep)steps[steps.Count - 1]) : new API_empty();
    }

    public List<API_JSON> StepsVisualization(SSSP problem, List<Object> steps) {
        var solver = new SSSPSolver();
        var tableSteps = solver.GetTableSteps(problem);
        return tableSteps.Select(s => (API_JSON)TranslateToTableJSON((SSSPTableStep)s)).ToList();
    }

    private static API_TableJSON TranslateToTableJSON(SSSPTableStep step) {
        var result = new API_TableJSON {
            columns = new List<TableColumn>
            {
                new TableColumn {key = "vertex", label = "Vertex"},
                new TableColumn {key = "order", label = "Order"},
                new TableColumn {key = "known", label = "Known"},
                new TableColumn {key = "costPredecessor", label = "Cost,Predecessor"},
                new TableColumn {key = "path", label = "Path"}
            }
        };

        var knownSet = new HashSet<string>(step.knownSet);

        foreach (var vertex in step.vertices) {
            bool isKnown = knownSet.Contains(vertex.name);
            bool isCurrent = vertex.name == step.currentVertex;

            var row = new TableRow {
                id = vertex.name,
                cells = new Dictionary<string, string>
               {
                   {"vertex", vertex.name },
                   {"order", vertex.order },
                   {"known", isKnown ? "\u2705" : "\u274c" },
                   {"costPredecessor", vertex.display },
                   {"path", vertex.path ?? "-" }
               },
                cellColors = isCurrent ? new Dictionary<string, string> { { "costPredecessor", "ElementHighlight" } } : null
            };
            result.rows.Add(row);
        }
        return result;
    }

}