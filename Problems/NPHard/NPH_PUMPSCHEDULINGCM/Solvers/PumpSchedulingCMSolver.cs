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
        "(hour, exact tank level, previous-pump-mask). A forward pass computes the minimum-cost " +
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
    // polynomial in the hour count H and reachable tank-level count B, but the per-hour transition
    // enumerates prevMask x mask over nMasks = 2^n pump-on/off states (n = pump count),
    // i.e. exponential in the number of pumps.
    public MathematicalFunction timeComplexity { get; } = new("H * B * n * 4^n", ", H = 24 hours, B = reachable tank levels per hour (whole gallons if there are too many to track exactly), n = number of pumps");

    // The most (tank level, pumps on) states the exact table may hold in one hour; past it the solver falls back to whole
    // gallons (#887). The table keeps every hour for the trace back, 16 bytes a state, so at most 24 x 100,000 x 16 B,
    // about 38 MB: what the whole-gallon table took for the default 10,000-gal, 3-pump instance (25 x 10,001 x 8 states).
    internal const int DefaultStateCap = 100_000;
    // Internal, so it stays out of /info; tests lower it to reach the fallback on a small instance.
    internal int StateCap { get; init; } = DefaultStateCap;

    private const int Hours = 24;
    private const double Inf = double.PositiveInfinity;

    // One (tank level, pumps on) state: the cheapest cost to reach it and where that came from (the row and mask of the
    // hour before).
    private readonly record struct Slot(double Cost, int ParentRow, int ParentMask);
    private static readonly Slot Unreached = new(Inf, -1, -1);

    public string solve(PUMPSCHEDULINGCM problem) => Solve(problem, StepRecorder<PumpPlan>.Off);

    // Steps: the forward pass, one Try per hour with the band of tank levels still reachable (a Reject, and no answer, at
    // an hour no state survives); the trace back, one Accept that commits the schedule; the replay, one Try per hour of
    // it; then Done.
    public string Solve(PUMPSCHEDULINGCM problem, StepRecorder<PumpPlan> rec) {
        int n = problem.Pumps.Count;
        int nMasks = 1 << n;
        // The tank level exactly (#887), one state per level reached, unless that is too many states: then whole gallons.
        var tank = TankLevels.For(problem.TankCapacity, problem.TankCurrentLevel, 0, problem.DemandGph,
            problem.Pumps.Select(p => p.FlowRateGph).ToList(), StateCap);

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

        // One layer per hour, layer 0 holding the starting level; a layer has a row per level it reaches.
        var start = new TankLayer<Slot>(nMasks, Unreached);
        start[start.Row(tank.Start), 0] = new Slot(0.0, -1, -1);
        var dp = new List<TankLayer<Slot>> { start };
        // The forward pass, hour by hour, for the steps.
        var reached = new List<ReachedHour>();

        for (int h = 0; h < Hours; h++) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer;

            var now = dp[h];
            var next = new TankLayer<Slot>(nMasks, Unreached);

            // Lowest level first, then masks in order: the order the per-gallon table filled in, so ties break the same way.
            foreach (int b in now.ByLevel()) {
                if (timerHasExpired) return SolveRun.TimedOutAnswer; // #935: an hour can take long with many pumps
                long levelNow = now.Levels[b];

                for (int prevMask = 0; prevMask < nMasks; prevMask++) {
                    double stateCost = now[b, prevMask].Cost;
                    if (stateCost >= Inf) continue;

                    for (int mask = 0; mask < nMasks; mask++) {
                        if (!tank.Next(levelNow, h, mask, out long newLevel, out _)) continue;

                        double stepCost = EnergyCost(mask, h) + StartupCost(prevMask, mask);
                        double candidate = stateCost + stepCost;

                        int newB = next.Row(newLevel);
                        if (candidate < next[newB, mask].Cost)
                            next[newB, mask] = new Slot(candidate, b, prevMask);
                    }
                }
            }
            dp.Add(next);

            // The states the table holds after hour h: how many, their tank levels and the cheapest cost so far.
            int states = 0;
            double low = Inf, high = -Inf, cheapest = Inf;
            for (int b = 0; b < next.Levels.Count; b++)
                for (int m = 0; m < nMasks; m++)
                    if (next[b, m].Cost < Inf) {
                        states++;
                        low = Math.Min(low, tank.Gallons(next.Levels[b]));
                        high = Math.Max(high, tank.Gallons(next.Levels[b]));
                        cheapest = Math.Min(cheapest, next[b, m].Cost);
                    }
            int hour = h;
            if (states == 0) {
                // Nothing reaches this hour, so nothing reaches hour 24: the answer is already "no answer".
                rec.Reject(() => new PumpPlan(reached, [], null),
                    () => $"Forward pass, hour {hour}: no state survives. Every pump mix either drains the tank below empty or overflows it.",
                    ScheduleFrames.HourId(hour));
                rec.Done(new PumpPlan(reached, [], null), false, NoAnswerCaption(tank));
                return "{}";
            }
            reached.Add(new ReachedHour(states, low, high, cheapest));
            rec.Try(() => new PumpPlan(reached, [], null),
                () => $"{ScheduleFrames.ForwardCaption(hour, states, low, high)}, cheapest so far {ScheduleFrames.Money(cheapest)}.",
                ScheduleFrames.HourId(hour));
        }

        var last = dp[Hours];
        double minCost = Inf;
        int bestB = -1, bestM = -1;
        foreach (int b in last.ByLevel())
            for (int m = 0; m < nMasks; m++)
                if (last[b, m].Cost < minCost) {
                    minCost = last[b, m].Cost;
                    bestB = b;
                    bestM = m;
                }

        // No schedule keeps the tank between empty and full: "{}" is "no answer" (#803, #605).
        if (bestB == -1) {
            rec.Done(new PumpPlan(reached, [], null), false, NoAnswerCaption(tank));
            return "{}";
        }

        int[] schedMasks = new int[Hours];
        int curB = bestB, curM = bestM;
        for (int h = Hours; h > 0; h--) {
            schedMasks[h - 1] = curM;
            var slot = dp[h][curB, curM];
            curB = slot.ParentRow;
            curM = slot.ParentMask;
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
        // The verifier simulates the schedule exactly, as the table does unless it fell back to whole gallons, when the two
        // can disagree (#887).
        bool ok;
        try { ok = problem.defaultVerifier.verify(problem, certificate); } catch { ok = false; }
        rec.Done(plan, ok, DoneCaption(problem, on, ok, tank.Rounded));
        return certificate;
    }

    private static string NoAnswerCaption(TankLevels tank) =>
        "No schedule keeps the tank between empty and full for all 24 hours, so there is no answer."
        + (tank.Rounded ? " The tank has too many levels to track exactly, so the solver rounded it to whole gallons." : "");

    private static string DoneCaption(PUMPSCHEDULINGCM problem, IReadOnlyList<IReadOnlyCollection<string>> on, bool ok, bool rounded) {
        var spec = PumpSchedulingCMVisualization.Spec(problem);
        var (levels, cost) = ScheduleFrames.Exact(spec, on);
        if (ok) {
            double low = Math.Min(spec.Level, levels.Min()), high = Math.Max(spec.Level, levels.Max());
            return $"Cheapest schedule: {ScheduleFrames.Money(cost)} for the day. The tank stays between {ScheduleFrames.Gallons(low)} and {ScheduleFrames.Gallons(high)} gal."
                + (rounded ? " The tank has too many levels to track exactly, so the solver rounded it to whole gallons: a cheaper schedule may exist." : "");
        }
        return ScheduleFrames.FirstOutOfLimits(spec, on) is var (hour, level)
            ? $"The schedule costs {ScheduleFrames.Money(cost)}, but the tank ends hour {hour} at {ScheduleFrames.Gallons(level)} gal, outside 0 to "
                + $"{ScheduleFrames.Gallons(spec.Capacity)} gal"
                + (rounded ? ": the solver rounds the tank level to whole gallons, so it did not see this." : ".")
            : $"The schedule costs {ScheduleFrames.Money(cost)}, which the verifier does not accept.";
    }
}
