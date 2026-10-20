using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Nested Set picture of Set Cover, Exact Cover and Hitting Set (#926) on the actual endpoint output:
// /ProblemProvider/visualize?format=frames with every solver, and visualizeRun. It is a second view of the Bipartite Graph
// without the membership edges, so most checks compare it with that picture of the same run: the same ids, node states and
// hits on every frame. The three visualizations are new, so the guard for old frontends is that their list format is exactly
// the default pictures', that /info shows nothing of the frames plumbing, and that the problems' defaults are unchanged.
public class NestedSetFrames_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public NestedSetFrames_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private static string ReadSnapshot(string file) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Endpoints", "Snapshots", file)).Replace("\r\n", "\n");

    private async Task<(HttpStatusCode Status, string Body)> Post(string path, string query, string instance) {
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/{path}?{query}", content, TestContext.Current.CancellationToken);
        return (response.StatusCode, (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Replace("\r\n", "\n"));
    }

    private async Task<JsonElement> Json(string path, string query, string instance) {
        var (status, body) = await Post(path, query, instance);
        Assert.True(status == HttpStatusCode.OK, body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static List<JsonElement> Frames(JsonElement response) => response.GetProperty("frames").EnumerateArray().ToList();

    private static Dictionary<string, string> States(JsonElement frame) =>
        frame.GetProperty("nodes").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetProperty("state").GetString()!);

    private static readonly HashSet<string> Vocabulary = new() {
        "Background", "ElementHighlight", "Solution", "Rejected", "Untraveled", "Covered", "Blocked", "Committed",
    };

    // the Nested Set, the Bipartite picture of the same problem, its display name
    private static readonly (string Vis, string Bipartite, string Name, string Pick, string Rule)[] Pictures = {
        ("setcovernestedsetvisualization", "setcoverdefaultvisualization", "Set Cover Nested Sets", "sets", "atLeastOnce"),
        ("exactcovernestedsetvisualization", "exactcoverdefaultvisualization", "Exact Cover Nested Sets", "sets", "exactlyOnce"),
        ("hittingsetnestedsetvisualization", "hittingsetdefaultvisualization", "Hitting Set Nested Sets", "elements", "exactlyOnce"),
    };

    // Every solver of each problem.
    private static readonly (string Vis, string Solver)[] SolverRows = {
        ("setcovernestedsetvisualization", "setcoverbruteforce"),
        ("setcovernestedsetvisualization", "greedysetcover"),
        ("setcovernestedsetvisualization", "heuristicsolver"),
        ("exactcovernestedsetvisualization", "exactcoverbruteforce"),
        ("exactcovernestedsetvisualization", "dancinglinks"),
        ("exactcovernestedsetvisualization", "exactcoverrecursive"),
        ("hittingsetnestedsetvisualization", "hittingsetbruteforce"),
    };

    public static TheoryData<string> Visualizations => new(Pictures.Select(p => p.Vis));

    public static TheoryData<string, string> Solvers {
        get {
            var data = new TheoryData<string, string>();
            foreach (var r in SolverRows) data.Add(r.Vis, r.Solver);
            return data;
        }
    }

    private static (string Vis, string Bipartite, string Name, string Pick, string Rule) Row(string vis) => Pictures.Single(p => p.Vis == vis);

    private static string Instance(string vis) => ReadSnapshot($"{Row(vis).Bipartite}.instance.txt");

    // ---- What old clients read ----

    [Theory]
    [MemberData(nameof(Solvers))]
    public async Task List_IsExactlyTheBipartitePicturesList_WhicheverSolverIsChosen(string vis, string solver) {
        string instance = Instance(vis);
        var (_, expected) = await Post("visualize", $"visualization={Row(vis).Bipartite}&solver={solver}", instance);
        var (status, body) = await Post("visualize", $"visualization={vis}&solver={solver}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(expected, body);
    }

    [Theory]
    [MemberData(nameof(Visualizations))]
    public async Task Info_ReportsTheLegacyType_AndNoFramesPlumbing(string vis) {
        var json = JsonDocument.Parse(await _client.GetStringAsync($"/ProblemProvider/info?interface={vis}", TestContext.Current.CancellationToken)).RootElement;
        Assert.Equal(Row(vis).Name, json.GetProperty("visualizationName").GetString());
        Assert.Equal("SetD3", json.GetProperty("visualizationType").GetString());
        Assert.Equal(["visualizationName", "visualizationDefinition", "source", "sourceFile", "contributors", "visualizationType", "solver"],
            json.EnumerateObject().Select(p => p.Name));
        // Its own solver is the Bipartite picture's.
        var bipartite = JsonDocument.Parse(await _client.GetStringAsync($"/ProblemProvider/info?interface={Row(vis).Bipartite}", TestContext.Current.CancellationToken)).RootElement;
        Assert.Equal(bipartite.GetProperty("solver").GetRawText(), json.GetProperty("solver").GetRawText());
    }

    [Theory]
    [InlineData("SETCOVER", "SetCoverDefaultVisualization", "SetCoverNestedSetVisualization")]
    [InlineData("EXACTCOVER", "ExactCoverDefaultVisualization", "ExactCoverNestedSetVisualization")]
    [InlineData("HITTINGSET", "HittingSetDefaultVisualization", "HittingSetNestedSetVisualization")]
    public async Task TheProblemsDefault_IsUnchanged_AndTheNestedSetIsListedBesideIt(string problem, string defaultVis, string vis) {
        var p = (IProblem)Activator.CreateInstance(ProblemProvider.Problems[problem.ToLower()])!;
        Assert.Equal(defaultVis, p.defaultVisualization.GetType().Name);
        var listed = JsonDocument.Parse(await _client.GetStringAsync($"/Navigation/Problem_VisualizationsRefactor?chosenProblem={problem}", TestContext.Current.CancellationToken))
            .RootElement.EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains(defaultVis, listed);
        Assert.Contains(vis, listed);
    }

    // ---- format=frames ----

    [Theory]
    [MemberData(nameof(Solvers))]
    public async Task Frames_EverySolver_AreTheBipartiteFramesWithoutEdges(string vis, string solver) {
        string instance = Instance(vis);
        var root = await Json("visualize", $"visualization={vis}&format=frames&solver={solver}", instance);
        var graph = await Json("visualize", $"visualization={Row(vis).Bipartite}&format=frames&solver={solver}", instance);
        Assert.Equal("NestedSet", root.GetProperty("type").GetString());

        var payload = root.GetProperty("payload");
        bool hasK = graph.GetProperty("payload").TryGetProperty("k", out var k);
        Assert.Equal(["kind", "layout", "pickSide", "rule", "elements", "sets", .. hasK ? new[] { "k" } : []], payload.EnumerateObject().Select(p => p.Name));
        Assert.Equal(("nestedSet", "regions", Row(vis).Pick, Row(vis).Rule),
            (payload.GetProperty("kind").GetString(), payload.GetProperty("layout").GetString(), payload.GetProperty("pickSide").GetString(), payload.GetProperty("rule").GetString()));
        if (hasK) Assert.Equal(k.GetInt32(), payload.GetProperty("k").GetInt32());
        Assert.DoesNotContain("\"state\"", payload.GetRawText());
        // The Bipartite picture's elements and sets exactly (ids e:{x} and S{i+1}, labels, members), and none of its edges.
        foreach (var key in new[] { "elements", "sets" })
            Assert.Equal(graph.GetProperty("payload").GetProperty(key).GetRawText(), payload.GetProperty(key).GetRawText());
        var ids = payload.GetProperty("elements").EnumerateArray().Concat(payload.GetProperty("sets").EnumerateArray())
            .Select(e => e.GetProperty("id").GetString()!).ToHashSet();

        var frames = Frames(root);
        var graphFrames = Frames(graph);
        Assert.Equal(graphFrames.Count, frames.Count);
        for (int i = 0; i < frames.Count; i++) {
            var f = frames[i];
            bool isDone = f.GetProperty("event").GetString() == "Done";
            Assert.Equal(["event", "caption", "focus", "nodes", "hits", "step", .. isDone ? new[] { "ok" } : []], f.EnumerateObject().Select(p => p.Name));
            foreach (var key in new[] { "event", "caption", "focus", "nodes", "hits", "step" })
                Assert.Equal(graphFrames[i].GetProperty(key).GetRawText(), f.GetProperty(key).GetRawText());
            if (isDone) Assert.Equal(graphFrames[i].GetProperty("ok").GetBoolean(), f.GetProperty("ok").GetBoolean());
            foreach (var (id, state) in States(f)) {
                Assert.Contains(id, ids);
                Assert.Contains(state, Vocabulary);
            }
            foreach (var hit in f.GetProperty("hits").EnumerateObject()) Assert.Contains(hit.Name, ids);
        }
        // Solution only on the confirmed answer, and the default instances all have one.
        var last = frames[^1];
        Assert.True(last.GetProperty("ok").GetBoolean());
        Assert.Contains("Solution", States(last).Values);
        Assert.DoesNotContain(frames.Take(frames.Count - 2), f => States(f).ContainsValue("Solution"));
    }

    [Fact]
    public async Task Frames_ExactCover_BlocksTheSetsThatWouldDoubleUp() {
        // ({1,2,3,4},{{1,2,3},{2,3},{4,1}}): the answer {S2,S3} blocks S1, which shares 2 and 3 with S2.
        var last = Frames(await Json("visualize", "visualization=exactcovernestedsetvisualization&format=frames", Instance("exactcovernestedsetvisualization")))[^1];
        var nodes = States(last);
        Assert.Equal(("Solution", "Solution", "Blocked"), (nodes["S2"], nodes["S3"], nodes["S1"]));
        Assert.All(new[] { "e:1", "e:2", "e:3", "e:4" }, e => Assert.Equal("Covered", nodes[e]));
    }

    [Fact]
    public async Task Frames_HittingSet_ASetHitTwice_IsRejected_WithItsCountInHits() {
        // ({1,2,3,4},{{1,2},{3,4},{1,3}}): no single element hits all three sets, and {1,2} hits S1 = {1,2} twice.
        var frames = Frames(await Json("visualize", "visualization=hittingsetnestedsetvisualization&format=frames", "({1,2,3,4},{{1,2},{3,4},{1,3}})"));
        var twice = frames.First(f => f.GetProperty("hits").EnumerateObject().Any(h => h.Value.GetInt32() > 1));
        Assert.Equal(2, twice.GetProperty("hits").GetProperty("S1").GetInt32());
        Assert.Equal("Rejected", States(twice)["S1"]);
        Assert.True(frames[^1].GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task Frames_SetCoverGreedyOverK_RejectsTheSetsPastK_AndEndsOnABlankNotOkDone() {
        // Greedy takes S1 = {1,2,3,4} first and then needs both S2 and S3 for 5 and 6: three sets, K = 2 (S2 and S3 alone cover
        // U). Its Reject says so, with the set past K Rejected; its Done carries no picks, so it is blank apart from its caption.
        var frames = Frames(await Json("visualize", "visualization=setcovernestedsetvisualization&format=frames&solver=greedysetcover",
            "({1,2,3,4,5,6},{{1,2,3,4},{1,3,5},{2,4,6}},2)"));
        var reject = frames[^2];
        Assert.Equal("Reject", reject.GetProperty("event").GetString());
        Assert.Equal("Rejected", States(reject)["S3"]);
        var last = frames[^1];
        Assert.Equal("Done", last.GetProperty("event").GetString());
        Assert.False(last.GetProperty("ok").GetBoolean());
        Assert.Empty(last.GetProperty("nodes").EnumerateObject());
        Assert.DoesNotContain(frames, f => States(f).ContainsValue("Solution"));
    }

    [Theory]
    [InlineData("exactcovernestedsetvisualization", "exactcoverbruteforce", "({1,2,3},{{1,2},{2,3}})")]
    [InlineData("exactcovernestedsetvisualization", "dancinglinks", "({1,2,3},{{1,2},{2,3}})")]
    [InlineData("hittingsetnestedsetvisualization", "hittingsetbruteforce", "({1,2,3},{{1,2},{2,3},{1,3}})")]
    public async Task Frames_AnEmptyFailedAnswer_EndsNotOk_AndBlankApartFromItsCaption(string vis, string solver, string instance) {
        var last = Frames(await Json("visualize", $"visualization={vis}&format=frames&solver={solver}", instance))[^1];
        Assert.Equal("Done", last.GetProperty("event").GetString());
        Assert.False(last.GetProperty("ok").GetBoolean());
        Assert.NotEqual("", last.GetProperty("caption").GetString());
        Assert.Empty(last.GetProperty("nodes").EnumerateObject());
        Assert.Empty(last.GetProperty("hits").EnumerateObject());
    }

    // ---- One run, every picture ----

    [Theory]
    [MemberData(nameof(Solvers))]
    public async Task VisualizeRun_DrawsTheNestedSet_FromTheSameRunAsTheBipartiteGraph(string vis, string solver) {
        var body = await Json("visualizeRun", $"solver={solver}", Instance(vis));
        var pictures = body.GetProperty("pictures").EnumerateArray().ToDictionary(p => p.GetProperty("visualization").GetString()!.ToLower());
        var bipartite = Row(vis).Bipartite;
        // The default comes first; the Nested Set is an extra one.
        Assert.Equal(bipartite, body.GetProperty("pictures")[0].GetProperty("visualization").GetString()!.ToLower());
        Assert.Equal("NestedSet", pictures[vis].GetProperty("type").GetString());
        var nested = Frames(pictures[vis]);
        var graph = Frames(pictures[bipartite]);
        Assert.Equal(graph.Count, nested.Count);
        for (int i = 0; i < nested.Count; i++)
            foreach (var key in new[] { "step", "event", "nodes", "hits" })
                Assert.Equal(graph[i].GetProperty(key).GetRawText(), nested[i].GetProperty(key).GetRawText());
    }
}

[Collection(TimingCollection.Name)]
public class NestedSetFrames_StoppedEndpoint_Tests : IClassFixture<TinyBudgetAppFactory> {
    private readonly HttpClient _client;

    public NestedSetFrames_StoppedEndpoint_Tests(TinyBudgetAppFactory factory) {
        _client = factory.CreateClient();
    }

    // 2^40 combinations of the sets {1}, ..., {40}, none an exact cover (z is in no set).
    private static readonly string SlowExactCover =
        "({" + string.Join(",", Enumerable.Range(1, 40)) + ",z},{" + string.Join(",", Enumerable.Range(1, 40).Select(i => $"{{{i}}}")) + "})";

    [Fact]
    public async Task Frames_StoppedSolve_EndsWithABlankNotOkDoneFrame() {
        var sw = Stopwatch.StartNew();
        var content = new StringContent(JsonSerializer.Serialize(SlowExactCover), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/visualize?visualization=exactcovernestedsetvisualization&format=frames&solver=exactcoverbruteforce", content, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), $"took {sw.Elapsed}");
        var frames = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().ToList();
        var done = frames[^1];
        Assert.Equal("Done", done.GetProperty("event").GetString());
        Assert.False(done.GetProperty("ok").GetBoolean());
        Assert.StartsWith("Stopped after ", done.GetProperty("caption").GetString());
        Assert.Empty(done.GetProperty("nodes").EnumerateObject());
        Assert.Empty(done.GetProperty("hits").EnumerateObject());
        Assert.Equal(frames[0].EnumerateObject().Select(p => p.Name).Append("ok"), done.EnumerateObject().Select(p => p.Name));
    }
}
