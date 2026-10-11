using API.Interfaces.Steps;
using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;
using API.Interfaces.JSON_Objects.Tables;
using API.Interfaces.Table;
using API.Problems.P.P_SPSP;
using API.Problems.P.P_SPSP.Solvers;
using static API.Problems.P.P_SSSP.Solvers.SSSPSolver;
using SPSPTableStep = API.Problems.P.P_SPSP.Solvers.SPSPSolver.SPSPTableStep;

namespace API.Problems.P.P_SPSP.Visualizations;

/// <summary>
/// Dijkstra's table. The list format is the step table it always was; under format=frames it is the Table picture's trace
/// table (<see cref="DistanceTraceTable"/>), a second view of the run the Layered Graph picture draws: one row per node with
/// when it was settled, its cost, the node it is reached through and its path. The problem's default visualization is
/// unchanged.
/// </summary>
class SPSPTableVisualization : IVisualization<SPSP>, ITableVisualization<SPSP, Distances> {
    public string visualizationName { get; } = "Single Pair Shortest Path Table Visualization";
    public string visualizationDefinition { get; } = "Displays a step-by-step table of Dijkstra's algorithm execution, showing each vertex's known status, current cost, and path at each stage of the algorithm.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Rajit Nilkar" };
    public VisualizationType visualizationType => VisualizationType.DynamicTable;
    public ISolver solver { get; } = new SPSPSolver();

    // The run's steps are typed (Distances) since #667; this table makes its own from the solver, but must still be called
    // with them. Implemented explicitly so it is not part of /info.
    Type? IVisualization.StepShape => typeof(Distances);

    // The list methods were already given the typed steps (they need some to draw their tables), so they keep getting them.
    bool IFramesVisualization.ListReadsTypedSteps() => true;

    // The Table picture (format=frames only). Implemented explicitly so it is not part of /info.
    TableSpec ITableVisualization<SPSP, Distances>.Describe(SPSP problem) => DistanceTraceTable.Spec(problem.sourceNode, problem.targetNode);
    IReadOnlyList<TableDraft> ITableVisualization<SPSP, Distances>.Draw(SPSP problem, IReadOnlyList<SolverStep<Distances>> steps, bool incremental) =>
        DistanceTraceTable.Drafts(steps, problem.nodes, problem.sourceNode, problem.targetNode);
    TableDraft ITableVisualization<SPSP, Distances>.Blank(SPSP problem) => DistanceTraceTable.Blank(problem.nodes);

    public SPSPTableVisualization() { }

    public API_JSON visualize(SPSP problem) {
        var spspSolver = new SPSPSolver();
        var steps = spspSolver.GetTableSteps(problem);
        return steps.Count > 0 ? TranslateToTableJSON((SPSPTableStep)steps[0], isFinalStep: false) : new API_empty();
    }

    public API_JSON SolvedVisualization(SPSP problem, string solution) {
        var spspSolver = new SPSPSolver();
        var steps = spspSolver.GetTableSteps(problem);
        return steps.Count > 0 ? TranslateToTableJSON((SPSPTableStep)steps[steps.Count - 1], isFinalStep: true) : new API_empty();
    }

    public List<API_JSON> StepsVisualization(SPSP problem, List<Object> steps) {
        var spspSolver = new SPSPSolver();
        var tableSteps = spspSolver.GetTableSteps(problem);
        return tableSteps.Select((s, i) => (API_JSON)TranslateToTableJSON((SPSPTableStep)s, isFinalStep: i == tableSteps.Count - 1)).ToList();
    }

    private static API_TableJSON TranslateToTableJSON(SPSPTableStep step, bool isFinalStep) {
        var result = new API_TableJSON {
            columns = new List<TableColumn>
            {
                new TableColumn {key = "vertex", label = "Vertex"},
                new TableColumn {key = "known", label = "Known"},
                new TableColumn {key = "cost", label = "Cost"},
                new TableColumn {key = "path", label = "Path"}
            }
        };

        var knownSet = new HashSet<string>(step.knownSet);

        foreach (var vertex in step.vertices) {
            bool isKnown = knownSet.Contains(vertex.name);
            bool isCurrent = vertex.name == step.currentVertex;
            bool isShortestPath = isFinalStep && vertex.name == step.targetVertex && vertex.path != null;

            var row = new TableRow {
                id = vertex.name,
                cells = new Dictionary<string, string>
               {
                   {"vertex", vertex.name },
                   {"known", isKnown ? "✅" : "❌" },
                   {"cost", vertex.cost },
                   {"path", vertex.path ?? "-" }
               },
                cellColors = isCurrent ? new Dictionary<string, string> { { "cost", "ElementHighlight" } } :
                    isShortestPath ? new Dictionary<string, string> { { "path", "Solution" } } : null
            };
            result.rows.Add(row);
        }
        return result;
    }
}
