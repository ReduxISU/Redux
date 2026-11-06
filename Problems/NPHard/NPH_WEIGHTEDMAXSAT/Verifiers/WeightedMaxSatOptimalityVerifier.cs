using API.Interfaces;
using API.Problems.NPHard.NPH_WEIGHTEDMAXSAT.Solvers;

namespace API.Problems.NPHard.NPH_WEIGHTEDMAXSAT.Verifiers;

class WeightedMaxSatOptimalityVerifier : IVerifier<WEIGHTEDMAXSAT> {
    // --- Fields ---
    public string verifierName { get; } = "Weighted Max-SAT Optimality Verifier";
    public string verifierDefinition { get; } = "Checks assignment/value validity, then exhaustively confirms that no assignment has greater satisfied weight. Verification takes O(2^n |Φ|) time.";
    public string source { get; } = "Exhaustive optimality verification.";
    public string sourceFile { get; } = SourceFile.Path();
    public string certificate { get; } = "((x1:False,x2:True,x3:True,x4:False),3)";
    public string[] contributors { get; } = { "Corbin Hay" };

    // --- Methods Including Constructors ---
    public WeightedMaxSatOptimalityVerifier() { }

    public bool verify(WEIGHTEDMAXSAT problem, string certificateText) {
        var solutionVerifier = new WeightedMaxSatSolutionVerifier();
        if (!solutionVerifier.verify(problem, certificateText)) return false;
        var parsed = WeightedMaxSatSolutionVerifier.ParseCertificate(problem, certificateText);
        return WeightedMaxSatBruteForceSolver.Optimize(problem).Value == parsed.Value;
    }
}
