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
// If FFD needs more than K bins, it returns "{}" — but that does NOT prove
// the instance is a NO-instance. The brute force solver is the authoritative
// decider; FFD is offered as a fast heuristic alternative.
//
// Approximation guarantee (Dósa, 2007):
//   FFD uses at most (11/9) · OPT + 6/9 bins — tight bound.

using API.Interfaces;
using API.Interfaces.Packing;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_BINPACKING.Solvers;

// The steps Bin Packing First Fit Decreasing records, for the Packing picture (item i is S[i], bin b is container b):
//   - a Reject, before the pass, for an item bigger than a whole bin;
//   - an Update for the sorted order (nothing placed yet);
//   - an Accept for every item it puts in a bin, saying which bins it didn't fit in, or that it opens a new one;
//   - a Reject for the item that would need a bin past K;
//   - the Done, with the packing or why there is none.
// Incremental: a greedy pass that puts each item in a bin for good, so every step's partial is what is placed so far.
class BinPackingFFD : ISolver<BINPACKING, Placement>, IIncrementalSolver {

    // ── Metadata ─────────────────────────────────────────────────────────────

    public string solverName { get; } = "Bin Packing First Fit Decreasing";

    // The definition explains the algorithm, its complexity, and — importantly —
    // the caveat that FFD is a heuristic, not an exact decider.
    public string solverDefinition { get; } = "First Fit Decreasing is a classical approximation heuristic for Bin Packing. Items are first sorted in descending order of size; each item is then placed into the first existing bin with enough remaining capacity, or into a newly opened bin if no existing bin fits. FFD runs in O(n^2) time and is guaranteed to use at most (11/9) OPT + 6/9 bins (Dosa, 2007). Because FFD is a heuristic for the optimization variant, on a decision instance where the minimum number of bins equals K it may still open more than K bins; in that case this solver returns {}, which should not be interpreted as proof of infeasibility.";

    // Two sources: Johnson's original PhD thesis that introduced FFD, and
    // Dósa's 2007 paper that proved the tight (11/9) OPT bound.
    public string source { get; } = "Johnson, D. S. Near-optimal bin-packing algorithms. PhD thesis, MIT, 1973. Dosa, G. The Tight Bound of First Fit Decreasing Bin-Packing Algorithm Is FFD(I) <= (11/9) OPT(I) + 6/9. Combinatorics, Algorithms, Probabilistic and Experimental Methodologies, LNCS 4614, Springer, 2007, pp. 1-11.";
    public string sourceFile { get; } = SourceFile.Path();

    public string[] contributors { get; } = { "Himanshu Jha", "Rakesh Itani", "Prashant Aguragai" };

    public bool timerHasExpired { get; set; }
    // Declared, not derived. Both the guarantee and the mechanism: its own doc comment claims a proven
    // Dosa 2007 approximation-ratio bound ((11/9)*OPT + 6/9), which makes it Approximation, and it gets
    // there with one greedy first-fit pass, which makes it Greedy.
    public SolverType solverType { get; } = SolverType.Approximation | SolverType.Greedy;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;

    // Much cheaper than brute force — quadratic in the number of items.
    public MathematicalFunction timeComplexity { get; } = new("n^2", "");

    // ── Public entry point ────────────────────────────────────────────────────

    // How the steps show the pass, given by the item a step is about (-1: none) and the bin it goes in (-1: none). binOf
    // holds the bin of every item placed so far (-1: not placed).
    private static RunSteps<(int Item, int Bin), Placement> NewSteps(StepRecorder<Placement> rec, int[] binOf) => new(rec,
        // What every picture shows: the items placed so far, and the item with the bin it goes in.
        partial: _ => new Placement(binOf),
        focus: s => s.Item < 0 ? [] : s.Bin < 0 ? [PackingFrames.ItemId(s.Item)] : [PackingFrames.ItemId(s.Item), PackingFrames.ContainerId(s.Bin)]);

    public string solve(BINPACKING problem) => Solve(problem, StepRecorder<Placement>.Off);

    public string Solve(BINPACKING problem, StepRecorder<Placement> rec) {
        int C = problem.C;
        int K = problem.K;

        // Step 1: Sort items largest-first. We create a new sorted list so we
        // don't mutate the original problem's S (immutability principle).
        // The positions in S, in the same (stable) order, for the picture.
        List<int> order = Enumerable.Range(0, problem.S.Count).OrderByDescending(i => problem.S[i]).ToList();
        List<int> sorted = order.Select(i => problem.S[i]).ToList();
        int[] binOf = Enumerable.Repeat(-1, problem.S.Count).ToArray();
        var steps = NewSteps(rec, binOf);

        // Fast pre-check: if any item exceeds bin capacity, packing is impossible
        // regardless of how many bins we have.
        foreach (int i in order) {
            int size = problem.S[i];
            if (size > C) {
                steps.Reject((i, -1), $"{size} is bigger than a whole bin ({C}), so nothing can work.");
                steps.Done(new Placement([]), false, $"No packing exists: {size} doesn't fit in a bin of {C}.");
                return "{}";
            }
        }

        if (sorted.Count == 0) {
            steps.Done(new Placement([]), true, $"There are no items, so no bins are needed.");
            return FormatCertificate(new List<List<int>>());
        }
        steps.Update((-1, -1), $"Sort largest first: {string.Join(", ", sorted)}. Each goes in the first bin with room.");

        // bins holds the items placed in each bin so far.
        // binSums tracks each bin's current total — kept parallel to bins.
        List<List<int>> bins = new List<List<int>>();
        List<int> binSums = new List<int>();

        // Step 2: Place each item using the First Fit rule.
        foreach (int i in order) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer; // stop at the time limit
            int size = problem.S[i];
            bool placed = false;

            // Scan existing bins left to right — place in the first one that fits.
            for (int b = 0; b < bins.Count; b++) {
                if (binSums[b] + size <= C) {
                    bins[b].Add(size);
                    // Create a new sum value rather than mutating in-place.
                    binSums[b] = binSums[b] + size;
                    placed = true;
                    binOf[i] = b;
                    // The why names the bins it didn't fit in: none, bin 1, or bins 1 to b.
                    if (b == 0)
                        steps.Accept((i, b), $"{size} fits in bin 1: {binSums[b]} of {C} used.");
                    else if (b == 1)
                        steps.Accept((i, b), $"{size} doesn't fit in bin 1, so it goes in bin 2: {binSums[b]} of {C} used.");
                    else
                        steps.Accept((i, b), $"{size} doesn't fit in bins 1 to {b}, so it goes in bin {b + 1}: {binSums[b]} of {C} used.");
                    break;
                }
            }

            // No existing bin had room — open a new one.
            if (!placed) {
                // If opening a new bin would exceed the K-bin limit, we give up.
                // This returns "{}" which means "FFD couldn't find a packing within K bins".
                // It does NOT mean the instance is unsolvable — use brute force to be sure.
                if (bins.Count + 1 > K) {
                    if (bins.Count == 0)
                        steps.Reject((i, -1), $"There is no bin yet for {size}, and K = {K} allows none.");
                    else
                        steps.Reject((i, -1), $"No open bin has room for {size}, and opening another would need {bins.Count + 1} bins, more than K = {K}.");
                    steps.Done(new Placement([]), false,
                        $"First Fit Decreasing needs more than {Words.Count(K, "bin")}. It is a heuristic, so the brute force may still find a packing.");
                    return "{}";
                }
                bins.Add(new List<int> { size });
                binSums.Add(size);
                binOf[i] = bins.Count - 1;
                if (bins.Count == 1)
                    steps.Accept((i, 0), $"{size} goes in bin 1: {size} of {C} used.");
                else
                    steps.Accept((i, bins.Count - 1), $"No open bin has room for {size}, so open bin {bins.Count}: {size} of {C} used.");
            }
        }

        steps.Done(new Placement(binOf), true,
            $"Everything is packed into {Words.Count(bins.Count, "bin")} of {C}, within K = {K}.");
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
