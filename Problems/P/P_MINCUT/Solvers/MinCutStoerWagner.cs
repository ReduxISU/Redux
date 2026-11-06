using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;

namespace API.Problems.P.P_MINCUT.Solvers;

// The steps the Stoer-Wagner Algorithm records: one per phase. Each phase ends by cutting off the node added last (with
// everything already merged into it); that cut is an Accept if it is the lightest so far, else a Reject. Then the Done,
// with the minimum cut.
class MinCutStoerWagner : ISolver<MINCUT, Groups> {
    public string solverName { get; } = "Stoer-Wagner Algorithm";
    public string solverDefinition { get; } = "Finds the global minimum cut of a weighted undirected graph using the Stoer-Wagner algorithm in O(V³) time.";
    public string source { get; } = "Stoer, Mechthild, and Frank Wagner. \"A simple min-cut algorithm.\" Journal of the ACM 44, no. 4 (1997): 585-591.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Each phase greedily grows a "maximum adjacency" ordering by
    // repeatedly picking the most tightly-connected remaining vertex.
    public SolverType solverType { get; } = SolverType.Greedy;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
    // n-1 phases; each phase's maximum-adjacency-ordering search does an O(n) array scan to
    // pick the next node, n times (O(n^2) per phase), matching the solverDefinition's stated
    // O(V^3) bound (n = V here, on the dense adjacency matrix).
    public MathematicalFunction timeComplexity { get; } = new("V^3", "");

    public MinCutStoerWagner() { }

    // How the steps show a phase's cut, given as the original nodes merged into the node added last: the split that cuts
    // them off (that side is group 0, the rest group 1; the edges that cross are the cut), with that side in focus.
    private static RunSteps<HashSet<int>, Groups> NewSteps(IReadOnlyList<string> names, StepRecorder<Groups> rec) => new(rec,
        // What every picture shows: the split, and the side cut off.
        partial: cutOff => GraphEdgeFrames.Split(names, Names(names, cutOff)),
        focus: cutOff => Names(names, cutOff));

    // The names of a set of original nodes, in node order.
    private static string[] Names(IReadOnlyList<string> names, HashSet<int> nodes) => nodes.OrderBy(i => i).Select(i => names[i]).ToArray();

    public string solve(MINCUT problem) => Solve(problem, StepRecorder<Groups>.Off);

    public string Solve(MINCUT problem, StepRecorder<Groups> rec) {
        var steps = NewSteps(problem.nodes, rec);
        int n = problem.nodes.Count;
        if (n < 2) {
            steps.Done(new Groups([]), true, $"A graph with fewer than two nodes has nothing to cut.");
            return "{}";
        }

        Dictionary<string, int> nodeIndex = BuildNodeIndex(problem.nodes);
        int[,] w = BuildAdjacencyMatrix(n, nodeIndex, problem.edges);

        if (timerHasExpired) return SolveRun.TimedOutAnswer;
        var (minWeight, minSide) = RunStoerWagner(n, w, problem.nodes, steps, () => timerHasExpired);
        if (timerHasExpired) return SolveRun.TimedOutAnswer;

        List<string> cutEdgeParts = new();
        HashSet<string> seen = new();
        int listedWeight = 0;
        foreach (var edge in problem.edges) {
            int u = nodeIndex[edge.source];
            int v = nodeIndex[edge.destination];
            if (minSide.Contains(u) == minSide.Contains(v)) continue;

            string key = $"{Math.Min(u, v)}-{Math.Max(u, v)}";
            if (!seen.Add(key)) continue;
            cutEdgeParts.Add("({" + edge.source + "," + edge.destination + "}," + edge.weight + ")");
            listedWeight += edge.weight;
        }

        // The answer lists one edge per pair of nodes. Parallel edges are already merged into one with their summed weight,
        // so it weighs the cut it names; if it ever did not, the verifier would refuse it, and so must the Done step.
        bool listsTheCut = listedWeight == minWeight;
        Groups split = GraphEdgeFrames.Split(problem.nodes, minSide.Select(i => problem.nodes[i]));
        if (!listsTheCut)
            steps.Done(split, false, $"The minimum cut weighs {minWeight}, but the answer lists one edge per pair of nodes, weighing {listedWeight}, so it does not show that cut.");
        else if (minWeight == 0 && cutEdgeParts.Count == 0)
            steps.Done(split, true, $"The graph is already in more than one piece, so the minimum cut weighs 0.");
        else
            steps.Done(split, true, $"The minimum cut weighs {minWeight}: {GraphEdgeFrames.Side(minSide.Select(i => problem.nodes[i]))} against {GraphEdgeFrames.Side(problem.nodes.Where((_, i) => !minSide.Contains(i)))}.");
        return "{" + string.Join(",", cutEdgeParts) + "}";
    }

    // Runs Stoer-Wagner and returns (minCutWeight, one side of the min cut as original node indices). Each phase grows a
    // set A from one node, always adding the node most tightly joined to A; the node added last, with everything already
    // merged into it, cut off from the rest is the phase's cut. Then the last two nodes added are merged into one. The
    // lightest phase cut is a minimum cut. It records one step per phase (names are the nodes' names, for the why). With
    // stop, it stops between phases once stop() is true (the time limit); the caller reports it.
    internal static (int weight, HashSet<int> side) RunStoerWagner(int n, int[,] wInput, IReadOnlyList<string> names,
        RunSteps<HashSet<int>, Groups> steps, Func<bool>? stop = null) {
        int[,] w = (int[,])wInput.Clone();
        List<HashSet<int>> merged = Enumerable.Range(0, n).Select(i => new HashSet<int> { i }).ToList();
        bool[] active = Enumerable.Repeat(true, n).ToArray();

        int bestWeight = int.MaxValue;
        HashSet<int> bestSide = new();

        for (int phase = 0; phase < n - 1; phase++) {
            if (stop != null && stop()) break; // stop at the time limit; the caller reports it
            int[] key = new int[n];
            bool[] inA = new bool[n];
            int lastAdded = -1, secondLastAdded = -1;
            int activeCount = active.Count(x => x);

            for (int step = 0; step < activeCount; step++) {
                int z = -1;
                for (int i = 0; i < n; i++) {
                    if (active[i] && !inA[i] && (z == -1 || key[i] > key[z]))
                        z = i;
                }
                if (z == -1) break;

                secondLastAdded = lastAdded;
                lastAdded = z;
                inA[z] = true;

                for (int i = 0; i < n; i++)
                    if (active[i] && !inA[i])
                        key[i] += w[z, i];
            }

            if (lastAdded == -1 || secondLastAdded == -1) break;

            // The cut-of-the-phase separates lastAdded from the rest.
            // key[lastAdded] = sum of edges from lastAdded to all other active nodes.
            int phaseWeight = key[lastAdded];
            // The phase's cut: an Accept if it is the lightest so far (it becomes the best), else a Reject.
            if (phaseWeight < bestWeight) {
                steps.Accept(merged[lastAdded], $"Node {names[lastAdded]} is added last, so {GraphEdgeFrames.Side(Names(names, merged[lastAdded]))} is cut off from the rest at weight {phaseWeight}. It is the lightest cut so far.");
                bestWeight = phaseWeight;
                bestSide = new HashSet<int>(merged[lastAdded]);
            } else {
                steps.Reject(merged[lastAdded], $"Node {names[lastAdded]} is added last, so {GraphEdgeFrames.Side(Names(names, merged[lastAdded]))} is cut off from the rest at weight {phaseWeight}. It is no lighter than the best so far ({bestWeight}).");
            }

            // Merge lastAdded into secondLastAdded.
            for (int i = 0; i < n; i++) {
                w[secondLastAdded, i] += w[lastAdded, i];
                w[i, secondLastAdded] += w[i, lastAdded];
            }
            merged[secondLastAdded].UnionWith(merged[lastAdded]);
            active[lastAdded] = false;
        }

        return (bestWeight == int.MaxValue ? 0 : bestWeight, bestSide);
    }

    internal static int GetMinCutWeight(MINCUT problem) {
        int n = problem.nodes.Count;
        if (n < 2) return 0;
        Dictionary<string, int> nodeIndex = BuildNodeIndex(problem.nodes);
        int[,] w = BuildAdjacencyMatrix(n, nodeIndex, problem.edges);
        var (weight, _) = RunStoerWagner(n, w, problem.nodes, NewSteps(problem.nodes, StepRecorder<Groups>.Off));
        return weight;
    }

    internal static Dictionary<string, int> BuildNodeIndex(List<string> nodes) {
        Dictionary<string, int> index = new();
        for (int i = 0; i < nodes.Count; i++) index[nodes[i]] = i;
        return index;
    }

    internal static int[,] BuildAdjacencyMatrix(int n, Dictionary<string, int> nodeIndex,
        List<(string source, string destination, int weight)> edges) {
        int[,] w = new int[n, n];
        foreach (var edge in edges) {
            int u = nodeIndex[edge.source];
            int v = nodeIndex[edge.destination];
            w[u, v] += edge.weight;
            w[v, u] += edge.weight;
        }
        return w;
    }
}
