using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Graph picture's groups shape on the actual /ProblemProvider/visualize?format=frames output, for Graph Coloring and
// Clique Cover, plus the guards that keep everything old frontends read exactly as it was:
//   - the list format (snapshots in Snapshots/, captured before the groups shape existed),
//   - /info for every visualization and solver that was touched (info.<name>.json, captured the same way).
public class GraphGroupFrames_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public GraphGroupFrames_Endpoint_Tests(AppFactory factory) {
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

    private const string ColoringVis = "graphcoloringdefaultvisualization";
    private const string CoverVis = "cliquecoverdefaultvisualization";

    // visualization, its default solver, rule
    private static readonly (string Vis, string Solver, string Rule)[] CaseRows = {
        (ColoringVis, "graphcoloringbruteforce", "coloring"),
        (CoverVis, "cliquecoverbruteforce", "cliqueCover"),
    };

    public static TheoryData<string, string, string> Cases {
        get {
            var data = new TheoryData<string, string, string>();
            foreach (var c in CaseRows) data.Add(c.Vis, c.Solver, c.Rule);
            return data;
        }
    }
    public static TheoryData<string> Visualizations => new(CaseRows.Select(c => c.Vis));
    public static TheoryData<string, string> VisualizationsAndSolvers => new(CaseRows.Select(c => (c.Vis, c.Solver)));

    // Every visualization and solver touched by the groups shape and the step recording.
    public static TheoryData<string> TouchedInterfaces => new(CaseRows.Select(c => c.Vis)
        .Concat(["graphcoloringbruteforce", "graphcoloringgreedy", "cliquecoverbruteforce"]));

    private static API.Interfaces.IProblem ProblemOf(string vis) =>
        (API.Interfaces.IProblem)Activator.CreateInstance(ProblemProvider.Problems[vis.Replace("defaultvisualization", "")])!;

    private static string DefaultInstance(string vis) => ProblemOf(vis).defaultInstance;

    [Theory]
    [MemberData(nameof(TouchedInterfaces))]
    public async Task Info_IsUnchangedByTheGroupsShape(string name) {
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

    [Fact]
    public async Task List_WithTheGreedySolver_HasNoStepItems() {
        var (status, body) = await Visualize($"visualization={ColoringVis}&solver=graphcoloringgreedy", ReadSnapshot($"{ColoringVis}.instance.txt"));
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(2, JsonDocument.Parse(body).RootElement.GetArrayLength());
    }

    // ---- frames ----

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Frames_DefaultSolver_HasGraphTypePayloadAndFrames(string vis, string solver, string rule) {
        string instance = DefaultInstance(vis);
        var problem = ProblemOf(vis);
        var (status, body) = await Visualize($"visualization={vis}&format=frames&solver={solver}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var root = JsonDocument.Parse(body).RootElement;

        Assert.Equal("Graph", root.GetProperty("type").GetString());
        var payload = root.GetProperty("payload");
        Assert.Equal("graph", payload.GetProperty("kind").GetString());
        Assert.Equal("undirected", payload.GetProperty("subtype").GetString());
        Assert.Equal("force", payload.GetProperty("layout").GetString());
        Assert.Equal("groups", payload.GetProperty("shape").GetString());
        Assert.Equal(rule, payload.GetProperty("rule").GetString());
        Assert.Equal(3, payload.GetProperty("k").GetInt32());   // both default instances ask for 3

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
            Assert.False(e.TryGetProperty("weight", out _));
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
            foreach (var p in f.GetProperty("phantoms").EnumerateArray()) {
                Assert.Contains(p.GetProperty("a").GetString()!, nodeIds);
                Assert.Contains(p.GetProperty("b").GetString()!, nodeIds);
                Assert.Equal("Rejected", p.GetProperty("state").GetString());
            }
            if (rule == "coloring") Assert.Equal(0, f.GetProperty("phantoms").GetArrayLength());
            bool isDone = f.GetProperty("event").GetString() == "Done";
            Assert.Equal(isDone, f.TryGetProperty("ok", out _));
            foreach (var g in f.GetProperty("groups").EnumerateObject()) {
                Assert.Contains(g.Name, nodeIds);
                Assert.InRange(g.Value.GetInt32(), 0, 2);
            }
        }

        // Exactly one Done, and it is last; its ok agrees with the problem's verifier on the groups it shows.
        Assert.Single(frames, f => f.GetProperty("event").GetString() == "Done");
        var last = frames[^1];
        Assert.Equal("Done", last.GetProperty("event").GetString());
        Assert.True(last.GetProperty("ok").GetBoolean());
        var groups = last.GetProperty("groups").EnumerateObject().ToDictionary(g => g.Name, g => g.Value.GetInt32());
        Assert.Equal(nodeIds.Count, groups.Count);
        Assert.True(problem.defaultVerifier.verify(instance, Certificate(groups)), Certificate(groups));
    }

    // The classes as a certificate, written here from the frame's groups (not by the code under test).
    private static string Certificate(IReadOnlyDictionary<string, int> groups) =>
        "{" + string.Join(",", Enumerable.Range(0, groups.Count == 0 ? 0 : groups.Values.Max() + 1)
            .Select(g => "{" + string.Join(",", groups.Where(p => p.Value == g).Select(p => p.Key)) + "}")) + "}";

    private static List<JsonElement> Frames(string body) => JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().ToList();

    private static Dictionary<string, string> States(JsonElement frame, string collection) =>
        frame.GetProperty(collection).EnumerateObject().ToDictionary(e => e.Name, e => e.Value.GetProperty("state").GetString()!);

    [Fact]
    public async Task Frames_Coloring_BruteForce_FirstFrameIsAClash_LastIsTheMockupsColoring() {
        var (_, body) = await Visualize($"visualization={ColoringVis}&format=frames", DefaultInstance(ColoringVis));
        var frames = Frames(body);
        // First frame: every node in color 0, so the first edge of the instance clashes.
        var first = frames[0];
        Assert.Equal("Reject", first.GetProperty("event").GetString());
        Assert.Equal("Try {a,b,c,d,e,f,g,h,i},{},{}: a and b are both color 0.", first.GetProperty("caption").GetString());
        Assert.Equal("Rejected", States(first, "edges")["a-b"]);
        Assert.Equal("Rejected", States(first, "nodes")["a"]);   // in a Reject step the focus is what failed
        Assert.Equal(9, first.GetProperty("groups").EnumerateObject().Count());
        // Last frame: a solid coloring. No clash, no state on any node (group colors are the answer), nothing rejected.
        var last = frames[^1];
        Assert.Equal("Done", last.GetProperty("event").GetString());
        Assert.True(last.GetProperty("ok").GetBoolean());
        Assert.Empty(last.GetProperty("edges").EnumerateObject());
        Assert.Empty(last.GetProperty("nodes").EnumerateObject());
        var groups = last.GetProperty("groups").EnumerateObject().ToDictionary(g => g.Name, g => g.Value.GetInt32());
        // a is alone in its group; its neighbours share the other two (the instance is a "windmill" of triangles and edges).
        Assert.Equal(3, groups.Values.Distinct().Count());
        Assert.DoesNotContain(groups.Where(p => p.Key != "a"), p => p.Value == groups["a"]);
        Assert.Equal(groups["b"], groups["d"]);
        Assert.NotEqual(groups["b"], groups["c"]);
    }

    [Fact]
    public async Task Frames_Coloring_Greedy_ColorsOneNodePerFrame_InOrder() {
        var (status, body) = await Visualize($"visualization={ColoringVis}&format=frames&solver=graphcoloringgreedy", DefaultInstance(ColoringVis));
        Assert.Equal(HttpStatusCode.OK, status);
        var frames = Frames(body);
        Assert.Equal(10, frames.Count);   // nine nodes, then Done
        Assert.Equal("Accept", frames[0].GetProperty("event").GetString());
        Assert.Equal("Color node a with color 0: no neighbour is colored yet.", frames[0].GetProperty("caption").GetString());
        Assert.Equal("ElementHighlight", States(frames[0], "nodes")["a"]);
        // Each frame adds exactly one node to the groups, and the node being colored is the highlighted one.
        for (int i = 0; i < 9; i++) {
            Assert.Equal(i + 1, frames[i].GetProperty("groups").EnumerateObject().Count());
            string node = ((char)('a' + i)).ToString();
            Assert.Equal(node, frames[i].GetProperty("focus")[0].GetString());
            Assert.True(frames[i].GetProperty("groups").TryGetProperty(node, out _));
            Assert.Equal("ElementHighlight", States(frames[i], "nodes")[node]);
            Assert.Empty(States(frames[i], "edges"));   // greedy never colors a clash
        }
        var last = frames[^1];
        Assert.Equal("Done", last.GetProperty("event").GetString());
        Assert.True(last.GetProperty("ok").GetBoolean());
        Assert.Equal(0, last.GetProperty("groups").GetProperty("a").GetInt32());
        // Same picture as brute force: same payload.
        var (_, brute) = await Visualize($"visualization={ColoringVis}&format=frames", DefaultInstance(ColoringVis));
        Assert.Equal(JsonDocument.Parse(brute).RootElement.GetProperty("payload").GetRawText(), JsonDocument.Parse(body).RootElement.GetProperty("payload").GetRawText());
        Assert.NotEqual(brute, body);
    }

    [Fact]
    public async Task Frames_Coloring_GreedyThatNeedsTooManyColors_EndsNotOkWithNothingColored() {
        // A triangle cannot be colored with 2 colors.
        var (status, body) = await Visualize($"visualization={ColoringVis}&format=frames&solver=graphcoloringgreedy", "(({1,2,3},{{1,2},{1,3},{2,3}}),2)");
        Assert.Equal(HttpStatusCode.OK, status);
        var frames = Frames(body);
        Assert.Equal(["Accept", "Accept", "Reject", "Done"], frames.Select(f => f.GetProperty("event").GetString()));
        // The third node needs color 2, which is not allowed: it is Rejected, and its clash-free color is still shown.
        Assert.Equal("Rejected", States(frames[2], "nodes")["3"]);
        Assert.Equal(2, frames[2].GetProperty("groups").GetProperty("3").GetInt32());
        var last = frames[^1];
        Assert.False(last.GetProperty("ok").GetBoolean());
        Assert.Empty(last.GetProperty("groups").EnumerateObject());
        Assert.Empty(last.GetProperty("nodes").EnumerateObject());
        Assert.Contains("more than K = 2", last.GetProperty("caption").GetString());
    }

    [Fact]
    public async Task Frames_Coloring_ANodeWithNoColorYetIsLeftOutOfGroups() {
        // K = 1: brute force also tries candidates in which a node has no color at all.
        var (_, body) = await Visualize($"visualization={ColoringVis}&format=frames&solver=graphcoloringbruteforce", "(({1,2,3},{{1,2}}),1)");
        var frames = Frames(body);
        var uncolored = frames.First(f => f.GetProperty("caption").GetString()!.EndsWith("1 has no color."));
        Assert.False(uncolored.GetProperty("groups").TryGetProperty("1", out _));
        Assert.Equal("Rejected", States(uncolored, "nodes")["1"]);
    }

    [Fact]
    public async Task Frames_CliqueCover_ShowsTheCliquesSolid_AndAMissingEdgeAsAPhantom() {
        var (_, body) = await Visualize($"visualization={CoverVis}&format=frames", DefaultInstance(CoverVis));
        var frames = Frames(body);
        // Some candidate puts two unjoined nodes in one group: a Rejected phantom, and the caption names the pair.
        var withPhantom = frames.First(f => f.GetProperty("phantoms").GetArrayLength() > 0);
        Assert.Equal("Reject", withPhantom.GetProperty("event").GetString());
        Assert.Matches(@"^Try \{[^}]*\},\{[^}]*\},\{[^}]*\}: \S+ and \S+ are in one group but aren't joined\.$", withPhantom.GetProperty("caption").GetString());
        Assert.All(withPhantom.GetProperty("phantoms").EnumerateArray(), p => Assert.Equal("Rejected", p.GetProperty("state").GetString()));
        // The solved frame: three cliques, every edge inside a group is Solution, no phantom, no node state.
        var last = frames[^1];
        Assert.Equal("Done", last.GetProperty("event").GetString());
        Assert.True(last.GetProperty("ok").GetBoolean());
        Assert.Equal(0, last.GetProperty("phantoms").GetArrayLength());
        Assert.Empty(last.GetProperty("nodes").EnumerateObject());
        var solution = States(last, "edges");
        Assert.All(solution.Values, s => Assert.Equal("Solution", s));
        Assert.Equal(["1-2", "1-3", "2-3", "4-5", "6-7", "6-8", "7-8"], solution.Keys.Order());   // 2-4 and 3-5 join two groups: plain
        var groups = last.GetProperty("groups").EnumerateObject().ToDictionary(g => g.Name, g => g.Value.GetInt32());
        Assert.Equal(3, groups.Values.Distinct().Count());
        Assert.Equal(groups["1"], groups["3"]);
        Assert.Equal(groups["4"], groups["5"]);
        Assert.Equal(groups["6"], groups["8"]);
    }

    [Theory]
    [InlineData(CoverVis, "cliquecoverbruteforce", "(({1,2,3},{{1,2}}),4)")]
    [InlineData(CoverVis, "cliquecoverbruteforce", "(({1,2,3,4},{{1,2},{3,4}}),1)")]
    [InlineData(ColoringVis, "graphcoloringbruteforce", "(({1,2,3},{{1,2},{1,3},{2,3}}),2)")]
    [InlineData(ColoringVis, "graphcoloringgreedy", "(({1,2,3},{{1,2},{1,3},{2,3}}),2)")]
    public async Task Frames_WhenThereIsNoAnswer_EndsNotOkWithNoGroups(string vis, string solver, string instance) {
        var (status, body) = await Visualize($"visualization={vis}&format=frames&solver={solver}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var last = Frames(body).Last();
        Assert.Equal("Done", last.GetProperty("event").GetString());
        Assert.False(last.GetProperty("ok").GetBoolean());
        Assert.Empty(last.GetProperty("groups").EnumerateObject());
        Assert.Empty(last.GetProperty("edges").EnumerateObject());
        Assert.Empty(last.GetProperty("nodes").EnumerateObject());
    }

    [Theory]
    [InlineData(ColoringVis, "graphcoloringbruteforce")]
    [InlineData(ColoringVis, "graphcoloringgreedy")]
    [InlineData(CoverVis, "cliquecoverbruteforce")]
    public async Task Frames_EveryCompatibleSolver_EndsInAVerifiedDoneFrame(string vis, string solver) {
        var instance = ReadSnapshot($"{vis}.instance.txt");
        var (status, body) = await Visualize($"visualization={vis}&format=frames&solver={solver}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var frames = Frames(body);
        Assert.True(frames.Count >= 2);
        Assert.Single(frames, f => f.GetProperty("event").GetString() == "Done");
        Assert.Equal("Done", frames[^1].GetProperty("event").GetString());
        Assert.True(frames[^1].GetProperty("ok").GetBoolean());
        var groups = frames[^1].GetProperty("groups").EnumerateObject().ToDictionary(g => g.Name, g => g.Value.GetInt32());
        Assert.True(ProblemOf(vis).defaultVerifier.verify(instance, Certificate(groups)));
    }

    [Fact]
    public async Task Frames_StepsAreCappedAndSayHowManyAreHidden() {
        // K6 with K = 3: all 4^6 candidates fail, far more than the cap of 150.
        const string instance = "(({1,2,3,4,5,6},{{1,2},{1,3},{1,4},{1,5},{1,6},{2,3},{2,4},{2,5},{2,6},{3,4},{3,5},{3,6},{4,5},{4,6},{5,6}}),3)";
        var (status, body) = await Visualize($"visualization={ColoringVis}&format=frames&solver=graphcoloringbruteforce", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var frames = Frames(body);
        Assert.True(frames.Count <= 151, $"{frames.Count} frames");
        Assert.Equal("Done", frames[^1].GetProperty("event").GetString());
        Assert.Contains("not shown", frames[^1].GetProperty("caption").GetString());
    }
}
