using Xunit;
using API.Problems.NPComplete.NPC_DOMINATINGSET;
using API.Problems.NPComplete.NPC_DOMINATINGSET.Verifiers;
using API.Problems.NPComplete.NPC_DOMINATINGSET.Solvers;
using API.Interfaces;
using SPADE;

namespace redux_tests;
#pragma warning disable CS1591

public class DOMINATINGSET_Tests {

    [Fact]
    public void DOMINATINGSET_Default_Instantiation() {
        DOMINATINGSET dominatingset = new DOMINATINGSET();
        UtilCollectionGraph graph = dominatingset.graph;
        Assert.Equal(dominatingset.instance, "(" + graph.ToString() + ",2)");
        Assert.Equal(2, dominatingset.K);
        Assert.Equal(dominatingset.defaultInstance, "(" + graph.ToString() + ",2)");
    }

    [Fact]
    public void DOMINATINGSET_Custom_Instantiation() {
        DOMINATINGSET dominatingset = new DOMINATINGSET("(({1,2,3,4},{{1,2},{2,3},{3,4},{4,1}}),1)");
        UtilCollectionGraph graph = dominatingset.graph;
        Assert.Equal(dominatingset.instance, "(" + graph.ToString() + ",1)");
        Assert.Equal(1, dominatingset.K);
        Assert.Equal("(({1,2,3,4},{{1,2},{2,3},{3,4},{4,1}}),1)", dominatingset.instance);
    }

    [Theory] // Tests dominating set verifier with a few certificates against the default instance
    // Default instance: (({0,1,2,3,4},{{1,0},{0,3},{1,2},{2,4},{1,3},{3,4},{4,1}}),2)
    // adjacency: 0:{1,3} 1:{0,2,3,4} 2:{1,4} 3:{0,1,4} 4:{1,2,3}
    [InlineData("(({0,1,2,3,4},{{1,0},{0,3},{1,2},{2,4},{1,3},{3,4},{4,1}}),2)", "{1,3}", true)] // 1 dominates 0,2,4; 3 dominates 0,4 -- every node covered, |D|=2<=K
    [InlineData("(({0,1,2,3,4},{{1,0},{0,3},{1,2},{2,4},{1,3},{3,4},{4,1}}),2)", "{0}", false)] // 0 only dominates 1,3 -- node 2 is undominated
    [InlineData("(({0,1,2,3,4},{{1,0},{0,3},{1,2},{2,4},{1,3},{3,4},{4,1}}),2)", "{0,1,2}", false)] // dominates every node, but |D|=3 exceeds K=2
    [InlineData("(({0,1,2,3,4},{{1,0},{0,3},{1,2},{2,4},{1,3},{3,4},{4,1}}),2)", "{9}", false)] // vertex not present in the graph
    public void DOMINATINGSET_verifier(string instance, string certificate, bool expected) {
        DOMINATINGSET dominatingset = new DOMINATINGSET(instance);
        DominatingSetVerifier verifier = new DominatingSetVerifier();
        bool result = verifier.verify(dominatingset, certificate);
        Assert.Equal(expected, result);
    }

    [Theory] // Tests the solver produces a certificate the verifier accepts, of size <= K
    [InlineData("(({0,1,2,3,4},{{1,0},{0,3},{1,2},{2,4},{1,3},{3,4},{4,1}}),2)")]
    // 4-cycle: domination number is 2 (every vertex's closed neighborhood misses its opposite vertex),
    // so K=2, not K=1, is the smallest feasible target here.
    [InlineData("(({1,2,3,4},{{1,2},{2,3},{3,4},{4,1}}),2)")]
    public void DOMINATINGSET_solver(string instance) {
        DOMINATINGSET dominatingset = new DOMINATINGSET(instance);
        DominatingSetSolver solver = dominatingset.defaultSolver;
        string solvedString = solver.solve(dominatingset);
        Assert.True(dominatingset.defaultVerifier.verify(dominatingset, solvedString));
    }
}
