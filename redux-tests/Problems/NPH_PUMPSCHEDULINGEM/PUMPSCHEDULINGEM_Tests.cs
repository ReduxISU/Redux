using Xunit;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGEM;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGEM.Verifiers;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGEM.Solvers;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGEM.Visualizations;
using API.Interfaces;

namespace redux_tests;

#pragma warning disable CS1591

public class PUMPSCHEDULINGEM_Tests {
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

    // Reusable 24-hour all-zero demand curve for constructing malformed-instance fixtures.
    private const string ZeroDemand24 =
        "(0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)";

    // ── Instantiation ─────────────────────────────────────────────────────────

    [Fact]
    public void PUMPSCHEDULINGEM_Default_Instantiation() {
        PUMPSCHEDULINGEM p = new();
        Assert.Equal(10000, p.TankCapacity);
        Assert.Equal(5000, p.TankCurrentLevel);
        Assert.Equal(2000, p.TankMinLevel);
        Assert.Equal(24, p.DemandGph.Count);
        Assert.Equal(8, p.PeakHours.Count);
        Assert.Equal(0.12, p.OnPeakCostPerKwh);
        Assert.Equal(0.06, p.OffPeakCostPerKwh);
        Assert.Equal(3, p.Pumps.Count);
        Assert.Equal("PumpA", p.Pumps[0].Name);
        Assert.Equal(200, p.Pumps[0].FlowRateGph);
        Assert.Equal(5.0, p.Pumps[0].PowerKw);
        Assert.Equal(2.5, p.Pumps[0].StartupCostDollars);
        Assert.Equal(0.0, p.BudgetLimitDollars); // 0 = auto
    }

    [Fact]
    public void PUMPSCHEDULINGEM_Custom_Instantiation() {
        PUMPSCHEDULINGEM p = new(SimpleInstance);
        Assert.Equal(1000, p.TankCapacity);
        Assert.Equal(500, p.TankCurrentLevel);
        Assert.Equal(0, p.TankMinLevel);
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
        Assert.Equal(10.0, p.BudgetLimitDollars);
    }

    [Fact]
    public void PUMPSCHEDULINGEM_Bad_Instance_Throws() {
        Assert.Throws<ProblemParseException>(() => new PUMPSCHEDULINGEM("not-an-instance"));
    }

    [Fact]
    public void PUMPSCHEDULINGEM_Wrong_Section_Count_Throws() {
        // Only 3 sections, missing budget.
        string bad =
            "((1000,500,0)," +
            "((0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0),()," +
            "(0.10,0.05)),((PumpA,200,5.0,0.0)))";
        Assert.Throws<ProblemParseException>(() => new PUMPSCHEDULINGEM(bad));
    }

    [Fact]
    public void PUMPSCHEDULINGEM_Wrong_Demand_Count_Throws() {
        // Only 3 demand values instead of 24.
        string bad =
            "((1000,500,0),((100,100,100),(),(0.10,0.05)),((PumpA,200,5.0,0.0)),10.0)";
        Assert.Throws<ProblemParseException>(() => new PUMPSCHEDULINGEM(bad));
    }

    [Fact]
    public void PUMPSCHEDULINGEM_Negative_Budget_Throws() {
        string bad =
            "((1000,500,0)," +
            "((0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0),()," +
            "(0.10,0.05)),((PumpA,200,5.0,0.0)),-5.0)";
        Assert.Throws<ProblemParseException>(() => new PUMPSCHEDULINGEM(bad));
    }

    // ── Verifier — valid certificates ─────────────────────────────────────────

    [Fact]
    public void PUMPSCHEDULINGEM_Verifier_True_AllOff() {
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
    public void PUMPSCHEDULINGEM_Verifier_False_With_Tighter_Budget(string certificate, bool expected) {
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
    public void PUMPSCHEDULINGEM_Verifier_False_TankBelowMin(bool expected) {
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
    public void PUMPSCHEDULINGEM_Verifier_Malformed_Throws(string certificate) {
        PUMPSCHEDULINGEM p = new(SimpleInstance);
        PumpSchedulingEMVerifier v = new();
        Assert.Throws<CertificateParseException>(() => v.verify(p, certificate));
    }

    // ── Solver ────────────────────────────────────────────────────────────────

    [Fact]
    public void PUMPSCHEDULINGEM_Solver_Returns_NonEmpty_Simple() {
        PUMPSCHEDULINGEM p = new(SimpleInstance);
        PumpSchedulingEMSolver solver = new();
        string cert = solver.solve(p);
        Assert.False(string.IsNullOrEmpty(cert));
    }

    [Fact]
    public void PUMPSCHEDULINGEM_Solver_Returns_NonEmpty_Default() {
        PUMPSCHEDULINGEM p = new();
        PumpSchedulingEMSolver solver = new();
        string cert = solver.solve(p);
        Assert.False(string.IsNullOrEmpty(cert));
    }

    // ── Round-trip ────────────────────────────────────────────────────────────

    [Fact]
    public void PUMPSCHEDULINGEM_SolverOutput_PassesVerifier_Simple() {
        PUMPSCHEDULINGEM p = new(SimpleInstance);
        PumpSchedulingEMSolver solver = new();
        PumpSchedulingEMVerifier verifier = new();
        string cert = solver.solve(p);
        Assert.False(string.IsNullOrEmpty(cert));
        Assert.True(verifier.verify(p, cert));
    }

    [Fact]
    public void PUMPSCHEDULINGEM_SolverOutput_PassesVerifier_Default() {
        PUMPSCHEDULINGEM p = new();
        PumpSchedulingEMSolver solver = new();
        PumpSchedulingEMVerifier verifier = new();
        string cert = solver.solve(p);
        Assert.False(string.IsNullOrEmpty(cert));
        Assert.True(verifier.verify(p, cert));
    }

    [Fact]
    public void PUMPSCHEDULINGEM_SolverOutput_PassesVerifier_AutoBudget() {
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

    // Certificates for both CM and EM start with a bare numeric field up to the first
    // top-level comma (cost for CM; effectiveBudget for EM), so this avoids pulling in
    // the SPADE nested-collection parser just to read that one field back out in tests.
    private static double ParseFirstField(string certificate) {
        string inner = certificate.TrimStart('(');
        int comma = inner.IndexOf(',');
        return double.Parse(inner[..comma].Trim(), System.Globalization.CultureInfo.InvariantCulture);
    }

    [Theory]
    // Budget=0 (auto-compute), 2 pumps, medium tank, a peak window.
    [InlineData("((2000,1000,300),((300,300,300,300,300,300,300,300,300,300,300,300,300,300,300,300,300,300,300,300,300,300,300,300),(6,7,8),(0.15,0.07)),((PumpA,150,4.0,1.0),(PumpB,250,6.0,2.0)),0)")]
    // Explicit generous budget, 3 pumps, large tank, demand swinging low/high.
    [InlineData("((5000,2500,500),((200,200,200,200,200,200,800,800,800,800,800,800,200,200,200,200,200,200,800,800,800,800,800,800),(),(0.20,0.05)),((P1,100,2.0,0.5),(P2,150,3.0,0.5),(P3,300,7.0,1.5)),1000.0)")]
    // Explicit generous budget, 4 small pumps, small tank, flat low demand.
    [InlineData("((500,250,50),((50,50,50,50,50,50,50,50,50,50,50,50,50,50,50,50,50,50,50,50,50,50,50,50),(),(0.10,0.10)),((P1,60,1.0,0.1),(P2,40,0.8,0.1),(P3,80,1.5,0.2),(P4,20,0.5,0.05)),50.0)")]
    public void PUMPSCHEDULINGEM_SolverOutput_PassesVerifier_VariedInstances(string instance) {
        PUMPSCHEDULINGEM p = new(instance);
        PumpSchedulingEMSolver solver = new();
        PumpSchedulingEMVerifier verifier = new();
        string cert = solver.solve(p);
        Assert.False(string.IsNullOrEmpty(cert));
        Assert.True(verifier.verify(p, cert));
    }

    // ── Budget auto-compute / explicit budget assertions ─────────────────────

    [Fact]
    public void PUMPSCHEDULINGEM_AutoBudget_EffectiveBudget_Is_Positive_And_Finite() {
        const string autoBudgetInstance =
            "((10000,5000,1000)," +
            "((600,600,600,600,600,600,600,600,1000,1000,1000,1000,600,600,600,600,600,1000,1000,1000,1000,1000,600,600)," +
            "(8,9,10,11,17,18,19,20)," +
            "(0.12,0.06))," +
            "((PumpA,200,5.0,2.5),(PumpB,350,8.5,4.0),(PumpC,500,12.0,6.0))," +
            "0)";
        PUMPSCHEDULINGEM p = new(autoBudgetInstance);
        PumpSchedulingEMSolver solver = new();
        string cert = solver.solve(p);

        double effectiveBudget = ParseFirstField(cert);

        Assert.True(effectiveBudget > 0.0);
        Assert.True(double.IsFinite(effectiveBudget));
    }

    [Fact]
    public void PUMPSCHEDULINGEM_ExplicitBudget_EffectiveBudget_DoesNotExceed_InstanceBudget() {
        // SimpleInstance carries an explicit budget of 10.0 — the effective budget in the
        // certificate must be drawn directly from it (no auto-compute), so it can never
        // exceed the instance's declared bound.
        PUMPSCHEDULINGEM p = new(SimpleInstance);
        PumpSchedulingEMSolver solver = new();
        string cert = solver.solve(p);

        double effectiveBudget = ParseFirstField(cert);

        Assert.True(effectiveBudget <= p.BudgetLimitDollars + 0.01);
    }

    // ── Infeasible-path fallback ──────────────────────────────────────────────

    [Fact]
    public void PUMPSCHEDULINGEM_Solver_FallsBackTo_AllPumpsOn_When_NoFeasiblePathWithinBudget() {
        // Tank must stay >= minLevel (400) while demand (200/hr) alone would drain it
        // below that floor, so every hour requires the pump on. But the budget (0.5) is
        // far smaller than even a single hour's running cost (100 energy + 1000 startup
        // on the first activation), so every path that keeps the tank in-range is pruned
        // by the budget constraint. RunEmergencyResilience finds no feasible terminal
        // state, so solve() takes the documented fallback: run all pumps every hour,
        // regardless of budget or tank overflow, then reports whatever that costs.
        string instance =
            "((1000,500,400)," +
            "((200,200,200,200,200,200,200,200,200,200,200,200,200,200,200,200,200,200,200,200,200,200,200,200)," +
            "()," +
            "(1.0,1.0))," +
            "((PumpA,250,100.0,1000.0))," +
            "0.5)";
        PUMPSCHEDULINGEM p = new(instance);
        PumpSchedulingEMSolver solver = new();
        PumpSchedulingEMVerifier verifier = new();

        string cert = solver.solve(p);

        // The fallback always produces a non-empty certificate — solve() never throws
        // or returns empty for this problem, even when no budget-feasible path exists.
        Assert.False(string.IsNullOrEmpty(cert));

        // Document actual behavior rather than assume it: the fallback schedule (all
        // pumps on every hour) is not budget-constrained and, on this instance, also
        // overflows the tank (500 starting level + net +50 gph every hour eventually
        // exceeds the 1000 capacity), so the verifier is expected to reject it.
        bool verified = verifier.verify(p, cert);
        Assert.False(verified);
    }

    // ── Verifier — additional rejection cases ────────────────────────────────

    [Fact]
    public void PUMPSCHEDULINGEM_Verifier_TankAboveCapacity_ReturnsFalse() {
        PUMPSCHEDULINGEM p = new(SimpleInstance); // capacity 1000, current 500, zero demand
        PumpSchedulingEMVerifier v = new();
        // Pump on every hour (200 gph, zero demand) overflows past 1000 well before hour 24.
        string cert = "(10.0,6.0,((PumpA,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1)))";
        Assert.False(v.verify(p, cert));
    }

    [Fact]
    public void PUMPSCHEDULINGEM_Verifier_NonBinaryHourValue_ReturnsFalse() {
        PUMPSCHEDULINGEM p = new(SimpleInstance);
        PumpSchedulingEMVerifier v = new();
        string cert = "(10.0,0.0,((PumpA,2,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)))";
        Assert.False(v.verify(p, cert));
    }

    [Fact]
    public void PUMPSCHEDULINGEM_Verifier_WrongTopLevelSectionCount_Throws() {
        PUMPSCHEDULINGEM p = new(SimpleInstance);
        PumpSchedulingEMVerifier v = new();
        // Only 2 top-level sections instead of 3.
        string cert = "(10.0,0.0)";
        Assert.Throws<CertificateParseException>(() => v.verify(p, cert));
    }

    // ── Additional class parse-validation ────────────────────────────────────

    [Fact]
    public void PUMPSCHEDULINGEM_ZeroPumps_Throws() {
        string bad = $"((1000,500,0),({ZeroDemand24},(),(0.10,0.05)),(),10.0)";
        Assert.Throws<ProblemParseException>(() => new PUMPSCHEDULINGEM(bad));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void PUMPSCHEDULINGEM_NonPositiveCapacity_Throws(double capacity) {
        string bad = $"(({capacity.ToString(System.Globalization.CultureInfo.InvariantCulture)},0,0),({ZeroDemand24},(),(0.10,0.05)),((PumpA,200,5.0,0.0)),10.0)";
        Assert.Throws<ProblemParseException>(() => new PUMPSCHEDULINGEM(bad));
    }

    [Theory]
    [InlineData(-10)]   // below 0
    [InlineData(1000)]  // equal to capacity — must be strictly less
    public void PUMPSCHEDULINGEM_MinLevelOutOfRange_Throws(double minLevel) {
        string bad = $"((1000,500,{minLevel.ToString(System.Globalization.CultureInfo.InvariantCulture)}),({ZeroDemand24},(),(0.10,0.05)),((PumpA,200,5.0,0.0)),10.0)";
        Assert.Throws<ProblemParseException>(() => new PUMPSCHEDULINGEM(bad));
    }

    [Theory]
    [InlineData(50, 100)]   // currentLevel (50) below minLevel (100)
    [InlineData(1500, 100)] // currentLevel (1500) above capacity (1000)
    public void PUMPSCHEDULINGEM_CurrentLevelOutOfRange_Throws(double currentLevel, double minLevel) {
        string bad =
            $"((1000,{currentLevel.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
            $"{minLevel.ToString(System.Globalization.CultureInfo.InvariantCulture)})," +
            $"({ZeroDemand24},(),(0.10,0.05)),((PumpA,200,5.0,0.0)),10.0)";
        Assert.Throws<ProblemParseException>(() => new PUMPSCHEDULINGEM(bad));
    }

    [Fact]
    public void PUMPSCHEDULINGEM_MalformedRatesSubsection_Throws() {
        // Rates sub-list has only 1 value instead of 2.
        string bad = $"((1000,500,0),({ZeroDemand24},(),(0.10)),((PumpA,200,5.0,0.0)),10.0)";
        Assert.Throws<ProblemParseException>(() => new PUMPSCHEDULINGEM(bad));
    }

    [Fact]
    public void PUMPSCHEDULINGEM_PumpTupleWrongFieldCount_Throws() {
        // Pump tuple has 3 fields instead of 4 (missing startup cost).
        string bad = $"((1000,500,0),({ZeroDemand24},(),(0.10,0.05)),((PumpA,200,5.0)),10.0)";
        Assert.Throws<ProblemParseException>(() => new PUMPSCHEDULINGEM(bad));
    }

    // ── Visualization ─────────────────────────────────────────────────────────

    [Fact]
    public void PUMPSCHEDULINGEM_Visualization_Typed_Returns_24_Frames_InOrder_With_NonDecreasing_CumulativeCost_And_NonNegative_BudgetRemaining() {
        PUMPSCHEDULINGEM p = new();
        PumpSchedulingEMSolver solver = new();
        PumpSchedulingEMVisualization visualization = new();

        var steps = solver.GetSteps(p);
        var frames = visualization.StepsVisualization(p, steps);

        Assert.Equal(24, frames.Count);

        double previousCumulative = -1.0;
        for (int h = 0; h < 24; h++) {
            var frame = Assert.IsType<API_EmPumpFrame>(frames[h]);
            Assert.Equal(h, frame.metrics.hour);
            Assert.True(frame.metrics.cumulativeCost >= previousCumulative);
            Assert.True(frame.metrics.budgetRemaining >= 0.0);
            previousCumulative = frame.metrics.cumulativeCost;
        }
    }

    [Fact]
    public void PUMPSCHEDULINGEM_Visualization_InterfaceOverride_ReParsesInstance_Returns_24_Frames() {
        // Dispatch through the IVisualization reference so the explicit
        // `IVisualization.StepsVisualization(string,List<object>)` override is exercised
        // (rather than the typed method called directly), confirming it re-parses the
        // instance string itself instead of being short-circuited by the default
        // interface method's empty-steps guard.
        IVisualization visualization = new PumpSchedulingEMVisualization();
        var steps = new List<object> { true };

        var frames = visualization.StepsVisualization(PUMPSCHEDULINGEM.DefaultInstance, steps);

        Assert.Equal(24, frames.Count);
        for (int h = 0; h < 24; h++) {
            var frame = Assert.IsType<API_EmPumpFrame>(frames[h]);
            Assert.Equal(h, frame.metrics.hour);
            Assert.True(frame.metrics.budgetRemaining >= 0.0);
        }
    }
}
