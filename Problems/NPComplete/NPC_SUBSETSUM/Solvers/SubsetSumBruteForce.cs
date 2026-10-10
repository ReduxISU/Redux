using API.Interfaces;
using SPADE;

namespace API.Problems.NPComplete.NPC_SUBSETSUM.Solvers;

class SubsetSumBruteForce : ISolver<SUBSETSUM> {

    // --- Fields ---
    public string solverName { get; } = "Subset Sum Brute Force";
    public string solverDefinition { get; } = "This is a brute force solver for Subset Sum";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Caleb Eardley", "Garret Stouffer" };
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

    private static string SubsetToCertificate(bool[] picked, List<string> S) {
        UtilCollection certificate = new UtilCollection("{}");
        for (int i = 0; i < S.Count; i++) {
            if (picked[i])
                certificate.Add(new UtilCollection(S[i]));
        }
        return certificate.ToString();
    }

    // Moves to the next subset in mask order (bit i picks item i), like adding 1 to the mask. Returns false once every
    // subset has been seen. A bool per item, not an int mask, so there is no limit on the number of items (#791).
    private static bool NextSubset(bool[] picked) {
        for (int i = 0; i < picked.Length; i++) {
            picked[i] = !picked[i];
            if (picked[i]) return true;
        }
        return false;
    }

    public string solve(SUBSETSUM subsetSum) {
        int n = subsetSum.S.Count;

        // Enumerate every subset in bitmask order (mask 1, 2, 3, ...) so that all 2^n subsets -- including
        // the singleton {S[0]} -- are tested exactly once. Mask 0 (the empty subset)
        // is skipped: with positive integers and T > 0 it is never a witness, and
        // "{}" doubles as the no-solution sentinel.
        bool[] picked = new bool[n];
        while (NextSubset(picked)) {
            if (timerHasExpired) return "{}";

            string certificate = SubsetToCertificate(picked, subsetSum.S);
            if (subsetSum.defaultVerifier.verify(subsetSum, certificate)) {
                return certificate;
            }
        }
        return "{}";
    }
}
