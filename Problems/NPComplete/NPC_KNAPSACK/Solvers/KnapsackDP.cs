using API.Interfaces;
using API.Interfaces.Packing;
using API.Interfaces.Steps;
using SPADE;
using System;
using System.Collections.Generic;
using System.Linq;

namespace API.Problems.NPComplete.NPC_KNAPSACK.Solvers;

// The steps Knapsack Dynamic Programming records, for the Packing picture (item i is the instance's i-th item; the one
// container is the knapsack):
//   - an Update per row of the table, its item in focus and nothing taken yet, saying whether taking the item raises the
//     best value within W;
//   - the trace-back: an Accept per item it takes, saying why the table says to take it;
//   - the Done, with the best value against V.
// The fill has no verdicts (every row is simply worked out), so it is Updates; the trace-back keeps items for the answer,
// so it is Accepts. Incremental: the table is filled first, then the trace-back takes the answer's items one at a time.
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

    // How the steps show the table's work, given by the position of the item a step is about. taken holds the items the
    // trace-back has taken so far (0: in the knapsack, -1: not), so it is empty while the table is filled.
    private static RunSteps<int, Placement> NewSteps(StepRecorder<Placement> rec, int[] taken) => new(rec,
        // What every picture shows: the items taken so far, and the step's item.
        partial: _ => new Placement(taken),
        focus: item => [PackingFrames.ItemId(item)]);

    public string solve(KNAPSACK knapsack) => Solve(knapsack, StepRecorder<Placement>.Off);

    // dp[i, w] is the best value of the first i items within weight w: either item i is left out (dp[i - 1, w]) or it is
    // taken (its value plus dp[i - 1, w - weight]). The best value within W is the corner dp[n, W]; the trace-back walks
    // the rows back up to find the items that make it.
    public string Solve(KNAPSACK knapsack, StepRecorder<Placement> rec) {
        List<UtilCollection> itemValues = knapsack.items.ToList();
        int n = itemValues.Count;
        int capacity = knapsack.W;
        // The trace-back's items so far, by position: 0 (in the knapsack) or -1.
        int[] taken = Enumerable.Repeat(-1, n).ToArray();
        var steps = NewSteps(rec, taken);

        int[,] dp = new int[n + 1, capacity + 1];

        for (int i = 1; i <= n; i++) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer;

            int weight = int.Parse(itemValues[i - 1][0].ToString());
            int value = int.Parse(itemValues[i - 1][1].ToString());

            for (int w = 0; w <= capacity; w++) {
                if (weight <= w)
                    dp[i, w] = Math.Max(dp[i - 1, w], dp[i - 1, w - weight] + value);
                else
                    dp[i, w] = dp[i - 1, w];
            }
            if (dp[i, capacity] != dp[i - 1, capacity])
                steps.Update(i - 1, $"In row {i} of the table, taking item {PackingFrames.Key(itemValues[i - 1])} raises the best value within {capacity} to {dp[i, capacity]}.");
            else
                steps.Update(i - 1, $"In row {i} of the table, item {PackingFrames.Key(itemValues[i - 1])} doesn't raise the best value within {capacity} above {dp[i, capacity]}.");
        }

        if (dp[n, capacity] < knapsack.V) {
            steps.Done(new Placement([]), false, $"The table's best value within {capacity} is {dp[n, capacity]}, short of {knapsack.V}.");
            return "{}";
        }

        // A list, not a SPADE set, so two identical items the table took are both in the answer.
        List<string> selectedItems = new List<string>();
        int remainingCapacity = capacity;

        // Every row, even once the capacity is used up: a zero-weight item the table took still has to be picked up.
        // With no room left, an item that weighs something never changes dp[i, 0], so it is not taken.
        for (int i = n; i > 0; i--) {
            if (dp[i, remainingCapacity] != dp[i - 1, remainingCapacity]) {
                var item = itemValues[i - 1];
                selectedItems.Add(item.ToString());

                int itemWeight = int.Parse(item[0].ToString());
                // Row i changes the best value within the room left, so the table took item i.
                int before = remainingCapacity;
                remainingCapacity -= itemWeight;
                taken[i - 1] = 0;
                steps.Accept(i - 1, $"Trace back: take {PackingFrames.Key(item)}. Without it the best value within {before} would be {dp[i - 1, before]}, not {dp[i, before]}. {remainingCapacity} of {capacity} left.");
            }
        }

        string answer = "{" + string.Join(",", selectedItems) + "}";
        // The Done says what the verifier says of the answer; a plain solve skips asking.
        if (rec.Enabled) {
            bool ok = knapsack.defaultVerifier.verify(knapsack, answer);
            if (taken.All(t => t < 0))
                steps.Done(new Placement(taken), ok, $"The table's best value within {capacity} is {dp[n, capacity]}, at least {knapsack.V}, and the empty set is enough.");
            else
                steps.Done(new Placement(taken), ok, $"The table's best value within {capacity} is {dp[n, capacity]}, at least {knapsack.V}. The items taken weigh {capacity - remainingCapacity}.");
        }
        return answer;
    }
}
