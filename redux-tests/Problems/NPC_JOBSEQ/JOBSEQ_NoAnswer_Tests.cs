using API.Problems.NPComplete.NPC_JOBSEQ;
using API.Problems.NPComplete.NPC_JOBSEQ.Solvers;
using API.Problems.NPComplete.NPC_JOBSEQ.Verifiers;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// #801: when no order keeps the late penalty within K, the brute force answers {} (no answer), and the verifier says false.
public class JOBSEQ_NoAnswer_Tests {
    // Every job takes 4, so whatever runs second or third is late; any two penalties make 10 > K = 4.
    private const string NothingFits = "((4,4,4),(4,5,6),(5,5,5),4)";

    [Fact]
    public void JOBSEQ_BruteForce_Answers_Braces_When_No_Order_Fits() {
        Assert.Equal("{}", new JobSeqBruteForce().solve(new JOBSEQ(NothingFits)));
    }

    [Fact]
    public void JOBSEQ_Verifier_Says_False_For_No_Answer() {
        Assert.False(new JobSeqVerifier().verify(new JOBSEQ(NothingFits), "{}"));
    }

    [Fact]
    public void JOBSEQ_No_Jobs_Still_Answers_The_Empty_Order() {
        // The empty order is a real answer when there are no jobs: nothing is late.
        var none = new JOBSEQ("((),(),(),4)");
        Assert.Equal("()", new JobSeqBruteForce().solve(none));
    }
}
