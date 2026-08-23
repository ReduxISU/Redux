using Xunit;
using API.Problems.NPComplete.NPC_STEINERTREE;
using API.Problems.NPComplete.NPC_STEINERTREE.Verifiers;

namespace redux_tests;

#pragma warning disable CS1591

public class STEINERTREE_Tests {
    // -------------------------------------------------------------------------
    // Format declarations
    // -------------------------------------------------------------------------

    [Fact]
    public void STEINERTREE_Instance_Format_Described() {
        STEINERTREE problem = new STEINERTREE();
        Assert.NotNull(problem.instanceFormat);
        Assert.NotEmpty(problem.instanceFormat);
        Assert.Contains("N,E),R,K", problem.instanceFormat);
    }

    [Fact]
    public void STEINERTREE_Certificate_Format_Described() {
        STEINERTREE problem = new STEINERTREE();
        Assert.NotNull(problem.certificateFormat);
        Assert.NotEmpty(problem.certificateFormat);
        Assert.Contains("terminal", problem.certificateFormat);
    }

    [Fact]
    public void STEINERTREE_Certificate_Format_Example_Is_Actually_Valid() {
        // The example quoted in certificateFormat must be a real, verifiable
        // certificate for defaultInstance — not just descriptive prose.
        STEINERTREE problem = new STEINERTREE();
        SteinerTreeVerifier verifier = new SteinerTreeVerifier();
        Assert.True(verifier.verify(problem, "{{8,6},{6,1},{1,2},{2,3},{3,5}}"));
    }
}
