using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Steps;
using API.Interfaces.JSON_Objects.Tables;
using API.Problems.P.P_NFA.Solvers;
using API.Problems.P.P_NFA.Visualizations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The time limit on solves that record steps (the visualize endpoint). Uses the NFA solver on an automaton whose
// every state moves to every state on 'a' and which can never accept: it enumerates 2^n runs on a^n, so it is
// slow enough to be stopped, and it records a step for every move.
[Collection(TimingCollection.Name)]
public class StepTimeLimit_Tests {
    // 2^40 runs: never finishes within a test's lifetime unless stopped.
    internal const string SlowNfa = "(({1,2,3},{a},{(1,a,1),(1,a,2),(2,a,1),(2,a,2)},1,{3}),aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa)";
    // 2^12 runs: finishes quickly.
    private const string SmallNfa = "(({1,2,3},{a},{(1,a,1),(1,a,2),(2,a,1),(2,a,2)},1,{3}),aaaaaaaaaaaa)";

    [Fact]
    public void SlowSolve_WithA50msBudget_StopsQuickly_AndSaysSo() {
        var sw = Stopwatch.StartNew();
        var run = ((ISolver)new NFASolver()).Run(SlowNfa, withSteps: true, TimeSpan.FromMilliseconds(50));
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), $"took {sw.Elapsed}");
        Assert.True(run.TimedOut);
        Assert.NotEqual("{}", run.Answer);
        Assert.Equal(SolveRun.TimedOutAnswer, run.Answer);
        Assert.Equal(typeof(ActiveStates), run.StepShape);

        var steps = run.Steps.Cast<SolverStep<ActiveStates>>().ToList();
        Assert.True(steps.Count <= StepRecorder<ActiveStates>.DefaultCap + 2);
        var done = steps[^1];
        Assert.Equal(StepEvent.Done, done.Event);
        Assert.False(done.Ok);
        Assert.StartsWith("Stopped after 0.05 s and ", done.Caption);
        Assert.Contains("the solver had not finished.", done.Caption);
        Assert.Equal(steps[^2].Partial, done.Partial);
        Assert.Single(steps, s => s.Event == StepEvent.Done);
    }

    [Fact]
    public void RecordingOff_StillStopsAtTheLimit() {
        // #935: a plain solve (no steps) is stopped too, by a recorder that keeps nothing but checks the clock.
        var sw = Stopwatch.StartNew();
        var run = ((ISolver)new NFASolver()).Run(SlowNfa, withSteps: false, TimeSpan.FromMilliseconds(50));
        sw.Stop();
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), $"took {sw.Elapsed}");
        Assert.True(run.TimedOut);
        Assert.Equal(SolveRun.TimedOutAnswer, run.Answer);
        Assert.Empty(run.Steps);
    }

    [Fact]
    public void RecordingOff_WithinTheLimit_AnswersAsBefore() {
        var run = ((ISolver)new NFASolver()).Run(SmallNfa, withSteps: false, TimeSpan.FromSeconds(60));
        Assert.False(run.TimedOut);
        Assert.Equal(new NFASolver().solve(new API.Problems.P.P_NFA.NFA(SmallNfa)), run.Answer);
        Assert.Empty(run.Steps);
    }

    [Fact]
    public void NoBudget_RunsToTheEnd() {
        var run = ((ISolver)new NFASolver()).Run(SmallNfa, withSteps: true);
        Assert.False(run.TimedOut);
        var done = run.Steps.Cast<SolverStep<ActiveStates>>().Last();
        Assert.Equal(StepEvent.Done, done.Event);
        Assert.StartsWith("Rejected: no run accepts the input.", done.Caption);
    }

    // #764: the NFA table draws the runs in the recorded steps (#734), so a stopped solve gives the runs found before
    // the time limit and then a table that says it stopped; nothing searches the NFA again.
    [Fact]
    public void NfaTable_StoppedRun_ListsTheRunsFound_ThenSaysItStopped() {
        var run = ((ISolver)new NFASolver()).Run(SlowNfa, withSteps: true, TimeSpan.FromMilliseconds(50));
        Assert.True(run.TimedOut);
        var sw = Stopwatch.StartNew();
        var tables = new NFATableVisualization().StepsVisualization(new API.Problems.P.P_NFA.NFA(SlowNfa), run.Steps.ToList())
            .Cast<API_TableJSON>().ToList();
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), $"took {sw.Elapsed}");
        var last = tables[^1];
        Assert.Equal(NFATableVisualization.StoppedTitle, last.title);
        Assert.Empty(last.rows);
        Assert.Equal(5, last.columns.Count);
        Assert.All(tables.Take(tables.Count - 1), t => Assert.Contains("Rejected", t.title));
    }

    [Fact]
    public void NfaTable_RunPastTheStepCap_ListsTheRunsInTheKeptSteps() {
        // 2^12 runs take far more steps than the cap keeps, so the table lists the runs in the kept steps and then says
        // the rest were not kept.
        var run = ((ISolver)new NFASolver()).Run(SmallNfa, withSteps: true, TimeSpan.FromSeconds(60));
        Assert.False(run.TimedOut);
        var tables = new NFATableVisualization().StepsVisualization(new API.Problems.P.P_NFA.NFA(SmallNfa), run.Steps.ToList())
            .Cast<API_TableJSON>().ToList();
        int notShown = StepCaptions.NotShown(((SolverStep<ActiveStates>)run.Steps[^1]).Caption);
        Assert.True(notShown > 0);
        Assert.Equal(NFATableVisualization.NotAllRunsTitle(notShown), tables[^1].title);
        Assert.InRange(tables.Count - 1, 1, (1 << 12) - 1);
    }

    [Fact]
    public void StoppedDoneStep_SerializesWithTheUsualFieldsOnly() {
        var run = ((ISolver)new NFASolver()).Run(SlowNfa, withSteps: true, TimeSpan.FromMilliseconds(20));
        var json = JsonDocument.Parse(JsonSerializer.Serialize(run.Steps[^1], run.Steps[^1].GetType(), ProblemProvider.VisualizeJsonOptions())).RootElement;
        Assert.Equal(new[] { "partial", "event", "focus", "caption", "ok" }, json.EnumerateObject().Select(p => p.Name));
        Assert.False(json.GetProperty("ok").GetBoolean());
    }
}

// The app with a 50 ms solve budget for pictures.
public class TinyBudgetAppFactory : AppFactory {
    protected override void ConfigureWebHost(IWebHostBuilder builder) {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { [ProblemProvider.SolveTimeLimitKey] = "0.05" }));
    }
}

[Collection(TimingCollection.Name)]
public class StepTimeLimit_Endpoint_Tests : IClassFixture<TinyBudgetAppFactory> {
    private readonly HttpClient _client;

    public StepTimeLimit_Endpoint_Tests(TinyBudgetAppFactory factory) {
        _client = factory.CreateClient();
    }

    private async Task<(HttpStatusCode Status, string Body, TimeSpan Took)> Post(string path, string instance) {
        var sw = Stopwatch.StartNew();
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync(path, content, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return (response.StatusCode, body, sw.Elapsed);
    }

    [Fact]
    public async Task Frames_StoppedSolve_Returns200_WithoutASolvedPicture() {
        var (status, body, took) = await Post("/ProblemProvider/visualize?visualization=nfavisualization&format=frames", StepTimeLimit_Tests.SlowNfa);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True(took < TimeSpan.FromSeconds(10), $"took {took}");
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal(new[] { "type", "payload", "frames" }, root.EnumerateObject().Select(p => p.Name));
        // NFAVisualization draws no step frames, so a stopped run leaves only the payload (the initial picture):
        // no solved picture is appended because there is no answer.
        Assert.Equal(JsonValueKind.Object, root.GetProperty("payload").ValueKind);
        Assert.Equal(0, root.GetProperty("frames").GetArrayLength());
    }

    [Fact]
    public async Task List_StoppedSolve_Returns200_WithTheUnsolvedPictureLast() {
        var (status, body, took) = await Post("/ProblemProvider/visualize?visualization=nfavisualization", StepTimeLimit_Tests.SlowNfa);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True(took < TimeSpan.FromSeconds(10), $"took {took}");
        var list = JsonDocument.Parse(body).RootElement.EnumerateArray().Select(e => e.GetRawText()).ToList();
        // [initial, unsolved]: the unsolved picture stands in for the solved one.
        Assert.Equal(2, list.Count);
        Assert.Equal(list[0], list[1]);
    }

    // #764: the stopped solve no longer leaves the table to enumerate 2^40 runs with no limit. The test server runs
    // the controller on the calling thread, so the request runs on its own task and the test stops waiting after 30 s:
    // a hang fails the test instead of stalling the run.
    [Theory]
    [InlineData("frames")]
    [InlineData("list")]
    public async Task NfaTable_StoppedSolve_Returns200_WithTheStoppedTable(string format) {
        var ct = TestContext.Current.CancellationToken;
        var sw = Stopwatch.StartNew();
        var content = new StringContent(JsonSerializer.Serialize(StepTimeLimit_Tests.SlowNfa), Encoding.UTF8, "application/json");
        var response = await Task.Run(() => _client.PostAsync($"/ProblemProvider/visualize?visualization=nfatablevisualization&format={format}", content, ct), ct)
            .WaitAsync(TimeSpan.FromSeconds(30), ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), $"took {sw.Elapsed}");
        var root = JsonDocument.Parse(body).RootElement;
        var tables = (format == "frames" ? root.GetProperty("frames") : root).EnumerateArray().ToList();
        // The runs found before the limit, then the table that says the run stopped.
        var table = tables[^1];
        Assert.Equal(NFATableVisualization.StoppedTitle, table.GetProperty("title").GetString());
        Assert.Equal(0, table.GetProperty("rows").GetArrayLength());
        Assert.All(tables.Take(tables.Count - 1), t => Assert.Contains("Rejected", t.GetProperty("title").GetString()));
    }

    [Fact]
    public async Task Solve_IsNotLimited() {
        // The plain solve endpoint records no steps, so the picture budget does not apply to it.
        var (status, body, _) = await Post("/ProblemProvider/solve?solver=nfasolver",
            "(({1,2,3},{a},{(1,a,1),(1,a,2),(2,a,1),(2,a,2)},1,{3}),aaaaaaaaaaaaaaa)");
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("No Solution Exists", body);
    }
}
