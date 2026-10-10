using API.Interfaces;

namespace API.Problems.NPHard.NPH_MINIMUMTSP.Solvers;

class BruteForceMinimumTSP : ISolver<MINIMUMTSP> {

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
    public string complexity { get; } = "O((n-1)! * n + m), n = |nodes|, m = |edges|";

    // --- Methods Including Constructors ---
    public BruteForceMinimumTSP() {

    }

    /// <summary>
    /// A cheapest tour, as <c>{a,c,b,e,d,a}</c> (the closing repeat of the first city included, as Traveling
    /// Salesperson's solvers write it), or <c>{}</c> when there is none: fewer than three cities, or no ordering whose
    /// every hop is an edge.
    /// </summary>
    public string solve(MINIMUMTSP problem) {
        List<string> nodes = problem.nodes;
        int n = nodes.Count;
        if (n < 3) {
            return "{}";
        }

        int?[,] weight = WeightTable(problem);
        // order[0] stays 0; order[1..] runs through every permutation of 1..n-1 in lexicographic order.
        int[] order = Enumerable.Range(0, n).ToArray();
        int[]? best = null;
        long bestCost = long.MaxValue;
        do {
            long? cost = Cost(weight, order);
            // Strictly cheaper, so the first of several equally cheap tours is kept.
            if (cost is long c && c < bestCost) {
                bestCost = c;
                best = (int[])order.Clone();
            }
        } while (NextPermutation(order, 1));

        return best == null ? "{}" : ToCertificate(best, nodes);
    }

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
