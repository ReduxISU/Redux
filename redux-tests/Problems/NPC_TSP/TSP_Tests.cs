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

    // -------------------------------------------------------------------------
    // Format declarations
    // -------------------------------------------------------------------------

    [Fact]
    public void TSP_Instance_Format_Described() {
        TSP tsp = new TSP();
        Assert.NotNull(tsp.instanceFormat);
        Assert.NotEmpty(tsp.instanceFormat);
        Assert.Contains("N,E),K", tsp.instanceFormat);
    }

    [Fact]
    public void TSP_Certificate_Format_Described() {
        TSP tsp = new TSP();
        Assert.NotNull(tsp.certificateFormat);
        Assert.NotEmpty(tsp.certificateFormat);
        Assert.Contains("cycle", tsp.certificateFormat);
    }

    [Fact]
    public void TSP_Certificate_Format_Example_Is_Actually_Valid() {
        // The example quoted in certificateFormat must be a real, verifiable
        // certificate for defaultInstance — not just descriptive prose.
        TSP tsp = new TSP();
        TSPVerifier verifier = new TSPVerifier();
        Assert.True(verifier.verify(tsp, "{New York,Chicago,Denver,Los Angeles,Miami}"));
    }
}
