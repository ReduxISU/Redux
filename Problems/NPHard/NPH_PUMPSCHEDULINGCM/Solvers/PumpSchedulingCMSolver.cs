using System;
using API.Interfaces;
using API.Interfaces.Schedule;
using API.Interfaces.Steps;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGCM;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGCM.Visualizations;
using SPADE;

namespace API.Problems.NPHard.NPH_PUMPSCHEDULINGCM.Solvers;

// Incremental: the forward pass fills the table hour by hour, the trace back commits the schedule, and the replay walks
// the committed schedule hour by hour.
class PumpSchedulingCMSolver : ISolver<PUMPSCHEDULINGCM, PumpPlan>, IIncrementalSolver {
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
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Memo table + optimal-substructure recurrence.
    public SolverType solverType { get; } = SolverType.DynamicProgramming;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
    // Declared, not derived. Pseudo-polynomial, same nuance as KnapsackDP's "O(n * W)":
    // polynomial in the hour count H and tank-bucket count B, but the per-hour transition
    // enumerates prevMask x mask over nMasks = 2^n pump-on/off states (n = pump count),
    // i.e. exponential in the number of pumps.
    public MathematicalFunction timeComplexity { get; } = new("H * B * n * 4^n", ", H = 24 hours, B = tank buckets, n = number of pumps");

    private const int Hours = 24;
    private const double Inf = double.PositiveInfinity;

    public string solve(PUMPSCHEDULINGCM problem) => Solve(problem, StepRecorder<PumpPlan>.Off);

    // Steps: the forward pass, one Try per hour with the band of tank levels still reachable (a Reject, and no answer, at
    // an hour no state survives); the trace back, one Accept that commits the schedule; the replay, one Try per hour of
    // it; then Done.
    public string Solve(PUMPSCHEDULINGCM problem, StepRecorder<PumpPlan> rec) {
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
        // The forward pass, hour by hour, for the steps.
        var reached = new List<ReachedHour>();

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

            // The states the table holds after hour h: how many, their tank levels and the cheapest cost so far.
            int states = 0;
            double low = Inf, high = -Inf, cheapest = Inf;
            for (int b = 0; b < stateB; b++)
                for (int m = 0; m < nMasks; m++)
                    if (dp[h + 1, b, m] < Inf) {
                        states++;
                        low = Math.Min(low, ToLevel(b));
                        high = Math.Max(high, ToLevel(b));
                        cheapest = Math.Min(cheapest, dp[h + 1, b, m]);
                    }
            int hour = h;
            if (states == 0) {
                // Nothing reaches this hour, so nothing reaches hour 24: the answer is already "no answer".
                rec.Reject(() => new PumpPlan(reached, [], null),
                    () => $"Forward pass, hour {hour}: no state survives. Every pump mix either drains the tank below empty or overflows it.",
                    ScheduleFrames.HourId(hour));
                rec.Done(new PumpPlan(reached, [], null), false,
                    "No schedule keeps the tank between empty and full for all 24 hours, so there is no answer.");
                return "{}";
            }
            reached.Add(new ReachedHour(states, low, high, cheapest));
            rec.Try(() => new PumpPlan(reached, [], null),
                () => $"{ScheduleFrames.ForwardCaption(hour, states, low, high)}, cheapest so far {ScheduleFrames.Money(cheapest)}.",
                ScheduleFrames.HourId(hour));
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

        // No schedule keeps the tank between empty and full: "{}" is "no answer" (#803, #605).
        if (bestB == -1) {
            rec.Done(new PumpPlan(reached, [], null), false,
                "No schedule keeps the tank between empty and full for all 24 hours, so there is no answer.");
            return "{}";
        }

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
        string certificate = cert.ToString();

        var on = schedMasks.Select(m => (IReadOnlyCollection<string>)ScheduleFrames.OnFromMask(m, n)).ToList();
        var plan = new PumpPlan(reached, on, null);
        rec.Accept(() => plan,
            () => $"Trace back from the cheapest state at hour 24 ({ScheduleFrames.Money(minCost)}), following each state's parent, to recover the schedule.");
        for (int h = 0; h < Hours; h++) {
            int hour = h;
            rec.Try(() => plan, () => ScheduleFrames.ReplayCaption(PumpSchedulingCMVisualization.Spec(problem), on, hour, null), ScheduleFrames.HourId(hour));
        }
        // The verifier simulates the schedule exactly; the table rounds the tank to whole gallons (#738), so it can disagree.
        bool ok;
        try { ok = problem.defaultVerifier.verify(problem, certificate); } catch { ok = false; }
        rec.Done(plan, ok, DoneCaption(problem, on, ok));
        return certificate;
    }

    private static string DoneCaption(PUMPSCHEDULINGCM problem, IReadOnlyList<IReadOnlyCollection<string>> on, bool ok) {
        var spec = PumpSchedulingCMVisualization.Spec(problem);
        var (levels, cost) = ScheduleFrames.Exact(spec, on);
        if (ok) {
            double low = Math.Min(spec.Level, levels.Min()), high = Math.Max(spec.Level, levels.Max());
            return $"Cheapest schedule: {ScheduleFrames.Money(cost)} for the day. The tank stays between {ScheduleFrames.Gallons(low)} and {ScheduleFrames.Gallons(high)} gal.";
        }
        return ScheduleFrames.FirstOutOfLimits(spec, on) is var (hour, level)
            ? $"The schedule costs {ScheduleFrames.Money(cost)}, but the tank ends hour {hour} at {ScheduleFrames.Gallons(level)} gal, outside 0 to "
                + $"{ScheduleFrames.Gallons(spec.Capacity)} gal: the solver rounds the tank level to whole gallons, so it did not see this."
            : $"The schedule costs {ScheduleFrames.Money(cost)}, which the verifier does not accept.";
    }
}
