using Xunit;
using API.Problems.NPComplete.NPC_NODESET;
using API.Problems.NPComplete.NPC_NODESET.Verifiers;
using API.Problems.NPComplete.NPC_NODESET.Solvers;
using API.Interfaces;
using SPADE;

namespace redux_tests;
#pragma warning disable CS1591

public class NODESET_Tests {

    [Fact]
    public void NODESET_Default_Instantiation() {
        NODESET nodeset = new NODESET();
        UtilCollectionGraph graph = nodeset.graph;
        Assert.Equal(nodeset.instance, "(" + graph.ToString() + ",1)");
        Assert.Equal(1, nodeset.K);
        Assert.Equal(nodeset.defaultInstance, "(" + graph.ToString() + ",1)");
    }

    [Fact]
    public void NODESET_Custom_Instantiation() {
        NODESET nodeset = new NODESET("(({a,b,c,d},{(a,b),(b,a),(c,d),(d,c)}),2)");
        UtilCollectionGraph graph = nodeset.graph;
        Assert.Equal(nodeset.instance, "(" + graph.ToString() + ",2)");
        Assert.Equal(2, nodeset.K);
        Assert.Equal("(({a,b,c,d},{(a,b),(b,a),(c,d),(d,c)}),2)", nodeset.instance);
    }

    [Theory] // default instance has two directed cycles sharing nodes 2 and 3: 1->2->3->1 and 2->3->4->5->2
    [InlineData("(({1,2,3,4,5},{(1,2),(2,3),(3,1),(4,5),(5,2),(3,4)}),1)", "{2}", true)] // node 2 is common to both cycles
    [InlineData("(({1,2,3,4,5},{(1,2),(2,3),(3,1),(4,5),(5,2),(3,4)}),1)", "{3}", true)] // node 3 is also common to both cycles
    [InlineData("(({1,2,3,4,5},{(1,2),(2,3),(3,1),(4,5),(5,2),(3,4)}),1)", "{1}", false)] // leaves the 2-3-4-5 cycle intact
    [InlineData("(({1,2,3,4,5},{(1,2),(2,3),(3,1),(4,5),(5,2),(3,4)}),1)", "{4}", false)] // leaves the 1-2-3 cycle intact
    [InlineData("(({1,2,3,4,5},{(1,2),(2,3),(3,1),(4,5),(5,2),(3,4)}),1)", "{5}", false)] // leaves the 1-2-3 cycle intact
    [InlineData("(({1,2,3,4,5},{(1,2),(2,3),(3,1),(4,5),(5,2),(3,4)}),1)", "{}", false)] // no node removed, both cycles remain
    [InlineData("(({a,b,c,d},{(a,b),(b,a),(c,d),(d,c)}),2)", "{a,c}", true)] // breaks both disjoint 2-cycles
    [InlineData("(({a,b,c,d},{(a,b),(b,a),(c,d),(d,c)}),2)", "{a}", false)] // only breaks the a-b cycle
    [InlineData("(({a,b,c,d},{(a,b),(b,a),(c,d),(d,c)}),2)", "{a,b,c}", false)] // valid removal set, but |certificate|=3 exceeds K=2
    [InlineData("(({x,y},{(x,y)}),0)", "{}", true)] // already acyclic; empty certificate satisfies K=0
    public void NODESET_verifier(string instance, string certificate, bool expected) {
        NODESET nodeset = new NODESET(instance);
        NodeSetVerifier verifier = new NodeSetVerifier();
        bool result = verifier.verify(nodeset, certificate);
        Assert.Equal(expected, result);
    }

    [Theory] // Tests the solver produces a certificate the verifier accepts, of size <= K
    [InlineData("(({1,2,3,4,5},{(1,2),(2,3),(3,1),(4,5),(5,2),(3,4)}),1)")]
    [InlineData("(({a,b,c,d},{(a,b),(b,a),(c,d),(d,c)}),2)")]
    public void NODESET_solver(string instance) {
        NODESET nodeset = new NODESET(instance);
        NodeSetBruteForce solver = nodeset.defaultSolver;
        string solvedString = solver.solve(nodeset);
        Assert.True(nodeset.defaultVerifier.verify(nodeset, solvedString));
    }

    [Fact]
    public void NODESET_solver_already_acyclic_returns_empty_certificate() {
        NODESET nodeset = new NODESET("(({x,y},{(x,y)}),0)");
        NodeSetBruteForce solver = nodeset.defaultSolver;
        string solvedString = solver.solve(nodeset);
        Assert.Equal("{}", solvedString);
    }
}
