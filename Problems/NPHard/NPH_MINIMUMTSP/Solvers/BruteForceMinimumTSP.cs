using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;

namespace API.Problems.NPHard.NPH_MINIMUMTSP.Solvers;

// The steps Minimum Traveling Salesperson Brute Force records: one per ordering, in the order the solver tries them, each
// a whole closed tour (the solver judges whole candidates, so the picture shows each one as tried, never as built up).
//   - an Accept for an ordering that is a tour and is cheaper than every tour before it: the best so far, and the last
//     Accept is the answer. Past the recorder's cap the most recent Accept is still kept, just before the Done, so a long
//     run always shows the step that found the cheapest tour;
//   - a Reject for an ordering with a hop that is not an edge (about that hop's two cities), or for a tour that costs no
//     less than the best so far (about the whole tour);
//   - the Done, with the cheapest tour or none.
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

    // How the steps show an ordering: the closed tour through it (the first city repeated at the end), and, given as the
    // state, the hop of it that is not an edge: from order[missing] to the city after it (-1: none, so the step is about
    // the whole tour). An Accept says its own focus, none, so the picture draws the new best tour as taken rather than as
    // looked at.
    private static RunSteps<int, Tour> NewSteps(List<string> nodes, int[] order, StepRecorder<Tour> rec) => new(rec,
        // What every picture shows: the closed tour, and its missing hop or its edges.
        partial: _ => new Tour(Closed(nodes, order)),
        focus: missing => missing >= 0
            ? [nodes[order[missing]], nodes[order[(missing + 1) % order.Length]]]
            : EdgeIds(Closed(nodes, order)));

    /// <summary>
    /// A cheapest tour, as <c>{a,c,b,e,d,a}</c> (the closing repeat of the first city included, as Traveling
    /// Salesperson's solvers write it), or <c>{}</c> when there is none: fewer than three cities, or no ordering whose
    /// every hop is an edge.
    /// </summary>
    public string solve(MINIMUMTSP problem) => Solve(problem, StepRecorder<Tour>.Off);

    // Keeps the first city fixed and tries every ordering of the others, each a whole closed tour, keeping the cheapest
    // one found (the first of several equally cheap ones).
    public string Solve(MINIMUMTSP problem, StepRecorder<Tour> rec) {
        List<string> nodes = problem.nodes;
        int n = nodes.Count;
        // order[0] stays 0; order[1..] runs through every permutation of 1..n-1 in lexicographic order.
        int[] order = Enumerable.Range(0, n).ToArray();
        var steps = NewSteps(nodes, order, rec);
        if (n == 0) {
            steps.Done(new Tour([]), false, $"There are no cities to tour.");
            return "{}";
        }
        if (n < 3) {
            steps.Done(new Tour([]), false, $"A tour needs at least three cities, and there {(n == 1 ? "is one" : "are two")}.");
            return "{}";
        }

        int?[,] weight = WeightTable(problem);
        int[]? best = null;
        long bestCost = long.MaxValue;
        int tried = 0;
        do {
            tried++;
            long? cost = Cost(weight, order, out int missing);
            if (cost is not long c) {
                steps.Reject(missing, $"Try {GraphTourFrames.Arrows(Closed(nodes, order))}: there is no edge between {nodes[order[missing]]} and {nodes[order[(missing + 1) % n]]}.");
            } else if (c < bestCost) {
                // Strictly cheaper, so the first of several equally cheap tours is kept.
                long previous = bestCost;
                bestCost = c;
                best = (int[])order.Clone();
                if (previous == long.MaxValue)
                    steps.Accept(-1, $"Try {GraphTourFrames.Arrows(Closed(nodes, order))}: cost {c}, the first tour found, so the best so far.", focus: () => []);
                else
                    steps.Accept(-1, $"Try {GraphTourFrames.Arrows(Closed(nodes, order))}: cost {c}, a new best tour (it beats {previous}).", focus: () => []);
            } else {
                steps.Reject(-1, $"Try {GraphTourFrames.Arrows(Closed(nodes, order))}: cost {c}, not better than the best so far ({bestCost}).");
            }
        } while (NextPermutation(order, 1));

        if (best == null) {
            steps.Done(new Tour([]), false, $"Tried all {Words.Count(tried, "ordering")} of the cities after {nodes[0]}. None is a tour.");
            return "{}";
        }
        string answer = ToCertificate(best, nodes);
        steps.Done(GraphTourFrames.ParseTour(answer), true,
            $"Tried all {Words.Count(tried, "ordering")} of the cities after {nodes[0]}. The cheapest tour is {GraphTourFrames.Arrows(GraphTourFrames.ParseTour(answer).Order)}, cost {bestCost}.");
        return answer;
    }

    // The closed tour through order, by name: the first city repeated at the end.
    private static string[] Closed(List<string> nodes, int[] order) => order.Append(order[0]).Select(i => nodes[i]).ToArray();

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
    internal static long? Cost(int?[,] weight, int[] order) => Cost(weight, order, out _);

    /// <summary>
    /// The cost of the closed tour through <paramref name="order"/>, or null if a hop is not an edge; then
    /// <paramref name="missing"/> is the first such hop, from <c>order[missing]</c> to the city after it (else -1).
    /// </summary>
    internal static long? Cost(int?[,] weight, int[] order, out int missing) {
        long total = 0;
        for (int i = 0; i < order.Length; i++) {
            int? w = weight[order[i], order[(i + 1) % order.Length]];
            if (w == null) {
                missing = i;
                return null;
            }
            total += w.Value;
        }
        missing = -1;
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
