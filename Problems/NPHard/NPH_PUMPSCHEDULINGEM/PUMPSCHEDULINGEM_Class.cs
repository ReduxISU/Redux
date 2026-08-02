using System.Collections.Generic;
using System.Linq;
using API.Interfaces;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGEM.Solvers;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGEM.Verifiers;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGEM.Visualizations;
using SPADE;

namespace API.Problems.NPHard.NPH_PUMPSCHEDULINGEM;

record EmPumpData(string Name, double FlowRateGph, double PowerKw, double StartupCostDollars);

class PUMPSCHEDULINGEM : IProblem<PumpSchedulingEMSolver, PumpSchedulingEMVerifier, PumpSchedulingEMVisualization>
{
    public string problemName { get; } = "Pump Scheduling Emergency Resilience";
    public string problemLink { get; } = "";
    public string formalDefinition { get; } =
        "An instance of the Pump Scheduling Emergency Resilience is defined as a 4-tuple (T,D,P,B) where:\r\n" +
        "\tthe tank T=(c,v,m) is an ordered triple where c is an\r\n" +
        "\tinteger representing the capacity of the tank,\r\n" +
        "\tv is the current volume within the tank, and\r\n" +
        "\tm is the minimum allowed volume in the tank;\r\n" +
        "\tthe demand configuration D=((d_0,...,d_{23}),\r\n" +
        "\t(h_1,...,h_k),(r_on,r_off)) is an ordered triple\r\n" +
        "\twhere (d_0,...,d_{23}) is a list of 24 integers\r\n" +
        "\trepresenting the water demand in gallons per hour\r\n" +
        "\tfor each hour of the day, (h_1,...,h_k) is a list\r\n" +
        "\tof integers representing the peak hours (using\r\n" +
        "\t0-based indexing), and (r_on,r_off) is an ordered\r\n" +
        "\tpair of real numbers representing the\r\n" +
        "\ton-peak and off-peak energy costs per kWh;\r\n" +
        "\tthe pumps\r\n" +
        "\tP=((n_1,f_1,p_1,s_1),...,(n_m,f_m,p_m,s_m)) is a\r\n" +
        "\tlist of m ordered 4-tuples where each 4-tuple\r\n" +
        "\t(n_i,f_i,p_i,s_i) represents a pump with name\r\n" +
        "\tn_i, flow rate f_i in gallons per hour, power\r\n" +
        "\tconsumption p_i in kW, and startup cost s_i\r\n" +
        "\tin dollars;\r\n" +
        "\tand the budget B is a non-negative real number\r\n" +
        "\trepresenting the maximum allowable total cost in\r\n" +
        "\tdollars, where B=0 denotes that the budget is\r\n" +
        "\tauto-computed as 1.5 times the optimal cost of\r\n" +
        "\tthe corresponding cost-minimization instance.\r\n" +
        "Note: any water (e.g., gallons, liters, etc.) and cost units (e.g., $, euros, etc.) are assumed to be consistent throughout the instance definition";
    public string problemDefinition { get; } =
        "Determine which pumps to activate each hour over a 24-hour period so that: " +
        "the storage tank never drops below its minimum level or exceeds its capacity, " +
        "budget total energy cost (tariffs plus startup costs) stays within the given budget, " +
        "and the cumulative water stored across all hours is maximized. " +
        "This models emergency resilience scenarios where maximizing stored water supply " +
        "is prioritized within an operational cost constraint.";
    public string source { get; } = "";
    public string wikiName { get; } = "";
    public string[] contributors { get; } = { "Michael Trosper" };
    // Declared, not derived. Correctly filed under Problems/NPHard/ already; this
    // makes the folder's claim machine-checkable instead of implicit.
    public ComplexityClass complexityClass { get; } = ComplexityClass.NPHard;

    // 4-section tuple: Tank (capacity, currentLevel, minLevel), Demand config, Pumps, Budget.
    // Budget = 0 means auto-compute as 1.5× the cost-minimization optimum.
    public const string InstanceGrammar =
        "{(T,D,P,B) | T is list, D is list, P is list, B is number}";

    public const string DefaultInstance =
        "((10000,5000,2000)," +
        "((600,600,600,600,600,600,600,600,1000,1000,1000,1000,600,600,600,600,600,1000,1000,1000,1000,1000,600,600)," +
        "(8,9,10,11,17,18,19,20)," +
        "(0.12,0.06))," +
        "((PumpA,200,5.0,2.5),(PumpB,350,8.5,4.0),(PumpC,500,12.0,6.0))," +
        "0)";

    public string defaultInstance { get; } = DefaultInstance;
    public string instance { get; set; } = string.Empty;

    public string instanceFormat { get; } =
        $"Format: {InstanceGrammar}  Example: {DefaultInstance}";

    public string certificateFormat { get; } =
        $"Format: {PumpSchedulingEMVerifier.CertificateGrammar}  Example: {PumpSchedulingEMVerifier.CertificateExample}";

    // --- Parsed properties ---
    public double TankCapacity     { get; private set; }
    public double TankCurrentLevel { get; private set; }
    public double TankMinLevel     { get; private set; }
    public List<double> DemandGph  { get; private set; } = new();
    public HashSet<int> PeakHours  { get; private set; } = new();
    public double OnPeakCostPerKwh  { get; private set; }
    public double OffPeakCostPerKwh { get; private set; }
    public List<EmPumpData> Pumps   { get; private set; } = new();
    // 0 means auto-compute budget as 1.5× cost-minimization optimum.
    public double BudgetLimitDollars { get; private set; }

    public PumpSchedulingEMSolver       defaultSolver        { get; } = new();
    public PumpSchedulingEMVerifier     defaultVerifier      { get; } = new();
    public PumpSchedulingEMVisualization defaultVisualization { get; } = new();

    public PUMPSCHEDULINGEM() : this(DefaultInstance) { }

    public PUMPSCHEDULINGEM(string input)
    {
        instance = input;

        UtilCollection parsed;
        try {
            parsed = new UtilCollection(input);
        } catch (Exception ex) {
            throw new ProblemParseException(problemName, input, ex.Message);
        }

        try {
            var sections = parsed.ToList();
            if (sections.Count != 4)
                throw new ProblemParseException(problemName, input,
                    "Instance must have 4 sections: (tank),(demand),(pumps),budget.");

            // Section 0: Tank — (capacity, currentLevel, minLevel)
            var tank = ((UtilCollection)sections[0]).ToList();
            if (tank.Count != 3)
                throw new ProblemParseException(problemName, input,
                    "Tank section must have exactly 3 values: (capacity,currentLevel,minLevel).");
            TankCapacity     = ParseDouble(tank[0].ToString());
            TankCurrentLevel = ParseDouble(tank[1].ToString());
            TankMinLevel     = ParseDouble(tank[2].ToString());

            if (TankCapacity <= 0)
                throw new ProblemParseException(problemName, input, "Tank capacity must be positive.");
            if (TankMinLevel < 0 || TankMinLevel >= TankCapacity)
                throw new ProblemParseException(problemName, input,
                    "Tank minimum level must be in [0, capacity).");
            if (TankCurrentLevel < TankMinLevel || TankCurrentLevel > TankCapacity)
                throw new ProblemParseException(problemName, input,
                    "Tank current level must be within [minLevel, capacity].");

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
                Pumps.Add(new EmPumpData(
                    Name:               parts[0].ToString().Trim(),
                    FlowRateGph:        ParseDouble(parts[1].ToString()),
                    PowerKw:            ParseDouble(parts[2].ToString()),
                    StartupCostDollars: ParseDouble(parts[3].ToString())
                ));
            }
            if (Pumps.Count == 0)
                throw new ProblemParseException(problemName, input, "At least one pump is required.");

            // Section 3: Budget (scalar, not a nested list)
            BudgetLimitDollars = ParseDouble(sections[3].ToString());
            if (BudgetLimitDollars < 0)
                throw new ProblemParseException(problemName, input,
                    "Budget must be >= 0 (use 0 to auto-compute as 1.5× the cost-minimization optimum).");
        }
        catch (ProblemParseException) { throw; }
        catch (Exception ex) {
            throw new ProblemParseException(problemName, input, ex.Message);
        }
    }

    private static double ParseDouble(string s) =>
        double.Parse(s.Trim(), System.Globalization.CultureInfo.InvariantCulture);
}
