using Xunit;
using API.Problems.NPComplete.NPC_EDITDISTANCE;
using API.Problems.NPComplete.NPC_EDITDISTANCE.Verifiers;
using API.Problems.NPComplete.NPC_EDITDISTANCE.Solvers;
using API.Interfaces;

namespace redux_tests;
#pragma warning disable CS1591

public class EDITDISTANCE_Tests {
    [Fact]
    public void EDITDISTANCE_Default_Instantiation() {
        var problem = new EDITDISTANCE();
        Assert.Equal("(horse, ros)", problem.instance);
        Assert.Equal("(horse, ros)", problem.defaultInstance);
    }

    [Fact]
    public void EDITDISTANCE_Custom_Instantiation() {
        var problem = new EDITDISTANCE("(intention, execution, 5)");
        Assert.Equal("(intention, execution, 5)", problem.instance);

    }

    [Theory] //tests verifier
    [InlineData("(horse, ros, 3)", "3")]
    [InlineData("(intention, execution, 5)", "5")]
    [InlineData("(cute, cute, 0)", "0")]
    [InlineData("(a, b, 1)", "1")]
    [InlineData("(cut, cuts, 1)", "1")]
    [InlineData("(cuts, cut, 1)", "1")]
    [InlineData("(abc, def, 3)", "3")]
    [InlineData("(cat, cut, 1)", "1")]
    [InlineData("(a, a, 0)", "0")]
    public void EDITDISTANCE_verifier(string instance, string certificate) {
        var problem = new EDITDISTANCE(instance);
        var verifier = new EditDistanceVerifier();
        Assert.True(verifier.verify(problem, certificate));
    }


    [Theory] //tests solver
    [InlineData("(horse, ros, 3)", "3")]
    [InlineData("(intention, execution, 5)", "5")]
    [InlineData("(cute, cute, 0)", "0")]
    [InlineData("(a, b, 1)", "1")]
    [InlineData("(cut, cuts, 1)", "1")]
    [InlineData("(cuts, cut, 1)", "1")]
    [InlineData("(abc, def, 3)", "3")]
    [InlineData("(cat, cut, 1)", "1")]
    [InlineData("(a, a, 0)", "0")]
    public void EDITDISTANCE_solver(string instance, string certificate) {
        var problem = new EDITDISTANCE(instance);
        var solver = new EditDistanceDPSolver();
        string solvedString = solver.solve(problem);
        Assert.Equal(certificate, solvedString);
    }
}