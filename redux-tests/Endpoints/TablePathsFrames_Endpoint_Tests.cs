using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Steps;
using API.Interfaces.Table;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Table picture's Dijkstra table for SSSP and SPSP, the second view of the runs the Layered Graph picture draws, on the
// actual /ProblemProvider/visualize?format=frames output of SSSPTableVisualization and SPSPTableVisualization. Their list
// format and /info are held to the snapshots taken before the Layered Graph picture (LayeredFrames_Endpoint_Tests), so that
// is not repeated here.
public class TablePathsFrames_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public TablePathsFrames_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private async Task<JsonElement> Visualize(string query, string instance) {
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/visualize?{query}", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement;
    }

    private static string Readable(JsonElement element) =>
        JsonSerializer.Serialize(element, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    private static string RowStates(JsonElement frame) =>
        string.Join(" ", frame.GetProperty("rows").EnumerateObject().Select(r => $"{r.Name}:{r.Value.GetProperty("state").GetString()}"));

    private static List<string> Column(JsonElement frame, int column) =>
        frame.GetProperty("values").EnumerateArray().Select(r => r[column].ValueKind == JsonValueKind.Null ? "null" : r[column].GetString()!).ToList();

    public static TheoryData<string, string, string> FramesCases {
        get {
            var data = new TheoryData<string, string, string>();
            var path = Path.Combine(AppContext.BaseDirectory, "Endpoints", "Snapshots", "layered_solver_answers.tsv");
            foreach (var line in File.ReadAllLines(path).Where(l => l.Length > 0)) {
                var parts = line.Split('\t');
                if (parts[0] is "ssspsolver" or "spspsolver") data.Add(parts[0], parts[1], JsonSerializer.Deserialize<string>(parts[2])!);
            }
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(FramesCases))]
    public async Task Frames_EveryRun_IsOneRowPerNode_AndEndsOnTheAnswer(string solver, string instance, string answer) {
        bool pair = solver == "spspsolver";
        var root = await Visualize($"visualization={(pair ? "spsp" : "sssp")}tablevisualization&format=frames", instance);
        Assert.Equal("Table", root.GetProperty("type").GetString());
        var payload = root.GetProperty("payload");
        Assert.Equal(["kind", "shape", "columns", "rows", "title"], payload.EnumerateObject().Select(p => p.Name));
        Assert.Equal("trace", payload.GetProperty("shape").GetString());
        Assert.Equal(["node", "settled", "cost", "via", "path"], payload.GetProperty("columns").EnumerateArray().Select(c => c.GetProperty("key").GetString()));
        int nodes = payload.GetProperty("rows").GetArrayLength();

        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        var settledOrder = new List<string>();
        foreach (var f in frames) {
            Assert.Equal(["event", "caption", "focus", "values", "rows", "cells", "changed", .. f.TryGetProperty("ok", out _) ? new[] { "ok" } : []],
                f.EnumerateObject().Select(p => p.Name));
            // Every node, every frame.
            Assert.Equal(nodes, f.GetProperty("values").GetArrayLength());
            Assert.Empty(f.GetProperty("cells").EnumerateObject());
            Assert.All(f.GetProperty("changed").EnumerateArray(), id => Assert.Matches(@"^r\d+c[23]$", id.GetString()));
            string ev = f.GetProperty("event").GetString()!;
            if (ev == "Accept") {
                // The settled node is the focus, and earlier settled rows stay Solution (Dijkstra is incremental).
                var focus = Assert.Single(f.GetProperty("focus").EnumerateArray()).GetString()!;
                Assert.Equal("ElementHighlight", f.GetProperty("rows").GetProperty(focus).GetProperty("state").GetString());
                Assert.All(settledOrder, r => Assert.Equal("Solution", f.GetProperty("rows").GetProperty(r).GetProperty("state").GetString()));
                Assert.Equal($"#{settledOrder.Count + 1}", f.GetProperty("values")[int.Parse(focus[1..])][1].GetString());
                settledOrder.Add(focus);
            }
        }
        var done = frames[^1];
        Assert.Equal("Done", done.GetProperty("event").GetString());
        bool ok = done.GetProperty("ok").GetBoolean();
        Assert.Equal(!pair || answer != "{}", ok);
        var names = Column(done, 0);
        var costs = Column(done, 2);
        if (!pair) {
            // Each node's path in the answer, "(5,{1,3,5})", is its Path cell; a node with no path is ∞ and Untraveled.
            foreach (var (node, i) in names.Select((n, i) => (n, i))) {
                var listed = System.Text.RegularExpressions.Regex.Match(answer, @"\(" + System.Text.RegularExpressions.Regex.Escape(node) + @",\{([^}]*)\}\)").Groups[1].Value;
                if (listed.Length == 0) {
                    Assert.Equal("∞", costs[i]);
                    Assert.Equal("Untraveled", done.GetProperty("rows").GetProperty($"r{i}").GetProperty("state").GetString());
                } else {
                    Assert.Equal(listed.Replace(",", " → "), Column(done, 4)[i]);
                    Assert.Equal("Solution", done.GetProperty("rows").GetProperty($"r{i}").GetProperty("state").GetString());
                }
            }
        } else if (ok) {
            var path = answer.Trim('{', '}').Split(',');
            Assert.Equal(path.Select(n => $"r{names.IndexOf(n)}").Order(StringComparer.Ordinal),
                done.GetProperty("rows").EnumerateObject().Where(r => r.Value.GetProperty("state").GetString() == "Solution").Select(r => r.Name));
            Assert.Equal(string.Join(" → ", path), Column(done, 4)[names.IndexOf(path[^1])]);
        } else {
            Assert.Contains("Rejected", RowStates(done));
            Assert.DoesNotContain("Solution", RowStates(done));
        }
    }

    [Fact]
    public async Task Frames_Sssp_AsTheMockupDrawsIt() {
        var root = await Visualize("visualization=sssptablevisualization&format=frames", "({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)},1)");
        Assert.Equal("Shortest paths from 1", root.GetProperty("payload").GetProperty("title").GetString());
        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        Assert.Equal(7, frames.Count);
        Assert.Equal(
            "{\"event\":\"Accept\",\"caption\":\"Settle 1 at distance 0. Shorter routes found: 2: ∞ → 4, 3: ∞ → 2.\",\"focus\":[\"r0\"],"
            + "\"values\":[[\"1\",\"#1\",\"0\",\"—\",\"1\"],[\"2\",\"not yet\",\"4\",\"1\",\"1 → 2\"],[\"3\",\"not yet\",\"2\",\"1\",\"1 → 3\"],"
            + "[\"4\",\"not yet\",\"∞\",\"—\",\"—\"],[\"5\",\"not yet\",\"∞\",\"—\",\"—\"]],"
            + "\"rows\":{\"r0\":{\"state\":\"ElementHighlight\"}},\"cells\":{},\"changed\":[\"r1c2\",\"r1c3\",\"r2c2\",\"r2c3\"]}",
            Readable(frames[1]));
        // The mockup's solved table: settled #1 #3 #2 #4 #5, costs 0 4 2 7 9.
        Assert.Equal(["#1", "#3", "#2", "#4", "#5"], Column(frames[^1], 1));
        Assert.Equal(["0", "4", "2", "7", "9"], Column(frames[^1], 2));
        Assert.Equal(["1", "1 → 2", "1 → 3", "1 → 2 → 4", "1 → 3 → 5"], Column(frames[^1], 4));
    }

    [Fact]
    public async Task Frames_Spsp_StopsAtTheTarget_AndMarksOnlyThePath() {
        var root = await Visualize("visualization=spsptablevisualization&format=frames", "({s,a,b,c,t},{((s,a),1),((a,t),2),((s,b),2),((b,c),5),((c,t),9)},s,t)");
        Assert.Equal("Shortest path from s to t", root.GetProperty("payload").GetProperty("title").GetString());
        var done = root.GetProperty("frames").EnumerateArray().Last();
        Assert.Equal(["#1", "#2", "#3", "not yet", "#4"], Column(done, 1));
        Assert.Equal("r0:Solution r1:Solution r4:Solution", RowStates(done));
        Assert.True(done.GetProperty("ok").GetBoolean());

        var none = (await Visualize("visualization=spsptablevisualization&format=frames", "({1,2,3},{((1,2),1)},1,3)")).GetProperty("frames").EnumerateArray().Last();
        Assert.Equal(["#1", "#2", "unreachable"], Column(none, 1));
        Assert.Equal("r2:Rejected", RowStates(none));
        Assert.False(none.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public void Frames_AStoppedRun_EndsWithTheNodesAndNothingWorkedOut() {
        // Dijkstra is too quick to stop through the endpoint, so the stopped run is built by hand, as SolveRun.Record makes it.
        string instance = "({1,2,3},{((1,2),1),((2,3),1)},1)";
        var start = new SolverStep<Distances>(new Distances([new("1", 0)], [], []), StepEvent.Try, [], "Start at 1 with distance 0. Every other node starts at ∞.");
        var stopped = new SolverStep<Distances>(new Distances([], [], []), StepEvent.Done, [], "Stopped after 0.05 s and 2 steps; the solver had not finished.") { Ok = false };
        var run = new SolveRun(SolveRun.TimedOutAnswer, [start, stopped], typeof(Distances)) { TimedOut = true, Incremental = true };
        var vis = (IFramesVisualization)Activator.CreateInstance(ProblemProvider.Visualizers["sssptablevisualization"])!;
        var frames = vis.BuildFrames(instance, run).Frames.Cast<TableFrame>().ToList();
        Assert.Equal(2, frames.Count);
        var done = frames[^1];
        Assert.Equal((StepEvent.Done, false), (done.Event, done.Ok));
        Assert.Equal([["1", null, null, null, null], ["2", null, null, null, null], ["3", null, null, null, null]], done.Values.Select(r => r.ToArray()));
        Assert.Empty(done.Rows);
        Assert.Empty(done.Changed);
    }
}
