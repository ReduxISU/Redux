using Xunit;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGEM;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGEM.Verifiers;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGEM.Solvers;
using API.Interfaces;

namespace redux_tests;

#pragma warning disable CS1591

public class PUMPSCHEDULINGEM_Tests
{
    // ── Shared instances ──────────────────────────────────────────────────────

    // Simple instance: 1 pump, zero demand, no peak hours, explicit budget.
    // Tank starts at 500, min = 0, capacity = 1000.
    // Pump adds 200 gph; with zero demand and capacity 1000, pump can run only
    // hours 0 and 1 before tank is full (500→700→900; hour 2: 900+200=1100 > 1000).
    // Budget $10 easily covers any 24-hour schedule.
    private const string SimpleInstance =
        "((1000,500,0)," +
        "((0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)," +
        "()," +
        "(0.10,0.05))," +
        "((PumpA,200,5.0,0.0))," +
        "10.0)";

    // Valid certificate for SimpleInstance: pump off all hours (cost=0 ≤ budget=10.0).
    // Tank stays at 500 throughout — a feasible (not optimal) schedule.
    private const string SimpleCertValid =
        "(10.0,0.0,((PumpA,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)))";

    // ── Instantiation ─────────────────────────────────────────────────────────

    [Fact]
    public void PUMPSCHEDULINGEM_Default_Instantiation()
    {
        PUMPSCHEDULINGEM p = new();
        Assert.Equal(10000, p.TankCapacity);
        Assert.Equal(5000,  p.TankCurrentLevel);
        Assert.Equal(2000,  p.TankMinLevel);
        Assert.Equal(24,    p.DemandGph.Count);
        Assert.Equal(8,     p.PeakHours.Count);
        Assert.Equal(0.12,  p.OnPeakCostPerKwh);
        Assert.Equal(0.06,  p.OffPeakCostPerKwh);
        Assert.Equal(3,     p.Pumps.Count);
        Assert.Equal("PumpA", p.Pumps[0].Name);
        Assert.Equal(200,   p.Pumps[0].FlowRateGph);
        Assert.Equal(5.0,   p.Pumps[0].PowerKw);
        Assert.Equal(2.5,   p.Pumps[0].StartupCostDollars);
        Assert.Equal(0.0,   p.BudgetLimitDollars); // 0 = auto
    }

    [Fact]
    public void PUMPSCHEDULINGEM_Custom_Instantiation()
    {
        PUMPSCHEDULINGEM p = new(SimpleInstance);
        Assert.Equal(1000,  p.TankCapacity);
        Assert.Equal(500,   p.TankCurrentLevel);
        Assert.Equal(0,     p.TankMinLevel);
        Assert.Equal(24,    p.DemandGph.Count);
        Assert.All(p.DemandGph, d => Assert.Equal(0.0, d));
        Assert.Empty(p.PeakHours);
        Assert.Equal(0.10,  p.OnPeakCostPerKwh);
        Assert.Equal(0.05,  p.OffPeakCostPerKwh);
        Assert.Single(p.Pumps);
        Assert.Equal("PumpA", p.Pumps[0].Name);
        Assert.Equal(200,   p.Pumps[0].FlowRateGph);
        Assert.Equal(5.0,   p.Pumps[0].PowerKw);
        Assert.Equal(0.0,   p.Pumps[0].StartupCostDollars);
        Assert.Equal(10.0,  p.BudgetLimitDollars);
    }

    [Fact]
    public void PUMPSCHEDULINGEM_Bad_Instance_Throws()
    {
        Assert.Throws<ProblemParseException>(() => new PUMPSCHEDULINGEM("not-an-instance"));
    }

    [Fact]
    public void PUMPSCHEDULINGEM_Wrong_Section_Count_Throws()
    {
        // Only 3 sections, missing budget.
        string bad =
            "((1000,500,0)," +
            "((0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0),()," +
            "(0.10,0.05)),((PumpA,200,5.0,0.0)))";
        Assert.Throws<ProblemParseException>(() => new PUMPSCHEDULINGEM(bad));
    }

    [Fact]
    public void PUMPSCHEDULINGEM_Wrong_Demand_Count_Throws()
    {
        // Only 3 demand values instead of 24.
        string bad =
            "((1000,500,0),((100,100,100),(),(0.10,0.05)),((PumpA,200,5.0,0.0)),10.0)";
        Assert.Throws<ProblemParseException>(() => new PUMPSCHEDULINGEM(bad));
    }

    [Fact]
    public void PUMPSCHEDULINGEM_Negative_Budget_Throws()
    {
        string bad =
            "((1000,500,0)," +
            "((0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0),()," +
            "(0.10,0.05)),((PumpA,200,5.0,0.0)),-5.0)";
        Assert.Throws<ProblemParseException>(() => new PUMPSCHEDULINGEM(bad));
    }

    // ── Verifier — valid certificates ─────────────────────────────────────────

    [Fact]
    public void PUMPSCHEDULINGEM_Verifier_True_AllOff()
    {
        PUMPSCHEDULINGEM p = new(SimpleInstance);
        PumpSchedulingEMVerifier v = new();
        Assert.True(v.verify(p, SimpleCertValid));
    }

    [Theory]
    // Budget in cert (10.0) exceeds explicit instance budget (5.0) — invalid.
    [InlineData("(10.0,0.0,((PumpA,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)))", false)]
    // Wrong reported cost (computed is 0, reported is 5.0).
    [InlineData("(5.0,5.0,((PumpA,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)))", false)]
    // Wrong pump name.
    [InlineData("(5.0,0.0,((PumpX,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)))", false)]
    // Too few pumps (0 pumps listed, need 1).
    [InlineData("(5.0,0.0,())", false)]
    // Pump on all hours — cost = 24×5×0.05 = 6.0 but budget cert says 5.0 — exceeds budget.
    [InlineData("(5.0,6.0,((PumpA,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1)))", false)]
    public void PUMPSCHEDULINGEM_Verifier_False_With_Tighter_Budget(string certificate, bool expected)
    {
        // Use an instance that has an explicit $5 budget.
        const string tightBudgetInstance =
            "((1000,500,0)," +
            "((0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)," +
            "()," +
            "(0.10,0.05))," +
            "((PumpA,200,5.0,0.0))," +
            "5.0)";
        PUMPSCHEDULINGEM p = new(tightBudgetInstance);
        PumpSchedulingEMVerifier v = new();
        Assert.Equal(expected, v.verify(p, certificate));
    }

    [Theory]
    // Tank would drop below min level (min=200, capacity=1000, initial=500, demand=300/hr).
    // All pumps off: tank goes 500→200 at h=1, then 200-300=-100 < min=200 at h=2 → invalid.
    [InlineData(false)]
    public void PUMPSCHEDULINGEM_Verifier_False_TankBelowMin(bool expected)
    {
        const string highDemandInstance =
            "((1000,500,200)," +
            "((300,300,300,300,300,300,300,300,300,300,300,300,300,300,300,300,300,300,300,300,300,300,300,300)," +
            "()," +
            "(0.10,0.05))," +
            "((PumpA,200,5.0,0.0))," +
            "100.0)";
        // Pump off all 24 hours: h=0: 500-300=200 (ok), h=1: 200-300=-100 < 200 (min) → fail.
        const string allOffCert =
            "(100.0,0.0,((PumpA,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)))";
        PUMPSCHEDULINGEM p = new(highDemandInstance);
        PumpSchedulingEMVerifier v = new();
        Assert.Equal(expected, v.verify(p, allOffCert));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-cert")]
    [InlineData("(bad")]
    public void PUMPSCHEDULINGEM_Verifier_Malformed_Throws(string certificate)
    {
        PUMPSCHEDULINGEM p = new(SimpleInstance);
        PumpSchedulingEMVerifier v = new();
        Assert.Throws<CertificateParseException>(() => v.verify(p, certificate));
    }

    // ── Solver ────────────────────────────────────────────────────────────────

    [Fact]
    public void PUMPSCHEDULINGEM_Solver_Returns_NonEmpty_Simple()
    {
        PUMPSCHEDULINGEM p = new(SimpleInstance);
        PumpSchedulingEMSolver solver = new();
        string cert = solver.solve(p);
        Assert.False(string.IsNullOrEmpty(cert));
    }

    [Fact]
    public void PUMPSCHEDULINGEM_Solver_Returns_NonEmpty_Default()
    {
        PUMPSCHEDULINGEM p = new();
        PumpSchedulingEMSolver solver = new();
        string cert = solver.solve(p);
        Assert.False(string.IsNullOrEmpty(cert));
    }

    // ── Round-trip ────────────────────────────────────────────────────────────

    [Fact]
    public void PUMPSCHEDULINGEM_SolverOutput_PassesVerifier_Simple()
    {
        PUMPSCHEDULINGEM p = new(SimpleInstance);
        PumpSchedulingEMSolver solver = new();
        PumpSchedulingEMVerifier verifier = new();
        string cert = solver.solve(p);
        Assert.False(string.IsNullOrEmpty(cert));
        Assert.True(verifier.verify(p, cert));
    }

    [Fact]
    public void PUMPSCHEDULINGEM_SolverOutput_PassesVerifier_Default()
    {
        PUMPSCHEDULINGEM p = new();
        PumpSchedulingEMSolver solver = new();
        PumpSchedulingEMVerifier verifier = new();
        string cert = solver.solve(p);
        Assert.False(string.IsNullOrEmpty(cert));
        Assert.True(verifier.verify(p, cert));
    }

    [Fact]
    public void PUMPSCHEDULINGEM_SolverOutput_PassesVerifier_AutoBudget()
    {
        // Auto-budget: solver computes CM optimal then sets budget = 1.5× that cost.
        // Three pumps (total 1050 gph) comfortably exceed the 1000 gph peak demand,
        // ensuring the CM sub-solve finds a feasible positive-cost solution.
        const string autoBudgetInstance =
            "((10000,5000,1000)," +
            "((600,600,600,600,600,600,600,600,1000,1000,1000,1000,600,600,600,600,600,1000,1000,1000,1000,1000,600,600)," +
            "(8,9,10,11,17,18,19,20)," +
            "(0.12,0.06))," +
            "((PumpA,200,5.0,2.5),(PumpB,350,8.5,4.0),(PumpC,500,12.0,6.0))," +
            "0)";
        PUMPSCHEDULINGEM p = new(autoBudgetInstance);
        PumpSchedulingEMSolver solver = new();
        PumpSchedulingEMVerifier verifier = new();
        string cert = solver.solve(p);
        Assert.False(string.IsNullOrEmpty(cert));
        Assert.True(verifier.verify(p, cert));
    }
}
