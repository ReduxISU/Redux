using API.Interfaces;
using API.Interfaces.JSON_Objects;
using SPADE;
using System;
using System.Collections.Generic;
using System.Linq;

namespace API.Problems.NPComplete.NPC_KNAPSACK.Solvers;

class KnapsackDP : ISolver<KNAPSACK>
{
    public string solverName { get; } = "Knapsack DP Solver";
    public string solverDefinition { get; } = "A pseudo-polynomial time solver using dynamic programming (tabulation).";
    public string source { get; } = "https://en.wikipedia.org/wiki/Knapsack_problem#0/1_knapsack_problem";
    public string[] contributors { get; } = { "Your Name" };
    public bool timerHasExpired { get; set; }
    public string complexity { get; } = "O(n * W)";

    public string solve(KNAPSACK knapsack)
    {
        // In your class, 'items' is a UtilCollection and 'W' is the capacity
        List<UtilCollection> itemValues = knapsack.items.ToList();
        int n = itemValues.Count;
        int capacity = knapsack.W; 

        int[,] dp = new int[n + 1, capacity + 1];

        // 1. Build the DP Table
        for (int i = 1; i <= n; i++)
        {
            // Accessing elements from the parsed tuple (weight, value)
            // Based on your defaultInstance "{(10,60)...}", index 0 is weight, index 1 is value
            int weight = int.Parse(itemValues[i - 1][0].ToString());
            int value = int.Parse(itemValues[i - 1][1].ToString());

            for (int w = 0; w <= capacity; w++)
            {
                if (weight <= w)
                    dp[i, w] = Math.Max(dp[i - 1, w], dp[i - 1, w - weight] + value);
                else
                    dp[i, w] = dp[i - 1, w];
            }
        }

        // 2. Backtrack to find the selected items
        UtilCollection selectedItems = new UtilCollection("{}");
        int remainingCapacity = capacity;

        for (int i = n; i > 0 && remainingCapacity > 0; i--)
        {
            if (dp[i, remainingCapacity] != dp[i - 1, remainingCapacity])
            {
                var item = itemValues[i - 1];
                selectedItems.Add(item);
                
                int itemWeight = int.Parse(item[0].ToString());
                remainingCapacity -= itemWeight;
            }
        }

        return selectedItems.ToString();
    }
}