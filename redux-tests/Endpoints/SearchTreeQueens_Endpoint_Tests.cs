using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// N-Queens' search tree, a second picture of its backtracking solver, on the actual /ProblemProvider/visualize?format=frames
// output. The visualization is new, so the guard for old frontends is that its list format is exactly the board picture's,
// that /info shows nothing of the frames plumbing, and that /info for the problem and the solver is what it was
// (info.<name>.json).
public class SearchTreeQueens_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public SearchTreeQueens_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private const string Vis = "nqueenssearchtreevisualization";
    private const string Solver = "nqueensbacktracking";

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
            // From n = 20 up the solver needs seconds, which a busy test run can stretch past the endpoint's time budget
            // (as in BoardFrames_Endpoint_Tests); SearchTreeQueens_Tests checks those rows without one.
            foreach (var (_, instance, _, _) in SearchTreeQueens_Tests.Rows())
                if (int.Parse(instance.Trim()) < 20) data.Add(instance);
            return data;
        }
    }

    public static TheoryData<string> TouchedInterfaces => new(["nqueens", Solver]);

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
        Assert.Equal("N-Queens Search Tree", json.GetProperty("visualizationName").GetString());
        Assert.Equal("DynamicTable", json.GetProperty("visualizationType").GetString());
        Assert.Equal(["visualizationName", "visualizationDefinition", "source", "sourceFile", "contributors", "visualizationType", "pictureType", "solver"],
            json.EnumerateObject().Select(p => p.Name));
        Assert.Equal("N-Queens Backtracking", json.GetProperty("solver").GetProperty("solverName").GetString());
    }

    [Theory]
    [InlineData("4")]
    [InlineData("3")]
    public async Task List_IsExactlyTheBoardPicturesList(string instance) {
        var (status, body) = await Visualize($"visualization={Vis}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var (_, expected) = await Visualize("visualization=nqueensvisualization", instance);
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
        var frames = SearchTreeJson.Check(body, "N-Queens", instance);
        var done = frames[^1];
        // The same run as the board picture: same events and captions.
        var (_, boardBody) = await Visualize($"visualization=nqueensvisualization&format=frames&solver={Solver}", instance);
        var boardFrames = JsonDocument.Parse(boardBody).RootElement.GetProperty("frames").EnumerateArray().ToList();
        Assert.Equal(boardFrames.Select(f => (f.GetProperty("event").GetString(), f.GetProperty("caption").GetString())),
            frames.Select(f => (f.GetProperty("event").GetString(), f.GetProperty("caption").GetString())));
        Assert.All(frames.SkipLast(1), f => Assert.Single(f.GetProperty("focus").EnumerateArray()));
        // As on the board (#839), nothing is Solution while the solver searches: the answer's path turns green only in the Done.
        Assert.All(frames.SkipLast(1), f => Assert.DoesNotContain(f.GetProperty("nodes").EnumerateArray(), n => SearchTreeJson.State(n) == "Solution"));
        // Depth first: nothing is ever queued, so there are no order badges.
        Assert.DoesNotContain(frames.SelectMany(f => f.GetProperty("nodes").EnumerateArray()), n => SearchTreeJson.Kind(n) == "queued" || n.TryGetProperty("order", out _));

        // Done: ok when there is an answer, and then the path from the root is green and spells it, row by row.
        bool ok = boardFrames[^1].GetProperty("ok").GetBoolean();
        Assert.Equal(ok, done.GetProperty("ok").GetBoolean());
        var green = SearchTreeJson.GreenPath(done);
        int n = int.Parse(instance.Trim());
        if (!ok || n == 0) {
            Assert.Empty(green);
            return;
        }
        string answer = ((API.Interfaces.ISolver)Activator.CreateInstance(ProblemProvider.Solvers[Solver])!).solve(instance);
        Assert.Equal(answer, "{" + string.Join(",", green.Skip(1).Select(q => q.GetProperty("label").GetString())) + "}");
        Assert.Equal("solution", SearchTreeJson.Kind(green[^1]));
    }

    [Fact]
    public async Task Frames_FourQueens_MatchTheMockup() {
        // The mockup's 4 x 4 board: (0,0) fails after two rows, (0,1) leads to {(0,1),(1,3),(2,0),(3,2)}. Past 12 leaves the cut
        // squares under each queen share one node.
        var (_, body) = await Visualize($"visualization={Vis}&format=frames", "4");
        var frames = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().ToList();
        Assert.Equal(13, frames.Count);
        Assert.Equal(
            "[{\"id\":\"n0\",\"parent\":null,\"label\":\"start\",\"kind\":\"explored\",\"state\":\"Covered\"},"
            + "{\"id\":\"n1\",\"parent\":\"n0\",\"label\":\"(0,0)\",\"kind\":\"explored\",\"state\":\"Covered\"},"
            + "{\"id\":\"n2\",\"parent\":\"n1\",\"label\":\"(1,0)\",\"kind\":\"pruned\",\"state\":\"Rejected\",\"bound\":\"hit by (0,0)\"},"
            + "{\"id\":\"n3\",\"parent\":\"n1\",\"label\":\"(1,1)\",\"kind\":\"pruned\",\"state\":\"Rejected\",\"bound\":\"hit by (0,0)\"},"
            + "{\"id\":\"n4\",\"parent\":\"n1\",\"label\":\"(1,2)\",\"kind\":\"explored\",\"state\":\"ElementHighlight\"}]",
            Readable(frames[1].GetProperty("nodes")));
        Assert.Equal(
            "{\"event\":\"Done\",\"caption\":\"All 4 queens are placed and none attacks another: {(0,1),(1,3),(2,0),(3,2)}. Found after 8 placements.\",\"focus\":[],\"nodes\":["
            + "{\"id\":\"n0\",\"parent\":null,\"label\":\"start\",\"kind\":\"explored\",\"state\":\"Solution\"},"
            + "{\"id\":\"n1\",\"parent\":\"n0\",\"label\":\"(0,0)\",\"kind\":\"failed\",\"state\":\"Rejected\"},"
            + "{\"id\":\"n1.cut\",\"parent\":\"n1\",\"label\":\"2 cut\",\"kind\":\"cut\",\"state\":\"Rejected\",\"hidden\":2},"
            + "{\"id\":\"n4\",\"parent\":\"n1\",\"label\":\"(1,2)\",\"kind\":\"failed\",\"state\":\"Rejected\"},"
            + "{\"id\":\"n4.cut\",\"parent\":\"n4\",\"label\":\"4 cut\",\"kind\":\"cut\",\"state\":\"Rejected\",\"hidden\":4},"
            + "{\"id\":\"n9\",\"parent\":\"n1\",\"label\":\"(1,3)\",\"kind\":\"failed\",\"state\":\"Rejected\"},"
            + "{\"id\":\"n9.cut\",\"parent\":\"n9\",\"label\":\"3 cut\",\"kind\":\"cut\",\"state\":\"Rejected\",\"hidden\":3},"
            + "{\"id\":\"n11\",\"parent\":\"n9\",\"label\":\"(2,1)\",\"kind\":\"failed\",\"state\":\"Rejected\"},"
            + "{\"id\":\"n11.cut\",\"parent\":\"n11\",\"label\":\"4 cut\",\"kind\":\"cut\",\"state\":\"Rejected\",\"hidden\":4},"
            + "{\"id\":\"n18\",\"parent\":\"n0\",\"label\":\"(0,1)\",\"kind\":\"explored\",\"state\":\"Solution\"},"
            + "{\"id\":\"n18.cut\",\"parent\":\"n18\",\"label\":\"3 cut\",\"kind\":\"cut\",\"state\":\"Rejected\",\"hidden\":3},"
            + "{\"id\":\"n22\",\"parent\":\"n18\",\"label\":\"(1,3)\",\"kind\":\"explored\",\"state\":\"Solution\"},"
            + "{\"id\":\"n23\",\"parent\":\"n22\",\"label\":\"(2,0)\",\"kind\":\"explored\",\"state\":\"Solution\"},"
            + "{\"id\":\"n23.cut\",\"parent\":\"n23\",\"label\":\"2 cut\",\"kind\":\"cut\",\"state\":\"Rejected\",\"hidden\":2},"
            + "{\"id\":\"n26\",\"parent\":\"n23\",\"label\":\"(3,2)\",\"kind\":\"solution\",\"state\":\"Solution\"}],"
            + "\"total\":27,\"cut\":18,\"step\":12,\"ok\":true}",
            Readable(frames[^1]));
    }

    [Fact]
    public async Task Frames_NoAnswer_EndWithTheRootTakenBack() {
        var (_, body) = await Visualize($"visualization={Vis}&format=frames", "3");
        var frames = SearchTreeJson.Check(body, "N-Queens", "3");
        var done = frames[^1];
        Assert.False(done.GetProperty("ok").GetBoolean());
        Assert.Empty(SearchTreeJson.GreenPath(done));
        // The last Reject takes back the root: row 0 has run out of columns.
        var last = frames[^2];
        Assert.Equal(["n0"], last.GetProperty("focus").EnumerateArray().Select(x => x.GetString()));
        var root = last.GetProperty("nodes").EnumerateArray().First();
        Assert.Equal(("failed", "Rejected"), (SearchTreeJson.Kind(root), SearchTreeJson.State(root)));
    }

    [Fact]
    public async Task Frames_PastTheCap_EndOnTheAnswersWholePath() {
        // n = 12: the search runs past the 150-step cap, so the Done step names the answer's path; every queen of it is drawn.
        var (_, body) = await Visualize($"visualization={Vis}&format=frames", "12");
        var frames = SearchTreeJson.Check(body, "N-Queens", "12");
        Assert.EndsWith(" not shown)", frames[^1].GetProperty("caption").GetString());
        var green = SearchTreeJson.GreenPath(frames[^1]);
        Assert.Equal(13, green.Count);
        Assert.DoesNotContain(green, q => SearchTreeJson.Kind(q) == "hidden");
        Assert.Contains(frames, f => f.GetProperty("nodes").EnumerateArray().Any(n => SearchTreeJson.Kind(n) is "more" or "cut"));
    }
}

// A stopped run, under the 0.05 s budget of TinyBudgetAppFactory.
[Collection(TimingCollection.Name)]
public class SearchTreeQueens_StoppedRun_Endpoint_Tests : IClassFixture<TinyBudgetAppFactory> {
    private readonly HttpClient _client;

    public SearchTreeQueens_StoppedRun_Endpoint_Tests(TinyBudgetAppFactory factory) {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Frames_Stopped_EndWithTheTreeSoFar_NotOk() {
        // n = 30: the backtracking needs far longer than 0.05 s (as in BoardFrames_Endpoint_Tests).
        var sw = Stopwatch.StartNew();
        var content = new StringContent(JsonSerializer.Serialize("30"), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/visualize?visualization=nqueenssearchtreevisualization&format=frames", content, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(20), $"took {sw.Elapsed}");
        var frames = SearchTreeJson.Check(body, "N-Queens", "30");
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
