using API.Interfaces;
using API.Problems.NPComplete.NPC_JOBSEQ;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// #800: T, D and P give one number per job, so they must be the same length, there is at least one job, and no time, deadline
// or penalty is negative.
public class JOBSEQ_Input_Tests {
    [Theory]
    [InlineData("((4,2,5),(9,13),(1,4,3),4)")]       // two deadlines for three jobs
    [InlineData("((4,2,5),(9,13,1),(1,4),4)")]       // two penalties for three jobs
    [InlineData("((4,2),(9,13,1),(1,4,3),4)")]       // two times for three jobs
    [InlineData("((-4,2,5),(9,13,1),(1,4,3),4)")]    // a negative time
    [InlineData("((4,2,5),(9,-13,1),(1,4,3),4)")]    // a negative deadline
    [InlineData("((4,2,5),(9,13,1),(1,-4,3),4)")]    // a negative penalty
    [InlineData("((),(),(),4)")]                     // no jobs at all
    public void JOBSEQ_Mismatched_Or_Negative_Is_A_Parse_Error(string instance) {
        Assert.Throws<ProblemParseException>(() => new JOBSEQ(instance));
    }

    [Theory]
    [InlineData("((4,2,5,9,4,3),(9,13,2,17,21,16),(1,4,3,2,5,8),4)")]
    [InlineData("((0,2),(0,0),(0,4),0)")]   // zeros are fine
    [InlineData("((3),(2),(5),0)")]         // one job
    public void JOBSEQ_Matching_NonNegative_Lists_Still_Parse(string instance) {
        var jobSeq = new JOBSEQ(instance);
        Assert.Equal(jobSeq.T.Count, jobSeq.D.Count);
        Assert.Equal(jobSeq.T.Count, jobSeq.P.Count);
    }
}
