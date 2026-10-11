using API.Interfaces;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGCM;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGCM.Solvers;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGCM.Verifiers;
using API.Problems.NPHard.NPH_PUMPSCHEDULINGCM.Visualizations;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// #803: when no schedule keeps the tank between empty and full, the solver answers {} (no answer), and the verifier says
// false, as Emergency Resilience does (#701).
public class PUMPSCHEDULINGCM_NoAnswer_Tests {
    // Demand of 900 to 1,500 gal an hour, and all three pumps together give 1,050: the 1,500-gal tank runs dry.
    internal const string DemandTooHigh =
        "((3000,1500),((900,900,900,900,900,900,900,900,1500,1500,1500,1500,900,900,900,900,900,1500,1500,1500,1500,1500,900,900)," +
        "(8,9,10,11,17,18,19,20),(0.12,0.06)),((PumpA,200,5.0,2.5),(PumpB,350,8.5,4.0),(PumpC,500,12.0,6.0)))";

    [Fact]
    public void PumpCM_Solver_Answers_Braces_When_No_Schedule_Fits() {
        Assert.Equal("{}", new PumpSchedulingCMSolver().solve(new PUMPSCHEDULINGCM(DemandTooHigh)));
    }

    [Fact]
    public void PumpCM_Verifier_Says_False_For_No_Answer() {
        Assert.False(new PumpSchedulingCMVerifier().verify(new PUMPSCHEDULINGCM(DemandTooHigh), "{}"));
        Assert.False(new PumpSchedulingCMVerifier().verify(new PUMPSCHEDULINGCM(), " {} "));
    }

    [Fact]
    public void PumpCM_Verifier_Still_Refuses_A_Malformed_Certificate() {
        // Only {} means "no answer"; other text the verifier cannot read stays a parse error (a 400).
        Assert.Throws<CertificateParseException>(() => new PumpSchedulingCMVerifier().verify(new PUMPSCHEDULINGCM(), "(1,2,3)"));
    }

    [Fact]
    public void PumpCM_Visualization_Still_Draws_No_Hours_For_No_Answer() {
        // The visualization solves again and reads the answer; {} is no schedule, as "" was.
        Assert.Empty(new PumpSchedulingCMVisualization().StepsVisualization(new PUMPSCHEDULINGCM(DemandTooHigh), []));
    }
}
