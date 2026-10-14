using API.Interfaces;
using API.Interfaces.Steps;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Tables;
using API.Problems.P.P_DFA;
using API.Problems.P.P_DFA.Solvers;

namespace API.Problems.P.P_DFA.Visualizations;

class DFATableVisualization : IVisualization<DFA, API_empty> {
    public string visualizationName { get; } = "Deterministic Finite Automata Table Visualization";
    public string visualizationDefinition { get; } = "Displays a step-by-step table tracing the DFA's single deterministic path through the input string, showing the symbol consumed, the state transition, and whether the resulting state is accepting at each step.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType => VisualizationType.DynamicTable;
    public ISolver solver { get; } = new DFASolver();

    // Implemented explicitly so it is not a public member (and so not part of /info).
    Type? IVisualization.StepShape => typeof(ActiveStates);

    // One row of the trace table.
    private sealed class Row {
        public int step { get; init; }
        public string symbol { get; init; } = "-";
        public string fromState { get; init; } = "-";
        public string toState { get; init; } = "";
        public bool accepting { get; init; }
    }

    public DFATableVisualization() { }

    // `visualize`/`SolvedVisualization` deliberately return API_empty: the controller
    // concatenates visualize() + StepsVisualization() + SolvedVisualization() into one flat
    // list with no de-duplication, and both of those would otherwise just repeat the first/last
    // entries already present in StepsVisualization(), producing duplicate steps in the step slider.
    public API_empty visualize(DFA problem) {
        return new API_empty();
    }

    public API_JSON SolvedVisualization(DFA problem, string solution) {
        return new API_empty();
    }

    public List<API_JSON> StepsVisualization(DFA problem, List<Object> steps) {
        // The trace is the start state plus one row per transition taken (the solver's Try steps). Every
        // frame carries every row and only the highlighted row moves, so the table never changes shape.
        var rows = new List<Row>();
        string previous = "-";
        foreach (var s in steps.Cast<SolverStep<ActiveStates>>().Where(s => s.Event == StepEvent.Try)) {
            string state = s.Partial.States[0];
            int n = s.Partial.Position;
            rows.Add(new Row {
                step = n,
                symbol = n == 0 ? "-" : problem.inputString[n - 1].ToString(),
                fromState = previous,
                toState = state,
                accepting = problem.acceptStates.Contains(state)
            });
            previous = state;
        }

        return rows
            .Select((_, i) => (API_JSON)TranslateToTableJSON(rows, i))
            .ToList();
    }

    private static API_TableJSON TranslateToTableJSON(List<Row> rows, int currentRow) {
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

        for (int i = 0; i < rows.Count; i++) {
            var row = rows[i];
            bool isCurrent = i == currentRow;

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
