using API.Interfaces;
using SPADE;

namespace API.Problems.NPComplete.NPC_SUBSETPRODUCT.Solvers;

class SubsetProductSolver : ISolver<SUBSETPRODUCT> {

    // --- Fields ---
    public string solverName { get; } = "Subset Product Brute Force";
    public string solverDefinition { get; } = "Tries every non-empty subset of S and returns the first one the verifier accepts.";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Michael Trosper" };
    // TODO: replace Unclassified. A test in redux-tests/Metadata fails until you do, on purpose. See Interfaces/SolverType.cs for what each value means.
    public SolverType solverType { get; } = SolverType.Unclassified;
    // TODO: replace Unclassified. A test in redux-tests/Metadata fails until you do, on purpose. See Interfaces/SolverComplexityBucket.cs for what each value means.
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Unclassified;
    // TODO: replace "" with a confidently-known Big-O string such as "O(n log n)", never a guess. A test in redux-tests/Metadata fails until you do, on purpose.
    public string complexity { get; } = "";
    public bool timerHasExpired { get; set; }

    // --- Methods Including Constructors ---
    public SubsetProductSolver() { }

    private static string SubsetToCertificate(int mask, List<string> S) {
        UtilCollection certificate = new UtilCollection("{}");
        for (int i = 0; i < S.Count; i++) {
            if ((mask & (1 << i)) != 0)
                certificate.Add(new UtilCollection(S[i]));
        }
        return certificate.ToString();
    }

    public string solve(SUBSETPRODUCT problem) {
        int attempts = 0;
        int n = problem.S.Count;
        for (int mask = 1; mask < (1 << n); mask++) {
            if (timerHasExpired) return "{}";

            string certificate = SubsetToCertificate(mask, problem.S);
            if (problem.defaultVerifier.verify(problem, certificate)) {
                return certificate;
            }
        }
        return "{}";
    }
}
