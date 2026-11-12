using API.Interfaces;

namespace API.Problems.NPHard.NPH_WEIGHTEDMAXCLIQUE.Solvers;

internal sealed class WeightedMaxCliqueGreedyLocalSearchSolver : WeightedCliqueSolver {
    internal const int MaxStarts = 32;
    internal const int MaxImprovementRounds = 32;
    public override string solverName { get; } = "Weighted Maximum Clique Greedy Multi-Start Local Search Heuristic";
    public override string solverDefinition { get; } = "Tries at most 32 greedy starts ordered by weight, degree, and identifier, then at most 32 rounds of improving additions or one-for-one replacements per start. Returns a feasible clique; guarantees neither optimality nor an approximation ratio.";
    public override string source { get; } = "Bounded greedy multi-start construction and one-for-one local search for maximum-weight clique.";
    public override string sourceLink { get; } = "https://en.wikipedia.org/wiki/Local_search_(optimization)";
    public override string sourceFile { get; } = SourceFile.Path();
    public override SolverType solverType { get; } = SolverType.Greedy | SolverType.Heuristic;
    public override MathematicalFunction timeComplexity { get; } = new("n^3", ", n = vertices; at most 32 starts and 32 improvement rounds per start");
    public override SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;

    internal override WeightedCliqueResult Optimize(WEIGHTEDMAXCLIQUE problem, Action check) {
        var graph = new WeightedCliqueGraph(problem, check);
        var best = new List<int>();
        long bestWeight = 0;
        // First start is unseeded greedy; remaining starts force different positive vertices.
        int starts = Math.Min(MaxStarts, graph.Count + 1);
        for (int start = 0; start < starts; start++) {
            check();
            var clique = new List<int>();
            if (start > 0) {
                int seed = graph.Priority[start - 1];
                if (graph.Weights[seed] == 0) continue;
                clique.Add(seed);
            }
            graph.GreedyExtend(clique, check);
            Improve(graph, clique, check);
            long weight = graph.Weight(clique, check);
            if (weight > bestWeight) {
                bestWeight = weight;
                best = clique;
            }
        }
        check();
        return graph.Result(best, bestWeight);
    }

    internal static void Improve(WeightedCliqueGraph graph, List<int> clique, Action check) {
        for (int round = 0; round < MaxImprovementRounds; round++) {
            check();
            bool improved = false;
            foreach (int vertex in graph.Priority) {
                check();
                if (clique.Contains(vertex) || graph.Weights[vertex] == 0) continue;
                if (graph.CanAdd(vertex, clique, check)) {
                    clique.Add(vertex); // Positive weight: strict improvement.
                    improved = true;
                    break;
                }
                for (int removed = 0; removed < clique.Count; removed++) {
                    check();
                    if (graph.Weights[vertex] <= graph.Weights[clique[removed]]) continue;
                    bool compatible = true;
                    for (int j = 0; j < clique.Count; j++) {
                        check();
                        if (j != removed && !graph.Adjacent[vertex][clique[j]]) {
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
            graph.GreedyExtend(clique, check);
        }
    }
}
