using API.Interfaces;

namespace API.Problems.NPHard.NPH_WEIGHTEDMAXCLIQUE.Solvers;

internal sealed class WeightedMaxCliqueGreedyLocalSearchSolver : ISolver<WEIGHTEDMAXCLIQUE> {
    internal const int MaxStarts = 32;
    internal const int MaxImprovementRounds = 32;
    public string solverName { get; } = "Weighted Maximum Clique Greedy Multi-Start Local Search Heuristic";
    public string solverDefinition { get; } = "Tries at most 32 greedy starts ordered by weight, degree, and identifier, then at most 32 rounds of improving additions or one-for-one replacements per start. Returns a feasible clique; guarantees neither optimality nor an approximation ratio.";
    public string source { get; } = "Bounded greedy multi-start construction and one-for-one local search for maximum-weight clique.";
    public string sourceLink { get; } = "https://en.wikipedia.org/wiki/Local_search_(optimization)";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Corbin Hay" };
    public SolverType solverType { get; } = SolverType.Greedy | SolverType.Heuristic;
    public MathematicalFunction timeComplexity { get; } = new("n^3", ", n = vertices; at most 32 starts and 32 improvement rounds per start");
    public MathematicalFunction spaceComplexity { get; } = new("n + m", ", n = vertices, m = edges; adjacency index and clique storage");
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
    private volatile bool _timerHasExpired;
    public bool timerHasExpired { get => _timerHasExpired; set => _timerHasExpired = value; }

    public string solve(WEIGHTEDMAXCLIQUE problem) {
        if (timerHasExpired) return SolveRun.TimedOutAnswer;
        int[] priority = Enumerable.Range(0, problem.VertexCount).OrderByDescending(problem.VertexWeight)
            .ThenByDescending(problem.Degree).ThenBy(v => v).ToArray();
        var best = new List<int>();
        long bestWeight = 0;
        // First start is unseeded greedy; remaining starts force different positive vertices.
        int starts = Math.Min(MaxStarts, problem.VertexCount + 1);
        for (int start = 0; start < starts; start++) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer;
            var clique = new List<int>();
            if (start > 0) {
                int seed = priority[start - 1];
                if (problem.VertexWeight(seed) == 0) continue;
                clique.Add(seed);
            }
            GreedyExtend(problem, priority, clique);
            Improve(problem, priority, clique);
            if (timerHasExpired) return SolveRun.TimedOutAnswer;
            long weight = problem.Value(clique);
            if (weight > bestWeight) {
                bestWeight = weight;
                best = clique;
            }
        }
        string certificate = problem.FormatCertificate(best, bestWeight);
        return timerHasExpired ? SolveRun.TimedOutAnswer : certificate;
    }

    private void GreedyExtend(WEIGHTEDMAXCLIQUE problem, int[] priority, List<int> clique) {
        foreach (int vertex in priority) {
            if (timerHasExpired) return;
            if (problem.VertexWeight(vertex) > 0 && problem.CanAdd(vertex, clique)) clique.Add(vertex);
        }
    }

    private void Improve(WEIGHTEDMAXCLIQUE problem, int[] priority, List<int> clique) {
        for (int round = 0; round < MaxImprovementRounds; round++) {
            if (timerHasExpired) return;
            bool improved = false;
            foreach (int vertex in priority) {
                if (timerHasExpired) return;
                if (clique.Contains(vertex) || problem.VertexWeight(vertex) == 0) continue;
                if (problem.CanAdd(vertex, clique)) {
                    clique.Add(vertex); // Positive weight: strict improvement.
                    improved = true;
                    break;
                }
                for (int removed = 0; removed < clique.Count; removed++) {
                    if (timerHasExpired) return;
                    if (problem.VertexWeight(vertex) <= problem.VertexWeight(clique[removed])) continue;
                    bool compatible = true;
                    for (int j = 0; j < clique.Count; j++) {
                        if (timerHasExpired) return;
                        if (j != removed && !problem.AreAdjacent(vertex, clique[j])) {
                            compatible = false;
                            break;
                        }
                    }
                    if (!compatible) continue;
                    clique[removed] = vertex;
                    improved = true;
                    break;
                }
                if (improved) break;
            }
            if (!improved) return;
            // A replacement can unlock several compatible positive additions.
            GreedyExtend(problem, priority, clique);
        }
    }
}
