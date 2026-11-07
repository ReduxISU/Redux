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

// The steps Bin Packing Brute Force records, for the Packing picture (item i is S[i], bin b is container b):
//   - a Reject, before the search, for an item bigger than a whole bin;
//   - an Accept for every item it puts in a bin, saying how full the bin is now;
//   - a Backtrack for every placement it takes back;
//   - a Reject when an item has nowhere to go (no bin with room, or none that works for the items after it);
//   - the Done, with the packing or why there is none.
// The same steps draw the Search Tree picture. In the tree, a node is an item put in a bin ("4 in bin 2"), and its children
// are the bins tried for the next item; the root is cut off (pruned, "9 > C = 8") by an item bigger than a whole bin. An
// Accept's node is explored, or the solution for the last item; a placement taken back fails; an item with no bin that has
// room is a pruned child named after it ("4", noted "no bin has room"), and when every bin with room for an item has
// failed, the Reject fails the placement before it (or the root). The Done names the answer's path, root first, or marks
// the root failed.
// Incremental: it places one item at a time and takes placements back, so every step's partial is what is placed so far.
class BinPackingBruteForce : ISolver<BINPACKING, Placement>, IIncrementalSolver, ISearchTreeSolver {

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
    public MathematicalFunction timeComplexity { get; } = new("K^n", "");

    // ── Public entry point ────────────────────────────────────────────────────

    // A step of the search is given by the item it is about and the bin it goes in, or, when it is about no one bin, why:
    // a whole bin is too small for the item, no bin has room for it, or every bin with room for it has failed. Two plain
    // ints, so solving without steps passes no more through the search than before.
    private const int TooBig = -3, NoBinHasRoom = -2, EveryBinFailed = -1;

    // How the steps show the search. binOf holds the bin of every item placed so far (-1: not placed). The search keeps its
    // own tree (a "start" root, an item with no room as a pruned node, a placement failed when it is taken back), so it gives
    // RunSteps the tree node of each step rather than using SearchSteps.
    private static RunSteps<(int Item, int Bin), Placement> NewSteps(StepRecorder<Placement> rec, int[] binOf, List<int> items, int C, DepthFirstTree tree) => new(rec,
        // What every picture shows: the items placed so far, and the item, with the bin it goes in when it is placed there.
        partial: _ => new Placement(binOf),
        focus: s => s.Bin >= 0 && binOf[s.Item] == s.Bin ? [PackingFrames.ItemId(s.Item), PackingFrames.ContainerId(s.Bin)] : [PackingFrames.ItemId(s.Item)],
        // The Search Tree picture's node only.
        tree: s => TreeNode(s.Item, s.Bin, binOf, items, C, tree));

    // The search-tree node of a step about item i and bin b. Item i's placement is the node at depth i + 1, labelled with
    // the item and its bin ("4 in bin 2"); the root is at depth 0.
    private static SearchNode[] TreeNode(int i, int b, int[] binOf, List<int> items, int C, DepthFirstTree tree) {
        string Placed(int item, int bin) => $"{PackingFrames.Num(items[item])} in bin {bin + 1}";
        return b switch {
            // An item bigger than a bin cuts the root off.
            TooBig => tree.Step(0, "", SearchNodeState.Pruned, $"{items[i]} > C = {C}"),
            // An item no bin has room for is a dead end under the item before it.
            NoBinHasRoom => tree.Step(i + 1, PackingFrames.Num(items[i]), SearchNodeState.Pruned, "no bin has room"),
            // When every bin with room for an item has failed, the placement before it (or the root) fails.
            EveryBinFailed => i == 0 ? tree.Step(0, "", SearchNodeState.Failed) : tree.Step(i, Placed(i - 1, binOf[i - 1]), SearchNodeState.Failed),
            // An item just put in bin b is explored, or the solution when it is the last item; one taken back out fails.
            _ => tree.Step(i + 1, Placed(i, b), binOf[i] != b ? SearchNodeState.Failed : i == items.Count - 1 ? SearchNodeState.Solution : SearchNodeState.Explored),
        };
    }

    public string solve(BINPACKING problem) => Solve(problem, StepRecorder<Placement>.Off);

    public string Solve(BINPACKING problem, StepRecorder<Placement> rec) {
        List<int> items = problem.S;
        int C = problem.C;
        int K = problem.K;
        int n = items.Count;
        // The bin of every item placed so far (-1: not placed), for the picture.
        int[] binOf = Enumerable.Repeat(-1, n).ToArray();
        // A branch places each item once.
        var tree = new DepthFirstTree(n);
        var steps = NewSteps(rec, binOf, items, C, tree);

        // Fast pre-check: if any single item is larger than a bin can hold,
        // there is no valid packing regardless of how many bins we have.
        for (int i = 0; i < n; i++) {
            int size = items[i];
            if (size > C) {
                // In the tree, the item cuts the root off.
                steps.Reject((i, TooBig), $"{size} is bigger than a whole bin ({C}), so nothing can work.");
                steps.Done(new Placement([]), false, $"No packing exists: {size} doesn't fit in a bin of {C}.", tree: () => tree.NoAnswer());
                return "{}";
            }
        }

        // Edge case: no bins allowed.
        // An empty item list can be packed into zero bins; anything else cannot.
        if (K <= 0) {
            if (n == 0) {
                steps.Done(new Placement([]), true, $"There are no items, so no bins are needed.");
                return "()";
            }
            steps.Done(new Placement([]), false, $"No bins are allowed (K = {K}), so the items can't be packed.");
            return "{}";
        }

        // Set up K empty bins — each bin tracks its running sum and its contents.
        int[] binSums = new int[K];
        List<int>[] binContents = new List<int>[K];
        for (int i = 0; i < K; i++) binContents[i] = new List<int>();

        // Run the backtracking search starting from the first item.
        if (!Place(0, items, binSums, binContents, C, K, binOf, tree, steps)) {
            steps.Done(new Placement([]), false, $"Every arrangement overflows: the {n} items don't fit in {Words.Count(K, "bin")} of {C}.",
                tree: () => tree.NoAnswer());
            return "{}";
        }
        if (timerHasExpired) return SolveRun.TimedOutAnswer; // Place unwinds as if done at the time limit
        int used = binContents.Count(b => b.Count > 0);
        // The answer's path in the tree: each item, in order, in its bin.
        var path = () => tree.AnswerPath(items.Select((size, i) => $"{PackingFrames.Num(size)} in bin {binOf[i] + 1}").ToList());
        if (n == 0)
            steps.Done(new Placement(binOf), true, $"There are no items, so no bins are needed.", tree: path);
        else
            steps.Done(new Placement(binOf), true, $"All {n} items fit in {Words.Count(used, "bin")} of {C}, within K = {K}.", tree: path);
        return FormatCertificate(binContents);
    }

    // ── Recursive backtracking ────────────────────────────────────────────────

    // Tries to place items[i] into one of the K bins, then recurses on items[i+1].
    // Returns true as soon as a complete valid assignment is found.
    private bool Place(int i, List<int> items, int[] binSums, List<int>[] binContents, int C, int K, int[] binOf,
        DepthFirstTree tree, RunSteps<(int Item, int Bin), Placement> steps) {

        // At the time limit, unwind as if done; solve() then returns the timed-out answer.
        if (timerHasExpired) return true;

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
                tree.Branch(i + 1);
                steps.Accept((i, b), $"Put {size} in bin {b + 1}: {binSums[b]} of {C} used.");

                if (Place(i + 1, items, binSums, binContents, C, K, binOf, tree, steps)) return true;

                // That branch failed — undo the placement and try the next bin.
                binSums[b] -= size;
                binContents[b].RemoveAt(binContents[b].Count - 1);
                binOf[i] = -1;
                steps.Backtrack((i, b), $"Take {size} back out of bin {b + 1} and try the next bin.");
            }

            // Symmetry break: all empty bins are identical, so there is no point
            // trying item i in bin b+1 if bin b is also empty and already failed.
            // This single check dramatically shrinks the search space when K > n.
            if (binSums[b] == 0) break;
        }

        // No valid bin was found for items[i] in this branch: back up to the item before it, if there is one. In the tree,
        // an item no bin has room for is a new node.
        if (!roomSomewhere) tree.Branch(i + 1);
        if (roomSomewhere && i > 0)
            steps.Reject((i, EveryBinFailed), $"Every bin with room for {size} leaves no room for the items after it, so take back the last choice.");
        else if (roomSomewhere)
            steps.Reject((i, EveryBinFailed), $"Every bin with room for {size} leaves no room for the items after it.");
        else if (i > 0)
            steps.Reject((i, NoBinHasRoom), $"No bin has room for {size}, so take back the last choice.");
        else
            steps.Reject((i, NoBinHasRoom), $"No bin has room for {size}.");
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
