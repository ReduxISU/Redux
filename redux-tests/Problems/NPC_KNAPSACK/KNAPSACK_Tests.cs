using Xunit;
using API.Problems.NPComplete.NPC_KNAPSACK;
using API.Problems.NPComplete.NPC_KNAPSACK.Solvers;
using API.Problems.NPComplete.NPC_KNAPSACK.Verifiers;

namespace redux_tests;
#pragma warning disable CS1591

public class KNAPSACK_Tests
{
    [Fact]
    public void KnapsackDP_Default_Instance_Verifies()
    {
        KNAPSACK problem = new KNAPSACK();
        string certificate = new KnapsackDP().solve(problem);
        Assert.True(new KnapsackVerifier().verify(problem, certificate));
    }

    [Fact]
    public void KnapsackDP_BeatsGreedy_OnSplitItems()
    {
        // greedy-by-value picks (20,100) alone for value 100; optimum is (10,60)+(10,55) = 115
        KNAPSACK problem = new KNAPSACK("({(20,100),(10,60),(10,55)},20,115)");
        string certificate = new KnapsackDP().solve(problem);
        Assert.True(new KnapsackVerifier().verify(problem, certificate));
    }

    [Fact]
    public void KnapsackDP_ZeroCapacity_ReturnsEmpty()
    {
        KNAPSACK problem = new KNAPSACK("({(10,60),(20,100)},0,0)");
        string certificate = new KnapsackDP().solve(problem);
        Assert.Equal("{}", certificate);
        Assert.True(new KnapsackVerifier().verify(problem, certificate));
    }

    [Fact]
    public void KnapsackDP_TargetValueZero_AnySubsetVerifies()
    {
        KNAPSACK problem = new KNAPSACK("({(5,10),(7,15)},20,0)");
        string certificate = new KnapsackDP().solve(problem);
        Assert.True(new KnapsackVerifier().verify(problem, certificate));
    }

    [Fact]
    public void KnapsackDP_SingleItem_Fits_ReachesTarget()
    {
        KNAPSACK problem = new KNAPSACK("({(5,10)},10,10)");
        string certificate = new KnapsackDP().solve(problem);
        Assert.True(new KnapsackVerifier().verify(problem, certificate));
    }

    [Fact]
    public void KnapsackDP_SingleItem_DoesNotFit_VerifierRejects()
    {
        KNAPSACK problem = new KNAPSACK("({(20,100)},10,1)");
        string certificate = new KnapsackDP().solve(problem);
        Assert.False(new KnapsackVerifier().verify(problem, certificate));
    }

    [Fact]
    public void KnapsackDP_AllItemsFit_TakesEnoughForTarget()
    {
        KNAPSACK problem = new KNAPSACK("({(1,10),(2,20),(3,30)},100,60)");
        string certificate = new KnapsackDP().solve(problem);
        Assert.True(new KnapsackVerifier().verify(problem, certificate));
    }

    [Theory]
    [InlineData("({(10,60),(20,100),(30,120)},50,220)")]
    [InlineData("({(2,3),(3,4),(4,5),(5,6)},5,7)")]
    [InlineData("({(1,1),(2,2),(3,3),(4,4),(5,5)},10,9)")]
    [InlineData("({(7,9),(3,4),(4,5),(8,11),(2,3)},10,14)")]
    public void KnapsackDP_Verifies_OnFeasibleInstances(string instance)
    {
        KNAPSACK problem = new KNAPSACK(instance);
        string certificate = new KnapsackDP().solve(problem);
        Assert.True(new KnapsackVerifier().verify(problem, certificate));
    }

    // -------------------------------------------------------------------------
    // Format declarations
    // -------------------------------------------------------------------------

    [Fact]
    public void KNAPSACK_Instance_Format_Described()
    {
        KNAPSACK problem = new KNAPSACK();
        Assert.NotNull(problem.instanceFormat);
        Assert.NotEmpty(problem.instanceFormat);
        Assert.Contains("(w1,v1)", problem.instanceFormat);
    }

    [Fact]
    public void KNAPSACK_Certificate_Format_Described()
    {
        KNAPSACK problem = new KNAPSACK();
        Assert.NotNull(problem.certificateFormat);
        Assert.NotEmpty(problem.certificateFormat);
        Assert.Contains("weight,value", problem.certificateFormat);
    }

    [Fact]
    public void KNAPSACK_Certificate_Format_Example_Is_Actually_Valid()
    {
        // The example quoted in certificateFormat must be a real, verifiable
        // certificate for defaultInstance — not just descriptive prose.
        KNAPSACK problem = new KNAPSACK();
        KnapsackVerifier verifier = new KnapsackVerifier();
        Assert.True(verifier.verify(problem, "{(20,100),(30,120)}"));
    }
}
