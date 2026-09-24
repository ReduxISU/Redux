using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using System.Linq;
using System.Numerics;

namespace API.Problems.NPHard.NPH_MINIMUMVERTEXCOVER.Solvers;

class BruteForceMinimumVertexCover : ISolver<MINIMUMVERTEXCOVER> {

    // --- Fields ---
    public string solverName { get; } = "Minimum Vertex Cover Brute Force";
    public string solverDefinition { get; } = "This solver tests every subset size, smallest first, and returns the first subset that covers all edges.";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public bool timerHasExpired { get; set; }
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    public string complexity { get; } = "O(2^n * n^2), n = |nodes|";

    // --- Methods Including Constructors ---
    public BruteForceMinimumVertexCover() {

    }

    private BigInteger factorial(BigInteger x) {
        BigInteger y = 1;
        for (BigInteger i = 1; i <= x; i++) {
            y *= i;
        }
        return y;
    }
    private string indexListToCertificate(List<int> indecies, List<string> nodes) {
        return "{" + string.Join(",", indecies.Select(i => nodes[i])) + "}";
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
    /// <summary>
    /// Checks whether the given set of node indices covers every edge of G.
    /// </summary>
    private bool isCover(MINIMUMVERTEXCOVER G, List<int> combination) {
        var covered = new HashSet<string>(combination.Select(i => G.nodes[i]));
        foreach (var edge in G.edges) {
            if (!covered.Contains(edge.Key) && !covered.Contains(edge.Value)) {
                return false;
            }
        }
        return true;
    }
    /// <summary>
    /// Solves a MINIMUMVERTEXCOVER instance input.
    /// </summary>
    /// <param name="G"> G is an undirected graph instance string</param>
    /// <returns>
    ///  Smallest subset of nodes that covers all edges of G.
    /// </returns>
    public string solve(MINIMUMVERTEXCOVER G) {
        int n = G.nodes.Count;

        if (G.edges.Count == 0) {
            return "{}";
        }

        for (int k = 1; k <= n; k++) {
            List<int> combination = new List<int>();
            for (int i = 0; i < k; i++) {
                combination.Add(i);
            }
            BigInteger reps = factorial(n) / (factorial(k) * factorial(n - k));
            for (int i = 0; i < reps; i++) {
                if (isCover(G, combination)) {
                    return indexListToCertificate(combination, G.nodes);
                }
                combination = nextComb(combination, n);
            }
        }
        return "{}";
    }
}