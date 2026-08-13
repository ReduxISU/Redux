// BinPackingFFD.cs
// This is the First Fit Decreasing (FFD) heuristic solver for Bin Packing.
//
// The idea is simple and intuitive:
//   1. Sort all items from largest to smallest.
//   2. For each item, scan existing bins left to right and place it in the
//      first bin that still has enough room.
//   3. If no existing bin fits, open a new bin (as long as we haven't hit K).
//
// FFD is much faster than brute force — O(n²) — and usually finds a good
// packing, but it is NOT guaranteed to find one when the answer is YES.
// If FFD needs more than K bins, it returns "" — but that does NOT prove
// the instance is a NO-instance. The brute force solver is the authoritative
// decider; FFD is offered as a fast heuristic alternative.
//
// Approximation guarantee (Dósa, 2007):
//   FFD uses at most (11/9) · OPT + 6/9 bins — tight bound.

using API.Interfaces;

namespace API.Problems.NPComplete.NPC_BINPACKING.Solvers;

class BinPackingFFD : ISolver<BINPACKING> {

    // ── Metadata ─────────────────────────────────────────────────────────────

    public string solverName { get; } = "Bin Packing First Fit Decreasing (FFD)";

    // The definition explains the algorithm, its complexity, and — importantly —
    // the caveat that FFD is a heuristic, not an exact decider.
    public string solverDefinition { get; } = "First Fit Decreasing is a classical approximation heuristic for Bin Packing. Items are first sorted in descending order of size; each item is then placed into the first existing bin with enough remaining capacity, or into a newly opened bin if no existing bin fits. FFD runs in O(n^2) time and is guaranteed to use at most (11/9) OPT + 6/9 bins (Dosa, 2007). Because FFD is a heuristic for the optimization variant, on a decision instance where the minimum number of bins equals K it may still open more than K bins; in that case this solver returns an empty certificate, which should not be interpreted as proof of infeasibility.";

    // Two sources: Johnson's original PhD thesis that introduced FFD, and
    // Dósa's 2007 paper that proved the tight (11/9) OPT bound.
    public string source { get; } = "Johnson, D. S. Near-optimal bin-packing algorithms. PhD thesis, MIT, 1973. Dosa, G. The Tight Bound of First Fit Decreasing Bin-Packing Algorithm Is FFD(I) <= (11/9) OPT(I) + 6/9. Combinatorics, Algorithms, Probabilistic and Experimental Methodologies, LNCS 4614, Springer, 2007, pp. 1-11.";

    public string[] contributors { get; } = { "Himanshu", "Rakesh", "Prashanta" };

    public bool timerHasExpired { get; set; }
    // Declared, not derived. Tag reflects the guarantee class, not the mechanism: the algorithm's
    // mechanism is a greedy first-fit pass, but its own doc comment claims a proven Dosa 2007
    // approximation-ratio bound ((11/9)*OPT + 6/9) -- that guarantee makes it Approximation, not
    // Greedy.
    public SolverType solverType { get; } = SolverType.Approximation;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;

    // Much cheaper than brute force — quadratic in the number of items.
    public string complexity { get; } = "O(n^2)";

    // ── Public entry point ────────────────────────────────────────────────────

    public string solve(BINPACKING problem) {
        int C = problem.C;
        int K = problem.K;

        // Step 1: Sort items largest-first. We create a new sorted list so we
        // don't mutate the original problem's S (immutability principle).
        List<int> sorted = problem.S.OrderByDescending(x => x).ToList();

        // Fast pre-check: if any item exceeds bin capacity, packing is impossible
        // regardless of how many bins we have.
        foreach (int size in sorted) {
            if (size > C) return "";
        }

        // bins holds the items placed in each bin so far.
        // binSums tracks each bin's current total — kept parallel to bins.
        List<List<int>> bins = new List<List<int>>();
        List<int> binSums = new List<int>();

        // Step 2: Place each item using the First Fit rule.
        foreach (int size in sorted) {
            bool placed = false;

            // Scan existing bins left to right — place in the first one that fits.
            for (int b = 0; b < bins.Count; b++) {
                if (binSums[b] + size <= C) {
                    bins[b].Add(size);
                    // Create a new sum value rather than mutating in-place.
                    binSums[b] = binSums[b] + size;
                    placed = true;
                    break;
                }
            }

            // No existing bin had room — open a new one.
            if (!placed) {
                // If opening a new bin would exceed the K-bin limit, we give up.
                // This returns "" which means "FFD couldn't find a packing within K bins".
                // It does NOT mean the instance is unsolvable — use brute force to be sure.
                if (bins.Count + 1 > K) return "";
                bins.Add(new List<int> { size });
                binSums.Add(size);
            }
        }

        return FormatCertificate(bins);
    }

    // ── Certificate formatter ─────────────────────────────────────────────────

    // Converts the bin list into the Redux certificate string format.
    // Example: bins [[8,2],[7,3],[6,4]] → "((8,2),(7,3),(6,4))"
    // An empty bins list (no items) produces "()" — a valid empty certificate.
    private string FormatCertificate(List<List<int>> bins) {
        if (bins.Count == 0) return "()";
        List<string> binStrings = new List<string>();
        foreach (List<int> bin in bins) {
            binStrings.Add("(" + string.Join(",", bin) + ")");
        }
        return "(" + string.Join(",", binStrings) + ")";
    }
}
