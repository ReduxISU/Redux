using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Packing picture on the actual /ProblemProvider/visualize?format=frames output, for Partition, Subset Sum, Knapsack and
// Bin Packing with every one of their solvers, plus the guards that keep everything old frontends read exactly as it was:
//   - the list format of Partition's visualization (Snapshots/packing_list_snapshots.tsv, captured before the picture existed),
//   - /info for the four problems, their seven solvers and Partition's visualization (info.<name>.json, captured the same way).
// The deliberate exception is the default visualization of Subset Sum, Knapsack and Bin Packing, which moved off
// DummyVisualization to their Packing visualizations: see the "rewire" tests.
public class PackingFrames_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public PackingFrames_Endpoint_Tests(AppFactory factory) {
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
        "Background", "ElementHighlight", "Solution", "Rejected", "Untraveled", "Covered", "Blocked", "Committed",
    };

    public static TheoryData<string, string, string> FramesCases {
        get {
            var data = new TheoryData<string, string, string>();
            foreach (var (solver, instance, answer) in PackingSolvers_Tests.Rows()) data.Add(solver, instance, answer);
            return data;
        }
    }

    public static TheoryData<string, string, string> ListCases {
        get {
            var data = new TheoryData<string, string, string>();
            foreach (var line in ReadSnapshot("packing_list_snapshots.tsv").Split('\n').Where(l => l.Length > 0)) {
                var parts = line.Split('\t');
                data.Add(parts[0], parts[1], parts[2]);
            }
            return data;
        }
    }

    public static TheoryData<string> TouchedInterfaces => new([
        "partition", "partitionbruteforce", "subsetsumbruteforce", "fastapproximation",
        "knapsackbruteforce", "knapsackdp", "binpackingbruteforce", "binpackingffd", "partitiondefaultvisualization",
    ]);

    // ---- What old frontends read must not change. ----

    [Theory]
    [MemberData(nameof(TouchedInterfaces))]
    public async Task Info_IsUnchangedByThePackingPicture(string name) {
        var response = await _client.GetAsync($"/ProblemProvider/info?interface={name}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ReadSnapshot($"info.{name}.json"), body.Replace("\r\n", "\n"));
        Assert.DoesNotContain("\"Packing\"", InfoJson.WithoutPictureType(body));
        Assert.DoesNotContain("StepShape", body);
        Assert.DoesNotContain("Describe", body);
        Assert.DoesNotContain("ParseAnswer", body);
    }

    // ---- Subset Sum, Knapsack and Bin Packing moved off DummyVisualization: the only thing that may change. ----

    // What embeds one of their defaultVisualizations: the three problems, and the reductions into or out of them (Exact
    // Cover to Subset Sum, Subset Sum to Knapsack, Subset Sum to Partition). Their snapshots were taken after the rewire;
    // the diff against the pre-change capture is only the nested defaultVisualization objects (and reduce's visualization).
    public static TheoryData<string> EmbedsAPackingDefault => new() {
        "subsetsum", "knapsack", "binpacking", "karpexactcovertosubsetsum", "fengreduction", "subsetsumtopartitionreduction",
    };

    [Theory]
    [MemberData(nameof(EmbedsAPackingDefault))]
    public async Task Rewire_Info_ShowsThePackingVisualizations_AndNothingElse(string name) {
        var response = await _client.GetAsync($"/ProblemProvider/info?interface={name}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Replace("\r\n", "\n");
        Assert.Equal(ReadSnapshot($"info.{name}.json"), body);
        Assert.DoesNotContain("DummyVisualization", body);
        Assert.DoesNotContain("\"Packing\"", InfoJson.WithoutPictureType(body));
        Assert.DoesNotContain("StepShape", body);
    }

    [Theory]
    [InlineData("subsetsum", "subsetsumvisualization", "Subset Sum Visualization", "Subset Sum Brute Force")]
    [InlineData("knapsack", "knapsackvisualization", "Knapsack Visualization", "Knapsack Brute Force")]
    [InlineData("binpacking", "binpackingvisualization", "Bin Packing Visualization", "Bin Packing Brute Force")]
    public async Task Rewire_ProblemInfo_ReportsItsPackingVisualization(string problem, string vis, string name, string solver) {
        var response = await _client.GetAsync($"/ProblemProvider/info?interface={problem}", TestContext.Current.CancellationToken);
        var nested = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement.GetProperty("defaultVisualization");
        Assert.Equal(name, nested.GetProperty("visualizationName").GetString());
        Assert.Equal("SetD3", nested.GetProperty("visualizationType").GetString());
        Assert.Equal(solver, nested.GetProperty("solver").GetProperty("solverName").GetString());
        // The same object the visualization reports about itself.
        var own = await _client.GetAsync($"/ProblemProvider/info?interface={vis}", TestContext.Current.CancellationToken);
        Assert.Equal(Compact(await own.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)), Compact(nested.GetRawText()));
    }

    [Theory]
    [InlineData("karpexactcovertosubsetsum", "({1,2,3,4},{{1,2,3},{2,3},{4,1}})")]
    [InlineData("fengreduction", "({1,7,12,15},28)")]
    [InlineData("subsetsumtopartitionreduction", "({1,7,12,15},28)")]
    public async Task Rewire_Reduce_ChangesOnlyTheNestedVisualization(string reduction, string instance) {
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/reduce?reduction={reduction}", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ReadSnapshot($"{reduction}.reduce.json"), (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Replace("\r\n", "\n"));
    }

    // Before the rewire the target's visualization was DummyVisualization, so visualizeReduction answered an empty list
    // (#755). Now it answers the target's list items: the unsolved picture, then the solved one, which colors the mapped answer
    // (both reductions map answers forward since #828).
    [Theory]
    [InlineData("karpexactcovertosubsetsum", "{{2,3},{4,1}}", "({1,2,3,4},{{1,2,3},{2,3},{4,1}})")]
    [InlineData("fengreduction", "{1,12,15}", "({1,7,12,15},28)")]
    public async Task Rewire_VisualizeReduction_IntoSubsetSumOrKnapsack_NowReturnsThePicture(string reduction, string solution, string instance) {
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/visualizeReduction?reduction={reduction}&solution={Uri.EscapeDataString(solution)}", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Replace("\r\n", "\n");
        Assert.Equal(ReadSnapshot($"{reduction}.visualizereduction.json"), body);
        var items = JsonDocument.Parse(body).RootElement.EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.All(items, i => Assert.Equal("set", i.GetProperty("kind").GetString()));
    }

    [Theory]
    [MemberData(nameof(ListCases))]
    public async Task List_MatchesPreChangeSnapshot(string vis, string instance, string expected) {
        var (status, body) = await Visualize($"visualization={vis}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(expected, Compact(body));
    }

    // The new visualizations report their legacy list type in /info and nothing of the picture.
    [Theory]
    [InlineData("subsetsumvisualization", "Subset Sum Brute Force")]
    [InlineData("knapsackvisualization", "Knapsack Brute Force")]
    [InlineData("binpackingvisualization", "Bin Packing Brute Force")]
    public async Task NewVisualizations_Info_IsTheListTypeOnly(string name, string solver) {
        var response = await _client.GetAsync($"/ProblemProvider/info?interface={name}", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal(["visualizationName", "visualizationDefinition", "source", "sourceFile", "contributors", "visualizationType", "pictureType", "solver"],
            root.EnumerateObject().Select(p => p.Name));
        Assert.Equal("SetD3", root.GetProperty("visualizationType").GetString());
        Assert.Equal(solver, root.GetProperty("solver").GetProperty("solverName").GetString());
        Assert.DoesNotContain("\"Packing\"", InfoJson.WithoutPictureType(body));
    }

    [Theory]
    [InlineData("subsetsumvisualization", "({1,7,12,15},28)", "[\"Background\",\"Background\",\"Background\",\"Background\"]", "[\"Solution\",\"Background\",\"Solution\",\"Solution\"]")]
    [InlineData("binpackingvisualization", "((4,7,3,6,2,8),10,3)", "[\"Background\",\"Background\",\"Background\",\"Background\",\"Background\",\"Background\"]", "[\"Solution\",\"Solution\",\"Solution\",\"Solution\",\"Solution\",\"Solution\"]")]
    public async Task NewVisualizations_ListFormat_DrawsTheItemsAndMarksTheAnswer(string vis, string instance, string before, string after) {
        var (status, body) = await Visualize($"visualization={vis}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var list = JsonDocument.Parse(body).RootElement.EnumerateArray().ToList();
        Assert.Equal(2, list.Count);
        string Colors(JsonElement set) => JsonSerializer.Serialize(set.GetProperty("data").GetProperty("list").EnumerateArray().Select(e => e.GetProperty("color").GetString()));
        Assert.Equal(before, Colors(list[0]));
        Assert.Equal(after, Colors(list[1]));
    }

    [Fact]
    public async Task Knapsack_ListFormat_MarksBothNumbersOfEveryChosenItem() {
        var (_, body) = await Visualize("visualization=knapsackvisualization", "({(10,60),(20,100),(30,120)},50,220)");
        var solved = JsonDocument.Parse(body).RootElement.EnumerateArray().Last().GetProperty("data").GetProperty("list").EnumerateArray()
            .ToDictionary(e => e.GetProperty("id").GetString()!, e => string.Join(",", e.GetProperty("list").EnumerateArray().Select(n => n.GetProperty("color").GetString())));
        // Each item has the Packing picture's item id by position.
        Assert.Equal("Background,Background", solved["I1"]);
        Assert.Equal("Solution,Solution", solved["I2"]);
        Assert.Equal("Solution,Solution", solved["I3"]);
    }

    // ---- The picture ----

    [Theory]
    [MemberData(nameof(FramesCases))]
    public async Task Frames_EverySolver_HasThePackingPayloadAndFramesThatAgreeWithTheAnswer(string solver, string instance, string answer) {
        string vis = PackingSolvers_Tests.VisualizationOf[solver];
        var (status, body) = await Visualize($"visualization={vis}&format=frames&solver={solver}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal(["type", "payload", "frames"], root.EnumerateObject().Select(p => p.Name));
        Assert.Equal("Packing", root.GetProperty("type").GetString());

        var payload = root.GetProperty("payload");
        string rule = payload.GetProperty("rule").GetString()!;
        Assert.Equal(["kind", "rule", "items", "containers", "total", .. rule == "binPacking" ? new[] { "maxBins" } : []],
            payload.EnumerateObject().Select(p => p.Name));
        Assert.Equal("packing", payload.GetProperty("kind").GetString());
        Assert.Equal(vis switch {
            "partitiondefaultvisualization" => "partition",
            "subsetsumvisualization" => "subsetSum",
            "knapsackvisualization" => "knapsack",
            _ => "binPacking",
        }, rule);

        var items = payload.GetProperty("items").EnumerateArray().ToList();
        var sizes = new List<int>();
        var values = new List<int>();
        for (int i = 0; i < items.Count; i++) {
            Assert.Equal(["id", "label", "size", .. rule == "knapsack" ? new[] { "value" } : []], items[i].EnumerateObject().Select(p => p.Name));
            Assert.Equal($"I{i + 1}", items[i].GetProperty("id").GetString());
            sizes.Add(items[i].GetProperty("size").GetInt32());
            values.Add(rule == "knapsack" ? items[i].GetProperty("value").GetInt32() : 0);
        }
        Assert.Equal(sizes.Sum(), payload.GetProperty("total").GetInt32());
        var containers = payload.GetProperty("containers").EnumerateArray().ToList();
        for (int c = 0; c < containers.Count; c++) Assert.Equal($"C{c + 1}", containers[c].GetProperty("id").GetString());
        var itemIds = items.Select(i => i.GetProperty("id").GetString()!).ToHashSet();
        var containerIds = containers.Select(c => c.GetProperty("id").GetString()!).ToHashSet();

        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        bool incremental = IncrementalSolvers_Tests.Classification[solver];
        foreach (var f in frames) {
            Assert.Equal(["event", "caption", "focus", "items", "containers", "assign", "totals", "step", .. f.TryGetProperty("ok", out _) ? new[] { "ok" } : []],
                f.EnumerateObject().Select(p => p.Name));
            string ev = f.GetProperty("event").GetString()!;
            Assert.All(f.GetProperty("focus").EnumerateArray(), id => Assert.True(itemIds.Contains(id.GetString()!) || containerIds.Contains(id.GetString()!), id.GetString()));
            foreach (var x in f.GetProperty("items").EnumerateObject()) {
                Assert.Contains(x.Name, itemIds);
                Assert.Contains(x.Value.GetProperty("state").GetString()!, Vocabulary);
            }
            foreach (var x in f.GetProperty("containers").EnumerateObject()) {
                Assert.Contains(x.Name, containerIds);
                Assert.Contains(x.Value.GetProperty("state").GetString()!, Vocabulary);
            }
            // One container per item (or -1), and totals that add up what each container holds.
            var assign = f.GetProperty("assign").EnumerateArray().Select(a => a.GetInt32()).ToList();
            Assert.Equal(items.Count, assign.Count);
            Assert.All(assign, a => Assert.InRange(a, -1, containers.Count - 1));
            var totals = f.GetProperty("totals").EnumerateArray().ToList();
            Assert.Equal(containers.Count, totals.Count);
            for (int c = 0; c < containers.Count; c++) {
                Assert.Equal(Enumerable.Range(0, items.Count).Where(i => assign[i] == c).Sum(i => sizes[i]), totals[c].GetProperty("size").GetInt32());
                if (rule == "knapsack")
                    Assert.Equal(Enumerable.Range(0, items.Count).Where(i => assign[i] == c).Sum(i => values[i]), totals[c].GetProperty("value").GetInt32());
                else
                    Assert.False(totals[c].TryGetProperty("value", out _));
            }
            // A whole candidate is only highlighted: Solution is for an Accept or the answer.
            if (!incremental && ev is "Try" or "Reject")
                Assert.DoesNotContain(f.GetProperty("items").EnumerateObject(), x => x.Value.GetProperty("state").GetString() == "Solution");
        }

        // One Done frame, last, with the verifier's verdict; a correct answer is all Solution or Untraveled, and its lines met.
        var done = Assert.Single(frames, f => f.GetProperty("event").GetString() == "Done");
        Assert.Equal(done, frames[^1]);
        bool ok = done.GetProperty("ok").GetBoolean();
        // Every packing solver answers only what its verifier accepts, or {} (which is correct only for Subset Sum with T = 0;
        // PackingSolvers_Tests holds the verdict to the verifier's).
        if (answer != "{}") Assert.True(ok);
        if (ok) {
            Assert.Equal(items.Count, done.GetProperty("items").EnumerateObject().Count());
            Assert.All(done.GetProperty("items").EnumerateObject(), x => Assert.Contains(x.Value.GetProperty("state").GetString(), new[] { "Solution", "Untraveled" }));
            Assert.DoesNotContain(done.GetProperty("containers").EnumerateObject(), x => x.Value.GetProperty("state").GetString() == "Rejected");
        } else {
            Assert.Empty(done.GetProperty("items").EnumerateObject());
            Assert.All(done.GetProperty("assign").EnumerateArray(), a => Assert.Equal(-1, a.GetInt32()));
        }
    }

    [Fact]
    public async Task Frames_SubsetSumDefault_MatchesTheMockup() {
        var (_, body) = await Visualize("visualization=subsetsumvisualization&format=frames", "({1,7,12,15},28)");
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal(
            "{\"kind\":\"packing\",\"rule\":\"subsetSum\","
            + "\"items\":[{\"id\":\"I1\",\"label\":\"1\",\"size\":1},{\"id\":\"I2\",\"label\":\"7\",\"size\":7},"
            + "{\"id\":\"I3\",\"label\":\"12\",\"size\":12},{\"id\":\"I4\",\"label\":\"15\",\"size\":15}],"
            + "\"containers\":[{\"id\":\"C1\",\"label\":\"Chosen\",\"target\":28}],\"total\":35}",
            Readable(root.GetProperty("payload")));
        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        Assert.Equal(14, frames.Count);
        Assert.Equal(
            "{\"event\":\"Reject\",\"caption\":\"Try {1,7}: 1 + 7 = 8, not 28.\",\"focus\":[\"I1\",\"I2\"],"
            + "\"items\":{\"I1\":{\"state\":\"ElementHighlight\"},\"I2\":{\"state\":\"ElementHighlight\"}},"
            + "\"containers\":{\"C1\":{\"state\":\"Rejected\"}},\"assign\":[0,0,-1,-1],\"totals\":[{\"size\":8}],\"step\":2}",
            Readable(frames[2]));
        Assert.Equal(
            "{\"event\":\"Done\",\"caption\":\"{1,12,15} adds up to exactly 28, found after 13 subsets.\",\"focus\":[],"
            + "\"items\":{\"I1\":{\"state\":\"Solution\"},\"I2\":{\"state\":\"Untraveled\"},\"I3\":{\"state\":\"Solution\"},\"I4\":{\"state\":\"Solution\"}},"
            + "\"containers\":{\"C1\":{\"state\":\"Covered\"}},\"assign\":[0,-1,0,0],\"totals\":[{\"size\":28}],\"step\":13,\"ok\":true}",
            Readable(frames[^1]));
    }

    [Fact]
    public async Task Frames_KnapsackAndBinPacking_PayloadsCarryTheirLines() {
        var (_, knapsack) = await Visualize("visualization=knapsackvisualization&format=frames&solver=knapsackdp", "({(10,60),(20,100),(30,120)},50,220)");
        Assert.Equal(
            "{\"kind\":\"packing\",\"rule\":\"knapsack\","
            + "\"items\":[{\"id\":\"I1\",\"label\":\"(10,60)\",\"size\":10,\"value\":60},{\"id\":\"I2\",\"label\":\"(20,100)\",\"size\":20,\"value\":100},"
            + "{\"id\":\"I3\",\"label\":\"(30,120)\",\"size\":30,\"value\":120}],"
            + "\"containers\":[{\"id\":\"C1\",\"label\":\"Knapsack\",\"limit\":50,\"atLeast\":220}],\"total\":60}",
            Readable(JsonDocument.Parse(knapsack).RootElement.GetProperty("payload")));
        var (_, bins) = await Visualize("visualization=binpackingvisualization&format=frames", "((4,7,3,6,2,8),10,3)");
        var payload = JsonDocument.Parse(bins).RootElement.GetProperty("payload");
        Assert.Equal(
            "[{\"id\":\"C1\",\"label\":\"Bin 1\",\"limit\":10},{\"id\":\"C2\",\"label\":\"Bin 2\",\"limit\":10},{\"id\":\"C3\",\"label\":\"Bin 3\",\"limit\":10}]",
            Readable(payload.GetProperty("containers")));
        Assert.Equal(3, payload.GetProperty("maxBins").GetInt32());
        var (_, partition) = await Visualize("visualization=partitiondefaultvisualization&format=frames", "{3,5,7}");
        Assert.Equal("[{\"id\":\"C1\",\"label\":\"S1\",\"target\":7.5},{\"id\":\"C2\",\"label\":\"S2\",\"target\":7.5}]",
            Readable(JsonDocument.Parse(partition).RootElement.GetProperty("payload").GetProperty("containers")));
    }

    [Fact]
    public async Task Frames_EqualSizes_AreDistinctItems() {
        var (status, body) = await Visualize("visualization=binpackingvisualization&format=frames", "((4,4,4),8,2)");
        Assert.Equal(HttpStatusCode.OK, status);
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal(["I1", "I2", "I3"], root.GetProperty("payload").GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetString()));
        Assert.Equal("[0,0,1]", JsonSerializer.Serialize(root.GetProperty("frames").EnumerateArray().Last().GetProperty("assign")));
    }

    [Fact]
    public async Task Frames_TheIncrementalBinPackingRun_MarksTheItemWithNoRoomRejected() {
        var (_, body) = await Visualize("visualization=binpackingvisualization&format=frames", "((6,6,6),10,2)");
        var reject = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().First(f => f.GetProperty("event").GetString() == "Reject");
        Assert.Equal(
            "{\"event\":\"Reject\",\"caption\":\"No bin has room for 6, so take back the last choice.\",\"focus\":[\"I3\"],"
            + "\"items\":{\"I1\":{\"state\":\"Committed\"},\"I2\":{\"state\":\"Committed\"},\"I3\":{\"state\":\"Rejected\"}},"
            + "\"containers\":{\"C1\":{\"state\":\"Covered\"},\"C2\":{\"state\":\"Covered\"}},\"assign\":[0,1,-1],\"totals\":[{\"size\":6},{\"size\":6}],\"step\":2}",
            Readable(reject));
    }

    [Theory]
    [InlineData("knapsackvisualization", "({(10,60)},-2,0)")]
    [InlineData("binpackingvisualization", "((0,3),5,1)")]
    public async Task Frames_ImpossibleInstance_IsA400_NotAPicture(string vis, string instance) {
        var (status, body) = await Visualize($"visualization={vis}&format=frames", instance);
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("instance_parse_error", body);
    }
}

// A stopped run, under the 0.05 s budget of TinyBudgetAppFactory.
[Collection(TimingCollection.Name)]
public class PackingFrames_StoppedRun_Endpoint_Tests : IClassFixture<TinyBudgetAppFactory> {
    private readonly HttpClient _client;

    public PackingFrames_StoppedRun_Endpoint_Tests(TinyBudgetAppFactory factory) {
        _client = factory.CreateClient();
    }

    // 25 distinct numbers with an odd total: no split balances, and Partition Brute Force tries all 2^25 before saying so.
    internal static string SlowPartition => "{" + string.Join(",", Enumerable.Range(1, 25)) + "}";

    [Fact]
    public async Task Frames_StoppedRun_EndsWithABlankNotOkDone() {
        var sw = Stopwatch.StartNew();
        var content = new StringContent(JsonSerializer.Serialize(SlowPartition), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/visualize?visualization=partitiondefaultvisualization&format=frames", content, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(20), $"took {sw.Elapsed}");

        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal("Packing", root.GetProperty("type").GetString());
        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        Assert.True(frames.Count >= 2);
        var done = frames[^1];
        Assert.Equal("Done", done.GetProperty("event").GetString());
        Assert.False(done.GetProperty("ok").GetBoolean());
        Assert.Matches(@"^Stopped after 0\.05 s and [\d,]+ steps?; the solver had not finished\.", done.GetProperty("caption").GetString());
        Assert.Empty(done.GetProperty("items").EnumerateObject());
        Assert.Empty(done.GetProperty("containers").EnumerateObject());
        Assert.All(done.GetProperty("assign").EnumerateArray(), a => Assert.Equal(-1, a.GetInt32()));
        Assert.All(done.GetProperty("totals").EnumerateArray(), t => Assert.Equal(0, t.GetProperty("size").GetInt32()));
        Assert.Single(frames, f => f.GetProperty("event").GetString() == "Done");
        Assert.DoesNotContain(frames, f => f.GetProperty("items").EnumerateObject().Any(n => n.Value.GetProperty("state").GetString() == "Solution"));
    }
}
