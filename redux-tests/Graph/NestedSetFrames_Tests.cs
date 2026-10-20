using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Unit tests of the Nested Set picture (#926): (universe, sets, rule, picks, event, focus) -> the Bipartite Graph's payload and
// frames without the membership edges. No endpoint, no solver.
public class NestedSetFrames_Tests {
    // U = {1,2,3,4}, S1 = {1,2,3}, S2 = {2,3}, S3 = {4,1}
    private static readonly BipartiteSpec ExactCover = new(["1", "2", "3", "4"], [["1", "2", "3"], ["2", "3"], ["4", "1"]], null, BipartitePick.Sets, BipartiteRule.ExactlyOnce);
    private static readonly BipartiteSpec SetCover = new(["1", "2", "3"], [["1", "2"], ["3"]], 2, BipartitePick.Sets, BipartiteRule.AtLeastOnce);

    [Fact]
    public void Payload_Json_IsTheContract_TheBipartiteElementsAndSets_WithoutEdges() {
        Assert.Equal(
            "{\"kind\":\"nestedSet\",\"layout\":\"regions\",\"pickSide\":\"sets\",\"rule\":\"atLeastOnce\"," +
            "\"elements\":[{\"id\":\"e:1\",\"label\":\"1\"},{\"id\":\"e:2\",\"label\":\"2\"},{\"id\":\"e:3\",\"label\":\"3\"}]," +
            "\"sets\":[{\"id\":\"S1\",\"label\":\"S1\",\"members\":[\"e:1\",\"e:2\"]},{\"id\":\"S2\",\"label\":\"S2\",\"members\":[\"e:3\"]}]," +
            "\"k\":2}",
            JsonSerializer.Serialize(NestedSetFrames.Payload(SetCover)));
        // No K: no "k".
        Assert.DoesNotContain("\"k\"", JsonSerializer.Serialize(NestedSetFrames.Payload(ExactCover)));
    }

    [Fact]
    public void Payload_ElementsInNoUniverse_AndRepeatedSets_AreTheBipartitePictures() {
        var spec = new BipartiteSpec(["a"], [["a", "b"], ["a", "b"]], null, BipartitePick.Sets, BipartiteRule.AtLeastOnce);
        var nested = NestedSetFrames.Payload(spec);
        var graph = BipartiteFrames.Payload(spec);
        Assert.Equal(JsonSerializer.Serialize(graph.Elements), JsonSerializer.Serialize(nested.Elements));
        Assert.Equal(JsonSerializer.Serialize(graph.Sets), JsonSerializer.Serialize(nested.Sets));
        Assert.Equal(["S1", "S2"], nested.Sets.Select(s => s.Id));
    }

    private static SolverStep<ChosenSets> Step(StepEvent ev, bool? ok, params string[] sets) => new(new ChosenSets(sets), ev, [], ev.ToString()) { Ok = ok };

    [Fact]
    public void Build_IsTheBipartiteBuild_FrameForFrame_WithoutEdges() {
        var run = new SolveRun("{{2,3},{4,1}}", [
            Step(StepEvent.Accept, null, "S2"),
            Step(StepEvent.Accept, null, "S2", "S3"),
            Step(StepEvent.Done, true, "S2", "S3"),
        ], typeof(ChosenSets)) { Incremental = true };
        var nested = NestedSetFrames.Build(ExactCover, run, a => BipartiteFrames.ParseAnswer(ExactCover, a), _ => true);
        var graph = BipartiteFrames.Build(ExactCover, run, a => BipartiteFrames.ParseAnswer(ExactCover, a), _ => true);
        Assert.Equal("NestedSet", nested.Type);
        Assert.Equal(graph.Frames.Count, nested.Frames.Count);
        for (int i = 0; i < nested.Frames.Count; i++) {
            var n = JsonDocument.Parse(JsonSerializer.Serialize(nested.Frames[i], FramesResponse.JsonOptions)).RootElement;
            var g = JsonDocument.Parse(JsonSerializer.Serialize(graph.Frames[i], FramesResponse.JsonOptions)).RootElement;
            Assert.Equal(g.EnumerateObject().Where(p => p.Name != "edges").Select(p => (p.Name, p.Value.GetRawText())),
                n.EnumerateObject().Select(p => (p.Name, p.Value.GetRawText())));
        }
        var first = (NestedSetFrame)nested.Frames[0];
        Assert.Equal(PictureState.Committed, first.Nodes["S2"].State);
        Assert.Equal(PictureState.Blocked, first.Nodes["S1"].State);
        var last = (NestedSetFrame)nested.Frames[^1];
        Assert.Equal(PictureState.Solution, last.Nodes["S3"].State);
        Assert.Equal(2, last.Step);
    }

    [Fact]
    public void Build_StoppedRun_EndsOnABlankDone() {
        var run = new SolveRun(SolveRun.TimedOutAnswer, [Step(StepEvent.Try, null, "S1")], typeof(ChosenSets)) { TimedOut = true };
        var last = (NestedSetFrame)NestedSetFrames.Build(ExactCover, run, _ => null, _ => false).Frames[^1];
        Assert.Equal((StepEvent.Done, false, (int?)null), (last.Event, last.Ok, last.Step));
        Assert.Empty(last.Nodes);
        Assert.Empty(last.Hits);
    }
}
