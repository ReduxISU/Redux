using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using static API.Interfaces.Graphs.GraphSubsetFrames;

namespace API.Problems.NPHard.NPH_WEIGHTEDMAXCLIQUE.Solvers;

// The steps it records, one per nonempty subset in the order it tries them: a Reject naming two nodes that aren't joined,
// an Accept for a clique heavier than the best so far, a Reject for a clique that is not; then the Done with the best.
[DefaultFor(typeof(WEIGHTEDMAXCLIQUE))]
internal sealed class WeightedMaxCliqueBruteForceSolver : ISolver<WEIGHTEDMAXCLIQUE, NodeSet> {
    public string solverName { get; } = "Weighted Maximum Clique Exact Brute Force";
    public string solverDefinition { get; } = "Enumerates all vertex subsets and checks pairwise adjacency, returning one maximum-weight clique only when the search completes.";
    public string source { get; } = "Exhaustive subset enumeration for maximum-weight clique.";
    public string sourceLink { get; } = "https://en.wikipedia.org/wiki/Clique_problem";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Corbin Hay" };
    public SolverType solverType { get; } = SolverType.BruteForce;
    public MathematicalFunction timeComplexity { get; } = new("2^|N| * |N|^2", ", |N| = number of nodes; exhaustive subset checks");
    public MathematicalFunction spaceComplexity { get; } = new("n + m", ", n = vertices, m = edges; adjacency index and subset storage");
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    private volatile bool _timerHasExpired;
    public bool timerHasExpired { get => _timerHasExpired; set => _timerHasExpired = value; }

    internal const int MaxVertices = 20;

    internal static void ThrowIfSearchLimitExceeded(int count) {
        if (count > MaxVertices)
            throw new ExactSearchLimitExceededException("Weighted Maximum Clique", count, MaxVertices);
    }

    // How the steps show a subset (vertex indices): its nodes, every one of them in focus.
    private static RunSteps<List<int>, NodeSet> NewSteps(StepRecorder<NodeSet> rec, WEIGHTEDMAXCLIQUE problem) => new(rec,
        partial: subset => new NodeSet(subset.Select(problem.VertexName)),
        focus: subset => subset.Select(problem.VertexName).ToArray());

    public string solve(WEIGHTEDMAXCLIQUE problem) => Solve(problem, StepRecorder<NodeSet>.Off);

    public string Solve(WEIGHTEDMAXCLIQUE problem, StepRecorder<NodeSet> rec) {
        ThrowIfSearchLimitExceeded(problem.VertexCount);
        if (timerHasExpired) return SolveRun.TimedOutAnswer;
        var steps = NewSteps(rec, problem);
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
            if (!problem.IsClique(selected)) {
                steps.Reject(selected,
                    $"Try {Words.Braces(selected.Select(problem.VertexName))}: {Why(SubsetRule.Clique, problem.nodes, problem.edges, selected.Select(problem.VertexName))}.");
                continue;
            }
            long value = problem.Value(selected);
            if (value > bestWeight) {
                steps.Accept(selected, $"Try {Words.Braces(selected.Select(problem.VertexName))}: a clique of weight {value}, more than the best so far, {bestWeight}.");
                bestWeight = value;
                best = new List<int>(selected);
            } else if (selected.Count > 0) {
                steps.Reject(selected, $"Try {Words.Braces(selected.Select(problem.VertexName))}: a clique of weight {value}, not more than the best, {bestWeight}.");
            }
        }
        if (timerHasExpired) return SolveRun.TimedOutAnswer;
        steps.Done(new NodeSet(best.Select(problem.VertexName)), true,
            $"The heaviest clique is {Words.Braces(best.Order().Select(problem.VertexName))}, weight {bestWeight}.");
        return problem.FormatCertificate(best, bestWeight);
    }
}
