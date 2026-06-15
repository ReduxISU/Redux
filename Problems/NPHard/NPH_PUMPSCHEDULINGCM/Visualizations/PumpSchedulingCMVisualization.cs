using System;
using System.Collections.Generic;
using System.Linq;
using API.DummyClasses;
using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGCM.Solvers;
using SPADE;

namespace API.Problems.NPHard.NPH_PUMPSCHEDULINGCM.Visualizations;

// ── Problem-specific frame content types ─────────────────────────────────────

public record PumpFrameMetrics(
    int Hour,
    double StepCost,
    double CumulativeCost,
    double TankLevel,
    double TankCapacity,
    double TankFillRatio,
    double FlowIn,
    double Demand,
    bool IsPeakHour
);

public record PumpStatus(string Name, bool IsOn, double FlowGph, double PowerKw);

public record DagNode(int Hour, int Bucket, double Level, int PumpMask, double CumulativeCost);

public record DagState(
    List<DagNode> PathSoFar,
    int CurrentHour,
    int CurrentBucket,
    double CurrentLevel
);

public record PumpFrameState(List<PumpStatus> Pumps, DagState Dag);

// ── Visualization class ───────────────────────────────────────────────────────

class PumpSchedulingCMVisualization
    : IVisualization<PUMPSCHEDULINGCM>,
      IAnimatable<PUMPSCHEDULINGCM>
{
    public string visualizationName { get; } = "Pump Scheduling CM Animation";
    public string visualizationDefinition { get; } =
        "Returns 24 per-hour SolverFrames containing pump on/off status, tank level, " +
        "step costs, and the optimal DAG path traced hour by hour.";
    public string visualizationType { get; } = "PumpScheduling";
    public string source { get; } = "";
    public string[] contributors { get; } = { "SARE 2026 Team" };
    public ISolver solver { get; } = new PumpSchedulingCMSolver();

    public PumpSchedulingCMVisualization() { }

    // IVisualization stubs — Redux_GUI compatibility; not used by SARE 2026 frontend
    public API_JSON visualize(PUMPSCHEDULINGCM problem) => new API_empty();
    public API_JSON SolvedVisualization(PUMPSCHEDULINGCM problem, string solution) => new API_empty();
    public List<API_JSON> StepsVisualization(PUMPSCHEDULINGCM problem, List<object> steps) => new();

    // ── IAnimatable<PUMPSCHEDULINGCM> ─────────────────────────────────────────

    public List<SolverFrame> GetAnimationFrames(PUMPSCHEDULINGCM problem)
    {
        string cert = new PumpSchedulingCMSolver().solve(problem);
        if (string.IsNullOrEmpty(cert))
            return new List<SolverFrame>();

        int[] schedMasks = ParseCertificateMasks(cert, problem.Pumps.Count);
        return BuildFrames(problem, schedMasks);
    }

    // ── Certificate parser ────────────────────────────────────────────────────

    // Certificate format: (cost,((PumpName,b0,...,b23),...))
    private static int[] ParseCertificateMasks(string cert, int pumpCount)
    {
        var top = new UtilCollection(cert).ToList();
        var pumpRows = ((UtilCollection)top[1]).ToList();
        int[] masks = new int[24];

        for (int p = 0; p < pumpRows.Count && p < pumpCount; p++)
        {
            var parts = ((UtilCollection)pumpRows[p]).ToList();
            for (int h = 0; h < 24; h++)
                if (parts[h + 1].ToString().Trim() == "1")
                    masks[h] |= (1 << p);
        }

        return masks;
    }

    // ── Frame builder ─────────────────────────────────────────────────────────

    private static List<SolverFrame> BuildFrames(PUMPSCHEDULINGCM problem, int[] schedMasks)
    {
        int n = problem.Pumps.Count;
        double bucketSize = problem.TankCapacity / 1000.0;

        int ToBucket(double level) =>
            Math.Clamp((int)Math.Round(level / bucketSize), 0, 1000);

        double tankLevel = problem.TankCurrentLevel;
        double cumCost = 0.0;
        int prevMask = 0;

        var dagPath = new List<DagNode>
        {
            new(0, ToBucket(tankLevel), Math.Round(tankLevel, 2), 0, 0.0)
        };

        var frames = new List<SolverFrame>();

        for (int h = 0; h < 24; h++)
        {
            int mask = schedMasks[h];

            double flowIn = 0.0, kw = 0.0;
            for (int p = 0; p < n; p++)
                if ((mask & (1 << p)) != 0)
                {
                    flowIn += problem.Pumps[p].FlowRateGph;
                    kw += problem.Pumps[p].PowerKw;
                }

            double rate = problem.PeakHours.Contains(h)
                ? problem.OnPeakCostPerKwh
                : problem.OffPeakCostPerKwh;

            int startups = (~prevMask) & mask & ((1 << n) - 1);
            double stepCost = kw * rate + Enumerable.Range(0, n)
                .Where(p => (startups & (1 << p)) != 0)
                .Sum(p => problem.Pumps[p].StartupCostDollars);

            cumCost += stepCost;
            tankLevel = tankLevel + flowIn - problem.DemandGph[h];
            prevMask = mask;

            int bucket = ToBucket(tankLevel);
            double roundedLevel = Math.Round(tankLevel, 2);

            dagPath.Add(new(h + 1, bucket, roundedLevel, mask, Math.Round(cumCost, 4)));

            var pumpStatuses = problem.Pumps.Select((pump, p) => new PumpStatus(
                pump.Name, (mask & (1 << p)) != 0, pump.FlowRateGph, pump.PowerKw
            )).ToList();

            var onNames = pumpStatuses.Where(s => s.IsOn).Select(s => s.Name).ToList();
            string action = onNames.Count > 0
                ? $"Hour {h}: {string.Join(", ", onNames)} ON"
                : $"Hour {h}: All pumps OFF";

            frames.Add(new SolverFrame(
                Action: action,
                Metrics: new PumpFrameMetrics(
                    Hour: h,
                    StepCost: Math.Round(stepCost, 4),
                    CumulativeCost: Math.Round(cumCost, 4),
                    TankLevel: roundedLevel,
                    TankCapacity: problem.TankCapacity,
                    TankFillRatio: Math.Round(tankLevel / problem.TankCapacity, 4),
                    FlowIn: flowIn,
                    Demand: problem.DemandGph[h],
                    IsPeakHour: problem.PeakHours.Contains(h)
                ),
                State: new PumpFrameState(
                    Pumps: pumpStatuses,
                    Dag: new DagState(
                        PathSoFar: new List<DagNode>(dagPath),
                        CurrentHour: h + 1,
                        CurrentBucket: bucket,
                        CurrentLevel: roundedLevel
                    )
                )
            ));
        }

        return frames;
    }
}
