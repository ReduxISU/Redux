using API.Interfaces;
using API.Interfaces.Steps;

namespace API.Problems.NPHard.NPH_WEIGHTEDMAXCLIQUE.Solvers;

// The steps it records as it grows a clique one vertex at a time:
//   - an Accept for the greedy clique it starts from, the first best;
//   - an Accept for each vertex added, with the clique's weight and whether it is the heaviest so far;
//   - a Reject when the clique and every candidate left could weigh no more than the best (the candidates are the focus);
//   - a Backtrack when it backs a vertex out again;
//   - the Done, with the heaviest clique.
internal sealed class WeightedMaxCliqueBranchAndBoundSolver : ISolver<WEIGHTEDMAXCLIQUE, NodeSet>, IIncrementalSolver {
    public string solverName { get; } = "Weighted Maximum Clique Exact Branch and Bound";
    public string solverDefinition { get; } = "Searches partial cliques by intersecting candidates with neighbors, pruning with the sum of remaining candidate weights. Guarantees optimality on completion; no coloring bound is used.";
    public string source { get; } = "Candidate-intersection search adapted from Carraghan and Pardalos (1990), An Exact Algorithm for the Maximum Clique Problem, with a nonnegative vertex-weight sum bound.";
    public string sourceLink { get; } = "https://doi.org/10.1016/0167-6377(90)90057-C";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Corbin Hay" };
    public SolverType solverType { get; } = SolverType.Backtracking;
    public MathematicalFunction timeComplexity { get; } = new("2^|N| * |N|^2", ", |N| = number of nodes; conservative worst-case bound including candidate work; exponential despite pruning");
    public MathematicalFunction spaceComplexity { get; } = new("n^2", ", n = vertices; adjacency index and recursive candidate lists");
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    private volatile bool _timerHasExpired;
    public bool timerHasExpired { get => _timerHasExpired; set => _timerHasExpired = value; }

    internal long SearchNodes { get; private set; }
    internal long PrunedBranches { get; private set; }

    // A step of the search: the clique so far and the vertices the step is about (vertex indices).
    private readonly record struct Move(List<int> Clique, IEnumerable<int> About);

    // How the steps show a move: the clique so far, with the vertices the step is about in focus.
    private static RunSteps<Move, NodeSet> NewSteps(StepRecorder<NodeSet> rec, WEIGHTEDMAXCLIQUE problem) => new(rec,
        partial: m => new NodeSet(m.Clique.Select(problem.VertexName)),
        focus: m => m.About.Select(problem.VertexName).ToArray());

    public string solve(WEIGHTEDMAXCLIQUE problem) => Solve(problem, StepRecorder<NodeSet>.Off);

    public string Solve(WEIGHTEDMAXCLIQUE problem, StepRecorder<NodeSet> rec) {
        SearchNodes = PrunedBranches = 0;
        if (timerHasExpired) return SolveRun.TimedOutAnswer;
        var steps = NewSteps(rec, problem);
        string Named(IEnumerable<int> vertices) => Words.Braces(vertices.Order().Select(problem.VertexName));
        int[] priority = Enumerable.Range(0, problem.VertexCount).OrderByDescending(problem.VertexWeight)
            .ThenByDescending(problem.Degree).ThenBy(v => v).ToArray();
        var best = new List<int>();
        foreach (int vertex in priority) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer;
            if (problem.VertexWeight(vertex) > 0 && problem.CanAdd(vertex, best)) best.Add(vertex);
        }
        long bestWeight = problem.Value(best);
        if (best.Count > 0)
            steps.Accept(new(best, best), $"Start from {Named(best)}, weight {bestWeight}: the heaviest vertices first, each joined to all before it. It is the best so far.");
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
                    if (clique.Count == 0)
                        steps.Reject(new(clique, candidates.Skip(i)),
                            $"Stop: every vertex left to start from weighs at most {upperBound} together, not more than the best, {bestWeight}.");
                    else
                        steps.Reject(new(clique, candidates.Skip(i)),
                            $"Stop: {Named(clique)} and every vertex left to add weigh at most {upperBound}, not more than the best, {bestWeight}.");
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
                long weight = checked(currentWeight + problem.VertexWeight(vertex));
                if (weight > bestWeight)
                    steps.Accept(new(clique, [vertex]), $"Add {problem.VertexName(vertex)}: {Named(clique)} weighs {weight}, the heaviest so far.");
                else
                    steps.Accept(new(clique, [vertex]), $"Add {problem.VertexName(vertex)}: {Named(clique)} weighs {weight}; the best is {bestWeight}.");
                Search(next, weight);
                clique.RemoveAt(clique.Count - 1);
                steps.Backtrack(new(clique, [vertex]), $"Back out {problem.VertexName(vertex)}.");
                upperBound = checked(upperBound - problem.VertexWeight(vertex));
            }
        }

        Search(priority.ToList(), 0);
        if (timerHasExpired) return SolveRun.TimedOutAnswer;
        steps.Done(new NodeSet(best.Select(problem.VertexName)), true, $"The heaviest clique is {Named(best)}, weight {bestWeight}.");
        return problem.FormatCertificate(best, bestWeight);
    }
}
