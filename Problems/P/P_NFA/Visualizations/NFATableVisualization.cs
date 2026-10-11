using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Tables;
using API.Interfaces.Table;
using API.Problems.P.P_NFA;
using API.Problems.P.P_NFA.Solvers;
using NFATableStep = API.Problems.P.P_NFA.Solvers.NFASolver.NFATableStep;

namespace API.Problems.P.P_NFA.Visualizations;

/// <summary>
/// The NFA's run tables. The list format is one table per run, as it always was; under format=frames it is the Table
/// picture's trace table (<see cref="AutomatonTraceTable"/>), a second view of the steps the Automaton picture draws: with
/// NFA Backtracking (the default) the branch being explored, row by row; with NFA Subset Simulation one row per symbol with
/// every active state. The NFA's default visualization is unchanged.
/// </summary>
class NFATableVisualization : IVisualization<NFA, API_empty>, ITableVisualization<NFA, ActiveStates> {
    public string visualizationName { get; } = "Non-Deterministic Finite Automata Table Visualization";
    public string visualizationDefinition { get; } = "Displays a table for one explored run of the NFA on the input string at a time, showing the symbol consumed, the state transition, and acceptance at each step of that run. Accepting runs are listed before rejected runs, so the default run shown is the first accepting run, or the first rejected run if none accept.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType => VisualizationType.DynamicTable;
    public ISolver solver { get; } = new NFASolver();

    // Implemented explicitly so it is not a public member (and so not part of /info).
    // The table is still built from the solver's own run enumeration (GetTableSteps), not from the typed
    // steps, but it declares the shape its solver records so the steps are not dropped as a mismatch.
    Type? IVisualization.StepShape => typeof(ActiveStates);

    // The list format only draws tables when it is given steps (see StepsVisualization), which it was before the Table
    // picture existed, so it keeps getting them.
    bool IFramesVisualization.ListReadsTypedSteps() => true;

    // The Table picture (format=frames only). Implemented explicitly so it is not part of /info. A single state is written
    // as its name, a set of them (subset simulation) in braces.
    TableSpec ITableVisualization<NFA, ActiveStates>.Describe(NFA problem) => AutomatonTraceTable.Spec();
    IReadOnlyList<TableDraft> ITableVisualization<NFA, ActiveStates>.Draw(NFA problem, IReadOnlyList<SolverStep<ActiveStates>> steps, bool incremental) {
        // Sets of states (subset simulation) are written in braces once any of them has more than one state.
        bool sets = steps.Any(s => s.Partial.States.Length > 1);
        return AutomatonTraceTable.Drafts(steps, AutomatonFrames.InputSymbols(problem.inputString),
            states => states.Count == 1 && !sets ? states[0] : AutomatonFrames.Braces(states), problem.acceptStates.Contains, AutomatonTraceTable.Dead, oneRun: false);
    }
    TableDraft ITableVisualization<NFA, ActiveStates>.Blank(NFA problem) => new();

    public NFATableVisualization() { }

    // `visualize`/`SolvedVisualization` deliberately return API_empty: the controller
    // concatenates visualize() + StepsVisualization() + SolvedVisualization() into one flat
    // list with no de-duplication, and both of those would otherwise just repeat an entry
    // already present in StepsVisualization(), producing duplicate steps in the step slider.
    public API_empty visualize(NFA problem) {
        return new API_empty();
    }

    public API_JSON SolvedVisualization(NFA problem, string solution) {
        return new API_empty();
    }

    public List<API_JSON> StepsVisualization(NFA problem, List<Object> steps) => Tables(problem, null);

    // The visualize endpoint's entry point. The tables come from re-enumerating every run (GetTableSteps), not from
    // the steps, so that enumeration gets the endpoint's time limit: it can take as long as the solve (#764).
    List<API_JSON> IVisualization.StepsVisualization(string problem, List<Object> steps, TimeSpan? timeLimit) {
        if (steps.Count == 0)
            return new List<API_JSON>();
        return Tables(ParseGuard.CreateProblem<NFA>(problem), timeLimit);
    }

    // One table per run; when the time limit runs out first, one empty table that says so instead.
    private static List<API_JSON> Tables(NFA problem, TimeSpan? timeLimit) {
        var rec = timeLimit is null ? StepRecorder<ActiveStates>.Off : new StepRecorder<ActiveStates>(cap: 0, timeLimit);
        try {
            var tableSteps = new NFASolver().GetTableSteps(problem, rec);
            return tableSteps.Select(s => (API_JSON)TranslateToTableJSON((NFATableStep)s)).ToList();
        } catch (StepBudgetExceededException ex) when (ex.Recorder == rec) {
            return new List<API_JSON> { new API_TableJSON { title = StoppedTitle, columns = Columns() } };
        }
    }

    internal const string StoppedTitle = "Stopped: the time limit ran out before every run was listed.";

    private static List<TableColumn> Columns() => new List<TableColumn>
    {
        new TableColumn { key = "step", label = "Step" },
        new TableColumn { key = "symbol", label = "Symbol" },
        new TableColumn { key = "fromState", label = "From State" },
        new TableColumn { key = "toState", label = "To State" },
        new TableColumn { key = "accepting", label = "Accepting" }
    };

    private static API_TableJSON TranslateToTableJSON(NFATableStep step) {
        // Unlike DFA/SPSP/SSSP, consecutive frames here are different runs rather than later
        // moments of one run, so the caption is what tells the reader which of them they are on.
        var result = new API_TableJSON {
            title = $"Run {step.pathIndex + 1} of {step.pathCount} — {(step.accepted ? "Accepted" : "Rejected")}",
            columns = Columns()
        };

        for (int i = 0; i < step.rows.Count; i++) {
            var row = step.rows[i];
            bool isLast = i == step.rows.Count - 1;

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
