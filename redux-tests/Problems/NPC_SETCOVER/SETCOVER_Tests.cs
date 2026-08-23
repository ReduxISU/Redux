using Xunit;
using API.Problems.NPComplete.NPC_SETCOVER;
using API.Problems.NPComplete.NPC_SETCOVER.Verifiers;

namespace redux_tests;

#pragma warning disable CS1591

public class SETCOVER_Tests {
    // -------------------------------------------------------------------------
    // Format declarations
    // -------------------------------------------------------------------------

    [Fact]
    public void SETCOVER_Instance_Format_Described() {
        SETCOVER problem = new SETCOVER();
        Assert.NotNull(problem.instanceFormat);
        Assert.NotEmpty(problem.instanceFormat);
        Assert.Contains("(U,S,K)", problem.instanceFormat);
    }

    [Fact]
    public void SETCOVER_Certificate_Format_Described() {
        SETCOVER problem = new SETCOVER();
        Assert.NotNull(problem.certificateFormat);
        Assert.NotEmpty(problem.certificateFormat);
        Assert.Contains("universal set", problem.certificateFormat);
    }

    [Fact]
    public void SETCOVER_Certificate_Format_Example_Is_Actually_Valid() {
        // The example quoted in certificateFormat must be a real, verifiable
        // certificate for defaultInstance — not just descriptive prose.
        SETCOVER problem = new SETCOVER();
        SetCoverVerifier verifier = new SetCoverVerifier();
        Assert.True(verifier.verify(problem, "{{1,2,3},{4,5}}"));
    }
}
