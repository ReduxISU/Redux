using API.Interfaces;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Weighted-shortest-path reduction finder (GitHub issue #148 on Redux_Frontend).
//
// ReductionGraphData.WeightedPathBetween runs Dijkstra over the reduction graph,
// weighting each hop by the cheapest declared ReductionCost among that hop's parallel
// edges (see ReductionGraphData.CostRank). This complements the existing unweighted,
// hop-count-only PathBetween BFS.
//
// As of this writing the real reduction graph has no (from, to) pair with more than
// one parallel edge, and no pair reachable by more than one distinct route -- so there
// is no real example where Dijkstra's chosen route differs from a naive BFS route
// (see MinimalityTest_CostIsGenericallyMinimal below for the fallback this implies
// for the "minimality" requirement). There IS a real multi-hop chain to exercise the
// happy path end-to-end: INDEPENDENTSET -> CLIQUE -> VERTEXCOVER -> SETCOVER, via
// reduceToCLIQUE, sipserReductionVertexCover, and KarpVertexCoverToSetCover.
public class ReductionPath_Tests {
    /// <summary>
    /// A real 3-hop chain (INDEPENDENTSET -> CLIQUE -> VERTEXCOVER -> SETCOVER) exists
    /// in the graph today via reduceToCLIQUE, sipserReductionVertexCover, and
    /// KarpVertexCoverToSetCover. WeightedPathBetween must find it, report it in order,
    /// and every className along the way must be a real, registered reduction type.
    /// </summary>
    [Fact]
    public void WeightedPathBetween_FindsKnownMultiHopChain() {
        var result = ReductionGraphData.WeightedPathBetween("INDEPENDENTSET", "SETCOVER");

        Assert.True(result.found);
        Assert.Equal(new List<string> { "INDEPENDENTSET", "CLIQUE", "VERTEXCOVER", "SETCOVER" }, result.nodes);
        Assert.Equal(3, result.hops.Count);

        Assert.Equal("INDEPENDENTSET", result.hops[0].from);
        Assert.Equal("CLIQUE", result.hops[0].to);
        Assert.Equal("VERTEXCOVER", result.hops[1].to);
        Assert.Equal("SETCOVER", result.hops[2].to);

        foreach (var hop in result.hops) {
            Assert.True(ProblemProvider.Reductions.ContainsKey(hop.className.ToLower()),
                $"{hop.className} (hop {hop.from} -> {hop.to}) is not a registered reduction type in ProblemProvider.Reductions.");
        }
    }

    /// <summary>An unknown source name returns found=false with empty nodes/hops, not an exception.</summary>
    [Fact]
    public void WeightedPathBetween_UnknownSource_ReturnsNotFound() {
        var result = ReductionGraphData.WeightedPathBetween("NOT_A_REAL_PROBLEM", "SETCOVER");

        Assert.False(result.found);
        Assert.Empty(result.nodes);
        Assert.Empty(result.hops);
    }

    /// <summary>An unknown target name returns found=false with empty nodes/hops, not an exception.</summary>
    [Fact]
    public void WeightedPathBetween_UnknownTarget_ReturnsNotFound() {
        var result = ReductionGraphData.WeightedPathBetween("CLIQUE", "NOT_A_REAL_PROBLEM");

        Assert.False(result.found);
        Assert.Empty(result.nodes);
        Assert.Empty(result.hops);
    }

    /// <summary>source == target returns found=false rather than a trivial zero-hop path.</summary>
    [Fact]
    public void WeightedPathBetween_SourceEqualsTarget_ReturnsNotFound() {
        var result = ReductionGraphData.WeightedPathBetween("CLIQUE", "CLIQUE");

        Assert.False(result.found);
        Assert.Empty(result.nodes);
        Assert.Empty(result.hops);
    }

    /// <summary>Case-insensitive resolution matches PathBetween's existing convention.</summary>
    [Fact]
    public void WeightedPathBetween_IsCaseInsensitive() {
        var result = ReductionGraphData.WeightedPathBetween("independentset", "setcover");

        Assert.True(result.found);
        Assert.Equal(new List<string> { "INDEPENDENTSET", "CLIQUE", "VERTEXCOVER", "SETCOVER" }, result.nodes);
    }

    /// <summary>
    /// Genuine "did Dijkstra actually minimize" coverage needs either (a) a (from, to) pair
    /// with multiple parallel edges of differing cost, or (b) a pair reachable by more than
    /// one distinct route through the graph. Neither exists in the real reduction data today
    /// (confirmed by dumping ReductionGraphData.Graph: every (from, to) pair currently has
    /// exactly one edge, and the graph is presently acyclic with no two distinct simple paths
    /// between any pair). So per the task's documented fallback, this instead asserts the
    /// general correctness property directly: for every hop WeightedPathBetween reports,
    /// on every real reachable pair in the graph, the chosen edge really is the minimum-
    /// CostRank edge among Graph[hop.from][hop.to] -- i.e. the per-hop edge selection itself
    /// (the piece Dijkstra's weighting depends on) is correct, even though today's data can't
    /// exercise a case where minimizing total cost beats minimizing hop count.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllReachablePairs))]
    public void WeightedPathBetween_EachHopUsesCheapestParallelEdge(string source, string target) {
        var result = ReductionGraphData.WeightedPathBetween(source, target);
        Assert.True(result.found, $"{source} -> {target} should be reachable (BFS PathBetween found it) but WeightedPathBetween did not.");

        foreach (var hop in result.hops) {
            var candidates = ReductionGraphData.Graph[hop.from][hop.to];
            string cheapest = candidates
                .OrderBy(e => CostRank(e.cost))
                .First().cost;
            Assert.Equal(CostRank(cheapest), CostRank(hop.cost));
        }
    }

    // Mirrors ReductionGraphData's private CostRank table (Linear cheapest ... Unclassified
    // most expensive) so this test can independently check "is this really the minimum" without
    // reaching into the class's private state.
    private static int CostRank(string cost) => cost switch {
        nameof(ReductionCost.Linear) => 0,
        nameof(ReductionCost.Quadratic) => 1,
        nameof(ReductionCost.Cubic) => 2,
        nameof(ReductionCost.HigherPolynomial) => 3,
        _ => 4,
    };

    // Every (source, target) pair in the graph that the existing unweighted PathBetween
    // can already reach -- used to exercise WeightedPathBetween's per-hop edge selection
    // across all of today's real reduction data, not just the one hand-picked chain above.
    public static IEnumerable<object[]> AllReachablePairs() {
        foreach (var source in ReductionGraphData.Graph.Keys)
            foreach (var target in ReductionGraphData.ReachableFrom(source))
                yield return new object[] { source, target };
    }
}
