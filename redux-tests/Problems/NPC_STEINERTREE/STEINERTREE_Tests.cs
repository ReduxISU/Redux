using Xunit;
using API.Problems.NPComplete.NPC_STEINERTREE;
using API.Problems.NPComplete.NPC_STEINERTREE.Verifiers;
using API.Problems.NPComplete.NPC_STEINERTREE.Solvers;

namespace redux_tests;
#pragma warning disable CS1591

public class STEINERTREE_Tests {

    [Fact]
    public void STEINERTREE_Default_Instantiation() {
        STEINERTREE steiner = new STEINERTREE();
        Assert.Equal(STEINERTREE._defaultInstance, steiner.instance);
        Assert.Equal(6, steiner.K);
        Assert.Equal(8, steiner.nodes.Count);
        Assert.Equal(10, steiner.edges.Count);
        Assert.Equal(3, steiner.terminals.Count);
        Assert.Contains("5", steiner.terminals);
        Assert.Contains("2", steiner.terminals);
        Assert.Contains("8", steiner.terminals);
    }

    [Fact]
    public void STEINERTREE_Custom_Instantiation() {
        STEINERTREE steiner = new STEINERTREE("(({1,2,3},{{1,2},{2,3}}),{1,3},2)");
        Assert.Equal(2, steiner.K);
        Assert.Equal(3, steiner.nodes.Count);
        Assert.Equal(2, steiner.edges.Count);
        Assert.Equal(2, steiner.terminals.Count);
        Assert.Contains("1", steiner.terminals);
        Assert.Contains("3", steiner.terminals);
    }

    [Theory] // verifier against the default graph: N={1..8}, E={{2,1},{1,3},{2,3},{3,5},{2,4},{4,5},{6,7},{7,8},{6,8},{6,1}}, R={5,2,8}
    [InlineData("{{3,5},{2,3},{2,1},{6,1},{6,8}}", true)] // connects terminals 5,2,8 through the 3-2-1-6-8 bridge
    [InlineData("{{3,5},{2,3},{2,1},{6,1}}", false)] // drops {6,8} -- terminal 8 never appears among the edges
    [InlineData("{{3,5},{2,3},{6,8}}", false)] // covers all 3 terminals but forms two disconnected components
    public void STEINERTREE_verifier(string certificate, bool expected) {
        STEINERTREE steiner = new STEINERTREE();
        SteinerTreeVerifier verifier = new SteinerTreeVerifier();
        Assert.Equal(expected, verifier.verify(steiner, certificate));
    }

    // BUG: SteinerTreeVerifier.verify() never checks that the certificate's edges are a subset
    // of problem.edges -- it just parses whatever edge pairs are handed to it and checks (a) the
    // resulting graph is connected and (b) every terminal appears among the edge endpoints. A
    // certificate built entirely from edges that don't exist in G is accepted regardless.
    [Fact(Skip = "BUG: SteinerTreeVerifier accepts fabricated edges that don't exist in problem.edges -- see comment")]
    public void STEINERTREE_verifier_rejects_edges_not_in_graph() {
        STEINERTREE steiner = new STEINERTREE(); // default graph has no edge {5,2} or {2,8}
        SteinerTreeVerifier verifier = new SteinerTreeVerifier();
        Assert.False(verifier.verify(steiner, "{{5,2},{2,8}}"));
    }

    // BUG: SteinerTreeVerifier.verify() never reads problem.K -- a certificate using more edges
    // (i.e. more weight, since every edge here is unit-weight) than K permits is still accepted
    // as long as it stays connected and covers the terminals.
    [Fact(Skip = "BUG: SteinerTreeVerifier never checks certificate weight/edge-count against problem.K -- see comment")]
    public void STEINERTREE_verifier_rejects_certificate_over_K() {
        STEINERTREE steiner = new STEINERTREE(); // K = 6
        SteinerTreeVerifier verifier = new SteinerTreeVerifier();
        // 7 real edges, all connected, covers every terminal -- but 7 > K (6)
        Assert.False(verifier.verify(steiner, "{{3,5},{2,3},{2,1},{6,1},{6,8},{2,4},{4,5}}"));
    }

    [Fact] // solver must find the minimum Steiner tree -- constructed so the correct answer is the very first edge combination tried
    public void STEINERTREE_solver_finds_minimal_tree() {
        STEINERTREE steiner = new STEINERTREE("(({A,B,C},{{A,B},{B,C}}),{A,B},1)");
        SteinerTreeBruteForce solver = steiner.defaultSolver;
        string certificate = solver.solve(steiner);
        Assert.Equal("{{A,B}}", certificate);
        Assert.True(steiner.defaultVerifier.verify(steiner, certificate));
    }
}
