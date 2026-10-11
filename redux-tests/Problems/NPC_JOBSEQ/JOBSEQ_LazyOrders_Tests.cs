using API.Problems.NPComplete.NPC_JOBSEQ;
using API.Problems.NPComplete.NPC_JOBSEQ.Solvers;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// #802: the brute force tries the orders one at a time instead of building all n! of them first.
public class JOBSEQ_LazyOrders_Tests {
    [Fact]
    public void JOBSEQ_BruteForce_Does_Not_Build_Every_Order_When_The_First_Fits() {
        // Ten unit jobs, all due at 10: the first order, (0,1,...,9), fits. Building all 10! orders first allocates
        // hundreds of MB; trying them one at a time allocates almost nothing.
        var jobSeq = new JOBSEQ("((1,1,1,1,1,1,1,1,1,1),(10,10,10,10,10,10,10,10,10,10),(1,1,1,1,1,1,1,1,1,1),0)");
        var solver = new JobSeqBruteForce();
        long before = GC.GetAllocatedBytesForCurrentThread();
        string answer = solver.solve(jobSeq);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal("(0,1,2,3,4,5,6,7,8,9)", answer);
        Assert.True(allocated < 1_000_000, $"allocated {allocated:N0} bytes");
    }

    [Fact]
    public void JOBSEQ_Orders_Come_In_The_Same_Order_As_Before() {
        // The answer is the first order that fits, so the order of the orders must not change.
        for (int n = 0; n <= 6; n++)
            Assert.Equal(JobSeqBruteForce.GenerateCombinations(n).Select(o => string.Join(",", o)),
                JobSeqBruteForce.Orders(n).Select(o => string.Join(",", o)));
    }

    [Theory]
    [InlineData("((4,2,5,9,4,3),(9,13,2,17,21,16),(1,4,3,2,5,8),4)", "(1,3,5,4,0,2)")]
    [InlineData("((5,1,5,4),(6,12,9,6),(7,9,12,17),9)", "(3,2,1,0)")]
    [InlineData("((1,1,4,1),(6,2,4,4),(4,5,6,3),6)", "(0,1,3,2)")]
    public void JOBSEQ_BruteForce_Answers_Are_Unchanged(string instance, string expected) {
        Assert.Equal(expected, new JobSeqBruteForce().solve(new JOBSEQ(instance)));
    }
}
