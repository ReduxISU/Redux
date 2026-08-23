using Xunit;
using API.Problems.NPComplete.NPC_DEUTSCHJOZSA;
using API.Problems.NPComplete.NPC_DEUTSCHJOZSA.Verifiers;
using API.Problems.NPComplete.NPC_DEUTSCHJOZSA.Solvers;

namespace redux_tests;
#pragma warning disable CS1591

public class DEUTSCHJOZSA_tests {
    [Fact]
    public void DEUTSCH_Default_Instantiation() {
        var problem = new DEUTSCHJOZSA();
        Assert.Equal("(1, 1, 1, 1)", problem.instance);
        Assert.Equal("(1, 1, 1, 1)", problem.defaultInstance);
    }

    [Fact]
    public void DEUTSCH_Custom_Instantiation() {
        string instance = "(0,0,1,1)";
        var problem = new DEUTSCHJOZSA(instance);
        Assert.Equal(instance, problem.instance);
    }

    [Theory] //tests verifier
    [InlineData("(0,0)", "constant", true)]
    [InlineData("(1,1)", "constant", true)]
    [InlineData("(0,1)", "balanced", true)]
    [InlineData("(1,0)", "balanced", true)]
    [InlineData("(0,0)", "balanced", false)]
    [InlineData("(1,1)", "balanced", false)]
    [InlineData("(0,1)", "constant", false)]
    [InlineData("(1,0)", "constant", false)]
    [InlineData("(0,0,0,0)", "constant", true)]
    [InlineData("(1,1,0,0)", "balanced", true)]
    [InlineData("(1,0,1,0)", "balanced", true)]
    [InlineData("(1,0,0,1)", "balanced", true)]
    [InlineData("(0,1,1,0)", "balanced", true)]
    [InlineData("(0,1,0,1)", "balanced", true)]
    [InlineData("(0,0,1,1)", "balanced", true)]
    [InlineData("(1,1,1,1)", "constant", true)]
    public void DEUTSCHJOZSA_verifier(string instance, string certificate, bool expected) {
        var problem = new DEUTSCHJOZSA(instance);
        var verifier = new DeutschJozsaVerifier();
        bool result = verifier.verify(problem, certificate);
        Assert.Equal(expected, result);
    }

    [Theory] //tests solver
    [InlineData("(0,0)", "constant")]
    [InlineData("(1,1)", "constant")]
    [InlineData("(0,1)", "balanced")]
    [InlineData("(1,0)", "balanced")]
    [InlineData("(0,0,0,0)", "constant")]
    [InlineData("(1,1,0,0)", "balanced")]
    [InlineData("(1,0,1,0)", "balanced")]
    [InlineData("(1,0,0,1)", "balanced")]
    [InlineData("(0,1,1,0)", "balanced")]
    [InlineData("(0,1,0,1)", "balanced")]
    [InlineData("(0,0,1,1)", "balanced")]
    [InlineData("(1,1,1,1)", "constant")]
    public void DEUTSCHJOZSA_solver(string instance, string certificate) {
        var problem = new DEUTSCHJOZSA(instance);
        var solver = new DeutschJozsaClassicalSolver();
        string solvedString = solver.solve(problem);

        // XXX normalize the certificate for now (can be removed after PR #95 is merged)
        solvedString = solvedString.Trim('{', '}').ToLower();
        Assert.Equal(certificate, solvedString);
    }

    // -------------------------------------------------------------------------
    // Format declarations
    // -------------------------------------------------------------------------

    [Fact]
    public void DEUTSCHJOZSA_Instance_Format_Described()
    {
        var problem = new DEUTSCHJOZSA();
        Assert.NotNull(problem.instanceFormat);
        Assert.NotEmpty(problem.instanceFormat);
        Assert.Contains("2^n", problem.instanceFormat);
    }

    [Fact]
    public void DEUTSCHJOZSA_Certificate_Format_Described()
    {
        var problem = new DEUTSCHJOZSA();
        Assert.NotNull(problem.certificateFormat);
        Assert.NotEmpty(problem.certificateFormat);
        Assert.Contains("balanced", problem.certificateFormat);
    }

    [Fact]
    public void DEUTSCHJOZSA_Certificate_Format_Example_Is_Actually_Valid()
    {
        // The "Example: constant" quoted in certificateFormat must be a real,
        // verifiable certificate for defaultInstance — not just descriptive prose.
        var problem = new DEUTSCHJOZSA();
        var verifier = new DeutschJozsaVerifier();
        Assert.True(verifier.verify(problem, "constant"));
    }
}