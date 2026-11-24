using System.Numerics;
using API.Interfaces;

namespace API.Problems.NPComplete.NPC_UNRESTRICTEDLONGESTPATH.Solvers;

class UnrestrictedLongestPathSubsetDP : ISolver<UNRESTRICTEDLONGESTPATH> {
    public string solverName { get; } = "Unrestricted Longest Path Subset Dynamic Programming";
    public string solverDefinition { get; } =
        "Exact sparse subset dynamic programming. State (S,v) records a simple path visiting exactly S and ending at v. " +
        "Equivalent prefixes are merged; parent links reconstruct a witness after K+1 vertices. " +
        "Returns a path, {} for an exhaustive NO, or Redux's stopped message.";
    public string source { get; } =
        "Held, M., and Karp, R. M. (1962). A Dynamic Programming Approach to Sequencing Problems. " +
        "Journal of the Society for Industrial and Applied Mathematics, 10(1), 196-210. " +
        "Adaptation of the subset-and-endpoint paradigm to unweighted simple-path feasibility.";
    public string sourceLink { get; } = "https://doi.org/10.1137/0110015";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = Array.Empty<string>();
    private volatile bool expired;
    public bool timerHasExpired { get => expired; set => expired = value; }
    public SolverType solverType { get; } = SolverType.DynamicProgramming;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // BigInteger masks can span O(n) machine words: include that conservative factor.
    public MathematicalFunction timeComplexity { get; } = new("2^n * n * (n + m)", ", n = |V|, m = |E|; expected dictionary costs with BigInteger operations, excluding parsing");
    public MathematicalFunction spaceComplexity { get; } = new("2^n * n^2 + m", ", n = |V|, m = |E|; conservative machine-word bound including BigInteger masks, excluding parsing");

    public UnrestrictedLongestPathSubsetDP() { }

    public string solve(UNRESTRICTEDLONGESTPATH problem) {
        if (timerHasExpired) return UnrestrictedLongestPathSearchGraph.Timeout;
        if (UnrestrictedLongestPathSearchGraph.TryTrivial(problem, out string answer)) return answer;
        var graph = UnrestrictedLongestPathSearchGraph.Create(problem, () => timerHasExpired);
        if (graph is null) return UnrestrictedLongestPathSearchGraph.Timeout;

        // Arbitrary-width masks avoid 32/64-bit shift limits. No hard graph-size cutoff returns a false NO.
        BigInteger[] bits = new BigInteger[graph.Count];
        Dictionary<(BigInteger Mask, int Last), int> parent = new();
        List<(BigInteger Mask, int Last)> frontier = new();
        for (int v = 0; v < graph.Count; v++) {
            if (timerHasExpired) return UnrestrictedLongestPathSearchGraph.Timeout;
            bits[v] = BigInteger.One << v;
            parent.Add((bits[v], v), -1);
            frontier.Add((bits[v], v));
        }

        int target = checked((int)problem.K + 1);
        for (int length = 1; length < target; length++) {
            List<(BigInteger Mask, int Last)> nextLayer = new();
            foreach (var state in frontier) {
                if (timerHasExpired) return UnrestrictedLongestPathSearchGraph.Timeout;
                foreach (int next in graph.Neighbors[state.Last]) {
                    if (timerHasExpired) return UnrestrictedLongestPathSearchGraph.Timeout;
                    if ((state.Mask & bits[next]) != BigInteger.Zero) continue;
                    var extended = (Mask: state.Mask | bits[next], Last: next);
                    if (!parent.TryAdd(extended, state.Last)) continue;
                    if (length + 1 == target) {
                        List<int> path = new(target);
                        var cursor = extended;
                        while (true) {
                            if (timerHasExpired) return UnrestrictedLongestPathSearchGraph.Timeout;
                            path.Add(cursor.Last);
                            int previous = parent[cursor];
                            if (previous == -1) break;
                            cursor = (cursor.Mask ^ bits[cursor.Last], previous);
                        }
                        path.Reverse();
                        return graph.Certificate(path);
                    }
                    nextLayer.Add(extended);
                }
            }
            if (nextLayer.Count == 0) {
                return timerHasExpired ? UnrestrictedLongestPathSearchGraph.Timeout : UnrestrictedLongestPathSearchGraph.NoSolution;
            }
            frontier = nextLayer;
        }
        return timerHasExpired ? UnrestrictedLongestPathSearchGraph.Timeout : UnrestrictedLongestPathSearchGraph.NoSolution;
    }
}
