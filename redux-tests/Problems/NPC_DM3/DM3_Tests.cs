using Xunit;
using API.Problems.NPComplete.NPC_DM3;
using API.Problems.NPComplete.NPC_DM3.Verifiers;

namespace redux_tests;

#pragma warning disable CS1591

public class DM3_Tests
{
    // -------------------------------------------------------------------------
    // Format declarations
    // -------------------------------------------------------------------------

    [Fact]
    public void DM3_Instance_Format_Described()
    {
        DM3 problem = new DM3();
        Assert.NotNull(problem.instanceFormat);
        Assert.NotEmpty(problem.instanceFormat);
        Assert.Contains("3-tuple", problem.instanceFormat);
    }

    [Fact]
    public void DM3_Certificate_Format_Described()
    {
        DM3 problem = new DM3();
        Assert.NotNull(problem.certificateFormat);
        Assert.NotEmpty(problem.certificateFormat);
        Assert.Contains("3-tuples", problem.certificateFormat);
    }

    [Fact]
    public void DM3_Certificate_Format_Example_Is_Actually_Valid()
    {
        // The example quoted in certificateFormat must be a real, verifiable
        // certificate for defaultInstance — not just descriptive prose.
        DM3 problem = new DM3();
        GenericVerifierDM3 verifier = new GenericVerifierDM3();
        Assert.True(verifier.verify(problem, "{Paul,Austin,Jake}"));
    }
}
