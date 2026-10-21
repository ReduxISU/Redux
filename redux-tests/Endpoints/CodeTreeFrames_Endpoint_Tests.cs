using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Code Tree picture on the actual /ProblemProvider/visualize?format=frames output, for Lossless Data Compression and its
// solver, plus the guards that keep everything old frontends read exactly as it was:
//   - the list format of LosslessDataCompressionVisualization (Snapshots/codetree_list_snapshots.tsv, captured before the
//     picture existed),
//   - /info for the problem, its solver, its verifier and its visualization (info.<name>.json, captured the same way).
// The deliberate exception is the list format, which was empty (Unimplemented, answering []) and is now the code table
// (DynamicTable): its rows in codetree_list_snapshots.tsv and info.losslessdatacompression.json /
// info.losslessdatacompressionvisualization.json were retaken. See the "List" tests. The format=frames picture is unchanged.
public class CodeTreeFrames_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public CodeTreeFrames_Endpoint_Tests(AppFactory factory) {
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

    private static string Compact(string json) => JsonSerializer.Serialize(JsonDocument.Parse(json).RootElement);

    private static string Readable(JsonElement element) =>
        JsonSerializer.Serialize(element, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    private static Dictionary<string, string> StatesOf(JsonElement map) =>
        map.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetProperty("state").GetString()!);

    private static readonly HashSet<string> Vocabulary = new() {
        "Background", "ElementHighlight", "Solution", "Rejected", "Untraveled", "Covered", "Blocked",
    };

    public static TheoryData<string, string, string> FramesCases {
        get {
            var data = new TheoryData<string, string, string>();
            foreach (var (solver, instance, answer) in CodeTreeSolvers_Tests.Rows()) data.Add(solver, instance, answer);
            return data;
        }
    }

    public static TheoryData<string, string, string> ListCases {
        get {
            var data = new TheoryData<string, string, string>();
            foreach (var line in ReadSnapshot("codetree_list_snapshots.tsv").Split('\n').Where(l => l.Length > 0)) {
                var parts = line.Split('\t');
                data.Add(parts[0], JsonSerializer.Deserialize<string>(parts[1])!, parts[2]);
            }
            return data;
        }
    }

    public static TheoryData<string> TouchedInterfaces => new([
        "losslessdatacompressionsolver", "losslessdatacompressionverifier",
    ]);

    // ---- What old frontends read must not change. ----

    [Theory]
    [MemberData(nameof(TouchedInterfaces))]
    public async Task Info_IsUnchangedByTheCodeTreePicture(string name) {
        var response = await _client.GetAsync($"/ProblemProvider/info?interface={name}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ReadSnapshot($"info.{name}.json"), body.Replace("\r\n", "\n"));
        Assert.DoesNotContain("CodeTree", body);
        Assert.DoesNotContain("StepShape", body);
        Assert.DoesNotContain("Describe", body);
        Assert.DoesNotContain("ParseAnswer", body);
    }

    [Theory]
    [MemberData(nameof(ListCases))]
    public async Task List_MatchesPreChangeSnapshot(string vis, string instance, string expected) {
        var (status, body) = await Visualize($"visualization={vis}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(expected, Compact(body));
    }

    // /info for the problem and its visualization, retaken after the list format became the code table: the only difference
    // from the pre-change capture is the visualization's visualizationType (Unimplemented to DynamicTable) and its
    // visualizationDefinition (was "TODO").
    // Only a client that asks with includePictures=true is told DynamicTable: the view declares its legacy type
    // ([LegacyVisualizationType(Unimplemented)]), which Redux_GUI is still told (GuiView_Endpoint_Tests).
    [Theory]
    [InlineData("losslessdatacompression")]
    [InlineData("losslessdatacompressionvisualization")]
    public async Task List_Info_ReportsDynamicTable_AndNothingOfThePicture(string name) {
        var response = await _client.GetAsync($"/ProblemProvider/info?interface={name}&includePictures=true", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ReplaceLineEndings("\n");
        Assert.Equal(ReadSnapshot($"info.{name}.json"), body);
        var root = JsonDocument.Parse(body).RootElement;
        var vis = name == "losslessdatacompression" ? root.GetProperty("defaultVisualization") : root;
        Assert.Equal("DynamicTable", vis.GetProperty("visualizationType").GetString());
        Assert.DoesNotContain("\"CodeTree\"", body);
        Assert.DoesNotContain("StepShape", body);
    }

    [Fact]
    public async Task List_IsTheCodeTableBeforeAndAfter() {
        var (status, body) = await Visualize("visualization=losslessdatacompressionvisualization", "abracadabra");
        Assert.Equal(HttpStatusCode.OK, status);
        var list = JsonDocument.Parse(body).RootElement.EnumerateArray().ToList();
        Assert.Equal(2, list.Count);
        Assert.All(list, t => Assert.Equal("table", t.GetProperty("kind").GetString()));
        Assert.Equal(["Character", "Count", "Code", "Bits"], list[0].GetProperty("columns").EnumerateArray().Select(c => c.GetProperty("label").GetString()));
        Assert.Equal("11 characters, 5 different", list[0].GetProperty("title").GetString());
        var before = list[0].GetProperty("rows").EnumerateArray().ToList();
        Assert.Equal(["a", "b", "c", "d", "r"], before.Select(r => r.GetProperty("cells").GetProperty("symbol").GetString()));
        Assert.Equal(["5", "2", "1", "1", "2"], before.Select(r => r.GetProperty("cells").GetProperty("count").GetString()));
        Assert.All(before, r => Assert.Equal("", r.GetProperty("cells").GetProperty("code").GetString()));
        // After: every character has its code, and the bits add up to the title's total.
        var after = list[1].GetProperty("rows").EnumerateArray().ToList();
        Assert.All(after, r => Assert.Equal("Solution", r.GetProperty("color").GetString()));
        Assert.All(after, r => Assert.Equal(int.Parse(r.GetProperty("cells").GetProperty("count").GetString()!) * r.GetProperty("cells").GetProperty("code").GetString()!.Length,
            int.Parse(r.GetProperty("cells").GetProperty("bits").GetString()!)));
        int total = after.Sum(r => int.Parse(r.GetProperty("cells").GetProperty("bits").GetString()!));
        Assert.Equal($"11 characters, 5 different, encoded in {total} bits", list[1].GetProperty("title").GetString());
        Assert.Equal("1", after[0].GetProperty("cells").GetProperty("code").GetString()!.Length.ToString());
    }

    // ---- The picture ----

    [Theory]
    [MemberData(nameof(FramesCases))]
    public async Task Frames_EveryText_HasTheCodeTreePayloadAndFramesThatAgreeWithTheAnswer(string solver, string instance, string answer) {
        var (status, body) = await Visualize($"visualization={CodeTreeSolvers_Tests.Visualization}&format=frames&solver={solver}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal(["type", "payload", "frames"], root.EnumerateObject().Select(p => p.Name));
        Assert.Equal("CodeTree", root.GetProperty("type").GetString());
        var payload = root.GetProperty("payload");
        Assert.Equal(["kind", "text", "symbols"], payload.EnumerateObject().Select(p => p.Name));
        Assert.Equal("codeTree", payload.GetProperty("kind").GetString());
        Assert.Equal(instance, payload.GetProperty("text").GetString());
        var symbols = payload.GetProperty("symbols").EnumerateArray().ToList();
        Assert.All(symbols, (s, i) => {
            Assert.Equal(["id", "code", "label", "count"], s.EnumerateObject().Select(p => p.Name));
            Assert.Equal($"c{i + 1}", s.GetProperty("id").GetString());
            Assert.Equal(instance.Count(c => c == s.GetProperty("code").GetInt32()), s.GetProperty("count").GetInt32());
        });
        Assert.Equal(instance.Length, symbols.Sum(s => s.GetProperty("count").GetInt32()));
        var leafIds = symbols.Select(s => s.GetProperty("id").GetString()!).ToHashSet();

        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        var done = Assert.Single(frames, f => f.GetProperty("event").GetString() == "Done");
        Assert.Equal(done, frames[^1]);
        Assert.All(frames, f => Assert.False(string.IsNullOrWhiteSpace(f.GetProperty("caption").GetString())));
        Assert.All(frames.SkipLast(1), f => Assert.False(f.TryGetProperty("ok", out _)));
        foreach (var f in frames) {
            bool hasBits = f.TryGetProperty("bits", out _);
            Assert.Equal(["event", "caption", "focus", "merged", "roots", "codes", "nodes", "edges", .. hasBits ? new[] { "bits" } : [], .. f.TryGetProperty("ok", out _) ? new[] { "ok" } : []],
                f.EnumerateObject().Select(p => p.Name));
            var merged = f.GetProperty("merged").EnumerateArray().ToList();
            var ids = leafIds.Concat(merged.Select(m => m.GetProperty("id").GetString()!)).ToHashSet();
            Assert.All(merged, (m, k) => {
                Assert.Equal(["id", "weight", "left", "right"], m.EnumerateObject().Select(p => p.Name));
                Assert.Equal($"n{k + 1}", m.GetProperty("id").GetString());
            });
            var nodes = StatesOf(f.GetProperty("nodes"));
            var edges = StatesOf(f.GetProperty("edges"));
            Assert.All(nodes, s => { Assert.Contains(s.Key, ids); Assert.Contains(s.Value, Vocabulary); });
            var edgeIds = merged.SelectMany(m => new[] { m.GetProperty("id").GetString() + ".0", m.GetProperty("id").GetString() + ".1" }).ToHashSet();
            Assert.Equal(edgeIds.Order(), edges.Keys.Order());
            // Incremental: what is merged is Solution; only the two trees being merged, their parent and its edges are highlighted.
            var focus = f.GetProperty("focus").EnumerateArray().Select(x => x.GetString()!).ToHashSet();
            foreach (var m in merged) {
                string id = m.GetProperty("id").GetString()!;
                foreach (var n in new[] { id, m.GetProperty("left").GetString()!, m.GetProperty("right").GetString()! })
                    Assert.Equal(focus.Contains(n) ? "ElementHighlight" : "Solution", nodes[n]);
                Assert.Equal(focus.Contains(id) && focus.Contains(m.GetProperty("left").GetString()!) ? "ElementHighlight" : "Solution", edges[id + ".0"]);
                Assert.Equal(focus.Contains(id) && focus.Contains(m.GetProperty("right").GetString()!) ? "ElementHighlight" : "Solution", edges[id + ".1"]);
            }
            // The trees left: each leaf and merged node no merge has used yet, once.
            var used = merged.SelectMany(m => new[] { m.GetProperty("left").GetString()!, m.GetProperty("right").GetString()! }).ToHashSet();
            Assert.Equal(ids.Except(used).Order(), f.GetProperty("roots").EnumerateArray().Select(x => x.GetString()!).Order());
            Assert.Equal(f.GetProperty("codes").EnumerateObject().Any(), hasBits);
        }
        Assert.True(done.GetProperty("ok").GetBoolean());
        if (instance.Length == 0) {
            Assert.Equal("()", answer);
            return;
        }
        // The Done frame's codes are the answer's, and its bits the encoded text's length.
        var table = answer[2..answer.LastIndexOf('}')].Split("),(").Select(e => e.Trim('(', ')').Split(','))
            .ToDictionary(e => int.Parse(e[0]), e => e[1]);
        Assert.Equal(symbols.Select(s => table[s.GetProperty("code").GetInt32()]),
            symbols.Select(s => done.GetProperty("codes").GetProperty(s.GetProperty("id").GetString()!).GetString()));
        Assert.Equal(answer[(answer.LastIndexOf(',') + 1)..^1].Length, done.GetProperty("bits").GetInt64());
        Assert.All(leafIds, id => Assert.Equal("Solution", StatesOf(done.GetProperty("nodes"))[id]));
    }

    [Fact]
    public async Task Frames_Abracadabra_MatchesTheMockup() {
        var (_, body) = await Visualize("visualization=losslessdatacompressionvisualization&format=frames", "abracadabra");
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal(
            "{\"kind\":\"codeTree\",\"text\":\"abracadabra\",\"symbols\":[{\"id\":\"c1\",\"code\":97,\"label\":\"a\",\"count\":5},{\"id\":\"c2\",\"code\":98,\"label\":\"b\",\"count\":2},"
            + "{\"id\":\"c3\",\"code\":99,\"label\":\"c\",\"count\":1},{\"id\":\"c4\",\"code\":100,\"label\":\"d\",\"count\":1},{\"id\":\"c5\",\"code\":114,\"label\":\"r\",\"count\":2}]}",
            Readable(root.GetProperty("payload")));
        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        Assert.Equal(7, frames.Count);
        Assert.Equal(
            "{\"event\":\"Try\",\"caption\":\"Merge b (2) and the tree of weight 2 (c and d), the two lightest trees left, into a node of weight 4. (A tie: the tree holding the earliest character goes first.)\","
            + "\"focus\":[\"c2\",\"n1\",\"n2\"],\"merged\":[{\"id\":\"n1\",\"weight\":2,\"left\":\"c3\",\"right\":\"c4\"},{\"id\":\"n2\",\"weight\":4,\"left\":\"c2\",\"right\":\"n1\"}],"
            + "\"roots\":[\"c5\",\"n2\",\"c1\"],\"codes\":{},"
            + "\"nodes\":{\"c2\":{\"state\":\"ElementHighlight\"},\"c3\":{\"state\":\"Solution\"},\"c4\":{\"state\":\"Solution\"},\"n1\":{\"state\":\"ElementHighlight\"},\"n2\":{\"state\":\"ElementHighlight\"}},"
            + "\"edges\":{\"n1.0\":{\"state\":\"Solution\"},\"n1.1\":{\"state\":\"Solution\"},\"n2.0\":{\"state\":\"ElementHighlight\"},\"n2.1\":{\"state\":\"ElementHighlight\"}}}",
            Readable(frames[2]));
        // The mockup's solved view: a gets the 1-bit code, the encoded text is 23 bits instead of 88.
        var done = frames[^1];
        Assert.Equal("{\"c1\":\"0\",\"c2\":\"110\",\"c3\":\"1110\",\"c4\":\"1111\",\"c5\":\"10\"}", Readable(done.GetProperty("codes")));
        Assert.Equal(23, done.GetProperty("bits").GetInt32());
        Assert.Equal("Encoded: 23 bits, versus 88 bits at 8 bits per character, 74% smaller. No code is the start of another, so the bits decode back to exactly the text.",
            done.GetProperty("caption").GetString());
        Assert.True(done.GetProperty("ok").GetBoolean());
    }
}

// Stopped runs, under the 0.05 s budget of TinyBudgetAppFactory.
[Collection(TimingCollection.Name)]
public class CodeTreeFrames_StoppedRun_Endpoint_Tests : IClassFixture<TinyBudgetAppFactory> {
    private readonly HttpClient _client;

    public CodeTreeFrames_StoppedRun_Endpoint_Tests(TinyBudgetAppFactory factory) {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Frames_Stopped_EndsBlank_NotOk() {
        // 4,000 different characters: the solver re-sorts what is left before each of its 3,999 merges, far longer than 0.05 s.
        string text = new string(Enumerable.Range(0, 4000).Select(i => (char)(0x4E00 + i)).ToArray());
        var sw = Stopwatch.StartNew();
        var content = new StringContent(JsonSerializer.Serialize(text), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/visualize?visualization=losslessdatacompressionvisualization&format=frames", content, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(20), $"took {sw.Elapsed}");
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal("CodeTree", root.GetProperty("type").GetString());
        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        Assert.True(frames.Count >= 2);
        Assert.Equal("Try", frames[0].GetProperty("event").GetString());
        var done = frames[^1];
        Assert.Single(frames, f => f.GetProperty("event").GetString() == "Done");
        Assert.False(done.GetProperty("ok").GetBoolean());
        Assert.Matches(@"^Stopped after 0\.05 s and [\d,]+ steps?; the solver had not finished\.", done.GetProperty("caption").GetString());
        Assert.Empty(done.GetProperty("focus").EnumerateArray());
        Assert.Empty(done.GetProperty("merged").EnumerateArray());
        Assert.Empty(done.GetProperty("roots").EnumerateArray());
        Assert.Empty(done.GetProperty("codes").EnumerateObject());
        Assert.Empty(done.GetProperty("nodes").EnumerateObject());
        Assert.Empty(done.GetProperty("edges").EnumerateObject());
        Assert.False(done.TryGetProperty("bits", out _));
    }
}
