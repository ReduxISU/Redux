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
    // The outer loop sums C(m, i+1) (m = |edges|) over i from |terminals|-1 to K -- a partial sum of
    // binomial coefficients of m, which is bounded above by the sum of ALL of them, 2^m, regardless of
    // where K falls (and reaches that order whenever K is near m/2). So this enumerates edge subsets,
    // not node subsets or permutations. Each candidate costs O(i) to build (indexListToCertificate) and
    // SteinerTreeVerifier's IsConnected/terminal-coverage check costs O(i*t) (t = |terminals|, from the
    // per-edge-endpoint List.Contains/Remove scans against the terminals list), i <= m.
    // Written with the K bound, so the Big-O precheck (#843) does not refuse a small K for what a large K costs.
    public MathematicalFunction timeComplexity { get; } = new("min(2^|E|, (|E|+1)^(K+1)) * (|E| + 1) * |R|", ", |E| = number of edges, |R| = number of terminals, K = edge bound");

    // --- Methods Including Constructors ---
    public SteinerTreeBruteForce() {

    }

    // The number of ways to choose k of n, as n/1 * (n-1)/2 * ...: each partial product is itself a binomial, so it divides
    // exactly. Written so in place of n! / (k! (n-k)!), whose factorials overflow a long past 20 items and gave a wrong count.
    private static long Binomial(long n, long k) {
        long r = 1;
        for (long j = 1; j <= k; j++) r = checked(r * (n - k + j)) / j;
        return r;
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

    private string indexListToCertificate(List<int> binary, List<KeyValuePair<string, string>> edges) {
        string certificate = "";
        foreach (int i in binary) {
            certificate += "{" + edges[i].Key + ',' + edges[i].Value + "},";
        }
        return "{" + certificate.TrimEnd(',') + "}";
    }

    public string solve(STEINERTREE steiner) {

        for (int i = steiner.terminals.Count - 1; i <= steiner.K; i++) {
            List<int> combination = new List<int>();
            for (int j = 0; j < i; j++) {
                combination.Add(j);
            }

            // Each candidate has i edges, so there are C(|E|, i) of them (this once counted the sets of i + 1).
            long reps = Binomial(steiner.edges.Count, i);
            for (int k = 0; k < reps; k++) {
                if (timerHasExpired) return SolveRun.TimedOutAnswer; // #935: stop at the time limit
                string certificate = indexListToCertificate(combination, steiner.edges);
                if (steiner.defaultVerifier.verify(steiner, certificate)) {
                    return certificate;
                }
                combination = nextComb(combination, steiner.edges.Count);

            }

        }
        return "{}";
    }

}
