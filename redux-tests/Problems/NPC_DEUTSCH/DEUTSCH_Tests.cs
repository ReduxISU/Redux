using Xunit;
using API.Problems.NPComplete.NPC_DEUTSCH;
using API.Problems.NPComplete.NPC_DEUTSCH.Verifiers;

namespace redux_tests;
#pragma warning disable CS1591

public class DEUTSCH_tests {
    [Fact]
    public void DEUTSCH_Default_Instantiation() {
        var problem = new DEUTSCH();
        Assert.Equal("(0,1)", problem.instance);
        Assert.Equal("(0,1)", problem.defaultInstance);
    }

    [Fact]
    public void DEUTSCH_Custom_Instantiation() {
        string instance = "(0,0)";
        var problem = new DEUTSCH(instance);
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
    public void DEUTSCH_verifier(string instance, string certificate, bool expected) {
        var problem = new DEUTSCH(instance);
        var verifier = new DeutschClassicalVerifier();
        bool result = verifier.verify(problem, certificate);
        Assert.Equal(expected, result);

    }

    // -------------------------------------------------------------------------
    // Format declarations
    // -------------------------------------------------------------------------

    [Fact]
    public void DEUTSCH_Instance_Format_Described() {
        var problem = new DEUTSCH();
        Assert.NotNull(problem.instanceFormat);
        Assert.NotEmpty(problem.instanceFormat);
        Assert.Contains("i,w", problem.instanceFormat);
    }

    [Fact]
    public void DEUTSCH_Certificate_Format_Described() {
        var problem = new DEUTSCH();
        Assert.NotNull(problem.certificateFormat);
        Assert.NotEmpty(problem.certificateFormat);
        Assert.Contains("balanced", problem.certificateFormat);
    }

    [Fact]
    public void DEUTSCH_Certificate_Format_Example_Is_Actually_Valid() {
        // The "Example: balanced" quoted in certificateFormat must be a real,
        // verifiable certificate for defaultInstance — not just descriptive prose.
        var problem = new DEUTSCH();
        var verifier = new DeutschClassicalVerifier();
        Assert.True(verifier.verify(problem, "balanced"));
    }

    [Theory] //tests solver
    [InlineData("(0,0)", "constant")]
    [InlineData("(1,1)", "constant")]
    [InlineData("(0,1)", "balanced")]
    [InlineData("(1,0)", "balanced")]
    public void DEUTSCH_solver(string instance, string certificate) {
        var problem = new DEUTSCH(instance);
        var solver = problem.defaultSolver;
        string solvedString = solver.solve(problem);
        Assert.Equal(certificate, solvedString);
    }
}