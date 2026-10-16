using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Traveling Salesperson's search tree, a second picture of its branch and bound, on the actual
// /ProblemProvider/visualize?format=frames output. The visualization is new, so the guard for old frontends is that its list
// format is exactly the default TSP picture's for the same solver, that /info shows nothing of the frames plumbing, and that
// /info for the solver and the default picture is what it was (info.<name>.json).
public class SearchTreeTsp_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public SearchTreeTsp_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private const string Vis = "tspsearchtreevisualization";
    private const string Solver = "tspbranchandbound";

    private async Task<(HttpStatusCode Status, string Body)> Visualize(string query, string instance) {
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/visualize?{query}", content, TestContext.Current.CancellationToken);
        return (response.StatusCode, (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Replace("\r\n", "\n"));
    }

    private static string ReadSnapshot(string file) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Endpoints", "Snapshots", file)).Replace("\r\n", "\n");

    private static string Readable(JsonElement element) =>
        JsonSerializer.Serialize(element, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    public static TheoryData<string> Instances {
        get {
            var data = new TheoryData<string>();
            foreach (var row in SearchTreeTsp_Tests.Snapshot) data.Add(row.Data.Item2);
            return data;
        }
    }

    public static TheoryData<string> TouchedInterfaces => new([Solver, "tspdefaultvisualization"]);

    // ---- What old frontends read must not change. ----

    [Theory]
    [MemberData(nameof(TouchedInterfaces))]
    public async Task Info_IsUnchangedByTheSearchTree(string name) {
        var body = await _client.GetStringAsync($"/ProblemProvider/info?interface={name}", TestContext.Current.CancellationToken);
        Assert.Equal(ReadSnapshot($"info.{name}.json"), body.Replace("\r\n", "\n"));
        Assert.DoesNotContain("SearchTree", InfoJson.WithoutPictureType(body));
    }

    [Fact]
    public async Task Info_ReportsTheLegacyType_AndNoFramesPlumbing() {
        var body = await _client.GetStringAsync($"/ProblemProvider/info?interface={Vis}", TestContext.Current.CancellationToken);
        var json = JsonDocument.Parse(body).RootElement;
        Assert.Equal("Traveling Salesperson Search Tree", json.GetProperty("visualizationName").GetString());
        Assert.Equal("GraphD3", json.GetProperty("visualizationType").GetString());
        Assert.Equal(["visualizationName", "visualizationDefinition", "source", "sourceFile", "contributors", "visualizationType", "pictureType", "solver"],
            json.EnumerateObject().Select(p => p.Name));
        Assert.Equal("Traveling Salesperson Branch and Bound", json.GetProperty("solver").GetProperty("solverName").GetString());
        // The problem still draws its default picture.
        var problem = await _client.GetStringAsync("/ProblemProvider/info?interface=tsp", TestContext.Current.CancellationToken);
        Assert.Equal("Traveling Salesperson Visualization", JsonDocument.Parse(problem).RootElement.GetProperty("defaultVisualization").GetProperty("visualizationName").GetString());
    }

    [Fact]
    public async Task List_IsExactlyTheDefaultPicturesList_ForTheSameSolver() {
        string instance = new API.Problems.NPComplete.NPC_TSP.TSP().defaultInstance;
        var (status, body) = await Visualize($"visualization={Vis}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var (_, expected) = await Visualize($"visualization=tspdefaultvisualization&solver={Solver}", instance);
        Assert.Equal(expected, body);
        var (_, withSolver) = await Visualize($"visualization={Vis}&solver={Solver}&format=list", instance);
        Assert.Equal(expected, withSolver);
    }

    // ---- The picture ----

    [Theory]
    [MemberData(nameof(Instances))]
    public async Task Frames_HaveTheSearchTreePayload_AndAWellFormedTreeInEveryFrame(string instance) {
        var (status, body) = await Visualize($"visualization={Vis}&format=frames", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var frames = SearchTreeJson.Check(body, "Traveling Salesperson", instance);
        var done = frames[^1];
        // The same run as the tour picture: same events and captions.
        var (_, tourBody) = await Visualize($"visualization=tspdefaultvisualization&format=frames&solver={Solver}", instance);
        var tourFrames = JsonDocument.Parse(tourBody).RootElement.GetProperty("frames").EnumerateArray().ToList();
        Assert.Equal(tourFrames.Select(f => (f.GetProperty("event").GetString(), f.GetProperty("caption").GetString())),
            frames.Select(f => (f.GetProperty("event").GetString(), f.GetProperty("caption").GetString())));
        Assert.All(frames.SkipLast(1), f => Assert.Single(f.GetProperty("focus").EnumerateArray()));
        // Branch and bound queues: the root and every path it expands get an order badge (the Done step's path past the cap
        // may name nodes whose steps were not kept, which have none; the greedy tour's leaf, once beaten, was never queued).
        Assert.All(frames.SkipLast(1).SelectMany(f => f.GetProperty("nodes").EnumerateArray())
                .Where(n => SearchTreeJson.Kind(n) == "explored" && n.GetProperty("label").GetString() != "greedy tour"),
            n => Assert.True(n.TryGetProperty("order", out _)));

        // Done: ok when there is an answer within K. The green path spells the answer when the search beat the greedy tour, and
        // ends on the greedy tour's leaf when it did not (#897).
        string answer = ((ISolver)Activator.CreateInstance(ProblemProvider.Solvers[Solver])!).solve(instance);
        bool ok = tourFrames[^1].GetProperty("ok").GetBoolean();
        Assert.Equal(ok, done.GetProperty("ok").GetBoolean());
        var green = SearchTreeJson.GreenPath(done);
        if (!ok) {
            Assert.Empty(green);
            return;
        }
        Assert.NotEmpty(green);
        Assert.Equal("solution", SearchTreeJson.Kind(green[^1]));
        if (green[^1].GetProperty("label").GetString() == "greedy tour") {
            Assert.Equal(2, green.Count);
            Assert.StartsWith("The cheapest tour is the greedy tour ", done.GetProperty("caption").GetString());
            return;
        }
        if (green.Any(n => SearchTreeJson.Kind(n) == "hidden")) return;
        var cities = answer.Trim('{', '}').Split(',').SkipLast(1);
        Assert.Equal(cities, green.Select(n => n.GetProperty("label").GetString()));
    }

    [Fact]
    public async Task Frames_GreedyIsNotOptimal_MatchesTheMockup() {
        // The mockup's "Greedy isn't optimal" example: the greedy tour costs 31, the search finds a → c → b → d → e → a at 27
        // and then drops every queued path whose bound has reached 27.
        var (_, body) = await Visualize($"visualization={Vis}&format=frames",
            "(({a,b,c,d,e},{({a,b},3),({a,c},3),({a,d},5),({a,e},8),({b,c},3),({b,d},4),({b,e},10),({c,d},8),({c,e},12),({d,e},9)}),100)");
        var frames = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().ToList();
        Assert.Equal(24, frames.Count);
        Assert.Equal(
            "{\"event\":\"Done\",\"caption\":\"The cheapest tour is a → c → b → d → e → a, total 27, within K = 100.\",\"focus\":[],\"nodes\":["
            + "{\"id\":\"n0\",\"parent\":null,\"label\":\"a\",\"kind\":\"explored\",\"state\":\"Solution\",\"bound\":\"lb 27\",\"order\":1},"
            + "{\"id\":\"n1\",\"parent\":\"n0\",\"label\":\"greedy tour\",\"kind\":\"explored\",\"state\":\"Covered\",\"bound\":\"tour 31\"},"
            + "{\"id\":\"n2\",\"parent\":\"n0\",\"label\":\"b\",\"kind\":\"explored\",\"state\":\"Covered\",\"bound\":\"lb 27\",\"order\":2},"
            + "{\"id\":\"n6\",\"parent\":\"n2\",\"label\":\"c\",\"kind\":\"pruned\",\"state\":\"Rejected\",\"bound\":\"lb 31 ≥ 31\"},"
            + "{\"id\":\"n7\",\"parent\":\"n2\",\"label\":\"d\",\"kind\":\"pruned\",\"state\":\"Rejected\",\"bound\":\"lb 31 ≥ 31\"},"
            + "{\"id\":\"n8\",\"parent\":\"n2\",\"label\":\"e\",\"kind\":\"pruned\",\"state\":\"Rejected\",\"bound\":\"lb 33 ≥ 31\"},"
            + "{\"id\":\"n3\",\"parent\":\"n0\",\"label\":\"c\",\"kind\":\"explored\",\"state\":\"Solution\",\"bound\":\"lb 27\",\"order\":3},"
            + "{\"id\":\"n9\",\"parent\":\"n3\",\"label\":\"b\",\"kind\":\"explored\",\"state\":\"Solution\",\"bound\":\"lb 27\",\"order\":4},"
            + "{\"id\":\"n12\",\"parent\":\"n9\",\"label\":\"d\",\"kind\":\"explored\",\"state\":\"Solution\",\"bound\":\"lb 27\",\"order\":5},"
            + "{\"id\":\"n14\",\"parent\":\"n12\",\"label\":\"e\",\"kind\":\"solution\",\"state\":\"Solution\",\"bound\":\"tour 27\",\"order\":6},"
            + "{\"id\":\"n13\",\"parent\":\"n9\",\"label\":\"e\",\"kind\":\"pruned\",\"state\":\"Rejected\",\"bound\":\"lb 30 ≥ 27\"},"
            + "{\"id\":\"n10\",\"parent\":\"n3\",\"label\":\"d\",\"kind\":\"pruned\",\"state\":\"Rejected\",\"bound\":\"lb 31 ≥ 31\"},"
            + "{\"id\":\"n11\",\"parent\":\"n3\",\"label\":\"e\",\"kind\":\"pruned\",\"state\":\"Rejected\",\"bound\":\"lb 31 ≥ 31\"},"
            + "{\"id\":\"n4\",\"parent\":\"n0\",\"label\":\"d\",\"kind\":\"pruned\",\"state\":\"Rejected\",\"bound\":\"lb 28 ≥ 27\"},"
            + "{\"id\":\"n5\",\"parent\":\"n0\",\"label\":\"e\",\"kind\":\"pruned\",\"state\":\"Rejected\",\"bound\":\"lb 27 ≥ 27\"}"
            + "],\"total\":15,\"cut\":8,\"step\":23,\"ok\":true}",
            Readable(frames[^1]));
        // The greedy tour is the first step: the root, and the greedy tour's own leaf under it, the best so far, in focus (#897).
        // Once the search beats it, the leaf is only explored.
        Assert.Equal("[{\"id\":\"n0\",\"parent\":null,\"label\":\"a\",\"kind\":\"explored\",\"state\":\"Committed\",\"bound\":\"lb 27\",\"order\":1},{\"id\":\"n1\",\"parent\":\"n0\",\"label\":\"greedy tour\",\"kind\":\"solution\",\"state\":\"ElementHighlight\",\"bound\":\"tour 31\"}]",
            Readable(frames[0].GetProperty("nodes")));
    }

    [Theory]
    [InlineData("(({New York,Chicago,Denver,Los Angeles,Miami},{({New York,Chicago},790),({New York,Denver},1770),({New York,Los Angeles},2450),({New York,Miami},1280),({Chicago,Denver},1000),({Chicago,Los Angeles},2015),({Chicago,Miami},1370),({Denver,Los Angeles},1015),({Denver,Miami},2060),({Los Angeles,Miami},2745)}),8000)", "tour 6830")]
    [InlineData("(({1,2,3,4},{({1,2},3),({1,3},9),({1,4},4),({2,3},2),({2,4},8),({3,4},5)}),20)", "tour 14")]
    public async Task Frames_GreedyNeverBeaten_EndOnTheGreedyToursLeaf(string instance, string cost) {
        // The search never beats the greedy tour, so the answer is that tour: its own leaf under the root, green in the Done
        // (#897). It is the best so far from the first frame on.
        var (_, body) = await Visualize($"visualization={Vis}&format=frames", instance);
        var frames = SearchTreeJson.Check(body, "Traveling Salesperson", instance);
        var done = frames[^1];
        Assert.True(done.GetProperty("ok").GetBoolean());
        Assert.EndsWith(": the search found nothing cheaper.", done.GetProperty("caption").GetString());
        var green = SearchTreeJson.GreenPath(done);
        Assert.Equal([("n0", "Solution"), ("n1", "Solution")], green.Select(n => (SearchTreeJson.Id(n), SearchTreeJson.State(n))));
        Assert.Equal(("greedy tour", "solution", cost), (green[1].GetProperty("label").GetString(), SearchTreeJson.Kind(green[1]), green[1].GetProperty("bound").GetString()));
        Assert.Equal(["n1"], frames[0].GetProperty("focus").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal("Committed", SearchTreeJson.State(frames[0].GetProperty("nodes")[0]));
        Assert.All(frames.Skip(1).SkipLast(1), f => Assert.Empty(SearchTreeJson.GreenPath(f)));
    }

    [Fact]
    public async Task Frames_PastTheCap_FoldTheTree_AndKeepIdsStable() {
        // 11 cities: the search runs past the 150-step cap, so the tree is wide enough to fold.
        var instance = SearchTreeTsp_Tests.Snapshot.Select(r => r.Data.Item2).Last();
        var (_, body) = await Visualize($"visualization={Vis}&format=frames", instance);
        var frames = SearchTreeJson.Check(body, "Traveling Salesperson", instance);
        Assert.Contains(frames, f => f.GetProperty("nodes").EnumerateArray().Any(n => SearchTreeJson.Kind(n) is "more" or "cut"));
        var parentOf = new Dictionary<string, string?>();
        foreach (var n in frames.SelectMany(f => f.GetProperty("nodes").EnumerateArray())) {
            string id = SearchTreeJson.Id(n);
            if (parentOf.TryGetValue(id, out var p)) Assert.Equal(p, SearchTreeJson.Parent(n));
            else parentOf[id] = SearchTreeJson.Parent(n);
        }
        Assert.EndsWith(" not shown)", frames[^1].GetProperty("caption").GetString());
    }
}

// A stopped run, under the 0.05 s budget of TinyBudgetAppFactory.
[Collection(TimingCollection.Name)]
public class SearchTreeTsp_StoppedRun_Endpoint_Tests : IClassFixture<TinyBudgetAppFactory> {
    private readonly HttpClient _client;

    public SearchTreeTsp_StoppedRun_Endpoint_Tests(TinyBudgetAppFactory factory) {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Frames_Stopped_EndWithTheTreeSoFar_NotOk() {
        // 40 cities, every pair joined: the branch and bound reduces a 40 x 40 table for every extension, far longer than 0.05 s.
        var random = new Random(771);
        var cities = Enumerable.Range(1, 40).Select(i => $"c{i}").ToList();
        var edges = cities.SelectMany((a, i) => cities.Skip(i + 1).Select(b => $"({{{a},{b}}},{random.Next(1, 100)})"));
        string instance = $"(({{{string.Join(",", cities)}}},{{{string.Join(",", edges)}}}),100000)";
        var sw = Stopwatch.StartNew();
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/visualize?visualization=tspsearchtreevisualization&format=frames", content, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(20), $"took {sw.Elapsed}");
        var frames = SearchTreeJson.Check(body, "Traveling Salesperson", instance);
        Assert.True(frames.Count >= 2);
        var done = frames[^1];
        Assert.False(done.GetProperty("ok").GetBoolean());
        Assert.Matches(@"^Stopped after 0\.05 s and [\d,]+ steps?; the solver had not finished\.", done.GetProperty("caption").GetString());
        // The tree so far, with nothing claimed: no answer path, nothing highlighted.
        var nodes = done.GetProperty("nodes").EnumerateArray().ToList();
        Assert.NotEmpty(nodes);
        Assert.Equal(frames[^2].GetProperty("total").GetInt32(), done.GetProperty("total").GetInt32());
        Assert.DoesNotContain(nodes, n => SearchTreeJson.State(n) is "Solution" or "ElementHighlight");
    }
}
