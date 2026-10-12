using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Flow Network picture on the actual /ProblemProvider/visualize?format=frames output, for Minimum S-T Cut with its
// solver, plus the guards that keep everything old frontends read exactly as it was:
//   - the list format (Snapshots/flow_list_snapshots.tsv, captured before the picture existed),
//   - /info for the problem, its solver and its visualization (info.<name>.json, captured the same way).
public class FlowFrames_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public FlowFrames_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private static string ReadSnapshot(string file) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Endpoints", "Snapshots", file)).Replace("\r\n", "\n");

    private async Task<(HttpStatusCode Status, string Body)> Visualize(string query, string instance) {
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/visualize?{query}", content, TestContext.Current.CancellationToken);
        return (response.StatusCode, (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Replace("\r\n", "\n"));
    }

    private static string Compact(string json) => JsonSerializer.Serialize(JsonDocument.Parse(json).RootElement);

    // Relaxed escaping only to keep expected text readable: the response writes ">" as >.
    private static string Readable(JsonElement element) =>
        JsonSerializer.Serialize(element, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    private static readonly HashSet<string> Vocabulary = new() {
        "Background", "ElementHighlight", "Solution", "Rejected", "Untraveled", "Covered", "Blocked",
    };

    public static TheoryData<string, string, string> FramesCases {
        get {
            var data = new TheoryData<string, string, string>();
            foreach (var (solver, instance, answer) in FlowSolvers_Tests.Rows()) data.Add(solver, instance, answer);
            return data;
        }
    }

    public static TheoryData<string, string, string> ListCases {
        get {
            var data = new TheoryData<string, string, string>();
            foreach (var line in ReadSnapshot("flow_list_snapshots.tsv").Split('\n').Where(l => l.Length > 0)) {
                var parts = line.Split('\t');
                data.Add(parts[0], parts[1], parts[2]);
            }
            return data;
        }
    }

    public static TheoryData<string> TouchedInterfaces => new(["minstcut", "minstcutsolver", "minstcutvisualization"]);

    // ---- What old frontends read must not change. ----

    [Theory]
    [MemberData(nameof(TouchedInterfaces))]
    public async Task Info_IsUnchangedByTheFlowNetworkPicture(string name) {
        var response = await _client.GetAsync($"/ProblemProvider/info?interface={name}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ReadSnapshot($"info.{name}.json"), body.Replace("\r\n", "\n"));
        Assert.DoesNotContain("FlowNetwork", InfoJson.WithoutPictureType(body));
        Assert.DoesNotContain("StepShape", body);
        Assert.DoesNotContain("Describe", body);
    }

    [Theory]
    [MemberData(nameof(ListCases))]
    public async Task List_MatchesPreChangeSnapshot(string vis, string instance, string expected) {
        var (status, body) = await Visualize($"visualization={vis}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(expected, Compact(body));
    }

    // ---- The picture ----

    [Theory]
    [MemberData(nameof(FramesCases))]
    public async Task Frames_TheSolver_HasTheFlowPayloadAndFramesThatAgreeWithTheAnswer(string solver, string instance, string answer) {
        string vis = FlowSolvers_Tests.VisualizationOf[solver];
        var (status, body) = await Visualize($"visualization={vis}&format=frames&solver={solver}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal(new[] { "type", "payload", "frames" }, root.EnumerateObject().Select(p => p.Name));
        Assert.Equal("FlowNetwork", root.GetProperty("type").GetString());

        var payload = root.GetProperty("payload");
        Assert.Equal(["kind", "layout", "rule", "source", "sink", "nodes", "edges", "layers"], payload.EnumerateObject().Select(p => p.Name));
        Assert.Equal(("flowNetwork", "sourceToSink", "minimumSTCut"),
            (payload.GetProperty("kind").GetString(), payload.GetProperty("layout").GetString(), payload.GetProperty("rule").GetString()));

        var nodeIds = new HashSet<string>();
        foreach (var n in payload.GetProperty("nodes").EnumerateArray()) {
            Assert.Equal(["id", "label", "layer", "row"], n.EnumerateObject().Select(p => p.Name));
            Assert.True(nodeIds.Add(n.GetProperty("id").GetString()!));
        }
        string source = payload.GetProperty("source").GetString()!, sink = payload.GetProperty("sink").GetString()!;
        var layers = payload.GetProperty("layers").EnumerateArray().Select(l => l.EnumerateArray().Select(x => x.GetString()!).ToList()).ToList();
        Assert.Equal(nodeIds.Order(), layers.SelectMany(l => l).Order());
        Assert.Equal([source], layers[0]);
        Assert.Equal([sink], layers[^1]);
        foreach (var n in payload.GetProperty("nodes").EnumerateArray())
            Assert.Equal(n.GetProperty("id").GetString(), layers[n.GetProperty("layer").GetInt32()][n.GetProperty("row").GetInt32()]);

        var capacity = new Dictionary<string, int>();
        var ends = new Dictionary<string, (string A, string B)>();
        foreach (var e in payload.GetProperty("edges").EnumerateArray()) {
            string id = e.GetProperty("id").GetString()!, a = e.GetProperty("a").GetString()!, b = e.GetProperty("b").GetString()!;
            Assert.Contains(a, nodeIds);
            Assert.Contains(b, nodeIds);
            Assert.Equal($"{a}>{b}", id);
            Assert.DoesNotContain(id, nodeIds);
            capacity.Add(id, e.GetProperty("capacity").GetInt32()); // one edge per id
            ends[id] = (a, b);
            if (e.TryGetProperty("label", out var label))
                Assert.Equal(capacity[id], label.GetString()!.Split(',').Sum(int.Parse));
        }

        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        foreach (var f in frames) {
            bool cutFrame = f.TryGetProperty("groups", out _);
            Assert.Equal(["event", "caption", "focus", "nodes", "edges", "flow", "value", "path",
                .. cutFrame ? new[] { "groups", "cut" } : [], .. f.TryGetProperty("ok", out _) ? new[] { "ok" } : []],
                f.EnumerateObject().Select(p => p.Name));
            Assert.All(f.GetProperty("focus").EnumerateArray(), id => Assert.True(nodeIds.Contains(id.GetString()!) || capacity.ContainsKey(id.GetString()!), id.GetString()));
            foreach (var n in f.GetProperty("nodes").EnumerateObject()) {
                Assert.Contains(n.Name, nodeIds);
                Assert.Contains(n.Value.GetProperty("state").GetString()!, Vocabulary);
            }
            foreach (var e in f.GetProperty("edges").EnumerateObject()) {
                Assert.Contains(e.Name, capacity.Keys);
                Assert.Contains(e.Value.GetProperty("state").GetString()!, Vocabulary);
            }
            // Every edge's flow, within its capacity.
            var flow = f.GetProperty("flow").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetInt32());
            Assert.Equal(capacity.Keys.Order(), flow.Keys.Order());
            Assert.All(flow, p => Assert.InRange(p.Value, 0, capacity[p.Key]));
            Assert.Equal(f.GetProperty("value").GetInt32(), flow.Where(p => ends[p.Key].A == source).Sum(p => p.Value) - flow.Where(p => ends[p.Key].B == source).Sum(p => p.Value));
            foreach (var arc in f.GetProperty("path").EnumerateArray()) {
                Assert.Equal(["edge", "forward", "units"], arc.EnumerateObject().Select(p => p.Name));
                Assert.Contains(arc.GetProperty("edge").GetString()!, capacity.Keys);
            }
            // Solution is the cut, only in the Done frame.
            if (f.GetProperty("event").GetString() != "Done")
                Assert.DoesNotContain(f.GetProperty("edges").EnumerateObject(), x => x.Value.GetProperty("state").GetString() == "Solution");
        }

        // One Done frame, last, ok, splitting the nodes as the answer does; its cut is Solution and adds up to the flow.
        var done = Assert.Single(frames, f => f.GetProperty("event").GetString() == "Done");
        Assert.Equal(done, frames[^1]);
        Assert.True(done.GetProperty("ok").GetBoolean());
        var sSide = answer.Trim('{', '}').Split(',').Where(x => x.Length > 0).Select(API.Interfaces.Graphs.FlowFrames.NodeId).ToHashSet();
        var groups = done.GetProperty("groups").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetInt32());
        Assert.Equal(nodeIds.Order(), groups.Keys.Order());
        Assert.All(groups, g => Assert.Equal(sSide.Contains(g.Key) ? 0 : 1, g.Value));
        var cut = done.GetProperty("cut").EnumerateArray().Select(x => x.GetString()!).ToList();
        Assert.Equal(capacity.Keys.Where(id => sSide.Contains(ends[id].A) && !sSide.Contains(ends[id].B)).Order(), cut.Order());
        Assert.Equal(done.GetProperty("value").GetInt32(), cut.Sum(id => capacity[id]));
        Assert.All(cut, id => Assert.Equal("Solution", done.GetProperty("edges").GetProperty(id).GetProperty("state").GetString()));
        Assert.Empty(done.GetProperty("nodes").EnumerateObject());
    }

    [Fact]
    public async Task Frames_Default_MatchesTheMockup() {
        var (_, body) = await Visualize("visualization=minstcutvisualization&format=frames", "({1,2,3,4},{((1,2),10),((1,3),2),((2,4),3),((3,4),5)},1,4)");
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal(
            "{\"kind\":\"flowNetwork\",\"layout\":\"sourceToSink\",\"rule\":\"minimumSTCut\",\"source\":\"1\",\"sink\":\"4\","
            + "\"nodes\":[{\"id\":\"1\",\"label\":\"1\",\"layer\":0,\"row\":0},{\"id\":\"2\",\"label\":\"2\",\"layer\":1,\"row\":0},"
            + "{\"id\":\"3\",\"label\":\"3\",\"layer\":1,\"row\":1},{\"id\":\"4\",\"label\":\"4\",\"layer\":2,\"row\":0}],"
            + "\"edges\":[{\"id\":\"1>2\",\"a\":\"1\",\"b\":\"2\",\"capacity\":10},{\"id\":\"1>3\",\"a\":\"1\",\"b\":\"3\",\"capacity\":2},"
            + "{\"id\":\"2>4\",\"a\":\"2\",\"b\":\"4\",\"capacity\":3},{\"id\":\"3>4\",\"a\":\"3\",\"b\":\"4\",\"capacity\":5}],"
            + "\"layers\":[[\"1\"],[\"2\",\"3\"],[\"4\"]]}",
            Readable(root.GetProperty("payload")));
        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        Assert.Equal(["Try", "Accept", "Accept", "Done"], frames.Select(f => f.GetProperty("event").GetString()));
        Assert.Equal(
            "{\"event\":\"Accept\",\"caption\":\"Path 2: push 2 along 1 → 3 → 4. 1 → 3 is now full. Flow is now 5.\","
            + "\"focus\":[\"1\",\"1>3\",\"3\",\"3>4\",\"4\"],"
            + "\"nodes\":{\"1\":{\"state\":\"ElementHighlight\"},\"3\":{\"state\":\"ElementHighlight\"},\"4\":{\"state\":\"ElementHighlight\"}},"
            + "\"edges\":{\"1>3\":{\"state\":\"ElementHighlight\"},\"2>4\":{\"state\":\"Covered\"},\"3>4\":{\"state\":\"ElementHighlight\"}},"
            + "\"flow\":{\"1>2\":3,\"1>3\":2,\"2>4\":3,\"3>4\":2},\"value\":5,"
            + "\"path\":[{\"edge\":\"1>3\",\"forward\":true,\"units\":2},{\"edge\":\"3>4\",\"forward\":true,\"units\":2}]}",
            Readable(frames[2]));
        Assert.Equal(
            "{\"event\":\"Done\",\"caption\":\"No augmenting path is left. The nodes still reachable from 1 form S = {1,2}. The 2 edges from S to T are full and add up to 5, equal to the maximum flow.\","
            + "\"focus\":[],\"nodes\":{},\"edges\":{\"1>3\":{\"state\":\"Solution\"},\"2>4\":{\"state\":\"Solution\"}},"
            + "\"flow\":{\"1>2\":3,\"1>3\":2,\"2>4\":3,\"3>4\":2},\"value\":5,\"path\":[],"
            + "\"groups\":{\"1\":0,\"2\":0,\"3\":1,\"4\":1},\"cut\":[\"1>3\",\"2>4\"],\"ok\":true}",
            Readable(frames[3]));
    }

    [Fact]
    public async Task Frames_ParallelEdges_AreOneArrowPerDirection_WhoseCapacityIsTheSum() {
        var (_, body) = await Visualize("visualization=minstcutvisualization&format=frames", "({1,2,3},{((1,2),2),((2,3),4),((1,2),5),((2,1),7),((1,2),2)},1,3)");
        Assert.Equal(
            // ((1,2),2) twice is one edge: the instance's edges are a set.
            "[{\"id\":\"1>2\",\"a\":\"1\",\"b\":\"2\",\"capacity\":7,\"label\":\"2,5\"},"
            + "{\"id\":\"2>3\",\"a\":\"2\",\"b\":\"3\",\"capacity\":4},"
            + "{\"id\":\"2>1\",\"a\":\"2\",\"b\":\"1\",\"capacity\":7}]",
            Readable(JsonDocument.Parse(body).RootElement.GetProperty("payload").GetProperty("edges")));
    }

    [Fact]
    public async Task Frames_NamesWithSeparators_KeepEveryIdUnique() {
        var (status, body) = await Visualize("visualization=minstcutvisualization&format=frames", "({a-b,c>d,e},{((a-b,c>d),2),((c>d,e),1),((a-b,e),1)},a-b,e)");
        Assert.Equal(HttpStatusCode.OK, status);
        var root = JsonDocument.Parse(body).RootElement;
        var payload = root.GetProperty("payload");
        Assert.Equal([@"a\-b", @"c\>d", "e"], payload.GetProperty("nodes").EnumerateArray().Select(n => n.GetProperty("id").GetString()));
        Assert.Equal([@"a\-b>c\>d", @"c\>d>e", @"a\-b>e"], payload.GetProperty("edges").EnumerateArray().Select(e => e.GetProperty("id").GetString()));
        Assert.Equal((@"a\-b", "e"), (payload.GetProperty("source").GetString(), payload.GetProperty("sink").GetString()));
        var done = root.GetProperty("frames").EnumerateArray().Last();
        Assert.Equal("{\"a\\\\-b\":0,\"c\\\\>d\":0,\"e\":1}", Readable(done.GetProperty("groups")));
    }

    [Fact]
    public async Task Frames_ImpossibleInstance_IsA400_NotAPicture() {
        var (status, body) = await Visualize("visualization=minstcutvisualization&format=frames", "({1,2},{((1,2),3)},1,1)");
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("instance_parse_error", body);
    }
}

// A stopped run, under the 0.05 s budget of TinyBudgetAppFactory.
[Collection(TimingCollection.Name)]
public class FlowFrames_StoppedRun_Endpoint_Tests : IClassFixture<TinyBudgetAppFactory> {
    private readonly HttpClient _client;

    public FlowFrames_StoppedRun_Endpoint_Tests(TinyBudgetAppFactory factory) {
        _client = factory.CreateClient();
    }

    // 600 separate paths s → a_i → t of capacity 1: one augmenting path each, far more steps than 0.05 s allows.
    internal static string SlowNetwork {
        get {
            var hubs = Enumerable.Range(1, 600).Select(i => $"a{i}").ToList();
            var edges = hubs.SelectMany(a => new[] { $"((s,{a}),1)", $"(({a},t),1)" });
            return $"({{s,t,{string.Join(",", hubs)}}},{{{string.Join(",", edges)}}},s,t)";
        }
    }

    [Fact]
    public async Task Frames_StoppedRun_EndsWithABlankNotOkDone() {
        var sw = Stopwatch.StartNew();
        var content = new StringContent(JsonSerializer.Serialize(SlowNetwork), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/visualize?visualization=minstcutvisualization&format=frames", content, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(20), $"took {sw.Elapsed}");

        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal("FlowNetwork", root.GetProperty("type").GetString());
        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        Assert.True(frames.Count >= 2);
        Assert.True(frames.Count < 600, $"{frames.Count} frames: the run was not stopped");
        var done = frames[^1];
        Assert.Equal("Done", done.GetProperty("event").GetString());
        Assert.False(done.GetProperty("ok").GetBoolean());
        Assert.Matches(@"^Stopped after 0\.05 s and [\d,]+ steps?; the solver had not finished\.", done.GetProperty("caption").GetString());
        Assert.Empty(done.GetProperty("nodes").EnumerateObject());
        Assert.Empty(done.GetProperty("edges").EnumerateObject());
        Assert.All(done.GetProperty("flow").EnumerateObject(), p => Assert.Equal(0, p.Value.GetInt32()));
        Assert.Equal(0, done.GetProperty("value").GetInt32());
        Assert.Empty(done.GetProperty("path").EnumerateArray());
        Assert.False(done.TryGetProperty("groups", out _));
        Assert.False(done.TryGetProperty("cut", out _));
        Assert.Single(frames, f => f.GetProperty("event").GetString() == "Done");
        Assert.DoesNotContain(frames, f => f.GetProperty("edges").EnumerateObject().Any(n => n.Value.GetProperty("state").GetString() == "Solution"));
    }
}
