using API.Interfaces;

namespace API.Problems.NPComplete.NPC_BINPACKING.Solvers;

class BinPackingFFD : ISolver<BINPACKING> {

    // --- Fields ---
    public string solverName { get; } = "Bin Packing First Fit Decreasing (FFD)";
    public string solverDefinition { get; } = "First Fit Decreasing is a classical approximation heuristic for Bin Packing. Items are first sorted in descending order of size; each item is then placed into the first existing bin with enough remaining capacity, or into a newly opened bin if no existing bin fits. FFD runs in O(n^2) time and is guaranteed to use at most (11/9) OPT + 6/9 bins (Dosa, 2007). Because FFD is a heuristic for the optimization variant, on a decision instance where the minimum number of bins equals K it may still open more than K bins; in that case this solver returns an empty certificate, which should not be interpreted as proof of infeasibility.";
    public string source { get; } = "Johnson, D. S. Near-optimal bin-packing algorithms. PhD thesis, MIT, 1973. Dosa, G. The Tight Bound of First Fit Decreasing Bin-Packing Algorithm Is FFD(I) <= (11/9) OPT(I) + 6/9. Combinatorics, Algorithms, Probabilistic and Experimental Methodologies, LNCS 4614, Springer, 2007, pp. 1-11.";
    public string[] contributors { get; } = { "TBD" };
    public bool timerHasExpired { get; set; }

    public string complexity { get; } = "O(n^2)";

    // --- Methods Including Constructors ---
    public string solve(BINPACKING problem) {
        int C = problem.C;
        int K = problem.K;

        List<int> sorted = problem.S.OrderByDescending(x => x).ToList();

        foreach (int size in sorted) {
            if (size > C) return "";
        }

        List<List<int>> bins = new List<List<int>>();
        List<int> binSums = new List<int>();

        foreach (int size in sorted) {
            bool placed = false;
            for (int b = 0; b < bins.Count; b++) {
                if (binSums[b] + size <= C) {
                    bins[b].Add(size);
                    binSums[b] = binSums[b] + size;
                    placed = true;
                    break;
                }
            }
            if (!placed) {
                if (bins.Count + 1 > K) return "";
                bins.Add(new List<int> { size });
                binSums.Add(size);
            }
        }

        return FormatCertificate(bins);
    }

    private string FormatCertificate(List<List<int>> bins) {
        if (bins.Count == 0) return "()";
        List<string> binStrings = new List<string>();
        foreach (List<int> bin in bins) {
            binStrings.Add("(" + string.Join(",", bin) + ")");
        }
        return "(" + string.Join(",", binStrings) + ")";
    }
}
