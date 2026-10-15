using API.Interfaces;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGCM;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGEM;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// #804: both Pump Scheduling problems refuse peak hours outside 0 to 23 and negative demand, flow, power, startup cost or
// tariff, which have no meaning in the model.
public class PUMPSCHEDULING_Input_Tests {
    private const string Demand = "(600,600,600,600,600,600,600,600,1000,1000,1000,1000,600,600,600,600,600,1000,1000,1000,1000,1000,600,600)";
    private const string Pumps = "((PumpA,200,5.0,2.5),(PumpB,350,8.5,4.0),(PumpC,500,12.0,6.0))";

    // (demand, peak hours, rates, pumps) for both problems.
    public static TheoryData<string, string, string, string> Bad => new() {
        { Demand, "(8,24)", "(0.12,0.06)", Pumps },                                   // hour 24 does not exist
        { Demand, "(-1,8)", "(0.12,0.06)", Pumps },                                   // nor does hour -1
        { Demand.Replace("(600,", "(-600,"), "(8)", "(0.12,0.06)", Pumps },           // a negative demand
        { Demand, "(8)", "(-0.12,0.06)", Pumps },                                     // a negative on-peak tariff
        { Demand, "(8)", "(0.12,-0.06)", Pumps },                                     // a negative off-peak tariff
        { Demand, "(8)", "(0.12,0.06)", "((PumpA,-200,5.0,2.5))" },                   // a negative flow
        { Demand, "(8)", "(0.12,0.06)", "((PumpA,200,-5.0,2.5))" },                   // a negative power
        { Demand, "(8)", "(0.12,0.06)", "((PumpA,200,5.0,-2.5))" },                   // a negative startup cost
    };

    [Theory]
    [MemberData(nameof(Bad))]
    public void PumpCM_Meaningless_Numbers_Are_A_Parse_Error(string demand, string peaks, string rates, string pumps) {
        Assert.Throws<ProblemParseException>(() => new PUMPSCHEDULINGCM($"((10000,5000),({demand},{peaks},{rates}),{pumps})"));
    }

    [Theory]
    [MemberData(nameof(Bad))]
    public void PumpEM_Meaningless_Numbers_Are_A_Parse_Error(string demand, string peaks, string rates, string pumps) {
        Assert.Throws<ProblemParseException>(() => new PUMPSCHEDULINGEM($"((10000,5000,2000),({demand},{peaks},{rates}),{pumps},0)"));
    }

    // #888: NaN and ±Infinity in any number are a parse error, not a 500 or a meaningless answer. Each template names its numbers in braces.
    private const string Day = "({d},600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600)";
    private const string CmTemplate = "(({c},{l}),(" + Day + ",(8),({on},0.06)),((PumpA,{f},{p},{s})))";
    private const string EmTemplate = "(({c},{l},{m}),(" + Day + ",(8),({on},0.06)),((PumpA,{f},{p},{s})),{b})";

    public static TheoryData<string, string> NonFinite() {
        var data = new TheoryData<string, string>();
        foreach (string bad in new[] { "NaN", "Infinity", "-Infinity" })
            foreach (string field in new[] { "c", "l", "d", "on", "f", "p", "s" })
                data.Add(field, bad);
        return data;
    }

    private static string Fill(string template, string field, string bad) {
        var values = new Dictionary<string, string> { ["c"] = "10000", ["l"] = "5000", ["m"] = "2000", ["d"] = "600", ["on"] = "0.12", ["f"] = "200", ["p"] = "5.0", ["s"] = "2.5", ["b"] = "0" };
        values[field] = bad;
        foreach (var (key, value) in values) template = template.Replace("{" + key + "}", value);
        return template;
    }

    [Theory]
    [MemberData(nameof(NonFinite))]
    public void PumpCM_NonFinite_Numbers_Are_A_Parse_Error(string field, string bad) {
        Assert.Throws<ProblemParseException>(() => new PUMPSCHEDULINGCM(Fill(CmTemplate, field, bad)));
    }

    [Theory]
    [MemberData(nameof(NonFinite))]
    [InlineData("m", "NaN")]
    [InlineData("m", "Infinity")]
    [InlineData("b", "NaN")]
    [InlineData("b", "Infinity")]
    public void PumpEM_NonFinite_Numbers_Are_A_Parse_Error(string field, string bad) {
        Assert.Throws<ProblemParseException>(() => new PUMPSCHEDULINGEM(Fill(EmTemplate, field, bad)));
    }

    [Fact]
    public void Pump_Template_With_Finite_Numbers_Parses() {
        Assert.Equal(10000, new PUMPSCHEDULINGCM(Fill(CmTemplate, "c", "10000")).TankCapacity);
        Assert.Equal(2000, new PUMPSCHEDULINGEM(Fill(EmTemplate, "c", "10000")).TankMinLevel);
    }

    [Fact]
    public void Pump_Zeros_And_The_Edge_Hours_Still_Parse() {
        string zeros = Demand.Replace("(600,", "(0,");
        var cm = new PUMPSCHEDULINGCM($"((10000,5000),({zeros},(0,23),(0,0)),((PumpA,0,0,0)))");
        Assert.Equal([0, 23], cm.PeakHours.Order());
        var em = new PUMPSCHEDULINGEM($"((10000,5000,2000),({zeros},(),(0,0)),((PumpA,0,0,0)),0)");
        Assert.Empty(em.PeakHours);
    }
}
