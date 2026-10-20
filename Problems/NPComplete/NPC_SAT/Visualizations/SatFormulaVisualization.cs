using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_SAT;
using API.Problems.NPComplete.NPC_SAT.Solvers;

/// <summary>
/// SAT's formula picture (#925): the CNF as it is written, one box per clause, the literals of each joined by ∨ and the clauses
/// by ∧, with the assignment so far in a strip of the variables. It is served only by <c>format=frames</c> (type
/// <c>Formula</c>) and draws the same ids, states and steps as the clause graph. The list format, which old frontends read, is
/// exactly <see cref="SatDefaultVisualization"/>'s, so this entry renders like the default SAT picture until a frontend draws
/// the formula.
/// </summary>
class SatFormulaVisualization : IVisualization<SAT, API_SAT>, IFormulaVisualization<SAT> {
    public string visualizationName { get; } = "SAT Formula";
    public string visualizationDefinition { get; } = "The SAT formula as it is written: one box per clause, its literals joined by OR, the clauses joined by AND, and each variable's value so far underneath. With format=frames, each literal shows whether it is true or false under the assignment, step by step, and each clause whether it is satisfied.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.BooleanSatisfiability;
    public ISolver solver { get; } = new SATBruteForceSolver();

    private readonly SatDefaultVisualization _listFormat = new();

    public API_SAT visualize(SAT instance) => _listFormat.visualize(instance);

    public API_JSON SolvedVisualization(SAT instance, string solution) => _listFormat.SolvedVisualization(instance, solution);

    // --- Formula picture (format=frames only) ---
    Type? IVisualization.StepShape => typeof(Assignment);

    GroupedGraphSpec IFormulaVisualization<SAT>.Describe(SAT problem) =>
        GroupedGraphSpec.FromLiterals(problem.clauses);
}
