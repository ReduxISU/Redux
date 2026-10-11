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
        Assert.DoesNotContain("SearchTree", body);
    }

    [Fact]
    public async Task Info_ReportsTheLegacyType_AndNoFramesPlumbing() {
        var body = await _client.GetStringAsync($"/ProblemProvider/info?interface={Vis}", TestContext.Current.CancellationToken);
        var json = JsonDocument.Parse(body).RootElement;
        Assert.Equal("3SAT Search Tree", json.GetProperty("visualizationName").GetString());
        Assert.Equal("BooleanSatisfiability", json.GetProperty("visualizationType").GetString());
        Assert.Equal(["visualizationName", "visualizationDefinition", "source", "sourceFile", "contributors", "visualizationType", "solver"],
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

    private static readonly HashSet<string> Vocabulary = ["Background", "ElementHighlight", "Solution", "Rejected", "Untraveled", "Covered", "Blocked"];
    private static readonly HashSet<string> Kinds = ["queued", "explored", "pruned", "failed", "solution", "skipped", "more", "cut", "hidden"];

    [Theory]
    [MemberData(nameof(Instances))]
    public async Task Frames_HaveTheSearchTreePayload_AndAWellFormedTreeInEveryFrame(string instance) {
        var (status, body) = await Visualize($"visualization={Vis}&format=frames", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal(["type", "payload", "frames"], root.EnumerateObject().Select(p => p.Name));
        Assert.Equal("SearchTree", root.GetProperty("type").GetString());
        var payload = root.GetProperty("payload");
        Assert.Equal(["kind", "problem", "instance", "maxLeaves"], payload.EnumerateObject().Select(p => p.Name));
        Assert.Equal("searchTree", payload.GetProperty("kind").GetString());
        Assert.Equal("3SAT", payload.GetProperty("problem").GetString());
        Assert.Equal(instance, payload.GetProperty("instance").GetString());
        Assert.Equal(12, payload.GetProperty("maxLeaves").GetInt32());

        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        var done = Assert.Single(frames, f => f.GetProperty("event").GetString() == "Done");
        Assert.Equal(done, frames[^1]);
        var (_, answer) = await Visualize($"visualization=sat3defaultvisualization&format=frames&solver={Solver}", instance);
        // The same run as the clause graph: same events and captions.
        var clauseFrames = JsonDocument.Parse(answer).RootElement.GetProperty("frames").EnumerateArray().ToList();
        Assert.Equal(clauseFrames.Select(f => (f.GetProperty("event").GetString(), f.GetProperty("caption").GetString())),
            frames.Select(f => (f.GetProperty("event").GetString(), f.GetProperty("caption").GetString())));
        foreach (var f in frames) {
            bool isDone = f.GetProperty("event").GetString() == "Done";
            Assert.Equal(["event", "caption", "focus", "nodes", "total", "cut", .. isDone ? new[] { "ok" } : []], f.EnumerateObject().Select(p => p.Name));
            var nodes = f.GetProperty("nodes").EnumerateArray().ToList();
            var ids = new HashSet<string>();
            foreach (var n in nodes) {
                string id = n.GetProperty("id").GetString()!;
                Assert.True(ids.Add(id), $"{id} twice");
                Assert.Matches(@"^n\d+(\.more|\.cut)?$", id);
                var parent = n.GetProperty("parent");
                if (parent.ValueKind == JsonValueKind.Null) Assert.Equal("n0", id);
                else Assert.Contains(parent.GetString()!, ids);   // parents come first
                Assert.Contains(n.GetProperty("kind").GetString()!, Kinds);
                Assert.Contains(n.GetProperty("state").GetString()!, Vocabulary);
            }
            Assert.True(nodes.Count(n => !nodes.Any(k => k.GetProperty("parent").ValueKind == JsonValueKind.String && k.GetProperty("parent").GetString() == n.GetProperty("id").GetString())) <= 12
                || nodes.Any(n => n.GetProperty("kind").GetString() == "queued"), "more than 12 leaves without a queued node holding them open");
            var focus = f.GetProperty("focus").EnumerateArray().Select(x => x.GetString()!).ToList();
            Assert.True(focus.Count <= 1);
            Assert.All(focus, id => Assert.Contains(id, ids));
            if (isDone) Assert.Empty(focus);
            else Assert.Single(focus);
            Assert.Equal(nodes.Count(n => n.GetProperty("kind").GetString() is not ("more" or "cut")) + nodes.Sum(n => n.TryGetProperty("hidden", out var h) ? h.GetInt32() : 0),
                f.GetProperty("total").GetInt32());
        }

        // Done: ok when there is an answer, and then the path from the root to it is green and spells it (when its steps
        // were all kept).
        bool satisfiable = clauseFrames[^1].GetProperty("ok").GetBoolean();
        Assert.Equal(satisfiable, done.GetProperty("ok").GetBoolean());
        var green = done.GetProperty("nodes").EnumerateArray().Where(n => n.GetProperty("state").GetString() == "Solution").ToList();
        if (!satisfiable) {
            Assert.Empty(green);
            return;
        }
        Assert.Equal("n0", green[0].GetProperty("id").GetString());
        Assert.Equal("solution", green[^1].GetProperty("kind").GetString());
        for (int i = 1; i < green.Count; i++) Assert.Equal(green[i - 1].GetProperty("id").GetString(), green[i].GetProperty("parent").GetString());
        if (green.Any(n => n.GetProperty("kind").GetString() == "hidden")) return;
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
            + "{\"id\":\"n2\",\"parent\":\"n0\",\"label\":\"x1=F\",\"kind\":\"explored\",\"state\":\"ElementHighlight\",\"bound\":\"priority -1\",\"order\":3}],\"total\":5,\"cut\":2}",
            Readable(frames[8]));
        var done = frames[^1];
        Assert.False(done.GetProperty("ok").GetBoolean());
        Assert.Equal(7, done.GetProperty("total").GetInt32());
        Assert.Equal(4, done.GetProperty("cut").GetInt32());
    }
}

// A stopped run, under the 0.05 s budget of TinyBudgetAppFactory.
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
