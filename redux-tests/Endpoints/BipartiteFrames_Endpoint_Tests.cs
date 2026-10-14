using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Bipartite Graph picture on the actual /ProblemProvider/visualize?format=frames output, for Set Cover, Exact Cover and
// Hitting Set with every one of their solvers, plus the guards that keep everything old frontends read exactly as it was:
//   - the list format (snapshots in Snapshots/, captured before the Bipartite picture existed),
//   - /info for every visualization and solver that was touched (info.<name>.json, captured the same way).
// The one deliberate exception is Set Cover's default visualization, which moved off DummyVisualization: see the "rewire" tests.
public class BipartiteFrames_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public BipartiteFrames_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private static string ReadSnapshot(string file) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Endpoints", "Snapshots", file)).Replace("\r\n", "\n");

    private async Task<(HttpStatusCode Status, string Body)> Visualize(string query, string instance) {
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/visualize?{query}", content, TestContext.Current.CancellationToken);
        return (response.StatusCode, (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Replace("\r\n", "\n"));
    }

    private static readonly HashSet<string> Vocabulary = new() {
        "Background", "ElementHighlight", "Solution", "Rejected", "Untraveled", "Covered", "Blocked", "Committed",
    };

    // visualization, problem, pick side, rule, whether the instance has a K
    private static readonly (string Vis, string Problem, string Pick, string Rule, bool HasK)[] Pictures = {
        ("setcoverdefaultvisualization", "setcover", "sets", "atLeastOnce", true),
        ("exactcoverdefaultvisualization", "exactcover", "sets", "exactlyOnce", false),
        ("hittingsetdefaultvisualization", "hittingset", "elements", "exactlyOnce", false),
    };

    // Every solver of each problem, and whether it builds its answer piece by piece.
    private static readonly (string Vis, string Solver, bool Incremental)[] SolverRows = {
        ("setcoverdefaultvisualization", "setcoverbruteforce", false),
        ("setcoverdefaultvisualization", "greedysetcover", true),
        ("setcoverdefaultvisualization", "heuristicsolver", true),
        ("exactcoverdefaultvisualization", "exactcoverbruteforce", false),
        ("exactcoverdefaultvisualization", "dancinglinks", true),
        ("exactcoverdefaultvisualization", "exactcoverrecursive", true),
        ("hittingsetdefaultvisualization", "hittingsetbruteforce", false),
    };

    public static TheoryData<string> Visualizations => new(Pictures.Select(p => p.Vis));
    public static TheoryData<string, string, bool> Solvers {
        get {
            var data = new TheoryData<string, string, bool>();
            foreach (var r in SolverRows) data.Add(r.Vis, r.Solver, r.Incremental);
            return data;
        }
    }

    // Every visualization and solver touched by the Bipartite picture and the step recording.
    public static TheoryData<string> TouchedInterfaces => new(Pictures.Select(p => p.Vis).Concat(SolverRows.Select(r => r.Solver)));

    [Theory]
    [MemberData(nameof(TouchedInterfaces))]
    public async Task Info_IsUnchangedByTheBipartitePicture(string name) {
        var response = await _client.GetAsync($"/ProblemProvider/info?interface={name}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ReadSnapshot($"info.{name}.json"), body.Replace("\r\n", "\n"));
    }

    [Theory]
    [MemberData(nameof(Visualizations))]
    public async Task Info_StillReportsTheLegacyType_NotBipartiteGraph(string vis) {
        var response = await _client.GetAsync($"/ProblemProvider/info?interface={vis}", TestContext.Current.CancellationToken);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement;
        Assert.Equal("SetD3", json.GetProperty("visualizationType").GetString());
    }

    // ---- Set Cover's default visualization moved off DummyVisualization: the only thing that may change. ----

    // What embeds Set Cover's defaultVisualization: Set Cover itself and the reduction into it (Vertex Cover to Set Cover).
    // Their snapshots were taken after the rewire; the diff against the pre-change capture is only that nested object.
    public static TheoryData<string> EmbedsSetCoversVisualization => new() { "setcover", "karpvertexcovertosetcover" };

    [Theory]
    [MemberData(nameof(EmbedsSetCoversVisualization))]
    public async Task Rewire_Info_ShowsSetCoversRealVisualization_AndNothingElse(string name) {
        var response = await _client.GetAsync($"/ProblemProvider/info?interface={name}", TestContext.Current.CancellationToken);
        var body = (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Replace("\r\n", "\n");
        Assert.Equal(ReadSnapshot($"info.{name}.json"), body);
        Assert.Contains("\"visualizationName\":\"Set Cover Visualization\"", body);
        Assert.DoesNotContain("DummyVisualization", body);
    }

    [Fact]
    public async Task Rewire_SetCoverInfo_ReportsTheSetCoverVisualizationAndItsSolver() {
        var response = await _client.GetAsync("/ProblemProvider/info?interface=setcover", TestContext.Current.CancellationToken);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement;
        var vis = json.GetProperty("defaultVisualization");
        Assert.Equal("Set Cover Visualization", vis.GetProperty("visualizationName").GetString());
        Assert.Equal("SetD3", vis.GetProperty("visualizationType").GetString());
        Assert.Equal("Set Cover Brute Force", vis.GetProperty("solver").GetProperty("solverName").GetString());
        // The same object the visualization reports about itself.
        var own = JsonDocument.Parse(ReadSnapshot("info.setcoverdefaultvisualization.json")).RootElement;
        Assert.Equal(own.GetProperty("visualizationName").GetString(), vis.GetProperty("visualizationName").GetString());
        Assert.Equal(own.GetProperty("sourceFile").GetString(), vis.GetProperty("sourceFile").GetString());
    }

    [Fact]
    public async Task Rewire_Reduce_IntoSetCover_ChangesOnlyTheNestedVisualization() {
        string instance = ReadSnapshot("karpvertexcovertosetcover.instance.txt");
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/reduce?reduction=karpvertexcovertosetcover", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ReadSnapshot("karpvertexcovertosetcover.reduce.json"), (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Replace("\r\n", "\n"));
    }

    [Fact]
    public async Task Rewire_VisualizeReduction_IntoSetCover_NowReturnsTheSetCoverPicture() {
        // Before the rewire the reduction's target visualization was DummyVisualization, so the endpoint answered an
        // empty list (#755). Now it answers with Set Cover's list items.
        string instance = ReadSnapshot("karpvertexcovertosetcover.instance.txt");
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/visualizeReduction?reduction=karpvertexcovertosetcover&solution=" + Uri.EscapeDataString("{a,b,c}"), content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Replace("\r\n", "\n");
        Assert.Equal(ReadSnapshot("karpvertexcovertosetcover.visualizereduction.json"), body);
        var items = JsonDocument.Parse(body).RootElement.EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.All(items, i => Assert.Equal("set", i.GetProperty("kind").GetString()));
    }

    // ---- The list format is what old frontends read: it must not change. ----

    [Theory]
    [MemberData(nameof(Solvers))]
    public async Task List_DefaultResponse_MatchesPreChangeSnapshot_WhicheverSolverIsChosen(string vis, string solver, bool _) {
        var instance = ReadSnapshot($"{vis}.instance.txt");
        var (status, body) = await Visualize($"visualization={vis}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(ReadSnapshot($"{vis}.json"), body);
        // The solver now records typed steps, but the list format is [initial, solved] exactly as before.
        var (_, withSolver) = await Visualize($"visualization={vis}&solver={solver}&format=list", instance);
        Assert.Equal(2, JsonDocument.Parse(withSolver).RootElement.GetArrayLength());
    }

    // ---- The picture ----

    // The certificate a Done frame's Solution nodes stand for, in the problem's own format.
    private static string Certificate(JsonElement payload, JsonElement frame) {
        var chosen = frame.GetProperty("nodes").EnumerateObject().Where(n => n.Value.GetProperty("state").GetString() == "Solution").Select(n => n.Name).ToHashSet();
        var labels = payload.GetProperty("elements").EnumerateArray().ToDictionary(e => e.GetProperty("id").GetString()!, e => e.GetProperty("label").GetString()!);
        if (payload.GetProperty("pickSide").GetString() == "elements")
            return "{" + string.Join(",", payload.GetProperty("elements").EnumerateArray().Select(e => e.GetProperty("id").GetString()!).Where(chosen.Contains).Select(id => labels[id])) + "}";
        return "{" + string.Join(",", payload.GetProperty("sets").EnumerateArray().Where(s => chosen.Contains(s.GetProperty("id").GetString()!))
            .Select(s => "{" + string.Join(",", s.GetProperty("members").EnumerateArray().Select(m => labels[m.GetString()!])) + "}")) + "}";
    }

    [Theory]
    [MemberData(nameof(Solvers))]
    public async Task Frames_EverySolver_HasTheBipartitePayloadAndFramesThatAgreeWithTheVerifier(string vis, string solver, bool incremental) {
        var picture = Pictures.Single(p => p.Vis == vis);
        var problem = (API.Interfaces.IProblem)Activator.CreateInstance(ProblemProvider.Problems[picture.Problem])!;
        var (status, body) = await Visualize($"visualization={vis}&format=frames&solver={solver}", problem.defaultInstance);
        Assert.Equal(HttpStatusCode.OK, status);
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal(new[] { "type", "payload", "frames" }, root.EnumerateObject().Select(p => p.Name));
        Assert.Equal("BipartiteGraph", root.GetProperty("type").GetString());

        var payload = root.GetProperty("payload");
        Assert.Equal(["kind", "layout", "pickSide", "rule", "elements", "sets", "edges", .. picture.HasK ? new[] { "k" } : []],
            payload.EnumerateObject().Select(p => p.Name));
        Assert.Equal("bipartiteGraph", payload.GetProperty("kind").GetString());
        Assert.Equal("columns", payload.GetProperty("layout").GetString());
        Assert.Equal(picture.Pick, payload.GetProperty("pickSide").GetString());
        Assert.Equal(picture.Rule, payload.GetProperty("rule").GetString());
        // The payload is static and colorless: no state names anywhere in it.
        Assert.DoesNotContain("\"state\"", payload.GetRawText());
        Assert.DoesNotContain("\"color\"", payload.GetRawText());

        var elementIds = payload.GetProperty("elements").EnumerateArray().Select(e => e.GetProperty("id").GetString()!).ToList();
        var setIds = payload.GetProperty("sets").EnumerateArray().Select(s => s.GetProperty("id").GetString()!).ToList();
        Assert.All(elementIds, id => Assert.StartsWith("e:", id));
        Assert.Equal(Enumerable.Range(1, setIds.Count).Select(i => $"S{i}"), setIds);
        var nodeIds = elementIds.Concat(setIds).ToHashSet();
        Assert.Equal(elementIds.Count + setIds.Count, nodeIds.Count);
        var edges = payload.GetProperty("edges").EnumerateArray().ToList();
        Assert.NotEmpty(edges);
        foreach (var e in edges) {
            string a = e.GetProperty("a").GetString()!, b = e.GetProperty("b").GetString()!;
            Assert.Contains(a, elementIds);
            Assert.Contains(b, setIds);
            Assert.Equal($"{a}~{b}", e.GetProperty("id").GetString());
        }
        var edgeIds = edges.Select(e => e.GetProperty("id").GetString()!).ToHashSet();
        Assert.Equal(edges.Count, edgeIds.Count);
        // Each set's members are exactly the elements its edges reach.
        foreach (var s in payload.GetProperty("sets").EnumerateArray())
            Assert.Equal(edges.Where(e => e.GetProperty("b").GetString() == s.GetProperty("id").GetString()).Select(e => e.GetProperty("a").GetString()),
                s.GetProperty("members").EnumerateArray().Select(m => m.GetString()));

        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        Assert.True(frames.Count >= 2, "the solver records steps");
        var picks = picture.Pick == "sets" ? setIds.ToHashSet() : elementIds.ToHashSet();
        foreach (var f in frames) {
            bool isDone = f.GetProperty("event").GetString() == "Done";
            Assert.Equal(["event", "caption", "focus", "nodes", "edges", "hits", .. isDone ? new[] { "ok" } : []], f.EnumerateObject().Select(p => p.Name));
            Assert.Contains(f.GetProperty("event").GetString(), new[] { "Try", "Accept", "Reject", "Backtrack", "Done" });
            Assert.False(string.IsNullOrWhiteSpace(f.GetProperty("caption").GetString()));
            foreach (var id in f.GetProperty("focus").EnumerateArray()) Assert.True(nodeIds.Contains(id.GetString()!) || edgeIds.Contains(id.GetString()!), id.GetString());
            foreach (var (collection, ids) in new[] { ("nodes", nodeIds), ("edges", edgeIds) })
                foreach (var entry in f.GetProperty(collection).EnumerateObject()) {
                    Assert.Contains(entry.Name, ids);
                    string state = entry.Value.GetProperty("state").GetString()!;
                    Assert.Contains(state, Vocabulary);
                    Assert.NotEqual("Background", state); // Background is omitted
                }
            foreach (var hit in f.GetProperty("hits").EnumerateObject()) {
                Assert.Contains(hit.Name, nodeIds);
                Assert.DoesNotContain(hit.Name, picks);   // hits count the other column
                Assert.True(hit.Value.GetInt32() > 0);
            }
            // Solution is for what is taken: never in a Try or Reject of a whole candidate.
            if (!incremental && f.GetProperty("event").GetString() is "Try" or "Reject")
                Assert.DoesNotContain(f.GetProperty("nodes").EnumerateObject(), n => n.Value.GetProperty("state").GetString() == "Solution");
        }

        // Exactly one Done, and it is last; its ok agrees with the problem's verifier on the answer it draws.
        Assert.Single(frames, f => f.GetProperty("event").GetString() == "Done");
        var last = frames[^1];
        Assert.Equal("Done", last.GetProperty("event").GetString());
        Assert.True(last.GetProperty("ok").GetBoolean(), $"{solver} solves the default instance");
        Assert.True(problem.defaultVerifier.verify(problem.defaultInstance, Certificate(payload, last)), Certificate(payload, last));
        // Every constraint is met, so none is Rejected.
        Assert.DoesNotContain(last.GetProperty("nodes").EnumerateObject(), n => n.Value.GetProperty("state").GetString() == "Rejected");
    }

    [Theory]
    [InlineData("setcoverdefaultvisualization", "greedysetcover")]
    [InlineData("exactcoverdefaultvisualization", "dancinglinks")]
    public async Task Frames_AnIncrementalSolver_KeepsWhatItTookAsSolution_AndHighlightsOnlyThePieceUnderTest(string vis, string solver) {
        var (_, body) = await Visualize($"visualization={vis}&format=frames&solver={solver}", ReadSnapshot($"{vis}.instance.txt"));
        var frames = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().ToList();
        var accepts = frames.Where(f => f.GetProperty("event").GetString() == "Accept").ToList();
        Assert.True(accepts.Count >= 2);
        // From the second Accept on, the first pick stays Solution while the new one is highlighted.
        var second = accepts[1];
        var focus = second.GetProperty("focus").EnumerateArray().Select(f => f.GetString()).Single()!;
        Assert.Equal("ElementHighlight", second.GetProperty("nodes").GetProperty(focus).GetProperty("state").GetString());
        Assert.Contains(second.GetProperty("nodes").EnumerateObject(), n => n.Name != focus && n.Value.GetProperty("state").GetString() == "Solution");
    }

    [Fact]
    public async Task Frames_ExactCover_BlocksSetsThatWouldDoubleUp() {
        // Default instance ({1,2,3,4},{{1,2,3},{2,3},{4,1}}): the answer {S2,S3} blocks S1, which shares 2 and 3 with S2.
        var (_, body) = await Visualize("visualization=exactcoverdefaultvisualization&format=frames", ReadSnapshot("exactcoverdefaultvisualization.instance.txt"));
        var last = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().Last();
        var nodes = last.GetProperty("nodes");
        Assert.Equal("Solution", nodes.GetProperty("S2").GetProperty("state").GetString());
        Assert.Equal("Solution", nodes.GetProperty("S3").GetProperty("state").GetString());
        Assert.Equal("Blocked", nodes.GetProperty("S1").GetProperty("state").GetString());
        Assert.All(new[] { "e:1", "e:2", "e:3", "e:4" }, e => Assert.Equal("Covered", nodes.GetProperty(e).GetProperty("state").GetString()));
    }

    [Fact]
    public async Task Frames_HittingSetBruteForce_HighlightsEachTriedSubset_AndMarksDoubledSetsRejected() {
        var (_, body) = await Visualize("visualization=hittingsetdefaultvisualization&format=frames", ReadSnapshot("hittingsetdefaultvisualization.instance.txt"));
        var frames = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().ToList();
        // Try {1}: element 1 is highlighted; S1 = {1,3} and S3 = {1,4} are hit once, S2 is missed.
        var one = frames[1];
        Assert.Equal("Try {1}: S2 isn't hit.", one.GetProperty("caption").GetString());
        Assert.Equal("ElementHighlight", one.GetProperty("nodes").GetProperty("e:1").GetProperty("state").GetString());
        Assert.Equal("Rejected", one.GetProperty("nodes").GetProperty("S2").GetProperty("state").GetString());
        Assert.Equal("Covered", one.GetProperty("nodes").GetProperty("S1").GetProperty("state").GetString());
        Assert.Equal(1, one.GetProperty("hits").GetProperty("S1").GetInt32());
    }

    [Fact]
    public async Task Frames_NoCoverWithinK_EndsNotOk_WithNothingTaken() {
        var (status, body) = await Visualize("visualization=setcoverdefaultvisualization&format=frames&solver=setcoverbruteforce", "({1,2,3,4,5},{{1,2,3},{2,4},{3,4},{4,5}},1)");
        Assert.Equal(HttpStatusCode.OK, status);
        var frames = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().ToList();
        var last = frames[^1];
        Assert.False(last.GetProperty("ok").GetBoolean());
        Assert.Equal("Checked all 4 combinations of up to K = 1 sets. None covers U.", last.GetProperty("caption").GetString());
        Assert.Empty(last.GetProperty("nodes").EnumerateObject());
        // Each candidate tried was highlighted and what it missed was Rejected.
        Assert.All(frames.Take(frames.Count - 1), f => Assert.Contains(f.GetProperty("nodes").EnumerateObject(), n => n.Value.GetProperty("state").GetString() == "Rejected"));
    }

    [Fact]
    public async Task Frames_ElementNamesWithSeparators_GetUniqueIds_AndTheEdgesNameTheirEnds() {
        // SPADE accepts these names; unescaped, "a~S1" in S2 would give "e:a~S1~S2", which reads like an edge of S1.
        const string instance = "({a~S1,S1,c:d,x},{{a~S1,S1},{c:d,x}},2)";
        var (status, body) = await Visualize("visualization=setcoverdefaultvisualization&format=frames", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var payload = JsonDocument.Parse(body).RootElement.GetProperty("payload");
        Assert.Equal([@"e:a\~S1", "e:S1", @"e:c\:d", "e:x"], payload.GetProperty("elements").EnumerateArray().Select(e => e.GetProperty("id").GetString()));
        Assert.Equal(["a~S1", "S1", "c:d", "x"], payload.GetProperty("elements").EnumerateArray().Select(e => e.GetProperty("label").GetString()));
        var edges = payload.GetProperty("edges").EnumerateArray().ToList();
        Assert.Equal([@"e:a\~S1~S1", "e:S1~S1", @"e:c\:d~S2", "e:x~S2"], edges.Select(e => e.GetProperty("id").GetString()));
        Assert.Equal([(@"e:a\~S1", "S1"), ("e:S1", "S1"), (@"e:c\:d", "S2"), ("e:x", "S2")], edges.Select(e => (e.GetProperty("a").GetString(), e.GetProperty("b").GetString())));
        var last = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().Last();
        Assert.True(last.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task Frames_ChoosingAnotherSolver_ChangesTheStepsButNotThePayload() {
        const string vis = "setcoverdefaultvisualization";
        var instance = ReadSnapshot($"{vis}.instance.txt");
        var (_, brute) = await Visualize($"visualization={vis}&format=frames", instance);
        var (_, greedy) = await Visualize($"visualization={vis}&format=frames&solver=greedysetcover", instance);
        string[] Captions(string json) => JsonDocument.Parse(json).RootElement.GetProperty("frames").EnumerateArray().Select(f => f.GetProperty("caption").GetString()!).ToArray();
        Assert.StartsWith("Try {S1}", Captions(brute)[0]);
        Assert.StartsWith("Pick S1 {1,2,3}", Captions(greedy)[0]);
        Assert.Equal(JsonDocument.Parse(brute).RootElement.GetProperty("payload").GetRawText(), JsonDocument.Parse(greedy).RootElement.GetProperty("payload").GetRawText());
    }
}

[Collection(TimingCollection.Name)]
public class BipartiteFrames_StoppedEndpoint_Tests : IClassFixture<TinyBudgetAppFactory> {
    private readonly HttpClient _client;

    public BipartiteFrames_StoppedEndpoint_Tests(TinyBudgetAppFactory factory) {
        _client = factory.CreateClient();
    }

    // 2^40 combinations of the sets {1}, ..., {40}, none an exact cover (z is in no set).
    private static readonly string SlowExactCover =
        "({" + string.Join(",", Enumerable.Range(1, 40)) + ",z},{" + string.Join(",", Enumerable.Range(1, 40).Select(i => $"{{{i}}}")) + "})";

    [Theory]
    [InlineData("exactcoverbruteforce")]
    [InlineData("exactcoverrecursive")]
    public async Task Frames_StoppedSolve_EndsWithABlankNotOkDoneFrame(string solver) {
        var sw = Stopwatch.StartNew();
        var content = new StringContent(JsonSerializer.Serialize(SlowExactCover), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/visualize?visualization=exactcoverdefaultvisualization&format=frames&solver={solver}", content, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), $"took {sw.Elapsed}");
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal("BipartiteGraph", root.GetProperty("type").GetString());
        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        Assert.True(frames.Count <= 152, $"{frames.Count} frames");
        var done = frames[^1];
        Assert.Equal("Done", done.GetProperty("event").GetString());
        Assert.False(done.GetProperty("ok").GetBoolean());
        Assert.StartsWith("Stopped after 0.05 s and ", done.GetProperty("caption").GetString());
        Assert.Single(frames, f => f.GetProperty("event").GetString() == "Done");
        // The solver had not finished: nothing is drawn as an answer, nothing is judged, nothing is counted.
        Assert.Empty(done.GetProperty("nodes").EnumerateObject());
        Assert.Empty(done.GetProperty("edges").EnumerateObject());
        Assert.Empty(done.GetProperty("hits").EnumerateObject());
        // The frame has exactly the fields every other frame has.
        Assert.Equal(frames[0].EnumerateObject().Select(p => p.Name).Append("ok"), done.EnumerateObject().Select(p => p.Name));
    }
}
