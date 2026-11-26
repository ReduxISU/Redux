using Xunit;
using API.Interfaces;
using API.Problems.NPComplete.NPC_HITTINGSET;
using API.Problems.NPComplete.NPC_KNAPSACK;
using API.Problems.NPComplete.NPC_MAXCUT;
using API.Problems.NPComplete.NPC_SUBSETSUM;

namespace redux_tests;
#pragma warning disable CS1591

// #1028: these brute forces numbered their subsets with an int bit mask, which goes wrong past 30 items: 1 << 31 is
// negative and 1 << 32 is 1, so Subset Sum and Max Cut never searched, and Knapsack and Hitting Set built their
// candidates wrong. Each now uses a long mask and refuses more than 62 items with the exact-search-limit error.
public class BruteForcePastThirtyItems_Tests {

    private static string Numbers(int count) => string.Join(",", Enumerable.Range(1, count));

    [Fact] // 31 integers, target 1: the first subset tried, {1}, already sums to it
    public void SubsetSum_FindsASubset_On31Integers() {
        var problem = new SUBSETSUM($"({{{Numbers(31)}}},1)");

        string answer = problem.defaultSolver.solve(problem);

        Assert.Equal("{1}", answer);
    }

    [Fact] // 31 items (weight i, value 1; distinct, since the items are a set), capacity 1, value 1: item (1,1) alone is enough
    public void Knapsack_FindsAPacking_On31Items() {
        string items = string.Join(",", Enumerable.Range(1, 31).Select(i => $"({i},1)"));
        var problem = new KNAPSACK($"({{{items}}},1,1)");

        Assert.Equal(31, problem.items.Count());
        string answer = problem.defaultSolver.solve(problem);

        Assert.True(problem.defaultVerifier.verify(problem, answer));
    }

    [Fact] // a universe of 31 elements whose subsets all hold 1: {1} hits every one
    public void HittingSet_FindsASet_On31Elements() {
        var problem = new HITTINGSET($"({{{Numbers(31)}}},{{{{1,2}},{{1,3}}}})");

        string answer = problem.defaultSolver.solve(problem);

        Assert.True(problem.defaultVerifier.verify(problem, answer));
    }

    // 32 nodes: 2^32 sides are too many to wait for, so the timer is already up and the solver must say so;
    // with the int mask it never entered its loop and answered "{}" at once
    [Fact]
    public void MaxCut_Searches_On32Nodes() {
        string edges = string.Join(",", Enumerable.Range(1, 31).Select(i => $"({{{i},{i + 1}}},1)"));
        var problem = new MAXCUT($"({{{Numbers(32)}}},{{{edges}}})");
        var solver = problem.defaultSolver;
        solver.timerHasExpired = true;

        Assert.Equal(SolveRun.TimedOutAnswer, solver.solve(problem));
    }

    [Fact] // past 62 items a long mask goes wrong too, so the solvers refuse with the exact-search-limit error
    public void BruteForces_Refuse_MoreThan62Items() {
        var subsetSum = new SUBSETSUM($"({{{Numbers(63)}}},1)");
        Assert.Throws<ExactSearchLimitExceededException>(() => subsetSum.defaultSolver.solve(subsetSum));

        var knapsack = new KNAPSACK($"({{{string.Join(",", Enumerable.Range(1, 63).Select(i => $"({i},1)"))}}},1,1)");
        Assert.Throws<ExactSearchLimitExceededException>(() => knapsack.defaultSolver.solve(knapsack));

        var hittingSet = new HITTINGSET($"({{{Numbers(63)}}},{{{{1,2}}}})");
        Assert.Throws<ExactSearchLimitExceededException>(() => hittingSet.defaultSolver.solve(hittingSet));

        string edges = string.Join(",", Enumerable.Range(1, 62).Select(i => $"({{{i},{i + 1}}},1)"));
        var maxCut = new MAXCUT($"({{{Numbers(63)}}},{{{edges}}})");
        Assert.Throws<ExactSearchLimitExceededException>(() => maxCut.defaultSolver.solve(maxCut));
    }
}
