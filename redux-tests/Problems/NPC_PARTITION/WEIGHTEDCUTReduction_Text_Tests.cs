#pragma warning disable CS1591
using Xunit;
using API.Problems.NPComplete.NPC_PARTITION.ReduceTo.NPC_WEIGHTEDCUT;

namespace API.Tests.Problems.NPC_PARTITION;

// #834: Partition to Weighted Cut's description named another reduction (Graph Coloring to Clique Cover).
public class WEIGHTEDCUTReduction_Text_Tests {

    [Fact]
    public void Definition_DescribesPartitionToWeightedCut() {
        string text = new WEIGHTEDCUTReduction().reductionDefinition;
        Assert.Contains("Partition to Weighted Cut", text);
        Assert.Contains("product", text);
        Assert.DoesNotContain("Graph Coloring", text);
        Assert.DoesNotContain("Clique Cover", text);
    }

    [Fact]
    public void Definition_MatchesWhatReduceBuilds() {
        // {1,2,3}: a node per number, an edge per pair weighing the product, K = (1+2+3)^2 / 4 = 9.
        var red = new WEIGHTEDCUTReduction("{1,2,3}");
        Assert.Equal(new[] { "1", "2", "3" }, red.reductionTo.nodes);
        Assert.Equal(new[] { 2, 3, 6 }, red.reductionTo.edges.Select(e => e.weight));
        Assert.Equal(9, red.reductionTo.K);
    }
}
