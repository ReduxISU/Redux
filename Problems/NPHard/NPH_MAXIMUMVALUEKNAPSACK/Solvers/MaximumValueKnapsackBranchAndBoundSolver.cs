using System.Numerics;
using API.Interfaces;

namespace API.Problems.NPHard.NPH_MAXIMUMVALUEKNAPSACK.Solvers;

internal sealed class MaximumValueKnapsackBranchAndBoundSolver : ISolver<MAXIMUMVALUEKNAPSACK> {
    public string solverName { get; } = "Maximum-Value 0-1 Knapsack Exact Branch and Bound";
    public string solverDefinition { get; } = "Uses explicit-stack, include-first depth-first search in descending exact value/weight density, breaking density ties by original item index. Prunes with a fractional-knapsack upper bound calculated with BigInteger and rounded down for integer objectives. Maintains a feasible greedy incumbent and returns an optimum only on completion. There is no 20-item limit; worst-case search remains exponential. Positive-value zero-weight items are always included and zero-value or individually overweight items are omitted. Tied incumbents are retained deterministically.";
    public string source { get; } = "Fractional-knapsack relaxation and branch-and-bound search for 0-1 knapsack; Kellerer, Pferschy, and Pisinger, Knapsack Problems (2004).";
    public string sourceLink { get; } = "https://doi.org/10.1007/978-3-540-24777-7";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Corbin Hay" };
    public SolverType solverType { get; } = SolverType.Backtracking;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    public MathematicalFunction timeComplexity { get; } = new("n * 2^n", ", n = original item occurrences; conservative worst case including bounds and incumbent copies, plus O(n log n) preprocessing; BigInteger products involve Int64 inputs");
    public MathematicalFunction spaceComplexity { get; } = new("n", ", n = original item occurrences; sorted items, merge buffer, DFS stack, and current/best selections");
    private volatile bool _timerHasExpired;
    public bool timerHasExpired { get => _timerHasExpired; set => _timerHasExpired = value; }

    internal long SearchNodes { get; private set; }
    internal long PrunedBranches { get; private set; }

    public string solve(MAXIMUMVALUEKNAPSACK problem) {
        SearchNodes = PrunedBranches = 0;
        if (timerHasExpired) return SolveRun.TimedOutAnswer;
        var selected = new List<int>();
        var candidates = new List<MaximumValueKnapsackItem>();
        long freeValue = 0;
        foreach (var item in problem.items) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer;
            if (item.Value == 0) continue;
            if (item.Weight == 0) {
                selected.Add(item.Index);
                freeValue = checked(freeValue + item.Value);
            } else if (item.Weight <= problem.capacity) {
                candidates.Add(item);
            }
        }
        MaximumValueKnapsackItem[] priority = candidates.ToArray();
        if (!SortByDensity(priority)) return SolveRun.TimedOutAnswer;

        var best = new List<int>(selected);
        long bestValue = freeValue;
        long remaining = problem.capacity;
        foreach (var item in priority) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer;
            if (item.Weight > remaining) continue;
            remaining -= item.Weight;
            bestValue = checked(bestValue + item.Value);
            best.Add(item.Index);
        }

        // Each pending node stores a prefix length and at most one new selection.
        // DFS leaves that prefix in the mutable path, so no per-node path copies
        // or recursion are needed. Push exclude first to explore include first.
        var pending = new Stack<(int Next, long Remaining, long Value, int PrefixCount, int AddIndex)>();
        pending.Push((0, problem.capacity, freeValue, selected.Count, 0));
        while (pending.Count > 0) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer;
            var node = pending.Pop();
            selected.RemoveRange(node.PrefixCount, selected.Count - node.PrefixCount);
            if (node.AddIndex != 0) selected.Add(node.AddIndex);
            if (SearchNodes < long.MaxValue) SearchNodes++;
            if (node.Value > bestValue) {
                bestValue = node.Value;
                best = new List<int>(selected);
            }
            BigInteger bound = UpperBound(priority, node.Next, node.Remaining, node.Value);
            if (timerHasExpired) return SolveRun.TimedOutAnswer;
            if (bound <= bestValue) {
                if (PrunedBranches < long.MaxValue) PrunedBranches++;
                continue;
            }
            if (node.Next == priority.Length) continue;
            var item = priority[node.Next];
            pending.Push((node.Next + 1, node.Remaining, node.Value, selected.Count, 0));
            if (item.Weight <= node.Remaining)
                pending.Push((node.Next + 1, node.Remaining - item.Weight,
                    checked(node.Value + item.Value), selected.Count, item.Index));
        }
        if (timerHasExpired) return SolveRun.TimedOutAnswer;
        string certificate = problem.FormatCertificate(best);
        return timerHasExpired ? SolveRun.TimedOutAnswer : certificate;
    }

    private BigInteger UpperBound(IReadOnlyList<MaximumValueKnapsackItem> priority, int start, long remaining, long value) {
        BigInteger bound = value;
        for (int i = start; i < priority.Count && remaining > 0; i++) {
            if (timerHasExpired) return BigInteger.Zero;
            var item = priority[i];
            if (item.Weight <= remaining) {
                remaining -= item.Weight;
                bound += item.Value;
            } else {
                // Relax the next item to a fraction. Casting BEFORE multiplication
                // avoids Int64 overflow. Integer objectives cannot exceed the floor
                // of the rational relaxation, so rounding down is safe and tighter.
                BigInteger numerator = (BigInteger)remaining * item.Value;
                bound += numerator / item.Weight;
                break;
            }
        }
        return bound;
    }

    private bool SortByDensity(MaximumValueKnapsackItem[] items) {
        // Bottom-up merge sort allows cooperative expiration checks during sorting,
        // without throwing from a comparison callback or using the call stack.
        var original = items;
        var buffer = new MaximumValueKnapsackItem[items.Length];
        for (long width = 1; width < items.Length; width *= 2) {
            for (long begin = 0; begin < items.Length; begin += 2 * width) {
                int left = (int)begin;
                int middle = (int)Math.Min(begin + width, items.Length);
                int right = middle;
                int end = (int)Math.Min(begin + 2 * width, items.Length);
                for (int output = left; output < end; output++) {
                    if (timerHasExpired) return false;
                    bool takeLeft = right == end || (left < middle && CompareDensity(items[left], items[right]) <= 0);
                    buffer[output] = takeLeft ? items[left++] : items[right++];
                }
            }
            (items, buffer) = (buffer, items);
        }
        // An odd number of passes leaves the result outside the caller's array.
        if (!ReferenceEquals(items, original)) {
            for (int i = 0; i < items.Length; i++) {
                if (timerHasExpired) return false;
                original[i] = items[i];
            }
        }
        return !timerHasExpired;
    }

    private static int CompareDensity(MaximumValueKnapsackItem a, MaximumValueKnapsackItem b) {
        int comparison = ((BigInteger)b.Value * a.Weight).CompareTo((BigInteger)a.Value * b.Weight);
        return comparison != 0 ? comparison : a.Index.CompareTo(b.Index);
    }
}
