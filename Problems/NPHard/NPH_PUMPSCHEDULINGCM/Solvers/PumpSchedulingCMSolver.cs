using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using API.Interfaces;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGCM;

namespace API.Problems.NPHard.NPH_PUMPSCHEDULINGCM.Solvers;

class PumpSchedulingCMSolver : ISolver<PUMPSCHEDULINGCM>
{
    // --- Fields ---
    public string solverName { get; } = "Pump Scheduling CM — DAG Dynamic Programming";
    public string solverDefinition { get; } =
        "Models the 24-hour pump scheduling problem as a DAG where each node represents " +
        "(hour, tank-level-bucket, previous-pump-mask). A forward pass computes the minimum-cost " +
        "path from the initial state to any valid end state. The optimal schedule is recovered " +
        "by backtracking through parent pointers.";
    public string source { get; } = "";
    public string[] contributors { get; } = { "SARE 2026 Team" };
    public bool timerHasExpired { get; set; }

    private const int Hours = 24;
    private const int Buckets = 1000; // tank discretization resolution
    private const double Inf = double.MaxValue / 2;

    public string solve(PUMPSCHEDULINGCM problem)
    {
        int n = problem.Pumps.Count;
        int nMasks = 1 << n;
        double cap = problem.TankCapacity;
        double bucketSize = cap / Buckets;

        // Converts a continuous tank level to the nearest bucket index [0, Buckets].
        int ToBucket(double level) =>
            Math.Clamp((int)Math.Round(level / bucketSize), 0, Buckets);

        double ToLevel(int b) => b * bucketSize;

        // Precompute total flow (gph) for each pump bitmask.
        double[] maskFlow = new double[nMasks];
        for (int mask = 0; mask < nMasks; mask++)
            for (int p = 0; p < n; p++)
                if ((mask & (1 << p)) != 0)
                    maskFlow[mask] += problem.Pumps[p].FlowRateGph;

        // Energy cost (dollars) of running pump mask for one hour at hour h.
        double EnergyCost(int mask, int h)
        {
            double rate = problem.PeakHours.Contains(h)
                ? problem.OnPeakCostPerKwh
                : problem.OffPeakCostPerKwh;
            double kw = 0;
            for (int p = 0; p < n; p++)
                if ((mask & (1 << p)) != 0)
                    kw += problem.Pumps[p].PowerKw;
            return kw * rate;
        }

        // Startup cost (dollars) for pumps that switch from off to on.
        double StartupCost(int prevMask, int currMask)
        {
            double cost = 0;
            int startups = (~prevMask) & currMask & (nMasks - 1);
            for (int p = 0; p < n; p++)
                if ((startups & (1 << p)) != 0)
                    cost += problem.Pumps[p].StartupCostDollars;
            return cost;
        }

        // DP state: dp[h, bucket, prevMask] = min cost to start hour h at that bucket
        //           with prevMask being the pump action used during hour h-1.
        int stateB = Buckets + 1;
        double[,,] dp       = new double[Hours + 1, stateB, nMasks];
        int[,,]    parentB  = new int   [Hours + 1, stateB, nMasks];
        int[,,]    parentM  = new int   [Hours + 1, stateB, nMasks];

        for (int h = 0; h <= Hours; h++)
            for (int b = 0; b < stateB; b++)
                for (int m = 0; m < nMasks; m++)
                {
                    dp[h, b, m] = Inf;
                    parentB[h, b, m] = -1;
                    parentM[h, b, m] = -1;
                }

        // Initial state: hour 0, initial tank bucket, no pumps previously running.
        int initB = ToBucket(problem.TankCurrentLevel);
        dp[0, initB, 0] = 0.0;

        // Forward pass — propagate cheapest cost hour by hour.
        for (int h = 0; h < Hours; h++)
        {
            if (timerHasExpired) return "{}";

            double demand = problem.DemandGph[h];

            for (int b = 0; b < stateB; b++)
            {
                double levelNow = ToLevel(b);

                for (int prevMask = 0; prevMask < nMasks; prevMask++)
                {
                    double stateCost = dp[h, b, prevMask];
                    if (stateCost >= Inf) continue;

                    for (int mask = 0; mask < nMasks; mask++)
                    {
                        double newLevel = levelNow - demand + maskFlow[mask];
                        if (newLevel < 0 || newLevel > cap) continue;

                        int newB = ToBucket(newLevel);
                        double stepCost = EnergyCost(mask, h) + StartupCost(prevMask, mask);
                        double candidate = stateCost + stepCost;

                        if (candidate < dp[h + 1, newB, mask])
                        {
                            dp[h + 1, newB, mask] = candidate;
                            parentB[h + 1, newB, mask] = b;
                            parentM[h + 1, newB, mask] = prevMask;
                        }
                    }
                }
            }
        }

        // Find the minimum-cost reachable final state.
        double minCost = Inf;
        int bestB = -1, bestM = -1;
        for (int b = 0; b < stateB; b++)
            for (int m = 0; m < nMasks; m++)
                if (dp[Hours, b, m] < minCost)
                {
                    minCost = dp[Hours, b, m];
                    bestB = b;
                    bestM = m;
                }

        if (bestB == -1) return "{}"; // No feasible schedule exists.

        // Backtrack to recover the per-hour pump mask schedule.
        // At state (h+1, b, m): m is the pump action applied during hour h.
        int[] schedMasks = new int[Hours];
        int curB = bestB, curM = bestM;
        for (int h = Hours; h > 0; h--)
        {
            schedMasks[h - 1] = curM;
            int pb = parentB[h, curB, curM];
            int pm = parentM[h, curB, curM];
            curB = pb;
            curM = pm;
        }

        // Build certificate: (cost,{(PumpA,0,1,...),(PumpB,1,0,...)})
        var sb = new StringBuilder("(");
        sb.Append(Math.Round(minCost, 2).ToString(System.Globalization.CultureInfo.InvariantCulture));
        sb.Append(",{");
        for (int p = 0; p < n; p++)
        {
            if (p > 0) sb.Append(',');
            sb.Append('(');
            sb.Append(problem.Pumps[p].Name);
            for (int h = 0; h < Hours; h++)
            {
                sb.Append(',');
                sb.Append((schedMasks[h] & (1 << p)) != 0 ? 1 : 0);
            }
            sb.Append(')');
        }
        sb.Append("})");
        return sb.ToString();
    }
}
