using System.Numerics;
using API.Interfaces;

namespace API.Problems.NPHard.NPH_MAXIMUMVALUEKNAPSACK.Solvers;

internal sealed class MaximumValueKnapsackModifiedGreedySolver : ISolver<MAXIMUMVALUEKNAPSACK> {
    public string solverName { get; } = "Maximum-Value 0-1 Knapsack Modified Greedy Approximation";
    public string solverDefinition { get; } = "A deterministic 1/2-approximation: ALG >= OPT / 2 for nonnegative weights and values. Includes all positive-value zero-weight items, omits zero-value and individually overweight items, and sorts the rest by exact decreasing value/weight density with original-index ties. Takes the better of the density prefix (stopping at its first non-fitting item) and the best feasible singleton, adding the free items to either candidate. Prefix wins equal-value ties; singleton ties use the lowest original index. Completed results are feasible but not necessarily optimal. No 20-item limit.";
    public string source { get; } = "Modified density greedy with best feasible singleton, using the fractional-knapsack relaxation to prove a 1/2 approximation.";
    public string sourceLink { get; } = "https://www.cs.cornell.edu/courses/cs4820/2019fa/lectures/approx-knapsack.pdf";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Corbin Hay" };
    public SolverType solverType { get; } = SolverType.Greedy | SolverType.Approximation;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
    public MathematicalFunction timeComplexity { get; } = new("|i| * log(|i|)", ", |i| = number of items, duplicates counted; O(|i| log |i|) time under the fixed-width arithmetic model, including certificate formatting; densities use BigInteger products of Int64 inputs");
    public MathematicalFunction spaceComplexity { get; } = new("|i|", ", |i| = number of items, duplicates counted; candidates, merge buffer, and selected indices");
    private volatile bool _timerHasExpired;
    public bool timerHasExpired { get => _timerHasExpired; set => _timerHasExpired = value; }

    public string solve(MAXIMUMVALUEKNAPSACK problem) {
        if (timerHasExpired) return SolveRun.TimedOutAnswer;
        var selected = new List<int>();
        var candidates = new List<MaximumValueKnapsackItem>();
        MaximumValueKnapsackItem? singleton = null;
        foreach (var item in problem.items) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer;
            if (item.Value == 0) continue;
            if (item.Weight == 0) {
                selected.Add(item.Index);
            } else if (item.Weight <= problem.capacity) {
                candidates.Add(item);
                if (singleton is null || item.Value > singleton.Value
                    || (item.Value == singleton.Value && item.Index < singleton.Index))
                    singleton = item;
            }
        }
        var priority = candidates.ToArray();
        if (!SortByDensity(priority)) return SolveRun.TimedOutAnswer;
        var prefix = new List<int>();
        long remaining = problem.capacity;
        long prefixValue = 0;
        foreach (var item in priority) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer;
            if (item.Weight > remaining) break;
            remaining -= item.Weight;
            prefixValue = checked(prefixValue + item.Value);
            prefix.Add(item.Index);
        }

        // For the positive-weight residual problem, fractional OPT is at most
        // prefixValue plus the first excluded item's value, which is at most the
        // best singleton value. Thus max(prefix,singleton) >= residual OPT / 2.
        // Adding the same nonnegative free value F to both candidates preserves
        // F + ALG_residual >= (F + OPT_residual) / 2.
        if (timerHasExpired) return SolveRun.TimedOutAnswer;
        if (singleton is not null && singleton.Value > prefixValue) {
            selected.Add(singleton.Index);
        } else {
            foreach (int index in prefix) {
                if (timerHasExpired) return SolveRun.TimedOutAnswer;
                selected.Add(index);
            }
        }
        if (timerHasExpired) return SolveRun.TimedOutAnswer;
        string certificate = problem.FormatCertificate(selected);
        return timerHasExpired ? SolveRun.TimedOutAnswer : certificate;
    }

    private bool SortByDensity(MaximumValueKnapsackItem[] items) {
        // Local bottom-up merge sort follows B4's pattern, allowing expiration
        // checks throughout sorting without changing the existing exact solver.
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
