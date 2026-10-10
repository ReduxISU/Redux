// BinPackingBruteForce.cs
// This is the exact / correct solver for Bin Packing.
// It uses recursive backtracking to try every possible way of placing items
// into bins, pruning branches that would exceed the bin capacity.
//
// Because this solver is exhaustive, it always gives the right answer:
//   - If a valid packing exists, it returns one as a certificate string.
//   - If no packing fits within K bins of capacity C, it returns "{}" (no answer).
//
// Worst-case time complexity is O(K^n) — exponential — so it will be slow
// on large inputs. That's expected; Bin Packing is NP-Complete.

using API.Interfaces;
using API.Interfaces.Packing;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_BINPACKING.Solvers;

// Incremental: it places one item at a time and takes placements back, so every step's partial is what is placed so far.
class BinPackingBruteForce : ISolver<BINPACKING, Placement>, IIncrementalSolver {

    // ── Metadata ─────────────────────────────────────────────────────────────

    public string solverName { get; } = "Bin Packing Brute Force";

    // Plain-English explanation of the algorithm strategy, including the two
    // pruning tricks that make it faster in practice than the raw O(K^n) bound.
    public string solverDefinition { get; } = "Exhaustive backtracking solver for the Bin Packing decision problem. For each item in the input order, the solver attempts to place the item into each of the K candidate bins; branches that would exceed capacity C are pruned, and symmetric placements into equivalent empty bins are skipped. The first complete assignment that respects capacity and the bin-count limit is returned as the certificate; if no such assignment exists the solver returns {}.";

    public string source { get; } = "Garey, M. R., and Johnson, D. S. Computers and Intractability: A Guide to the Theory of NP-Completeness. W. H. Freeman, 1979. Problem SR1.";
    public string sourceFile { get; } = SourceFile.Path();

    public string[] contributors { get; } = { "Himanshu Jha", "Rakesh Itani", "Prashant Aguragai", "Michael Trosper" };

    public bool timerHasExpired { get; set; }
    // Declared, not derived. Does real (if minor) pruning, but tagged BruteForce per its class name
    // and because the pruning is incidental, not the algorithm's defining feature -- contrast with
    // the Backtracking cluster below, where pruning/bounding IS the defining feature.
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;

    // Worst-case complexity — each of the n items can go into any of K bins.
    public string complexity { get; } = "O(K^n)";

    // ── Public entry point ────────────────────────────────────────────────────

    public string solve(BINPACKING problem) => Solve(problem, StepRecorder<Placement>.Off);

    // Steps: an Accept for every item put in a bin, a Reject when an item has nowhere to go, a Backtrack for every placement
    // taken back, then the answer. Item i of the picture is items[i]; bin b is container b.
    public string Solve(BINPACKING problem, StepRecorder<Placement> rec) {
        List<int> items = problem.S;
        int C = problem.C;
        int K = problem.K;
        int n = items.Count;

        // Fast pre-check: if any single item is larger than a bin can hold,
        // there is no valid packing regardless of how many bins we have.
        for (int i = 0; i < n; i++) {
            int size = items[i];
            if (size > C) {
                rec.Reject(() => new Placement([]), () => $"{size} is bigger than a whole bin ({C}), so nothing can work.", PackingFrames.ItemId(i));
                rec.Done(new Placement([]), false, $"No packing exists: {size} doesn't fit in a bin of {C}.");
                return "{}";
            }
        }

        // Edge case: no bins allowed.
        // An empty item list can be packed into zero bins; anything else cannot.
        if (K <= 0) {
            rec.Done(new Placement([]), n == 0, n == 0
                ? "There are no items, so no bins are needed."
                : $"No bins are allowed (K = {K}), so the items can't be packed.");
            return n == 0 ? "()" : "{}";
        }

        // Set up K empty bins — each bin tracks its running sum and its contents.
        int[] binSums = new int[K];
        List<int>[] binContents = new List<int>[K];
        for (int i = 0; i < K; i++) binContents[i] = new List<int>();
        // The bin of every item placed so far (-1: not placed), for the picture.
        int[] binOf = Enumerable.Repeat(-1, n).ToArray();

        // Run the backtracking search starting from the first item.
        if (!Place(0, items, binSums, binContents, C, K, binOf, rec)) {
            rec.Done(new Placement([]), false, $"Every arrangement overflows: the {n} items don't fit in {K} {(K == 1 ? "bin" : "bins")} of {C}.");
            return "{}";
        }
        int used = binContents.Count(b => b.Count > 0);
        rec.Done(new Placement(binOf), true, n == 0
            ? "There are no items, so no bins are needed."
            : $"All {n} items fit in {used} {(used == 1 ? "bin" : "bins")} of {C}, within K = {K}.");
        return FormatCertificate(binContents);
    }

    // ── Recursive backtracking ────────────────────────────────────────────────

    // Tries to place items[i] into one of the K bins, then recurses on items[i+1].
    // Returns true as soon as a complete valid assignment is found.
    private bool Place(int i, List<int> items, int[] binSums, List<int>[] binContents, int C, int K, int[] binOf, StepRecorder<Placement> rec) {

        // Base case: all items have been placed successfully.
        if (i == items.Count) return true;

        int size = items[i];
        bool roomSomewhere = false;

        for (int b = 0; b < K; b++) {

            // Capacity pruning: skip this bin if the item won't fit.
            if (binSums[b] + size <= C) {
                // Place the item and recurse.
                binSums[b] += size;
                binContents[b].Add(size);
                binOf[i] = b;
                roomSomewhere = true;
                int bin = b, now = binSums[b];
                rec.Accept(() => new Placement(binOf), () => $"Put {size} in bin {bin + 1}: {now} of {C} used.",
                    PackingFrames.ItemId(i), PackingFrames.ContainerId(b));

                if (Place(i + 1, items, binSums, binContents, C, K, binOf, rec)) return true;

                // That branch failed — undo the placement and try the next bin.
                binSums[b] -= size;
                binContents[b].RemoveAt(binContents[b].Count - 1);
                binOf[i] = -1;
                rec.Backtrack(() => new Placement(binOf), () => $"Take {size} back out of bin {bin + 1} and try the next bin.", PackingFrames.ItemId(i));
            }

            // Symmetry break: all empty bins are identical, so there is no point
            // trying item i in bin b+1 if bin b is also empty and already failed.
            // This single check dramatically shrinks the search space when K > n.
            if (binSums[b] == 0) break;
        }

        // No valid bin was found for items[i] in this branch.
        rec.Reject(() => new Placement(binOf), () => (roomSomewhere
            ? $"Every bin with room for {size} leaves no room for the items after it"
            : $"No bin has room for {size}") + (i > 0 ? ", so take back the last choice." : "."), PackingFrames.ItemId(i));
        return false;
    }

    // ── Certificate formatter ─────────────────────────────────────────────────

    // Converts the bin contents into the Redux certificate string format.
    // Example: bins [[4,6],[7,3],[2,8]] → "((4,6),(7,3),(2,8))"
    // Empty bins are skipped — only non-empty bins appear in the output.
    private string FormatCertificate(List<int>[] binContents) {
        List<string> binStrings = new List<string>();
        foreach (List<int> bin in binContents) {
            if (bin.Count == 0) continue;
            binStrings.Add("(" + string.Join(",", bin) + ")");
        }
        return "(" + string.Join(",", binStrings) + ")";
    }
}
