using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_MAXCUT.Solvers;

// The steps Max Cut Brute Force records: an Accept each time a split beats every split before it, the best so far (the
// 2^n splits themselves are not shown one by one), then the Done with the heaviest cut.
class MaxCutSolver : ISolver<MAXCUT, Groups> {
    public string solverName { get; } = "Max Cut Brute Force";
    public string solverDefinition { get; } = "Enumerates all 2^n partitions of the vertex set and returns the S side of the partition whose crossing edges have maximum total weight. Certificate format: {node1,node2,...} representing the S side.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Max Grünwoldt", "Eric Hill" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Unpruned exhaustive enumeration.
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // Declared, not derived. Iterates every mask from 1 to (1<<n)-2, i.e. all 2^n vertex
    // subsets; each mask costs O(n) to build S plus O(m) to sum crossing-edge weights
    // (CutWeight, HashSet lookups are O(1)).
    public MathematicalFunction timeComplexity { get; } = new("2^n * (n + m)", ", n = |nodes|, m = |edges|");

    public MaxCutSolver() { }

    // How the steps show a split, given by its S side: S as group 0 and the rest as group 1 (the edges that cross are the cut).
    private static RunSteps<HashSet<string>, Groups> NewSteps(List<string> nodes, StepRecorder<Groups> rec) => new(rec,
        // What every picture shows: the split.
        partial: side => GraphEdgeFrames.Split(nodes, side));

    public string solve(MAXCUT problem) => Solve(problem, StepRecorder<Groups>.Off);

    // Tries every S side (every mask but none and all) and keeps the split whose crossing edges weigh the most.
    public string Solve(MAXCUT problem, StepRecorder<Groups> rec) {
        List<string> nodes = problem.nodes;
        var steps = NewSteps(nodes, rec);
        int n = nodes.Count;
        if (n < 2) {
            steps.Done(new Groups([]), false, $"A graph needs at least two nodes to have a cut.");
            return "{}";
        }

        // Any split beats this, even one whose crossing edges weigh less than zero.
        int bestWeight = int.MinValue;
        HashSet<string> bestS = new();

        int limit = 1 << n;
        for (int mask = 1; mask < limit - 1; mask++) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer;

            HashSet<string> S = new();
            for (int i = 0; i < n; i++)
                if ((mask >> i & 1) == 1) S.Add(nodes[i]);

            int w = CutWeight(problem.edges, S);
            if (w > bestWeight) {
                bestWeight = w;
                bestS = S;
                steps.Accept(S, $"Put {GraphEdgeFrames.Side(S)} against {GraphEdgeFrames.Side(nodes.Where(x => !S.Contains(x)))}: the crossing edges weigh {w}, the most so far.");
            }
        }

        if (bestS.Count == 0) {
            steps.Done(new Groups([]), false, $"No split was found.");
            return "{}";
        }
        steps.Done(GraphEdgeFrames.Split(nodes, bestS), true,
            $"The heaviest cut weighs {bestWeight}: {GraphEdgeFrames.Side(bestS)} against {GraphEdgeFrames.Side(nodes.Where(x => !bestS.Contains(x)))}, after trying all {limit - 2} choices of the S side.");
        return "{" + string.Join(",", bestS) + "}";
    }

    internal static int GetMaxCutWeight(MAXCUT problem) {
        List<string> nodes = problem.nodes;
        int n = nodes.Count;
        if (n < 2) return 0;

        int bestWeight = int.MinValue; // as in solve(), so negative weights give the same maximum
        int limit = 1 << n;
        for (int mask = 1; mask < limit - 1; mask++) {
            HashSet<string> S = new();
            for (int i = 0; i < n; i++)
                if ((mask >> i & 1) == 1) S.Add(nodes[i]);

            int w = CutWeight(problem.edges, S);
            if (w > bestWeight)
                bestWeight = w;
        }
        return bestWeight;
    }

    internal static int CutWeight(List<(string source, string destination, int weight)> edges, HashSet<string> S) {
        int total = 0;
        foreach (var edge in edges) {
            bool srcInS = S.Contains(edge.source);
            bool dstInS = S.Contains(edge.destination);
            if (srcInS != dstInS)
                total += edge.weight;
        }
        return total;
    }
}
