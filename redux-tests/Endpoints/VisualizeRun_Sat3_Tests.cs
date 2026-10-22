using System.Net;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_SAT3;
using API.Problems.NPComplete.NPC_SAT3.Solvers;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// visualizeRun (#895) on 3SAT, which has several pictures: every picture comes from the one run, and frames line up by step.
public class VisualizeRun_Sat3_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public VisualizeRun_Sat3_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private static string Instance => ((IProblem)new SAT3()).defaultInstance;

    private Task<(HttpStatusCode Status, JsonElement Body)> Run(string query) =>
        VisualizeRun_Endpoint_Tests.Post(_client, "visualizeRun", query, Instance);

    private static List<JsonElement> FramesOf(JsonElement picture) => picture.GetProperty("frames").EnumerateArray().ToList();

    [Fact]
    public async Task Backtracking_EveryPictureDrawsTheSameSteps_AndFramesLineUpByStep() {
        var (status, body) = await Run("solver=Sat3BacktrackingSolver");
        Assert.Equal(HttpStatusCode.OK, status);
        var pictures = VisualizeRun_Endpoint_Tests.Pictures(body);
        Assert.True(pictures.Count >= 2, "3SAT should have at least two pictures that draw Assignment steps.");
        // The backtracking is deterministic, so a run made here is the run the endpoint made.
        var run = ((ISolver)new Sat3BacktrackingSolver()).Run(Instance, withSteps: true);
        var expected = Enumerable.Range(0, run.Steps.Count).Select(i => (int?)i).ToList();
        foreach (var picture in pictures) {
            var steps = VisualizeRun_Endpoint_Tests.Steps(picture).Where(s => s != null).ToList();
            Assert.Equal(expected, steps);
            // Frames with the same step show the same moment: the step's own caption.
            foreach (var frame in FramesOf(picture)) {
                if (frame.GetProperty("step") is { ValueKind: JsonValueKind.Number } s)
                    Assert.Equal(((SolverStep<Assignment>)run.Steps[s.GetInt32()]).Caption, frame.GetProperty("caption").GetString());
            }
        }
    }

    [Theory]
    [InlineData("PPZ")]
    [InlineData("Schoning")]
    [InlineData("WalkSAT")]
    public async Task RandomizedSolver_EveryPictureDrawsTheSeededRun_AndEndsOnTheOneAnswer(string solver) {
        // A randomized solver records its steps and reports its seed (#932): a run made here with that seed is the run the
        // endpoint made, every picture draws its steps in order, and every picture ends on the one answer the response reports.
        var (status, body) = await Run($"solver={solver}");
        Assert.Equal(HttpStatusCode.OK, status);
        int seed = body.GetProperty("seed").GetInt32();
        var again = (ISolver)Activator.CreateInstance(ProblemProvider.Solvers[solver.ToLowerInvariant()])!;
        RandomizedSolvers.Apply(again, seed);
        var run = again.Run(Instance, withSteps: true);
        Assert.Equal(run.Answer, body.GetProperty("answer").GetString());
        var expected = Enumerable.Range(0, run.Steps.Count).Select(i => (int?)i).ToList();
        var answerOk = body.GetProperty("ok");
        var assignments = new List<Dictionary<string, bool>>();
        var pictures = VisualizeRun_Endpoint_Tests.Pictures(body);
        Assert.True(pictures.Count >= 2);
        foreach (var picture in pictures) {
            Assert.Equal(expected, VisualizeRun_Endpoint_Tests.Steps(picture).Where(s => s != null).ToList());
            var done = FramesOf(picture)[^1];
            Assert.Equal("Done", done.GetProperty("event").GetString());
            if (answerOk.ValueKind != JsonValueKind.Null)
                Assert.Equal(answerOk.GetBoolean(), done.GetProperty("ok").GetBoolean());
            if (done.TryGetProperty("assignment", out var a))
                assignments.Add(a.EnumerateObject().ToDictionary(p => p.Name.StartsWith("v:") ? p.Name[2..] : p.Name, p => p.Value.GetBoolean()));
        }
        Assert.True(assignments.Count >= 2);
        Assert.All(assignments, a => Assert.Equal(assignments[0].OrderBy(p => p.Key), a.OrderBy(p => p.Key)));
    }
}
