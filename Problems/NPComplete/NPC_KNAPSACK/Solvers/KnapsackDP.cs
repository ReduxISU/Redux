using API.Interfaces;
using SPADE;
using System;
using System.Collections.Generic;
using System.Linq;

namespace API.Problems.NPComplete.NPC_KNAPSACK.Solvers;

class KnapsackDP : ISolver<KNAPSACK> {
        public string solverName { get; } = "Knapsack DP Solver";
        public string solverDefinition { get; } = "A pseudo-polynomial time solver for the 0-1 Knapsack decision problem using bottom-up dynamic programming. Builds an (N+1) x (W+1) table where dp[i,w] is the maximum value achievable using the first i items within capacity w, then backtracks through the table to recover the chosen subset. Runs in O(N*W) time and space.";
        public string source { get; } = "Bellman, Richard. Dynamic Programming. Princeton University Press, 1957.";
        public string sourceLink { get; } = "https://en.wikipedia.org/wiki/Knapsack_problem#0/1_knapsack_problem";
        public string[] contributors { get; } = { "Musab Khan", "Calvin Condie" };
        public bool timerHasExpired { get; set; }
        // Declared, not derived. Memo table + optimal-substructure recurrence. Bucket stays Polynomial by
        // convention even though O(n * W) is pseudo-polynomial -- polynomial in the *value* of W,
        // exponential in W's *bit-length*; the existing complexity string above already carries that
        // nuance.
        public SolverType solverType { get; } = SolverType.DynamicProgramming;
        public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
        public string complexity { get; } = "O(n * W)";

        public string solve(KNAPSACK knapsack) {
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
                }

                UtilCollection selectedItems = new UtilCollection("{}");
                int remainingCapacity = capacity;

                for (int i = n; i > 0 && remainingCapacity > 0; i--) {
                        if (dp[i, remainingCapacity] != dp[i - 1, remainingCapacity]) {
                                var item = itemValues[i - 1];
                                selectedItems.Add(item);

                                int itemWeight = int.Parse(item[0].ToString());
                                remainingCapacity -= itemWeight;
                        }
                }

                return selectedItems.ToString();
        }
}
