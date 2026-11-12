using API.Interfaces;

namespace API.Problems.NPHard.NPH_WEIGHTEDMAXCLIQUE.Solvers;

internal sealed class WeightedMaxCliqueBranchAndBoundSolver : WeightedCliqueSolver {
    public override string solverName { get; } = "Weighted Maximum Clique Exact Branch and Bound";
    public override string solverDefinition { get; } = "Searches partial cliques by intersecting candidates with neighbors, pruning with the sum of remaining candidate weights. Guarantees optimality on completion; no coloring bound is used.";
    public override string source { get; } = "Candidate-intersection search adapted from Carraghan and Pardalos (1990), An Exact Algorithm for the Maximum Clique Problem, with a nonnegative vertex-weight sum bound.";
    public override string sourceLink { get; } = "https://doi.org/10.1016/0167-6377(90)90057-C";
    public override string sourceFile { get; } = SourceFile.Path();
    public override SolverType solverType { get; } = SolverType.Backtracking;
    public override MathematicalFunction timeComplexity { get; } = new("2^n * n^2", ", n = vertices; conservative worst-case bound including candidate work; exponential despite pruning");
    public override SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;

    internal long SearchNodes { get; private set; }
    internal long PrunedBranches { get; private set; }

    internal override WeightedCliqueResult Optimize(WEIGHTEDMAXCLIQUE problem, Action check) {
        SearchNodes = PrunedBranches = 0;
        var graph = new WeightedCliqueGraph(problem, check);
        var best = new List<int>();
        graph.GreedyExtend(best, check);
        long bestWeight = graph.Weight(best, check);
        var clique = new List<int>();

        void Search(List<int> candidates, long currentWeight) {
            check();
            if (SearchNodes < long.MaxValue) SearchNodes++;
            if (currentWeight > bestWeight) {
                bestWeight = currentWeight;
                best = new List<int>(clique);
            }
            long upperBound = currentWeight;
            foreach (int candidate in candidates) {
                check();
                upperBound = checked(upperBound + graph.Weights[candidate]);
            }
            for (int i = 0; i < candidates.Count; i++) {
                check();
                if (upperBound <= bestWeight) {
                    if (PrunedBranches < long.MaxValue) PrunedBranches++;
                    return;
                }
                int vertex = candidates[i];
                var next = new List<int>();
                // Only later candidates remain: every subset has exactly one search path.
                // Intersecting at every depth preserves adjacency to ALL clique members.
                for (int j = i + 1; j < candidates.Count; j++) {
                    check();
                    if (graph.Adjacent[vertex][candidates[j]]) next.Add(candidates[j]);
                }
                clique.Add(vertex);
                Search(next, checked(currentWeight + graph.Weights[vertex]));
                clique.RemoveAt(clique.Count - 1);
                upperBound = checked(upperBound - graph.Weights[vertex]);
            }
        }

        Search(graph.Priority.ToList(), 0);
        check();
        return graph.Result(best, bestWeight);
    }
}
