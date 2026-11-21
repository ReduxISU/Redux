using Xunit;
using API.Problems.NPComplete.NPC_WEIGHTEDCUT;
using API.Problems.NPComplete.NPC_WEIGHTEDCUT.Verifiers;
using API.Problems.NPComplete.NPC_WEIGHTEDCUT.Solvers;

namespace redux_tests;
#pragma warning disable CS1591

public class WEIGHTEDCUT_Tests {

    [Fact]
    public void WEIGHTEDCUT_Default_Instantiation() {
        WEIGHTEDCUT cut = new WEIGHTEDCUT();
        Assert.Equal(WEIGHTEDCUT._defaultInstance, cut.instance);
        Assert.Equal(5, cut.K);
        Assert.Equal(5, cut.nodes.Count);
        Assert.Equal(6, cut.edges.Count);
        Assert.Contains(("2", "1", 5), cut.edges);
        Assert.Contains(("3", "5", 1), cut.edges);
    }

    [Fact]
    public void WEIGHTEDCUT_Custom_Instantiation() {
        WEIGHTEDCUT cut = new WEIGHTEDCUT("(({A,B,C},{({A,B},3),({B,C},2)}),3)");
        Assert.Equal(3, cut.K);
        Assert.Equal(3, cut.nodes.Count);
        Assert.Equal(2, cut.edges.Count);
        Assert.Contains(("A", "B", 3), cut.edges);
    }

    [Theory] // verifier against the default graph: N={1..5}, E={(2,1,5),(1,3,4),(2,3,2),(3,5,1),(2,4,4),(4,5,2)}, K=5
    [InlineData("{({2,1},5)}", false)] // real edge with weight K, but removing it alone is not a cut
    [InlineData("{({2,4},4),({3,5},1)}", true)] // two real edges summing exactly to K
    [InlineData("{}", false)] // explicit empty-certificate rejection
    [InlineData("{({2,1},5),({3,5},1)}", false)] // real edges, but sum (6) misses K (5)
    [InlineData("{({2,1},3)}", false)] // edge exists but with the wrong declared weight -- never matches, counts as 0
    public void WEIGHTEDCUT_verifier(string certificate, bool expected) {
        WEIGHTEDCUT cut = new WEIGHTEDCUT();
        WeightedCutVerifier verifier = new WeightedCutVerifier();
        Assert.Equal(expected, verifier.verify(cut, certificate));
    }

    [Fact] // naming all three triangle edges as crossing edges is impossible for any 2-coloring
    public void WEIGHTEDCUT_verifier_rejects_inconsistent_cut() {
        WEIGHTEDCUT cut = new WEIGHTEDCUT("(({1,2,3},{({2,1},5),({1,3},4),({2,3},2)}),11)");
        WeightedCutVerifier verifier = new WeightedCutVerifier();
        Assert.False(verifier.verify(cut, "{({2,1},5),({1,3},4),({2,3},2)}"));
    }

    [Fact] // S={1,2,3} crosses exactly {3,5} and {2,4}
    public void WEIGHTEDCUT_verifier_accepts_valid_cut() {
        Assert.True(new WeightedCutVerifier().verify(new WEIGHTEDCUT(), "{({3,5},1),({2,4},4)}"));
    }

    [Fact] // edge exists in neither orientation
    public void WEIGHTEDCUT_verifier_rejects_nonexistent_edge() {
        WEIGHTEDCUT cut = new WEIGHTEDCUT("(({1,2,3},{({1,2},3),({2,3},2)}),3)");
        Assert.False(new WeightedCutVerifier().verify(cut, "{({1,3},3)}"));
    }

    [Fact] // a repeated edge must not count twice toward K
    public void WEIGHTEDCUT_verifier_rejects_repeated_edge() {
        WEIGHTEDCUT cut = new WEIGHTEDCUT("(({1,2,3},{({1,2},3),({2,3},2)}),6)");
        Assert.False(new WeightedCutVerifier().verify(cut, "{({1,2},3),({2,1},3)}"));
    }

    [Fact] // in a 4-cycle, one edge alone is not a crossing set but two opposite edges are
    public void WEIGHTEDCUT_verifier_rejects_partial_crossing_set() {
        WEIGHTEDCUT cut = new WEIGHTEDCUT("(({1,2,3,4},{({1,2},1),({2,3},1),({3,4},1),({4,1},1)}),2)");
        WEIGHTEDCUT one = new WEIGHTEDCUT("(({1,2,3,4},{({1,2},1),({2,3},1),({3,4},1),({4,1},1)}),1)");
        Assert.False(new WeightedCutVerifier().verify(one, "{({1,2},1)}"));
        Assert.True(new WeightedCutVerifier().verify(cut, "{({2,3},1),({4,1},1)}"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("{({2,1},x)}")]
    [InlineData("{({2,1},99999999999)}")]
    [InlineData("{({2,1})}")]
    [InlineData("{({2},5)}")]
    [InlineData("{{2,1},5}")]
    public void WEIGHTEDCUT_verifier_malformed_certificate_returns_false(string certificate) {
        Assert.False(new WeightedCutVerifier().verify(new WEIGHTEDCUT(), certificate));
    }

    [Fact] // solver on a small graph where isolating one node is the only viable cut of weight K
    public void WEIGHTEDCUT_solver_simple() {
        WEIGHTEDCUT cut = new WEIGHTEDCUT("(({A,B,C},{({A,B},3),({B,C},2)}),3)");
        WeightedCutBruteForce solver = cut.defaultSolver;
        string certificate = solver.solve(cut);
        Assert.Equal("{({A,B},3)}", certificate);
    }

    [Fact] // solver on the default instance -- checked via re-verification since the returned partition's edge order isn't guaranteed
    public void WEIGHTEDCUT_solver_default() {
        WEIGHTEDCUT cut = new WEIGHTEDCUT();
        WeightedCutBruteForce solver = cut.defaultSolver;
        string certificate = solver.solve(cut);
        Assert.NotEqual("{}", certificate);
        Assert.True(cut.defaultVerifier.verify(cut, certificate));
    }
}
