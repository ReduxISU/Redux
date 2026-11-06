using System;
using API.Interfaces;
using API.Interfaces.Schedule;
using API.Interfaces.Steps;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGCM;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGCM.Visualizations;
using SPADE;

namespace API.Problems.NPHard.NPH_PUMPSCHEDULINGCM.Solvers;

// The steps Pump Scheduling Cost Minimization records, for the Schedule picture:
//   - the forward pass: an Update per hour with the band of tank levels still reachable and the cheapest cost so far, or
//     a Reject (and no answer) at an hour no state survives;
//   - the trace back: an Accept that commits the cheapest schedule;
//   - the replay: an Update per hour of it (pumps on, water in and out, the hour's cost);
//   - the Done, with the verifier's verdict.
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
    // gallons. The table keeps every hour for the trace back, 16 bytes a state, so at most 24 x 100,000 x 16 B,
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

    // How the steps show the plan so far, given by the hour a step is about (-1: the whole day).
    private static RunSteps<int, PumpPlan> NewSteps(StepRecorder<PumpPlan> rec, List<ReachedHour> reached, List<IReadOnlyCollection<string>> on) => new(rec,
        // What every picture shows: the hours the forward pass has reached, the pumps on each hour once the schedule is
        // committed, and the hour.
        partial: _ => new PumpPlan(reached, on, null),
        focus: hour => hour < 0 ? [] : [ScheduleFrames.HourId(hour)]);

    public string solve(PUMPSCHEDULINGCM problem) => Solve(problem, StepRecorder<PumpPlan>.Off);

    public string Solve(PUMPSCHEDULINGCM problem, StepRecorder<PumpPlan> rec) {
        int n = problem.Pumps.Count;
        int nMasks = 1 << n;
        // The tank level exactly, one state per level reached, unless that is too many states: then whole gallons.
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
        // The forward pass, hour by hour, and the schedule's pumps on each hour once it is committed, for the steps.
        var reached = new List<ReachedHour>();
        var on = new List<IReadOnlyCollection<string>>();
        var steps = NewSteps(rec, reached, on);

        for (int h = 0; h < Hours; h++) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer;

            var now = dp[h];
            var next = new TankLayer<Slot>(nMasks, Unreached);

            // Lowest level first, then masks in order: the order the per-gallon table filled in, so ties break the same way.
            foreach (int b in now.ByLevel()) {
                if (timerHasExpired) return SolveRun.TimedOutAnswer; // an hour can take long with many pumps
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
            if (states == 0) {
                // Nothing reaches this hour, so nothing reaches hour 24: the answer is already "no answer".
                steps.Reject(h, $"At hour {h}, no state survives the forward pass. Every pump mix either drains the tank below empty or overflows it.");
                if (tank.Rounded)
                    steps.Done(new PumpPlan(reached, [], null), false, $"No schedule keeps the tank between empty and full for all 24 hours, so there is no answer. The tank has too many levels to track exactly, so the solver rounded it to whole gallons.");
                else
                    steps.Done(new PumpPlan(reached, [], null), false, $"No schedule keeps the tank between empty and full for all 24 hours, so there is no answer.");
                return "{}";
            }
            reached.Add(new ReachedHour(states, low, high, cheapest));
            steps.Update(h, $"At hour {h}, the forward pass holds {ScheduleFrames.ReachableStates(states)}, {ScheduleFrames.LevelRange(low, high)}, cheapest so far {ScheduleFrames.Money(cheapest)}.");
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

        // No schedule keeps the tank between empty and full: "{}" is "no answer".
        if (bestB == -1) {
            if (tank.Rounded)
                steps.Done(new PumpPlan(reached, [], null), false, $"No schedule keeps the tank between empty and full for all 24 hours, so there is no answer. The tank has too many levels to track exactly, so the solver rounded it to whole gallons.");
            else
                steps.Done(new PumpPlan(reached, [], null), false, $"No schedule keeps the tank between empty and full for all 24 hours, so there is no answer.");
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

        // The rest is for the steps only: the schedule's pumps on each hour, its replay, and the verdict.
        if (!rec.Enabled) return certificate;
        on.AddRange(schedMasks.Select(m => (IReadOnlyCollection<string>)ScheduleFrames.OnFromMask(m, n)));
        steps.Accept(-1, $"Trace back from the cheapest state at hour 24 ({ScheduleFrames.Money(minCost)}), following each state's parent, to recover the schedule.");
        var spec = PumpSchedulingCMVisualization.Spec(problem);
        for (int h = 0; h < Hours; h++)
            steps.Update(h, $"{ScheduleFrames.ReplayCaption(spec, on, h, null)}");
        // The verifier simulates the schedule exactly, as the table does unless it fell back to whole gallons, when the two
        // can disagree.
        bool ok;
        try { ok = problem.defaultVerifier.verify(problem, certificate); } catch { ok = false; }
        // What the schedule costs and where it takes the tank, worked out exactly.
        var (levels, cost) = ScheduleFrames.Exact(spec, on);
        var plan = new PumpPlan(reached, on, null);
        if (ok) {
            double low = Math.Min(spec.Level, levels.Min()), high = Math.Max(spec.Level, levels.Max());
            if (tank.Rounded)
                steps.Done(plan, true, $"Cheapest schedule: {ScheduleFrames.Money(cost)} for the day. The tank stays between {ScheduleFrames.Gallons(low)} and {ScheduleFrames.Gallons(high)} gal. The tank has too many levels to track exactly, so the solver rounded it to whole gallons: a cheaper schedule may exist.");
            else
                steps.Done(plan, true, $"Cheapest schedule: {ScheduleFrames.Money(cost)} for the day. The tank stays between {ScheduleFrames.Gallons(low)} and {ScheduleFrames.Gallons(high)} gal.");
        } else if (ScheduleFrames.FirstOutOfLimits(spec, on) is var (hour, level)) {
            if (tank.Rounded)
                steps.Done(plan, false, $"The schedule costs {ScheduleFrames.Money(cost)}, but the tank ends hour {hour} at {ScheduleFrames.Gallons(level)} gal, outside 0 to {ScheduleFrames.Gallons(spec.Capacity)} gal: the solver rounds the tank level to whole gallons, so it did not see this.");
            else
                steps.Done(plan, false, $"The schedule costs {ScheduleFrames.Money(cost)}, but the tank ends hour {hour} at {ScheduleFrames.Gallons(level)} gal, outside 0 to {ScheduleFrames.Gallons(spec.Capacity)} gal.");
        } else {
            steps.Done(plan, false, $"The schedule costs {ScheduleFrames.Money(cost)}, which the verifier does not accept.");
        }
        return certificate;
    }
}
