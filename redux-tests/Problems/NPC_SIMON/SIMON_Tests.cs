using Xunit;
using API.Problems.NPComplete.NPC_SIMON;
using API.Problems.NPComplete.NPC_SIMON.Verifiers;
using API.Problems.NPComplete.NPC_SIMON.Solvers;

namespace redux_tests;
#pragma warning disable CS1591

public class SIMON_tests {
    [Fact]
    public void SIMON_Default_Instantiation() {
        SIMON jobSeq = new SIMON();
        Assert.Equal("(5, 6, 5, 6, 3, 2, 3, 2)", jobSeq.instance);
        Assert.Equal("(5, 6, 5, 6, 3, 2, 3, 2)", jobSeq.defaultInstance);
    } 

    [Fact]
    public void SIMON_Custom_Instantiation() {
        string instance = "(5,1,3,2,3,2,5,1)";
        var problem = new SIMON(instance);
        Assert.Equal(instance, problem.instance);
    }

    [Theory] //Tests independent set verifier with a few certificates
    [InlineData("(5,6,5,6,3,2,3,2)", "010", true)]
    [InlineData("(5,1,3,2,3,2,5,1)", "000", false)]
    [InlineData("(5,1,3,2,3,2,5,1)", "001", false)]
    [InlineData("(5,1,3,2,3,2,5,1)", "010", false)]
    [InlineData("(5,1,3,2,3,2,5,1)", "011", false)]
    [InlineData("(5,1,3,2,3,2,5,1)", "100", false)]
    [InlineData("(5,1,3,2,3,2,5,1)", "101", false)]
    [InlineData("(5,1,3,2,3,2,5,1)", "110", true)]
    [InlineData("(5,1,3,2,3,2,5,1)", "111", false)]
    public void SIMON_verifier(string instance, string certificate, bool expected) {
        var problem = new SIMON(instance);
        var verifier = new SimonVerifier();
        bool result = verifier.verify(problem, certificate);
        Assert.Equal(expected, result);

    }


    [Theory] //tests solver
    [InlineData("(5,6,5,6,3,2,3,2)", "010")]
    [InlineData("(5,1,3,2,3,2,5,1)", "110")]
    public void JOBSEQ_solver(string instance, string certificate) {
        var problem = new SIMON(instance);
        var solver = problem.defaultSolver;
        string solvedString = solver.solve(problem);
        Assert.Equal(certificate, solvedString);
    }
}