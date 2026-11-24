using API.Interfaces;

namespace API.Problems.NPHard.NPH_MAXIMUMVALUEKNAPSACK.Solvers;

[DefaultFor(typeof(MAXIMUMVALUEKNAPSACK))]
internal sealed class MaximumValueKnapsackBruteForceSolver : ISolver<MAXIMUMVALUEKNAPSACK> {
    public string solverName { get; } = "Maximum-Value 0-1 Knapsack Exact Brute Force";
    public string solverDefinition { get; } = "Enumerates every item subset, including empty, and returns an optimal feasible selection only when enumeration completes. Supports at most 20 item occurrences. On ties, retains the first subset in ascending bit-mask order; output indices are ascending. An interrupted search returns no incumbent certificate.";
    public string source { get; } = "Exhaustive subset enumeration for 0-1 knapsack.";
    public string sourceLink { get; } = "https://doi.org/10.1007/978-3-540-24777-7";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Corbin Hay" };
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    public MathematicalFunction timeComplexity { get; } = new("n * 2^n", ", n = item occurrences; exhaustive subset feasibility and value evaluation");
    public MathematicalFunction spaceComplexity { get; } = new("n", ", n = item occurrences; current and best selections and evaluation bookkeeping");
    private volatile bool _timerHasExpired;
    public bool timerHasExpired { get => _timerHasExpired; set => _timerHasExpired = value; }

    internal const int MaxItems = 20;

    internal static void ThrowIfSearchLimitExceeded(int count) {
        if (count > MaxItems)
            throw new ExactSearchLimitExceededException("Maximum-Value 0-1 Knapsack", count, MaxItems);
    }

    public string solve(MAXIMUMVALUEKNAPSACK problem) {
        ThrowIfSearchLimitExceeded(problem.items.Count);
        if (timerHasExpired) return SolveRun.TimedOutAnswer;
        var best = new List<int>();
        long bestValue = 0;
        var selected = new List<int>(problem.items.Count);
        // The size guard makes the shift safe. Mask 0 evaluates the empty subset.
        for (int mask = 0; mask < (1 << problem.items.Count); mask++) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer;
            selected.Clear();
            for (int item = 0; item < problem.items.Count; item++) {
                if (timerHasExpired) return SolveRun.TimedOutAnswer;
                if ((mask & (1 << item)) != 0) selected.Add(item + 1);
            }
            if (!problem.IsFeasible(selected)) continue;
            long value = problem.Value(selected);
            if (value > bestValue) {
                bestValue = value;
                best = new List<int>(selected);
            }
        }
        string certificate = problem.FormatCertificate(best);
        return timerHasExpired ? SolveRun.TimedOutAnswer : certificate;
    }
}
