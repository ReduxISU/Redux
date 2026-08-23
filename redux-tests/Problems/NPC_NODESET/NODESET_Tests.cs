using Xunit;
using API.Problems.NPComplete.NPC_NODESET;
using API.Problems.NPComplete.NPC_NODESET.Verifiers;

namespace redux_tests;

#pragma warning disable CS1591

public class NODESET_Tests
{
    // -------------------------------------------------------------------------
    // Format declarations
    // -------------------------------------------------------------------------

    [Fact]
    public void NODESET_Instance_Format_Described()
    {
        NODESET problem = new NODESET();
        Assert.NotNull(problem.instanceFormat);
        Assert.NotEmpty(problem.instanceFormat);
        Assert.Contains("N,E),K", problem.instanceFormat);
    }

    [Fact]
    public void NODESET_Certificate_Format_Described()
    {
        NODESET problem = new NODESET();
        Assert.NotNull(problem.certificateFormat);
        Assert.NotEmpty(problem.certificateFormat);
        Assert.Contains("nodes", problem.certificateFormat);
    }

    [Fact]
    public void NODESET_Certificate_Format_Example_Is_Actually_Valid()
    {
        // The "Example: {3}" quoted in certificateFormat must be a real, verifiable
        // certificate for defaultInstance — not just descriptive prose.
        NODESET problem = new NODESET();
        NodeSetVerifier verifier = new NodeSetVerifier();
        Assert.True(verifier.verify(problem, "{3}"));
    }
}
