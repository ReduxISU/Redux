using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;
using System.Numerics;

namespace API.Problems.NPComplete.NPC_SETCOVER.Solvers;

class SetCoverBruteForce : ISolver<SETCOVER> {

    // --- Fields ---
    public string solverName { get; } = "Set Cover Brute Force Solver";
    public string solverDefinition { get; } = "This is a brute force solver for the NP-Complete Set Cover problem";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Unpruned exhaustive enumeration.
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // Declared, not derived. Outer loop runs K times; for j = 0..K-1 the inner loop runs
    // C(m, j) times (m = |subsets|), so total iterations are bounded by sum_{j=0}^{K-1} C(m,j)
    // <= 2^m. Each iteration's verify() call additionally costs O(u^2) (List.Contains/Remove
    // scans over the universal set, u = |universal|).
    public string complexity { get; } = "O(2^m * u^2), m = |subsets|, u = |universal|";

    // --- Methods Including Constructors ---
    public SetCoverBruteForce() {

    }
    private BigInteger factorial(BigInteger x) {
        BigInteger y = 1;
        for (BigInteger i = 1; i <= x; i++) {
            y *= i;
        }
        return y;
    }
    private string indexListToCertificate(List<int> indecies, List<List<string>> subsets) {

        string certificate = "{";
        foreach (int i in indecies) {
            foreach (var j in subsets[i]) {
                certificate += j + ",";
            }
            certificate = certificate.TrimEnd(',') + "},{";
        }
        certificate = certificate.TrimEnd('{');
        certificate = certificate.TrimEnd(',');

        return "{" + certificate + "}";
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
    public string solve(SETCOVER setCover) {
        for (int j = 0; j < setCover.K; j++) {
            List<int> combination = new List<int>();
            for (int i = 0; i <= j; i++) {
                combination.Add(i);
            }
            BigInteger reps = factorial(setCover.subsets.Count) / (factorial(j) * factorial(setCover.subsets.Count - j));
            for (BigInteger i = 0; i < reps; i++) {
                string certificate = indexListToCertificate(combination, setCover.subsets);
                if (setCover.defaultVerifier.verify(setCover, certificate)) {
                    return certificate;
                }
                combination = nextComb(combination, setCover.subsets.Count);

            }
        }
        return "{}";
    }

}
