using Xunit;
using API.Problems.NPComplete.NPC_PARTITION;
using API.Problems.NPComplete.NPC_PARTITION.Solvers;
using API.Problems.NPComplete.NPC_PARTITION.Verifiers;

namespace redux_tests;
#pragma warning disable CS1591

public class PARTITION_Tests {
    // -------------------------------------------------------------------------
    // PartitionBruteForce
    // -------------------------------------------------------------------------

    [Fact]
    public void PARTITION_BruteForce_Two_Distinct_Elements_Have_No_Solution() {
        // PARTITION's instance grammar parses S with "is set" semantics (duplicates
        // collapse), so two distinct positive integers can never balance into two
        // equal-sum singleton groups. The solver must visit all 2^2 = 4 binary
        // assignments -- including nextBinary's double-carry rollover from [1,1] back to
        // [0,0] -- before exhausting the search and returning "{}".
        PARTITION problem = new PARTITION("{3,5}");
        PartitionBruteForce solver = new PartitionBruteForce();

        string solution = solver.solve(problem);

        Assert.Equal("{}", solution);
    }

    [Fact]
    public void PARTITION_BruteForce_Three_Elements_Finds_Balanced_Split() {
        // {1,2,3}: sum = 6, so {1,2} vs {3} (both sum to 3) is a valid split. The solver
        // must advance past one failing candidate ([0,1,0] -> group sums 2 vs 4) before
        // reaching the valid one ([1,1,0] -> group sums 3 vs 3).
        PARTITION problem = new PARTITION("{1,2,3}");
        PartitionBruteForce solver = new PartitionBruteForce();

        string solution = solver.solve(problem);

        Assert.Equal("{(1,2),(3)}", solution);
        Assert.True(new PartitionVerifier().verify(problem, solution));
    }

    [Fact]
    public void PARTITION_BruteForce_Single_Element_Has_No_Solution() {
        // A lone positive element can never be split into two equal-sum halves; the
        // solver must exhaust both candidates ([1] and, after nextBinary's carry
        // rollover, [0]) and return "{}".
        PARTITION problem = new PARTITION("{5}");
        PartitionBruteForce solver = new PartitionBruteForce();

        string solution = solver.solve(problem);

        Assert.Equal("{}", solution);
    }

    [Fact]
    public void PARTITION_BruteForce_Odd_Sum_Has_No_Solution() {
        // {1,2,4}: sum = 7 is odd, so no subset can match the complementary subset's sum.
        // The solver exhausts all 2^3 = 8 binary assignments and returns "{}".
        PARTITION problem = new PARTITION("{1,2,4}");
        PartitionBruteForce solver = new PartitionBruteForce();

        string solution = solver.solve(problem);

        Assert.Equal("{}", solution);
    }

    [Fact]
    public void PARTITION_BruteForce_Default_Instance_Output_Passes_Verifier() {
        PARTITION problem = new PARTITION();
        PartitionBruteForce solver = new PartitionBruteForce();
        PartitionVerifier verifier = new PartitionVerifier();

        string solution = solver.solve(problem);

        Assert.NotEqual("{}", solution);
        Assert.True(verifier.verify(problem, solution), $"Solver output failed verifier for default instance: {solution}");
    }

    [Fact]
    public void PARTITION_BruteForce_Four_Elements_Finds_A_Balanced_Split() {
        // {1,2,3,4}: sum = 10, and several balanced splits exist (e.g. {1,4}/{2,3} or
        // {3,4}... no -- {1,2,3,4} needing subset-sum 5: {1,4} or {2,3}). This exercises
        // nextBinary across a larger (2^4 = 16) search space than the smaller cases above.
        PARTITION problem = new PARTITION("{1,2,3,4}");
        PartitionBruteForce solver = new PartitionBruteForce();
        PartitionVerifier verifier = new PartitionVerifier();

        string solution = solver.solve(problem);

        Assert.NotEqual("{}", solution);
        Assert.True(verifier.verify(problem, solution), $"Solver output failed verifier for: {solution}");
    }
}
