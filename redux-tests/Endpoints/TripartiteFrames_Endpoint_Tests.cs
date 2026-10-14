using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Tripartite Matching picture on the actual /ProblemProvider/visualize?format=frames output, for 3-Dimensional Matching
// and its brute force solver, plus the guard that keeps what old frontends read exactly as it was: /info for the problem, its
// solver and its verifier (info.<name>.json, captured before the picture existed).
// The deliberate exception is the problem's default visualization, which moved off DummyVisualization to DM3Visualization:
// see the "rewire" tests.
public class TripartiteFrames_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public TripartiteFrames_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private static string ReadSnapshot(string file) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Endpoints", "Snapshots", file)).Replace("\r\n", "\n");

    private async Task<(HttpStatusCode Status, string Body)> Post(string path, string json) {
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/{path}", content, TestContext.Current.CancellationToken);
        return (response.StatusCode, (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Replace("\r\n", "\n"));
    }

    private Task<(HttpStatusCode Status, string Body)> Visualize(string query, string instance) =>
        Post($"visualize?{query}", JsonSerializer.Serialize(instance));

    private static string Readable(JsonElement element) =>
        JsonSerializer.Serialize(element, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    private static Dictionary<string, string> StatesOf(JsonElement map) =>
        map.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetProperty("state").GetString()!);

    private static readonly HashSet<string> Vocabulary = new() {
        "Background", "ElementHighlight", "Solution", "Rejected", "Untraveled", "Covered", "Blocked", "Committed",
    };

    public static TheoryData<string, string, string> FramesCases {
        get {
            var data = new TheoryData<string, string, string>();
            foreach (var (solver, instance, answer) in TripartiteSolvers_Tests.Rows()) data.Add(solver, instance, answer);
            return data;
        }
    }

    public static TheoryData<string> TouchedInterfaces => new(["threedimensionalmatchingbruteforce", "genericverifierdm3"]);

    // ---- What old frontends read must not change. ----

    [Theory]
    [MemberData(nameof(TouchedInterfaces))]
    public async Task Info_IsUnchangedByTheTripartitePicture(string name) {
        var response = await _client.GetAsync($"/ProblemProvider/info?interface={name}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ReadSnapshot($"info.{name}.json"), body.Replace("\r\n", "\n"));
        Assert.DoesNotContain("TripartiteMatching", body);
        Assert.DoesNotContain("StepShape", body);
        Assert.DoesNotContain("Describe", body);
        Assert.DoesNotContain("ParseAnswer", body);
    }

    // ---- 3-Dimensional Matching moved off DummyVisualization: the only thing that may change. ----

    // What embeds its defaultVisualization: the problem and the reduction into it (3SAT to 3DM, Garey and Johnson). Their
    // snapshots were taken after the rewire; the diff against the pre-change capture is only the nested defaultVisualization
    // object (and reduce's visualization).
    private const string Sat3Default = "(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | !x1)";

    [Theory]
    [InlineData("dm3")]
    [InlineData("gareyjohnson")]
    public async Task Rewire_Info_ShowsDM3Visualization_AndNothingElse(string name) {
        var response = await _client.GetAsync($"/ProblemProvider/info?interface={name}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ReplaceLineEndings("\n");
        Assert.Equal(ReadSnapshot($"info.{name}.json"), body);
        Assert.DoesNotContain("DummyVisualization", body);
        Assert.DoesNotContain("TripartiteMatching", body);
        Assert.DoesNotContain("StepShape", body);
    }

    [Fact]
    public async Task Rewire_Dm3Info_ReportsDM3VisualizationAndItsSolver() {
        var response = await _client.GetAsync("/ProblemProvider/info?interface=dm3", TestContext.Current.CancellationToken);
        var vis = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement.GetProperty("defaultVisualization");
        Assert.Equal("3-Dimensional Matching Tripartite Visualization", vis.GetProperty("visualizationName").GetString());
        Assert.Equal("DynamicTable", vis.GetProperty("visualizationType").GetString());
        var own = await _client.GetAsync("/ProblemProvider/info?interface=dm3visualization", TestContext.Current.CancellationToken);
        Assert.Equal(JsonSerializer.Serialize(JsonDocument.Parse(await own.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement), JsonSerializer.Serialize(vis));
    }

    [Fact]
    public async Task Rewire_Reduce_Sat3To3DM_ChangesOnlyTheNestedVisualization() {
        var (status, body) = await Post("reduce?reduction=gareyjohnson", JsonSerializer.Serialize(Sat3Default));
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(ReadSnapshot("gareyjohnson.reduce.json"), body);
    }

    // Before the rewire 3DM's visualization was DummyVisualization, so visualizeReduction answered an empty list (#755). Now
    // it answers the table of the reduced instance's triples before and after; the mapped matching picks 18 of the 135
    // triples, one for each element of X.
    [Fact]
    public async Task Rewire_VisualizeReduction_Sat3To3DM_NowReturnsTheTriples() {
        var (status, body) = await Post("visualizeReduction?reduction=gareyjohnson&solution=" + Uri.EscapeDataString("(x1:True,x2:True,x3:False)"), JsonSerializer.Serialize(Sat3Default));
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(ReadSnapshot("gareyjohnson.visualizereduction.json"), body);
        var items = JsonDocument.Parse(body).RootElement.EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.All(items, i => Assert.Equal("table", i.GetProperty("kind").GetString()));
        Assert.Equal(18, items[1].GetProperty("rows").EnumerateArray().Count(r => r.GetProperty("color").ValueKind == JsonValueKind.String && r.GetProperty("color").GetString() == "Solution"));
    }

    // The new visualization reports its legacy list type in /info and nothing of the picture.
    [Fact]
    public async Task DM3Visualization_Info_IsTheListTypeOnly() {
        var response = await _client.GetAsync("/ProblemProvider/info?interface=dm3visualization", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal(["visualizationName", "visualizationDefinition", "source", "sourceFile", "contributors", "visualizationType", "solver"],
            root.EnumerateObject().Select(p => p.Name));
        Assert.Equal("DynamicTable", root.GetProperty("visualizationType").GetString());
        Assert.Equal("3-Dimensional Matching Brute Force", root.GetProperty("solver").GetProperty("solverName").GetString());
        Assert.DoesNotContain("TripartiteMatching", body);
        Assert.DoesNotContain("Describe", body);
    }

    [Fact]
    public async Task DM3Visualization_ListFormat_IsTheTripleTableBeforeAndAfter() {
        var (status, body) = await Visualize("visualization=dm3visualization", "{a,b}{c,d}{e,f}{a,c,e}{a,d,f}{b,d,f}");
        Assert.Equal(HttpStatusCode.OK, status);
        var list = JsonDocument.Parse(body).RootElement.EnumerateArray().ToList();
        Assert.Equal(2, list.Count);
        Assert.Equal(
            "{\"kind\":\"table\",\"title\":\"3 triples over |X| = 2, |Y| = 2, |Z| = 2\",\"columns\":[{\"key\":\"triple\",\"label\":\"Triple\"},{\"key\":\"x\",\"label\":\"X\"},"
            + "{\"key\":\"y\",\"label\":\"Y\"},{\"key\":\"z\",\"label\":\"Z\"},{\"key\":\"chosen\",\"label\":\"Chosen\"}],"
            + "\"rows\":[{\"id\":\"T1\",\"color\":null,\"cells\":{\"triple\":\"T1\",\"x\":\"a\",\"y\":\"c\",\"z\":\"e\",\"chosen\":\"\"},\"cellColors\":null},"
            + "{\"id\":\"T2\",\"color\":null,\"cells\":{\"triple\":\"T2\",\"x\":\"a\",\"y\":\"d\",\"z\":\"f\",\"chosen\":\"\"},\"cellColors\":null},"
            + "{\"id\":\"T3\",\"color\":null,\"cells\":{\"triple\":\"T3\",\"x\":\"b\",\"y\":\"d\",\"z\":\"f\",\"chosen\":\"\"},\"cellColors\":null}]}",
            Readable(list[0]));
        Assert.Equal(["T1 yes Solution", "T2 no ", "T3 yes Solution"], list[1].GetProperty("rows").EnumerateArray()
            .Select(r => $"{r.GetProperty("id").GetString()} {r.GetProperty("cells").GetProperty("chosen").GetString()} {(r.GetProperty("color").ValueKind == JsonValueKind.Null ? "" : r.GetProperty("color").GetString())}"));

        // No answer: the table stays blank.
        var (_, none) = await Visualize("visualization=dm3visualization", TripartiteSolvers_Tests.NoMatching);
        var tables = JsonDocument.Parse(none).RootElement.EnumerateArray().ToList();
        Assert.All(tables[^1].GetProperty("rows").EnumerateArray(), r => Assert.Equal("", r.GetProperty("cells").GetProperty("chosen").GetString()));
    }

    // ---- The picture ----

    [Theory]
    [MemberData(nameof(FramesCases))]
    public async Task Frames_EveryInstance_HasTheTripartitePayloadAndFramesThatAgreeWithTheAnswer(string solver, string instance, string answer) {
        var (status, body) = await Visualize($"visualization={TripartiteSolvers_Tests.Visualization}&format=frames&solver={solver}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal(["type", "payload", "frames"], root.EnumerateObject().Select(p => p.Name));
        Assert.Equal("TripartiteMatching", root.GetProperty("type").GetString());
        var payload = root.GetProperty("payload");
        Assert.Equal(["kind", "x", "y", "z", "triples"], payload.EnumerateObject().Select(p => p.Name));
        Assert.Equal("tripartite", payload.GetProperty("kind").GetString());
        var elementIds = new HashSet<string>();
        foreach (var set in new[] { "x", "y", "z" }) {
            Assert.All(payload.GetProperty(set).EnumerateArray(), (e, i) => {
                Assert.Equal(["id", "name"], e.EnumerateObject().Select(p => p.Name));
                Assert.Equal($"{set}{i + 1}", e.GetProperty("id").GetString());
                elementIds.Add(e.GetProperty("id").GetString()!);
            });
        }
        var triples = payload.GetProperty("triples").EnumerateArray().ToList();
        Assert.All(triples, (t, i) => {
            Assert.Equal(["id", "x", "y", "z"], t.EnumerateObject().Select(p => p.Name));
            Assert.Equal($"T{i + 1}", t.GetProperty("id").GetString());
            Assert.StartsWith("x", t.GetProperty("x").GetString());
            Assert.StartsWith("y", t.GetProperty("y").GetString());
            Assert.StartsWith("z", t.GetProperty("z").GetString());
            Assert.Contains(t.GetProperty("x").GetString()!, elementIds);
        });
        var byId = triples.ToDictionary(t => t.GetProperty("id").GetString()!);
        string Name(string id) => payload.GetProperty(id[..1]).EnumerateArray().Single(e => e.GetProperty("id").GetString() == id).GetProperty("name").GetString()!;

        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        var done = Assert.Single(frames, f => f.GetProperty("event").GetString() == "Done");
        Assert.Equal(done, frames[^1]);
        Assert.All(frames, f => Assert.False(string.IsNullOrWhiteSpace(f.GetProperty("caption").GetString())));
        Assert.All(frames.SkipLast(1), f => Assert.False(f.TryGetProperty("ok", out _)));
        foreach (var f in frames) {
            Assert.Equal(["event", "caption", "focus", "chosen", "repeats", "elements", "triples", .. f.TryGetProperty("ok", out _) ? new[] { "ok" } : []],
                f.EnumerateObject().Select(p => p.Name));
            var chosen = f.GetProperty("chosen").EnumerateArray().Select(x => x.GetString()!).ToList();
            Assert.All(chosen, id => Assert.Contains(id, byId.Keys));
            // How often the chosen triples use each element; repeats lists the ones used more than once.
            var uses = chosen.SelectMany(id => new[] { "x", "y", "z" }.Select(k => byId[id].GetProperty(k).GetString()!))
                .GroupBy(e => e).ToDictionary(g => g.Key, g => g.Count());
            Assert.Equal(uses.Where(u => u.Value > 1).OrderBy(u => u.Key, StringComparer.Ordinal).Select(u => $"{u.Key}={u.Value}"),
                f.GetProperty("repeats").EnumerateObject().Select(p => $"{p.Name}={p.Value.GetInt32()}"));
            var elements = StatesOf(f.GetProperty("elements"));
            var tripleStates = StatesOf(f.GetProperty("triples"));
            Assert.All(elements, s => { Assert.Contains(s.Key, elementIds); Assert.Contains(s.Value, Vocabulary); });
            Assert.All(tripleStates, s => { Assert.Contains(s.Key, byId.Keys); Assert.Contains(s.Value, Vocabulary); });
            // Used once: Covered; more than once: Rejected. Unused elements are left Background.
            Assert.Equal(uses.OrderBy(u => u.Key, StringComparer.Ordinal).Select(u => $"{u.Key}:{(u.Value > 1 ? "Rejected" : "Covered")}"),
                elements.OrderBy(e => e.Key, StringComparer.Ordinal).Select(e => $"{e.Key}:{e.Value}"));
            // Whole sets: chosen triples are highlighted while tried, Solution once accepted, Rejected when they share an element.
            string ev = f.GetProperty("event").GetString()!;
            foreach (var id in chosen) {
                bool clash = new[] { "x", "y", "z" }.Any(k => uses[byId[id].GetProperty(k).GetString()!] > 1);
                Assert.Equal(clash ? "Rejected" : ev == "Reject" ? "ElementHighlight" : "Solution", tripleStates[id]);
            }
            foreach (var (id, t) in byId.Where(p => !chosen.Contains(p.Key)))
                Assert.Equal(new[] { "x", "y", "z" }.Any(k => uses.ContainsKey(t.GetProperty(k).GetString()!)) ? "Blocked" : null, tripleStates.GetValueOrDefault(id));
        }
        if (answer == "{}") {
            Assert.False(done.GetProperty("ok").GetBoolean());
            Assert.Empty(done.GetProperty("chosen").EnumerateArray());
        } else {
            Assert.True(done.GetProperty("ok").GetBoolean());
            // The Done frame's triples are the answer's, in M's order.
            var expected = answer[2..^2].Split("},{").ToList();
            Assert.Equal(expected, done.GetProperty("chosen").EnumerateArray().Select(id => string.Join(",", new[] { "x", "y", "z" }.Select(k => Name(byId[id.GetString()!].GetProperty(k).GetString()!)))));
        }
    }

    [Fact]
    public async Task Frames_Default_MatchesTheMockup() {
        var (_, body) = await Visualize("visualization=dm3visualization&format=frames", TripartiteSolvers_Tests.Default);
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal(
            "{\"kind\":\"tripartite\",\"x\":[{\"id\":\"x1\",\"name\":\"Paul\"},{\"id\":\"x2\",\"name\":\"Sally\"},{\"id\":\"x3\",\"name\":\"Dave\"}],"
            + "\"y\":[{\"id\":\"y1\",\"name\":\"Madison\"},{\"id\":\"y2\",\"name\":\"Austin\"},{\"id\":\"y3\",\"name\":\"Bob\"}],"
            + "\"z\":[{\"id\":\"z1\",\"name\":\"Chloe\"},{\"id\":\"z2\",\"name\":\"Frank\"},{\"id\":\"z3\",\"name\":\"Jake\"}],"
            + "\"triples\":[{\"id\":\"T1\",\"x\":\"x1\",\"y\":\"y1\",\"z\":\"z1\"},{\"id\":\"T2\",\"x\":\"x1\",\"y\":\"y2\",\"z\":\"z3\"},{\"id\":\"T3\",\"x\":\"x2\",\"y\":\"y3\",\"z\":\"z1\"},"
            + "{\"id\":\"T4\",\"x\":\"x2\",\"y\":\"y1\",\"z\":\"z2\"},{\"id\":\"T5\",\"x\":\"x3\",\"y\":\"y2\",\"z\":\"z1\"},{\"id\":\"T6\",\"x\":\"x3\",\"y\":\"y3\",\"z\":\"z1\"}]}",
            Readable(root.GetProperty("payload")));
        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        Assert.Equal(16, frames.Count);
        Assert.Equal(
            "{\"event\":\"Reject\",\"caption\":\"Try T1, T2 and T3: Paul and Chloe are used twice.\",\"focus\":[\"x1\",\"z1\"],\"chosen\":[\"T1\",\"T2\",\"T3\"],"
            + "\"repeats\":{\"x1\":2,\"z1\":2},"
            + "\"elements\":{\"x1\":{\"state\":\"Rejected\"},\"x2\":{\"state\":\"Covered\"},\"y1\":{\"state\":\"Covered\"},\"y2\":{\"state\":\"Covered\"},\"y3\":{\"state\":\"Covered\"},"
            + "\"z1\":{\"state\":\"Rejected\"},\"z3\":{\"state\":\"Covered\"}},"
            + "\"triples\":{\"T1\":{\"state\":\"Rejected\"},\"T2\":{\"state\":\"Rejected\"},\"T3\":{\"state\":\"Rejected\"},\"T4\":{\"state\":\"Blocked\"},\"T5\":{\"state\":\"Blocked\"},\"T6\":{\"state\":\"Blocked\"}}}",
            Readable(frames[0]));
        // The mockup's solved view: T2, T4 and T6 chosen, every name used once, the other triples ruled out.
        Assert.Equal(
            "{\"event\":\"Done\",\"caption\":\"T2 (Paul, Austin, Jake), T4 (Sally, Madison, Frank) and T6 (Dave, Bob, Chloe) use every element of X, Y and Z exactly once. Found after 15 tries.\","
            + "\"focus\":[],\"chosen\":[\"T2\",\"T4\",\"T6\"],\"repeats\":{},"
            + "\"elements\":{\"x1\":{\"state\":\"Covered\"},\"x2\":{\"state\":\"Covered\"},\"x3\":{\"state\":\"Covered\"},\"y1\":{\"state\":\"Covered\"},\"y2\":{\"state\":\"Covered\"},"
            + "\"y3\":{\"state\":\"Covered\"},\"z1\":{\"state\":\"Covered\"},\"z2\":{\"state\":\"Covered\"},\"z3\":{\"state\":\"Covered\"}},"
            + "\"triples\":{\"T1\":{\"state\":\"Blocked\"},\"T2\":{\"state\":\"Solution\"},\"T3\":{\"state\":\"Blocked\"},\"T4\":{\"state\":\"Solution\"},\"T5\":{\"state\":\"Blocked\"},\"T6\":{\"state\":\"Solution\"}},\"ok\":true}",
            Readable(frames[^1]));
    }

    [Theory]
    [InlineData("{a,d}{b,e}{c,f}{a,b}{c,d,e,f}")]   // #805: a pair and a 4-tuple
    [InlineData("{a}{b}{c}{q,r,s}")]               // #805: names in none of the sets
    [InlineData("{}{}{}")]                         // #805: empty sets
    public async Task Frames_UnreadableInstance_IsA400_NotAPicture(string instance) {
        var (status, body) = await Visualize("visualization=dm3visualization&format=frames", instance);
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("instance_parse_error", body);
    }

    // #745 on the real endpoints: fewer triples than X is no answer, {}, and the verifier says False to it.
    [Theory]
    [InlineData("{a}{b}{c}")]
    [InlineData("{a,b}{c,d}{e,f}{a,c,e}")]
    public async Task Solve_FewerTriplesThanX_IsBraces_AndVerifiesFalse(string instance) {
        var (status, body) = await Post("solve?solver=threedimensionalmatchingbruteforce", JsonSerializer.Serialize(instance));
        Assert.Equal((HttpStatusCode.OK, "\"{}\""), (status, body));
        var (vStatus, vBody) = await Post("verify?verifier=genericverifierdm3", JsonSerializer.Serialize(new { ProblemInstance = instance, Certificate = "{}" }));
        Assert.Equal((HttpStatusCode.OK, "\"False\""), (vStatus, vBody));
        var (fStatus, frames) = await Visualize("visualization=dm3visualization&format=frames", instance);
        Assert.Equal(HttpStatusCode.OK, fStatus);
        var done = Assert.Single(JsonDocument.Parse(frames).RootElement.GetProperty("frames").EnumerateArray());
        Assert.False(done.GetProperty("ok").GetBoolean());
    }
}

// Stopped runs, under the 0.05 s budget of TinyBudgetAppFactory.
[Collection(TimingCollection.Name)]
public class TripartiteFrames_StoppedRun_Endpoint_Tests : IClassFixture<TinyBudgetAppFactory> {
    private readonly HttpClient _client;

    public TripartiteFrames_StoppedRun_Endpoint_Tests(TinyBudgetAppFactory factory) {
        _client = factory.CreateClient();
    }

    // Ten elements per set and 40 triples, none of them using a0: no matching, and C(40, 10) = 847,660,528 sets to try.
    internal static readonly string NoA0 = "{" + string.Join(",", Enumerable.Range(0, 10).Select(i => $"a{i}")) + "}"
        + "{" + string.Join(",", Enumerable.Range(0, 10).Select(i => $"b{i}")) + "}"
        + "{" + string.Join(",", Enumerable.Range(0, 10).Select(i => $"c{i}")) + "}"
        + string.Concat(Enumerable.Range(0, 40).Select(i => $"{{a{1 + i % 9},b{i % 10},c{i * 3 % 10}}}"));

    [Fact]
    public async Task Frames_Stopped_EndsBlank_NotOk() {
        var sw = Stopwatch.StartNew();
        var content = new StringContent(JsonSerializer.Serialize(NoA0), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/visualize?visualization=dm3visualization&format=frames", content, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(20), $"took {sw.Elapsed}");
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal("TripartiteMatching", root.GetProperty("type").GetString());
        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        Assert.True(frames.Count >= 2);
        Assert.Equal("Reject", frames[0].GetProperty("event").GetString());
        var done = frames[^1];
        Assert.Single(frames, f => f.GetProperty("event").GetString() == "Done");
        Assert.Equal("Done", done.GetProperty("event").GetString());
        Assert.False(done.GetProperty("ok").GetBoolean());
        Assert.Matches(@"^Stopped after 0\.05 s and [\d,]+ steps?; the solver had not finished\.", done.GetProperty("caption").GetString());
        Assert.Empty(done.GetProperty("focus").EnumerateArray());
        Assert.Empty(done.GetProperty("chosen").EnumerateArray());
        Assert.Empty(done.GetProperty("repeats").EnumerateObject());
        Assert.Empty(done.GetProperty("elements").EnumerateObject());
        Assert.Empty(done.GetProperty("triples").EnumerateObject());
    }
}
