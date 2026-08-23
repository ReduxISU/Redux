using API.Interfaces;
using System.Numerics;

namespace API.Problems.NPComplete.NPC_DM3.Solvers;

class ThreeDimensionalMatchingBruteForce : ISolver<DM3> {

    // --- Fields ---
    public string solverName { get; } = "3-Dimensional Matching Brute Force Solver";
    public string solverDefinition { get; } = "This is a generic local search solver for 3-Dimensional Matching, which, while possible, removes one constraint from the current solution, and swaps in two more constraints.";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Caleb Eardley" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Unpruned exhaustive enumeration.
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // SURPRISING: reps is the exact binomial coefficient C(|M|,|X|) (the factorial-ratio formula
    // below, computed in BigInteger because it can be huge) -- NOT a flat 2^n or n! bound. |M|
    // (candidate triples) is an input size independent of |X|, |Y|, |Z|; when |M| is large relative
    // to n = |X| (up to n^3 possible triples), C(|M|,n) grows asymptotically worse than n! (e.g.
    // n=3, |M|=27 already gives C(27,3)=2925 > 3!=6). Exponential is kept as the closest available
    // bucket -- same reasoning as CliqueCoverBruteForce's (K+1)^n case, this combinatorial growth
    // isn't cleanly "exponential" or "factorial" in a single variable either. Each combination costs
    // O(n^2) to verify (GenericVerifierDM3's parse plus per-coordinate List.Contains membership scans).
    public string complexity { get; } = "O(C(m, n) * n^2), m = |M| (candidate triples), n = |X| = |Y| = |Z|";

    // --- Methods Including Constructors ---
    public ThreeDimensionalMatchingBruteForce() {

    }


    private BigInteger factorial(long x) {
        BigInteger y = 1;
        for (BigInteger i = 1; i <= x; i++) {
            y *= i;
        }
        return y;
    }
    private string indexListToCertificate(List<int> indecies, List<List<string>> M) {
        string certificate = "";
        foreach (int i in indecies) {
            string set = "";
            foreach (string e in M[i]) {
                set += "," + e;
            }
            set = "{" + set.Substring(1) + "}";
            certificate += "," + set;
        }
        return "{" + certificate.Substring(1) + "}";
    }
    private List<int> nextComb(List<int> combination, int size) {
        for (int i = combination.Count - 1; i >= 0; i--) {
            if (combination[i] + 1 <= (i + size - combination.Count)) {
                combination[i] += 1;
                for (int j = i + 1; j < combination.Count; j++) {
                    combination[j] = combination[j - 1] + 1;
                }
                return combination;
            }
        }
        return combination;
    }
    public string solve(DM3 problem) {
        List<int> combination = new List<int>();
        for (int i = 0; i < problem.X.Count(); i++) {
            combination.Add(i);
        }
        BigInteger reps = factorial(problem.M.Count()) / (factorial(problem.X.Count()) * factorial(problem.M.Count() - problem.X.Count()));
        for (int i = 0; i < reps; i++) {
            string certificate = indexListToCertificate(combination, problem.M);
            if (problem.defaultVerifier.verify(problem, certificate)) {
                return certificate;
            }
            combination = nextComb(combination, problem.M.Count);

        }
        return "{}";
    }
}
