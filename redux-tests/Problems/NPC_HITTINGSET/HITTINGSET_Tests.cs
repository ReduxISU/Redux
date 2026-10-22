using Xunit;
using API.Problems.NPComplete.NPC_HITTINGSET;
using API.Problems.NPComplete.NPC_HITTINGSET.Solvers;
using API.Problems.NPComplete.NPC_HITTINGSET.Verifiers;

namespace redux_tests;

#pragma warning disable CS1591

public class HITTINGSET_Tests {
    // -------------------------------------------------------------------------
    // Format declarations
    // -------------------------------------------------------------------------

    [Fact]
    public void HITTINGSET_Instance_Format_Described() {
        HITTINGSET problem = new HITTINGSET();
        Assert.NotNull(problem.instanceFormat);
        Assert.NotEmpty(problem.instanceFormat);
        Assert.Contains("(U,S)", problem.instanceFormat);
    }

    [Fact]
    public void HITTINGSET_Certificate_Format_Described() {
        HITTINGSET problem = new HITTINGSET();
        Assert.NotNull(problem.certificateFormat);
        Assert.NotEmpty(problem.certificateFormat);
        Assert.Contains("exactly one element", problem.certificateFormat);
    }

    [Fact]
    public void HITTINGSET_Certificate_Format_Example_Is_Actually_Valid() {
        // The example quoted in certificateFormat must be a real, verifiable
        // certificate for defaultInstance — not just descriptive prose.
        HITTINGSET problem = new HITTINGSET();
        HittingSetVerifier verifier = new HittingSetVerifier();
        Assert.True(verifier.verify(problem, HittingSetVerifier.CertificateExample));
    }
    // #776: with no hitting set the brute force answered "", which the verifier threw on; and the verifier accepted
    // elements outside U.
    [Theory]
    [InlineData("({1},{{}})")]
    [InlineData("({1,2,3},{{1,2},{2,3},{1,3}})")]
    public void HittingSetBruteForce_Answers_Empty_Braces_When_No_Hitting_Set_Exists(string instance) {
        HITTINGSET problem = new HITTINGSET(instance);
        string certificate = new HittingSetBruteForce().solve(problem);
        Assert.Equal("{}", certificate);
        Assert.False(new HittingSetVerifier().verify(problem, certificate));
    }

    [Fact]
    public void HittingSetVerifier_Returns_False_For_An_Empty_Certificate() {
        Assert.False(new HittingSetVerifier().verify(new HITTINGSET("({1},{{1}})"), ""));
    }

    [Theory]
    [InlineData("({1,2},{{1,2}})", "{1,5}", false)]
    [InlineData("({1,2},{{1,2}})", "{1}", true)]
    [InlineData("({1,2},{})", "{}", true)]
    public void HittingSetVerifier_Requires_W_To_Be_A_Subset_Of_U(string instance, string certificate, bool valid) {
        Assert.Equal(valid, new HittingSetVerifier().verify(new HITTINGSET(instance), certificate));
    }

    [Fact]
    public void HITTINGSET_Is_Named_Exact_Hitting_Set() {
        // The code hits every set exactly once, so the name and text say so (#698).
        HITTINGSET problem = new HITTINGSET();
        Assert.Equal("Exact Hitting Set", problem.problemName);
        Assert.Contains("Exact Hitting Set", problem.problemDefinition);
        Assert.Contains("exactly one", problem.outputDescription);
        Assert.Contains("Exact Hitting Set", problem.defaultVerifier.verifierName);
        Assert.Contains("Exact Hitting Set", problem.defaultSolver.solverName);
    }

    [Theory]
    [InlineData("{1,2}", true)]
    [InlineData("{1,4}", false)]       // hits {1,4} twice
    [InlineData("{1,2,3}", false)]     // hits {1,3} twice
    [InlineData("{9}", false)]         // not a subset of U
    [InlineData("{1,2,9}", false)]     // 9 is not in U
    [InlineData("{}", false)]          // hits nothing
    public void HITTINGSET_Verifier_Requires_Exactly_One_Hit_And_Subset_Of_U(string certificate, bool expected) {
        HITTINGSET problem = new HITTINGSET();
        Assert.Equal(expected, new HittingSetVerifier().verify(problem, certificate));
    }
}
