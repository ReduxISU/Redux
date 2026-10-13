using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Interfaces.Packing;
using API.Problems.NPComplete.NPC_EXACTCOVER.ReduceTo.NPC_SUBSETSUM;
using API.Problems.NPComplete.NPC_KNAPSACK;
using API.Problems.NPComplete.NPC_KNAPSACK.Visualizations;
using API.Problems.NPComplete.NPC_PARTITION;
using API.Problems.NPComplete.NPC_PARTITION.Visualizations;
using API.Problems.NPComplete.NPC_SUBSETSUM;
using API.Problems.NPComplete.NPC_SUBSETSUM.ReduceTo.NPC_KNAPSACK;
using API.Problems.NPComplete.NPC_SUBSETSUM.Visualizations;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The pictures of Partition, Subset Sum and Knapsack with repeated numbers (#628): every copy is its own item, with its own
// stable, position-based id, and an answer that uses a number twice places both copies.
public class RepeatedNumbersPictures_Tests {

    [Fact]
    public void Groups_KeepRepeats() {
        Assert.Equal([["7", "7"]], PackingFrames.Groups("{7,7}", nested: false));
        Assert.Equal([["3"], ["3"]], PackingFrames.Groups("{(3),(3)}", nested: true));
        Assert.Equal([["(1,2)", "(1,2)"]], PackingFrames.Groups("{(1,2),(1,2)}", nested: false));
        // Unchanged where nothing repeats.
        Assert.Equal([["4", "6"], ["4"], ["9"]], PackingFrames.Groups("((4,6),(4),(9))", nested: true));
        Assert.Empty(PackingFrames.Groups("{}", nested: true));
        Assert.Empty(PackingFrames.Groups("not an answer {", nested: true));
    }

    [Fact]
    public void Packing_Ids_ArePositions_SoRepeatsAreDistinct() {
        var payload = PackingFrames.Payload(PartitionDefaultVisualization.Spec(new PARTITION()));
        Assert.Equal(Enumerable.Range(1, 12).Select(i => $"I{i}"), payload.Items.Select(i => i.Id));
        Assert.Equal(["12", "12"], new[] { payload.Items[2].Label, payload.Items[5].Label });
        Assert.Equal(150, payload.Total);
    }

    [Fact]
    public void Partition_ParseAnswer_PutsEachCopyOnItsSide() {
        var problem = new PARTITION("{3,3,2,2}");
        var hook = (IPackingVisualization<PARTITION>)new PartitionDefaultVisualization();
        Assert.Equal([0, 1, 0, 1], hook.ParseAnswer(problem, "{(3,2),(3,2)}").Assign);
        Assert.Equal([0, 1], hook.ParseAnswer(new PARTITION("{3,3}"), "{(3),(3)}").Assign);
    }

    [Fact]
    public void SubsetSum_Pictures_MarkEachCopy() {
        var problem = new SUBSETSUM("({7,7,1},14)");
        var vis = new SubsetSumVisualization();
        Assert.Equal([0, 0], ((IPackingVisualization<SUBSETSUM>)vis).ParseAnswer(problem, "{7,7}").Assign); // trailing -1s are dropped
        var list = ((API_SET)vis.SolvedVisualization(problem, "{7,7}")).data.list!;
        Assert.Equal(["I1", "I2", "I3"], list.Select(e => e.id));
        Assert.Equal(["Solution", "Solution", "Background"], list.Select(e => e.color));
        // One 7 chosen marks the first copy only.
        list = ((API_SET)vis.SolvedVisualization(new SUBSETSUM("({7,7,1},8)"), "{7,1}")).data.list!;
        Assert.Equal(["Solution", "Background", "Solution"], list.Select(e => e.color));
    }

    [Fact]
    public void SubsetSum_ListPicture_WithoutRepeats_IsTheNumbersInOrder_WithItemIds() {
        var list = new SubsetSumVisualization().visualize(new SUBSETSUM()).data.list!;
        Assert.Equal(["1", "7", "12", "15"], list.Select(e => e.value));
        Assert.Equal(["I1", "I2", "I3", "I4"], list.Select(e => e.id));
    }

    [Fact]
    public void Knapsack_Pictures_MarkEachCopy() {
        var problem = new KNAPSACK("({(1,2),(1,2),(5,5)},2,4)");
        var vis = new KnapsackVisualization();
        Assert.Equal([0, 0], ((IPackingVisualization<KNAPSACK>)vis).ParseAnswer(problem, "{(1,2),(1,2)}").Assign); // trailing -1s are dropped
        Assert.Equal(3, KnapsackVisualization.Spec(problem).Items.Count);
        var list = ((API_SET)vis.SolvedVisualization(problem, "{(1,2),(1,2)}")).data.list!;
        Assert.Equal(["I1", "I2", "I3"], list.Select(e => e.id));
        Assert.Equal(["I2-0", "I2-1"], list[1].list!.Select(e => e.id));
        Assert.Equal(["Solution", "Solution", "Background"], list.Select(e => e.list![0].color));
    }

    [Fact]
    public void Knapsack_ListPicture_WithoutRepeats_IsTheItemsInOrder_WithItemIds() {
        var list = new KnapsackVisualization().visualize(new KNAPSACK()).data.list!;
        Assert.Equal(["I1", "I2", "I3"], list.Select(e => e.id));
        Assert.Equal("I1-0", list[0].list![0].id);
    }

    [Fact]
    public void Feng_MapsAnswersWithRepeats_BothWays() {
        var red = new FengReduction("({7,7,1},14)");
        Assert.Equal("{(7,7),(7,7)}", red.mapSolutions("{7,7}"));
        Assert.Throws<ReductionInputException>(() => new FengReduction("({7,1},14)").mapSolutions("{7,7}"));
        var (answer, partial) = red.MapBack("{(7,7),(7,7)}", API.Interfaces.Steps.StepRecorder<API.Interfaces.Steps.Placement>.Off);
        Assert.Equal("{7,7}", answer);
        Assert.Equal([0, 0], partial.Assign); // trailing -1s are dropped
    }

    [Fact]
    public void ExactCoverToSubsetSum_TwoEqualSets_AreTwoEqualNumbers() {
        // {1,2} and {2,1} are the same set written twice; SPADE 0.1.2 keeps both, so Subset Sum gets the same number twice and
        // keeps both (#628).
        var red = new KarpExactCoverToSubsetSum("({1,2},{{1,2},{2,1}})");
        var reread = new SUBSETSUM(red.reductionTo.instance);
        Assert.Equal(red.reductionTo.S, reread.S);
        Assert.Equal(red.reductionFrom.S.Count, reread.S.Count);
        Assert.True(reread.defaultVerifier.verify(reread, reread.defaultSolver.solve(reread)));
    }
}
