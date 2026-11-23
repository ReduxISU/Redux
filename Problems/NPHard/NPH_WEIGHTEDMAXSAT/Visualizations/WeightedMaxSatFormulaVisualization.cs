using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.JSON_Objects;
using API.Interfaces.Steps;
using API.Problems.NPHard.NPH_WEIGHTEDMAXSAT.Solvers;
using API.Problems.NPHard.NPH_WEIGHTEDMAXSAT.Verifiers;

namespace API.Problems.NPHard.NPH_WEIGHTEDMAXSAT.Visualizations;

/// <summary>
/// Weighted Max-SAT's formula picture: the CNF as it is written, one box per clause, with the assignment in a strip of the
/// variables. It draws the same Formula picture type as SAT's, so it needs no frontend work. The list format, which old
/// frontends read, is exactly <see cref="WeightedMaxSatVisualization"/>'s.
/// </summary>
class WeightedMaxSatFormulaVisualization : IVisualization<WEIGHTEDMAXSAT, API_SAT>, IFormulaVisualization<WEIGHTEDMAXSAT> {
    public string visualizationName { get; } = "Weighted Max-SAT Formula";
    public string visualizationDefinition { get; } = "The weighted formula as it is written: one box per clause and each variable's value underneath. Each clause shows whether the assignment satisfies it, so the clauses the answer gives up are the ones left unsatisfied.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.BooleanSatisfiability;
    public ISolver solver { get; } = new WeightedMaxSatBruteForceSolver();

    private readonly WeightedMaxSatVisualization _listFormat = new();

    public API_SAT visualize(WEIGHTEDMAXSAT problem) => _listFormat.visualize(problem);

    public API_JSON SolvedVisualization(WEIGHTEDMAXSAT problem, string solution) => _listFormat.SolvedVisualization(problem, solution);

    // --- Formula picture (format=frames only) ---

    // What the problem is: its clauses, written the way SAT's are.
    GroupedGraphSpec IFormulaVisualization<WEIGHTEDMAXSAT>.Describe(WEIGHTEDMAXSAT problem) =>
        GroupedGraphSpec.FromLiterals(problem.weightedClauses.Select(w => w.Clause.Literals.Select(l => l.ToString())));

    // A Weighted Max-SAT answer is "((x1:False,...),3)": the assignment and its weight. The picture draws the assignment.
    Assignment IFormulaVisualization<WEIGHTEDMAXSAT>.ParseAnswer(WEIGHTEDMAXSAT problem, string answer) {
        try {
            return GroupedGraphFrames.ToAssignment(WeightedMaxSatSolutionVerifier.ParseCertificate(problem, answer).Assignment);
        } catch (CertificateParseException) {
            return new Assignment([]);
        }
    }
}
