using Xunit;
using API.Problems.NPComplete.NPC_BERNSTEINVAZIRANI;
using API.Problems.NPComplete.NPC_BERNSTEINVAZIRANI.Verifiers;

namespace redux_tests;
#pragma warning disable CS1591

public class BERNSTEINVAZIRANI_tests {
    [Fact]
    public void BERNSTEINVAZIRANI_Default_Instantiation() {
        var problem = new BERNSTEINVAZIRANI();
        Assert.Equal("(0,1,0,1,1,0,1,0)", problem.instance);
        Assert.Equal("(0,1,0,1,1,0,1,0)", problem.defaultInstance);
    } 

    [Fact]
    public void BERNSTEINVAZIRANI_Custom_Instantiation() {
        string instance = "(0,1,0,1,0,1,0,1)";
        var problem = new BERNSTEINVAZIRANI(instance);
        Assert.Equal(instance, problem.instance);
    }

    [Theory] //tests verifier
    [InlineData("(0,1,0,1,1,0,1,0)", "(1,0,1)", true)]
    [InlineData("(0,1,1,0,1,0,0,1)", "(1,1,1)", true)]
    [InlineData("(0,1,0,1,0,1,0,1)", "(0,0,1)", true)]
    [InlineData("(0,0,0,0,0,0,0,0)", "(0,0,0)", true)]
    public void BERNSTEINVAZIRANI_verifier(string instance, string certificate, bool expected) {
        var problem = new BERNSTEINVAZIRANI(instance);
        var verifier = new BernsteinVaziraniClassicalVerifier();
        bool result = verifier.verify(problem, certificate);
        Assert.Equal(expected, result);

    }

    [Theory] //tests solver
    [InlineData("(0,1,0,1,1,0,1,0)", "(1,0,1)")]
    [InlineData("(0,1,1,0,1,0,0,1)", "(1,1,1)")]
    [InlineData("(0,0,0,0,0,0,0,0)", "(0,0,0)")]
    [InlineData("(0,1,0,1,0,1,0,1)", "(0,0,1)")]
    public void BERNSTEINVAZIRANI_solver(string instance, string certificate) {
        var problem = new BERNSTEINVAZIRANI(instance);
        var solver = problem.defaultSolver;
        string solvedString = solver.solve(problem);
        Assert.Equal(certificate, solvedString);
    }
}