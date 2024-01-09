using Xunit;
using API.Problems.NPComplete.NPC_JOBSEQ;
using API.Problems.NPComplete.NPC_JOBSEQ.Verifiers;
using API.Problems.NPComplete.NPC_JOBSEQ.Solvers;

namespace redux_tests;
#pragma warning disable CS1591

public class JOBSEQ_tests {
    [Fact]
    public void JOBSEQ_Default_Instantiation() {
        JOBSEQ jobSeq = new JOBSEQ();
        Assert.Equal(jobSeq.instance, "((4,2,5,9,4,3),(9,13,2,17,21,16),(1,4,3,2,5,8),4)");
        Assert.Equal("((4,2,5,9,4,3),(9,13,2,17,21,16),(1,4,3,2,5,8),4)", jobSeq.defaultInstance);
    } 

    [Fact]
    public void JOBSEQ_Custom_Instantiation() {
        string instance = "((5,1,5,4),(6,12,9,6),(7,9,12,17),9)";
        JOBSEQ jobSeq = new JOBSEQ(instance);
        Assert.Equal(instance, jobSeq.instance);
    }

    [Theory] //Tests independent set verifier with a few certificates

    [InlineData("((5,1,5,4),(6,12,9,6),(7,9,12,17),9)", "(3,2,1,0)", true)]
    [InlineData("((4,2,5,9,4,3),(9,13,2,17,21,16),(1,4,3,2,5,8),4)", "(1,3,5,4,0,2)", true)]
    [InlineData("((5,1,5,4),(6,12,9,6),(7,9,12,17),9)", "(0,2,1,3)", false)]
    [InlineData("((4,2,5,9,4,3),(9,13,2,17,21,16),(1,4,3,2,5,8),4)", "(0,1,2,3,4,5)", false)]
    public void JOBSEQ_verifier(string instance, string certificate, bool expected) {
        JOBSEQ jobSeq = new JOBSEQ(instance);
        JobSeqVerifier verifier = new JobSeqVerifier();
        bool result = verifier.verify(jobSeq, certificate);
        Assert.Equal(expected, result);

    }


    [Theory] //tests solver
    [InlineData("((5,1,5,4),(6,12,9,6),(7,9,12,17),9)", "(3,2,1,0)")]
    [InlineData("((4,2,5,9,4,3),(9,13,2,17,21,16),(1,4,3,2,5,8),4)", "(1,3,5,4,0,2)")]
    public void JOBSEQ_solver(string instance, string certificate) {
        JOBSEQ jobSeq = new JOBSEQ(instance);
        JobSeqBruteForce solver = jobSeq.defaultSolver;
        string solvedString = solver.solve(jobSeq);
        Assert.Equal(certificate, solvedString);
    }
}