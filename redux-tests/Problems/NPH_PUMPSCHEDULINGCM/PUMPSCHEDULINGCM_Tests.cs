using Xunit;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGCM;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGCM.Verifiers;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGCM.Solvers;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGCM.Visualizations;
using API.Interfaces;

namespace redux_tests;

#pragma warning disable CS1591

public class PUMPSCHEDULINGCM_Tests {
    // ── Shared instances ──────────────────────────────────────────────────────

    // Simple instance: 1 pump, zero demand, no peak hours.
    // Optimal solution: run no pumps (cost = 0), tank stays at 500 throughout.
    private const string SimpleInstance =
        "((1000,500)," +
        "((0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)," +
        "()," +
        "(0.10,0.05))," +
        "((PumpA,200,5.0,0.0)))";

    // All-zeros schedule for SimpleInstance (no pumps running).
    private const string SimpleCertValid =
        "(0,((PumpA,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)))";

    // Reusable 24-hour all-zero demand curve for constructing malformed-instance fixtures.
    private const string ZeroDemand24 =
        "(0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)";

    // ── Instantiation ─────────────────────────────────────────────────────────

    [Fact]
    public void PUMPSCHEDULINGCM_Default_Instantiation() {
        PUMPSCHEDULINGCM p = new();
        Assert.Equal(10000, p.TankCapacity);
        Assert.Equal(5000, p.TankCurrentLevel);
        Assert.Equal(24, p.DemandGph.Count);
        Assert.Equal(8, p.PeakHours.Count);
        Assert.Equal(0.12, p.OnPeakCostPerKwh);
        Assert.Equal(0.06, p.OffPeakCostPerKwh);
        Assert.Equal(3, p.Pumps.Count);
        Assert.Equal("PumpA", p.Pumps[0].Name);
        Assert.Equal(200, p.Pumps[0].FlowRateGph);
        Assert.Equal(5.0, p.Pumps[0].PowerKw);
        Assert.Equal(2.5, p.Pumps[0].StartupCostDollars);
    }

    [Fact]
    public void PUMPSCHEDULINGCM_Custom_Instantiation() {
        PUMPSCHEDULINGCM p = new(SimpleInstance);
        Assert.Equal(1000, p.TankCapacity);
        Assert.Equal(500, p.TankCurrentLevel);
        Assert.Equal(24, p.DemandGph.Count);
        Assert.All(p.DemandGph, d => Assert.Equal(0.0, d));
        Assert.Empty(p.PeakHours);
        Assert.Equal(0.10, p.OnPeakCostPerKwh);
        Assert.Equal(0.05, p.OffPeakCostPerKwh);
        Assert.Single(p.Pumps);
        Assert.Equal("PumpA", p.Pumps[0].Name);
        Assert.Equal(200, p.Pumps[0].FlowRateGph);
        Assert.Equal(5.0, p.Pumps[0].PowerKw);
        Assert.Equal(0.0, p.Pumps[0].StartupCostDollars);
    }

    [Fact]
    public void PUMPSCHEDULINGCM_Bad_Instance_Throws() {
        Assert.Throws<ProblemParseException>(() => new PUMPSCHEDULINGCM("not-an-instance"));
    }

    [Fact]
    public void PUMPSCHEDULINGCM_Wrong_Demand_Count_Throws() {
        // Only 3 demand values instead of 24.
        string bad = "((1000,500),((100,100,100),(),(0.10,0.05)),((PumpA,200,5.0,0.0)))";
        Assert.Throws<ProblemParseException>(() => new PUMPSCHEDULINGCM(bad));
    }

    // ── Verifier — valid certificates ─────────────────────────────────────────

    [Fact]
    public void PUMPSCHEDULINGCM_Verifier_True_ZeroDemand() {
        PUMPSCHEDULINGCM p = new(SimpleInstance);
        PumpSchedulingCMVerifier v = new();
        Assert.True(v.verify(p, SimpleCertValid));
    }

    [Theory]
    // Wrong reported cost (computed is 0, reported is 99.0).
    [InlineData("(99.0,((PumpA,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)))", false)]
    // Wrong pump name.
    [InlineData("(0,((PumpX,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)))", false)]
    // Too few pumps in schedule (0 pumps listed, need 1).
    [InlineData("(0,())", false)]
    // Pump on for all hours against zero demand: tank overflows (1000+200=1200>1000 at h=0).
    [InlineData("(0,((PumpA,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1)))", false)]
    public void PUMPSCHEDULINGCM_Verifier_False(string certificate, bool expected) {
        PUMPSCHEDULINGCM p = new(SimpleInstance);
        PumpSchedulingCMVerifier v = new();
        Assert.Equal(expected, v.verify(p, certificate));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-cert")]
    [InlineData("(bad")]
    public void PUMPSCHEDULINGCM_Verifier_Malformed_Throws(string certificate) {
        PUMPSCHEDULINGCM p = new(SimpleInstance);
        PumpSchedulingCMVerifier v = new();
        Assert.Throws<CertificateParseException>(() => v.verify(p, certificate));
    }

    // ── Solver ────────────────────────────────────────────────────────────────

    [Fact]
    public void PUMPSCHEDULINGCM_Solver_ZeroDemand_Returns_ZeroCost() {
        PUMPSCHEDULINGCM p = new(SimpleInstance);
        PumpSchedulingCMSolver solver = new();
        string cert = solver.solve(p);
        Assert.Equal(SimpleCertValid, cert);
    }

    // ── Round-trip ────────────────────────────────────────────────────────────

    [Fact]
    public void PUMPSCHEDULINGCM_SolverOutput_PassesVerifier_Simple() {
        PUMPSCHEDULINGCM p = new(SimpleInstance);
        PumpSchedulingCMSolver solver = new();
        PumpSchedulingCMVerifier verifier = new();
        string cert = solver.solve(p);
        Assert.False(string.IsNullOrEmpty(cert));
        Assert.True(verifier.verify(p, cert));
    }

    [Fact]
    public void PUMPSCHEDULINGCM_SolverOutput_PassesVerifier_Default() {
        PUMPSCHEDULINGCM p = new();
        PumpSchedulingCMSolver solver = new();
        PumpSchedulingCMVerifier verifier = new();
        string cert = solver.solve(p);
        Assert.False(string.IsNullOrEmpty(cert));
        Assert.True(verifier.verify(p, cert));
    }

    [Theory]
    // 2 pumps, medium tank, moderate constant demand with a peak window.
    [InlineData("((2000,1000),((300,300,300,300,300,300,300,300,300,300,300,300,300,300,300,300,300,300,300,300,300,300,300,300),(6,7,8),(0.15,0.07)),((PumpA,150,4.0,1.0),(PumpB,250,6.0,2.0)))")]
    // 3 pumps, large tank, demand swinging between low and high blocks, no peak hours.
    [InlineData("((5000,2500),((200,200,200,200,200,200,800,800,800,800,800,800,200,200,200,200,200,200,800,800,800,800,800,800),(),(0.20,0.05)),((P1,100,2.0,0.5),(P2,150,3.0,0.5),(P3,300,7.0,1.5)))")]
    // 4 small pumps, small tank, low flat demand.
    [InlineData("((500,250),((50,50,50,50,50,50,50,50,50,50,50,50,50,50,50,50,50,50,50,50,50,50,50,50),(),(0.10,0.10)),((P1,60,1.0,0.1),(P2,40,0.8,0.1),(P3,80,1.5,0.2),(P4,20,0.5,0.05)))")]
    public void PUMPSCHEDULINGCM_SolverOutput_PassesVerifier_VariedInstances(string instance) {
        PUMPSCHEDULINGCM p = new(instance);
        PumpSchedulingCMSolver solver = new();
        PumpSchedulingCMVerifier verifier = new();
        string cert = solver.solve(p);
        Assert.False(string.IsNullOrEmpty(cert));
        Assert.True(verifier.verify(p, cert));
    }

    // ── Infeasible instance ──────────────────────────────────────────────────

    [Fact]
    public void PUMPSCHEDULINGCM_Solver_Infeasible_Returns_Empty() {
        // Demand of 10000 gph vastly exceeds the tank's starting level (0) plus the
        // single pump's max flow (100 gph) at every hour, so no reachable state
        // survives hour 0 — the DAG has no path from source to any sink.
        string instance =
            "((1000,0)," +
            "((10000,10000,10000,10000,10000,10000,10000,10000,10000,10000,10000,10000," +
            "10000,10000,10000,10000,10000,10000,10000,10000,10000,10000,10000,10000)," +
            "()," +
            "(0.10,0.10))," +
            "((PumpA,100,5.0,1.0)))";
        PUMPSCHEDULINGCM p = new(instance);
        PumpSchedulingCMSolver solver = new();
        string cert = solver.solve(p);
        Assert.Equal(string.Empty, cert);
    }

    // ── Additional class parse-validation ────────────────────────────────────

    [Fact]
    public void PUMPSCHEDULINGCM_ZeroPumps_Throws() {
        string bad = $"((1000,500),({ZeroDemand24},(),(0.10,0.05)),())";
        Assert.Throws<ProblemParseException>(() => new PUMPSCHEDULINGCM(bad));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void PUMPSCHEDULINGCM_NonPositiveCapacity_Throws(double capacity) {
        string bad = $"(({capacity.ToString(System.Globalization.CultureInfo.InvariantCulture)},0),({ZeroDemand24},(),(0.10,0.05)),((PumpA,200,5.0,0.0)))";
        Assert.Throws<ProblemParseException>(() => new PUMPSCHEDULINGCM(bad));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1500)]
    public void PUMPSCHEDULINGCM_CurrentLevelOutOfRange_Throws(double currentLevel) {
        string bad = $"((1000,{currentLevel.ToString(System.Globalization.CultureInfo.InvariantCulture)}),({ZeroDemand24},(),(0.10,0.05)),((PumpA,200,5.0,0.0)))";
        Assert.Throws<ProblemParseException>(() => new PUMPSCHEDULINGCM(bad));
    }

    [Fact]
    public void PUMPSCHEDULINGCM_WrongTopLevelSectionCount_Throws() {
        // Only 2 sections; missing pumps.
        string bad = $"((1000,500),({ZeroDemand24},(),(0.10,0.05)))";
        Assert.Throws<ProblemParseException>(() => new PUMPSCHEDULINGCM(bad));
    }

    [Fact]
    public void PUMPSCHEDULINGCM_MalformedRatesSubsection_Throws() {
        // Rates sub-list has only 1 value instead of 2.
        string bad = $"((1000,500),({ZeroDemand24},(),(0.10)),((PumpA,200,5.0,0.0)))";
        Assert.Throws<ProblemParseException>(() => new PUMPSCHEDULINGCM(bad));
    }

    [Fact]
    public void PUMPSCHEDULINGCM_PumpTupleWrongFieldCount_Throws() {
        // Pump tuple has 3 fields instead of 4 (missing startup cost).
        string bad = $"((1000,500),({ZeroDemand24},(),(0.10,0.05)),((PumpA,200,5.0)))";
        Assert.Throws<ProblemParseException>(() => new PUMPSCHEDULINGCM(bad));
    }

    // ── Verifier — additional rejection cases ────────────────────────────────

    [Fact]
    public void PUMPSCHEDULINGCM_Verifier_NonBinaryHourValue_ReturnsFalse() {
        PUMPSCHEDULINGCM p = new(SimpleInstance);
        PumpSchedulingCMVerifier v = new();
        string cert = "(0,((PumpA,2,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)))";
        Assert.False(v.verify(p, cert));
    }

    [Fact]
    public void PUMPSCHEDULINGCM_Verifier_WrongTopLevelSectionCount_Throws() {
        PUMPSCHEDULINGCM p = new(SimpleInstance);
        PumpSchedulingCMVerifier v = new();
        // 3 top-level sections instead of 2.
        string cert = "(0,(),0)";
        Assert.Throws<CertificateParseException>(() => v.verify(p, cert));
    }

    // ── Visualization ─────────────────────────────────────────────────────────

    [Fact]
    public void PUMPSCHEDULINGCM_Visualization_Typed_Returns_24_Frames_InOrder_With_NonDecreasing_CumulativeCost() {
        PUMPSCHEDULINGCM p = new();
        PumpSchedulingCMSolver solver = new();
        PumpSchedulingCMVisualization visualization = new();

        var steps = solver.GetSteps(p);
        var frames = visualization.StepsVisualization(p, steps);

        Assert.Equal(24, frames.Count);

        double previousCumulative = -1.0;
        for (int h = 0; h < 24; h++) {
            var frame = Assert.IsType<API_PumpFrame>(frames[h]);
            Assert.Equal(h, frame.metrics.hour);
            Assert.True(frame.metrics.cumulativeCost >= previousCumulative);
            previousCumulative = frame.metrics.cumulativeCost;
        }
    }

    [Fact]
    public void PUMPSCHEDULINGCM_Visualization_InterfaceOverride_ReParsesInstance_Returns_24_Frames() {
        // Dispatch through the IVisualization reference so the explicit
        // `IVisualization.StepsVisualization(string,List<object>)` override is exercised
        // (rather than the typed method called directly), confirming it re-parses the
        // instance string itself instead of being short-circuited by the default
        // interface method's empty-steps guard.
        IVisualization visualization = new PumpSchedulingCMVisualization();
        var steps = new List<object> { true };

        var frames = visualization.StepsVisualization(PUMPSCHEDULINGCM.DefaultInstance, steps);

        Assert.Equal(24, frames.Count);
        for (int h = 0; h < 24; h++) {
            var frame = Assert.IsType<API_PumpFrame>(frames[h]);
            Assert.Equal(h, frame.metrics.hour);
        }
    }
}
