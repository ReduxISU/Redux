using API.Interfaces;
using API.Problems.NPHard.NPH_WEIGHTEDMAXSAT.Solvers;
using API.Problems.NPHard.NPH_WEIGHTEDMAXSAT.Verifiers;

namespace API.Problems.NPHard.NPH_WEIGHTEDMAX3SAT.Verifiers;

class WeightedMax3SatOptimalityVerifier : IVerifier<WEIGHTEDMAX3SAT> {
    // --- Fields ---
    public string verifierName { get; } = "Weighted Max-3SAT Optimality Verifier";
    public string verifierDefinition { get; } = "Checks assignment/value validity, then exhaustively confirms that no assignment has greater satisfied clause weight. Verification takes O(2^n |Φ|) time and supports at most 20 variables.";
    public string source { get; } = "Exhaustive optimality verification.";
    public string sourceFile { get; } = SourceFile.Path();
    public string certificate { get; } = WeightedMax3SatSolutionVerifier.CertificateExample;
    public string[] contributors { get; } = { "Corbin Hay" };

    // --- Methods Including Constructors ---
    public WeightedMax3SatOptimalityVerifier() { }

    public bool verify(WEIGHTEDMAX3SAT problem, string certificateText) {
        if (!new WeightedMax3SatSolutionVerifier().verify(problem, certificateText)) return false;
        var parsed = WeightedMaxSatSolutionVerifier.ParseCertificate(problem.weightedMaxSat, certificateText);
        return WeightedMaxSatBruteForceSolver.Optimize(
            problem.weightedMaxSat, problemName: problem.problemName).Value == parsed.Value;
    }
}
