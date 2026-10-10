using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;

namespace API.Problems.NPComplete.NPC_STEINERTREE.Solvers;

class SteinerTreeBruteForce : ISolver<STEINERTREE> {

    // --- Fields ---
    public string solverName { get; } = "Steiner Tree Brute Force";
    public string solverDefinition { get; } = "This is a brute force solver for the NP-Complete Steiner Tree problem";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Unpruned exhaustive enumeration.
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // The outer loop sums C(m, i) (m = |edges|) over i from |terminals|-1 to min(K, m) -- a partial sum of
    // binomial coefficients of m, which is bounded above by the sum of ALL of them, 2^m, regardless of
    // where K falls (and reaches that order whenever K is near m/2). So this enumerates edge subsets,
    // not node subsets or permutations. Each candidate costs O(i) to build (indexListToCertificate) and
    // SteinerTreeVerifier's IsConnected/terminal-coverage check costs O(i*t) (t = |terminals|, from the
    // per-edge-endpoint List.Contains/Remove scans against the terminals list), i <= m.
    public string complexity { get; } = "O(2^m * m * t), m = |edges|, t = |terminals|";

    // --- Methods Including Constructors ---
    public SteinerTreeBruteForce() {

    }

    // Moves combination to the next combination of its size from 0..size-1, in lexicographic order. Returns false,
    // leaving it unchanged, when it is already the last one (an empty combination has no next one).
    private bool nextComb(List<int> combination, int size) {
        for (int i = combination.Count - 1; i >= 0; i--) {
            if (combination[i] + 1 <= (i + size - combination.Count)) {
                combination[i] += 1;
                for (int j = i + 1; j < combination.Count; j++) {
                    combination[j] = combination[j - 1] + 1;
                }
                return true;
            }
        }
        return false;
    }

    private string indexListToCertificate(List<int> binary, List<KeyValuePair<string, string>> edges) {
        string certificate = "";
        foreach (int i in binary) {
            certificate += "{" + edges[i].Key + ',' + edges[i].Value + "},";
        }
        return "{" + certificate.TrimEnd(',') + "}";
    }

    public string solve(STEINERTREE steiner) {

        // Every combination of i edges, for i from one fewer than the terminals up to K (no more than the edges there are).
        int largest = Math.Min(steiner.K, steiner.edges.Count);
        for (int i = Math.Max(steiner.terminals.Count - 1, 0); i <= largest; i++) {
            List<int> combination = new List<int>();
            for (int j = 0; j < i; j++) {
                combination.Add(j);
            }

            do {
                string certificate = indexListToCertificate(combination, steiner.edges);
                if (steiner.defaultVerifier.verify(steiner, certificate)) {
                    return certificate;
                }
            } while (nextComb(combination, steiner.edges.Count));

        }
        return "{}";
    }

}
