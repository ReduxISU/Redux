using System;
using System.Collections.Generic;
using API.Interfaces;
using API.Interfaces.Schedule;
using API.Interfaces.Steps;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGEM;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGEM.Visualizations;
using SPADE;

namespace API.Problems.NPHard.NPH_PUMPSCHEDULINGEM.Solvers;

// The steps Pump Scheduling Emergency Resilience records, for the Schedule picture:
//   - when the budget is 0, an Update that fixes it at 1.5 times the cheapest schedule;
//   - the forward pass: an Update per hour with the band of tank levels still reachable and the most water stored so far,
//     or a Reject (and no answer) at an hour no state survives;
//   - the trace back: an Accept that commits the schedule that stores the most water;
//   - the replay: an Update per hour of it (pumps on, water in and out, the hour's cost and the budget spent);
//   - the Done, with the verifier's verdict.
// Incremental: the forward pass fills the table hour by hour, the trace back commits the schedule, and the replay walks
// the committed schedule hour by hour.
class PumpSchedulingEMSolver : ISolver<PUMPSCHEDULINGEM, PumpPlan>, IIncrementalSolver {
    public string solverName { get; } = "Pump Scheduling Emergency Resilience Longest Path";
    public string solverDefinition { get; } =
        "Models the 24-hour pump scheduling problem as a DAG where each node represents\r\n" +
        "(hour, exact tank level, previous-pump-mask). For emergency resilience, a forward\r\n" +
        "pass computes the maximum-cumulative-water-stored path subject to total cost\r\n" +
        "remaining within the effective budget. If the instance budget is 0, the effective\r\n" +
        "budget is auto-computed as 1.5× the cost-minimization optimum via a preliminary\r\n" +
        "shortest-path solve. The optimal schedule is recovered by backtracking.\r\n" +
        "Certificate format: (effectiveBudget, totalCost, schedule) where schedule is\r\n" +
        "((PumpName,h0,...,h23),...) with binary per-hour on/off values.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Memo table + optimal-substructure recurrence.
    public SolverType solverType { get; } = SolverType.DynamicProgramming;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
    // Declared, not derived. Same pseudo-polynomial nuance as PumpSchedulingCMSolver: the
    // optional cost-minimization pre-solve and the emergency-resilience solve each run the
    // same H x B x nMasks x nMasks DP transition (nMasks = 2^n pump-on/off states), so the
    // two sequential passes don't change the asymptotic bound.
    public MathematicalFunction timeComplexity { get; } = new("H * B * n * 4^n", ", H = 24 hours, B = reachable tank levels per hour (whole gallons if there are too many to track exactly), n = number of pumps");

    // The most (tank level, pumps on) states the exact table may hold in one hour, in either pass; past it the solver falls
    // back to whole gallons. Each pass keeps only the hour at hand and the next, so the cap bounds the table to
    // a few MB; the labels on the Pareto fronts are not capped, as they never were. The same cap as Cost Minimization.
    internal const int DefaultStateCap = 100_000;
    // Internal, so it stays out of /info; tests lower it to reach the fallback on a small instance.
    internal int StateCap { get; init; } = DefaultStateCap;

    private const string NoSolution = "{}";
    private const int Hours = 24;
    private const double BudgetSlack = 1.5;
    private const double Inf = double.PositiveInfinity;
    private const double NegInf = double.NegativeInfinity;

    // How the steps show the plan so far, given by the hour a step is about (-1: the whole day).
    private static RunSteps<int, PumpPlan> NewSteps(StepRecorder<PumpPlan> rec, List<ReachedHour> reached, List<IReadOnlyCollection<string>> on,
                                                    double budget) => new(rec,
        // What every picture shows: the hours the forward pass has reached, the pumps on each hour once the schedule is
        // committed, the budget, and the hour.
        partial: _ => new PumpPlan(reached, on, budget),
        focus: hour => hour < 0 ? [] : [ScheduleFrames.HourId(hour)]);

    public string solve(PUMPSCHEDULINGEM problem) => Solve(problem, StepRecorder<PumpPlan>.Off);

    public string Solve(PUMPSCHEDULINGEM problem, StepRecorder<PumpPlan> rec) {
        // The tank level exactly, one state per level reached, unless that is too many states: then whole gallons. Both
        // passes count in the same tank.
        var tank = TankLevels.For(problem.TankCapacity, problem.TankCurrentLevel, problem.TankMinLevel, problem.DemandGph,
            problem.Pumps.Select(p => p.FlowRateGph).ToList(), StateCap);
        double effectiveBudget = problem.BudgetLimitDollars;
        bool auto = effectiveBudget <= 0;

        // Auto-compute: run cost-minimization solve first.
        double minCost = auto ? RunCostMinimization(problem, tank) : 0;
        if (auto) effectiveBudget = minCost * BudgetSlack;
        // The budget as the certificate writes it.
        double budget = Math.Round(effectiveBudget, 2);

        // The forward pass, hour by hour, and the schedule's pumps on each hour once it is committed, for the steps.
        var reached = new List<ReachedHour>();
        var on = new List<IReadOnlyCollection<string>>();
        var steps = NewSteps(rec, reached, on, budget);
        if (auto && minCost > 0)
            steps.Update(-1, $"The budget is 0, which means 1.5 times the cheapest schedule that keeps the tank within its limits. That costs {ScheduleFrames.Money(minCost)}, so the budget is {ScheduleFrames.Money(budget)}.");
        else if (auto)
            steps.Update(-1, $"The budget is 0, which means 1.5 times the cheapest schedule that keeps the tank within its limits. That costs nothing (or there is none), so the budget is $0.00.");

        var (schedMasks, bestScore) = RunEmergencyResilience(problem, tank, effectiveBudget, steps, reached);

        // No schedule fits the tank limits and the budget: report "no solution" ("{}", the repo's
        // standard convention) rather than inventing an over-budget schedule.
        if (schedMasks == null) {
            // A timed run is stopped by SolveRun.Record, which puts its stopped Done in place of this one.
            if (timerHasExpired) {
                steps.Done(new PumpPlan(reached, [], budget), false, $"The solver's timer ran out, so there is no answer.");
                return SolveRun.TimedOutAnswer;
            }
            if (tank.Rounded)
                steps.Done(new PumpPlan(reached, [], budget), false, $"No schedule keeps the tank between {ScheduleFrames.Gallons(problem.TankMinLevel)} and {ScheduleFrames.Gallons(problem.TankCapacity)} gal within the {ScheduleFrames.Money(budget)} budget for all 24 hours, so there is no answer. The tank has too many levels to track exactly, so the solver rounded it to whole gallons.");
            else
                steps.Done(new PumpPlan(reached, [], budget), false, $"No schedule keeps the tank between {ScheduleFrames.Gallons(problem.TankMinLevel)} and {ScheduleFrames.Gallons(problem.TankCapacity)} gal within the {ScheduleFrames.Money(budget)} budget for all 24 hours, so there is no answer.");
            return NoSolution;
        }

        double totalCost = ComputeTotalCost(problem, schedMasks);

        string certificate = BuildCertificate(problem, schedMasks,
            Math.Round(effectiveBudget, 2),
            Math.Round(totalCost, 2));

        // The rest is for the steps only: the schedule's pumps on each hour, its replay, and the verdict.
        if (!rec.Enabled) return certificate;
        int n = problem.Pumps.Count;
        on.AddRange(schedMasks.Select(m => (IReadOnlyCollection<string>)ScheduleFrames.OnFromMask(m, n)));
        steps.Accept(-1, $"Trace back from the state that stores the most water by hour 24 ({ScheduleFrames.Gallons(tank.Gallons(bestScore))} gal·h), following each step's parent, to recover the schedule.");
        var spec = PumpSchedulingEMVisualization.Spec(problem);
        for (int h = 0; h < Hours; h++)
            steps.Update(h, $"{ScheduleFrames.ReplayCaption(spec, on, h, budget)}");
        // The verifier simulates the schedule exactly, as the table does unless it fell back to whole gallons, when the two
        // can disagree.
        bool ok;
        try { ok = problem.defaultVerifier.verify(problem, certificate); } catch { ok = false; }
        // What the schedule costs and where it takes the tank, worked out exactly.
        var (levels, cost) = ScheduleFrames.Exact(spec, on);
        var plan = new PumpPlan(reached, on, budget);
        if (ok && tank.Rounded)
            steps.Done(plan, true, $"Most water stored within the {ScheduleFrames.Money(budget)} budget{(auto ? " (1.5 times the cheapest schedule)" : "")}: {ScheduleFrames.Gallons(levels.Sum())} gal·h, for {ScheduleFrames.Money(cost)}. The tank has too many levels to track exactly, so the solver rounded it to whole gallons: a schedule may store more.");
        else if (ok)
            steps.Done(plan, true, $"Most water stored within the {ScheduleFrames.Money(budget)} budget{(auto ? " (1.5 times the cheapest schedule)" : "")}: {ScheduleFrames.Gallons(levels.Sum())} gal·h, for {ScheduleFrames.Money(cost)}.");
        else if (ScheduleFrames.FirstOutOfLimits(spec, on) is var (hour, level)) {
            if (tank.Rounded)
                steps.Done(plan, false, $"The schedule costs {ScheduleFrames.Money(cost)}, but the tank ends hour {hour} at {ScheduleFrames.Gallons(level)} gal, outside {ScheduleFrames.Gallons(spec.Minimum)} to {ScheduleFrames.Gallons(spec.Capacity)} gal: the solver rounds the tank level to whole gallons, so it did not see this.");
            else
                steps.Done(plan, false, $"The schedule costs {ScheduleFrames.Money(cost)}, but the tank ends hour {hour} at {ScheduleFrames.Gallons(level)} gal, outside {ScheduleFrames.Gallons(spec.Minimum)} to {ScheduleFrames.Gallons(spec.Capacity)} gal.");
        } else {
            steps.Done(plan, false, $"The schedule costs {ScheduleFrames.Money(cost)}, which the verifier does not accept.");
        }
        return certificate;
    }

    // ── Cost-minimization sub-solve (shortest path) — returns total cost ──────
    private double RunCostMinimization(PUMPSCHEDULINGEM problem, TankLevels tank) {
        int n = problem.Pumps.Count;
        int nMasks = 1 << n;

        var dp = new TankLayer<double>(nMasks, Inf);
        dp[dp.Row(tank.Start), 0] = 0.0;

        for (int h = 0; h < Hours; h++) {
            if (timerHasExpired) return 0.0;

            var next = new TankLayer<double>(nMasks, Inf);

            foreach (int b in dp.ByLevel()) {
                if (timerHasExpired) return 0.0; // an hour can take long with many pumps
                long levelNow = dp.Levels[b];
                for (int prevMask = 0; prevMask < nMasks; prevMask++) {
                    double cur = dp[b, prevMask];
                    if (cur >= Inf) continue;

                    for (int mask = 0; mask < nMasks; mask++) {
                        if (!tank.Next(levelNow, h, mask, out long newLevel, out _)) continue;

                        double edgeCost = EdgeCost(problem, prevMask, mask, h, n, nMasks);
                        double candidate = cur + edgeCost;
                        int newB = next.Row(newLevel);

                        if (candidate < next[newB, mask])
                            next[newB, mask] = candidate;
                    }
                }
            }

            dp = next;
        }

        double minCost = Inf;
        for (int b = 0; b < dp.Levels.Count; b++)
            for (int m = 0; m < nMasks; m++)
                if (dp[b, m] < minCost)
                    minCost = dp[b, m];

        return minCost >= Inf ? 0.0 : minCost;
    }

    // One DP label: a path to some (tank level, mask) state with its cumulative cost and score.
    private sealed class Label {
        public readonly double Cost;
        public readonly double Score;
        public readonly int Mask;
        public readonly Label? Parent;
        public Label(double cost, double score, int mask, Label? parent) {
            Cost = cost; Score = score; Mask = mask; Parent = parent;
        }
    }

    // Adds a label to a state's Pareto front (lower cost and higher score are better), discarding
    // it if an existing label dominates it and evicting labels it dominates.
    private static void AddToFront(List<Label>? front, Label cand, out List<Label> result) {
        if (front == null) {
            result = new List<Label> { cand };
            return;
        }
        foreach (var l in front)
            if (l.Cost <= cand.Cost && l.Score >= cand.Score) {
                result = front;
                return;
            }
        front.RemoveAll(l => cand.Cost <= l.Cost && cand.Score >= l.Score);
        front.Add(cand);
        result = front;
    }

    // ── Emergency resilience solve (constrained longest path) ─────────────────
    // Each (tank level, previous mask) state keeps the Pareto front of (cost, score) labels instead
    // of a single max-score path: a cheaper, lower-scoring path may be the only one that still fits
    // the budget in later hours, so it must not be discarded.
    // Returns the schedule (null when there is none) and the water it stores, as the table counts it (in the tank's units).
    private (int[]? Masks, double Score) RunEmergencyResilience(PUMPSCHEDULINGEM problem, TankLevels tank, double budgetLimit,
        RunSteps<int, PumpPlan> steps, List<ReachedHour> reached) {
        int n = problem.Pumps.Count;
        int nMasks = 1 << n;

        var dp = new TankLayer<List<Label>?>(nMasks, null);
        dp[dp.Row(tank.Start), 0] = new List<Label> { new Label(0.0, 0.0, 0, null) };

        for (int h = 0; h < Hours; h++) {
            if (timerHasExpired) return (null, 0);

            var next = new TankLayer<List<Label>?>(nMasks, null);

            foreach (int b in dp.ByLevel()) {
                if (timerHasExpired) return (null, 0);
                long levelNow = dp.Levels[b];
                for (int prevMask = 0; prevMask < nMasks; prevMask++) {
                    var front = dp[b, prevMask];
                    if (front == null) continue;

                    for (int mask = 0; mask < nMasks; mask++) {
                        if (!tank.Next(levelNow, h, mask, out long newLevel, out double stored)) continue;

                        double edgeCost = EdgeCost(problem, prevMask, mask, h, n, nMasks);

                        foreach (var label in front) {
                            double totalCost = label.Cost + edgeCost;
                            // Prune paths that exceed the budget.
                            if (totalCost > budgetLimit) continue;

                            // Score is the water stored after this hour.
                            var cand = new Label(totalCost, label.Score + stored, mask, label);
                            int newB = next.Row(newLevel);
                            AddToFront(next[newB, mask], cand, out var updated);
                            next[newB, mask] = updated;
                        }
                    }
                }
            }

            dp = next;

            // The states the table holds after hour h: how many, their tank levels and the most water stored so far.
            int states = 0;
            double low = Inf, high = NegInf, most = NegInf;
            for (int b = 0; b < dp.Levels.Count; b++)
                for (int m = 0; m < nMasks; m++) {
                    var front = dp[b, m];
                    if (front == null) continue;
                    states++;
                    low = Math.Min(low, tank.Gallons(dp.Levels[b]));
                    high = Math.Max(high, tank.Gallons(dp.Levels[b]));
                    foreach (var l in front) most = Math.Max(most, tank.Gallons(l.Score));
                }
            if (states == 0) {
                // Nothing reaches this hour, so nothing reaches hour 24: the answer is already "no answer".
                steps.Reject(h, $"Forward pass, hour {h}: no state survives. Every pump mix either breaks the tank's limits or the budget.");
                return (null, 0);
            }
            reached.Add(new ReachedHour(states, low, high, most));
            steps.Update(h, $"{ScheduleFrames.ForwardCaption(h, states, low, high)}, most water stored so far {ScheduleFrames.Gallons(most)} gal·h.");
        }

        // Pick the terminal label with the highest cumulative water score.
        Label? best = null;
        foreach (int b in dp.ByLevel())
            for (int m = 0; m < nMasks; m++) {
                var front = dp[b, m];
                if (front == null) continue;
                foreach (var l in front)
                    if (best == null || l.Score > best.Score)
                        best = l;
            }

        if (best == null) return (null, 0);

        // Walk the parent chain to recover the per-hour masks.
        int[] schedMasks = new int[Hours];
        Label? cur = best;
        for (int h = Hours; h > 0; h--) {
            schedMasks[h - 1] = cur!.Mask;
            cur = cur.Parent;
        }

        return (schedMasks, best.Score);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static double EdgeCost(PUMPSCHEDULINGEM problem, int prevMask, int mask, int h, int n, int nMasks) {
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

    internal static double ComputeTotalCost(PUMPSCHEDULINGEM problem, int[] schedMasks) {
        int n = problem.Pumps.Count;
        int nMasks = 1 << n;
        double totalCost = 0.0;
        int prevMask = 0;

        for (int h = 0; h < Hours; h++) {
            int mask = schedMasks[h];
            totalCost += EdgeCost(problem, prevMask, mask, h, n, nMasks);
            prevMask = mask;
        }

        return totalCost;
    }

    private static string BuildCertificate(PUMPSCHEDULINGEM problem, int[] schedMasks,
        double effectiveBudget, double totalCost) {
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
