using Xunit;
using API.Problems.NPComplete.NPC_EXACTCOVER;
using API.Problems.NPComplete.NPC_EXACTCOVER.Verifiers;

namespace redux_tests;

#pragma warning disable CS1591

public class EXACTCOVER_Tests {
    // -------------------------------------------------------------------------
    // Format declarations
    // -------------------------------------------------------------------------

    [Fact]
    public void EXACTCOVER_Instance_Format_Described() {
        EXACTCOVER problem = new EXACTCOVER();
        Assert.NotNull(problem.instanceFormat);
        Assert.NotEmpty(problem.instanceFormat);
        Assert.Contains("(U,S)", problem.instanceFormat);
    }

    [Fact]
    public void EXACTCOVER_Certificate_Format_Described() {
        EXACTCOVER problem = new EXACTCOVER();
        Assert.NotNull(problem.certificateFormat);
        Assert.NotEmpty(problem.certificateFormat);
        Assert.Contains("pairwise disjoint", problem.certificateFormat);
    }

    [Fact]
    public void EXACTCOVER_Certificate_Format_Example_Is_Actually_Valid() {
        // The example quoted in certificateFormat must be a real, verifiable
        // certificate for defaultInstance — not just descriptive prose.
        EXACTCOVER problem = new EXACTCOVER();
        ExactCoverVerifier verifier = new ExactCoverVerifier();
        Assert.True(verifier.verify(problem, "{{2,3},{4,1}}"));
    }
}
