using Xunit;
using API.Problems.NPComplete.NPC_DOMINATINGSET;
using API.Problems.NPComplete.NPC_DOMINATINGSET.Verifiers;

namespace redux_tests;

#pragma warning disable CS1591

public class DOMINATINGSET_Tests
{
    // -------------------------------------------------------------------------
    // Format declarations
    // -------------------------------------------------------------------------

    [Fact]
    public void DOMINATINGSET_Instance_Format_Described()
    {
        DOMINATINGSET problem = new DOMINATINGSET();
        Assert.NotNull(problem.instanceFormat);
        Assert.NotEmpty(problem.instanceFormat);
        Assert.Contains("N,E),K", problem.instanceFormat);
    }

    [Fact]
    public void DOMINATINGSET_Certificate_Format_Described()
    {
        DOMINATINGSET problem = new DOMINATINGSET();
        Assert.NotNull(problem.certificateFormat);
        Assert.NotEmpty(problem.certificateFormat);
        Assert.Contains("dominating", problem.certificateFormat);
    }

    [Fact]
    public void DOMINATINGSET_Certificate_Format_Example_Is_Actually_Valid()
    {
        // The example quoted in certificateFormat must be a real, verifiable
        // certificate for defaultInstance — not just descriptive prose.
        DOMINATINGSET problem = new DOMINATINGSET();
        DominatingSetVerifier verifier = new DominatingSetVerifier();
        Assert.True(verifier.verify(problem, "{1,3}"));
    }
}
