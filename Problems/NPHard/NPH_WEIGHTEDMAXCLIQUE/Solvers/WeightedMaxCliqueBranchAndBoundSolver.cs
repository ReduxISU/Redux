using API.Interfaces;

namespace API.Problems.NPHard.NPH_WEIGHTEDMAXCLIQUE.Solvers;

internal sealed class WeightedMaxCliqueBranchAndBoundSolver : ISolver<WEIGHTEDMAXCLIQUE> {
    public string solverName { get; } = "Weighted Maximum Clique Exact Branch and Bound";
    public string solverDefinition { get; } = "Searches partial cliques by intersecting candidates with neighbors, pruning with the sum of remaining candidate weights. Guarantees optimality on completion; no coloring bound is used.";
    public string source { get; } = "Candidate-intersection search adapted from Carraghan and Pardalos (1990), An Exact Algorithm for the Maximum Clique Problem, with a nonnegative vertex-weight sum bound.";
    public string sourceLink { get; } = "https://doi.org/10.1016/0167-6377(90)90057-C";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Corbin Hay" };
    public SolverType solverType { get; } = SolverType.Backtracking;
    public MathematicalFunction timeComplexity { get; } = new("2^n * n^2", ", n = vertices; conservative worst-case bound including candidate work; exponential despite pruning");
    public MathematicalFunction spaceComplexity { get; } = new("n^2", ", n = vertices; adjacency index and recursive candidate lists");
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    private volatile bool _timerHasExpired;
    public bool timerHasExpired { get => _timerHasExpired; set => _timerHasExpired = value; }

    internal long SearchNodes { get; private set; }
    internal long PrunedBranches { get; private set; }

    public string solve(WEIGHTEDMAXCLIQUE problem) {
        SearchNodes = PrunedBranches = 0;
        if (timerHasExpired) return SolveRun.TimedOutAnswer;
        int[] priority = Enumerable.Range(0, problem.VertexCount).OrderByDescending(problem.VertexWeight)
            .ThenByDescending(problem.Degree).ThenBy(v => v).ToArray();
        var best = new List<int>();
        foreach (int vertex in priority) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer;
            if (problem.VertexWeight(vertex) > 0 && problem.CanAdd(vertex, best)) best.Add(vertex);
        }
        long bestWeight = problem.Value(best);
        var clique = new List<int>();

        void Search(List<int> candidates, long currentWeight) {
            if (timerHasExpired) return;
            if (SearchNodes < long.MaxValue) SearchNodes++;
            if (currentWeight > bestWeight) {
                bestWeight = currentWeight;
                best = new List<int>(clique);
            }
            long upperBound = currentWeight;
            foreach (int candidate in candidates) {
                if (timerHasExpired) return;
                upperBound = checked(upperBound + problem.VertexWeight(candidate));
            }
            for (int i = 0; i < candidates.Count; i++) {
                if (timerHasExpired) return;
                if (upperBound <= bestWeight) {
                    if (PrunedBranches < long.MaxValue) PrunedBranches++;
                    return;
                }
                int vertex = candidates[i];
                var next = new List<int>();
                // Only later candidates remain: every subset has exactly one search path.
                // Intersecting at every depth preserves adjacency to ALL clique members.
                for (int j = i + 1; j < candidates.Count; j++) {
                    if (timerHasExpired) return;
                    if (problem.AreAdjacent(vertex, candidates[j])) next.Add(candidates[j]);
                }
                clique.Add(vertex);
                Search(next, checked(currentWeight + problem.VertexWeight(vertex)));
                clique.RemoveAt(clique.Count - 1);
                upperBound = checked(upperBound - problem.VertexWeight(vertex));
            }
        }

        Search(priority.ToList(), 0);
        string certificate = problem.FormatCertificate(best, bestWeight);
        return timerHasExpired ? SolveRun.TimedOutAnswer : certificate;
    }
}
