using Xunit;
using API.Problems.NPComplete.NPC_PRIMEFACTOR;
using API.Problems.NPComplete.NPC_PRIMEFACTOR.Verifiers;
using API.Problems.NPComplete.NPC_PRIMEFACTOR.Solvers;

namespace redux_tests;
#pragma warning disable CS1591

public class PRIMEFACTOR_tests {
    [Fact]
    public void DEUTSCH_Default_Instantiation()
    {
        var problem = new PRIMEFACTOR();
        Assert.Equal("15", problem.instance);
        Assert.Equal("15", problem.defaultInstance);
    } 

    [Fact]
    public void PRIMEFACTOR_Custom_Instantiation() {
        string instance = "56";
        var problem = new PRIMEFACTOR(instance);
        Assert.Equal(instance, problem.instance);
    }

    [Theory] //tests verifier
    [InlineData("15", "(3,5)")]
    [InlineData("15", "(5,3)")]
    [InlineData("7", "(7)")]
    [InlineData("100", "(2,5,2,5)")]
    [InlineData("97", "(97)")]
    [InlineData("3072", "(2,2,2,2,2,2,2,2,2,2,3)")]
    [InlineData("3072", "(2,2,2,2,2,2,2,3,2,2,2)")]
    [InlineData("87625999", "(71,839,1471)")]
    [InlineData("87625999", "(1471,71,839)")]
    public void PRIMEFACTOR_verifier(string instance, string certificate) {
        var problem = new PRIMEFACTOR(instance);
        var verifier = new PrimeFactorVerifier();
        Assert.True(verifier.verify(problem, certificate));
    }

    [Theory] //tests solver
    [InlineData("15", "(3,5)")]
    [InlineData("7", "(7)")]
    [InlineData("100", "(2,2,5,5)")]
    [InlineData("97", "(97)")]
    [InlineData("3072", "(2,2,2,2,2,2,2,2,2,2,3)")]
    [InlineData("87625999", "(71,839,1471)")]
    public void PRIMEFACTOR_solver(string instance, string certificate)
    {
        var problem = new PRIMEFACTOR(instance);
        var solver = new PrimeFactorSolver();
        string solvedString = solver.solve(problem);
        Assert.Equal(certificate, solvedString);
    }
}