using System;
using System.Collections.Generic;
using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Interfaces.Schedule;
using API.Interfaces.Steps;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGEM.Solvers;
using SPADE;

namespace API.Problems.NPHard.NPH_PUMPSCHEDULINGEM.Visualizations;

// --- Frame data types ---

record EmPumpStatusEntry(string name, bool isOn, double flowGph, double powerKw);

record EmPumpFrameMetrics(
    int hour,
    double stepCost,
    double cumulativeCost,
    double budgetLimit,
    double budgetRemaining,
    double tankLevel,
    double tankCapacity,
    double tankMinLevel,
    double tankFillRatio,
    double flowIn,
    double demand,
    bool isPeakHour
);

record EmPumpFrameState(List<EmPumpStatusEntry> pumps);

class API_EmPumpFrame : API_JSON {
    public string action { get; }
    public EmPumpFrameMetrics metrics { get; }
    public EmPumpFrameState state { get; }

    public API_EmPumpFrame(string action, EmPumpFrameMetrics metrics, EmPumpFrameState state) {
        this.action = action;
        this.metrics = metrics;
        this.state = state;
    }
}

// --- Visualization ---

/// <summary>
/// The list format animates the chosen schedule hour by hour (unchanged). With <c>format=frames</c> the problem is drawn as the
/// Schedule picture: the pumps on and off for 24 hours, the tank level against its minimum and capacity, and the cost of
/// each hour against the budget, step by step for the selected solver (the budget, the forward pass, the trace back and
/// the replay).
/// </summary>
class PumpSchedulingEMVisualization : IVisualization<PUMPSCHEDULINGEM, API_empty>, IPumpScheduleVisualization<PUMPSCHEDULINGEM> {
    public string visualizationName { get; } =
        "Pump Scheduling Emergency Resilience — DAG Animation";
    public string visualizationDefinition { get; } =
        "Animates the 24-hour optimal emergency-resilience pump schedule, showing per-hour " +
        "pump states, tank levels, budget consumption, and cumulative water stored, as " +
        "produced by the constrained longest-path DAG dynamic programming solver.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.PumpSchedule;
    public ISolver solver { get; } = new PumpSchedulingEMSolver();

    // The Schedule picture (format=frames only). Implemented explicitly so it is not part of /info.
    Type? IVisualization.StepShape => typeof(PumpPlan);
    PumpScheduleSpec IPumpScheduleVisualization<PUMPSCHEDULINGEM>.Describe(PUMPSCHEDULINGEM problem) => Spec(problem);
    PumpPlan IPumpScheduleVisualization<PUMPSCHEDULINGEM>.ParseAnswer(PUMPSCHEDULINGEM problem, string answer) =>
        ScheduleFrames.ParsePumpPlan(problem.Pumps.Count, answer, withBudget: true);

    /// <summary>The instance as the Schedule picture describes it: the tank with its minimum, the day's demand and tariffs, the
    /// pumps in instance order, and the budget as the instance writes it (0: 1.5 times the cheapest schedule).</summary>
    internal static PumpScheduleSpec Spec(PUMPSCHEDULINGEM problem) =>
        new(ScheduleRule.EmergencyResilience, problem.TankCapacity, problem.TankCurrentLevel, problem.TankMinLevel, problem.DemandGph,
            problem.PeakHours, problem.OnPeakCostPerKwh, problem.OffPeakCostPerKwh,
            problem.Pumps.Select(p => new SchedulePump(p.Name, p.FlowRateGph, p.PowerKw, p.StartupCostDollars)).ToList()) {
            Budget = problem.BudgetLimitDollars,
        };

    public API_empty visualize(PUMPSCHEDULINGEM problem) => new API_empty();
    public API_JSON SolvedVisualization(PUMPSCHEDULINGEM problem, string solution) => new API_empty();

    // Explicitly override the default interface dispatch so it is never short-circuited
    // by the empty-steps guard in IVisualization<U>.StepsVisualization.
    List<API_JSON> IVisualization.StepsVisualization(string instance, List<object> steps)
        => StepsVisualization(new PUMPSCHEDULINGEM(instance), steps);

    public List<API_JSON> StepsVisualization(PUMPSCHEDULINGEM problem, List<object> _steps) {
        string certificate = new PumpSchedulingEMSolver().solve(problem);
        if (string.IsNullOrEmpty(certificate))
            return new List<API_JSON>();

        // Parse certificate: (effectiveBudget, totalCost, ((PumpName,h0,...,h23),...))
        double effectiveBudget = 0.0;
        int n = problem.Pumps.Count;
        int[] schedMasks = new int[24];

        try {
            var cert = new UtilCollection(certificate);
            var certList = cert.ToList();

            double.TryParse(certList[0].ToString().Trim(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out effectiveBudget);

            var schedSection = (UtilCollection)certList[2];
            int pumpIdx = 0;
            foreach (UtilCollection pumpRow in schedSection) {
                var parts = pumpRow.ToList();
                for (int h = 0; h < 24 && h + 1 < parts.Count; h++)
                    if (parts[h + 1].ToString().Trim() == "1")
                        schedMasks[h] |= (1 << pumpIdx);
                pumpIdx++;
            }
        } catch {
            return new List<API_JSON>();
        }

        // Build one frame per hour.
        var frames = new List<API_JSON>();
        double tankLevel = problem.TankCurrentLevel;
        double cumulativeCost = 0.0;
        int prevMask = 0;
        int nMasks = 1 << n;

        for (int h = 0; h < 24; h++) {
            int mask = schedMasks[h];
            bool isPeak = problem.PeakHours.Contains(h);
            double rate = isPeak ? problem.OnPeakCostPerKwh : problem.OffPeakCostPerKwh;
            double hourDemand = problem.DemandGph[h];

            double energyCost = 0.0;
            double flowIn = 0.0;
            double startupCost = 0.0;
            int startups = (~prevMask) & mask & (nMasks - 1);

            for (int p = 0; p < n; p++) {
                if ((mask & (1 << p)) != 0) {
                    energyCost += problem.Pumps[p].PowerKw * rate;
                    flowIn += problem.Pumps[p].FlowRateGph;
                }
                if ((startups & (1 << p)) != 0)
                    startupCost += problem.Pumps[p].StartupCostDollars;
            }

            double stepCost = energyCost + startupCost;
            cumulativeCost += stepCost;

            // Clamp to [minLevel, capacity] to match solver behaviour.
            tankLevel = Math.Clamp(tankLevel - hourDemand + flowIn,
                problem.TankMinLevel, problem.TankCapacity);

            double budgetRemaining = Math.Max(0.0, effectiveBudget - cumulativeCost);

            var pumpStatuses = new List<EmPumpStatusEntry>();
            var activeNames = new List<string>();
            for (int p = 0; p < n; p++) {
                bool isOn = (mask & (1 << p)) != 0;
                if (isOn) activeNames.Add(problem.Pumps[p].Name);
                pumpStatuses.Add(new EmPumpStatusEntry(
                    problem.Pumps[p].Name,
                    isOn,
                    isOn ? problem.Pumps[p].FlowRateGph : 0.0,
                    problem.Pumps[p].PowerKw
                ));
            }

            string pumpsLabel = activeNames.Count > 0
                ? string.Join(", ", activeNames) + " ON"
                : "All pumps OFF";

            double fillRatio = problem.TankCapacity > 0
                ? tankLevel / problem.TankCapacity
                : 0.0;

            frames.Add(new API_EmPumpFrame(
                $"Hour {h} [{(isPeak ? "Peak" : "Off-Peak")}]: {pumpsLabel} — tank {tankLevel:F0} gal " +
                $"(budget ${budgetRemaining:F2} remaining)",
                new EmPumpFrameMetrics(
                    h,
                    Math.Round(stepCost, 4),
                    Math.Round(cumulativeCost, 4),
                    Math.Round(effectiveBudget, 2),
                    Math.Round(budgetRemaining, 4),
                    Math.Round(tankLevel, 1),
                    problem.TankCapacity,
                    problem.TankMinLevel,
                    Math.Round(fillRatio, 4),
                    Math.Round(flowIn, 2),
                    Math.Round(hourDemand, 2),
                    isPeak
                ),
                new EmPumpFrameState(pumpStatuses)
            ));

            prevMask = mask;
        }

        return frames;
    }
}
