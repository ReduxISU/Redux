using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// #932: Schöning and PPZ take a seed and report it on the endpoints that draw a run, like WalkSAT (see
// WalkSatSeed_Endpoint_Tests). Both draw on the Formula, Grouped Graph and factor graph; the Search Tree refuses them.
public class SchoningPpzSeed_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public SchoningPpzSeed_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private const string Instance = "(x1 | x2 | !x3) & (!x1 | x4 | x5) & (!x2 | !x4 | x6) & (x3 | !x5 | !x6) & (!x1 | !x2 | !x6)";
    private const string Reduction = "karpsattosat3";

    public static TheoryData<string> Names => new() { "Schoning", "PPZ" };

    // Each solver, and how its first step begins.
    public static TheoryData<string, string> Solvers => new() {
        { "Schoning", "Try 1: start from a random assignment" },
        { "PPZ", "Try 1: take the variables in a random order" },
    };

    public static TheoryData<string, string, string> SolversAndPictures {
        get {
            var data = new TheoryData<string, string, string>();
            foreach (var (solver, first) in new[] { ("Schoning", "Try 1: start from a random assignment"), ("PPZ", "Try 1: take the variables in a random order") })
                foreach (var vis in new[] { "Sat3DefaultVisualization", "Sat3FormulaVisualization", "Sat3FactorGraphVisualization" })
                    data.Add(solver, vis, first);
            return data;
        }
    }

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

    [Theory]
    [MemberData(nameof(SolversAndPictures))]
    public async Task Visualize_Frames_DrawsTheSteps_AndTheSameSeedGivesTheSameResponse(string solver, string vis, string first) {
        var (_, a) = await Post("visualize", $"visualization={vis}&solver={solver}&format=frames&seed=42", Instance);
        var (_, b) = await Post("visualize", $"visualization={vis}&solver={solver}&format=frames&seed=42", Instance);
        Assert.Equal(a, b);
        var root = JsonDocument.Parse(a).RootElement;
        Assert.Equal(42, root.GetProperty("seed").GetInt32());
        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        Assert.True(frames.Count > 1);
        Assert.StartsWith(first, frames[0].GetProperty("caption").GetString());
        Assert.Equal("Done", frames[^1].GetProperty("event").GetString());
    }

    [Theory]
    [MemberData(nameof(Names))]
    public async Task Visualize_WithoutASeed_ReportsTheOneChosen_AndThatSeedRepeatsTheRun(string solver) {
        var first = await Json("visualize", $"visualization=Sat3FormulaVisualization&solver={solver}&format=frames", Instance);
        int seed = first.GetProperty("seed").GetInt32();
        var (_, again) = await Post("visualize", $"visualization=Sat3FormulaVisualization&solver={solver}&format=frames&seed={seed}", Instance);
        Assert.Equal(first.GetRawText(), JsonDocument.Parse(again).RootElement.GetRawText());
    }

    [Theory]
    [MemberData(nameof(Names))]
    public async Task Visualize_TheListFormat_IsStillAList_WithOrWithoutASeed(string solver) {
        var (_, plain) = await Post("visualize", $"visualization=Sat3DefaultVisualization&solver={solver}", Instance);
        var (_, seeded) = await Post("visualize", $"visualization=Sat3DefaultVisualization&solver={solver}&seed=8", Instance);
        Assert.Equal(JsonValueKind.Array, JsonDocument.Parse(plain).RootElement.ValueKind);
        Assert.Equal(JsonValueKind.Array, JsonDocument.Parse(seeded).RootElement.ValueKind);
    }

    [Theory]
    [MemberData(nameof(Names))]
    public async Task TheSearchTree_RefusesTheSolver(string solver) {
        var (status, body) = await Post("visualize", $"visualization=Sat3SearchTreeVisualization&solver={solver}&format=frames", Instance);
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("solver_mismatch", JsonDocument.Parse(body).RootElement.GetProperty("error").GetString());
    }

    [Theory]
    [MemberData(nameof(Names))]
    public async Task VisualizeRun_DrawsTheSolverOnEveryPictureButTheSearchTree_AndReportsTheSeed(string solver) {
        var root = await Json("visualizeRun", $"solver={solver}&seed=99", Instance);
        Assert.Equal(99, root.GetProperty("seed").GetInt32());
        var drawn = root.GetProperty("pictures").EnumerateArray().Select(p => p.GetProperty("visualization").GetString()).ToList();
        Assert.Contains("Sat3FormulaVisualization", drawn);
        Assert.Contains("Sat3DefaultVisualization", drawn);
        Assert.Contains("Sat3FactorGraphVisualization", drawn);
        var tree = Assert.Single(root.GetProperty("unavailable").EnumerateArray(), u => u.GetProperty("visualization").GetString() == "Sat3SearchTreeVisualization");
        Assert.Equal("solver_mismatch", tree.GetProperty("reason").GetString());
        Assert.All(root.GetProperty("pictures").EnumerateArray(), p => Assert.True(p.GetProperty("frames").GetArrayLength() > 1));
        var again = await Json("visualizeRun", $"solver={solver}&seed=99", Instance);
        Assert.Equal(root.GetRawText(), again.GetRawText());
    }

    [Theory]
    [MemberData(nameof(Solvers))]
    public async Task ReductionView_TakesTheSeed_ForTheSolvePhase(string solver, string first) {
        string instance = ReductionInstance();
        var a = await Json("reductionView", $"reduction={Reduction}&solver={solver}&seed=7", instance);
        var b = await Json("reductionView", $"reduction={Reduction}&solver={solver}&seed=7", instance);
        Assert.Equal(a.GetRawText(), b.GetRawText());
        Assert.Equal(7, a.GetProperty("seed").GetInt32());
        Assert.Equal(["type", "kind", "reduction", "reductionName", "solver", "seed"], a.EnumerateObject().Take(6).Select(p => p.Name));
        var solve = a.GetProperty("frames").EnumerateArray().Where(f => f.GetProperty("phase").GetString() == "solve").ToList();
        Assert.Contains(solve, f => f.GetProperty("caption").GetString()!.StartsWith(first));

        var chosen = await Json("reductionView", $"reduction={Reduction}&solver={solver}", instance);
        int seed = chosen.GetProperty("seed").GetInt32();
        var repeated = await Json("reductionView", $"reduction={Reduction}&solver={solver}&seed={seed}", instance);
        Assert.Equal(chosen.GetRawText(), repeated.GetRawText());
    }

    [Theory]
    [MemberData(nameof(Names))]
    public async Task ReductionView_TheSearchTreeTarget_RefusesTheSolver(string solver) {
        var (status, body) = await Post("reductionView",
            $"reduction={Reduction}&solver={solver}&targetVisualization=Sat3SearchTreeVisualization", ReductionInstance());
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("solver_mismatch", JsonDocument.Parse(body).RootElement.GetProperty("error").GetString());
    }

    [Theory]
    [InlineData("Schoning")]
    [InlineData("PPZ")]
    public async Task Info_HasNoSeed(string solver) {
        var body = await (await _client.GetAsync($"/ProblemProvider/info?interface={solver}", TestContext.Current.CancellationToken))
            .Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("seed", body, StringComparison.OrdinalIgnoreCase);
    }
}
