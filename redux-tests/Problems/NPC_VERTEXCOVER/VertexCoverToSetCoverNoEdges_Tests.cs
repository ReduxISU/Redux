using Xunit;
using API.Problems.NPComplete.NPC_SETCOVER;
using API.Problems.NPComplete.NPC_VERTEXCOVER;
using API.Problems.NPComplete.NPC_VERTEXCOVER.ReduceTo.NPC_SETCOVER;

namespace redux_tests;

#pragma warning disable CS1591

// Vertex Cover to Set Cover with no edges (#826): every node's set is empty and dropped, and the target used to come out as
// "({}},K)", which Set Cover cannot parse. It is an empty universe and no sets.
public class VertexCoverToSetCoverNoEdges_Tests {

    [Theory]
    [InlineData("(({a,b},{}),1)", "({},{},1)")]
    [InlineData("(({a},{}),0)", "({},{},0)")]
    [InlineData("(({},{}),0)", "({},{},0)")]
    public void NoEdges_IsAnEmptyUniverseAndNoSets(string instance, string target) {
        var red = new KarpVertexCoverToSetCover(new VERTEXCOVER(instance));
        Assert.Equal(target, red.reductionTo.instance);
        var parsed = new SETCOVER(red.reductionTo.instance);
        Assert.Empty(parsed.universal);
        Assert.Empty(parsed.subsets);
    }

    // A graph with edges keeps its target as it was.
    [Fact]
    public void WithEdges_TheTargetIsUnchanged() {
        var red = new KarpVertexCoverToSetCover(new VERTEXCOVER("(({a,b,c},{{a,b}}),1)"));
        Assert.Equal("({a_b},{{a_b},{a_b}},1)", red.reductionTo.instance);
    }
}
