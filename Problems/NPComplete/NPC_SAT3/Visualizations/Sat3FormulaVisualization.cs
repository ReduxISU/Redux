using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_SAT3;
using API.Problems.NPComplete.NPC_SAT3.Solvers;

/// <summary>
/// 3SAT's formula picture (#925): the CNF as it is written, one box per clause, the literals of each joined by ∨ and the
/// clauses by ∧, with the assignment so far in a strip of the variables. It is served only by <c>format=frames</c> (type
/// <c>Formula</c>) and draws the same ids, states and steps as the clause graph. The list format, which old frontends read, is
/// exactly <see cref="Sat3DefaultVisualization"/>'s, so this entry renders like the default 3SAT picture until a frontend draws
/// the formula.
/// </summary>
class Sat3FormulaVisualization : IVisualization<SAT3>, IFormulaVisualization<SAT3> {
    public string visualizationName { get; } = "3SAT Formula";
    public string visualizationDefinition { get; } = "The 3SAT formula as it is written: one box per clause, its literals joined by OR, the clauses joined by AND, and each variable's value so far underneath. With format=frames, each literal shows whether it is true or false under the assignment, step by step, and each clause whether it is satisfied.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.BooleanSatisfiability;
    public ISolver solver { get; } = new Sat3BacktrackingSolver();

    private readonly Sat3DefaultVisualization _listFormat = new();

    public API_JSON visualize(SAT3 instance) => _listFormat.visualize(instance);

    public API_JSON SolvedVisualization(SAT3 instance, string solution) => _listFormat.SolvedVisualization(instance, solution);

    // --- Formula picture (format=frames only) ---
    Type? IVisualization.StepShape => typeof(Assignment);

    GroupedGraphSpec IFormulaVisualization<SAT3>.Describe(SAT3 problem) =>
        GroupedGraphSpec.FromLiterals(problem.clauses);
}
