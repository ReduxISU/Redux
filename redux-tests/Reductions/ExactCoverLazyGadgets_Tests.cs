using System.Text.Json;
using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_GRAPHCOLORING.ReduceTo.NPC_EXACTCOVER;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Graph Coloring to Exact Cover works out its gadgets the first time they are asked for, not in every reduce(): finding each
// set among the sets Exact Cover reads parses the target, which made reduce() some 25 times slower after #694.
public class ExactCoverLazyGadgets_Tests {
    private const string Instance = "(({a,b,c},{{a,b},{b,c}}),2)";

    [Fact]
    public void LazyGadgets_AreTheRecordedBuildsGadgets() {
        var plain = new KarpGraphColorToExactCover(Instance);
        var recorded = new KarpGraphColorToExactCover(Instance, new StepRecorder<BuildDelta>(cap: int.MaxValue));
        Assert.NotEmpty(plain.gadgets);
        Assert.Equal(JsonSerializer.Serialize(recorded.gadgets), JsonSerializer.Serialize(plain.gadgets));
    }

    [Fact]
    public void ReduceAgain_DoesNotDuplicateTheGadgets() {
        var red = new KarpGraphColorToExactCover(Instance);
        int count = red.gadgets.Count;
        red.reduce();
        red.reduce();
        Assert.Equal(count, red.gadgets.Count);
    }

    [Fact]
    public void MapBack_BeforeTheGadgetsAreAskedFor_StillMapsTheAnswer() {
        var red = new KarpGraphColorToExactCover(Instance);
        string cover = red.mapSolutions("{{a,c},{b}}");
        var (answer, _) = red.MapBack(cover, StepRecorder<Groups>.Off);
        Assert.Equal("{{a,c},{b}}", answer);
    }
}
