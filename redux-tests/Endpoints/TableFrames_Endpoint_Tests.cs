using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Table picture on the actual /ProblemProvider/visualize?format=frames output, for Edit Distance and 0-1 Integer Linear
// Programming with their solvers, plus the guard that keeps what old frontends read exactly as it was: /info for the two
// problems and their solvers (info.<name>.json, captured before the picture existed).
// The deliberate exception is the two problems' default visualization, which moved off DummyVisualization to their Table
// visualizations: see the "rewire" tests.
public class TableFrames_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public TableFrames_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private static string ReadSnapshot(string file) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Endpoints", "Snapshots", file)).Replace("\r\n", "\n");

    private async Task<(HttpStatusCode Status, string Body)> Visualize(string query, string instance) {
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/visualize?{query}", content, TestContext.Current.CancellationToken);
        return (response.StatusCode, (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Replace("\r\n", "\n"));
    }

    private static string Compact(string json) => JsonSerializer.Serialize(JsonDocument.Parse(json).RootElement);

    private static string Readable(JsonElement element) =>
        JsonSerializer.Serialize(element, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    private static readonly HashSet<string> Vocabulary = new() {
        "Background", "ElementHighlight", "Solution", "Rejected", "Untraveled", "Covered", "Blocked",
    };

    public static TheoryData<string, string, string> FramesCases {
        get {
            var data = new TheoryData<string, string, string>();
            foreach (var (solver, instance, answer) in TableSolvers_Tests.Rows()) data.Add(solver, instance, answer);
            return data;
        }
    }

    public static TheoryData<string> TouchedInterfaces => new([
        "editdistancedpsolver", "integerprogrammingbruteforce",
    ]);

    // ---- What old frontends read must not change. ----

    [Theory]
    [MemberData(nameof(TouchedInterfaces))]
    public async Task Info_IsUnchangedByTheTablePicture(string name) {
        var response = await _client.GetAsync($"/ProblemProvider/info?interface={name}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ReadSnapshot($"info.{name}.json"), body.Replace("\r\n", "\n"));
        Assert.DoesNotContain("\"Table\"", body);
        Assert.DoesNotContain("StepShape", body);
    }

    // ---- Edit Distance and 0-1 ILP moved off DummyVisualization: the only thing that may change. ----

    // What embeds one of their defaultVisualizations: the two problems and the reduction into 0-1 ILP (3SAT to 0-1 ILP).
    // No reduction goes into or out of Edit Distance. Their snapshots were taken after the rewire; the diff against the
    // pre-change capture is only the nested defaultVisualization object (and reduce's visualization).
    public static TheoryData<string> EmbedsATableDefault => new() { "editdistance", "intprogramming01", "karpintprogstandard" };

    private const string Sat3Default = "(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | !x1)";

    [Theory]
    [MemberData(nameof(EmbedsATableDefault))]
    public async Task Rewire_Info_ShowsTheTableVisualizations_AndNothingElse(string name) {
        var response = await _client.GetAsync($"/ProblemProvider/info?interface={name}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Replace("\r\n", "\n");
        Assert.Equal(ReadSnapshot($"info.{name}.json"), body);
        Assert.DoesNotContain("\"Table\"", body);
        Assert.DoesNotContain("StepShape", body);
        Assert.DoesNotContain("DummyVisualization", body);
    }

    [Theory]
    [InlineData("editdistance", "editdistancevisualization", "Edit Distance Table Visualization", "Edit Distance Dynamic Programming")]
    [InlineData("intprogramming01", "intprogrammingvisualization", "0-1 Integer Programming Table Visualization", "0-1 ILP Brute Force")]
    public async Task Rewire_ProblemInfo_ReportsItsTableVisualization(string problem, string vis, string name, string solver) {
        var response = await _client.GetAsync($"/ProblemProvider/info?interface={problem}", TestContext.Current.CancellationToken);
        var nested = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement.GetProperty("defaultVisualization");
        Assert.Equal(name, nested.GetProperty("visualizationName").GetString());
        Assert.Equal("DynamicTable", nested.GetProperty("visualizationType").GetString());
        Assert.Equal(solver, nested.GetProperty("solver").GetProperty("solverName").GetString());
        // The same object the visualization reports about itself.
        var own = await _client.GetAsync($"/ProblemProvider/info?interface={vis}", TestContext.Current.CancellationToken);
        Assert.Equal(Compact(await own.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)), Compact(nested.GetRawText()));
    }

    [Fact]
    public async Task Rewire_Reduce_Sat3ToIlp_ChangesOnlyTheNestedVisualization() {
        var content = new StringContent(JsonSerializer.Serialize(Sat3Default), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/reduce?reduction=karpintprogstandard", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ReadSnapshot("karpintprogstandard.reduce.json"), (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Replace("\r\n", "\n"));
    }

    // Before the rewire 0-1 ILP's visualization was DummyVisualization, so visualizeReduction answered an empty list (#755).
    // Now it answers the matrix before and after, the mapped assignment (1 1 0) meeting every row.
    [Fact]
    public async Task Rewire_VisualizeReduction_Sat3ToIlp_NowReturnsTheMatrix() {
        var content = new StringContent(JsonSerializer.Serialize(Sat3Default), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/visualizeReduction?reduction=karpintprogstandard&solution=" + Uri.EscapeDataString("(x1:True,x2:True,x3:False)"), content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Replace("\r\n", "\n");
        Assert.Equal(ReadSnapshot("karpintprogstandard.visualizereduction.json"), body);
        var items = JsonDocument.Parse(body).RootElement.EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.All(items, i => Assert.Equal("table", i.GetProperty("kind").GetString()));
        Assert.All(items[1].GetProperty("rows").EnumerateArray().Skip(1), r => Assert.Equal("Solution", r.GetProperty("color").GetString()));
    }

    // The new visualizations report their legacy list type in /info and nothing of the picture.
    [Theory]
    [InlineData("editdistancevisualization", "Edit Distance Dynamic Programming")]
    [InlineData("intprogrammingvisualization", "0-1 ILP Brute Force")]
    public async Task NewVisualizations_Info_IsTheListTypeOnly(string name, string solver) {
        var response = await _client.GetAsync($"/ProblemProvider/info?interface={name}", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal(["visualizationName", "visualizationDefinition", "source", "sourceFile", "contributors", "visualizationType", "solver"],
            root.EnumerateObject().Select(p => p.Name));
        Assert.Equal("DynamicTable", root.GetProperty("visualizationType").GetString());
        Assert.Equal(solver, root.GetProperty("solver").GetProperty("solverName").GetString());
        Assert.DoesNotContain("\"Table\"", body);
        Assert.DoesNotContain("Describe", body);
        Assert.DoesNotContain("Blank", body);
    }

    [Fact]
    public async Task NewVisualizations_ListFormat_IsTheTableBeforeAndAfter() {
        var (status, body) = await Visualize("visualization=intprogrammingvisualization", "(1 -1)<=(-1)");
        Assert.Equal(HttpStatusCode.OK, status);
        var list = JsonDocument.Parse(body).RootElement.EnumerateArray().ToList();
        Assert.Equal(2, list.Count);
        Assert.Equal(
            "{\"kind\":\"table\",\"title\":null,\"columns\":[{\"key\":\"row\",\"label\":\"\"},{\"key\":\"x1\",\"label\":\"x1\"},{\"key\":\"x2\",\"label\":\"x2\"},"
            + "{\"key\":\"total\",\"label\":\"Total\"},{\"key\":\"bound\",\"label\":\"Bound\"},{\"key\":\"holds\",\"label\":\"Holds?\"}],"
            + "\"rows\":[{\"id\":\"r0\",\"color\":null,\"cells\":{\"row\":\"x =\",\"x1\":\"0\",\"x2\":\"1\",\"total\":\"\",\"bound\":\"\",\"holds\":\"\"},\"cellColors\":{\"x1\":\"Solution\",\"x2\":\"Solution\"}},"
            + "{\"id\":\"r1\",\"color\":\"Solution\",\"cells\":{\"row\":\"row 1\",\"x1\":\"1\",\"x2\":\"-1\",\"total\":\"-1\",\"bound\":\"-1\",\"holds\":\"yes\"},\"cellColors\":null}]}",
            Readable(list[1]));
        Assert.Equal("?", list[0].GetProperty("rows")[0].GetProperty("cells").GetProperty("x1").GetString());

        var (_, grid) = await Visualize("visualization=editdistancevisualization", "(ab, b)");
        var tables = JsonDocument.Parse(grid).RootElement.EnumerateArray().ToList();
        Assert.Equal(2, tables.Count);
        Assert.Equal("", tables[0].GetProperty("rows")[1].GetProperty("cells").GetProperty("c0").GetString());
        Assert.Equal("1", tables[1].GetProperty("rows")[2].GetProperty("cells").GetProperty("c1").GetString());
        Assert.Equal("Solution", tables[1].GetProperty("rows")[2].GetProperty("cellColors").GetProperty("c1").GetString());
    }

    // ---- The picture ----

    [Theory]
    [MemberData(nameof(FramesCases))]
    public async Task Frames_EverySolver_HasTheTablePayloadAndFramesThatAgreeWithTheAnswer(string solver, string instance, string answer) {
        string vis = TableSolvers_Tests.VisualizationOf[solver];
        var (status, body) = await Visualize($"visualization={vis}&format=frames&solver={solver}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal(["type", "payload", "frames"], root.EnumerateObject().Select(p => p.Name));
        Assert.Equal("Table", root.GetProperty("type").GetString());

        var payload = root.GetProperty("payload");
        string shape = payload.GetProperty("shape").GetString()!;
        Assert.Equal(vis == "editdistancevisualization" ? "grid" : "matrix", shape);
        Assert.Equal(["kind", "shape", "columns", "rows", .. shape == "grid" ? new[] { "rowAxis", "columnAxis" } : []],
            payload.EnumerateObject().Select(p => p.Name));
        Assert.Equal("table", payload.GetProperty("kind").GetString());
        var columns = payload.GetProperty("columns").EnumerateArray().ToList();
        var rows = payload.GetProperty("rows").EnumerateArray().ToList();
        for (int j = 0; j < columns.Count; j++) Assert.Equal($"c{j}", columns[j].GetProperty("id").GetString());
        for (int i = 0; i < rows.Count; i++) {
            Assert.Equal($"r{i}", rows[i].GetProperty("id").GetString());
            Assert.False(string.IsNullOrEmpty(rows[i].GetProperty("label").GetString()));
        }
        var rowIds = rows.Select(r => r.GetProperty("id").GetString()!).ToHashSet();
        var cellIds = Enumerable.Range(0, rows.Count).SelectMany(i => Enumerable.Range(0, columns.Count).Select(j => $"r{i}c{j}")).ToHashSet();

        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        bool incremental = IncrementalSolvers_Tests.Classification[solver];
        foreach (var f in frames) {
            Assert.Equal(["event", "caption", "focus", "values", "rows", "cells", "changed", .. shape == "grid" ? new[] { "arrows" } : [],
                .. f.TryGetProperty("ok", out _) ? new[] { "ok" } : []], f.EnumerateObject().Select(p => p.Name));
            string ev = f.GetProperty("event").GetString()!;
            Assert.All(f.GetProperty("focus").EnumerateArray(), id => Assert.True(rowIds.Contains(id.GetString()!) || cellIds.Contains(id.GetString()!), id.GetString()));
            // Grids and matrices show every row in every frame, each as wide as the table.
            var values = f.GetProperty("values").EnumerateArray().ToList();
            Assert.Equal(rows.Count, values.Count);
            Assert.All(values, row => Assert.Equal(columns.Count, row.GetArrayLength()));
            foreach (var x in f.GetProperty("rows").EnumerateObject()) {
                Assert.Contains(x.Name, rowIds);
                Assert.Contains(x.Value.GetProperty("state").GetString()!, Vocabulary);
            }
            foreach (var x in f.GetProperty("cells").EnumerateObject()) {
                Assert.Contains(x.Name, cellIds);
                Assert.Contains(x.Value.GetProperty("state").GetString()!, Vocabulary);
            }
            Assert.All(f.GetProperty("changed").EnumerateArray(), id => Assert.Contains(id.GetString()!, cellIds));
            if (shape == "grid")
                foreach (var a in f.GetProperty("arrows").EnumerateArray()) {
                    Assert.Contains(a.GetProperty("from").GetString()!, cellIds);
                    Assert.Contains(a.GetProperty("to").GetString()!, cellIds);
                }
            // A whole candidate is only highlighted: Solution is for an Accept or the answer.
            if (!incremental && ev is "Try" or "Reject") {
                Assert.DoesNotContain(f.GetProperty("cells").EnumerateObject(), x => x.Value.GetProperty("state").GetString() == "Solution");
                Assert.DoesNotContain(f.GetProperty("rows").EnumerateObject(), x => x.Value.GetProperty("state").GetString() == "Solution");
            }
            // While Edit Distance fills its table nothing is the answer yet.
            if (shape == "grid" && ev == "Try")
                Assert.DoesNotContain(f.GetProperty("cells").EnumerateObject(), x => x.Value.GetProperty("state").GetString() == "Solution");
        }

        // One Done frame, last, with the verifier's verdict.
        var done = Assert.Single(frames, f => f.GetProperty("event").GetString() == "Done");
        Assert.Equal(done, frames[^1]);
        bool ok = done.GetProperty("ok").GetBoolean();
        Assert.Equal(answer != "{}", ok);
        if (shape == "grid") {
            // The answer is the corner, and the trace-back reaches it from the start.
            var corner = done.GetProperty("values")[rows.Count - 1][columns.Count - 1].GetString();
            Assert.Equal(answer, corner);
            var cells = done.GetProperty("cells");
            Assert.Equal("Solution", cells.GetProperty("r0c0").GetProperty("state").GetString());
            Assert.Equal("Solution", cells.GetProperty($"r{rows.Count - 1}c{columns.Count - 1}").GetProperty("state").GetString());
        } else if (ok) {
            // Every constraint row holds and is Solution; the assignment's cells spell the answer.
            Assert.Equal(rows.Count - 1, done.GetProperty("rows").EnumerateObject().Count());
            Assert.All(done.GetProperty("rows").EnumerateObject(), r => Assert.Equal("Solution", r.Value.GetProperty("state").GetString()));
            var x = done.GetProperty("values")[0].EnumerateArray().Take(columns.Count - 3).Select(v => v.GetString());
            Assert.Equal(answer, "(" + string.Join(" ", x) + ")");
        } else {
            Assert.Empty(done.GetProperty("rows").EnumerateObject());
            Assert.Empty(done.GetProperty("cells").EnumerateObject());
        }
    }

    [Fact]
    public async Task Frames_EditDistanceDefault_MatchesTheMockup() {
        var (_, body) = await Visualize("visualization=editdistancevisualization&format=frames", "(horse, ros)");
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal(
            "{\"kind\":\"table\",\"shape\":\"grid\","
            + "\"columns\":[{\"id\":\"c0\",\"label\":\"ε\"},{\"id\":\"c1\",\"label\":\"r\"},{\"id\":\"c2\",\"label\":\"o\"},{\"id\":\"c3\",\"label\":\"s\"}],"
            + "\"rows\":[{\"id\":\"r0\",\"label\":\"ε\"},{\"id\":\"r1\",\"label\":\"h\"},{\"id\":\"r2\",\"label\":\"o\"},{\"id\":\"r3\",\"label\":\"r\"},"
            + "{\"id\":\"r4\",\"label\":\"s\"},{\"id\":\"r5\",\"label\":\"e\"}],\"rowAxis\":\"source x\",\"columnAxis\":\"target y\"}",
            Readable(root.GetProperty("payload")));
        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        Assert.Equal(23, frames.Count);
        // The mockup's frame 6 of 17: cell (2,2) copies its diagonal.
        Assert.Equal(
            "{\"event\":\"Try\",\"caption\":\"Cell (2,2): 'o' = 'o', so copy the diagonal: 1.\",\"focus\":[\"r2c2\"],"
            + "\"values\":[[\"0\",\"1\",\"2\",\"3\"],[\"1\",\"1\",\"2\",\"3\"],[\"2\",\"2\",\"1\",null],[\"3\",null,null,null],[\"4\",null,null,null],[\"5\",null,null,null]],"
            + "\"rows\":{},\"cells\":{\"r2c2\":{\"state\":\"ElementHighlight\"}},\"changed\":[\"r2c2\"],"
            + "\"arrows\":[{\"from\":\"r1c1\",\"to\":\"r2c2\",\"best\":true}]}",
            Readable(frames[5]));
        Assert.Equal(
            "{\"event\":\"Done\",\"caption\":\"Edit distance 3: one cheapest way is 1 replacement, 2 deletions.\",\"focus\":[],"
            + "\"values\":[[\"0\",\"1\",\"2\",\"3\"],[\"1\",\"1\",\"2\",\"3\"],[\"2\",\"2\",\"1\",\"2\"],[\"3\",\"2\",\"2\",\"2\"],[\"4\",\"3\",\"3\",\"2\"],[\"5\",\"4\",\"4\",\"3\"]],"
            + "\"rows\":{},\"cells\":{\"r0c0\":{\"state\":\"Solution\"},\"r1c1\":{\"state\":\"Solution\"},\"r2c2\":{\"state\":\"Solution\"},"
            + "\"r3c2\":{\"state\":\"Solution\"},\"r4c3\":{\"state\":\"Solution\"},\"r5c3\":{\"state\":\"Solution\"}},\"changed\":[],\"arrows\":[],\"ok\":true}",
            Readable(frames[^1]));
    }

    [Fact]
    public async Task Frames_RepeatedLettersAndSeparators_StayDistinctCells() {
        var (status, body) = await Visualize("visualization=editdistancevisualization&format=frames", "(r0c1-, r0c1-)");
        Assert.Equal(HttpStatusCode.OK, status);
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal(["ε", "r", "0", "c", "1", "-"], root.GetProperty("payload").GetProperty("rows").EnumerateArray().Select(r => r.GetProperty("label").GetString()));
        var done = root.GetProperty("frames").EnumerateArray().Last();
        Assert.Equal(["r0c0", "r1c1", "r2c2", "r3c3", "r4c4", "r5c5"], done.GetProperty("cells").EnumerateObject().Select(c => c.Name));
        Assert.Equal("0", done.GetProperty("values")[5][5].GetString());
    }

    [Fact]
    public async Task Frames_IntProgrammingMockupExample_TriesUntilEveryRowHolds() {
        var (_, body) = await Visualize("visualization=intprogrammingvisualization&format=frames", "(-1 -1 -1 -1),(1 1 0 0),(0 0 1 1),(1 0 1 0)<=(-2 1 1 1)");
        var frames = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().ToList();
        Assert.Equal(["Reject", "Reject", "Reject", "Reject", "Reject", "Reject", "Accept", "Done"], frames.Select(f => f.GetProperty("event").GetString()));
        Assert.Equal(
            "{\"event\":\"Reject\",\"caption\":\"Try (1 1 0 0): row 2 adds up to 2, over its bound 1.\",\"focus\":[\"r2\"],"
            + "\"values\":[[\"1\",\"1\",\"0\",\"0\",null,null,null],[\"-1\",\"-1\",\"-1\",\"-1\",\"-2\",\"-2\",\"yes\"],[\"1\",\"1\",\"0\",\"0\",\"2\",\"1\",\"no\"],"
            + "[\"0\",\"0\",\"1\",\"1\",\"0\",\"1\",\"yes\"],[\"1\",\"0\",\"1\",\"0\",\"1\",\"1\",\"yes\"]],"
            + "\"rows\":{\"r1\":{\"state\":\"Covered\"},\"r2\":{\"state\":\"Rejected\"},\"r3\":{\"state\":\"Covered\"},\"r4\":{\"state\":\"Covered\"}},"
            + "\"cells\":{\"r0c0\":{\"state\":\"ElementHighlight\"},\"r0c1\":{\"state\":\"ElementHighlight\"},\"r0c2\":{\"state\":\"ElementHighlight\"},\"r0c3\":{\"state\":\"ElementHighlight\"}},"
            + "\"changed\":[]}",
            Readable(frames[3]));
        Assert.Equal("(0 1 1 0) satisfies every row, found on try 7 of 16.", frames[^1].GetProperty("caption").GetString());
    }

    [Theory]
    [InlineData("intprogrammingvisualization", "(1 a)<=(1)")]
    [InlineData("editdistancevisualization", "(abc)")]
    public async Task Frames_UnreadableInstance_IsA400_NotAPicture(string vis, string instance) {
        var (status, body) = await Visualize($"visualization={vis}&format=frames", instance);
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("instance_parse_error", body);
    }
}

// A stopped run, under the 0.05 s budget of TinyBudgetAppFactory.
[Collection(TimingCollection.Name)]
public class TableFrames_StoppedRun_Endpoint_Tests : IClassFixture<TinyBudgetAppFactory> {
    private readonly HttpClient _client;

    public TableFrames_StoppedRun_Endpoint_Tests(TinyBudgetAppFactory factory) {
        _client = factory.CreateClient();
    }

    // 24 variables whose sum must be negative: no 0-1 assignment works, and the brute force tries all 2^24 before saying so.
    internal static string SlowIntProgramming => "(" + string.Join(" ", Enumerable.Repeat(1, 24)) + ")<=(-1)";

    [Fact]
    public async Task Frames_StoppedRun_EndsWithTheBlankMatrix_NotOk() {
        var sw = Stopwatch.StartNew();
        var content = new StringContent(JsonSerializer.Serialize(SlowIntProgramming), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/visualize?visualization=intprogrammingvisualization&format=frames", content, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(20), $"took {sw.Elapsed}");

        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal("Table", root.GetProperty("type").GetString());
        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        Assert.True(frames.Count >= 2);
        var done = frames[^1];
        Assert.Equal("Done", done.GetProperty("event").GetString());
        Assert.False(done.GetProperty("ok").GetBoolean());
        Assert.Matches(@"^Stopped after 0\.05 s and [\d,]+ steps?; the solver had not finished\.", done.GetProperty("caption").GetString());
        Assert.Empty(done.GetProperty("rows").EnumerateObject());
        Assert.Empty(done.GetProperty("cells").EnumerateObject());
        Assert.Empty(done.GetProperty("focus").EnumerateArray());
        Assert.All(done.GetProperty("values")[0].EnumerateArray().Take(24), v => Assert.Equal("?", v.GetString()));
        Assert.Equal(JsonValueKind.Null, done.GetProperty("values")[1][24].ValueKind);
        Assert.Single(frames, f => f.GetProperty("event").GetString() == "Done");
        Assert.DoesNotContain(frames, f => f.GetProperty("rows").EnumerateObject().Any(n => n.Value.GetProperty("state").GetString() == "Solution"));
    }
}
