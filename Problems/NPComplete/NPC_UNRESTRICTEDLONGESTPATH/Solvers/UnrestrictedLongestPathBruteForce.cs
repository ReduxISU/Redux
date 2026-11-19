using API.Interfaces;

namespace API.Problems.NPComplete.NPC_UNRESTRICTEDLONGESTPATH.Solvers;

class UnrestrictedLongestPathBruteForce : ISolver<UNRESTRICTEDLONGESTPATH> {
    public string solverName { get; } = "Unrestricted Longest Path Brute Force";
    public string solverDefinition { get; } =
        "Enumerates every ordered sequence of K+1 distinct vertices without checking partial adjacency. " +
        "Only complete candidates are tested. Returns a path, {} for an exhaustive NO, or Redux's stopped message.";
    public string source { get; } = "Elementary exhaustive enumeration of ordered vertex sequences.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = Array.Empty<string>();
    private volatile bool expired;
    public bool timerHasExpired { get => expired; set => expired = value; }
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Factorial;
    public MathematicalFunction timeComplexity { get; } = new("n^2 * n! + m", ", n = |V|, m = |E|; conservative worst-case bound, excluding parsing");
    public MathematicalFunction spaceComplexity { get; } = new("n + m", ", n = |V|, m = |E|; excluding parsing");

    public UnrestrictedLongestPathBruteForce() { }

    public string solve(UNRESTRICTEDLONGESTPATH problem) {
        if (timerHasExpired) return UnrestrictedLongestPathSearchGraph.Timeout;
        if (UnrestrictedLongestPathSearchGraph.TryTrivial(problem, out string answer)) return answer;
        var graph = UnrestrictedLongestPathSearchGraph.Create(problem, () => timerHasExpired);
        if (graph is null) return UnrestrictedLongestPathSearchGraph.Timeout;

        // A longer witness has a prefix of exactly K edges, so K+1 vertices suffice.
        int target = checked((int)problem.K + 1);
        int[] candidate = new int[target];
        int[] next = new int[target];
        bool[] used = new bool[graph.Count];
        int depth = 0;
        while (depth >= 0) {
            if (timerHasExpired) return UnrestrictedLongestPathSearchGraph.Timeout;
            if (next[depth] == graph.Count) {
                next[depth] = 0;
                depth--;
                if (depth >= 0) used[candidate[depth]] = false;
                continue;
            }
            int vertex = next[depth]++;
            if (used[vertex]) continue;
            candidate[depth] = vertex;
            used[vertex] = true;
            if (depth + 1 < target) {
                next[++depth] = 0;
                continue;
            }

            // Deliberately do not prune prefixes based on missing edges.
            bool isPath = true;
            for (int i = 1; i < target; i++) {
                if (timerHasExpired) return UnrestrictedLongestPathSearchGraph.Timeout;
                if (!graph.HasEdge(candidate[i - 1], candidate[i])) {
                    isPath = false;
                    break;
                }
            }
            if (isPath) return graph.Certificate(candidate);
            used[vertex] = false;
        }
        return timerHasExpired ? UnrestrictedLongestPathSearchGraph.Timeout : UnrestrictedLongestPathSearchGraph.NoSolution;
    }
}
