// BinPackingBruteForce.cs
// This is the exact / correct solver for Bin Packing.
// It uses recursive backtracking to try every possible way of placing items
// into bins, pruning branches that would exceed the bin capacity.
//
// Because this solver is exhaustive, it always gives the right answer:
//   - If a valid packing exists, it returns one as a certificate string.
//   - If no packing fits within K bins of capacity C, it returns "".
//
// Worst-case time complexity is O(K^n) — exponential — so it will be slow
// on large inputs. That's expected; Bin Packing is NP-Complete.

using API.Interfaces;

namespace API.Problems.NPComplete.NPC_BINPACKING.Solvers;

class BinPackingBruteForce : ISolver<BINPACKING> {

        // ── Metadata ─────────────────────────────────────────────────────────────

        public string solverName { get; } = "Bin Packing Brute Force Solver";

        // Plain-English explanation of the algorithm strategy, including the two
        // pruning tricks that make it faster in practice than the raw O(K^n) bound.
        public string solverDefinition { get; } = "Exhaustive backtracking solver for the Bin Packing decision problem. For each item in the input order, the solver attempts to place the item into each of the K candidate bins; branches that would exceed capacity C are pruned, and symmetric placements into equivalent empty bins are skipped. The first complete assignment that respects capacity and the bin-count limit is returned as the certificate; if no such assignment exists the solver returns the empty string.";

        public string source { get; } = "Garey, M. R., and Johnson, D. S. Computers and Intractability: A Guide to the Theory of NP-Completeness. W. H. Freeman, 1979. Problem SR1.";

        public string[] contributors { get; } = { "Himanshu", "Rakesh", "Prashanta", "Michael Trosper" };

        public bool timerHasExpired { get; set; }
        // Declared, not derived. Does real (if minor) pruning, but tagged BruteForce per its class name
        // and because the pruning is incidental, not the algorithm's defining feature -- contrast with
        // the Backtracking cluster below, where pruning/bounding IS the defining feature.
        public SolverType solverType { get; } = SolverType.BruteForce;
        public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;

        // Worst-case complexity — each of the n items can go into any of K bins.
        public string complexity { get; } = "O(K^n)";

        // ── Public entry point ────────────────────────────────────────────────────

        public string solve(BINPACKING problem) {
                List<int> items = problem.S;
                int C = problem.C;
                int K = problem.K;
                int n = items.Count;

                // Fast pre-check: if any single item is larger than a bin can hold,
                // there is no valid packing regardless of how many bins we have.
                foreach (int size in items) {
                        if (size > C) return "";
                }

                // Edge case: no bins allowed.
                // An empty item list can be packed into zero bins; anything else cannot.
                if (K <= 0) {
                        return n == 0 ? "()" : "";
                }

                // Set up K empty bins — each bin tracks its running sum and its contents.
                int[] binSums = new int[K];
                List<int>[] binContents = new List<int>[K];
                for (int i = 0; i < K; i++) binContents[i] = new List<int>();

                // Run the backtracking search starting from the first item.
                if (!Place(0, items, binSums, binContents, C, K)) return "";
                return FormatCertificate(binContents);
        }

        // ── Recursive backtracking ────────────────────────────────────────────────

        // Tries to place items[i] into one of the K bins, then recurses on items[i+1].
        // Returns true as soon as a complete valid assignment is found.
        private bool Place(int i, List<int> items, int[] binSums, List<int>[] binContents, int C, int K) {

                // Base case: all items have been placed successfully.
                if (i == items.Count) return true;

                int size = items[i];

                for (int b = 0; b < K; b++) {

                        // Capacity pruning: skip this bin if the item won't fit.
                        if (binSums[b] + size <= C) {
                                // Place the item and recurse.
                                binSums[b] += size;
                                binContents[b].Add(size);

                                if (Place(i + 1, items, binSums, binContents, C, K)) return true;

                                // That branch failed — undo the placement and try the next bin.
                                binSums[b] -= size;
                                binContents[b].RemoveAt(binContents[b].Count - 1);
                        }

                        // Symmetry break: all empty bins are identical, so there is no point
                        // trying item i in bin b+1 if bin b is also empty and already failed.
                        // This single check dramatically shrinks the search space when K > n.
                        if (binSums[b] == 0) break;
                }

                // No valid bin was found for items[i] in this branch.
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
