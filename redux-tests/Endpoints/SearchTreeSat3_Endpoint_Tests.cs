using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// 3SAT's search tree, a second picture of 3SAT Backtracking, on the actual /ProblemProvider/visualize?format=frames output.
// The visualization is new, so the guard for old frontends is that its list format is exactly the default 3SAT picture's,
// that /info shows nothing of the frames plumbing, and that /info for the problem, the solver and the default picture is
// what it was (info.<name>.json).
public class SearchTreeSat3_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public SearchTreeSat3_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private const string Vis = "sat3searchtreevisualization";
    private const string Solver = "sat3backtrackingsolver";

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
            foreach (var row in SearchTreeSat3_Tests.Snapshot) data.Add(row.Data.Item2);
            return data;
        }
    }

    public static TheoryData<string> TouchedInterfaces => new(["sat3", Solver, "sat3defaultvisualization"]);

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
        Assert.Equal("3SAT Search Tree", json.GetProperty("visualizationName").GetString());
        Assert.Equal("BooleanSatisfiability", json.GetProperty("visualizationType").GetString());
        Assert.Equal(["visualizationName", "visualizationDefinition", "source", "sourceFile", "contributors", "visualizationType", "pictureType", "solver"],
            json.EnumerateObject().Select(p => p.Name));
        Assert.Equal("3SAT Backtracking", json.GetProperty("solver").GetProperty("solverName").GetString());
    }

    [Fact]
    public async Task List_IsExactlyTheDefaultPicturesList() {
        string instance = new API.Problems.NPComplete.NPC_SAT3.SAT3().defaultInstance;
        var (status, body) = await Visualize($"visualization={Vis}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var (_, expected) = await Visualize("visualization=sat3defaultvisualization", instance);
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
        var frames = SearchTreeJson.Check(body, "3SAT", instance);
        var done = frames[^1];
        // The same run as the clause graph: same events and captions.
        var (_, clauseBody) = await Visualize($"visualization=sat3defaultvisualization&format=frames&solver={Solver}", instance);
        var clauseFrames = JsonDocument.Parse(clauseBody).RootElement.GetProperty("frames").EnumerateArray().ToList();
        Assert.Equal(clauseFrames.Select(f => (f.GetProperty("event").GetString(), f.GetProperty("caption").GetString())),
            frames.Select(f => (f.GetProperty("event").GetString(), f.GetProperty("caption").GetString())));
        Assert.All(frames.SkipLast(1), f => Assert.Single(f.GetProperty("focus").EnumerateArray()));

        // Done: ok when there is an answer, and then the path from the root to it is green and spells it (past the cap too:
        // the Done step names the path).
        bool satisfiable = clauseFrames[^1].GetProperty("ok").GetBoolean();
        Assert.Equal(satisfiable, done.GetProperty("ok").GetBoolean());
        var green = SearchTreeJson.GreenPath(done);
        if (!satisfiable) {
            Assert.Empty(green);
            return;
        }
        Assert.Equal("solution", SearchTreeJson.Kind(green[^1]));
        var assignment = clauseFrames[^1].GetProperty("assignment").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetBoolean());
        foreach (var n in green.Skip(1)) {
            var label = n.GetProperty("label").GetString()!.Split('=');
            Assert.Equal(label[1] == "T", assignment["v:" + label[0]]);
        }
    }

    [Fact]
    public async Task Frames_Unsatisfiable_MatchesTheMockup() {
        // The mockup's "Unsatisfiable" example: x1 = True is queued first and both of its children are cut, so the search goes
        // back to x1 = False, whose children are cut too.
        var (_, body) = await Visualize($"visualization={Vis}&format=frames", "(x1 | x2) & (x1 | !x2) & (!x1 | x3) & (!x1 | !x3)");
        var frames = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().ToList();
        Assert.Equal(14, frames.Count);
        Assert.Equal(
            "{\"event\":\"Backtrack\",\"caption\":\"Neither value of the last variable works. Go back to x1 = False, the most promising branch still open: x2 is next.\",\"focus\":[\"n2\"],\"nodes\":["
            + "{\"id\":\"n0\",\"parent\":null,\"label\":\"start\",\"kind\":\"explored\",\"state\":\"Covered\",\"order\":1},"
            + "{\"id\":\"n1\",\"parent\":\"n0\",\"label\":\"x1=T\",\"kind\":\"explored\",\"state\":\"Covered\",\"bound\":\"priority -1\",\"order\":2},"
            + "{\"id\":\"n3\",\"parent\":\"n1\",\"label\":\"x3=T\",\"kind\":\"pruned\",\"state\":\"Rejected\",\"bound\":\"C4 false\"},"
            + "{\"id\":\"n4\",\"parent\":\"n1\",\"label\":\"x3=F\",\"kind\":\"pruned\",\"state\":\"Rejected\",\"bound\":\"C3 false\"},"
            + "{\"id\":\"n2\",\"parent\":\"n0\",\"label\":\"x1=F\",\"kind\":\"explored\",\"state\":\"ElementHighlight\",\"bound\":\"priority -1\",\"order\":3}],\"total\":5,\"cut\":2,\"step\":8}",
            Readable(frames[8]));
        var done = frames[^1];
        Assert.False(done.GetProperty("ok").GetBoolean());
        Assert.Equal(7, done.GetProperty("total").GetInt32());
        Assert.Equal(4, done.GetProperty("cut").GetInt32());
    }
}

// A stopped run, under the 0.05 s budget of TinyBudgetAppFactory.
[Collection(TimingCollection.Name)]
public class SearchTreeSat3_StoppedRun_Endpoint_Tests : IClassFixture<TinyBudgetAppFactory> {
    private readonly HttpClient _client;

    public SearchTreeSat3_StoppedRun_Endpoint_Tests(TinyBudgetAppFactory factory) {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Frames_Stopped_EndWithTheTreeSoFar_NotOk() {
        // 400 variables and 1,700 clauses: every node the search makes copies the whole formula, far longer than 0.05 s.
        var random = new Random(771);
        string formula = string.Join(" & ", Enumerable.Range(0, 1700).Select(_ =>
            "(" + string.Join(" | ", Enumerable.Range(0, 3).Select(_ => (random.Next(2) == 0 ? "!" : "") + "x" + (random.Next(400) + 1))) + ")"));
        var sw = Stopwatch.StartNew();
        var content = new StringContent(JsonSerializer.Serialize(formula), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/visualize?visualization=sat3searchtreevisualization&format=frames", content, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(20), $"took {sw.Elapsed}");
        var frames = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().ToList();
        Assert.True(frames.Count >= 2);
        var done = frames[^1];
        Assert.Single(frames, f => f.GetProperty("event").GetString() == "Done");
        Assert.False(done.GetProperty("ok").GetBoolean());
        Assert.Matches(@"^Stopped after 0\.05 s and [\d,]+ steps?; the solver had not finished\.", done.GetProperty("caption").GetString());
        Assert.Empty(done.GetProperty("focus").EnumerateArray());
        // The tree so far, with nothing claimed: no answer path, nothing highlighted.
        var nodes = done.GetProperty("nodes").EnumerateArray().ToList();
        Assert.Equal(frames[^2].GetProperty("nodes").EnumerateArray().Select(n => n.GetProperty("id").GetString()), nodes.Select(n => n.GetProperty("id").GetString()));
        Assert.NotEmpty(nodes);
        Assert.DoesNotContain(nodes, n => n.GetProperty("state").GetString() is "Solution" or "ElementHighlight");
    }
}
