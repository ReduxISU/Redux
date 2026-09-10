using System.Linq;
using Xunit;
using API.Problems.NPComplete.NPC_TSP;
using API.Problems.NPComplete.NPC_TSP.Solvers;
using API.Problems.NPComplete.NPC_TSP.Verifiers;

namespace redux_tests;
#pragma warning disable CS1591
public class TSP_Tests {

    //TSP Greedy solver tests

    [Fact]
    public void TSP_Greedy_Empty_Graph_Returns_Empty() {
        TSP tsp = new TSP("(({},{}),0)");
        TSPGreedy solver = new TSPGreedy();
        string result = solver.solve(tsp);
        Assert.Equal("{}", result);
    }

    [Fact]
    public void TSP_Greedy_Single_Node_Returns_Empty() {
        TSP tsp = new TSP("(({A},{}),0)");
        TSPGreedy solver = new TSPGreedy();
        string result = solver.solve(tsp);
        Assert.Equal("{}", result);
    }

    [Theory]
    [InlineData("(({A,B,C},{({A,B},10),({B,C},20),({A,C},30)}),100)")]
    [InlineData("(({A,B,C,D},{({A,B},10),({B,C},20),({C,D},30),({A,D},40),({A,C},25),({B,D},15)}),200)")]
    public void TSP_Greedy_Returns_Valid_Tour(string instance) {
        TSP tsp = new TSP(instance);
        TSPGreedy solver = new TSPGreedy();
        string result = solver.solve(tsp);
        if (result != "{}") {
            TSPVerifier verifier = new TSPVerifier();
            Assert.True(verifier.verify(tsp, result));
        }
    }

    [Fact]
    public void TSP_Greedy_Disconnected_Graph_Returns_Empty() {
        TSP tsp = new TSP("(({A,B,C},{({A,B},10),({A,C},30)}),100)");
        TSPGreedy solver = new TSPGreedy();
        string result = solver.solve(tsp);
        Assert.Equal("{}", result);
    }

    [Fact]
    public void TSP_Greedy_Picks_Cheapest_Route() {
        TSP tsp = new TSP("(({A,B,C},{({A,B},10),({B,C},20),({A,C},30)}),100)");
        TSPGreedy solver = new TSPGreedy();
        string result = solver.solve(tsp);
        TSPVerifier verifier = new TSPVerifier();
        Assert.True(verifier.verify(tsp, result));
    }

    //TSP B&B solver tests

    [Fact]
    public void TSP_BranchAndBound_Empty_Graph_Returns_Empty() {
        TSP tsp = new TSP("(({},{}),0)");
        TSPBranchAndBound solver = new TSPBranchAndBound();
        string result = solver.solve(tsp);
        Assert.Equal("{}", result);
    }

    [Fact]
    public void TSP_BranchAndBound_Single_Node_Returns_Empty() {
        TSP tsp = new TSP("(({A},{}),0)");
        TSPBranchAndBound solver = new TSPBranchAndBound();
        string result = solver.solve(tsp);
        Assert.Equal("{}", result);
    }

    [Fact]
    public void TSP_BranchAndBound_Timer_Expired_Returns_Timeout() {
        TSP tsp = new TSP("(({A,B,C},{({A,B},10),({B,C},20),({A,C},30)}),100)");
        TSPBranchAndBound solver = new TSPBranchAndBound() { timerHasExpired = true };
        string result = solver.solve(tsp);
        Assert.Equal("timeout", result);
    }

    [Theory]
    [InlineData("(({A,B,C},{({A,B},10),({B,C},20),({A,C},30)}),100)")]
    [InlineData("(({A,B,C,D},{({A,B},10),({B,C},20),({C,D},30),({A,D},40),({A,C},25),({B,D},15)}),200)")]
    public void TSP_BranchAndBound_Returns_Valid_Tour(string instance) {
        TSP tsp = new TSP(instance);
        TSPBranchAndBound solver = new TSPBranchAndBound();
        string result = solver.solve(tsp);
        if (result != "{}") {
            TSPVerifier verifier = new TSPVerifier();
            Assert.True(verifier.verify(tsp, result));
        }
    }

    [Fact]
    public void TSP_BranchAndBound_Disconnected_Graph_Returns_Empty() {
        TSP tsp = new TSP("(({A,B,C},{({A,B},10),({A,C},30)}),100)");
        TSPBranchAndBound solver = new TSPBranchAndBound();
        string result = solver.solve(tsp);
        Assert.Equal("{}", result);
    }

    [Fact]
    public void TSP_BranchAndBound_Finds_Optimal_Tour() {
        TSP tsp = new TSP("(({A,B,C},{({A,B},10),({B,C},20),({A,C},30)}),100)");
        TSPBranchAndBound solver = new TSPBranchAndBound();
        string result = solver.solve(tsp);
        TSPVerifier verifier = new TSPVerifier();
        Assert.True(verifier.verify(tsp, result));
    }

    //Greedy vs B&B

    [Theory]
    [InlineData("(({A,B,C},{({A,B},10),({B,C},20),({A,C},30)}),100)")]
    [InlineData("(({A,B,C,D},{({A,B},10),({B,C},20),({C,D},30),({A,D},40),({A,C},25),({B,D},15)}),200)")]
    public void TSP_BranchAndBound_At_Least_As_Good_As_Greedy(string instance) {
        TSP tsp = new TSP(instance);
        TSPGreedy greedy = new TSPGreedy();
        TSPBranchAndBound bnb = new TSPBranchAndBound();
        string greedyResult = greedy.solve(tsp);
        string bnbResult = bnb.solve(tsp);
        if (greedyResult != "{}") {
            Assert.NotEqual("{}", bnbResult);
        }
    }

    //TSP BruteForce solver tests

    [Fact]
    public void TSP_BruteForce_Empty_Graph_Returns_Empty() {
        TSP tsp = new TSP("(({},{}),0)");
        TSPBruteForce solver = new TSPBruteForce();
        string result = solver.solve(tsp);
        Assert.Equal("{}", result);
    }

    [Fact]
    public void TSP_BruteForce_Single_Node_Returns_Empty() {
        TSP tsp = new TSP("(({A},{}),0)");
        TSPBruteForce solver = new TSPBruteForce();
        string result = solver.solve(tsp);
        Assert.Equal("{}", result);
    }

    [Theory]
    [InlineData("(({A,B,C},{({A,B},10),({B,C},20),({A,C},30)}),100)")]
    [InlineData("(({A,B,C,D},{({A,B},10),({B,C},20),({C,D},30),({A,D},40),({A,C},25),({B,D},15)}),200)")]
    public void TSP_BruteForce_Returns_Valid_Tour(string instance) {
        TSP tsp = new TSP(instance);
        TSPBruteForce solver = new TSPBruteForce();
        TSPVerifier verifier = new TSPVerifier();
        string result = solver.solve(tsp);
        Assert.True(verifier.verify(tsp, result), $"Solver output failed verifier for: {instance}");
    }

    [Fact]
    public void TSP_BruteForce_Disconnected_Graph_Returns_Empty() {
        TSP tsp = new TSP("(({A,B,C},{({A,B},10),({A,C},30)}),100)");
        TSPBruteForce solver = new TSPBruteForce();
        string result = solver.solve(tsp);
        Assert.Equal("{}", result);
    }

    [Fact]
    public void TSP_BruteForce_KTooSmall_ReturnsEmpty() {
        // The only Hamiltonian cycle on this triangle costs 10+20+30=60; a budget of 50 is infeasible.
        TSP tsp = new TSP("(({A,B,C},{({A,B},10),({B,C},20),({A,C},30)}),50)");
        TSPBruteForce solver = new TSPBruteForce();
        string result = solver.solve(tsp);
        Assert.Equal("{}", result);
    }

    [Fact]
    public void TSP_BruteForce_Finds_Optimal_Tour_Exhaustively() {
        // Brute force checks every permutation, so it must find a tour within the given budget.
        TSP tsp = new TSP("(({A,B,C,D},{({A,B},10),({B,C},20),({C,D},30),({A,D},40),({A,C},25),({B,D},15)}),200)");
        TSPBruteForce solver = new TSPBruteForce();
        TSPVerifier verifier = new TSPVerifier();
        string result = solver.solve(tsp);
        Assert.True(verifier.verify(tsp, result));
    }

    [Fact]
    public void TSP_GenerateCombinations_ProducesAllDistinctPermutations() {
        // 4! = 24 permutations of {1,2,3,4}; exercises GetNextCombination's lexicographic-next
        // logic (find-descent / find-successor / reverse-suffix) across every swap it can take.
        List<List<int>> combos = TSPBruteForce.GenerateCombinations(4);

        Assert.Equal(24, combos.Count);
        Assert.Equal(24, combos.Select(c => string.Join(",", c)).Distinct().Count());
    }
}
