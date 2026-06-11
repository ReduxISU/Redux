using System;
using System.Collections.Generic;
using System.Linq;
using API.Interfaces;
using API.DummyClasses;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGCM.Solvers;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGCM.Verifiers;

namespace API.Problems.NPHard.NPH_PUMPSCHEDULINGCM;

record PumpData(string Name, double FlowRateGph, double PowerKw, double StartupCostDollars);

class PUMPSCHEDULINGCM : IProblem<PumpSchedulingCMSolver, PumpSchedulingCMVerifier, DummyVisualization>
{
    // --- Fields ---
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

    public string instanceFormat { get; } =
        "({tank_capacity_gal,tank_current_level_gal}," +
        "{(demand_h0,...,demand_h23),(peak_h0,...),(on_peak_cost_per_kwh,off_peak_cost_per_kwh)}," +
        "{(pump_name,flow_gph,kw,startup_cost_dollars),...}). " +
        "Example: ({5000,2000},{(300,...,340),(8,9,10,11),(0.12,0.06)},{(PumpA,200,5.0,2.5),(PumpB,350,8.5,4.0)})";

    public string certificateFormat { get; } =
        "(total_cost_dollars,{(pump_name,h0,...,h23),...}) where each h_i is 0 (off) or 1 (on). " +
        "Example: (63.28,{(PumpA,0,1,1,...,0),(PumpB,1,0,0,...,1)})";

    private static readonly string _defaultInstance =
        "({10000,5000}," +
        "{(600,600,600,600,600,600,600,600,1000,1000,1000,1000,600,600,600,600,600,1000,1000,1000,1000,1000,600,600)," +
        "(8,9,10,11,17,18,19,20),(0.12,0.06)}," +
        "{(PumpA,200,5.0,2.5),(PumpB,350,8.5,4.0),(PumpC,500,12.0,6.0)})";

    public string defaultInstance { get; } = _defaultInstance;
    public string instance { get; set; } = string.Empty;

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

    // --- Constructors ---
    public PUMPSCHEDULINGCM() : this(_defaultInstance) { }

    public PUMPSCHEDULINGCM(string instanceString)
    {
        instance = instanceString;

        string s = instanceString.Trim();
        if (s.StartsWith("(") && s.EndsWith(")"))
            s = s[1..^1].Trim();

        var sections = SplitTopLevel(s, ',');
        if (sections.Count != 3)
            throw new ArgumentException(
                $"Instance must have 3 sections (tank, demand, pumps); got {sections.Count}.");

        ParseTank(sections[0].Trim());
        ParseDemand(sections[1].Trim());
        ParsePumps(sections[2].Trim());
    }

    // --- Parsers ---

    private void ParseTank(string s)
    {
        s = StripBraces(s);
        var parts = SplitTopLevel(s, ',');
        if (parts.Count != 2)
            throw new ArgumentException("Tank section must be {capacity,currentLevel}.");
        TankCapacity = ParseDouble(parts[0]);
        TankCurrentLevel = ParseDouble(parts[1]);
        if (TankCapacity <= 0)
            throw new ArgumentException("Tank capacity must be positive.");
        if (TankCurrentLevel < 0 || TankCurrentLevel > TankCapacity)
            throw new ArgumentException("Tank current level must be in [0, capacity].");
    }

    private void ParseDemand(string s)
    {
        s = StripBraces(s);
        var tuples = SplitTopLevel(s, ',');
        if (tuples.Count != 3)
            throw new ArgumentException(
                "Demand section must have 3 tuples: (demands),(peak_hours),(costs).");

        DemandGph = SplitTopLevel(StripParens(tuples[0]), ',')
            .Select(p => ParseDouble(p))
            .ToList();
        if (DemandGph.Count != 24)
            throw new ArgumentException(
                $"Demand curve must have exactly 24 values; got {DemandGph.Count}.");

        PeakHours = SplitTopLevel(StripParens(tuples[1]), ',')
            .Select(p => int.Parse(p.Trim()))
            .ToHashSet();

        var costs = SplitTopLevel(StripParens(tuples[2]), ',');
        if (costs.Count != 2)
            throw new ArgumentException("Cost tuple must be (on_peak_rate,off_peak_rate).");
        OnPeakCostPerKwh = ParseDouble(costs[0]);
        OffPeakCostPerKwh = ParseDouble(costs[1]);
    }

    private void ParsePumps(string s)
    {
        s = StripBraces(s);
        var pumpTuples = SplitTopLevel(s, ',');
        foreach (var pt in pumpTuples)
        {
            var parts = SplitTopLevel(StripParens(pt.Trim()), ',');
            if (parts.Count != 4)
                throw new ArgumentException(
                    $"Each pump needs 4 fields (name,flow_gph,kw,startup_cost); got {parts.Count} in '{pt}'.");
            Pumps.Add(new PumpData(
                Name:               parts[0].Trim(),
                FlowRateGph:        ParseDouble(parts[1]),
                PowerKw:            ParseDouble(parts[2]),
                StartupCostDollars: ParseDouble(parts[3])
            ));
        }
        if (Pumps.Count == 0)
            throw new ArgumentException("At least one pump is required.");
    }

    // --- Shared parsing utilities (internal so solver/verifier can reuse) ---

    // Splits s at top-level (depth=0) occurrences of sep, respecting (), {}, [].
    internal static List<string> SplitTopLevel(string s, char sep)
    {
        var parts = new List<string>();
        int depth = 0;
        var cur = new System.Text.StringBuilder();
        foreach (char c in s)
        {
            if (c is '(' or '{' or '[') depth++;
            else if (c is ')' or '}' or ']') depth--;
            if (c == sep && depth == 0) { parts.Add(cur.ToString()); cur.Clear(); }
            else cur.Append(c);
        }
        if (cur.Length > 0) parts.Add(cur.ToString());
        return parts;
    }

    // Strips outermost { } if present.
    internal static string StripBraces(string s)
    {
        s = s.Trim();
        return s.StartsWith("{") && s.EndsWith("}") ? s[1..^1].Trim() : s;
    }

    // Strips outermost ( ) if present.
    internal static string StripParens(string s)
    {
        s = s.Trim();
        return s.StartsWith("(") && s.EndsWith(")") ? s[1..^1].Trim() : s;
    }

    private static double ParseDouble(string s) =>
        double.Parse(s.Trim(), System.Globalization.CultureInfo.InvariantCulture);
}
