using Xunit;
using API.Problems.NPComplete.NPC_EXACTCOVER;
using API.Problems.NPComplete.NPC_GRAPHCOLORING;
using API.Problems.NPComplete.NPC_GRAPHCOLORING.ReduceTo.NPC_EXACTCOVER;
using API.Problems.NPComplete.NPC_SETCOVER;
using API.Problems.NPComplete.NPC_VERTEXCOVER;
using API.Problems.NPComplete.NPC_VERTEXCOVER.ReduceTo.NPC_SETCOVER;

namespace redux_tests;

#pragma warning disable CS1591

// Vertex Cover to Set Cover and Graph Coloring to Exact Cover name their elements by joining node names with "_" (#824). A
// name that holds "_" used to be joined as it is, so two different pieces could share an element: the edges {a_b,c} and
// {a,b_c} were both "a_b_c", and the node a_b was the edge {a,b}. Such names are now escaped; other names are unchanged.
public class UnderscoreElements_Tests {

    // Every way to pick sets, smallest first, as index lists (small instances only).
    private static IEnumerable<List<int>> Picks(int sets) {
        for (int mask = 0; mask < 1 << sets; mask++)
            yield return Enumerable.Range(0, sets).Where(i => (mask >> i & 1) == 1).ToList();
    }

    private static bool HasSetCover(SETCOVER p) =>
        Picks(p.subsets.Count).Any(pick => pick.Count <= p.K && p.universal.All(u => pick.Any(i => p.subsets[i].Contains(u))));

    private static bool HasExactCover(EXACTCOVER p) =>
        Picks(p.S.Count).Any(pick => p.X.All(x => pick.Count(i => p.S[i].Contains(x)) == 1));

    private static bool HasVertexCover(VERTEXCOVER p) =>
        Picks(p.nodes.Count).Any(pick => pick.Count <= p.K
            && p.edges.All(e => pick.Any(i => p.nodes[i] == e.Key || p.nodes[i] == e.Value)));

    private static bool HasColoring(GRAPHCOLORING p) {
        var color = new int[p.nodes.Count];
        while (true) {
            if (p.edges.All(e => color[p.nodes.IndexOf(e.Key)] != color[p.nodes.IndexOf(e.Value)])) return true;
            int at = 0;
            while (at < color.Length && ++color[at] == p.K) color[at++] = 0;
            if (at == color.Length) return false;
        }
    }

    [Fact]
    public void SetCover_TwoEdgesWhoseNamesJoinTheSameWay_AreTwoElements() {
        var red = new KarpVertexCoverToSetCover(new VERTEXCOVER("(({a_b,c,a,b_c},{{a_b,c},{a,b_c}}),1)"));
        var target = new SETCOVER(red.reductionTo.instance);
        Assert.Equal(2, target.universal.Distinct().Count());
        Assert.False(HasSetCover(target));
    }

    [Fact]
    public void SetCover_OrdinaryNames_AreUnchanged() {
        var red = new KarpVertexCoverToSetCover(new VERTEXCOVER("(({a,b,c,d,e},{{a,b},{a,c},{a,e},{b,e},{c,d}}),3)"));
        Assert.Equal("({a_b,a_c,a_e,b_e,c_d},{{a_b,a_c,a_e},{a_b,b_e},{a_c,c_d},{c_d},{a_e,b_e}},3)", red.reductionTo.instance);
    }

    [Theory]
    [InlineData("(({a_b,c,a,b_c},{{a_b,c},{a,b_c}}),1)")]
    [InlineData("(({a_b,c,a,b_c},{{a_b,c},{a,b_c}}),2)")]
    [InlineData("(({x_,_y,x,__y},{{x_,_y},{x,__y},{x_,x}}),1)")]
    [InlineData("(({x_,_y,x,__y},{{x_,_y},{x,__y},{x_,x}}),2)")]
    [InlineData(@"(({a\,b,a\_b},{{a\,b},{a\_b,b}}),1)")]
    public void SetCover_HasACoverExactlyWhenTheGraphHasAVertexCover(string instance) {
        var source = new VERTEXCOVER(instance);
        var target = new SETCOVER(new KarpVertexCoverToSetCover(source).reductionTo.instance);
        Assert.Equal(target.universal.Count, target.universal.Distinct().Count());
        Assert.Equal(HasVertexCover(source), HasSetCover(target));
    }

    [Fact]
    public void ExactCover_ANodeNamedLikeAnEdge_IsItsOwnElement() {
        var red = new KarpGraphColorToExactCover(new GRAPHCOLORING("(({a_b,a,b},{{a,b}}),2)"));
        var target = new EXACTCOVER(red.reductionTo.instance);
        Assert.Equal(target.X.Count, target.X.Distinct().Count());
        Assert.True(HasExactCover(target));
    }

    [Fact]
    public void ExactCover_OrdinaryNames_AreUnchanged() {
        var red = new KarpGraphColorToExactCover(new GRAPHCOLORING("(({a,b,c},{{a,b},{b,c}}),2)"));
        Assert.Equal("({a,b,c,a_b,b_c,a_a_b_1,a_a_b_2,b_a_b_1,b_a_b_2,b_b_c_1,b_b_c_2,c_b_c_1,c_b_c_2},{{a,a_a_b_1},{a,a_a_b_2},{b,b_a_b_1,b_b_c_1},{b,b_a_b_2,b_b_c_2},{c,c_b_c_1},{c,c_b_c_2},{a_b,a_a_b_2,b_a_b_1},{a_b,a_a_b_1,b_a_b_2},{b_c,b_b_c_2,c_b_c_1},{b_c,b_b_c_1,c_b_c_2}})",
            red.reductionTo.instance);
    }

    [Theory]
    [InlineData("(({a_b,a,b},{{a,b}}),2)")]
    [InlineData("(({a_b,a,b},{{a,b},{a_b,a},{a_b,b}}),2)")]
    [InlineData("(({x_,_y},{{x_,_y}}),2)")]
    public void ExactCover_HasACoverExactlyWhenTheGraphHasAColoring(string instance) {
        var source = new GRAPHCOLORING(instance);
        var target = new EXACTCOVER(new KarpGraphColorToExactCover(source).reductionTo.instance);
        Assert.Equal(target.X.Count, target.X.Distinct().Count());
        Assert.Equal(HasColoring(source), HasExactCover(target));
    }
}
