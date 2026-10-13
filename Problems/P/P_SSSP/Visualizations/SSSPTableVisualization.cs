using API.Interfaces.Steps;
using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Tables;
using API.Interfaces.Table;
using API.Problems.P.P_SSSP.Solvers;
using SSSPTableStep = API.Problems.P.P_SSSP.Solvers.SSSPSolver.SSSPTableStep;

namespace API.Problems.P.P_SSSP.Visualizations;

/// <summary>
/// Dijkstra's table. The list format is the step table it always was; under format=frames it is the Table picture's trace
/// table (<see cref="DistanceTraceTable"/>), a second view of the run the Layered Graph picture draws: one row per node with
/// when it was settled, its cost, the node it is reached through and its path. The problem's default visualization is
/// unchanged.
/// </summary>
class SSSPTableVisualization : IVisualization<SSSP>, ITableVisualization<SSSP, Distances> {
    public string visualizationName { get; } = "SSSP Table Visualization";
    public string visualizationDefinition { get; } = "Displays a step-by-step table visualization of Dijkstra's alogrithm execution, showing each vertex's cost/predecessor and reconstructed path at each stage";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Rajit Nilkar" };
    public VisualizationType visualizationType => VisualizationType.DynamicTable;
    public ISolver solver { get; } = new SSSPSolver();

    // The run's steps are typed (Distances) since #667; the list format's tables are made from them (#734).
    // Implemented explicitly so it is not part of /info.
    Type? IVisualization.StepShape => typeof(Distances);

    // The list methods were already given the typed steps (they need some to draw their tables), so they keep getting them.
    bool IFramesVisualization.ListReadsTypedSteps() => true;

    // The Table picture (format=frames only). Implemented explicitly so it is not part of /info.
    TableSpec ITableVisualization<SSSP, Distances>.Describe(SSSP problem) => DistanceTraceTable.Spec(problem.sourceNode, null);
    IReadOnlyList<TableDraft> ITableVisualization<SSSP, Distances>.Draw(SSSP problem, IReadOnlyList<SolverStep<Distances>> steps, bool incremental) =>
        DistanceTraceTable.Drafts(steps, problem.nodes, problem.sourceNode, null);
    TableDraft ITableVisualization<SSSP, Distances>.Blank(SSSP problem) => DistanceTraceTable.Blank(problem.nodes);

    public SSSPTableVisualization() { }

    // The table before the search starts; it needs no run.
    public API_JSON visualize(SSSP problem) {
        var initial = SSSPSolver.InitialTableStep(problem);
        return initial != null ? TranslateToTableJSON(initial) : new API_empty();
    }

    // The tables are made from the run's typed steps (no second run). The solved table is the last of them, so
    // SolvedVisualization keeps the interface default (no frame).
    public List<API_JSON> StepsVisualization(SSSP problem, List<Object> steps) {
        return SSSPSolver.TableSteps(problem, steps).Select(s => (API_JSON)TranslateToTableJSON(s)).ToList();
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