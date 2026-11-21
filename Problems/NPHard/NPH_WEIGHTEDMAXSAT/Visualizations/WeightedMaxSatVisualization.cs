using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPHard.NPH_WEIGHTEDMAXSAT.Solvers;
using API.Problems.NPHard.NPH_WEIGHTEDMAXSAT.Verifiers;

namespace API.Problems.NPHard.NPH_WEIGHTEDMAXSAT.Visualizations;

class WeightedMaxSatVisualization : IVisualization<WEIGHTEDMAXSAT, API_SAT> {
    // --- Fields ---
    public string visualizationName { get; } = "Weighted Max-SAT Visualization";
    public string visualizationDefinition { get; } = "Displays each weighted CNF clause and highlights literals made true by the solution assignment.";
    public string source { get; } = "Redux Boolean satisfiability visualization.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Corbin Hay" };
    public VisualizationType visualizationType { get; } = VisualizationType.BooleanSatisfiability;
    public ISolver solver { get; } = new WeightedMaxSatBruteForceSolver();

    // --- Methods Including Constructors ---
    public WeightedMaxSatVisualization() { }

    public API_SAT visualize(WEIGHTEDMAXSAT problem) => Build(problem);

    public API_JSON SolvedVisualization(WEIGHTEDMAXSAT problem, string solution) {
        API_SAT result = Build(problem);
        (IReadOnlyDictionary<string, bool> Assignment, long Value) parsed;
        try {
            parsed = WeightedMaxSatSolutionVerifier.ParseCertificate(problem, solution);
        } catch (CertificateParseException) {
            return result;
        }

        foreach (API_SAT.Clause clause in result.clauses) {
            foreach (API_SAT.Literal literal in clause.literals) {
                string variable = literal.literal.TrimStart('!');
                if (parsed.Assignment.TryGetValue(variable, out bool value)
                    && value != literal.literal.StartsWith('!'))
                    literal.color = "Solution";
            }
        }
        return result;
    }

    private static API_SAT Build(WEIGHTEDMAXSAT problem) {
        return new API_SAT(problem.weightedClauses.Select((weighted, index) =>
            new API_SAT.Clause(weighted.Clause.Literals.Select(l => l.ToString()).ToList(), index.ToString(), weighted.Weight)).ToList());
    }
}
