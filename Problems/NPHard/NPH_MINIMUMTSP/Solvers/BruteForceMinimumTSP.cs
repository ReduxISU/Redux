using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;

namespace API.Problems.NPHard.NPH_MINIMUMTSP.Solvers;

class BruteForceMinimumTSP : ISolver<MINIMUMTSP, Tour> {

    // --- Fields ---
    public string solverName { get; } = "Minimum Traveling Salesperson Brute Force";
    public string solverDefinition { get; } = "This solver keeps the first city fixed, tries every ordering of the other cities, and keeps the cheapest tour it finds (the first one found, if several tie). Each tour is tried twice, once in each direction. It returns {} when the graph has no tour.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public bool timerHasExpired { get; set; }
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Factorial;
    // Declared, not derived. (n-1)! orderings of the cities after the first, each summed in O(n) from a weight table
    // built once in O(n^2 + m).
    public MathematicalFunction timeComplexity { get; } = new("(n-1)! * n + m", ", n = |nodes|, m = |edges|");

    // --- Methods Including Constructors ---
    public BruteForceMinimumTSP() {

    }

    /// <summary>
    /// A cheapest tour, as <c>{a,c,b,e,d,a}</c> (the closing repeat of the first city included, as Traveling
    /// Salesperson's solvers write it), or <c>{}</c> when there is none: fewer than three cities, or no ordering whose
    /// every hop is an edge.
    /// </summary>
    public string solve(MINIMUMTSP problem) => Solve(problem, StepRecorder<Tour>.Off);

    // Steps: one per ordering, in the order the solver tries them, each a whole closed tour (the solver judges whole
    // candidates, so the picture shows each one as tried, never as built up). An ordering that is a tour and is cheaper
    // than every tour before it is an Accept: the solver takes it as its best so far, and the last Accept is the answer.
    // That is also why it is an Accept: past the recorder's cap the most recent Accept is still kept, just before Done,
    // so a long run always shows the step that found the cheapest tour. An ordering with a hop that is not an edge, or
    // one that costs no less than the best so far, is a Reject that says why: its focus is the missing hop's two cities,
    // or the whole tour. An Accept has no focus, so the picture draws the new best tour as taken (its hops Solution)
    // rather than as looked at. Recorder state lives only in this call.
    public string Solve(MINIMUMTSP problem, StepRecorder<Tour> rec) {
        List<string> nodes = problem.nodes;
        int n = nodes.Count;
        if (n < 3) {
            rec.Done(new Tour([]), false, n == 0 ? "There are no cities to tour."
                : $"A tour needs at least three cities, and there {(n == 1 ? "is one" : "are two")}.");
            return "{}";
        }

        int?[,] weight = WeightTable(problem);
        // order[0] stays 0; order[1..] runs through every permutation of 1..n-1 in lexicographic order.
        int[] order = Enumerable.Range(0, n).ToArray();
        int[]? best = null;
        long bestCost = long.MaxValue;
        int tried = 0;
        // The closed tour (first city repeated at the end) of the current ordering, for captions and pictures.
        string[] Closed() => order.Append(order[0]).Select(i => nodes[i]).ToArray();
        do {
            tried++;
            long? cost = Cost(weight, order);
            if (cost is not long c) {
                rec.Reject(() => new Tour(Closed()),
                    () => {
                        var (from, to) = FirstMissingHop(weight, order, nodes);
                        return $"Try {GraphTourFrames.Arrows(Closed())}: there is no edge between {from} and {to}.";
                    },
                    () => {
                        var (from, to) = FirstMissingHop(weight, order, nodes);
                        return new[] { from, to };
                    });
            } else if (c < bestCost) {
                // Strictly cheaper, so the first of several equally cheap tours is kept.
                long previous = bestCost;
                bestCost = c;
                best = (int[])order.Clone();
                rec.Accept(() => new Tour(Closed()),
                    () => previous == long.MaxValue
                        ? $"Try {GraphTourFrames.Arrows(Closed())}: cost {c}, the first tour found, so the best so far."
                        : $"Try {GraphTourFrames.Arrows(Closed())}: cost {c}, a new best tour (it beats {previous}).");
            } else {
                rec.Reject(() => new Tour(Closed()),
                    () => $"Try {GraphTourFrames.Arrows(Closed())}: cost {c}, not better than the best so far ({bestCost}).",
                    () => EdgeIds(Closed()));
            }
        } while (NextPermutation(order, 1));

        string orderings = $"{tried} {(tried == 1 ? "ordering" : "orderings")} of the cities after {nodes[0]}";
        if (best == null) {
            rec.Done(new Tour([]), false, $"Tried all {orderings}. None is a tour.");
            return "{}";
        }
        string answer = ToCertificate(best, nodes);
        rec.Done(GraphTourFrames.ParseTour(answer), true,
            $"Tried all {orderings}. The cheapest tour is {GraphTourFrames.Arrows(GraphTourFrames.ParseTour(answer).Order)}, cost {bestCost}.");
        return answer;
    }

    // The first hop of the closed tour through order that is not an edge, by name.
    private static (string From, string To) FirstMissingHop(int?[,] weight, int[] order, List<string> nodes) {
        for (int i = 0; i < order.Length; i++) {
            int a = order[i], b = order[(i + 1) % order.Length];
            if (weight[a, b] == null) return (nodes[a], nodes[b]);
        }
        throw new InvalidOperationException("every hop is an edge");
    }

    // The ids of the edges a closed tour walks, all of which exist.
    private static string[] EdgeIds(string[] closed) =>
        Enumerable.Range(0, closed.Length - 1).Select(i => GraphTourFrames.EdgeId(false, closed[i], closed[i + 1])).ToArray();

    /// <summary>weight[i, j]: the weight of the edge between nodes i and j, or null if there is none.</summary>
    internal static int?[,] WeightTable(MINIMUMTSP problem) {
        int n = problem.nodes.Count;
        var table = new int?[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                table[i, j] = problem.Weight(problem.nodes[i], problem.nodes[j]);
        return table;
    }

    /// <summary>The cost of the closed tour through <paramref name="order"/>, or null if a hop is not an edge.</summary>
    internal static long? Cost(int?[,] weight, int[] order) {
        long total = 0;
        for (int i = 0; i < order.Length; i++) {
            int? w = weight[order[i], order[(i + 1) % order.Length]];
            if (w == null) return null;
            total += w.Value;
        }
        return total;
    }

    /// <summary>
    /// Rearranges <paramref name="order"/>[<paramref name="from"/>..] into the next permutation in lexicographic order;
    /// false (leaving it unchanged) after the last one.
    /// </summary>
    internal static bool NextPermutation(int[] order, int from) {
        int i = order.Length - 2;
        while (i >= from && order[i] >= order[i + 1]) i--;
        if (i < from) return false;
        int j = order.Length - 1;
        while (order[j] <= order[i]) j--;
        (order[i], order[j]) = (order[j], order[i]);
        Array.Reverse(order, i + 1, order.Length - i - 1);
        return true;
    }

    internal static string ToCertificate(int[] order, List<string> nodes) =>
        "{" + string.Join(",", order.Select(i => nodes[i])) + "," + nodes[order[0]] + "}";
}
