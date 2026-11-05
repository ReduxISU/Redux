using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_CLIQUE;
using API.Problems.NPComplete.NPC_CLIQUE.Solvers;
using API.Problems.NPComplete.NPC_CLIQUE.Visualizations;
using API.Problems.NPComplete.NPC_SAT3;
using API.Problems.NPComplete.NPC_VERTEXCOVER.Visualizations;
using API.Problems.P.P_DFA;
using API.Problems.P.P_NFA;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// POST /ProblemProvider/visualizeRun (#895): one solver run, every picture of its problem, on the real API JSON.
public class VisualizeRun_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public VisualizeRun_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    internal static async Task<(HttpStatusCode Status, JsonElement Body)> Post(HttpClient client, string path, string query, string instance) {
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"/ProblemProvider/{path}?{query}", content, TestContext.Current.CancellationToken);
        string text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return (response.StatusCode, JsonDocument.Parse(text).RootElement.Clone());
    }

    private Task<(HttpStatusCode Status, JsonElement Body)> Run(string query, string instance) => Post(_client, "visualizeRun", query, instance);

    private static string DefaultInstance<TProblem>() where TProblem : IProblem, new() => new TProblem().defaultInstance;

    /// <summary>The element as one line, so two responses compare by content, not by indentation.</summary>
    internal static string Compact(JsonElement element) => JsonSerializer.Serialize(element);

    internal static List<JsonElement> Pictures(JsonElement body) => body.GetProperty("pictures").EnumerateArray().ToList();

    internal static List<JsonElement> Unavailable(JsonElement body) => body.GetProperty("unavailable").EnumerateArray().ToList();

    internal static List<int?> Steps(JsonElement picture) => picture.GetProperty("frames").EnumerateArray()
        .Select(f => f.GetProperty("step") is { ValueKind: JsonValueKind.Number } s ? (int?)s.GetInt32() : null).ToList();

    /// <summary>Every visualization appears once, in pictures or in unavailable, in the order given.</summary>
    internal static void AssertEachOnce(JsonElement body, IReadOnlyList<string> expectedOrder) {
        var seen = Pictures(body).Select(p => p.GetProperty("visualization").GetString()!)
            .Concat(Unavailable(body).Select(u => u.GetProperty("visualization").GetString()!)).ToList();
        Assert.Equal(expectedOrder.OrderBy(n => n), seen.OrderBy(n => n));
        // Each list keeps the problem's order.
        foreach (var list in new[] { Pictures(body), Unavailable(body) }) {
            var names = list.Select(e => e.GetProperty("visualization").GetString()!).ToList();
            Assert.Equal(names.OrderBy(n => expectedOrder.ToList().IndexOf(n)), names);
        }
    }

    [Fact]
    public async Task Clique_OneRun_EveryVisualizationOnce_DefaultFirst() {
        string instance = DefaultInstance<CLIQUE>();
        var (status, body) = await Run("solver=CliqueBruteForce", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("CliqueBruteForce", body.GetProperty("solver").GetString());
        Assert.Equal(new CliqueBruteForce().solverName, body.GetProperty("solverName").GetString());
        string answer = body.GetProperty("answer").GetString()!;
        Assert.Equal(((IProblem)new CLIQUE()).defaultVerifier.verify(instance, answer), body.GetProperty("ok").GetBoolean());
        Assert.False(body.GetProperty("timedOut").GetBoolean());

        var order = ProblemProvider.VisualizationsOf(typeof(CLIQUE));
        Assert.Equal(nameof(CliqueDefaultVisualization), order[0]);
        AssertEachOnce(body, order);
        var picture = Pictures(body)[0];
        Assert.Equal(nameof(CliqueDefaultVisualization), picture.GetProperty("visualization").GetString());
        Assert.Equal("Graph", picture.GetProperty("type").GetString());
        Assert.Contains(Unavailable(body), u => u.GetProperty("visualization").GetString() == nameof(CliqueLatexVisualization)
            && u.GetProperty("reason").GetString() == "no_frames" && u.GetProperty("detail").GetString()!.Length > 0);
    }

    [Fact]
    public async Task Clique_StepsRise_IndexTheRun_AndAreNullOnTheAddedFrame() {
        string instance = DefaultInstance<CLIQUE>();
        var (_, body) = await Run("solver=CliqueBruteForce", instance);
        // The brute force is deterministic, so a run made here is the run the endpoint made.
        var run = ((ISolver)new CliqueBruteForce()).Run(instance, withSteps: true);
        var picture = Pictures(body)[0];
        var frames = picture.GetProperty("frames").EnumerateArray().ToList();
        var steps = Steps(picture);
        Assert.Equal(Enumerable.Range(0, run.Steps.Count).Select(i => (int?)i), steps.Take(run.Steps.Count));
        for (int i = 0; i < frames.Count; i++) {
            if (steps[i] is not int step) {
                // Only the frame the builder adds, at the end, comes from no step.
                Assert.Equal(frames.Count - 1, i);
                Assert.Equal("Done", frames[i].GetProperty("event").GetString());
                continue;
            }
            Assert.InRange(step, 0, run.Steps.Count - 1);
            Assert.Equal(((SolverStep<NodeSet>)run.Steps[step]).Caption, frames[i].GetProperty("caption").GetString());
        }
    }

    // Frames responses are written on one line (#909): indentation was over half of a large picture's bytes.
    [Theory]
    [InlineData("visualizeRun", "solver=CliqueBruteForce")]
    [InlineData("visualize", "visualization=CliqueDefaultVisualization&solver=CliqueBruteForce&format=frames")]
    public async Task FramesResponses_AreNotIndented(string path, string query) {
        var content = new StringContent(JsonSerializer.Serialize(DefaultInstance<CLIQUE>()), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/{path}?{query}", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain('\n', text);
        Assert.Equal(Compact(JsonDocument.Parse(text).RootElement), text);
    }

    [Fact]
    public async Task Clique_PictureIsTheVisualizeFramesResponse() {
        string instance = DefaultInstance<CLIQUE>();
        var (_, body) = await Run("solver=CliqueBruteForce", instance);
        var (status, single) = await Post(_client, "visualize", "visualization=CliqueDefaultVisualization&solver=CliqueBruteForce&format=frames", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var picture = Pictures(body)[0];
        foreach (string key in new[] { "type", "payload", "frames" })
            Assert.Equal(Compact(single.GetProperty(key)), Compact(picture.GetProperty(key)));
        // The single-visualization response carries step on every frame too.
        Assert.All(single.GetProperty("frames").EnumerateArray(), f => Assert.True(f.TryGetProperty("step", out _)));
    }

    [Fact]
    public async Task Filter_KeepsOnlyTheNamed_InTheProblemsOrder() {
        string instance = DefaultInstance<CLIQUE>();
        var (status, body) = await Run("solver=CliqueBruteForce&visualizations=cliquelatexvisualization, CliqueDefaultVisualization", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        AssertEachOnce(body, [nameof(CliqueDefaultVisualization), nameof(CliqueLatexVisualization)]);
        Assert.Single(Pictures(body));
        Assert.Single(Unavailable(body));

        (status, body) = await Run("solver=CliqueBruteForce&visualizations=CliqueLatexVisualization", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Empty(Pictures(body));
        Assert.Equal(nameof(CliqueLatexVisualization), Assert.Single(Unavailable(body)).GetProperty("visualization").GetString());
    }

    [Fact]
    public async Task Filter_AnotherProblemsVisualization_IsSolverMismatch() {
        var (status, body) = await Run("solver=CliqueBruteForce&visualizations=VertexCoverDefaultVisualization", DefaultInstance<CLIQUE>());
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Empty(Pictures(body));
        var u = Assert.Single(Unavailable(body));
        Assert.Equal(nameof(VertexCoverDefaultVisualization), u.GetProperty("visualization").GetString());
        Assert.Equal("solver_mismatch", u.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task RandomizedSolver_OkIsTheVerifierOnItsAnswer() {
        // PPZ makes random choices (and records its steps and takes a seed since #932).
        string instance = DefaultInstance<SAT3>();
        var (status, body) = await Run("solver=PPZ", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        string answer = body.GetProperty("answer").GetString()!;
        bool? expected;
        try { expected = ((IProblem)new SAT3()).defaultVerifier.verify(instance, answer); } catch { expected = null; }
        var ok = body.GetProperty("ok");
        Assert.Equal(expected, ok.ValueKind == JsonValueKind.Null ? null : ok.GetBoolean());
        AssertEachOnce(body, ProblemProvider.VisualizationsOf(typeof(SAT3)));
        // Every picture comes from the one run: its non-null steps are 0, 1, ... and the rest is one added frame.
        foreach (var picture in Pictures(body))
            FrameStep_TestsAccess.AssertStepsInOrder(picture);
    }

    // The default DFA and NFA runs accept, and their answers are certificates, so the verifier says ok (#896).
    [Theory]
    [InlineData("DFASolver", "1,3,3,2")]   // the default input is bba since #938
    [InlineData("NFASolver", "1,2,3,2\n1,2,2\n1,2")]
    [InlineData("NFASubsetSolver", "1,2")]
    public async Task AutomatonDefault_AcceptedRun_IsOk(string solver, string expected) {
        string instance = solver == "DFASolver" ? DefaultInstance<DFA>() : DefaultInstance<NFA>();
        var (status, body) = await Run($"solver={solver}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(expected, body.GetProperty("answer").GetString()!.Replace("\r\n", "\n"));
        Assert.True(body.GetProperty("ok").GetBoolean());
    }

    // ---- Errors, as visualize gives them ----

    [Theory]
    [InlineData("solver=NoSuchSolver", "unknown_solver")]
    [InlineData("solver=", "unknown_solver")]
    [InlineData("solver=DummySolver", "solver_mismatch")]
    [InlineData("solver=CliqueBruteForce&visualizations=NoSuchVisualization", "unknown_visualization")]
    public async Task BadRequest_NamesTheError(string query, string error) {
        var (status, body) = await Run(query, DefaultInstance<CLIQUE>());
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(error, body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task BadInstance_IsAnInstanceParseError() {
        var (status, body) = await Run("solver=CliqueBruteForce", "not a clique instance");
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("instance_parse_error", body.GetProperty("error").GetString());
        var (vStatus, vBody) = await Post(_client, "visualize", "visualization=CliqueDefaultVisualization&solver=CliqueBruteForce&format=frames", "not a clique instance");
        Assert.Equal(vStatus, status);
        Assert.Equal(vBody.GetProperty("error").GetString(), body.GetProperty("error").GetString());
    }
}

/// <summary>The frame step checks of <see cref="FrameStep_Tests"/>, on a picture of a visualizeRun response.</summary>
static class FrameStep_TestsAccess {
    public static void AssertStepsInOrder(JsonElement picture) =>
        FrameStep_Tests.AssertStepsInOrder(picture.GetProperty("frames").EnumerateArray().ToList());
}
