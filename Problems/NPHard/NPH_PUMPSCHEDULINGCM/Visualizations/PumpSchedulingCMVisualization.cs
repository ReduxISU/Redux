using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGCM.Solvers;
using SPADE;

namespace API.Problems.NPHard.NPH_PUMPSCHEDULINGCM.Visualizations;

// --- Frame data types (lowercase property names serialize to camelCase JSON) ---

record PumpStatusEntry(string name, bool isOn, double flowGph, double powerKw);

record PumpFrameMetrics(
    int hour,
    double stepCost,
    double cumulativeCost,
    double tankLevel,
    double tankCapacity,
    double tankFillRatio,
    double flowIn,
    double demand,
    bool isPeakHour
);

record PumpFrameState(List<PumpStatusEntry> pumps);

class API_PumpFrame : API_JSON
{
    public string action { get; }
    public PumpFrameMetrics metrics { get; }
    public PumpFrameState state { get; }

    public API_PumpFrame(string action, PumpFrameMetrics metrics, PumpFrameState state)
    {
        this.action = action;
        this.metrics = metrics;
        this.state = state;
    }
}

// --- Visualization ---

class PumpSchedulingCMVisualization : IVisualization<PUMPSCHEDULINGCM>
{
    public string visualizationName { get; } = "Pump Scheduling Cost Minimization — DAG Animation";
    public string visualizationDefinition { get; } =
        "Animates the 24-hour optimal pump schedule, showing per-hour pump states, " +
        "tank levels, and cumulative costs produced by the DAG dynamic programming solver.";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Michael Trosper" };
    public string visualizationType { get; } = "pump";
    public ISolver solver { get; } = new PumpSchedulingCMSolver();

    public API_JSON visualize(PUMPSCHEDULINGCM problem) => new API_empty();

    public API_JSON SolvedVisualization(PUMPSCHEDULINGCM problem, string solution) => new API_empty();

    // Explicitly override the default interface dispatch, which skips the typed method
    // when steps is empty (which it always is since GetSteps() returns [] by default).
    List<API_JSON> IVisualization.StepsVisualization(string instance, List<object> steps)
        => StepsVisualization(new PUMPSCHEDULINGCM(instance), steps);

    public List<API_JSON> StepsVisualization(PUMPSCHEDULINGCM problem, List<object> _steps)
    {
        string certificate = new PumpSchedulingCMSolver().solve(problem);
        if (string.IsNullOrEmpty(certificate))
            return new List<API_JSON>();

        // Parse certificate: (totalCost,((PumpName,h0,...,h23),...))
        int n = problem.Pumps.Count;
        int[] schedMasks = new int[24];

        try
        {
            var cert = new UtilCollection(certificate);
            var certParts = cert.ToList();
            var schedSection = (UtilCollection)certParts[1];

            int pumpIdx = 0;
            foreach (UtilCollection pumpRow in schedSection)
            {
                var parts = pumpRow.ToList();
                for (int h = 0; h < 24 && h + 1 < parts.Count; h++)
                {
                    if (parts[h + 1].ToString().Trim() == "1")
                        schedMasks[h] |= (1 << pumpIdx);
                }
                pumpIdx++;
            }
        }
        catch
        {
            return new List<API_JSON>();
        }

        // Build one frame per hour
        var frames = new List<API_JSON>();
        double tankLevel = problem.TankCurrentLevel;
        double cumulativeCost = 0.0;
        int prevMask = 0;
        int nMasks = 1 << n;

        for (int h = 0; h < 24; h++)
        {
            int mask = schedMasks[h];
            bool isPeak = problem.PeakHours.Contains(h);
            double rate = isPeak ? problem.OnPeakCostPerKwh : problem.OffPeakCostPerKwh;

            double energyCost = 0.0;
            double flowIn = 0.0;
            double startupCost = 0.0;
            int startups = (~prevMask) & mask & (nMasks - 1);

            for (int p = 0; p < n; p++)
            {
                if ((mask & (1 << p)) != 0)
                {
                    energyCost += problem.Pumps[p].PowerKw * rate;
                    flowIn += problem.Pumps[p].FlowRateGph;
                }
                if ((startups & (1 << p)) != 0)
                    startupCost += problem.Pumps[p].StartupCostDollars;
            }

            double stepCost = energyCost + startupCost;
            cumulativeCost += stepCost;
            double hourDemand = problem.DemandGph[h];
            tankLevel = Math.Clamp(tankLevel - hourDemand + flowIn, 0.0, problem.TankCapacity);

            var pumpStatuses = new List<PumpStatusEntry>();
            var activeNames = new List<string>();
            for (int p = 0; p < n; p++)
            {
                bool isOn = (mask & (1 << p)) != 0;
                if (isOn) activeNames.Add(problem.Pumps[p].Name);
                pumpStatuses.Add(new PumpStatusEntry(
                    problem.Pumps[p].Name,
                    isOn,
                    isOn ? problem.Pumps[p].FlowRateGph : 0.0,
                    problem.Pumps[p].PowerKw
                ));
            }

            string pumpsLabel = activeNames.Count > 0
                ? string.Join(", ", activeNames) + " ON"
                : "All pumps OFF";

            frames.Add(new API_PumpFrame(
                $"Hour {h} [{(isPeak ? "Peak" : "Off-Peak")}]: {pumpsLabel} — tank {tankLevel:F0} gal",
                new PumpFrameMetrics(
                    h,
                    Math.Round(stepCost, 4),
                    Math.Round(cumulativeCost, 4),
                    Math.Round(tankLevel, 1),
                    problem.TankCapacity,
                    Math.Round(problem.TankCapacity > 0 ? tankLevel / problem.TankCapacity : 0.0, 4),
                    Math.Round(flowIn, 2),
                    Math.Round(hourDemand, 2),
                    isPeak
                ),
                new PumpFrameState(pumpStatuses)
            ));

            prevMask = mask;
        }

        return frames;
    }
}
