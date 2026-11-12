using API.Interfaces;

namespace API.Problems.NPHard.NPH_WEIGHTEDMAXCLIQUE.Solvers;

internal sealed class WeightedMaxCliqueBruteForceSolver : WeightedCliqueSolver {
    public override string solverName { get; } = "Weighted Maximum Clique Exact Brute Force";
    public override string solverDefinition { get; } = "Enumerates all vertex subsets and checks pairwise adjacency, returning one maximum-weight clique only when the search completes.";
    public override string source { get; } = "Exhaustive subset enumeration for maximum-weight clique.";
    public override string sourceLink { get; } = "https://en.wikipedia.org/wiki/Clique_problem";
    public override string sourceFile { get; } = SourceFile.Path();
    public override SolverType solverType { get; } = SolverType.BruteForce;
    public override MathematicalFunction timeComplexity { get; } = new("2^n * n^2", ", n = vertices; exhaustive subset checks");
    public override SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;

    internal override WeightedCliqueResult Optimize(WEIGHTEDMAXCLIQUE problem, Action check) {
        WeightedCliqueExhaustiveSearchLimit.ThrowIfExceeded(problem.nodes.Count);
        var graph = new WeightedCliqueGraph(problem, check);
        var best = new List<int>();
        long bestWeight = 0;
        var selected = new List<int>(graph.Count);
        // The explicit limit makes the bit shift safe. Mask 0 includes the empty clique.
        for (int mask = 0; mask < (1 << graph.Count); mask++) {
            check();
            selected.Clear();
            for (int vertex = 0; vertex < graph.Count; vertex++) {
                if ((mask & (1 << vertex)) != 0) selected.Add(vertex);
            }
            if (!graph.IsClique(selected, check)) continue;
            long value = graph.Weight(selected, check);
            if (value > bestWeight) {
                bestWeight = value;
                best = new List<int>(selected);
            }
        }
        check();
        return graph.Result(best, bestWeight);
    }
}
