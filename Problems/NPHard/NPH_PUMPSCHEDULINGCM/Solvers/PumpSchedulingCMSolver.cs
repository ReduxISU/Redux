using System;
using API.Interfaces;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGCM;
using SPADE;

namespace API.Problems.NPHard.NPH_PUMPSCHEDULINGCM.Solvers;

class PumpSchedulingCMSolver : ISolver<PUMPSCHEDULINGCM> {
    public string solverName { get; } = "Pump Scheduling Cost Minimization Dynamic Programming";
    public string solverDefinition { get; } =
        "Models the 24-hour pump scheduling problem as a DAG where each node represents " +
        "(hour, tank-level-bucket, previous-pump-mask). A forward pass computes the minimum-cost " +
        "path from the initial state to any valid end state. The optimal schedule is recovered " +
        "by backtracking through parent pointers.\r\n" +
        "The output certificate is a 2-section tuple: (total-cost, schedule) where total-cost is the " +
        "total cost of the pump schedule, and schedule is a list of pump schedules (one per pump) where " +
        "each pump schedule is a list of 24 binary values indicating whether that pump is on or off for each hour.";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Michael Trosper" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Memo table + optimal-substructure recurrence.
    public SolverType solverType { get; } = SolverType.DynamicProgramming;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
    // Declared, not derived. Pseudo-polynomial, same nuance as KnapsackDP's "O(n * W)":
    // polynomial in the hour count H and tank-bucket count B, but the per-hour transition
    // enumerates prevMask x mask over nMasks = 2^n pump-on/off states (n = pump count),
    // i.e. exponential in the number of pumps.
    public string complexity { get; } = "O(H * B * n * 4^n), H = 24 hours, B = tank buckets, n = number of pumps";

    // Return a non-empty sentinel so the IVisualization<T> default dispatch in
    // VisualizationInterface.cs does not short-circuit StepsVisualization when
    // the steps list is empty.
    public List<object> GetSteps(PUMPSCHEDULINGCM _) => [true];

    private const int Hours = 24;
    private const double Inf = double.PositiveInfinity;

    public string solve(PUMPSCHEDULINGCM problem) {
        int n = problem.Pumps.Count;
        int nMasks = 1 << n;
        double cap = problem.TankCapacity;
        int buckets = (int)Math.Ceiling(cap);
        double bucketSize = 1.0;

        int ToBucket(double level) =>
            Math.Clamp((int)Math.Round(level / bucketSize), 0, buckets);

        double ToLevel(int b) => b * bucketSize;

        double[] maskFlow = new double[nMasks];
        for (int mask = 0; mask < nMasks; mask++)
            for (int p = 0; p < n; p++)
                if ((mask & (1 << p)) != 0)
                    maskFlow[mask] += problem.Pumps[p].FlowRateGph;

        double EnergyCost(int mask, int h) {
            double rate = problem.PeakHours.Contains(h)
                ? problem.OnPeakCostPerKwh
                : problem.OffPeakCostPerKwh;
            double kw = 0;
            for (int p = 0; p < n; p++)
                if ((mask & (1 << p)) != 0)
                    kw += problem.Pumps[p].PowerKw;
            return kw * rate;
        }

        double StartupCost(int prevMask, int currMask) {
            double cost = 0;
            int startups = (~prevMask) & currMask & (nMasks - 1);
            for (int p = 0; p < n; p++)
                if ((startups & (1 << p)) != 0)
                    cost += problem.Pumps[p].StartupCostDollars;
            return cost;
        }

        int stateB = buckets + 1;
        double[,,] dp = new double[Hours + 1, stateB, nMasks];
        int[,,] parentB = new int[Hours + 1, stateB, nMasks];
        int[,,] parentM = new int[Hours + 1, stateB, nMasks];

        for (int h = 0; h <= Hours; h++)
            for (int b = 0; b < stateB; b++)
                for (int m = 0; m < nMasks; m++) {
                    dp[h, b, m] = Inf;
                    parentB[h, b, m] = -1;
                    parentM[h, b, m] = -1;
                }

        int initB = ToBucket(problem.TankCurrentLevel);
        dp[0, initB, 0] = 0.0;

        for (int h = 0; h < Hours; h++) {
            if (timerHasExpired) return string.Empty;

            double demand = problem.DemandGph[h];

            for (int b = 0; b < stateB; b++) {
                double levelNow = ToLevel(b);

                for (int prevMask = 0; prevMask < nMasks; prevMask++) {
                    double stateCost = dp[h, b, prevMask];
                    if (stateCost >= Inf) continue;

                    for (int mask = 0; mask < nMasks; mask++) {
                        double newLevel = levelNow - demand + maskFlow[mask];
                        if (newLevel < 0 || newLevel > cap) continue;

                        int newB = ToBucket(newLevel);
                        double stepCost = EnergyCost(mask, h) + StartupCost(prevMask, mask);
                        double candidate = stateCost + stepCost;

                        if (candidate < dp[h + 1, newB, mask]) {
                            dp[h + 1, newB, mask] = candidate;
                            parentB[h + 1, newB, mask] = b;
                            parentM[h + 1, newB, mask] = prevMask;
                        }
                    }
                }
            }
        }

        double minCost = Inf;
        int bestB = -1, bestM = -1;
        for (int b = 0; b < stateB; b++)
            for (int m = 0; m < nMasks; m++)
                if (dp[Hours, b, m] < minCost) {
                    minCost = dp[Hours, b, m];
                    bestB = b;
                    bestM = m;
                }

        if (bestB == -1) return string.Empty;

        int[] schedMasks = new int[Hours];
        int curB = bestB, curM = bestM;
        for (int h = Hours; h > 0; h--) {
            schedMasks[h - 1] = curM;
            int pb = parentB[h, curB, curM];
            int pm = parentM[h, curB, curM];
            curB = pb;
            curM = pm;
        }

        // Build certificate using UtilCollection.
        UtilCollection cert = new("()");
        cert.Add(new UtilCollection(
            Math.Round(minCost, 2).ToString(System.Globalization.CultureInfo.InvariantCulture)));

        UtilCollection sched = new("()");
        for (int p = 0; p < n; p++) {
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
