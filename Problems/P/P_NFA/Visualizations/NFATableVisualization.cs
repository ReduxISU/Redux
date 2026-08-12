using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Tables;
using API.Problems.P.P_NFA;
using API.Problems.P.P_NFA.Solvers;
using NFATableStep = API.Problems.P.P_NFA.Solvers.NFASolver.NFATableStep;

namespace API.Problems.P.P_NFA.Visualizations;

class NFATableVisualization : IVisualization<NFA>
{
    public string visualizationName { get; } = "Non-deterministic Finite Automata Table Visualization";
    public string visualizationDefinition { get; } = "Displays a table for one explored run of the NFA on the input string at a time, showing the symbol consumed, the state transition, and acceptance at each step of that run. Accepting runs are listed before rejected runs, so the default run shown is the first accepting run, or the first rejected run if none accept.";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType => VisualizationType.DynamicTable;
    public ISolver solver { get; } = new NFASolver();

    public NFATableVisualization() { }

    // `visualize`/`SolvedVisualization` deliberately return API_empty: the controller
    // concatenates visualize() + StepsVisualization() + SolvedVisualization() into one flat
    // list with no de-duplication, and both of those would otherwise just repeat an entry
    // already present in StepsVisualization(), producing duplicate steps in the step slider.
    public API_JSON visualize(NFA problem)
    {
        return new API_empty();
    }

    public API_JSON SolvedVisualization(NFA problem, string solution)
    {
        return new API_empty();
    }

    public List<API_JSON> StepsVisualization(NFA problem, List<Object> steps)
    {
        var nfaSolver = new NFASolver();
        var tableSteps = nfaSolver.GetTableSteps(problem);
        return tableSteps.Select(s => (API_JSON)TranslateToTableJSON((NFATableStep)s)).ToList();
    }

    private static API_TableJSON TranslateToTableJSON(NFATableStep step)
    {
        // Unlike DFA/SPSP/SSSP, consecutive frames here are different runs rather than later
        // moments of one run, so the caption is what tells the reader which of them they are on.
        var result = new API_TableJSON
        {
            title = $"Run {step.pathIndex + 1} of {step.pathCount} — {(step.accepted ? "Accepted" : "Rejected")}",
            columns = new List<TableColumn>
            {
                new TableColumn { key = "step", label = "Step" },
                new TableColumn { key = "symbol", label = "Symbol" },
                new TableColumn { key = "fromState", label = "From State" },
                new TableColumn { key = "toState", label = "To State" },
                new TableColumn { key = "accepting", label = "Accepting" }
            }
        };

        for (int i = 0; i < step.rows.Count; i++)
        {
            var row = step.rows[i];
            bool isLast = i == step.rows.Count - 1;

            result.rows.Add(new TableRow
            {
                id = row.step.ToString(),
                cells = new Dictionary<string, string>
                {
                    { "step", row.step.ToString() },
                    { "symbol", row.symbol },
                    { "fromState", row.fromState },
                    { "toState", row.toState },
                    { "accepting", row.accepting ? "✅" : "❌" }
                },
                // A run's whole table is shown at once, so the "current" row is its final state:
                // tinted Solution only when that final state is what made the run accept.
                color = isLast && step.accepted ? "Solution" : null,
                cellColors = isLast
                    ? new Dictionary<string, string> { { "toState", "ElementHighlight" } }
                    : null
            });
        }
        return result;
    }
}
