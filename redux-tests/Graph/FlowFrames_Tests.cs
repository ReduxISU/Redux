using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Flow Network picture's base on its own: payload, ids, merged parallel edges, layout, the states of each frame, the
// answer-only Done frame and a stopped run. The solver's steps and the endpoint are tested in FlowSolvers_Tests and
// FlowFrames_Endpoint_Tests.
public class FlowFrames_Tests {
    private static FlowSpec Spec(string[] nodes, (string From, string To, int Capacity)[] edges, string source, string sink) =>
        new(nodes, edges.Select(e => new FlowEdgeInput(e.From, e.To, e.Capacity)).ToList(), source, sink);

    // The Minimum S-T Cut default: ({1,2,3,4},{((1,2),10),((1,3),2),((2,4),3),((3,4),5)},1,4).
    private static readonly FlowSpec Default = Spec(["1", "2", "3", "4"], [("1", "2", 10), ("1", "3", 2), ("2", "4", 3), ("3", "4", 5)], "1", "4");

    private static NetworkFlow Flow(params (string Edge, int Units)[] flow) =>
        new(flow.Select(f => KeyValuePair.Create(f.Edge, f.Units)), [], flow.Length, []);

    private static string Json(object value) => JsonSerializer.Serialize(value, FramesResponse.JsonOptions);

    private static string States(IReadOnlyDictionary<string, StateRef> map) => string.Join(" ", map.Select(p => $"{p.Key}:{p.Value.State}"));

    // ---- Payload ----

    [Fact]
    public void Payload_PutsTheSourceFirstAndTheSinkLast_AndCarriesEveryCapacity() {
        var payload = FlowFrames.Payload(Default);
        Assert.Equal(("flowNetwork", "sourceToSink", "minimumSTCut", "1", "4"), (payload.Kind, payload.Layout, payload.Rule, payload.Source, payload.Sink));
        Assert.Equal([["1"], ["2", "3"], ["4"]], payload.Layers);
        Assert.Equal(["1:0:0", "2:1:0", "3:1:1", "4:2:0"], payload.Nodes.Select(n => $"{n.Id}:{n.Layer}:{n.Row}"));
        Assert.Equal(["1>2 1 2 10", "1>3 1 3 2", "2>4 2 4 3", "3>4 3 4 5"], payload.Edges.Select(e => $"{e.Id} {e.A} {e.B} {e.Capacity}"));
        Assert.All(payload.Edges, e => Assert.Null(e.Label));
    }

    [Fact]
    public void Payload_Json_HasTheContractFieldsInOrder_AndNoLabelUnlessParallel() {
        string json = Json(FlowFrames.Payload(Spec(["s", "t"], [("s", "t", 2), ("s", "t", 5)], "s", "t")));
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(["kind", "layout", "rule", "source", "sink", "nodes", "edges", "layers"], doc.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal(["id", "label", "layer", "row"], doc.RootElement.GetProperty("nodes")[0].EnumerateObject().Select(p => p.Name));
        Assert.Equal(["id", "a", "b", "capacity", "label"], doc.RootElement.GetProperty("edges")[0].EnumerateObject().Select(p => p.Name));
        string single = Json(FlowFrames.Payload(Spec(["s", "t"], [("s", "t", 2)], "s", "t")));
        Assert.DoesNotContain("\"label\":\"", single.Split("\"edges\"")[1]);
    }

    [Fact]
    public void ParallelEdges_AreOneArrowPerDirection_WhoseCapacityIsTheSum_LabelledInInputOrder() {
        var payload = FlowFrames.Payload(Spec(["1", "2", "3"], [("1", "2", 2), ("2", "3", 4), ("1", "2", 5), ("2", "1", 7), ("1", "2", 1)], "1", "3"));
        Assert.Equal(["1>2 8 2,5,1", "2>3 4 ", "2>1 7 "], payload.Edges.Select(e => $"{e.Id} {e.Capacity} {e.Label}"));
    }

    [Fact]
    public void Ids_EscapeTheSeparators_SoNamesNeverCollide() {
        // "a>b" as a node and the edge a>b, and the edge between "a-" and "b" versus "a" and "-b".
        var payload = FlowFrames.Payload(Spec(["a", "b", "a>b", @"x\y", "a-", "-b"], [("a", "b", 1), ("a>b", @"x\y", 1), ("a-", "b", 1), ("a", "-b", 1)], "a", @"x\y"));
        var ids = payload.Nodes.Select(n => n.Id).Concat(payload.Edges.Select(e => e.Id)).ToList();
        Assert.Equal(["a", "b", @"a\>b", @"x\\y", @"a\-", @"\-b"], payload.Nodes.Select(n => n.Id));
        Assert.Equal(["a>b", @"a\>b>x\\y", @"a\->b", @"a>\-b"], payload.Edges.Select(e => e.Id));
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.Equal(["a", "b", "a>b", @"x\y", "a-", "-b"], payload.Nodes.Select(n => n.Label));
        Assert.All(payload.Edges, e => Assert.Equal($"{e.A}>{e.B}", e.Id));
    }

    [Fact]
    public void Layout_ColumnsAreHopsFromTheSource_UnreachedNodesSitBeforeTheSink() {
        // The mockup's "Cut isn't the smallest edge" example, plus u that nothing reaches.
        var payload = FlowFrames.Payload(Spec(["s", "a", "b", "c", "t", "u"],
            [("s", "a", 4), ("s", "b", 4), ("a", "c", 3), ("b", "c", 3), ("a", "b", 1), ("c", "t", 5), ("b", "t", 2), ("u", "t", 1)], "s", "t"));
        Assert.Equal([["s"], ["a", "b"], ["c", "u"], ["t"]], payload.Layers);

        // Nothing leaves the source: the other nodes still get a column before the sink.
        Assert.Equal([["1"], ["2"], ["3"]], FlowFrames.Payload(Spec(["1", "2", "3"], [("2", "3", 1)], "1", "3")).Layers);
        // A node named only by an edge still gets a node.
        Assert.Contains(FlowFrames.Payload(Spec(["1", "3"], [("1", "2", 1), ("2", "3", 1)], "1", "3")).Nodes, n => n.Id == "2");
    }

    // ---- Frames ----

    [Fact]
    public void AugmentingStep_HighlightsThePath_AndMarksFullEdgesCovered() {
        // Path 1 of the default: push 3 along 1 → 2 → 4; 2 → 4 is now full.
        var partial = new NetworkFlow([new("1>2", 3), new("2>4", 3)], [new FlowArc("1>2", true, 3), new FlowArc("2>4", true, 3)], 3, []);
        var frame = FlowFrames.Frame(Default, partial, StepEvent.Accept, ["1", "1>2", "2", "2>4", "4"], "Path 1.", incremental: true);
        Assert.Equal("1:ElementHighlight 2:ElementHighlight 4:ElementHighlight", States(frame.Nodes));
        Assert.Equal("1>2:ElementHighlight 2>4:ElementHighlight", States(frame.Edges));
        Assert.Equal("1>2=3 1>3=0 2>4=3 3>4=0", string.Join(" ", frame.Flow.Select(p => $"{p.Key}={p.Value}")));
        Assert.Equal(3, frame.Value);
        Assert.Equal(2, frame.Path.Count);
        Assert.Null(frame.Groups);
        Assert.Null(frame.Cut);
        Assert.Null(frame.Ok);

        // The next step: 1 → 3 → 4 pushes 2, so 1 → 3 is full; 2 → 4 stays full, and neither is highlighted any more.
        var next = new NetworkFlow([new("1>2", 3), new("2>4", 3), new("1>3", 2), new("3>4", 2)], [new FlowArc("1>3", true, 2), new FlowArc("3>4", true, 2)], 5, []);
        frame = FlowFrames.Frame(Default, next, StepEvent.Accept, ["1", "1>3", "3", "3>4", "4"], "Path 2.", incremental: true);
        Assert.Equal("1>3:ElementHighlight 2>4:Covered 3>4:ElementHighlight", States(frame.Edges));
    }

    [Fact]
    public void AZeroCapacityEdge_IsNeverFull() {
        var frame = FlowFrames.Frame(Spec(["1", "2"], [("1", "2", 0)], "1", "2"), Flow(), StepEvent.Try, [], "Start.", incremental: true);
        Assert.Empty(frame.Edges);
    }

    [Fact]
    public void AnIncrementalReject_MarksItsFocusRejected_AWholeCandidateOnlyHighlights() {
        var reject = FlowFrames.Frame(Default, Flow(), StepEvent.Reject, ["1>2"], "No.", incremental: true);
        Assert.Equal(PictureState.Rejected, reject.Edges["1>2"].State);
        var tried = FlowFrames.Frame(Default, Flow(), StepEvent.Reject, ["1>2"], "No.", incremental: false);
        Assert.Equal(PictureState.ElementHighlight, tried.Edges["1>2"].State);
    }

    [Fact]
    public void Done_SplitsTheNodesIntoTwoSides_TheCutIsSolution_EdgesBackIntoSAreFaded() {
        // ({1,2,3,4},{…,((3,2),4)},1,4) after the max flow: S = {1,2}; 3 → 2 runs from T back into S.
        var spec = Spec(["1", "2", "3", "4"], [("1", "2", 10), ("1", "3", 2), ("2", "4", 3), ("3", "4", 5), ("3", "2", 4)], "1", "4");
        var done = new NetworkFlow([new("1>2", 3), new("2>4", 3), new("1>3", 2), new("3>4", 2)], [], 5, ["1", "2"]);
        var frame = FlowFrames.Frame(spec, done, StepEvent.Done, [], "Done.", ok: true, incremental: true);
        Assert.Empty(frame.Nodes);
        Assert.Equal("1>3:Solution 2>4:Solution 3>2:Untraveled", States(frame.Edges));
        Assert.Equal(new Dictionary<string, int> { ["1"] = 0, ["2"] = 0, ["3"] = 1, ["4"] = 1 }, frame.Groups);
        Assert.Equal(["1>3", "2>4"], frame.Cut);
        Assert.True(frame.Ok);
        Assert.Empty(frame.Path);

        using var doc = JsonDocument.Parse(Json(frame));
        Assert.Equal(["event", "caption", "focus", "nodes", "edges", "flow", "value", "path", "step", "groups", "cut", "ok"], doc.RootElement.EnumerateObject().Select(p => p.Name));
        using var step = JsonDocument.Parse(Json(FlowFrames.Frame(spec, Flow(), StepEvent.Try, [], "Start.", incremental: true)));
        Assert.Equal(["event", "caption", "focus", "nodes", "edges", "flow", "value", "path", "step"], step.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal("{\"edge\":\"1>2\",\"forward\":false,\"units\":1}",
            JsonSerializer.Serialize(new FlowArc("1>2", false, 1), new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    }

    [Fact]
    public void IdsThePictureDoesNotHave_AreIgnored() {
        var frame = FlowFrames.Frame(Default, new NetworkFlow([new("9>9", 4)], [new FlowArc("9>9", true, 4)], 4, []), StepEvent.Accept, ["9", "9>9"], "?", incremental: true);
        Assert.Empty(frame.Nodes);
        Assert.Empty(frame.Edges);
        Assert.DoesNotContain("9>9", frame.Flow.Keys);
        Assert.Empty(frame.Path);
    }

    // ---- Whole responses ----

    [Fact]
    public void Build_AnAnswerWithoutSteps_IsDrawnFromAMaximumFlow() {
        var response = FlowFrames.Build(Default, new SolveRun("{1,2}", [], null), _ => true);
        Assert.Equal("FlowNetwork", response.Type);
        var done = Assert.IsType<FlowFrame>(Assert.Single(response.Frames));
        Assert.Equal((StepEvent.Done, true, 5, "The answer is {1,2}."), (done.Event, done.Ok!.Value, done.Value, done.Caption));
        Assert.Equal(["1>3", "2>4"], done.Cut);
        Assert.Equal(5, done.Flow.Where(p => p.Key.StartsWith("1>")).Sum(p => p.Value));

        var no = Assert.IsType<FlowFrame>(Assert.Single(FlowFrames.Build(Default, new SolveRun("{}", [], null), _ => false).Frames));
        Assert.Equal((false, "No answer was found."), (no.Ok!.Value, no.Caption));
        Assert.Null(no.Groups);
        Assert.Empty(no.Edges);
    }

    [Fact]
    public void Build_AStoppedRun_EndsWithABlankNotOkDone_AndNeverReadsItsAnswer() {
        var steps = new object[] {
            new SolverStep<NetworkFlow>(new NetworkFlow([new("1>2", 3), new("2>4", 3)], [new FlowArc("1>2", true, 3), new FlowArc("2>4", true, 3)], 3, []),
                StepEvent.Accept, ["1", "1>2", "2", "2>4", "4"], "Path 1."),
            new SolverStep<NetworkFlow>(new NetworkFlow([], [], 0, []), StepEvent.Done, [], "Stopped after 0.05 s and 2 steps; the solver had not finished.") { Ok = false },
        };
        var run = new SolveRun(SolveRun.TimedOutAnswer, steps, typeof(NetworkFlow)) { TimedOut = true, Incremental = true };
        var frames = FlowFrames.Build(Default, run, _ => throw new InvalidOperationException("a stopped run's answer is never verified")).Frames.Cast<FlowFrame>().ToList();
        Assert.Equal(2, frames.Count);
        var done = frames[^1];
        Assert.Equal((StepEvent.Done, false), (done.Event, done.Ok!.Value));
        Assert.StartsWith("Stopped after", done.Caption);
        Assert.Empty(done.Nodes);
        Assert.Empty(done.Edges);
        Assert.All(done.Flow.Values, units => Assert.Equal(0, units));
        Assert.Equal(4, done.Flow.Count);
        Assert.Equal(0, done.Value);
        Assert.Null(done.Groups);
        Assert.Null(done.Cut);

        // Steps of another shape cannot be drawn: the stopped frame is added with the shared caption.
        var legacy = new SolveRun(SolveRun.TimedOutAnswer, ["legacy"], null) { TimedOut = true };
        var only = Assert.IsType<FlowFrame>(Assert.Single(FlowFrames.Build(Default, legacy, _ => true).Frames));
        Assert.Equal(GraphSubsetFrames.StoppedCaption, only.Caption);
        Assert.False(only.Ok);
    }

    [Fact]
    public void NetworkFlow_HasValueEquality_AndDropsZeroFlow() {
        var a = new NetworkFlow([new("1>2", 3), new("1>3", 0)], [new FlowArc("1>2", true, 3)], 3, ["2", "1"]);
        var b = new NetworkFlow([new("1>2", 3)], [new FlowArc("1>2", true, 3)], 3, ["1", "2"]);
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, new NetworkFlow([new("1>2", 3)], [new FlowArc("1>2", false, 3)], 3, ["1", "2"]));
        Assert.Equal(new NetworkFlow([], [], 0, []), Shapes.EmptyAnswer(typeof(NetworkFlow)));
    }
}
