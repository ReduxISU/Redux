using API.Interfaces;
using API.Problems.NPHard.NPH_WEIGHTEDMAXSAT.Verifiers;

namespace API.Problems.NPHard.NPH_WEIGHTEDMAX3SAT.Verifiers;

[DefaultFor(typeof(WEIGHTEDMAX3SAT))]
class WeightedMax3SatSolutionVerifier : IVerifier<WEIGHTEDMAX3SAT> {
    internal const string CertificateExample = "((x1:False,x2:True,x3:True,x4:False),3)";

    // --- Fields ---
    public string verifierName { get; } = "Weighted Max-3SAT Solution Verifier";
    public string verifierDefinition { get; } = "Checks that a certificate assigns every problem variable exactly once, contains no unknown variables, and reports the resulting satisfied clause weight. It does not check optimality.";
    public string source { get; } = "Direct certificate evaluation.";
    public string sourceFile { get; } = SourceFile.Path();
    public string certificate { get; } = CertificateExample;
    public string[] contributors { get; } = { "Corbin Hay" };

    // --- Methods Including Constructors ---
    public WeightedMax3SatSolutionVerifier() { }

    public bool verify(WEIGHTEDMAX3SAT problem, string certificateText) {
        try {
            return new WeightedMaxSatSolutionVerifier().verify(problem.weightedMaxSat, certificateText);
        } catch (CertificateParseException ex) {
            throw new CertificateParseException(problem, certificateText, ex.Message, ex);
        }
    }
}
