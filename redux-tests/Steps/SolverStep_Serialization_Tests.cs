using System.Text.Json;
using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Asserts on the real JSON, using the serializer options the visualize endpoint uses.
public class SolverStep_Serialization_Tests {
    private static JsonElement Serialize<T>(T value) =>
        JsonDocument.Parse(JsonSerializer.Serialize(value, ProblemProvider.VisualizeJsonOptions())).RootElement;

    [Fact]
    public void NodeSetStep_UsesCamelCaseNames_AndEventAsString_AndOmitsOk() {
        var step = new SolverStep<NodeSet>(new NodeSet(["b", "a"]), StepEvent.Try, ["a"], "Pick a.");
        var json = Serialize(step);

        Assert.Equal(new[] { "partial", "event", "focus", "caption" }, json.EnumerateObject().Select(p => p.Name));
        Assert.Equal("Try", json.GetProperty("event").GetString());
        Assert.Equal("Pick a.", json.GetProperty("caption").GetString());
        Assert.Equal(new[] { "a" }, json.GetProperty("focus").EnumerateArray().Select(e => e.GetString()));
        var partial = json.GetProperty("partial");
        Assert.Equal(new[] { "nodes" }, partial.EnumerateObject().Select(p => p.Name));
        Assert.Equal(new[] { "a", "b" }, partial.GetProperty("nodes").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public void ActiveStatesDoneStep_IncludesOk() {
        var step = new SolverStep<ActiveStates>(new ActiveStates(["2"], 1), StepEvent.Done, [], "Accepted.") { Ok = true };
        var json = Serialize(step);

        Assert.Equal(new[] { "partial", "event", "focus", "caption", "ok" }, json.EnumerateObject().Select(p => p.Name));
        Assert.Equal("Done", json.GetProperty("event").GetString());
        Assert.True(json.GetProperty("ok").GetBoolean());
        var partial = json.GetProperty("partial");
        Assert.Equal(new[] { "states", "position" }, partial.EnumerateObject().Select(p => p.Name));
        Assert.Equal(1, partial.GetProperty("position").GetInt32());
        Assert.Equal(new[] { "2" }, partial.GetProperty("states").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public void FalseOk_IsKept_OnlyNullIsOmitted() {
        var step = new SolverStep<ActiveStates>(new ActiveStates([], 0), StepEvent.Done, [], "Rejected.") { Ok = false };
        Assert.False(Serialize(step).GetProperty("ok").GetBoolean());
    }

    // A visualization written through an IVisualization-typed property (as the reduce endpoint writes a
    // reduction's visualizations) must serialize, and must not expose StepShape: System.Text.Json rejects Type.
    [Fact]
    public void Visualization_WithAStepShape_SerializesThroughTheInterface_WithoutStepShape() {
        API.Interfaces.IVisualization vis = new API.Problems.P.P_DFA.Visualizations.DFAVisualization();
        Assert.NotNull(vis.StepShape);

        var json = JsonDocument.Parse(JsonSerializer.Serialize(vis, new JsonSerializerOptions { IncludeFields = true })).RootElement;

        Assert.False(json.TryGetProperty("StepShape", out _));
        Assert.False(json.TryGetProperty("stepShape", out _));
    }
}
