using API.Interfaces;

namespace API.Problems.NPComplete.NPC_BINPACKING.Solvers;

class BinPackingBruteForce : ISolver<BINPACKING> {

    // --- Fields ---
    public string solverName { get; } = "Bin Packing Brute Force Solver";
    public string solverDefinition { get; } = "Exhaustive backtracking solver for the Bin Packing decision problem. For each item in the input order, the solver attempts to place the item into each of the K candidate bins; branches that would exceed capacity C are pruned, and symmetric placements into equivalent empty bins are skipped. The first complete assignment that respects capacity and the bin-count limit is returned as the certificate; if no such assignment exists the solver returns the empty string.";
    public string source { get; } = "Garey, M. R., and Johnson, D. S. Computers and Intractability: A Guide to the Theory of NP-Completeness. W. H. Freeman, 1979. Problem SR1.";
    public string[] contributors { get; } = { "TBD" };
    public bool timerHasExpired { get; set; }

    public string complexity { get; } = "O(K^n)";

    // --- Methods Including Constructors ---
    public string solve(BINPACKING problem) {
        List<int> items = problem.S;
        int C = problem.C;
        int K = problem.K;
        int n = items.Count;

        foreach (int size in items) {
            if (size > C) return "";
        }

        if (K <= 0) {
            return n == 0 ? "()" : "";
        }

        int[] binSums = new int[K];
        List<int>[] binContents = new List<int>[K];
        for (int i = 0; i < K; i++) binContents[i] = new List<int>();

        if (!Place(0, items, binSums, binContents, C, K)) return "";
        return FormatCertificate(binContents);
    }

    private bool Place(int i, List<int> items, int[] binSums, List<int>[] binContents, int C, int K) {
        if (i == items.Count) return true;
        int size = items[i];
        for (int b = 0; b < K; b++) {
            if (binSums[b] + size <= C) {
                binSums[b] += size;
                binContents[b].Add(size);
                if (Place(i + 1, items, binSums, binContents, C, K)) return true;
                binSums[b] -= size;
                binContents[b].RemoveAt(binContents[b].Count - 1);
            }
            if (binSums[b] == 0) break;
        }
        return false;
    }

    private string FormatCertificate(List<int>[] binContents) {
        List<string> binStrings = new List<string>();
        foreach (List<int> bin in binContents) {
            if (bin.Count == 0) continue;
            binStrings.Add("(" + string.Join(",", bin) + ")");
        }
        return "(" + string.Join(",", binStrings) + ")";
    }
}
