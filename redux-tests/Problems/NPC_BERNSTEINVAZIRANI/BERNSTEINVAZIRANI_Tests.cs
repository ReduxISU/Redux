using Xunit;
using API.Problems.NPComplete.NPC_BERNSTEINVAZIRANI;
using API.Problems.NPComplete.NPC_BERNSTEINVAZIRANI.Verifiers;

namespace redux_tests;
#pragma warning disable CS1591

public class BERNSTEINVAZIRANI_tests {
    [Fact]
    public void BERNSTEINVAZIRANI_Default_Instantiation() {
        var problem = new BERNSTEINVAZIRANI();
        Assert.Equal("(3,(0,1,0,1,1,0,1,0))", problem.instance);
        Assert.Equal("(3,(0,1,0,1,1,0,1,0))", problem.defaultInstance);
    } 

    [Fact]
    public void BERNSTEINVAZIRANI_Custom_Instantiation() {
        string instance = "(3,(0,1,0,1,1,0,1,0))";
        var problem = new BERNSTEINVAZIRANI(instance);
        Assert.Equal(instance, problem.instance);
    }

    [Theory] //tests verifier
    [InlineData("(3,(0,1,0,1,1,0,1,0))", "101", true)]
    [InlineData("(3,(0,1,1,0,1,0,0,1))", "111", true)]
    [InlineData("(3,(0,1,0,1,0,1,0,1))", "001", true)]
    [InlineData("(3,(0,0,0,0,0,0,0,0))", "000", true)]
    public void BERNSTEINVAZIRANI_verifier(string instance, string certificate, bool expected) {
        var problem = new BERNSTEINVAZIRANI(instance);
        var verifier = new BernsteinVaziraniClassicalVerifier();
        bool result = verifier.verify(problem, certificate);
        Assert.Equal(expected, result);

    }

    [Theory] //tests solver
    [InlineData("(3,(0,1,0,1,1,0,1,0))", "101")]
    [InlineData("(3,(0,1,1,0,1,0,0,1))", "111")]
    [InlineData("(3,(0,0,0,0,0,0,0,0))", "000")]
    [InlineData("(3,(0,1,0,1,0,1,0,1))", "001")]
    public void BERNSTEINVAZIRANI_solver(string instance, string certificate) {
        var problem = new BERNSTEINVAZIRANI(instance);
        var solver = problem.defaultSolver;
        string solvedString = solver.solve(problem);
        Assert.Equal(certificate, solvedString);
    }
}