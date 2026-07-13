using Xunit;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGCM;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGCM.Verifiers;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGCM.Solvers;
using API.Interfaces;

namespace redux_tests;

#pragma warning disable CS1591

public class PUMPSCHEDULINGCM_Tests
{
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

    // ── Instantiation ─────────────────────────────────────────────────────────

    [Fact]
    public void PUMPSCHEDULINGCM_Default_Instantiation()
    {
        PUMPSCHEDULINGCM p = new();
        Assert.Equal(10000, p.TankCapacity);
        Assert.Equal(5000,  p.TankCurrentLevel);
        Assert.Equal(24, p.DemandGph.Count);
        Assert.Equal(8,  p.PeakHours.Count);
        Assert.Equal(0.12, p.OnPeakCostPerKwh);
        Assert.Equal(0.06, p.OffPeakCostPerKwh);
        Assert.Equal(3, p.Pumps.Count);
        Assert.Equal("PumpA", p.Pumps[0].Name);
        Assert.Equal(200,  p.Pumps[0].FlowRateGph);
        Assert.Equal(5.0,  p.Pumps[0].PowerKw);
        Assert.Equal(2.5,  p.Pumps[0].StartupCostDollars);
    }

    [Fact]
    public void PUMPSCHEDULINGCM_Custom_Instantiation()
    {
        PUMPSCHEDULINGCM p = new(SimpleInstance);
        Assert.Equal(1000, p.TankCapacity);
        Assert.Equal(500,  p.TankCurrentLevel);
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
    public void PUMPSCHEDULINGCM_Bad_Instance_Throws()
    {
        Assert.Throws<ProblemParseException>(() => new PUMPSCHEDULINGCM("not-an-instance"));
    }

    [Fact]
    public void PUMPSCHEDULINGCM_Wrong_Demand_Count_Throws()
    {
        // Only 3 demand values instead of 24.
        string bad = "((1000,500),((100,100,100),(),(0.10,0.05)),((PumpA,200,5.0,0.0)))";
        Assert.Throws<ProblemParseException>(() => new PUMPSCHEDULINGCM(bad));
    }

    // ── Verifier — valid certificates ─────────────────────────────────────────

    [Fact]
    public void PUMPSCHEDULINGCM_Verifier_True_ZeroDemand()
    {
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
    public void PUMPSCHEDULINGCM_Verifier_False(string certificate, bool expected)
    {
        PUMPSCHEDULINGCM p = new(SimpleInstance);
        PumpSchedulingCMVerifier v = new();
        Assert.Equal(expected, v.verify(p, certificate));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-cert")]
    [InlineData("(bad")]
    public void PUMPSCHEDULINGCM_Verifier_Malformed_Throws(string certificate)
    {
        PUMPSCHEDULINGCM p = new(SimpleInstance);
        PumpSchedulingCMVerifier v = new();
        Assert.Throws<CertificateParseException>(() => v.verify(p, certificate));
    }

    // ── Solver ────────────────────────────────────────────────────────────────

    [Fact]
    public void PUMPSCHEDULINGCM_Solver_ZeroDemand_Returns_ZeroCost()
    {
        PUMPSCHEDULINGCM p = new(SimpleInstance);
        PumpSchedulingCMSolver solver = new();
        string cert = solver.solve(p);
        Assert.Equal(SimpleCertValid, cert);
    }

    // ── Round-trip ────────────────────────────────────────────────────────────

    [Fact]
    public void PUMPSCHEDULINGCM_SolverOutput_PassesVerifier_Simple()
    {
        PUMPSCHEDULINGCM p = new(SimpleInstance);
        PumpSchedulingCMSolver solver = new();
        PumpSchedulingCMVerifier verifier = new();
        string cert = solver.solve(p);
        Assert.False(string.IsNullOrEmpty(cert));
        Assert.True(verifier.verify(p, cert));
    }

    [Fact]
    public void PUMPSCHEDULINGCM_SolverOutput_PassesVerifier_Default()
    {
        PUMPSCHEDULINGCM p = new();
        PumpSchedulingCMSolver solver = new();
        PumpSchedulingCMVerifier verifier = new();
        string cert = solver.solve(p);
        Assert.False(string.IsNullOrEmpty(cert));
        Assert.True(verifier.verify(p, cert));
    }
}
