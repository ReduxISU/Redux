using API.Interfaces;
using API.Interfaces.Steps;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;
using API.Interfaces.JSON_Objects.Tables;
using API.Problems.P.P_SPSP;
using API.Problems.P.P_SPSP.Solvers;
using static API.Problems.P.P_SSSP.Solvers.SSSPSolver;
using SPSPTableStep = API.Problems.P.P_SPSP.Solvers.SPSPSolver.SPSPTableStep;

namespace API.Problems.P.P_SPSP.Visualizations;

class SPSPTableVisualization : IVisualization<SPSP> {
    public string visualizationName { get; } = "Single Pair Shortest Path Table Visualization";
    public string visualizationDefinition { get; } = "Displays a step-by-step table of Dijkstra's algorithm execution, showing each vertex's known status, current cost, and path at each stage of the algorithm.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Rajit Nilkar" };
    public VisualizationType visualizationType => VisualizationType.DynamicTable;
    public ISolver solver { get; } = new SPSPSolver();

    // Implemented explicitly so it is not a public member (and so not part of /info).
    Type? IVisualization.StepShape => typeof(Distances);

    public SPSPTableVisualization() { }

    // The table before the search starts; it needs no run.
    public API_JSON visualize(SPSP problem) {
        var initial = SPSPSolver.InitialTableStep(problem);
        return initial != null ? TranslateToTableJSON(initial, isFinalStep: false) : new API_empty();
    }

    // The tables are made from the run's typed steps (no second run). The solved table is the last of them, and the
    // picture has always shown it once more at the end; StepsVisualization adds that copy, since SolvedVisualization
    // (the interface default, no frame) is not given the steps.
    public List<API_JSON> StepsVisualization(SPSP problem, List<Object> steps) {
        var tableSteps = SPSPSolver.TableSteps(problem, steps);
        var frames = tableSteps.Select((s, i) => (API_JSON)TranslateToTableJSON(s, isFinalStep: i == tableSteps.Count - 1)).ToList();
        if (tableSteps.Count > 0)
            frames.Add(TranslateToTableJSON(tableSteps[^1], isFinalStep: true));
        return frames;
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
