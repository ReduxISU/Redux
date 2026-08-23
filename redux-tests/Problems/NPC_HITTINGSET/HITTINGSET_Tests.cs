using Xunit;
using API.Problems.NPComplete.NPC_HITTINGSET;
using API.Problems.NPComplete.NPC_HITTINGSET.Verifiers;

namespace redux_tests;

#pragma warning disable CS1591

public class HITTINGSET_Tests
{
    // -------------------------------------------------------------------------
    // Format declarations
    // -------------------------------------------------------------------------

    [Fact]
    public void HITTINGSET_Instance_Format_Described()
    {
        HITTINGSET problem = new HITTINGSET();
        Assert.NotNull(problem.instanceFormat);
        Assert.NotEmpty(problem.instanceFormat);
        Assert.Contains("(U,S)", problem.instanceFormat);
    }

    [Fact]
    public void HITTINGSET_Certificate_Format_Described()
    {
        HITTINGSET problem = new HITTINGSET();
        Assert.NotNull(problem.certificateFormat);
        Assert.NotEmpty(problem.certificateFormat);
        Assert.Contains("exactly one element", problem.certificateFormat);
    }

    [Fact]
    public void HITTINGSET_Certificate_Format_Example_Is_Actually_Valid()
    {
        // The example quoted in certificateFormat must be a real, verifiable
        // certificate for defaultInstance — not just descriptive prose.
        HITTINGSET problem = new HITTINGSET();
        HittingSetVerifier verifier = new HittingSetVerifier();
        Assert.True(verifier.verify(problem, "{1,2}"));
    }
}
