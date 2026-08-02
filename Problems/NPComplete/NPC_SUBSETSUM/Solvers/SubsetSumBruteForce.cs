using API.Interfaces;
using SPADE;

namespace API.Problems.NPComplete.NPC_SUBSETSUM.Solvers;
class SubsetSumBruteForce : ISolver<SUBSETSUM> {

    // --- Fields ---
    public string solverName {get;} = "Subset Sum Brute Force Solver";
    public string solverDefinition {get;} = "This is a brute force solver for Subset Sum";
    public string source {get;} = "";
    public string[] contributors {get;} = { "Caleb Eardley","Garret Stouffer"};
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Unpruned exhaustive enumeration.
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // Declared, not derived. Enumerates all 2^n subset bitmasks; each candidate does O(n)
    // certificate building and an O(n) dictionary-based verify() -- no per-candidate factor
    // beyond linear.
    public string complexity { get; } = "O(n * 2^n), n = |S|";

    // --- Methods Including Constructors ---
    public SubsetSumBruteForce() {

    }

    private static string SubsetToCertificate(int mask, List<string> S) {
        UtilCollection certificate = new UtilCollection("{}");
        for (int i = 0; i < S.Count; i++) {
            if ((mask & (1 << i)) != 0)
                certificate.Add(new UtilCollection(S[i]));
        }
        return certificate.ToString();
    }

    public string solve(SUBSETSUM subsetSum){
        int n = subsetSum.S.Count;

        // Enumerate every subset by its bitmask so that all 2^n subsets -- including
        // the singleton {S[0]} -- are tested exactly once. Mask 0 (the empty subset)
        // is skipped: with positive integers and T > 0 it is never a witness, and
        // "{}" doubles as the no-solution sentinel.
        for (int mask = 1; mask < (1 << n); mask++) {
            if (timerHasExpired) return "{}";

            string certificate = SubsetToCertificate(mask, subsetSum.S);
            if (subsetSum.defaultVerifier.verify(subsetSum, certificate)) {
                return certificate;
            }
        }
        return "{}";
    }
}
