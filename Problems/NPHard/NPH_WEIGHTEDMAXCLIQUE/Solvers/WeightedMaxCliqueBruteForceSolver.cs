using API.Interfaces;

namespace API.Problems.NPHard.NPH_WEIGHTEDMAXCLIQUE.Solvers;

[DefaultFor(typeof(WEIGHTEDMAXCLIQUE))]
internal sealed class WeightedMaxCliqueBruteForceSolver : ISolver<WEIGHTEDMAXCLIQUE> {
    public string solverName { get; } = "Weighted Maximum Clique Exact Brute Force";
    public string solverDefinition { get; } = "Enumerates all vertex subsets and checks pairwise adjacency, returning one maximum-weight clique only when the search completes.";
    public string source { get; } = "Exhaustive subset enumeration for maximum-weight clique.";
    public string sourceLink { get; } = "https://en.wikipedia.org/wiki/Clique_problem";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Corbin Hay" };
    public SolverType solverType { get; } = SolverType.BruteForce;
    public MathematicalFunction timeComplexity { get; } = new("2^n * n^2", ", n = vertices; exhaustive subset checks");
    public MathematicalFunction spaceComplexity { get; } = new("n + m", ", n = vertices, m = edges; adjacency index and subset storage");
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    private volatile bool _timerHasExpired;
    public bool timerHasExpired { get => _timerHasExpired; set => _timerHasExpired = value; }

    internal const int MaxVertices = 20;

    internal static void ThrowIfSearchLimitExceeded(int count) {
        if (count > MaxVertices)
            throw new ExactSearchLimitExceededException("Weighted Maximum Clique", count, MaxVertices);
    }

    public string solve(WEIGHTEDMAXCLIQUE problem) {
        ThrowIfSearchLimitExceeded(problem.VertexCount);
        if (timerHasExpired) return SolveRun.TimedOutAnswer;
        var best = new List<int>();
        long bestWeight = 0;
        var selected = new List<int>(problem.VertexCount);
        // The explicit limit makes the bit shift safe. Mask 0 includes the empty clique.
        for (int mask = 0; mask < (1 << problem.VertexCount); mask++) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer;
            selected.Clear();
            for (int vertex = 0; vertex < problem.VertexCount; vertex++) {
                if ((mask & (1 << vertex)) != 0) selected.Add(vertex);
            }
            if (!problem.IsClique(selected)) continue;
            long value = problem.Value(selected);
            if (value > bestWeight) {
                bestWeight = value;
                best = new List<int>(selected);
            }
        }
        string certificate = problem.FormatCertificate(best, bestWeight);
        return timerHasExpired ? SolveRun.TimedOutAnswer : certificate;
    }
}
