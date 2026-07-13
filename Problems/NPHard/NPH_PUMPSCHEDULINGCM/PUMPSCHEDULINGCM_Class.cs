using System.Collections.Generic;
using System.Linq;
using API.Interfaces;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGCM.Solvers;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGCM.Verifiers;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGCM.Visualizations;
using SPADE;

namespace API.Problems.NPHard.NPH_PUMPSCHEDULINGCM;

record PumpData(string Name, double FlowRateGph, double PowerKw, double StartupCostDollars);

class PUMPSCHEDULINGCM : IProblem<PumpSchedulingCMSolver, PumpSchedulingCMVerifier, PumpSchedulingCMVisualization>
{
    public string problemName { get; } = "Pump Scheduling Cost Minimization";
    public string problemLink { get; } = "";
    public string formalDefinition { get; } =
        "An instance of the Pump Scheduling Cost Minimization is defined as a 3-tuple (T,D,P) where:\r\n" +
        "\tthe tank T=(c,v) is an ordered pair where c is an \r\n" + 
        "\tinteger representing the capacity of the tank, \r\n" + 
        "\tand v is the current volume within the tank; \r\n" +
        "\tthe demand configuration D=((d_0,...,d_{23}),\r\n" + 
        "\t(h_1,...,h_k),(r_on},r_off)) is an ordered triple \r\n" + 
        "\twhere (d_0,...,d_{23}) is a list of 24 integers \r\n" + 
        "\trepresenting the water demand in gallons per hour\r\n" +
        "\tfor each hour of the day, (h_1,...,h_k) is a list \r\n" +
        "\tof integers representing the peak hours (using \r\n" +
        "\t0-based indexing), and (r_on,r_off) is an ordered\r\n" + 
        "\tpair of real numbers representing the \r\n" +
        "\ton-peak and off-peak energy costs per kWh; \r\n" +
        "\tand the pumps\r\n" +
        "\tP=((n_1,f_1,p_1,s_1),...,(n_m,f_m,p_m,s_m)) is a\r\n" +
        "\tlist of m ordered 4-tuples where each 4-tuple \r\n" +
        "\t(n_i,f_i,p_i,s_i) represents a pump with name \r\n" +
        "\tn_i, flow rate f_i in gallons per hour, power \r\n" +
        "\tconsumption p_i in kW, and startup cost s_i \r\n" +
        "\tin dollars.\r\n" +
        "Note: any water (e.g., gallons, liters, etc.) and cost units (e.g., $, euros, etc.) are assumed to be consistent throughout the instance definition";
    public string problemDefinition { get; } =
        "Determine which pumps to activate each hour over a 24-hour period so that water demand is met, " +
        "the storage tank never overflows or fails to supply the water demand, and the total energy cost (based on peak/off-peak tariffs " +
        "plus pump startup costs) is minimized.";
    public string source { get; } = "";
    public string wikiName { get; } = "";
    public string[] contributors { get; } = { "Michael Trosper" };

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
    public PumpSchedulingCMVisualization defaultVisualization { get; } = new();

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
