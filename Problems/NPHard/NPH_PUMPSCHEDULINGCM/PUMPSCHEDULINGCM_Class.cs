using System.Collections.Generic;
using System.Linq;
using API.Interfaces;
using API.DummyClasses;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGCM.Solvers;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGCM.Verifiers;
using SPADE;

namespace API.Problems.NPHard.NPH_PUMPSCHEDULINGCM;

record PumpData(string Name, double FlowRateGph, double PowerKw, double StartupCostDollars);

class PUMPSCHEDULINGCM : IProblem<PumpSchedulingCMSolver, PumpSchedulingCMVerifier, DummyVisualization>
{
    public string problemName { get; } = "Pump Scheduling (Cost Minimization)";
    public string problemLink { get; } = "";
    public string formalDefinition { get; } =
        "Given a storage tank, hourly water demand curve, time-of-use electricity tariff, and a set of pumps " +
        "each with a flow rate, power draw, and startup cost, find the 24-hour on/off schedule for each pump " +
        "that satisfies demand, keeps the tank within capacity, and minimizes total electricity cost.";
    public string problemDefinition { get; } =
        "Determine which pumps to activate each hour over a 24-hour period so that water demand is met, " +
        "the storage tank never overflows or empties, and the total energy cost (based on peak/off-peak tariffs " +
        "plus pump startup costs) is minimized.";
    public string source { get; } = "";
    public string wikiName { get; } = "";
    public string[] contributors { get; } = { "SARE 2026 Team" };

    // Grammar: 3-section tuple — Tank, Demand config, Pumps.
    // D nests demand curve, peak hours, and tariff rates together as one section.
    public const string InstanceGrammar =
        "{(T,D,P) | T is list, D is list, P is list}";

    public const string DefaultInstance =
        "((10000,5000)," +
        "((600,600,600,600,600,600,600,600,1000,1000,1000,1000,600,600,600,600,600,1000,1000,1000,1000,1000,600,600)," +
        "(8,9,10,11,17,18,19,20)," +
        "(0.12,0.06))," +
        "((PumpA,200,5.0,2.5),(PumpB,350,8.5,4.0),(PumpC,500,12.0,6.0)))";

    public string defaultInstance { get; } = DefaultInstance;
    public string instance { get; set; } = string.Empty;

    public string instanceFormat { get; } =
        $"Format: {InstanceGrammar}  Example: {DefaultInstance}";

    public string certificateFormat { get; } =
        $"Format: {PumpSchedulingCMVerifier.CertificateGrammar}  Example: {PumpSchedulingCMVerifier.CertificateExample}";

    // --- Parsed properties ---
    public double TankCapacity { get; private set; }
    public double TankCurrentLevel { get; private set; }
    public List<double> DemandGph { get; private set; } = new();
    public HashSet<int> PeakHours { get; private set; } = new();
    public double OnPeakCostPerKwh { get; private set; }
    public double OffPeakCostPerKwh { get; private set; }
    public List<PumpData> Pumps { get; private set; } = new();

    public PumpSchedulingCMSolver defaultSolver { get; } = new();
    public PumpSchedulingCMVerifier defaultVerifier { get; } = new();
    public DummyVisualization defaultVisualization { get; } = new();

    public PUMPSCHEDULINGCM() : this(DefaultInstance) { }

    public PUMPSCHEDULINGCM(string input)
    {
        instance = input;

        // UtilCollection handles nested bracket/comma structures directly.
        // StringParser's grammar language only supports flat lists, so we use
        // UtilCollection for navigation instead.
        UtilCollection parsed;
        try {
            parsed = new UtilCollection(input);
        } catch (Exception ex) {
            throw new ProblemParseException(problemName, input, ex.Message);
        }

        try {
            var sections = parsed.ToList();
            if (sections.Count != 3)
                throw new ProblemParseException(problemName, input,
                    "Instance must have 3 sections: (tank),(demand),(pumps).");

            // Section 0: Tank — (capacity, currentLevel)
            var tank = ((UtilCollection)sections[0]).ToList();
            if (tank.Count != 2)
                throw new ProblemParseException(problemName, input,
                    "Tank section must have exactly 2 values: (capacity,currentLevel).");
            TankCapacity     = ParseDouble(tank[0].ToString());
            TankCurrentLevel = ParseDouble(tank[1].ToString());
            if (TankCapacity <= 0)
                throw new ProblemParseException(problemName, input, "Tank capacity must be positive.");
            if (TankCurrentLevel < 0 || TankCurrentLevel > TankCapacity)
                throw new ProblemParseException(problemName, input,
                    "Tank current level must be within [0, capacity].");

            // Section 1: Demand — ((d0,...,d23),(peak_hours,...),(on_rate,off_rate))
            var dSections = ((UtilCollection)sections[1]).ToList();
            if (dSections.Count != 3)
                throw new ProblemParseException(problemName, input,
                    "Demand section must have 3 sub-lists: (demands),(peak_hours),(rates).");

            DemandGph = ((UtilCollection)dSections[0]).ToList()
                .Select(x => ParseDouble(x.ToString()))
                .ToList();
            if (DemandGph.Count != 24)
                throw new ProblemParseException(problemName, input,
                    $"Demand curve must have exactly 24 values; got {DemandGph.Count}.");

            var hList = ((UtilCollection)dSections[1]).ToList();
            PeakHours = hList.Count == 0
                ? new HashSet<int>()
                : hList.Select(x => int.Parse(x.ToString().Trim())).ToHashSet();

            var costs = ((UtilCollection)dSections[2]).ToList();
            if (costs.Count != 2)
                throw new ProblemParseException(problemName, input,
                    "Rate sub-list must have exactly 2 values: (on_peak_rate,off_peak_rate).");
            OnPeakCostPerKwh  = ParseDouble(costs[0].ToString());
            OffPeakCostPerKwh = ParseDouble(costs[1].ToString());

            // Section 2: Pumps — ((name,flow_gph,kw,startup_cost),...)
            foreach (UtilCollection pump in (UtilCollection)sections[2])
            {
                var parts = pump.ToList();
                if (parts.Count != 4)
                    throw new ProblemParseException(problemName, input,
                        $"Each pump needs 4 fields (name,flow_gph,kw,startup_cost); got {parts.Count}.");
                Pumps.Add(new PumpData(
                    Name:               parts[0].ToString().Trim(),
                    FlowRateGph:        ParseDouble(parts[1].ToString()),
                    PowerKw:            ParseDouble(parts[2].ToString()),
                    StartupCostDollars: ParseDouble(parts[3].ToString())
                ));
            }
            if (Pumps.Count == 0)
                throw new ProblemParseException(problemName, input, "At least one pump is required.");
        }
        catch (ProblemParseException) { throw; }
        catch (Exception ex) {
            throw new ProblemParseException(problemName, input, ex.Message);
        }
    }

    private static double ParseDouble(string s) =>
        double.Parse(s.Trim(), System.Globalization.CultureInfo.InvariantCulture);
}
