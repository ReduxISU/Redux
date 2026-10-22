using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using API.Problems.NPComplete.NPC_SAT3;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// #932: the endpoints that draw a run take an optional seed for a randomized solver (WalkSAT) and report the seed used, so a
// run can be asked for again. Additive: a request without seed, and every solver that is not randomized, answer as before (no
// "seed" field); the list format Redux_GUI reads is unchanged. WalkSAT draws on the Formula, Grouped Graph and factor graph,
// and the Search Tree refuses it.
public class WalkSatSeed_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public WalkSatSeed_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private const string Instance = "(x1 | x2 | !x3) & (!x1 | x4 | x5) & (!x2 | !x4 | x6) & (x3 | !x5 | !x6) & (!x1 | !x2 | !x6)";
    private const string Reduction = "karpsattosat3";

    private async Task<(HttpStatusCode Status, string Body)> Post(string path, string query, string instance) {
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/{path}?{query}", content, TestContext.Current.CancellationToken);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private async Task<JsonElement> Json(string path, string query, string instance) {
        var (status, body) = await Post(path, query, instance);
        Assert.True(status == HttpStatusCode.OK, body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static string ReductionInstance() =>
        ((IReduction)Activator.CreateInstance(ProblemProvider.Reductions[Reduction])!).reductionFrom.defaultInstance;

    public static TheoryData<string> Pictures => new() {
        "Sat3DefaultVisualization", "Sat3FormulaVisualization", "Sat3FactorGraphVisualization",
    };

    [Theory]
    [MemberData(nameof(Pictures))]
    public async Task Visualize_Frames_DrawsWalkSatsSteps_AndTheSameSeedGivesTheSameResponse(string vis) {
        var (_, a) = await Post("visualize", $"visualization={vis}&solver=WalkSAT&format=frames&seed=42", Instance);
        var (_, b) = await Post("visualize", $"visualization={vis}&solver=WalkSAT&format=frames&seed=42", Instance);
        Assert.Equal(a, b);
        var root = JsonDocument.Parse(a).RootElement;
        Assert.Equal(42, root.GetProperty("seed").GetInt32());
        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        Assert.True(frames.Count > 1);
        Assert.StartsWith("Try 1: start from a random assignment", frames[0].GetProperty("caption").GetString());
        Assert.Equal("Done", frames[^1].GetProperty("event").GetString());
        Assert.True(frames[^1].GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task Visualize_WithoutASeed_ReportsTheOneChosen_AndThatSeedRepeatsTheRun() {
        var first = await Json("visualize", "visualization=Sat3FormulaVisualization&solver=WalkSAT&format=frames", Instance);
        int seed = first.GetProperty("seed").GetInt32();
        var (_, again) = await Post("visualize", $"visualization=Sat3FormulaVisualization&solver=WalkSAT&format=frames&seed={seed}", Instance);
        Assert.Equal(first.GetRawText(), JsonDocument.Parse(again).RootElement.GetRawText());
    }

    [Fact]
    public async Task Visualize_ANonRandomizedSolver_HasNoSeed_AndIgnoresOne() {
        var (_, plain) = await Post("visualize", "visualization=Sat3FormulaVisualization&format=frames", Instance);
        var (_, seeded) = await Post("visualize", "visualization=Sat3FormulaVisualization&format=frames&seed=5", Instance);
        Assert.Equal(plain, seeded);
        Assert.False(JsonDocument.Parse(plain).RootElement.TryGetProperty("seed", out _));
    }

    [Fact]
    public async Task Visualize_TheListFormat_IsStillAList_WithOrWithoutASeed() {
        var (_, plain) = await Post("visualize", "visualization=Sat3DefaultVisualization&solver=WalkSAT", Instance);
        var (_, seeded) = await Post("visualize", "visualization=Sat3DefaultVisualization&solver=WalkSAT&seed=8", Instance);
        Assert.Equal(JsonValueKind.Array, JsonDocument.Parse(plain).RootElement.ValueKind);
        Assert.Equal(JsonValueKind.Array, JsonDocument.Parse(seeded).RootElement.ValueKind);
    }

    [Fact]
    public async Task TheSearchTree_RefusesWalkSat() {
        var (status, body) = await Post("visualize", "visualization=Sat3SearchTreeVisualization&solver=WalkSAT&format=frames", Instance);
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("solver_mismatch", JsonDocument.Parse(body).RootElement.GetProperty("error").GetString());
        // Its own solver still draws on it.
        var (ok, _) = await Post("visualize", "visualization=Sat3SearchTreeVisualization&solver=Sat3BacktrackingSolver&format=frames", Instance);
        Assert.Equal(HttpStatusCode.OK, ok);
    }

    [Fact]
    public async Task VisualizeRun_DrawsWalkSatOnEveryPictureButTheSearchTree_AndReportsTheSeed() {
        var root = await Json("visualizeRun", "solver=WalkSAT&seed=99", Instance);
        Assert.Equal(99, root.GetProperty("seed").GetInt32());
        var drawn = root.GetProperty("pictures").EnumerateArray().Select(p => p.GetProperty("visualization").GetString()).ToList();
        Assert.Contains("Sat3FormulaVisualization", drawn);
        Assert.Contains("Sat3DefaultVisualization", drawn);
        Assert.Contains("Sat3FactorGraphVisualization", drawn);
        var tree = Assert.Single(root.GetProperty("unavailable").EnumerateArray(), u => u.GetProperty("visualization").GetString() == "Sat3SearchTreeVisualization");
        Assert.Equal("solver_mismatch", tree.GetProperty("reason").GetString());
        // Every picture draws the same steps: more than the solved frame alone.
        Assert.All(root.GetProperty("pictures").EnumerateArray(), p => Assert.True(p.GetProperty("frames").GetArrayLength() > 1));
        var again = await Json("visualizeRun", "solver=WalkSAT&seed=99", Instance);
        Assert.Equal(root.GetRawText(), again.GetRawText());

        var backtracking = await Json("visualizeRun", "solver=Sat3BacktrackingSolver&seed=99", Instance);
        Assert.False(backtracking.TryGetProperty("seed", out _));
    }

    [Fact]
    public async Task ReductionView_TakesTheSeed_ForWalkSatsSolvePhase() {
        string instance = ReductionInstance();
        var a = await Json("reductionView", $"reduction={Reduction}&solver=WalkSAT&seed=7", instance);
        var b = await Json("reductionView", $"reduction={Reduction}&solver=WalkSAT&seed=7", instance);
        Assert.Equal(a.GetRawText(), b.GetRawText());
        Assert.Equal(7, a.GetProperty("seed").GetInt32());
        // The seed follows the solver.
        Assert.Equal(["type", "kind", "reduction", "reductionName", "solver", "seed"], a.EnumerateObject().Take(6).Select(p => p.Name));
        var solve = a.GetProperty("frames").EnumerateArray().Where(f => f.GetProperty("phase").GetString() == "solve").ToList();
        Assert.Contains(solve, f => f.GetProperty("caption").GetString()!.StartsWith("Try 1: start from a random assignment"));

        var chosen = await Json("reductionView", $"reduction={Reduction}&solver=WalkSAT", instance);
        int seed = chosen.GetProperty("seed").GetInt32();
        var repeated = await Json("reductionView", $"reduction={Reduction}&solver=WalkSAT&seed={seed}", instance);
        Assert.Equal(chosen.GetRawText(), repeated.GetRawText());

        var plain = await Json("reductionView", $"reduction={Reduction}", instance);
        Assert.False(plain.TryGetProperty("seed", out _));
    }

    [Fact]
    public async Task ReductionView_TheSearchTreeTarget_RefusesWalkSat() {
        var (status, body) = await Post("reductionView",
            $"reduction={Reduction}&solver=WalkSAT&targetVisualization=Sat3SearchTreeVisualization", ReductionInstance());
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("solver_mismatch", JsonDocument.Parse(body).RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Info_HasNoSeed() {
        var body = await (await _client.GetAsync("/ProblemProvider/info?interface=WalkSAT", TestContext.Current.CancellationToken))
            .Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("seed", body, StringComparison.OrdinalIgnoreCase);
    }
}
