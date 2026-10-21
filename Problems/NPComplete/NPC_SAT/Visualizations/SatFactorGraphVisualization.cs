using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_SAT;
using API.Problems.NPComplete.NPC_SAT.Solvers;

/// <summary>
/// SAT's second picture, its factor graph: variables on the left, clauses on the right, one edge per literal (dashed for
/// a negated one). It is served only by <c>format=frames</c> (type <c>BipartiteGraph</c>). The list format, which old
/// frontends read, is exactly <see cref="SatDefaultVisualization"/>'s, so this entry renders like the default SAT
/// picture until a frontend draws the factor graph.
/// </summary>
class SatFactorGraphVisualization : IVisualization<SAT, API_SAT>, IFactorGraphVisualization<SAT>, IOptInVisualization {
    public string visualizationName { get; } = "SAT Factor Graph";
    public string visualizationDefinition { get; } = "The factor graph of a SAT formula: variables on one side, clauses on the other, and an edge for every literal (dashed when negated). With format=frames, the edges an assignment makes true turn green, step by step.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.BooleanSatisfiability;
    public ISolver solver { get; } = new SATBruteForceSolver();

    private readonly SatDefaultVisualization _listFormat = new();

    public API_SAT visualize(SAT instance) => _listFormat.visualize(instance);

    public API_JSON SolvedVisualization(SAT instance, string solution) => _listFormat.SolvedVisualization(instance, solution);

    // --- Factor graph picture (format=frames only) ---
    Type? IVisualization.StepShape => typeof(Assignment);

    GroupedGraphSpec IFactorGraphVisualization<SAT>.Describe(SAT problem) =>
        GroupedGraphSpec.FromLiterals(problem.clauses);
}
