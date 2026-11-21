using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPHard.NPH_WEIGHTEDMAXSAT.Visualizations;
using API.Problems.NPHard.NPH_WEIGHTEDMAX3SAT.Solvers;

namespace API.Problems.NPHard.NPH_WEIGHTEDMAX3SAT.Visualizations;

[DefaultFor(typeof(WEIGHTEDMAX3SAT))]
class WeightedMax3SatVisualization : IVisualization<WEIGHTEDMAX3SAT, API_SAT> {
    // --- Fields ---
    public string visualizationName { get; } = "Weighted Max-3SAT Visualization";
    public string visualizationDefinition { get; } = "Displays each weighted CNF clause and highlights literals made true by the solution assignment.";
    public string source { get; } = "Redux Boolean satisfiability visualization.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Corbin Hay" };
    public VisualizationType visualizationType { get; } = VisualizationType.BooleanSatisfiability;
    public ISolver solver { get; } = new WeightedMax3SatBruteForceSolver();

    private readonly WeightedMaxSatVisualization _visualization = new();

    // --- Methods Including Constructors ---
    public WeightedMax3SatVisualization() { }

    public API_SAT visualize(WEIGHTEDMAX3SAT problem) =>
        _visualization.visualize(problem.weightedMaxSat);

    public API_JSON SolvedVisualization(WEIGHTEDMAX3SAT problem, string solution) =>
        _visualization.SolvedVisualization(problem.weightedMaxSat, solution);
}
