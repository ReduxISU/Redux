using Xunit;
using API.Problems.NPComplete.NPC_CUT;
using API.Problems.NPComplete.NPC_CUT.Verifiers;

namespace redux_tests;

#pragma warning disable CS1591

public class CUT_Tests
{
    // -------------------------------------------------------------------------
    // Format declarations
    // -------------------------------------------------------------------------

    [Fact]
    public void CUT_Instance_Format_Described()
    {
        CUT cut = new CUT();
        Assert.NotNull(cut.instanceFormat);
        Assert.NotEmpty(cut.instanceFormat);
        Assert.Contains("N,E),K", cut.instanceFormat);
    }

    [Fact]
    public void CUT_Certificate_Format_Described()
    {
        CUT cut = new CUT();
        Assert.NotNull(cut.certificateFormat);
        Assert.NotEmpty(cut.certificateFormat);
        Assert.Contains("edges", cut.certificateFormat);
    }

    [Fact]
    public void CUT_Certificate_Format_Example_Is_Actually_Valid()
    {
        // The example quoted in certificateFormat must be a real, verifiable
        // certificate for defaultInstance — not just descriptive prose.
        CUT cut = new CUT();
        CutVerifier verifier = new CutVerifier();
        Assert.True(verifier.verify(cut, "{{2,1},{1,3},{2,3},{3,5},{2,4}}"));
    }
}
