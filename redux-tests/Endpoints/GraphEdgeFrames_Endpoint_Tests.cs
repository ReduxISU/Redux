using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Graph picture's edge and cut shapes on the actual /ProblemProvider/visualize?format=frames output, for Minimum
// Spanning Tree, Steiner Tree, Cut, Weighted Cut, Max Cut and Minimum Cut, plus the guards that keep everything old
// frontends read exactly as it was:
//   - the list format (snapshots in Snapshots/, captured before the edge shapes existed),
//   - /info for every visualization and solver that was touched (info.<name>.json, captured the same way).
public class GraphEdgeFrames_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public GraphEdgeFrames_Endpoint_Tests(AppFactory factory) {
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
        "Background", "ElementHighlight", "Solution", "Rejected", "Untraveled", "Covered", "Blocked",
    };

    // visualization, its default solver, shape, rule, whether the instance has a K, whether edges carry weights
    private static readonly (string Vis, string Solver, string Shape, string Rule, bool HasK, bool Weighted)[] CaseRows = {
        ("minimumspanningtreevisualization", "kruskalsolver", "edges", "spanningTree", false, true),
        ("steinertreedefaultvisualization", "steinertreebruteforce", "edges", "steinerTree", true, false),
        ("cutdefaultvisualization", "cutbruteforce", "cut", "cut", true, false),
        ("weightedcutdefaultvisualization", "weightedcutbruteforce", "cut", "weightedCut", true, true),
        ("maxcutvisualization", "maxcutsolver", "cut", "maxCut", false, true),
        ("mincutvisualization", "mincutstoerwagner", "cut", "minCut", false, true),
    };

    public static TheoryData<string, string, string, string, bool, bool> Cases {
        get {
            var data = new TheoryData<string, string, string, string, bool, bool>();
            foreach (var c in CaseRows) data.Add(c.Vis, c.Solver, c.Shape, c.Rule, c.HasK, c.Weighted);
            return data;
        }
    }
    public static TheoryData<string> Visualizations => new(CaseRows.Select(c => c.Vis));
    public static TheoryData<string, string> VisualizationsAndSolvers => new(CaseRows.Select(c => (c.Vis, c.Solver)));

    // Every visualization and solver touched by the edge shapes and the step recording.
    public static TheoryData<string> TouchedInterfaces => new(CaseRows.Select(c => c.Vis)
        .Concat(["cutbruteforce", "maxcutsolver", "weightedcutbruteforce", "mincutstoerwagner", "steinertreebruteforce", "kruskalsolver", "primsolver"]));

    private static string DefaultInstance(string vis) {
        string key = vis.Replace("defaultvisualization", "").Replace("visualization", "");
        return ((API.Interfaces.IProblem)Activator.CreateInstance(ProblemProvider.Problems[key])!).defaultInstance;
    }

    [Theory]
    [MemberData(nameof(TouchedInterfaces))]
    public async Task Info_IsUnchangedByTheEdgeShapes(string name) {
        var response = await _client.GetAsync($"/ProblemProvider/info?interface={name}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ReadSnapshot($"info.{name}.json"), body.Replace("\r\n", "\n"));
    }

    [Theory]
    [MemberData(nameof(Visualizations))]
    public async Task Info_StillReportsTheLegacyType_NotGraph(string vis) {
        var response = await _client.GetAsync($"/ProblemProvider/info?interface={vis}", TestContext.Current.CancellationToken);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement;
        Assert.Equal("GraphD3", json.GetProperty("visualizationType").GetString());
    }

    // ---- The list format is what old frontends read: it must not change. ----

    [Theory]
    [MemberData(nameof(VisualizationsAndSolvers))]
    public async Task List_DefaultResponse_MatchesPreChangeSnapshot(string vis, string solver) {
        var instance = ReadSnapshot($"{vis}.instance.txt");
        Assert.Equal(DefaultInstance(vis), instance);
        var (status, body) = await Visualize($"visualization={vis}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(ReadSnapshot($"{vis}.json"), body);
        var (_, withSolver) = await Visualize($"visualization={vis}&solver={solver}&format=list", instance);
        Assert.Equal(body, withSolver);
        Assert.Equal(2, JsonDocument.Parse(body).RootElement.GetArrayLength());
    }

    [Theory]
    [InlineData("minimumspanningtreevisualization", "primsolver")]
    public async Task List_WithATypedStepSolver_HasNoStepItems(string vis, string solver) {
        var (status, body) = await Visualize($"visualization={vis}&solver={solver}", ReadSnapshot($"{vis}.instance.txt"));
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(2, JsonDocument.Parse(body).RootElement.GetArrayLength());
    }

    // ---- frames ----

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Frames_DefaultSolver_HasGraphTypePayloadAndFrames(string vis, string solver, string shape, string rule, bool hasK, bool weighted) {
        string instance = DefaultInstance(vis);
        var problem = (API.Interfaces.IProblem)Activator.CreateInstance(ProblemProvider.Problems[vis.Replace("defaultvisualization", "").Replace("visualization", "")])!;
        var (status, body) = await Visualize($"visualization={vis}&format=frames&solver={solver}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var root = JsonDocument.Parse(body).RootElement;

        Assert.Equal("Graph", root.GetProperty("type").GetString());
        var payload = root.GetProperty("payload");
        Assert.Equal("graph", payload.GetProperty("kind").GetString());
        Assert.Equal("undirected", payload.GetProperty("subtype").GetString());
        Assert.Equal("force", payload.GetProperty("layout").GetString());
        Assert.Equal(shape, payload.GetProperty("shape").GetString());
        Assert.Equal(rule, payload.GetProperty("rule").GetString());
        Assert.Equal(hasK, payload.TryGetProperty("k", out var k) && k.ValueKind == JsonValueKind.Number);

        var nodes = payload.GetProperty("nodes").EnumerateArray().ToList();
        var nodeIds = nodes.Select(n => n.GetProperty("id").GetString()!).ToHashSet();
        Assert.All(nodes, n => Assert.Equal(n.GetProperty("id").GetString(), n.GetProperty("label").GetString()));
        var edges = payload.GetProperty("edges").EnumerateArray().ToList();
        Assert.NotEmpty(edges);
        foreach (var e in edges) {
            string a = e.GetProperty("a").GetString()!, b = e.GetProperty("b").GetString()!;
            Assert.True(string.CompareOrdinal(a, b) < 0, $"edge {a},{b} is not canonical (a < b)");
            Assert.Equal($"{a}-{b}", e.GetProperty("id").GetString());
            Assert.Contains(a, nodeIds);
            Assert.Contains(b, nodeIds);
            Assert.Equal(weighted, e.TryGetProperty("weight", out var w) && w.ValueKind == JsonValueKind.Number);
        }
        Assert.Equal(edges.Count, edges.Select(e => e.GetProperty("id").GetString()).Distinct().Count());
        // The payload is static and colorless: no state names anywhere in it.
        Assert.DoesNotContain("\"state\"", payload.GetRawText());
        Assert.DoesNotContain("\"color\"", payload.GetRawText());

        var edgeIds = edges.Select(e => e.GetProperty("id").GetString()!).ToHashSet();
        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        Assert.True(frames.Count >= 2, "the default solver records steps");
        foreach (var f in frames) {
            Assert.Contains(f.GetProperty("event").GetString(), new[] { "Try", "Accept", "Reject", "Backtrack", "Done" });
            Assert.False(string.IsNullOrWhiteSpace(f.GetProperty("caption").GetString()));
            foreach (var id in f.GetProperty("focus").EnumerateArray()) Assert.True(nodeIds.Contains(id.GetString()!) || edgeIds.Contains(id.GetString()!));
            foreach (var (collection, ids) in new[] { ("nodes", nodeIds), ("edges", edgeIds) })
                foreach (var entry in f.GetProperty(collection).EnumerateObject()) {
                    Assert.Contains(entry.Name, ids);
                    string state = entry.Value.GetProperty("state").GetString()!;
                    Assert.Contains(state, Vocabulary);
                    Assert.NotEqual("Background", state); // Background is omitted
                }
            Assert.Equal(0, f.GetProperty("phantoms").GetArrayLength());
            bool isDone = f.GetProperty("event").GetString() == "Done";
            Assert.Equal(isDone, f.TryGetProperty("ok", out _));
            if (shape == "cut") {
                foreach (var g in f.GetProperty("groups").EnumerateObject()) {
                    Assert.Contains(g.Name, nodeIds);
                    Assert.Contains(g.Value.GetInt32(), new[] { 0, 1 });
                }
            } else {
                Assert.False(f.TryGetProperty("groups", out _));
            }
        }

        // Exactly one Done, and it is last; its ok agrees with the problem's verifier on the answer.
        Assert.Single(frames, f => f.GetProperty("event").GetString() == "Done");
        var last = frames[^1];
        Assert.Equal("Done", last.GetProperty("event").GetString());
        Assert.True(last.GetProperty("ok").GetBoolean());
        string solverAnswer = ((API.Interfaces.ISolver)Activator.CreateInstance(ProblemProvider.Solvers[solver])!).solve(instance);
        Assert.True(problem.defaultVerifier.verify(instance, solverAnswer), solverAnswer);
        var solution = last.GetProperty("edges").EnumerateObject().Where(e => e.Value.GetProperty("state").GetString() == "Solution").Select(e => e.Name).ToList();
        Assert.NotEmpty(solution);
        if (shape == "edges") {
            // The Solution edges are the answer's edges.
            string answer = "{" + string.Join(",", solution.Select(id => "{" + id.Replace('-', ',') + "}")) + "}";
            Assert.True(problem.defaultVerifier.verify(instance, answer), answer);
        }
    }

    // MST: the first frame has nothing chosen; each Accept adds exactly one Solution edge and the nodes it joins are Covered.
    [Fact]
    public async Task Frames_MinimumSpanningTree_AddsEdgesInWeightOrder_AndCoversTheJoinedNodes() {
        var (_, body) = await Visualize("visualization=minimumspanningtreevisualization&format=frames", DefaultInstance("minimumspanningtreevisualization"));
        var frames = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().ToList();
        Assert.Equal(["Accept", "Accept", "Accept", "Done"], frames.Select(f => f.GetProperty("event").GetString()));
        // The edge just added is highlighted (over its Solution state); the earlier ones are Solution.
        string[] Chosen(JsonElement f) => f.GetProperty("edges").EnumerateObject().Select(e => e.Name).ToArray();
        Assert.Equal(["1-2"], frames[0].GetProperty("edges").EnumerateObject().Select(e => e.Name));
        Assert.Equal(["1-2", "3-4"], Chosen(frames[1]).Order());
        Assert.Equal(["1-2", "1-4", "3-4"], Chosen(frames[3]).Order());
        // The edge just added is highlighted over its Solution state; the nodes it joins are Covered.
        Assert.Equal("ElementHighlight", frames[0].GetProperty("edges").GetProperty("1-2").GetProperty("state").GetString());
        Assert.Equal("Covered", frames[0].GetProperty("nodes").GetProperty("1").GetProperty("state").GetString());
        Assert.False(frames[0].GetProperty("nodes").TryGetProperty("3", out _));
        Assert.Equal("Edge 1-2 joins two trees, so add it (weight 1).", frames[0].GetProperty("caption").GetString());
    }

    [Fact]
    public async Task Frames_MinimumSpanningTree_ARejectedEdgeIsRejected_AndNotChosen() {
        const string instance = "({1,2,3,4},{({1,2},1),({2,3},1),({1,3},3),({3,4},5)})";
        var (_, body) = await Visualize("visualization=minimumspanningtreevisualization&format=frames", instance);
        var frames = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().ToList();
        var reject = frames.Single(f => f.GetProperty("event").GetString() == "Reject");
        Assert.Equal("Rejected", reject.GetProperty("edges").GetProperty("1-3").GetProperty("state").GetString());
        // Kruskal builds the tree piece by piece: the tree so far stays the answer so far; only the skipped edge is marked.
        Assert.Equal("Solution", reject.GetProperty("edges").GetProperty("1-2").GetProperty("state").GetString());
        Assert.Equal("Solution", reject.GetProperty("edges").GetProperty("2-3").GetProperty("state").GetString());
        Assert.Equal("Covered", reject.GetProperty("nodes").GetProperty("1").GetProperty("state").GetString());
        Assert.False(frames[^1].GetProperty("edges").TryGetProperty("1-3", out _), "1-3 is Background in the answer");
    }

    [Fact]
    public async Task Frames_MinimumSpanningTree_PrimIsAnotherSolverOnTheSamePicture() {
        const string vis = "minimumspanningtreevisualization";
        var instance = ReadSnapshot($"{vis}.instance.txt");
        var (_, kruskal) = await Visualize($"visualization={vis}&format=frames", instance);
        var (status, prim) = await Visualize($"visualization={vis}&format=frames&solver=primsolver", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var frames = JsonDocument.Parse(prim).RootElement.GetProperty("frames").EnumerateArray().ToList();
        Assert.Equal("Try", frames[0].GetProperty("event").GetString());
        Assert.Equal("Done", frames[^1].GetProperty("event").GetString());
        Assert.True(frames[^1].GetProperty("ok").GetBoolean());
        Assert.Equal(JsonDocument.Parse(kruskal).RootElement.GetProperty("payload").GetRawText(), JsonDocument.Parse(prim).RootElement.GetProperty("payload").GetRawText());
        Assert.NotEqual(kruskal, prim);
    }

    [Fact]
    public async Task Frames_MinimumSpanningTree_ADisconnectedGraphEndsNotOkWithNothingChosen() {
        var (status, body) = await Visualize("visualization=minimumspanningtreevisualization&format=frames", "({1,2,3},{({1,2},4)})");
        Assert.Equal(HttpStatusCode.OK, status);
        var last = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().Last();
        Assert.Equal("Done", last.GetProperty("event").GetString());
        Assert.False(last.GetProperty("ok").GetBoolean());
        Assert.Empty(last.GetProperty("edges").EnumerateObject());
    }

    [Fact]
    public async Task Frames_SteinerTree_MarksTerminalsInThePayload_AndTheTreeIsSolution() {
        var (_, body) = await Visualize("visualization=steinertreedefaultvisualization&format=frames", DefaultInstance("steinertreedefaultvisualization"));
        var root = JsonDocument.Parse(body).RootElement;
        var terminals = root.GetProperty("payload").GetProperty("nodes").EnumerateArray()
            .Where(n => n.TryGetProperty("terminal", out var t) && t.GetBoolean()).Select(n => n.GetProperty("id").GetString()!).Order().ToArray();
        Assert.Equal(["2", "5", "8"], terminals);
        Assert.Equal(3, root.GetProperty("payload").GetProperty("nodes").EnumerateArray().Count(n => n.TryGetProperty("terminal", out _)));
        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        Assert.Contains(frames, f => f.GetProperty("event").GetString() == "Reject");
        var last = frames[^1];
        foreach (var t in terminals) Assert.Equal("Covered", last.GetProperty("nodes").GetProperty(t!).GetProperty("state").GetString());
        // The rejected candidates name what failed.
        Assert.All(frames.Where(f => f.GetProperty("event").GetString() == "Reject"),
            f => Assert.Matches(@"^Try edges .+: (terminals? .+ (isn't|aren't) reached|the edges form more than one piece)\.$", f.GetProperty("caption").GetString()));
    }

    [Fact]
    public async Task Frames_Cut_ShowsTheSplitAsGroups_AndItsCrossingEdgesAsSolution() {
        var (_, body) = await Visualize("visualization=cutdefaultvisualization&format=frames", DefaultInstance("cutdefaultvisualization"));
        var frames = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().ToList();
        Assert.Contains(frames, f => f.GetProperty("event").GetString() == "Reject");
        var last = frames[^1];
        // The default instance asks for K = 5 of its 6 edges.
        var groups = last.GetProperty("groups").EnumerateObject().ToDictionary(g => g.Name, g => g.Value.GetInt32());
        Assert.Equal(5, groups.Count);
        Assert.Equal(5, last.GetProperty("edges").EnumerateObject().Count(e => e.Value.GetProperty("state").GetString() == "Solution"));
        var endpoints = JsonDocument.Parse(body).RootElement.GetProperty("payload").GetProperty("edges").EnumerateArray()
            .ToDictionary(e => e.GetProperty("id").GetString()!, e => (e.GetProperty("a").GetString()!, e.GetProperty("b").GetString()!));
        foreach (var e in last.GetProperty("edges").EnumerateObject()) {
            var (a, b) = endpoints[e.Name];
            Assert.NotEqual(groups[a], groups[b]);
        }
        Assert.All(frames.Where(f => f.GetProperty("event").GetString() == "Reject"),
            f => Assert.Matches(@"^Put \{[^}]*\} against \{[^}]*\}: \d+ edges cross, not 5\.$", f.GetProperty("caption").GetString()));
    }

    [Fact]
    public async Task Frames_MaxCut_ImprovesStepByStep_ToTheBestSplit() {
        var (_, body) = await Visualize("visualization=maxcutvisualization&format=frames", DefaultInstance("maxcutvisualization"));
        var frames = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().ToList();
        Assert.All(frames.Take(frames.Count - 1), f => Assert.Equal("Accept", f.GetProperty("event").GetString()));
        var last = frames[^1];
        Assert.Contains("weighs 15", last.GetProperty("caption").GetString());
        var side = last.GetProperty("groups").EnumerateObject().ToDictionary(g => g.Name, g => g.Value.GetInt32());
        // The mockup's split: {1,4} against {2,3,5}, 15 of weight crossing. Which side is S depends on the solver's order.
        Assert.Equal(side["1"], side["4"]);
        Assert.Equal(side["2"], side["3"]);
        Assert.Equal(side["2"], side["5"]);
        Assert.NotEqual(side["1"], side["2"]);
    }

    [Fact]
    public async Task Frames_MinCut_HasOnePhaseStepEach_AndEndsOnTheLightestCut() {
        var (_, body) = await Visualize("visualization=mincutvisualization&format=frames", DefaultInstance("mincutvisualization"));
        var root = JsonDocument.Parse(body).RootElement;
        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        Assert.Equal(5, frames.Count);   // four phases, then Done
        Assert.All(frames.Take(4), f => Assert.StartsWith("Phase ", f.GetProperty("caption").GetString()));
        Assert.Contains("weighs 3", frames[^1].GetProperty("caption").GetString());
        var weights = root.GetProperty("payload").GetProperty("edges").EnumerateArray().ToDictionary(e => e.GetProperty("id").GetString()!, e => e.GetProperty("weight").GetInt32());
        int total = frames[^1].GetProperty("edges").EnumerateObject().Sum(e => weights[e.Name]);
        Assert.Equal(3, total);
    }

    [Fact]
    public async Task Frames_WeightedCut_CaptionsTheCrossingWeight() {
        var (_, body) = await Visualize("visualization=weightedcutdefaultvisualization&format=frames", DefaultInstance("weightedcutdefaultvisualization"));
        var root = JsonDocument.Parse(body).RootElement;
        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        Assert.All(frames.Where(f => f.GetProperty("event").GetString() == "Reject"),
            f => Assert.Matches(@"^Put \{[^}]*\} against \{[^}]*\}: the crossing edges weigh [\d.]+, not 5\.$", f.GetProperty("caption").GetString()));
        var weights = root.GetProperty("payload").GetProperty("edges").EnumerateArray().ToDictionary(e => e.GetProperty("id").GetString()!, e => e.GetProperty("weight").GetInt32());
        Assert.Equal(5, frames[^1].GetProperty("edges").EnumerateObject().Sum(e => weights[e.Name]));
    }

    [Theory]
    [InlineData("minimumspanningtreevisualization", "primsolver")]
    public async Task Frames_EveryCompatibleSolver_EndsInADoneFrame(string vis, string solver) {
        var (status, body) = await Visualize($"visualization={vis}&format=frames&solver={solver}", ReadSnapshot($"{vis}.instance.txt"));
        Assert.Equal(HttpStatusCode.OK, status);
        var frames = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().ToList();
        Assert.True(frames.Count >= 2);
        Assert.Equal("Done", frames[^1].GetProperty("event").GetString());
        Assert.True(frames[^1].GetProperty("ok").GetBoolean());
        Assert.Single(frames, f => f.GetProperty("event").GetString() == "Done");
    }

    [Fact]
    public async Task Frames_CutWithNoCutOfThatSize_EndsNotOkWithNoSplit() {
        // Three edges can never be cut four ways.
        var (status, body) = await Visualize("visualization=cutdefaultvisualization&format=frames", "(({1,2,3},{{1,2},{2,3},{1,3}}),4)");
        Assert.Equal(HttpStatusCode.OK, status);
        var last = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().Last();
        Assert.Equal("Done", last.GetProperty("event").GetString());
        Assert.False(last.GetProperty("ok").GetBoolean());
        Assert.Empty(last.GetProperty("edges").EnumerateObject());
        Assert.Empty(last.GetProperty("groups").EnumerateObject());
    }

    [Fact]
    public async Task Frames_StepsAreCappedAndSayHowManyAreHidden() {
        // 9 nodes and 9 edges, K = 9: the brute force tries a great many candidate S sides, far more than the cap of 150.
        const string instance = "(({1,2,3,4,5,6,7,8,9},{{1,2},{2,3},{3,4},{4,5},{5,6},{6,7},{7,8},{8,9},{1,9}}),9)";
        var (status, body) = await Visualize("visualization=cutdefaultvisualization&format=frames&solver=cutbruteforce", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var frames = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().ToList();
        Assert.Equal(StepRecorder<Groups>.DefaultCap + 1, frames.Count);   // the cap, then Done (no cut of 9 exists, so no Accept is kept)
        Assert.Equal("Done", frames[^1].GetProperty("event").GetString());
        Assert.Contains("not shown", frames[^1].GetProperty("caption").GetString());
    }
}
