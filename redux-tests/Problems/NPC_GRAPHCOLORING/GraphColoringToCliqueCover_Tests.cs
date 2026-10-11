using Xunit;
using API.Problems.NPComplete.NPC_CLIQUECOVER;
using API.Problems.NPComplete.NPC_GRAPHCOLORING;
using API.Problems.NPComplete.NPC_GRAPHCOLORING.ReduceTo.NPC_CLIQUECOVER;

namespace redux_tests;

#pragma warning disable CS1591

// Graph Coloring to Clique Cover builds the complement graph: a K-coloring of G is a cover of the complement by K cliques
// (each color class has no edge in G, so it is a clique in the complement). Until #822 an edge of G seen with its ends the
// other way round was not recognised, so every edge of G was copied into the "complement" too and the target was a complete
// graph that one clique covers.
public class GraphColoringToCliqueCover_Tests {

    // Whether the nodes can be split into at most k groups that each satisfy `fits` (tried by brute force on small graphs).
    private static bool Splits(List<string> nodes, int k, Func<List<string>, bool> fits) {
        var group = new int[nodes.Count];
        while (true) {
            bool ok = true;
            for (int g = 0; g < k && ok; g++)
                ok = fits(nodes.Where((_, i) => group[i] == g).ToList());
            if (ok) return true;
            int at = 0;
            while (at < nodes.Count && ++group[at] == k) group[at++] = 0;
            if (at == nodes.Count) return false;
        }
    }

    private static bool Joined(List<KeyValuePair<string, string>> edges, string a, string b) =>
        edges.Any(e => (e.Key == a && e.Value == b) || (e.Key == b && e.Value == a));

    [Fact]
    public void Triangle_BecomesAGraphWithNoEdges() {
        var red = new GraphColoringToCliqueCover(new GRAPHCOLORING("(({a,b,c},{{a,b},{b,c},{a,c}}),2)"));
        Assert.Equal("(({a,b,c},{}),2)", red.reductionTo.instance);
    }

    [Fact]
    public void Path_BecomesItsComplement() {
        var red = new GraphColoringToCliqueCover(new GRAPHCOLORING("(({a,b,c},{{a,b},{b,c}}),2)"));
        var target = new CLIQUECOVER(red.reductionTo.instance);
        Assert.True(Joined(target.edges, "a", "c"));
        Assert.False(Joined(target.edges, "a", "b"));
        Assert.False(Joined(target.edges, "b", "c"));
    }

    // The target has a cover by K cliques exactly when the source has a K-coloring, and its edges are exactly the pairs the
    // source is missing.
    [Theory]
    [InlineData("(({a,b,c},{{a,b},{b,c},{a,c}}),2)")]
    [InlineData("(({a,b,c},{{a,b},{b,c},{a,c}}),3)")]
    [InlineData("(({a,b,c},{{a,b},{b,c}}),2)")]
    [InlineData("(({1,2,3,4},{{1,2},{2,3},{3,4},{4,1}}),2)")]
    [InlineData("(({1,2,3,4},{{1,2},{2,3},{3,1},{3,4}}),2)")]
    [InlineData("(({1,2,3,4},{{1,2},{2,3},{3,1},{3,4}}),3)")]
    [InlineData("(({1,2,3,4,5},{{1,2},{2,3},{3,4},{4,5},{5,1}}),2)")]
    [InlineData("(({1,2,3,4,5},{{1,2},{2,3},{3,4},{4,5},{5,1}}),3)")]
    public void Target_IsTheComplement_AndHasACliqueCoverExactlyWhenTheSourceHasAColoring(string instance) {
        var source = new GRAPHCOLORING(instance);
        var red = new GraphColoringToCliqueCover(source);
        var target = new CLIQUECOVER(red.reductionTo.instance);
        Assert.Equal(source.K, target.K);
        foreach (string a in source.nodes)
            foreach (string b in source.nodes)
                if (a != b) Assert.Equal(!Joined(source.edges, a, b), Joined(target.edges, a, b));

        bool colorable = Splits(source.nodes, source.K, g => g.All(a => g.All(b => !Joined(source.edges, a, b))));
        bool covered = Splits(target.nodes, target.K, g => g.All(a => g.All(b => a == b || Joined(target.edges, a, b))));
        Assert.Equal(colorable, covered);
    }
}
