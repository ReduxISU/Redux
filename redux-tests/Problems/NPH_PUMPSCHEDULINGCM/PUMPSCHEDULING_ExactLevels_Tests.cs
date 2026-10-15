using API.Interfaces.Schedule;
using API.Interfaces.Steps;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGCM;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGCM.Solvers;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGCM.Verifiers;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGCM.Visualizations;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGEM;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGEM.Solvers;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGEM.Verifiers;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// #887 (Cost Minimization) and #738 (Emergency Resilience): both solvers track the tank level exactly, in units of
// 10^-d gallons, instead of rounding it to whole gallons every hour, and both verifiers simulate it in exact decimals. A
// solver with StateCap = 1 outgrows its exact table at once and falls back to whole gallons, which is how these tests show
// what the rounding did.
public class PUMPSCHEDULING_ExactLevels_Tests {
    private static string Hours(string value) => "(" + string.Join(",", Enumerable.Repeat(value, 24)) + ")";
    private static string Bits(int on) => string.Join(",", Enumerable.Range(0, 24).Select(h => h < on ? "1" : "0"));

    // 0.4 gal of demand an hour and one 1-gal pump that costs $1 an hour, from an empty 10-gal tank. Rounded, hour 0 with
    // the pump on ends at 0.6, which rounds to 1, and every hour after it ends at 0.6 again, so one pump hour looks enough:
    // the verifier sees 0.6, 0.2, -0.2 and rejects it. Exactly, the day needs 9.6 gal, so ten pump hours ($10).
    private const string CmFractional = "((10,0),(" + "(0.4,0.4,0.4,0.4,0.4,0.4,0.4,0.4,0.4,0.4,0.4,0.4,0.4,0.4,0.4,0.4,0.4,0.4,0.4,0.4,0.4,0.4,0.4,0.4)"
        + ",(),(1,1)),((A,1,1,0)))";
    private static readonly string EmFractional = "((10,0,0),(" + Hours("0.4") + ",(),(1,1)),((A,1,1,0)),0)";

    // A full 20-gal tank losing 0.6 gal an hour, and a 25-gal pump that overflows it from any level. Exactly, the tank just
    // drains to 5.6 gal (above the minimum of 2 in Emergency Resilience), with the pump off all day. Rounded, every hour
    // loses a whole gallon (19.4 rounds to 19, ...), so the tank runs dry at hour 20 (below the minimum at hour 18) and
    // there is no answer.
    private static readonly string CmOnlyExact = "((20,20),(" + Hours("0.6") + ",(),(1,1)),((A,25,1,0)))";
    private static readonly string EmOnlyExact = "((20,20,2),(" + Hours("0.6") + ",(),(1,1)),((A,25,1,0)),0)";

    private static (string Answer, SolverStep<PumpPlan> Done) SolveCm(string instance, int cap = PumpSchedulingCMSolver.DefaultStateCap) {
        var rec = new StepRecorder<PumpPlan>();
        string answer = new PumpSchedulingCMSolver { StateCap = cap }.Solve(new PUMPSCHEDULINGCM(instance), rec);
        return (answer, rec.Steps[^1]);
    }

    private static (string Answer, SolverStep<PumpPlan> Done) SolveEm(string instance, int cap = PumpSchedulingEMSolver.DefaultStateCap) {
        var rec = new StepRecorder<PumpPlan>();
        string answer = new PumpSchedulingEMSolver { StateCap = cap }.Solve(new PUMPSCHEDULINGEM(instance), rec);
        return (answer, rec.Steps[^1]);
    }

    // ---- The shared tank arithmetic ----

    [Fact]
    public void TankLevels_CountsInTheCoarsestUnitThatMakesEveryInputWhole() {
        var tank = TankLevels.Create(10, 0.25, 0, [0.4, 1.5, 3], [1, 0.125]);
        Assert.False(tank.Rounded);
        Assert.Equal(1000, tank.Scale);
        Assert.Equal(250, tank.Start);
        // 0.25 - 0.4 + 1 + 0.125, exactly.
        Assert.True(tank.Next(tank.Start, 0, 0b11, out long next, out double stored));
        Assert.Equal((975, 975.0, 0.975), (next, stored, tank.Gallons(next)));
        // Integer inputs count in whole gallons, exactly as the old table did.
        Assert.Equal((false, 1L), (TankLevels.Create(10000, 5000, 0, [600, 1000], [200, 350, 500]).Rounded, TankLevels.Create(10000, 5000, 0, [600, 1000], [200, 350, 500]).Scale));
    }

    [Fact]
    public void TankLevels_LandsOnALimitExactly_WhereDoublesOvershoot() {
        // 0.1 + 0.2 is 0.30000000000000004 in doubles, past a 0.3 capacity.
        Assert.True(0.1 + 0.2 > 0.3);
        var tank = TankLevels.Create(0.3, 0.1, 0, [0], [0.2]);
        Assert.True(tank.Next(tank.Start, 0, 1, out long next, out _));
        Assert.Equal(0.3, tank.Gallons(next));
        Assert.Equal(0.3m, TankLevels.Exact(0.1) + TankLevels.Exact(0.2));
    }

    [Fact]
    public void TankLevels_WithMoreDecimalsThanItCounts_FallsBackToWholeGallons() {
        var tank = TankLevels.Create(10, 0, 0, [0.0000000001], [1]);
        Assert.True(tank.Rounded);
        Assert.Equal(1, tank.Scale);
        Assert.True(TankLevels.Create(10, 0, 0, [0.4], [1], exact: false).Rounded);
    }

    // ---- Cost Minimization (#887) ----

    [Fact]
    public void PumpCM_FractionalDemand_IsExact_WhereWholeGallonsGaveAScheduleTheVerifierRejects() {
        var problem = new PUMPSCHEDULINGCM(CmFractional);
        var (answer, done) = SolveCm(CmFractional);
        Assert.Equal($"(10,((A,{Bits(10)})))", answer);
        Assert.True(new PumpSchedulingCMVerifier().verify(problem, answer));
        Assert.Equal((true, "Cheapest schedule: $10.00 for the day. The tank stays between 0 and 6 gal."), (done.Ok!.Value, done.Caption));

        // Rounded, the table believed one pump hour was enough.
        var (rounded, roundedDone) = SolveCm(CmFractional, cap: 1);
        Assert.Equal($"(1,((A,{Bits(1)})))", rounded);
        Assert.False(new PumpSchedulingCMVerifier().verify(problem, rounded));
        Assert.Equal((false, "The schedule costs $1.00, but the tank ends hour 2 at -0.2 gal, outside 0 to 10 gal: the solver rounds the tank level to whole gallons, so it did not see this."),
            (roundedDone.Ok!.Value, roundedDone.Caption));
    }

    [Fact]
    public void PumpCM_AnAnswerOnlyExactLevelsFind() {
        var (answer, done) = SolveCm(CmOnlyExact);
        Assert.Equal($"(0,((A,{Bits(0)})))", answer);
        Assert.True(new PumpSchedulingCMVerifier().verify(new PUMPSCHEDULINGCM(CmOnlyExact), answer));
        Assert.Equal("Cheapest schedule: $0.00 for the day. The tank stays between 5.6 and 20 gal.", done.Caption);
        Assert.Equal(5.6, done.Partial.Reached[^1].Low);

        var (rounded, roundedDone) = SolveCm(CmOnlyExact, cap: 1);
        Assert.Equal("{}", rounded);
        Assert.Equal("No schedule keeps the tank between empty and full for all 24 hours, so there is no answer. The tank has too many levels to track exactly, so the solver rounded it to whole gallons.",
            roundedDone.Caption);
    }

    [Fact]
    public void PumpCM_Verifier_AcceptsALevelThatLandsExactlyOnTheCapacity() {
        // 0.1 gal in a 0.3-gal tank, plus 0.2 gal in hour 0: exactly full, though doubles make it 0.30000000000000004.
        var problem = new PUMPSCHEDULINGCM("((0.3,0.1),(" + Hours("0") + ",(),(1,1)),((A,0.2,1,0)))");
        string answer = $"(1,((A,{Bits(1)})))";
        Assert.True(new PumpSchedulingCMVerifier().verify(problem, answer));
        // The picture agrees: no hour is out of limits.
        var on = Enumerable.Range(0, 24).Select(h => (IReadOnlyCollection<string>)(h < 1 ? ["P1"] : Array.Empty<string>())).ToList();
        Assert.Null(ScheduleFrames.FirstOutOfLimits(PumpSchedulingCMVisualization.Spec(problem), on));
        // A second hour overflows it.
        Assert.False(new PumpSchedulingCMVerifier().verify(problem, $"(2,((A,{Bits(2)})))"));
    }

    // ---- Emergency Resilience (#738) ----

    [Fact]
    public void PumpEM_FractionalDemand_IsExact_WhereWholeGallonsGaveAScheduleTheVerifierRejects() {
        var problem = new PUMPSCHEDULINGEM(EmFractional);
        var (answer, done) = SolveEm(EmFractional);
        // The cheapest schedule is ten pump hours ($10), so the budget is $15.
        Assert.StartsWith("(15.00,", answer);
        Assert.True(new PumpSchedulingEMVerifier().verify(problem, answer));
        Assert.True(done.Ok);
        Assert.DoesNotContain("whole gallons", done.Caption);

        // Rounded, the cheapest schedule looked like one pump hour, so the budget $1.50, and the schedule runs the tank dry.
        var (rounded, roundedDone) = SolveEm(EmFractional, cap: 1);
        Assert.StartsWith("(1.50,1.00,", rounded);
        Assert.False(new PumpSchedulingEMVerifier().verify(problem, rounded));
        Assert.Equal((false, "The schedule costs $1.00, but the tank ends hour 2 at -0.2 gal, outside 0 to 10 gal: the solver rounds the tank level to whole gallons, so it did not see this."),
            (roundedDone.Ok!.Value, roundedDone.Caption));
    }

    [Fact]
    public void PumpEM_AnAnswerOnlyExactLevelsFind() {
        var (answer, done) = SolveEm(EmOnlyExact);
        Assert.Equal($"(0.00,0.00,((A,{Bits(0)})))", answer);
        Assert.True(new PumpSchedulingEMVerifier().verify(new PUMPSCHEDULINGEM(EmOnlyExact), answer));
        Assert.True(done.Ok);
        Assert.Equal(5.6, done.Partial.Reached[^1].Low);

        var (rounded, roundedDone) = SolveEm(EmOnlyExact, cap: 1);
        Assert.Equal("{}", rounded);
        Assert.Equal("No schedule keeps the tank between 2 and 20 gal within the $0.00 budget for all 24 hours, so there is no answer. The tank has too many levels to track exactly, so the solver rounded it to whole gallons.",
            roundedDone.Caption);
    }

    [Fact]
    public void PumpEM_Verifier_AcceptsALevelThatLandsExactlyOnTheCapacity() {
        var problem = new PUMPSCHEDULINGEM("((0.3,0.1,0.1),(" + Hours("0") + ",(),(1,1)),((A,0.2,1,0)),2)");
        Assert.True(new PumpSchedulingEMVerifier().verify(problem, $"(2,1,((A,{Bits(1)})))"));
        Assert.False(new PumpSchedulingEMVerifier().verify(problem, $"(2,2,((A,{Bits(2)})))"));
    }

    [Fact]
    public void PumpEM_Verifier_AcceptsALevelThatLandsExactlyOnTheMinimum() {
        // 0.3 gal down to the 0.1 minimum by 0.1 + 0.1: doubles make it 0.09999999999999998, below it.
        Assert.True(0.3 - 0.1 - 0.1 < 0.1);
        var problem = new PUMPSCHEDULINGEM("((1,0.3,0.1),((0.1,0.1," + string.Join(",", Enumerable.Repeat("0", 22)) + "),(),(1,1)),((A,1,1,0)),0)");
        Assert.True(new PumpSchedulingEMVerifier().verify(problem, $"(0,0,((A,{Bits(0)})))"));
    }
}
