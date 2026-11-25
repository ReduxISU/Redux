using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Weighted Max-SAT's Formula picture on the actual endpoint output. It is a second picture beside the problem's old-style one,
// so the guard for old frontends is that its list format is exactly the old picture's; the rest checks what the new picture
// draws for a solver that records steps (the brute force) and for one that does not (branch and bound).
public class WeightedMaxSatFormula_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public WeightedMaxSatFormula_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private const string Vis = "WeightedMaxSatFormulaVisualization";
    private const string OldVis = "WeightedMaxSatVisualization";
    // (!x1 | x2) weighs 1, (x3 | x4) weighs 2: the best is 3, with x1 false and x4 true.
    private const string Instance = "(((!x1 | x2) & (x3 | x4)),(1,2))";

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

    private static Dictionary<string, string> States(JsonElement frame, string collection) =>
        frame.GetProperty(collection).EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetProperty("state").GetString()!);

    private static Dictionary<string, JsonElement> Pictures(JsonElement run) =>
        run.GetProperty("pictures").EnumerateArray().ToDictionary(p => p.GetProperty("visualization").GetString()!);

    [Fact]
    public async Task List_IsExactlyTheOldPicturesList() {
        var (status, body) = await Post("visualize", $"visualization={Vis}", Instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var (_, expected) = await Post("visualize", $"visualization={OldVis}", Instance);
        Assert.Equal(expected, body);
    }

    [Fact]
    public async Task BruteForce_DrawsEveryCandidate_ThenTheBestAssignment() {
        var run = await Json("visualizeRun", "solver=WeightedMaxSatBruteForceSolver", Instance);
        Assert.Equal("((x1:False,x2:False,x3:False,x4:True),3)", run.GetProperty("answer").GetString());
        // The old picture is still listed, as one that only serves the list format.
        var unavailable = run.GetProperty("unavailable").EnumerateArray()
            .ToDictionary(u => u.GetProperty("visualization").GetString()!, u => u.GetProperty("reason").GetString());
        Assert.Equal("no_frames", unavailable[OldVis]);

        var picture = Pictures(run)[Vis];
        Assert.Equal("Formula", picture.GetProperty("type").GetString());
        var frames = picture.GetProperty("frames").EnumerateArray().ToList();
        Assert.Equal(16 + 1, frames.Count);   // 2^4 candidates, then the Done

        // The first candidate is all false: it keeps C1 (!x1 | x2) and gives up C2 (x3 | x4).
        var first = frames[0];
        Assert.Equal("Accept", first.GetProperty("event").GetString());
        Assert.Equal("Covered", States(first, "groups")["c0"]);
        Assert.Equal("Rejected", States(first, "groups")["c1"]);
        Assert.Equal(["c1"], first.GetProperty("focus").EnumerateArray().Select(f => f.GetString()));

        // Each candidate's frame totals the weight it satisfies, the figure its caption gives.
        Assert.Equal("1", first.GetProperty("satisfiedWeight").GetString());
        Assert.Contains("satisfies weight 1", first.GetProperty("caption").GetString());
        foreach (var frame in frames.Take(16)) {
            string? weight = frame.GetProperty("satisfiedWeight").GetString();
            Assert.Contains($"satisfies weight {weight},", frame.GetProperty("caption").GetString());
        }

        var last = frames[^1];
        Assert.Equal("Done", last.GetProperty("event").GetString());
        Assert.True(last.GetProperty("ok").GetBoolean());
        Assert.Contains("satisfying weight 3 of 3", last.GetProperty("caption").GetString());
        Assert.Equal("3", last.GetProperty("satisfiedWeight").GetString());
    }

    [Fact]
    public async Task ThePayload_GivesEachClauseItsWeight() {
        var run = await Json("visualizeRun", "solver=WeightedMaxSatBruteForceSolver", Instance);
        var groups = Pictures(run)[Vis].GetProperty("payload").GetProperty("groups").EnumerateArray().ToList();
        Assert.Equal(["C1", "C2"], groups.Select(g => g.GetProperty("label").GetString()));
        Assert.Equal(["1", "2"], groups.Select(g => g.GetProperty("weight").GetString()));   // strings, so no browser rounds them (#1034)
    }

    [Fact]
    public async Task ASolverWithNoSteps_DrawsTheCheckedAnswerAlone() {
        var run = await Json("visualizeRun", "solver=WeightedMaxSatBranchAndBoundSolver", Instance);
        var frames = Pictures(run)[Vis].GetProperty("frames").EnumerateArray().ToList();
        var only = Assert.Single(frames);
        Assert.Equal("Done", only.GetProperty("event").GetString());
        Assert.True(only.GetProperty("ok").GetBoolean());
        Assert.Equal(JsonValueKind.Null, only.GetProperty("step").ValueKind);
        Assert.Equal("3", only.GetProperty("satisfiedWeight").GetString());
    }
}
