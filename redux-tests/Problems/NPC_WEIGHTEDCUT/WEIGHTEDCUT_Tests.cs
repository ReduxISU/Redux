using Xunit;
using API.Problems.NPComplete.NPC_WEIGHTEDCUT;
using API.Problems.NPComplete.NPC_WEIGHTEDCUT.Verifiers;

namespace redux_tests;

#pragma warning disable CS1591

public class WEIGHTEDCUT_Tests {
    // -------------------------------------------------------------------------
    // Format declarations
    // -------------------------------------------------------------------------

    [Fact]
    public void WEIGHTEDCUT_Instance_Format_Described() {
        WEIGHTEDCUT problem = new WEIGHTEDCUT();
        Assert.NotNull(problem.instanceFormat);
        Assert.NotEmpty(problem.instanceFormat);
        Assert.Contains("N,E),K", problem.instanceFormat);
    }

    [Fact]
    public void WEIGHTEDCUT_Certificate_Format_Described() {
        WEIGHTEDCUT problem = new WEIGHTEDCUT();
        Assert.NotNull(problem.certificateFormat);
        Assert.NotEmpty(problem.certificateFormat);
        Assert.Contains("weight", problem.certificateFormat);
    }

    [Fact]
    public void WEIGHTEDCUT_Certificate_Format_Example_Is_Actually_Valid() {
        // The example quoted in certificateFormat must be a real, verifiable
        // certificate for defaultInstance — not just descriptive prose.
        WEIGHTEDCUT problem = new WEIGHTEDCUT();
        WeightedCutVerifier verifier = new WeightedCutVerifier();
        Assert.True(verifier.verify(problem, "{({2,1},5)}"));
    }
}
