using Xunit;
using API.Problems.NPComplete.NPC_KNAPSACK;
using API.Problems.NPComplete.NPC_KNAPSACK.Solvers;
using API.Problems.NPComplete.NPC_KNAPSACK.Verifiers;

namespace redux_tests;
#pragma warning disable CS1591

public class KNAPSACK_Tests {
    [Fact]
    public void KnapsackDP_Default_Instance_Verifies() {
        KNAPSACK problem = new KNAPSACK();
        string certificate = new KnapsackDP().solve(problem);
        Assert.True(new KnapsackVerifier().verify(problem, certificate));
    }

    [Fact]
    public void KnapsackDP_BeatsGreedy_OnSplitItems() {
        // greedy-by-value picks (20,100) alone for value 100; optimum is (10,60)+(10,55) = 115
        KNAPSACK problem = new KNAPSACK("({(20,100),(10,60),(10,55)},20,115)");
        string certificate = new KnapsackDP().solve(problem);
        Assert.True(new KnapsackVerifier().verify(problem, certificate));
    }

    [Fact]
    public void KnapsackDP_ZeroCapacity_ReturnsEmpty() {
        KNAPSACK problem = new KNAPSACK("({(10,60),(20,100)},0,0)");
        string certificate = new KnapsackDP().solve(problem);
        Assert.Equal("{}", certificate);
        Assert.True(new KnapsackVerifier().verify(problem, certificate));
    }

    [Fact]
    public void KnapsackDP_TargetValueZero_AnySubsetVerifies() {
        KNAPSACK problem = new KNAPSACK("({(5,10),(7,15)},20,0)");
        string certificate = new KnapsackDP().solve(problem);
        Assert.True(new KnapsackVerifier().verify(problem, certificate));
    }

    [Fact]
    public void KnapsackDP_SingleItem_Fits_ReachesTarget() {
        KNAPSACK problem = new KNAPSACK("({(5,10)},10,10)");
        string certificate = new KnapsackDP().solve(problem);
        Assert.True(new KnapsackVerifier().verify(problem, certificate));
    }

    [Fact]
    public void KnapsackDP_SingleItem_DoesNotFit_VerifierRejects() {
        KNAPSACK problem = new KNAPSACK("({(20,100)},10,1)");
        string certificate = new KnapsackDP().solve(problem);
        Assert.False(new KnapsackVerifier().verify(problem, certificate));
    }

    [Fact]
    public void KnapsackDP_AllItemsFit_TakesEnoughForTarget() {
        KNAPSACK problem = new KNAPSACK("({(1,10),(2,20),(3,30)},100,60)");
        string certificate = new KnapsackDP().solve(problem);
        Assert.True(new KnapsackVerifier().verify(problem, certificate));
    }

    [Theory]
    [InlineData("({(10,60),(20,100),(30,120)},50,220)")]
    [InlineData("({(2,3),(3,4),(4,5),(5,6)},5,7)")]
    [InlineData("({(1,1),(2,2),(3,3),(4,4),(5,5)},10,9)")]
    [InlineData("({(7,9),(3,4),(4,5),(8,11),(2,3)},10,14)")]
    [InlineData("({(5,10),(4,40),(6,30),(3,50)},10,90)")]
    [InlineData("({(1,6),(2,10),(3,12)},5,22)")]
    public void KnapsackDP_Verifies_OnFeasibleInstances(string instance) {
        KNAPSACK problem = new KNAPSACK(instance);
        string certificate = new KnapsackDP().solve(problem);
        Assert.True(new KnapsackVerifier().verify(problem, certificate));
    }

    [Fact]
    public void KnapsackDP_ValueBelowOptimal_ReturnsNoSolutionSentinel() {
        // Same instance as the default, but V bumped from 220 to 221, which is unreachable
        // within weight 50 (optimal achievable value is still 220). Regression test for #256:
        // KnapsackDP previously ignored V entirely and returned the 220-value subset anyway.
        KNAPSACK problem = new KNAPSACK("({(10,60),(20,100),(30,120)},50,221)");
        string certificate = new KnapsackDP().solve(problem);
        Assert.Equal("{}", certificate);
        Assert.False(new KnapsackVerifier().verify(problem, certificate));
    }

    [Fact]
    public void KnapsackDP_And_KnapsackBruteForce_Agree_OnNoInstance() {
        // Single item (10,10) does not fit in capacity 5 at all, so even the empty
        // selection cannot reach V=1. Both solvers should fail to produce a
        // verifier-accepted certificate.
        KNAPSACK problem = new KNAPSACK("({(10,10)},5,1)");

        string dpCertificate = new KnapsackDP().solve(problem);
        string bruteForceCertificate = new KnapsackBruteForce().solve(problem);

        Assert.Equal("{}", dpCertificate);
        Assert.Equal("", bruteForceCertificate);
        Assert.False(new KnapsackVerifier().verify(problem, dpCertificate));
    }

    [Fact]
    public void KnapsackDP_ValueExactlyEqualsOptimal_ReturnsNonEmptyVerifiedCertificate() {
        // V equals the optimal achievable value exactly (220 = value of items 2+3 within
        // weight 50). This must still succeed -- confirms the new "< V" check isn't off-by-one.
        KNAPSACK problem = new KNAPSACK("({(10,60),(20,100),(30,120)},50,220)");
        string certificate = new KnapsackDP().solve(problem);
        Assert.NotEqual("{}", certificate);
        Assert.True(new KnapsackVerifier().verify(problem, certificate));
    }
}
