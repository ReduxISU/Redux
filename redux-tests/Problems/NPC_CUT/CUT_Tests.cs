using Xunit;
using API.Problems.NPComplete.NPC_CUT;
using API.Problems.NPComplete.NPC_CUT.Verifiers;
using API.Problems.NPComplete.NPC_CUT.Solvers;

namespace redux_tests;
#pragma warning disable CS1591

public class CUT_Tests {

    [Fact]
    public void CUT_Default_Instantiation() {
        CUT cut = new CUT();
        Assert.Equal("(({1,2,3,4,5},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5}}),5)", cut.instance);
        Assert.Equal(5, cut.K);
        Assert.Equal(cut.defaultInstance, cut.instance);
    }

    [Fact]
    public void CUT_Custom_Instantiation() {
        CUT cut = new CUT("(({a,b,c,d},{{a,b},{b,c},{c,d},{d,a}}),2)");
        Assert.Equal("(({a,b,c,d},{{a,b},{b,c},{c,d},{d,a}}),2)", cut.instance);
        Assert.Equal(2, cut.K);
        Assert.Equal(4, cut.nodes.Count);
        Assert.Equal(4, cut.edges.Count);
    }

    [Theory] // Verifier: certificate is the set of crossing edges for a bipartition; must be exactly K real, distinct edges
    [InlineData("(({a,b,c,d},{{a,b},{b,c},{c,d},{d,a}}),2)", "{{b,c},{d,a}}", true)]   // valid cut of size 2 (S={a,b} vs T={c,d})
    [InlineData("(({a,b,c,d},{{a,b},{b,c},{c,d},{d,a}}),3)", "{{b,c},{d,a}}", false)]  // count doesn't match K
    [InlineData("(({a,b,c,d},{{a,b},{b,c},{c,d},{d,a}}),2)", "{{b,c},{b,c}}", false)]  // duplicate edge
    [InlineData("(({a,b,c,d},{{a,b},{b,c},{c,d},{d,a}}),2)", "{{b,c},{c,b}}", false)]  // reversed duplicate of same edge
    [InlineData("(({a,b,c,d},{{a,b},{b,c},{c,d},{d,a}}),1)", "{{a,c}}", false)]        // a-c is not an edge in the graph
    public void CUT_verifier(string instance, string certificate, bool expected) {
        CUT cut = new CUT(instance);
        CutVerifier verifier = new CutVerifier();
        bool result = verifier.verify(cut, certificate);
        Assert.Equal(expected, result);
    }

    [Fact] // a triangle is not bipartite, so one of its edges can never be a whole cut
    public void CUT_verifier_rejects_edge_set_with_no_valid_partition() {
        CUT triangle = new CUT("(({x,y,z},{{x,y},{y,z},{z,x}}),1)");
        CutVerifier verifier = new CutVerifier();
        Assert.False(verifier.verify(triangle, "{{x,y}}"));
    }

    [Fact] // all 3 triangle edges can't all cross one partition
    public void CUT_verifier_rejects_triangle_edges() {
        CUT triangle = new CUT("(({x,y,z},{{x,y},{y,z},{z,x}}),3)");
        Assert.False(new CutVerifier().verify(triangle, "{{x,y},{y,z},{z,x}}"));
    }

    [Fact] // the 2 edges at a triangle vertex are the cut of S={x}
    public void CUT_verifier_accepts_valid_triangle_cut() {
        CUT triangle = new CUT("(({x,y,z},{{x,y},{y,z},{z,x}}),2)");
        Assert.True(new CutVerifier().verify(triangle, "{{x,y},{z,x}}"));
    }

    [Fact] // on a path a-b-c, the bridge {a,b} is the cut of S={a}
    public void CUT_verifier_accepts_single_bridge() {
        CUT path = new CUT("(({a,b,c},{{a,b},{b,c}}),1)");
        Assert.True(new CutVerifier().verify(path, "{{a,b}}"));
    }

    [Fact] // in a 4-cycle, one edge alone would cross while the other three stay uncut: a cycle crosses an even number of times
    public void CUT_verifier_rejects_subset_of_crossing_edges() {
        CUT square = new CUT("(({a,b,c,d},{{a,b},{b,c},{c,d},{d,a}}),1)");
        Assert.False(new CutVerifier().verify(square, "{{a,b}}"));
    }

    [Theory] // a cut splits the nodes into two non-empty sides, so {} is a cut only of a graph in separate pieces (#770)
    [InlineData("(({a,b,c},{{a,b},{b,c}}),0)", false)]         // connected path: every split cuts an edge
    [InlineData("(({x,y,z},{{x,y},{y,z},{z,x}}),0)", false)]   // connected triangle
    [InlineData("(({a},{}),0)", false)]                        // one node can't be split at all
    [InlineData("(({a,b,c,d},{{a,b},{c,d}}),0)", true)]        // two pieces: {a,b} against {c,d}
    [InlineData("(({a,b,c},{{a,b}}),0)", true)]                // an isolated node is a piece of its own
    [InlineData("(({a,b,c,d},{{a,b},{c,d}}),1)", false)]       // {} still has size 0, not K
    public void CUT_verifier_empty_certificate_needs_a_split_with_no_crossing_edge(string instance, bool expected) {
        Assert.Equal(expected, new CutVerifier().verify(new CUT(instance), "{}"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("{{a}}")]
    [InlineData("{{a,b,c}}")]
    [InlineData("{a,b}")]
    public void CUT_verifier_malformed_certificate_returns_false(string certificate) {
        CUT cut = new CUT("(({a,b,c},{{a,b},{b,c}}),1)");
        Assert.False(new CutVerifier().verify(cut, certificate));
    }

    [Theory] // Solver: any certificate it emits must itself satisfy the verifier
    [InlineData("(({1,2,3,4,5},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5}}),5)")]
    [InlineData("(({a,b,c,d},{{a,b},{b,c},{c,d},{d,a}}),2)")]
    public void CUT_solver_produces_verifiable_certificate(string instance) {
        CUT cut = new CUT(instance);
        CutBruteForce solver = cut.defaultSolver;
        string certificate = solver.solve(cut);
        Assert.True(cut.defaultVerifier.verify(cut, certificate));
    }

    [Fact] // Solver bails out immediately when K exceeds the number of edges available
    public void CUT_solver_returns_empty_when_K_exceeds_edge_count() {
        CUT cut = new CUT("(({1,2},{{1,2}}),5)");
        CutBruteForce solver = cut.defaultSolver;
        Assert.Equal("{}", solver.solve(cut));
    }

    // #751: the solver only tried S sides of 1..K nodes, but K counts the edges that cross, not the nodes on a side.
    // Two triangles joined by the edge c-d: the only cut of one edge puts a triangle on each side, and K = 1 meant
    // sides of three nodes were never tried.
    [Theory]
    [InlineData("(({a,b,c,d,e,f},{{a,b},{b,c},{a,c},{d,e},{e,f},{d,f},{c,d}}),1)", "{{c,d}}")]
    [InlineData("(({a,b,c,d,e,f,g,h},{{a,b},{a,c},{a,d},{b,c},{b,d},{c,d},{e,f},{e,g},{e,h},{f,g},{f,h},{g,h},{a,e},{b,f}}),2)", "{{a,e},{b,f}}")]
    public void CUT_solver_tries_sides_larger_than_K(string instance, string expected) {
        CUT cut = new CUT(instance);
        string certificate = cut.defaultSolver.solve(cut);
        Assert.Equal(expected, certificate);
        Assert.True(cut.defaultVerifier.verify(cut, certificate));
    }
}
