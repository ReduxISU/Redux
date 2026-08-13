using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Tables;
using API.Problems.P.P_DFA;
using API.Problems.P.P_DFA.Solvers;
using DFATableStep = API.Problems.P.P_DFA.Solvers.DFASolver.DFATableStep;

namespace API.Problems.P.P_DFA.Visualizations;

class DFATableVisualization : IVisualization<DFA> {
    public string visualizationName { get; } = "Deterministic Finite Automata Table Visualization";
    public string visualizationDefinition { get; } = "Displays a step-by-step table tracing the DFA's single deterministic path through the input string, showing the symbol consumed, the state transition, and whether the resulting state is accepting at each step.";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType => VisualizationType.DynamicTable;
    public ISolver solver { get; } = new DFASolver();

    public DFATableVisualization() { }

    // `visualize`/`SolvedVisualization` deliberately return API_empty: the controller
    // concatenates visualize() + StepsVisualization() + SolvedVisualization() into one flat
    // list with no de-duplication, and both of those would otherwise just repeat the first/last
    // entries already present in StepsVisualization(), producing duplicate steps in the step slider.
    public API_JSON visualize(DFA problem) {
        return new API_empty();
    }

    public API_JSON SolvedVisualization(DFA problem, string solution) {
        return new API_empty();
    }

    public List<API_JSON> StepsVisualization(DFA problem, List<Object> steps) {
        var dfaSolver = new DFASolver();
        var tableSteps = dfaSolver.GetTableSteps(problem);
        return tableSteps.Select(s => (API_JSON)TranslateToTableJSON((DFATableStep)s)).ToList();
    }

    private static API_TableJSON TranslateToTableJSON(DFATableStep step) {
        var result = new API_TableJSON {
            columns = new List<TableColumn>
            {
                new TableColumn { key = "step", label = "Step" },
                new TableColumn { key = "symbol", label = "Symbol" },
                new TableColumn { key = "fromState", label = "From State" },
                new TableColumn { key = "toState", label = "To State" },
                new TableColumn { key = "accepting", label = "Accepting" }
            }
        };

        for (int i = 0; i < step.rows.Count; i++) {
            var row = step.rows[i];
            bool isCurrent = i == step.currentRow;

            result.rows.Add(new TableRow {
                id = row.step.ToString(),
                cells = new Dictionary<string, string>
                {
                    { "step", row.step.ToString() },
                    { "symbol", row.symbol },
                    { "fromState", row.fromState },
                    { "toState", row.toState },
                    { "accepting", row.accepting ? "✅" : "❌" }
                },
                // The state the trace has reached is the one thing worth pointing at, so the
                // highlight sits on that cell; a whole-row Solution tint marks it accepting.
                color = isCurrent && row.accepting ? "Solution" : null,
                cellColors = isCurrent
                    ? new Dictionary<string, string> { { "toState", "ElementHighlight" } }
                    : null
            });
        }
        return result;
    }
}
