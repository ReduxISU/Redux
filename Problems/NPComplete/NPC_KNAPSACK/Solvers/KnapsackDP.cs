using API.Interfaces;
using API.Interfaces.Packing;
using API.Interfaces.Steps;
using SPADE;
using System;
using System.Collections.Generic;
using System.Linq;

namespace API.Problems.NPComplete.NPC_KNAPSACK.Solvers;

// Incremental: the table is filled first (one step per row, nothing taken yet), then the trace-back takes the answer's
// items one at a time. The table itself is drawn by the Table picture (#671); the Packing picture shows the rows' items and
// the items the trace-back commits.
class KnapsackDP : ISolver<KNAPSACK, Placement>, IIncrementalSolver {
    public string solverName { get; } = "Knapsack Dynamic Programming";
    public string solverDefinition { get; } = "A pseudo-polynomial time solver for the 0-1 Knapsack decision problem using bottom-up dynamic programming. Builds an (N+1) x (W+1) table where dp[i,w] is the maximum value achievable using the first i items within capacity w, then backtracks through the table to recover the chosen subset. Runs in O(N*W) time and space.";
    public string source { get; } = "Bellman, Richard. Dynamic Programming. Princeton University Press, 1957.";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://en.wikipedia.org/wiki/Knapsack_problem#0/1_knapsack_problem";
    public string[] contributors { get; } = { "Musab Khan", "Calvin Condie" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Memo table + optimal-substructure recurrence. Bucket stays Polynomial by
    // convention even though O(n * W) is pseudo-polynomial -- polynomial in the *value* of W,
    // exponential in W's *bit-length*; the existing complexity string above already carries that
    // nuance.
    public SolverType solverType { get; } = SolverType.DynamicProgramming;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
    public MathematicalFunction timeComplexity { get; } = new("n * W", "");

    public string solve(KNAPSACK knapsack) => Solve(knapsack, StepRecorder<Placement>.Off);

    // Steps: a Try per row of the table (its item in focus, nothing taken yet), then an Accept per item the trace-back takes,
    // then the answer.
    public string Solve(KNAPSACK knapsack, StepRecorder<Placement> rec) {
        List<UtilCollection> itemValues = knapsack.items.ToList();
        int n = itemValues.Count;
        int capacity = knapsack.W;

        int[,] dp = new int[n + 1, capacity + 1];

        for (int i = 1; i <= n; i++) {
            if (timerHasExpired) return "{}";

            int weight = int.Parse(itemValues[i - 1][0].ToString());
            int value = int.Parse(itemValues[i - 1][1].ToString());

            for (int w = 0; w <= capacity; w++) {
                if (weight <= w)
                    dp[i, w] = Math.Max(dp[i - 1, w], dp[i - 1, w - weight] + value);
                else
                    dp[i, w] = dp[i - 1, w];
            }
            int row = i;
            rec.Try(() => new Placement([]), () => dp[row, capacity] != dp[row - 1, capacity]
                ? $"Row {row} of the table, item {PackingFrames.Key(itemValues[row - 1])}: taking it raises the best value within {capacity} to {dp[row, capacity]}."
                : $"Row {row} of the table, item {PackingFrames.Key(itemValues[row - 1])}: it doesn't raise the best value within {capacity} above {dp[row, capacity]}.",
                PackingFrames.ItemId(i - 1));
        }

        if (dp[n, capacity] < knapsack.V) {
            rec.Done(new Placement([]), false, $"The table's best value within {capacity} is {dp[n, capacity]}, short of {knapsack.V}.");
            return "{}";
        }

        UtilCollection selectedItems = new UtilCollection("{}");
        int remainingCapacity = capacity;
        // The trace-back's items so far, by position: 0 (in the knapsack) or -1.
        int[] taken = Enumerable.Repeat(-1, n).ToArray();

        // Every row, even once the capacity is used up: a zero-weight item the table took still has to be picked up (#795).
        // With no room left, an item that weighs something never changes dp[i, 0], so it is not taken.
        for (int i = n; i > 0; i--) {
            if (dp[i, remainingCapacity] != dp[i - 1, remainingCapacity]) {
                var item = itemValues[i - 1];
                selectedItems.Add(item);

                int itemWeight = int.Parse(item[0].ToString());
                int before = remainingCapacity, at = i;
                remainingCapacity -= itemWeight;
                taken[i - 1] = 0;
                int left = remainingCapacity;
                rec.Accept(() => new Placement(taken), () => $"Trace back: take {PackingFrames.Key(item)}. Without it the best value within {before} "
                    + $"would be {dp[at - 1, before]}, not {dp[at, before]}. {left} of {capacity} left.", PackingFrames.ItemId(i - 1));
            }
        }

        string answer = selectedItems.ToString();
        if (rec != StepRecorder<Placement>.Off) {
            bool ok = knapsack.defaultVerifier.verify(knapsack, answer);
            int weightTaken = capacity - remainingCapacity;
            rec.Done(new Placement(taken), ok, taken.All(t => t < 0)
                ? $"The table's best value within {capacity} is {dp[n, capacity]}, at least {knapsack.V}, and the empty set is enough."
                : $"The table's best value within {capacity} is {dp[n, capacity]}, at least {knapsack.V}. The items taken weigh {weightTaken}.");
        }
        return answer;
    }
}
