using System;
using System.Collections.Generic;
using API.Interfaces;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGEM;
using SPADE;

namespace API.Problems.NPHard.NPH_PUMPSCHEDULINGEM.Solvers;

class PumpSchedulingEMSolver : ISolver<PUMPSCHEDULINGEM>
{
    public string solverName { get; } = "Pump Scheduling Emergency Resilience — DAG Constrained Longest Path";
    public string solverDefinition { get; } =
        "Models the 24-hour pump scheduling problem as a DAG where each node represents\r\n" +
        "(hour, tank-level-bucket, previous-pump-mask). For emergency resilience, a forward\r\n" +
        "pass computes the maximum-cumulative-water-stored path subject to total cost\r\n" +
        "remaining within the effective budget. If the instance budget is 0, the effective\r\n" +
        "budget is auto-computed as 1.5× the cost-minimization optimum via a preliminary\r\n" +
        "shortest-path solve. The optimal schedule is recovered by backtracking.\r\n" +
        "Certificate format: (effectiveBudget, totalCost, schedule) where schedule is\r\n" +
        "((PumpName,h0,...,h23),...) with binary per-hour on/off values.";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Michael Trosper" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Memo table + optimal-substructure recurrence.
    public SolverType solverType { get; } = SolverType.DynamicProgramming;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
    // Declared, not derived. Same pseudo-polynomial nuance as PumpSchedulingCMSolver: the
    // optional cost-minimization pre-solve and the emergency-resilience solve each run the
    // same H x B x nMasks x nMasks DP transition (nMasks = 2^n pump-on/off states), so the
    // two sequential passes don't change the asymptotic bound.
    public string complexity { get; } = "O(H * B * n * 4^n), H = 24 hours, B = tank buckets, n = number of pumps";

    public List<object> GetSteps(PUMPSCHEDULINGEM _) => [true];

    private const int Hours     = 24;
    private const double BudgetSlack = 1.5;
    private const double Inf    = double.PositiveInfinity;
    private const double NegInf = double.NegativeInfinity;

    public string solve(PUMPSCHEDULINGEM problem)
    {
        double effectiveBudget = problem.BudgetLimitDollars;

        if (effectiveBudget <= 0)
        {
            // Auto-compute: run cost-minimization solve first.
            double minCost = RunCostMinimization(problem);
            effectiveBudget = minCost * BudgetSlack;
        }

        int[]? schedMasks = RunEmergencyResilience(problem, effectiveBudget);

        // Fallback: no feasible EM path — use all pumps every hour.
        if (schedMasks == null)
        {
            schedMasks = new int[Hours];
            int fullMask = (1 << problem.Pumps.Count) - 1;
            for (int h = 0; h < Hours; h++)
                schedMasks[h] = fullMask;
        }

        double totalCost = ComputeTotalCost(problem, schedMasks);

        return BuildCertificate(problem, schedMasks,
            Math.Round(effectiveBudget, 2),
            Math.Round(totalCost, 2));
    }

    // ── Cost-minimization sub-solve (shortest path) — returns total cost ──────
    private double RunCostMinimization(PUMPSCHEDULINGEM problem)
    {
        int n = problem.Pumps.Count;
        int nMasks = 1 << n;
        double cap = problem.TankCapacity;
        double minLevel = problem.TankMinLevel;
        int buckets = (int)Math.Ceiling(cap);
        int stateB = buckets + 1;
        double bucketSize = 1.0;

        int ToBucket(double level) => Math.Clamp((int)Math.Round(level / bucketSize), 0, buckets);
        double ToLevel(int b) => b * bucketSize;

        double[] maskFlow = BuildMaskFlow(problem, n, nMasks);

        double[,] dp = new double[stateB, nMasks];
        for (int b = 0; b < stateB; b++)
            for (int m = 0; m < nMasks; m++)
                dp[b, m] = Inf;

        int initB = ToBucket(problem.TankCurrentLevel);
        dp[initB, 0] = 0.0;

        for (int h = 0; h < Hours; h++)
        {
            if (timerHasExpired) return 0.0;

            double[,] next = new double[stateB, nMasks];
            for (int b = 0; b < stateB; b++)
                for (int m = 0; m < nMasks; m++)
                    next[b, m] = Inf;

            double demand = problem.DemandGph[h];

            for (int b = 0; b < stateB; b++)
            {
                double levelNow = ToLevel(b);
                for (int prevMask = 0; prevMask < nMasks; prevMask++)
                {
                    double cur = dp[b, prevMask];
                    if (cur >= Inf) continue;

                    for (int mask = 0; mask < nMasks; mask++)
                    {
                        double newLevel = levelNow - demand + maskFlow[mask];
                        if (newLevel < minLevel || newLevel > cap) continue;

                        double edgeCost = EdgeCost(problem, prevMask, mask, h, n, nMasks);
                        double candidate = cur + edgeCost;
                        int newB = ToBucket(newLevel);

                        if (candidate < next[newB, mask])
                            next[newB, mask] = candidate;
                    }
                }
            }

            dp = next;
        }

        double minCost = Inf;
        for (int b = 0; b < stateB; b++)
            for (int m = 0; m < nMasks; m++)
                if (dp[b, m] < minCost)
                    minCost = dp[b, m];

        return minCost >= Inf ? 0.0 : minCost;
    }

    // ── Emergency resilience solve (constrained longest path) ─────────────────
    private int[]? RunEmergencyResilience(PUMPSCHEDULINGEM problem, double budgetLimit)
    {
        int n = problem.Pumps.Count;
        int nMasks = 1 << n;
        double cap = problem.TankCapacity;
        double minLevel = problem.TankMinLevel;
        int buckets = (int)Math.Ceiling(cap);
        int stateB = buckets + 1;
        double bucketSize = 1.0;

        int ToBucket(double level) => Math.Clamp((int)Math.Round(level / bucketSize), 0, buckets);
        double ToLevel(int b) => b * bucketSize;

        double[] maskFlow = BuildMaskFlow(problem, n, nMasks);

        // dp_cost[b, m]  = cumulative cost of the best path to (b, m)
        //                  "best" = highest score path still within budget
        // dp_score[b, m] = cumulative water stored along that path
        double[,] dpCost  = new double[stateB, nMasks];
        double[,] dpScore = new double[stateB, nMasks];

        // parent arrays indexed by hour (after transition), to allow backtracking
        int[,,] parentB = new int[Hours + 1, stateB, nMasks];
        int[,,] parentM = new int[Hours + 1, stateB, nMasks];

        for (int b = 0; b < stateB; b++)
            for (int m = 0; m < nMasks; m++)
            {
                dpCost[b, m]  = Inf;
                dpScore[b, m] = NegInf;
            }
        for (int h = 0; h <= Hours; h++)
            for (int b = 0; b < stateB; b++)
                for (int m = 0; m < nMasks; m++)
                {
                    parentB[h, b, m] = -1;
                    parentM[h, b, m] = -1;
                }

        int initB = ToBucket(problem.TankCurrentLevel);
        dpCost[initB, 0]  = 0.0;
        dpScore[initB, 0] = 0.0;

        for (int h = 0; h < Hours; h++)
        {
            if (timerHasExpired) return null;

            double[,] nextCost  = new double[stateB, nMasks];
            double[,] nextScore = new double[stateB, nMasks];
            for (int b = 0; b < stateB; b++)
                for (int m = 0; m < nMasks; m++)
                {
                    nextCost[b, m]  = Inf;
                    nextScore[b, m] = NegInf;
                }

            double demand = problem.DemandGph[h];

            for (int b = 0; b < stateB; b++)
            {
                double levelNow = ToLevel(b);
                for (int prevMask = 0; prevMask < nMasks; prevMask++)
                {
                    double curCost = dpCost[b, prevMask];
                    if (curCost >= Inf) continue;
                    double curScore = dpScore[b, prevMask];

                    for (int mask = 0; mask < nMasks; mask++)
                    {
                        double newLevel = levelNow - demand + maskFlow[mask];
                        if (newLevel < minLevel || newLevel > cap) continue;

                        double edgeCost  = EdgeCost(problem, prevMask, mask, h, n, nMasks);
                        double totalCost = curCost + edgeCost;

                        // Prune paths that exceed the budget.
                        if (totalCost > budgetLimit) continue;

                        double edgeScore  = newLevel; // water stored after this hour
                        double totalScore = curScore + edgeScore;

                        int newB = ToBucket(newLevel);

                        // Keep the path with the highest cumulative water score.
                        if (totalScore > nextScore[newB, mask])
                        {
                            nextCost[newB, mask]         = totalCost;
                            nextScore[newB, mask]        = totalScore;
                            parentB[h + 1, newB, mask]  = b;
                            parentM[h + 1, newB, mask]  = prevMask;
                        }
                    }
                }
            }

            dpCost  = nextCost;
            dpScore = nextScore;
        }

        // Pick the terminal state with the highest cumulative water score.
        double bestScore = NegInf;
        int bestB = -1, bestM = -1;
        for (int b = 0; b < stateB; b++)
            for (int m = 0; m < nMasks; m++)
                if (dpCost[b, m] < Inf && dpScore[b, m] > bestScore)
                {
                    bestScore = dpScore[b, m];
                    bestB = b;
                    bestM = m;
                }

        if (bestB == -1) return null;

        // Backtrack to recover the per-hour masks.
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

        return schedMasks;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static double[] BuildMaskFlow(PUMPSCHEDULINGEM problem, int n, int nMasks)
    {
        double[] maskFlow = new double[nMasks];
        for (int mask = 0; mask < nMasks; mask++)
            for (int p = 0; p < n; p++)
                if ((mask & (1 << p)) != 0)
                    maskFlow[mask] += problem.Pumps[p].FlowRateGph;
        return maskFlow;
    }

    private static double EdgeCost(PUMPSCHEDULINGEM problem, int prevMask, int mask, int h, int n, int nMasks)
    {
        double rate = problem.PeakHours.Contains(h)
            ? problem.OnPeakCostPerKwh
            : problem.OffPeakCostPerKwh;

        double kw = 0;
        for (int p = 0; p < n; p++)
            if ((mask & (1 << p)) != 0)
                kw += problem.Pumps[p].PowerKw;

        double energyCost = kw * rate;

        double startupCost = 0;
        int startups = (~prevMask) & mask & (nMasks - 1);
        for (int p = 0; p < n; p++)
            if ((startups & (1 << p)) != 0)
                startupCost += problem.Pumps[p].StartupCostDollars;

        return energyCost + startupCost;
    }

    internal static double ComputeTotalCost(PUMPSCHEDULINGEM problem, int[] schedMasks)
    {
        int n = problem.Pumps.Count;
        int nMasks = 1 << n;
        double totalCost = 0.0;
        int prevMask = 0;

        for (int h = 0; h < Hours; h++)
        {
            int mask = schedMasks[h];
            totalCost += EdgeCost(problem, prevMask, mask, h, n, nMasks);
            prevMask = mask;
        }

        return totalCost;
    }

    private static string BuildCertificate(PUMPSCHEDULINGEM problem, int[] schedMasks,
        double effectiveBudget, double totalCost)
    {
        string fmt = System.Globalization.CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator == "."
            ? "F2"
            : "F2";

        UtilCollection cert = new("()");
        cert.Add(new UtilCollection(
            effectiveBudget.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)));
        cert.Add(new UtilCollection(
            totalCost.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)));

        int n = problem.Pumps.Count;
        UtilCollection sched = new("()");
        for (int p = 0; p < n; p++)
        {
            UtilCollection pumpSched = new("()");
            pumpSched.Add(new UtilCollection(problem.Pumps[p].Name));
            for (int h = 0; h < Hours; h++)
                pumpSched.Add(new UtilCollection((schedMasks[h] & (1 << p)) != 0 ? "1" : "0"));
            sched.Add(pumpSched);
        }
        cert.Add(sched);

        return cert.ToString();
    }
}
