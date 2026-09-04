using Xunit;
using API.Problems.NPComplete.NPC_PARTITION;
using API.Problems.NPComplete.NPC_PARTITION.Verifiers;

namespace redux_tests;

#pragma warning disable CS1591

public class PARTITION_Tests {
    // -------------------------------------------------------------------------
    // Format declarations
    // -------------------------------------------------------------------------

    [Fact]
    public void PARTITION_Instance_Format_Described() {
        PARTITION problem = new PARTITION();
        Assert.NotNull(problem.instanceFormat);
        Assert.NotEmpty(problem.instanceFormat);
        Assert.Contains("set of positive integers", problem.instanceFormat);
    }

    [Fact]
    public void PARTITION_Certificate_Format_Described() {
        PARTITION problem = new PARTITION();
        Assert.NotNull(problem.certificateFormat);
        Assert.NotEmpty(problem.certificateFormat);
        Assert.Contains("(S1),(S2)", problem.certificateFormat);
    }

    [Fact]
    public void PARTITION_Certificate_Format_Example_Is_Actually_Valid() {
        // The example quoted in certificateFormat must be a real, verifiable
        // certificate for defaultInstance — not just descriptive prose.
        // (S is parsed as a set, so the duplicate "12" in defaultInstance collapses
        // to one element; S actually has 11 distinct values, not 12.)
        PARTITION problem = new PARTITION();
        PartitionVerifier verifier = new PartitionVerifier();
        Assert.True(verifier.verify(problem, "(33,21,15),(1,7,12,11,5,6,9,18)"));
    }
}
