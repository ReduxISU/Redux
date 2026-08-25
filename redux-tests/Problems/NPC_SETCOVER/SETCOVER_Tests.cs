using Xunit;
using API.Problems.NPComplete.NPC_SETCOVER;
using API.Problems.NPComplete.NPC_SETCOVER.Solvers;
using API.Problems.NPComplete.NPC_SETCOVER.Verifiers;

namespace redux_tests;
#pragma warning disable CS1591

public class SETCOVER_Tests {

    // ----- Construction ----- //

    [Fact]
    public void SETCOVER_Default_Instantiation() {
        SETCOVER problem = new SETCOVER();
        Assert.Equal(problem.defaultInstance, problem.instance);
        Assert.Equal(new List<string> { "1", "2", "3", "4", "5" }, problem.universal);
        Assert.Equal(
            new List<List<string>> {
                new List<string> { "1", "2", "3" },
                new List<string> { "2", "4" },
                new List<string> { "3", "4" },
                new List<string> { "4", "5" },
            },
            problem.subsets);
        Assert.Equal(3, problem.K);
    }

    [Fact]
    public void SETCOVER_Custom_Instantiation() {
        string instance = "({1,2,3,4,5,6},{{1,2},{3,4},{5,6}},2)";
        SETCOVER problem = new SETCOVER(instance);
        Assert.Equal(instance, problem.instance);
        Assert.Equal(new List<string> { "1", "2", "3", "4", "5", "6" }, problem.universal);
        Assert.Equal(
            new List<List<string>> {
                new List<string> { "1", "2" },
                new List<string> { "3", "4" },
                new List<string> { "5", "6" },
            },
            problem.subsets);
        Assert.Equal(2, problem.K);
    }

    // ----- Solver + Verifier round trip ----- //

    [Fact]
    public void SetCoverBruteForce_Default_Instance_Verifies() {
        SETCOVER problem = new SETCOVER();
        string certificate = new SetCoverBruteForce().solve(problem);
        Assert.NotEqual("{}", certificate);
        Assert.True(new SetCoverVerifier().verify(problem, certificate));
    }

    // ----- Regression tests: old (flattening) verifier incorrectly accepted these ----- //

    [Theory]
    [InlineData("{{1,2,3,4,5}}")] // single "subset" == whole universe, not a member of S
    [InlineData("{{1,2},{3,4,5}}")] // neither {1,2} nor {3,4,5} is a member of S
    [InlineData("{1,2,3,4,5}")] // bare universe elements, no subset structure at all
    public void SetCoverVerifier_Rejects_Certificates_With_NonMember_Subsets(string certificate) {
        SETCOVER problem = new SETCOVER();
        Assert.False(new SetCoverVerifier().verify(problem, certificate));
    }

    // ----- Valid certificate ----- //

    [Fact]
    public void SetCoverVerifier_Accepts_Valid_Cover_Within_K() {
        SETCOVER problem = new SETCOVER();
        string certificate = "{{1,2,3},{4,5}}";
        Assert.True(new SetCoverVerifier().verify(problem, certificate));
    }

    // ----- K-bound enforcement ----- //

    [Fact]
    public void SetCoverVerifier_Rejects_Certificate_Exceeding_K_Even_If_Every_Subset_Is_Valid_And_Covers_U() {
        SETCOVER problem = new SETCOVER();
        // All 4 subsets of S: every one is a genuine member, the union covers U, but count 4 > K=3.
        string certificate = "{{1,2,3},{2,4},{3,4},{4,5}}";
        Assert.False(new SetCoverVerifier().verify(problem, certificate));
    }

    // ----- Incomplete coverage ----- //

    [Fact]
    public void SetCoverVerifier_Rejects_Valid_Subsets_That_Do_Not_Cover_Universe() {
        SETCOVER problem = new SETCOVER();
        // {1,2,3} is a genuine member of S but leaves 4 and 5 uncovered.
        string certificate = "{{1,2,3}}";
        Assert.False(new SetCoverVerifier().verify(problem, certificate));
    }

    // ----- Order independence ----- //

    [Fact]
    public void SetCoverVerifier_Accepts_Subset_Elements_In_Different_Order() {
        SETCOVER problem = new SETCOVER();
        // {1,2,3} is a member of S; listing its elements as {3,1,2} should still match via set comparison.
        string certificate = "{{3,1,2},{4,5}}";
        Assert.True(new SetCoverVerifier().verify(problem, certificate));
    }
}
