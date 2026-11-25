using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Weighted Max Clique's Graph picture (#1058) on the actual endpoint output. It is a second picture beside the problem's
// old-style one and opt-in, so Redux_GUI never sees it: its list format is exactly the old picture's, and it is listed only for
// includePictures=true. The rest checks what it draws: a weight on every node, and in every frame the chosen nodes' total,
// which is the weight the caption names, for both solvers that record steps.
public class WeightedMaxCliqueGraph_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public WeightedMaxCliqueGraph_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private const string Vis = "WeightedMaxCliqueGraphVisualization";
    private const string OldVis = "WeightedMaxCliqueVisualization";
    // The default instance: a-b-c is the heaviest clique (4 + 3 + 5 = 12); d (7) is joined only to b.
    private const string Instance = "(({a,b,c,d},{{a,b},{a,c},{b,c},{b,d}}),((a,4),(b,3),(c,5),(d,7)))";
    private static readonly Dictionary<string, long> Weights = new() { ["a"] = 4, ["b"] = 3, ["c"] = 5, ["d"] = 7 };

    private async Task<(HttpStatusCode Status, string Body)> Post(string path, string query, string instance) {
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/{path}?{query}", content, TestContext.Current.CancellationToken);
        return (response.StatusCode, (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Replace("\r\n", "\n"));
    }

    private async Task<JsonElement> Json(string path, string query, string instance) {
        var (status, body) = await Post(path, query, instance);
        Assert.True(status == HttpStatusCode.OK, body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<string> Get(string url) {
        var response = await _client.GetAsync(url, TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        return body;
    }

    private static Dictionary<string, string> States(JsonElement frame, string collection) =>
        frame.GetProperty(collection).EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetProperty("state").GetString()!);

    private static JsonElement Picture(JsonElement run) =>
        run.GetProperty("pictures").EnumerateArray().Single(p => p.GetProperty("visualization").GetString() == Vis);

    [Fact]
    public async Task List_IsExactlyTheOldPicturesList() {
        var (status, body) = await Post("visualize", $"visualization={Vis}", Instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var (_, expected) = await Post("visualize", $"visualization={OldVis}", Instance);
        Assert.Equal(expected, body);
    }

    [Fact]
    public async Task ThePicture_IsListedOnlyForClientsThatOptIn() {
        string plain = await Get("/Navigation/Problem_VisualizationsRefactor/?chosenProblem=WEIGHTEDMAXCLIQUE");
        Assert.Contains(OldVis, plain);
        Assert.DoesNotContain(Vis, plain);
        Assert.Contains(Vis, await Get("/Navigation/Problem_VisualizationsRefactor/?chosenProblem=WEIGHTEDMAXCLIQUE&includePictures=true"));
        // The problem's default, what Redux_GUI draws, is still the old picture.
        Assert.Contains("\"visualizationName\":\"Weighted Maximum Clique Visualization\"", await Get("/ProblemProvider/info?interface=WEIGHTEDMAXCLIQUE"));
    }

    [Fact]
    public async Task ThePayload_WritesEveryNodesWeight() {
        var frames = await Json("visualize", $"visualization={Vis}&format=frames&solver=WeightedMaxCliqueBruteForceSolver", Instance);
        Assert.Equal("Graph", frames.GetProperty("type").GetString());
        var nodes = frames.GetProperty("payload").GetProperty("nodes").EnumerateArray()
            .ToDictionary(n => n.GetProperty("id").GetString()!, n => long.Parse(n.GetProperty("weight").GetString()!));   // strings, so no browser rounds them (#1034)
        Assert.Equal(Weights, nodes);
        Assert.False(frames.GetProperty("payload").TryGetProperty("k", out _));
    }

    // The weight a caption names for the clique it shows ("a clique of weight 12", "weighs 12", "weight 12."); null when it
    // names none (a set that is not a clique, a backtrack, a prune).
    private static long? CaptionWeight(string caption) {
        var match = Regex.Match(caption, @"(?:a clique of weight|weighs|clique is \{[^}]*\}, weight|Start from \{[^}]*\}, weight) (\d+)");
        return match.Success ? long.Parse(match.Groups[1].Value) : null;
    }

    [Theory]
    [InlineData("WeightedMaxCliqueBruteForceSolver")]
    [InlineData("WeightedMaxCliqueBranchAndBoundSolver")]
    public async Task EveryFrame_CarriesItsChosenNodesTotal_AndTheCaptionsAgree(string solver) {
        var run = await Json("visualizeRun", $"solver={solver}", Instance);
        Assert.Equal("({a,b,c},12)", run.GetProperty("answer").GetString());
        var frames = Picture(run).GetProperty("frames").EnumerateArray().ToList();
        Assert.True(frames.Count > 1);
        int named = 0;
        foreach (var frame in frames) {
            long total = long.Parse(frame.GetProperty("selectedWeight").GetString()!);
            var states = States(frame, "nodes");
            string caption = frame.GetProperty("caption").GetString()!;
            if (CaptionWeight(caption) is { } weight) {
                Assert.True(weight == total, $"{caption}: selectedWeight {total}");
                named++;
            }
            // The brute force judges whole candidates, so the nodes drawn are exactly the chosen ones (the clique rule marks no
            // others). Branch and bound also draws the candidates a prune rules out, which are not chosen.
            if (solver.Contains("Brute")) Assert.Equal(states.Keys.Sum(n => Weights[n]), total);
        }
        Assert.True(named >= 2, "the captions name the clique's weight");

        var done = frames[^1];
        Assert.Equal("Done", done.GetProperty("event").GetString());
        Assert.True(done.GetProperty("ok").GetBoolean());
        Assert.Equal("12", done.GetProperty("selectedWeight").GetString());
        Assert.Equal(new Dictionary<string, string> { ["a"] = "Solution", ["b"] = "Solution", ["c"] = "Solution" }, States(done, "nodes"));
        Assert.Equal(["a-b", "a-c", "b-c"], States(done, "edges").Where(e => e.Value == "Solution").Select(e => e.Key).Order());
        Assert.Equal("The heaviest clique is {a,b,c}, weight 12.", done.GetProperty("caption").GetString());
    }

    [Fact]
    public async Task BruteForce_RejectsASetThatIsNotAClique_WithTheMissingEdge() {
        var run = await Json("visualizeRun", "solver=WeightedMaxCliqueBruteForceSolver", Instance);
        var frames = Picture(run).GetProperty("frames").EnumerateArray().ToList();
        // {a,d}: a and d aren't joined, so the missing edge is drawn as a Rejected phantom; its weight is still 11.
        var ad = frames.Single(f => f.GetProperty("caption").GetString() == "Try {a,d}: a and d aren't joined.");
        Assert.Equal("Reject", ad.GetProperty("event").GetString());
        Assert.Equal("11", ad.GetProperty("selectedWeight").GetString());
        var phantom = Assert.Single(ad.GetProperty("phantoms").EnumerateArray());
        Assert.Equal("Rejected", phantom.GetProperty("state").GetString());
    }

    [Fact]
    public async Task BranchAndBound_StartsFromTheGreedyClique_AndPrunesWithTheBound() {
        var run = await Json("visualizeRun", "solver=WeightedMaxCliqueBranchAndBoundSolver", Instance);
        var frames = Picture(run).GetProperty("frames").EnumerateArray().ToList();
        // The greedy takes d (7), then b (3, joined to d); a and c are not joined to d.
        Assert.StartsWith("Start from {b,d}, weight 10", frames[0].GetProperty("caption").GetString());
        Assert.Equal("10", frames[0].GetProperty("selectedWeight").GetString());
        Assert.Contains(frames, f => f.GetProperty("event").GetString() == "Reject" && f.GetProperty("caption").GetString()!.StartsWith("Stop:"));
        Assert.Contains(frames, f => f.GetProperty("event").GetString() == "Backtrack");
    }

    [Fact]
    public async Task ASolverWithNoSteps_DrawsTheCheckedAnswerAlone_WithItsTotal() {
        var run = await Json("visualizeRun", "solver=WeightedMaxCliqueGreedyLocalSearchSolver", Instance);
        var only = Assert.Single(Picture(run).GetProperty("frames").EnumerateArray());
        Assert.Equal("Done", only.GetProperty("event").GetString());
        Assert.True(only.GetProperty("ok").GetBoolean());
        Assert.Equal(JsonValueKind.Null, only.GetProperty("step").ValueKind);
        Assert.Equal("12", only.GetProperty("selectedWeight").GetString());
    }
}
