using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Geometry picture on the actual /ProblemProvider/visualize?format=frames output, for Convex Hull and its solver, plus the
// guards that keep everything old frontends read exactly as it was:
//   - the list format of ConvexHullVisualization (Snapshots/geometry_list_snapshots.tsv, captured before the picture existed),
//   - /info for the problem, its solver, its verifier and its visualization (info.<name>.json, captured the same way).
// The deliberate exception is the list format, which was empty (Unimplemented, answering []) and is now a table of the points
// (DynamicTable): its rows in geometry_list_snapshots.tsv and info.convexhull.json / info.convexhullvisualization.json were
// retaken. See the "List" tests. The format=frames picture is unchanged.
public class GeometryFrames_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public GeometryFrames_Endpoint_Tests(AppFactory factory) {
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
        "Background", "ElementHighlight", "Solution", "Rejected", "Untraveled", "Covered", "Blocked", "Committed",
    };

    public static TheoryData<string, string, string> FramesCases {
        get {
            var data = new TheoryData<string, string, string>();
            foreach (var (solver, instance, answer) in GeometrySolvers_Tests.Rows()) data.Add(solver, instance, answer);
            return data;
        }
    }

    public static TheoryData<string, string, string> ListCases {
        get {
            var data = new TheoryData<string, string, string>();
            foreach (var line in ReadSnapshot("geometry_list_snapshots.tsv").Split('\n').Where(l => l.Length > 0)) {
                var parts = line.Split('\t');
                data.Add(parts[0], JsonSerializer.Deserialize<string>(parts[1])!, parts[2]);
            }
            return data;
        }
    }

    public static TheoryData<string> TouchedInterfaces => new(["convexhullsolver", "convexhullverifier"]);

    // ---- What old frontends read must not change. ----

    [Theory]
    [MemberData(nameof(TouchedInterfaces))]
    public async Task Info_IsUnchangedByTheGeometryPicture(string name) {
        var response = await _client.GetAsync($"/ProblemProvider/info?interface={name}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ReadSnapshot($"info.{name}.json"), body.Replace("\r\n", "\n"));
        Assert.DoesNotContain("\"Geometry\"", InfoJson.WithoutPictureType(body));
        Assert.DoesNotContain("StepShape", body);
        Assert.DoesNotContain("Describe", body);
        Assert.DoesNotContain("ParseAnswer", body);
        Assert.DoesNotContain("ParseHull", body);
    }

    [Theory]
    [MemberData(nameof(ListCases))]
    public async Task List_MatchesPreChangeSnapshot(string vis, string instance, string expected) {
        var (status, body) = await Visualize($"visualization={vis}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(expected, Compact(body));
    }

    // /info for Convex Hull and its visualization, retaken after the list format became a table: the only difference from
    // the pre-change capture is the visualization's visualizationType (Unimplemented to DynamicTable) and its
    // visualizationDefinition (was "TODO").
    [Theory]
    [InlineData("convexhull")]
    [InlineData("convexhullvisualization")]
    public async Task List_Info_ReportsDynamicTable_AndNothingOfThePicture(string name) {
        var response = await _client.GetAsync($"/ProblemProvider/info?interface={name}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ReplaceLineEndings("\n");
        Assert.Equal(ReadSnapshot($"info.{name}.json"), body);
        var root = JsonDocument.Parse(body).RootElement;
        var vis = name == "convexhull" ? root.GetProperty("defaultVisualization") : root;
        Assert.Equal("DynamicTable", vis.GetProperty("visualizationType").GetString());
        Assert.DoesNotContain("\"Geometry\"", InfoJson.WithoutPictureType(body));
        Assert.DoesNotContain("StepShape", body);
        Assert.DoesNotContain("ParseHull", body);
    }

    [Fact]
    public async Task List_IsThePointsBeforeAndAfter_TheCornersNumberedCounterclockwise() {
        var (status, body) = await Visualize("visualization=convexhullvisualization", "{(0,0),(2,0),(1,1),(2,2),(1,0),(0,2)}");
        Assert.Equal(HttpStatusCode.OK, status);
        var list = JsonDocument.Parse(body).RootElement.EnumerateArray().ToList();
        Assert.Equal(2, list.Count);
        Assert.All(list, t => Assert.Equal("table", t.GetProperty("kind").GetString()));
        Assert.Equal("6 points", list[0].GetProperty("title").GetString());
        Assert.Equal(["Point", "x", "y", "Hull corner"], list[0].GetProperty("columns").EnumerateArray().Select(c => c.GetProperty("label").GetString()));
        Assert.All(list[0].GetProperty("rows").EnumerateArray(), r => Assert.Equal("", r.GetProperty("cells").GetProperty("corner").GetString()));
        // The square's corners counterclockwise from (0,0); the centre and the point on the bottom edge are not corners.
        var rows = list[1].GetProperty("rows").EnumerateArray().ToList();
        Assert.Equal(["1", "2", "", "3", "", "4"], rows.Select(r => r.GetProperty("cells").GetProperty("corner").GetString()));
        Assert.Equal(["Solution", "Solution", null, "Solution", null, "Solution"],
            rows.Select(r => r.GetProperty("color").ValueKind == JsonValueKind.Null ? null : r.GetProperty("color").GetString()));
    }

    // ---- The picture ----

    [Theory]
    [MemberData(nameof(FramesCases))]
    public async Task Frames_EveryInstance_HasTheGeometryPayloadAndFramesThatAgreeWithTheAnswer(string solver, string instance, string answer) {
        var (status, body) = await Visualize($"visualization={GeometrySolvers_Tests.Visualization}&format=frames&solver={solver}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal(["type", "payload", "frames"], root.EnumerateObject().Select(p => p.Name));
        Assert.Equal("Geometry", root.GetProperty("type").GetString());
        var payload = root.GetProperty("payload");
        Assert.Equal(["kind", "points"], payload.EnumerateObject().Select(p => p.Name));
        Assert.Equal("geometry", payload.GetProperty("kind").GetString());
        var points = payload.GetProperty("points").EnumerateArray().ToList();
        Assert.All(points, (p, i) => {
            Assert.Equal(["id", "x", "y"], p.EnumerateObject().Select(x => x.Name));
            Assert.Equal($"P{i + 1}", p.GetProperty("id").GetString());
        });
        var byId = points.ToDictionary(p => p.GetProperty("id").GetString()!, p => (p.GetProperty("x").GetDouble(), p.GetProperty("y").GetDouble()));

        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        var done = Assert.Single(frames, f => f.GetProperty("event").GetString() == "Done");
        Assert.Equal(done, frames[^1]);
        Assert.All(frames, f => Assert.False(string.IsNullOrWhiteSpace(f.GetProperty("caption").GetString())));
        Assert.All(frames.SkipLast(1), f => Assert.False(f.TryGetProperty("ok", out _)));
        for (int i = 0; i < frames.Count; i++) {
            var f = frames[i];
            Assert.Equal(["event", "caption", "focus", "pieces", "chain", "hull", "points", .. f.TryGetProperty("ok", out _) ? new[] { "ok" } : []],
                f.EnumerateObject().Select(p => p.Name));
            var states = StatesOf(f.GetProperty("points"));
            Assert.All(states, s => { Assert.Contains(s.Key, byId.Keys); Assert.Contains(s.Value, Vocabulary); });
            Assert.All(f.GetProperty("focus").EnumerateArray(), id => Assert.Contains(id.GetString()!, byId.Keys));
            var pieces = f.GetProperty("pieces").EnumerateArray().ToList();
            Assert.All(pieces, p => {
                Assert.Equal(["points", "role"], p.EnumerateObject().Select(x => x.Name));
                Assert.Contains(p.GetProperty("role").GetString(), new[] { "piece", "left", "right" });
            });
            // Before the Done the hulls on the table and the chain are what the solver has kept so far: Committed (#849, #886),
            // unless they are the point under test (highlighted) or popped (Rejected). The Accept that ends the run (the last
            // merge, right before a correct Done) is the confirmed hull, so Solution.
            string ev = f.GetProperty("event").GetString()!;
            if (ev == "Done") continue;
            bool endsRun = ev == "Accept" && frames[i + 1].GetProperty("event").GetString() == "Done";
            string kept = endsRun ? "Solution" : "Committed";
            Assert.Empty(f.GetProperty("hull").EnumerateArray());
            var focus = f.GetProperty("focus").EnumerateArray().Select(x => x.GetString()!).ToHashSet();
            foreach (var id in pieces.SelectMany(p => p.GetProperty("points").EnumerateArray()).Concat(f.GetProperty("chain").EnumerateArray()).Select(x => x.GetString()!))
                Assert.Equal(focus.Contains(id) ? (ev == "Reject" ? "Rejected" : "ElementHighlight") : kept, states[id]);
            if (!endsRun) Assert.DoesNotContain("Solution", states.Values);
        }
        // The Done frame draws the answer's corners in order: Solution, every other point Covered.
        Assert.True(done.GetProperty("ok").GetBoolean());
        var hull = done.GetProperty("hull").EnumerateArray().Select(x => x.GetString()!).ToList();
        var corners = answer.Trim('(', ')').Split("), (", StringSplitOptions.RemoveEmptyEntries)
            .Select(c => c.Trim('(', ')').Split(',')).Select(c => (double.Parse(c[0], System.Globalization.CultureInfo.InvariantCulture), double.Parse(c[1], System.Globalization.CultureInfo.InvariantCulture))).ToList();
        Assert.Equal(corners, hull.Select(id => byId[id]));
        var doneStates = StatesOf(done.GetProperty("points"));
        Assert.All(byId.Keys, id => Assert.Equal(hull.Count == 0 ? null : corners.Contains(byId[id]) ? "Solution" : "Covered", doneStates.GetValueOrDefault(id)));
    }

    [Fact]
    public async Task Frames_SquareWithPointsInside_MatchesTheMockup() {
        var (_, body) = await Visualize("visualization=convexhullvisualization&format=frames", GeometrySolvers_Tests.SquareInside);
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal(
            "{\"kind\":\"geometry\",\"points\":[{\"id\":\"P1\",\"x\":0,\"y\":0},{\"id\":\"P2\",\"x\":4,\"y\":0},{\"id\":\"P3\",\"x\":4,\"y\":4},{\"id\":\"P4\",\"x\":0,\"y\":4},"
            + "{\"id\":\"P5\",\"x\":1,\"y\":1},{\"id\":\"P6\",\"x\":2,\"y\":3},{\"id\":\"P7\",\"x\":3,\"y\":2},{\"id\":\"P8\",\"x\":2,\"y\":2},{\"id\":\"P9\",\"x\":1,\"y\":3}]}",
            Readable(root.GetProperty("payload")));
        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        Assert.Equal(
            "{\"event\":\"Try\",\"caption\":\"Sort the 9 points by x, then y: P1, P4, P5, P9, P8, P6, P7, P2 and P3. Then split the list in half, again and again, until each piece has at most 3 points.\","
            + "\"focus\":[],\"pieces\":[],\"chain\":[],\"hull\":[],\"points\":{}}",
            Readable(frames[0]));
        // A pop during the first merge: the corner under test is Rejected, the chain and both hulls stay Committed.
        var pop = frames.First(f => f.GetProperty("event").GetString() == "Reject");
        Assert.Equal(
            "{\"event\":\"Reject\",\"caption\":\"The turn at P4 (from P1 to P5) is clockwise, so pop P4 from the bottom edge.\",\"focus\":[\"P4\"],"
            + "\"pieces\":[{\"points\":[\"P1\",\"P4\"],\"role\":\"left\"},{\"points\":[\"P5\",\"P9\"],\"role\":\"right\"}],\"chain\":[\"P1\"],\"hull\":[],"
            + "\"points\":{\"P1\":{\"state\":\"Committed\"},\"P4\":{\"state\":\"Rejected\"},\"P5\":{\"state\":\"Committed\"},\"P9\":{\"state\":\"Committed\"}}}",
            Readable(pop));
        // The mockup's solved view: the square's four corners in order, the five points inside Covered.
        Assert.Equal(
            "{\"event\":\"Done\",\"caption\":\"Done. The hull has 4 corners, listed counterclockwise from P1: P1, P2, P3 and P4. The other 5 points lie inside it or on an edge.\","
            + "\"focus\":[],\"pieces\":[],\"chain\":[],\"hull\":[\"P1\",\"P2\",\"P3\",\"P4\"],"
            + "\"points\":{\"P1\":{\"state\":\"Solution\"},\"P2\":{\"state\":\"Solution\"},\"P3\":{\"state\":\"Solution\"},\"P4\":{\"state\":\"Solution\"},"
            + "\"P5\":{\"state\":\"Covered\"},\"P6\":{\"state\":\"Covered\"},\"P7\":{\"state\":\"Covered\"},\"P8\":{\"state\":\"Covered\"},\"P9\":{\"state\":\"Covered\"}},\"ok\":true}",
            Readable(frames[^1]));
    }

    [Theory]
    [InlineData("{(0,0),(1,0),(NaN,1),(0,1)}")]     // #806
    [InlineData("{(0,0),(1,0),(1e400,1),(0,1)}")]   // #806: read as infinity
    [InlineData("{(0,0),(1,x),(2,2)}")]
    public async Task Frames_UnreadableInstance_IsA400_NotAPicture(string instance) {
        var (status, body) = await Visualize("visualization=convexhullvisualization&format=frames", instance);
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("instance_parse_error", body);
    }
}

// Stopped runs, under the 0.05 s budget of TinyBudgetAppFactory.
[Collection(TimingCollection.Name)]
public class GeometryFrames_StoppedRun_Endpoint_Tests : IClassFixture<TinyBudgetAppFactory> {
    private readonly HttpClient _client;

    public GeometryFrames_StoppedRun_Endpoint_Tests(TinyBudgetAppFactory factory) {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Frames_Stopped_EndsBlank_NotOk() {
        // 40,000 points on a parabola: every point is a corner, and recording the merges' pushes and pops takes 0.4 to 0.8 s
        // alone, far longer than 0.05 s. The clock is read only on a step, and the verdict after the last step is not one, so
        // a run whose steps all fit in 0.05 s finishes; 3,000 points took only 0.05 to 0.1 s and sometimes did (#837).
        string instance = "{" + string.Join(",", Enumerable.Range(0, 40000).Select(i => $"({i},{(long)i * i})")) + "}";
        var sw = Stopwatch.StartNew();
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/visualize?visualization=convexhullvisualization&format=frames", content, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(20), $"took {sw.Elapsed}");
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal("Geometry", root.GetProperty("type").GetString());
        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        Assert.True(frames.Count >= 2);
        Assert.Equal("Try", frames[0].GetProperty("event").GetString());
        var done = frames[^1];
        Assert.Single(frames, f => f.GetProperty("event").GetString() == "Done");
        Assert.False(done.GetProperty("ok").GetBoolean());
        Assert.Matches(@"^Stopped after 0\.05 s and [\d,]+ steps?; the solver had not finished\.", done.GetProperty("caption").GetString());
        Assert.Empty(done.GetProperty("focus").EnumerateArray());
        Assert.Empty(done.GetProperty("pieces").EnumerateArray());
        Assert.Empty(done.GetProperty("chain").EnumerateArray());
        Assert.Empty(done.GetProperty("hull").EnumerateArray());
        Assert.Empty(done.GetProperty("points").EnumerateObject());
    }
}
