using API.Interfaces;
using API.Problems.NPComplete.NPC_SUBSETPRODUCT;
using API.Problems.NPComplete.NPC_SUBSETPRODUCT.Solvers;
using API.Problems.NPComplete.NPC_SUBSETPRODUCT.Verifiers;
using Xunit;

namespace redux_tests;

public class SUBSETPRODUCT_Tests {

    [Fact]
    public void SUBSETPRODUCT_DefaultInstance_Parses() {
        SUBSETPRODUCT problem = new SUBSETPRODUCT();
        Assert.Equal(new List<string> { "2", "3", "5", "7" }, problem.S);
        Assert.Equal(30, problem.T);
    }

    [Fact]
    public void SUBSETPRODUCT_MalformedInstance_ThrowsProblemParseException() {
        Assert.Throws<ProblemParseException>(() => new SUBSETPRODUCT("not an instance"));
    }

    [Fact]
    public void SUBSETPRODUCT_Verifier_AcceptsCorrectCertificate() {
        SUBSETPRODUCT problem = new SUBSETPRODUCT();
        Assert.True(new SubsetProductVerifier().verify(problem, "{2,3,5}"));
    }

    [Fact]
    public void SUBSETPRODUCT_Verifier_RejectsWrongProduct() {
        SUBSETPRODUCT problem = new SUBSETPRODUCT();
        Assert.False(new SubsetProductVerifier().verify(problem, "{2,7}"));
    }

    [Fact]
    public void SUBSETPRODUCT_Verifier_RejectsNumberNotInS() {
        SUBSETPRODUCT problem = new SUBSETPRODUCT();
        Assert.False(new SubsetProductVerifier().verify(problem, "{6,5}"));
    }

    [Fact]
    public void SUBSETPRODUCT_Solver_FindsAnAnswerTheVerifierAccepts() {
        SUBSETPRODUCT problem = new SUBSETPRODUCT();
        string answer = new SubsetProductSolver().solve(problem);
        Assert.True(problem.defaultVerifier.verify(problem, answer));
    }

    [Fact]
    public void SUBSETPRODUCT_Solver_ReturnsEmptySetWhenNoSubsetWorks() {
        SUBSETPRODUCT problem = new SUBSETPRODUCT("({2,3,5},7)");
        Assert.Equal("{}", new SubsetProductSolver().solve(problem));
    }
}
