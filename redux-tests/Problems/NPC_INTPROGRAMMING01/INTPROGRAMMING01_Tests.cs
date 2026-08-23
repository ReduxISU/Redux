using Xunit;
using API.Problems.NPComplete.NPC_INTPROGRAMMING01;
using API.Problems.NPComplete.NPC_INTPROGRAMMING01.Verifiers;

namespace redux_tests;

#pragma warning disable CS1591

public class INTPROGRAMMING01_Tests
{
    // -------------------------------------------------------------------------
    // Format declarations
    // -------------------------------------------------------------------------

    [Fact]
    public void INTPROGRAMMING01_Instance_Format_Described()
    {
        INTPROGRAMMING01 problem = new INTPROGRAMMING01();
        Assert.NotNull(problem.instanceFormat);
        Assert.NotEmpty(problem.instanceFormat);
        Assert.Contains("<=", problem.instanceFormat);
    }

    [Fact]
    public void INTPROGRAMMING01_Certificate_Format_Described()
    {
        INTPROGRAMMING01 problem = new INTPROGRAMMING01();
        Assert.NotNull(problem.certificateFormat);
        Assert.NotEmpty(problem.certificateFormat);
        Assert.Contains("bits", problem.certificateFormat);
    }

    [Fact]
    public void INTPROGRAMMING01_Certificate_Format_Example_Is_Actually_Valid()
    {
        // The "Example: (0 0 0)" quoted in certificateFormat must be a real,
        // verifiable certificate for defaultInstance — not just descriptive prose.
        INTPROGRAMMING01 problem = new INTPROGRAMMING01();
        GenericVerifier01INTP verifier = new GenericVerifier01INTP();
        Assert.True(verifier.verify(problem, "(0 0 0)"));
    }
}
