using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Layered Graph picture on the actual /ProblemProvider/visualize?format=frames output, for Topological Sort, Feedback
// Arc Set, Feedback Node Set, Strongly Connected Components, SSSP and SPSP with their solvers, plus the guards that keep
// everything old frontends read exactly as it was:
//   - the list format (Snapshots/layered_list_snapshots.tsv, captured before the picture existed; two Arc Set / Node Set
//     rows retaken for #789, where an acyclic graph now gets {} and so nothing drawn as removed),
//   - /info for every problem, solver and visualization that was touched (info.<name>.json, captured the same way).
// The one deliberate exception is Strongly Connected Components' default visualization, which moved off
// DummyVisualization to SCCVisualization: see the "rewire" test.
public class LayeredFrames_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public LayeredFrames_Endpoint_Tests(AppFactory factory) {
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

    private static readonly HashSet<string> Vocabulary = new() {
        "Background", "ElementHighlight", "Solution", "Rejected", "Untraveled", "Covered", "Blocked", "Committed",
    };

    public static TheoryData<string, string, string> FramesCases {
        get {
            var data = new TheoryData<string, string, string>();
            foreach (var (solver, instance, answer) in LayeredSolvers_Tests.Rows()) data.Add(solver, instance, answer);
            return data;
        }
    }

    public static TheoryData<string, string, string> ListCases {
        get {
            var data = new TheoryData<string, string, string>();
            foreach (var line in ReadSnapshot("layered_list_snapshots.tsv").Split('\n').Where(l => l.Length > 0)) {
                var parts = line.Split('\t');
                data.Add(parts[0], parts[1], parts[2]);
            }
            return data;
        }
    }

    public static TheoryData<string> TouchedInterfaces => new([
        "topologicalsortdefaultvisualization", "arcsetdefaultvisualization", "nodesetdefaultvisualization", "ssspvisualization",
        "sssptablevisualization", "spspvisualization", "spsptablevisualization", "kahnsalgorithm", "arcsetbruteforce",
        "nodesetbruteforce", "kosarajusolver", "ssspsolver", "spspsolver", "topologicalsort", "arcset", "nodeset",
        "sssp", "spsp",
    ]);

    // ---- What old frontends read must not change. ----

    [Theory]
    [MemberData(nameof(TouchedInterfaces))]
    public async Task Info_IsUnchangedByTheLayeredGraphPicture(string name) {
        var response = await _client.GetAsync($"/ProblemProvider/info?interface={name}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ReadSnapshot($"info.{name}.json"), body.Replace("\r\n", "\n"));
        Assert.DoesNotContain("ListReadsTypedSteps", body);
        Assert.DoesNotContain("ListedEdges", body);
        Assert.DoesNotContain("LayeredGraph", InfoJson.WithoutPictureType(body));
    }

    // Strongly Connected Components' default visualization moved off DummyVisualization to SCCVisualization. Its /info
    // snapshot was retaken after the rewire; the diff against the pre-change capture is only that nested object.
    [Fact]
    public async Task Rewire_SccInfo_ShowsSccVisualization_AndNothingElse() {
        var response = await _client.GetAsync("/ProblemProvider/info?interface=stronglyconnectedcomponents", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Replace("\r\n", "\n");
        Assert.Equal(ReadSnapshot("info.stronglyconnectedcomponents.json"), body);
        Assert.DoesNotContain("DummyVisualization", body);
        Assert.DoesNotContain("LayeredGraph", InfoJson.WithoutPictureType(body));
        var vis = JsonDocument.Parse(body).RootElement.GetProperty("defaultVisualization");
        Assert.Equal("Strongly Connected Components Visualization", vis.GetProperty("visualizationName").GetString());
        Assert.Equal("GraphD3", vis.GetProperty("visualizationType").GetString());
        Assert.Equal("Kosaraju's Algorithm", vis.GetProperty("solver").GetProperty("solverName").GetString());
    }

    [Theory]
    [MemberData(nameof(ListCases))]
    public async Task List_MatchesPreChangeSnapshot(string vis, string instance, string expected) {
        var (status, body) = await Visualize($"visualization={vis}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(expected, Compact(body));
    }

    [Fact]
    public async Task Frames_TheShortestPathTables_AreTheTablePicture_NotTheLayeredGraph() {
        // The tables are the Table picture's second view of the same runs (#671, TablePathsFrames_Endpoint_Tests).
        foreach (var (vis, instance) in new[] {
            ("sssptablevisualization", "({1,2,3},{((1,2),1),((2,3),1)},1)"), ("spsptablevisualization", "({1,2,3},{((1,2),1),((2,3),1)},1,3)") }) {
            var (_, body) = await Visualize($"visualization={vis}&format=frames", instance);
            Assert.Equal("Table", JsonDocument.Parse(body).RootElement.GetProperty("type").GetString());
        }
    }

    // ---- The picture ----

    private static readonly Dictionary<string, string> Shapes = new() {
        ["kahnsalgorithm"] = "order", ["arcsetbruteforce"] = "removedSet", ["nodesetbruteforce"] = "removedSet",
        ["kosarajusolver"] = "groups", ["ssspsolver"] = "distances", ["spspsolver"] = "distances",
    };

    private static readonly Dictionary<string, string[]> Annotations = new() {
        ["order"] = ["order"], ["removedSet"] = [], ["groups"] = ["groups", "finish"], ["distances"] = ["dist", "pred"],
    };

    [Theory]
    [MemberData(nameof(FramesCases))]
    public async Task Frames_EverySolver_HasTheLayeredPayloadAndFramesThatAgreeWithTheAnswer(string solver, string instance, string answer) {
        string vis = LayeredSolvers_Tests.VisualizationOf[solver];
        var (status, body) = await Visualize($"visualization={vis}&format=frames&solver={solver}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal(new[] { "type", "payload", "frames" }, root.EnumerateObject().Select(p => p.Name));
        Assert.Equal("LayeredGraph", root.GetProperty("type").GetString());

        var payload = root.GetProperty("payload");
        string shape = Shapes[solver];
        var names = payload.EnumerateObject().Select(p => p.Name).ToList();
        Assert.Equal(["kind", "layout", "shape", "rule", "directed", "weighted", "nodes", "edges", "layers"], names.Take(9));
        Assert.Equal("layeredGraph", payload.GetProperty("kind").GetString());
        Assert.Equal("ranked", payload.GetProperty("layout").GetString());
        Assert.Equal(shape, payload.GetProperty("shape").GetString());
        Assert.Equal(solver is "arcsetbruteforce" or "nodesetbruteforce", names.Contains("k"));
        Assert.Equal(shape == "distances", names.Contains("source"));
        Assert.Equal(solver == "spspsolver", names.Contains("target"));

        var nodeIds = new HashSet<string>();
        foreach (var n in payload.GetProperty("nodes").EnumerateArray()) {
            Assert.Equal(["id", "label", "layer", "row"], n.EnumerateObject().Select(p => p.Name));
            Assert.True(nodeIds.Add(n.GetProperty("id").GetString()!));
        }
        var layers = payload.GetProperty("layers").EnumerateArray().Select(l => l.EnumerateArray().Select(x => x.GetString()!).ToList()).ToList();
        Assert.Equal(nodeIds.Order(), layers.SelectMany(l => l).Order());
        foreach (var n in payload.GetProperty("nodes").EnumerateArray())
            Assert.Equal(n.GetProperty("id").GetString(), layers[n.GetProperty("layer").GetInt32()][n.GetProperty("row").GetInt32()]);

        bool directed = payload.GetProperty("directed").GetBoolean();
        var edgeIds = new HashSet<string>();
        var layerOf = payload.GetProperty("nodes").EnumerateArray().ToDictionary(n => n.GetProperty("id").GetString()!, n => n.GetProperty("layer").GetInt32());
        foreach (var e in payload.GetProperty("edges").EnumerateArray()) {
            string id = e.GetProperty("id").GetString()!, a = e.GetProperty("a").GetString()!, b = e.GetProperty("b").GetString()!;
            Assert.Contains(a, nodeIds);
            Assert.Contains(b, nodeIds);
            Assert.True(edgeIds.Add(id), "one edge per id");
            Assert.DoesNotContain(id, nodeIds);
            if (directed) {
                Assert.Equal($"{a}>{b}", id);
                // A forward edge points to a later layer; only a back edge (or a loop) may point back.
                if (!e.GetProperty("back").GetBoolean()) Assert.True(layerOf[a] < layerOf[b], id);
            }
            Assert.Equal(payload.GetProperty("weighted").GetBoolean(), e.TryGetProperty("weight", out _));
        }

        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        string[] expectedAnnotations = Annotations[shape];
        foreach (var f in frames) {
            Assert.Equal(["event", "caption", "focus", "nodes", "edges", "step", .. expectedAnnotations, .. f.TryGetProperty("ok", out _) ? new[] { "ok" } : []],
                f.EnumerateObject().Select(p => p.Name));
            Assert.All(f.GetProperty("focus").EnumerateArray(), id => Assert.True(nodeIds.Contains(id.GetString()!) || edgeIds.Contains(id.GetString()!), id.GetString()));
            foreach (var n in f.GetProperty("nodes").EnumerateObject()) {
                Assert.Contains(n.Name, nodeIds);
                Assert.Contains(n.Value.GetProperty("state").GetString()!, Vocabulary);
            }
            foreach (var e in f.GetProperty("edges").EnumerateObject()) {
                Assert.Contains(e.Name, edgeIds);
                Assert.Contains(e.Value.GetProperty("state").GetString()!, Vocabulary);
            }
            if (shape == "distances") Assert.Equal(nodeIds.Order(), f.GetProperty("dist").EnumerateObject().Select(p => p.Name).Order());
            // A brute force's tries are highlighted, never drawn as the answer.
            if (!LayeredSolvers_Tests.Incremental.Contains(solver) && f.GetProperty("event").GetString() is "Try" or "Reject")
                Assert.DoesNotContain(f.GetProperty("nodes").EnumerateObject().Concat(f.GetProperty("edges").EnumerateObject()),
                    x => x.Value.GetProperty("state").GetString() == "Solution");
        }

        // One Done frame, last, and its verdict is the answer's.
        var done = Assert.Single(frames, f => f.GetProperty("event").GetString() == "Done");
        Assert.Equal(done, frames[^1]);
        bool ok = LayeredSolvers_Tests.AnswerIsOk(solver, instance, answer);
        Assert.Equal(ok, done.GetProperty("ok").GetBoolean());
        if (ok && answer is not ("{}" or "()") && shape != "groups")
            Assert.Contains(done.GetProperty("nodes").EnumerateObject().Concat(done.GetProperty("edges").EnumerateObject()),
                x => x.Value.GetProperty("state").GetString() == "Solution");
        if (!ok && shape is "order" or "removedSet")
            Assert.Contains(done.GetProperty("edges").EnumerateObject(), x => x.Value.GetProperty("state").GetString() == "Rejected");
    }

    [Fact]
    public async Task Frames_TopologicalSortDefault_PlacesTheNodesInOrder() {
        var (_, body) = await Visualize("visualization=topologicalsortdefaultvisualization&format=frames",
            "({1,2,3,4,5,6},{(1,2),(1,3),(2,4),(3,4),(4,5),(3,6)})");
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal("[[\"1\"],[\"2\",\"3\"],[\"4\",\"6\"],[\"5\"]]", Compact(root.GetProperty("payload").GetProperty("layers").GetRawText()));
        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        var second = frames[2];
        Assert.Equal("Place 2 at position 2. Ready: 3.", second.GetProperty("caption").GetString());
        Assert.Equal("{\"1\":{\"state\":\"Committed\"},\"2\":{\"state\":\"ElementHighlight\"},\"3\":{\"state\":\"Covered\"}}", Compact(second.GetProperty("nodes").GetRawText()));
        Assert.Equal("[\"1\",\"2\"]", Compact(second.GetProperty("order").GetRawText()));
        Assert.Equal("[\"1\",\"2\",\"3\",\"4\",\"6\",\"5\"]", Compact(frames[^1].GetProperty("order").GetRawText()));
    }

    [Fact]
    public async Task Frames_ParallelEdges_AreOneEdgePerDirection_LabelledWithEveryWeight() {
        var (_, body) = await Visualize("visualization=ssspvisualization&format=frames", "({1,2,3},{((1,2),5),((1,2),2),((2,3),1),((2,1),7)},1)");
        var edges = JsonDocument.Parse(body).RootElement.GetProperty("payload").GetProperty("edges");
        Assert.Equal(
            "[{\"id\":\"1>2\",\"a\":\"1\",\"b\":\"2\",\"back\":false,\"weight\":2,\"label\":\"5,2\"},"
            + "{\"id\":\"2>3\",\"a\":\"2\",\"b\":\"3\",\"back\":false,\"weight\":1},"
            + "{\"id\":\"2>1\",\"a\":\"2\",\"b\":\"1\",\"back\":true,\"weight\":7}]",
            // Relaxed escaping only to keep the expected text readable: the response writes ">" as >.
            JsonSerializer.Serialize(edges, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    }

    [Fact]
    public async Task Frames_UndirectedShortestPath_UsesUndirectedEdges() {
        var (_, body) = await Visualize("visualization=spspvisualization&format=frames", "({1,2,3},{({1,2},1),({2,3},1)},3,1)");
        var root = JsonDocument.Parse(body).RootElement;
        var payload = root.GetProperty("payload");
        Assert.False(payload.GetProperty("directed").GetBoolean());
        Assert.Equal(["1-2", "2-3"], payload.GetProperty("edges").EnumerateArray().Select(e => e.GetProperty("id").GetString()));
        var done = root.GetProperty("frames").EnumerateArray().Last();
        Assert.Equal("{\"1-2\":{\"state\":\"Solution\"},\"2-3\":{\"state\":\"Solution\"}}", Compact(done.GetProperty("edges").GetRawText()));
        Assert.Equal("{\"1\":2,\"2\":1,\"3\":0}", Compact(done.GetProperty("dist").GetRawText()));
    }

    [Fact]
    public async Task Frames_NamesWithSeparators_KeepEveryIdUnique() {
        var (status, body) = await Visualize("visualization=topologicalsortdefaultvisualization&format=frames", "({a,a-b,b>c},{(a,a-b),(a-b,b>c)})");
        Assert.Equal(HttpStatusCode.OK, status);
        var payload = JsonDocument.Parse(body).RootElement.GetProperty("payload");
        Assert.Equal(["a", @"a\-b", @"b\>c"], payload.GetProperty("nodes").EnumerateArray().Select(n => n.GetProperty("id").GetString()));
        Assert.Equal([@"a>a\-b", @"a\-b>b\>c"], payload.GetProperty("edges").EnumerateArray().Select(e => e.GetProperty("id").GetString()));
    }

    // #789: the feedback-set brute forces try removing nothing first. The empty candidate draws cleanly: nothing is
    // highlighted, an acyclic graph is accepted at once, and on a cyclic graph only the surviving cycle is Rejected.
    [Theory]
    [InlineData("arcsetdefaultvisualization", "arcsetbruteforce")]
    [InlineData("nodesetdefaultvisualization", "nodesetbruteforce")]
    public async Task Frames_RemovingNothing_IsTheFirstTry_AndDrawsNothingHighlighted(string vis, string solver) {
        var (status, body) = await Visualize($"visualization={vis}&format=frames&solver={solver}", "(({1,2,3,4,5,6},{(2,6),(1,2),(2,3),(1,3)}),1)");
        Assert.Equal(HttpStatusCode.OK, status);
        var frames = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().ToList();
        Assert.Equal(2, frames.Count);
        Assert.Equal("Accept", frames[0].GetProperty("event").GetString());
        Assert.Equal("Try removing nothing. No cycle is left.", frames[0].GetProperty("caption").GetString());
        Assert.Empty(frames[0].GetProperty("focus").EnumerateArray());
        Assert.Empty(frames[0].GetProperty("nodes").EnumerateObject());
        Assert.Empty(frames[0].GetProperty("edges").EnumerateObject());
        Assert.Equal("Done", frames[1].GetProperty("event").GetString());
        Assert.True(frames[1].GetProperty("ok").GetBoolean());
        Assert.Empty(frames[1].GetProperty("nodes").EnumerateObject());
        Assert.Empty(frames[1].GetProperty("edges").EnumerateObject());

        (status, body) = await Visualize($"visualization={vis}&format=frames&solver={solver}", "(({1,2,3},{(1,2),(2,3),(3,1)}),1)");
        Assert.Equal(HttpStatusCode.OK, status);
        var first = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().First();
        Assert.Equal("Reject", first.GetProperty("event").GetString());
        Assert.StartsWith("Try removing nothing: the cycle ", first.GetProperty("caption").GetString());
        Assert.Empty(first.GetProperty("nodes").EnumerateObject());
        Assert.Equal(["1>2", "2>3", "3>1"], first.GetProperty("edges").EnumerateObject().Select(e => e.Name).Order());
        Assert.All(first.GetProperty("edges").EnumerateObject(), e => Assert.Equal("Rejected", e.Value.GetProperty("state").GetString()));
    }

    // #790: {} is SPSP's "no path" answer and the verifier now says so too. The picture of an unreachable target ends on a
    // Done that is not ok, with the target Rejected, and the verifier agrees with that ok.
    [Fact]
    public async Task Frames_SpspUnreachableTarget_EndsNotOk_WithTheTargetRejected() {
        const string instance = "(({1,2,3},{((2,3),1)}),1,3)";
        var (status, body) = await Visualize("visualization=spspvisualization&format=frames", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var done = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().Last();
        Assert.Equal("Done", done.GetProperty("event").GetString());
        Assert.False(done.GetProperty("ok").GetBoolean());
        Assert.Equal("Rejected", done.GetProperty("nodes").GetProperty("3").GetProperty("state").GetString());
        var problem = new API.Problems.P.P_SPSP.SPSP(instance);
        Assert.False(problem.defaultVerifier.verify(problem, problem.defaultSolver.solve(problem)));
    }

    // #892: settling SPSP's target ends the run, but the answer is only the path. No frame marks a node or edge Solution
    // that the Done does not.
    [Fact]
    public async Task Frames_SpspDefault_NoFrameShowsMoreSolutionThanTheDone() {
        var (status, body) = await Visualize("visualization=spspvisualization&format=frames", new API.Problems.P.P_SPSP.SPSP().defaultInstance);
        Assert.Equal(HttpStatusCode.OK, status);
        var frames = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().ToList();
        static HashSet<string> Solution(JsonElement frame, string kind) =>
            frame.GetProperty(kind).EnumerateObject().Where(p => p.Value.GetProperty("state").GetString() == "Solution").Select(p => p.Name).ToHashSet();
        var done = frames[^1];
        Assert.Equal("Done", done.GetProperty("event").GetString());
        Assert.NotEmpty(Solution(done, "nodes"));
        foreach (var frame in frames) {
            Assert.Subset(Solution(done, "nodes"), Solution(frame, "nodes"));
            Assert.Subset(Solution(done, "edges"), Solution(frame, "edges"));
        }
        // The Accept that settles the target already draws the path, and keeps the other settled nodes Committed.
        var accept = frames[^2];
        Assert.Equal("Accept", accept.GetProperty("event").GetString());
        Assert.Equal(Solution(done, "edges"), Solution(accept, "edges"));
        Assert.Contains(accept.GetProperty("nodes").EnumerateObject(), p => p.Value.GetProperty("state").GetString() == "Committed");
    }

    [Fact]
    public async Task Frames_SccHasAPicture() {
        var (status, body) = await Visualize("visualization=sccvisualization&format=frames", "({1,2,3,4,5},{(1,2),(2,3),(3,1),(3,4),(4,5),(5,4)})");
        Assert.Equal(HttpStatusCode.OK, status);
        var done = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().Last();
        Assert.Equal("{\"1\":0,\"2\":0,\"3\":0,\"4\":1,\"5\":1}", Compact(done.GetProperty("groups").GetRawText()));
        Assert.Equal("[\"5\",\"4\",\"3\",\"2\",\"1\"]", Compact(done.GetProperty("finish").GetRawText()));
        // The list format of the new visualization: the graph, then the edges inside a component.
        var (_, list) = await Visualize("visualization=sccvisualization", "({1,2,3},{(1,2),(2,1),(2,3)})");
        var solved = JsonDocument.Parse(list).RootElement.EnumerateArray().Last();
        Assert.Equal(["Solution", "Solution", ""], solved.GetProperty("links").EnumerateArray().Select(l => l.GetProperty("color").GetString()));
    }
}

// A stopped run, under the 0.05 s budget of TinyBudgetAppFactory.
[Collection(TimingCollection.Name)]
public class LayeredFrames_StoppedRun_Endpoint_Tests : IClassFixture<TinyBudgetAppFactory> {
    private readonly HttpClient _client;

    public LayeredFrames_StoppedRun_Endpoint_Tests(TinyBudgetAppFactory factory) {
        _client = factory.CreateClient();
    }

    // Every ordered pair of 9 nodes is an edge, and up to 20 of the 72 edges may be removed: far more tries than 0.05 s allows.
    internal static string SlowArcSet {
        get {
            var nodes = Enumerable.Range(1, 9).ToList();
            var edges = nodes.SelectMany(a => nodes.Where(b => b != a).Select(b => $"({a},{b})"));
            return $"(({{{string.Join(",", nodes)}}},{{{string.Join(",", edges)}}}),20)";
        }
    }

    [Fact]
    public async Task Frames_StoppedArcSetRun_EndsWithABlankNotOkDone() {
        var sw = Stopwatch.StartNew();
        var content = new StringContent(JsonSerializer.Serialize(SlowArcSet), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/visualize?visualization=arcsetdefaultvisualization&format=frames", content, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(20), $"took {sw.Elapsed}");

        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal("LayeredGraph", root.GetProperty("type").GetString());
        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        Assert.True(frames.Count >= 2);
        var done = frames[^1];
        Assert.Equal("Done", done.GetProperty("event").GetString());
        Assert.False(done.GetProperty("ok").GetBoolean());
        Assert.Matches(@"^Stopped after 0\.05 s and [\d,]+ steps?; the solver had not finished\.", done.GetProperty("caption").GetString());
        Assert.Empty(done.GetProperty("nodes").EnumerateObject());
        Assert.Empty(done.GetProperty("edges").EnumerateObject());
        Assert.Single(frames, f => f.GetProperty("event").GetString() == "Done");
        Assert.DoesNotContain(frames, f => f.GetProperty("edges").EnumerateObject().Any(n => n.Value.GetProperty("state").GetString() == "Solution"));
    }
}
