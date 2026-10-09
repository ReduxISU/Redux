using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Backward maps (#690): B's answer mapped back to an answer A's own verifier accepts.
public class MapSolutionBack_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public MapSolutionBack_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private static IReduction Build(string reduction, string instance) =>
        (IReduction)Activator.CreateInstance(ProblemProvider.Reductions[reduction.ToLower()], instance)!;

    private static string DefaultInstance(string reduction) =>
        ((IReduction)Activator.CreateInstance(ProblemProvider.Reductions[reduction.ToLower()])!).reductionFrom.defaultInstance;

    // Each case: reduction, instance of A. Includes instances where B has no answer.
    public static IEnumerable<object[]> Cases() {
        yield return new object[] { "SipserReduceToCliqueStandard", DefaultInstance("SipserReduceToCliqueStandard") };
        yield return new object[] { "SipserReduceToCliqueStandard", "(x1 | x2 | x3) & (!x1 | !x2 | x3) & (x1 | !x3 | x2)" };
        yield return new object[] { "SipserReduceToCliqueStandard", "(x1 | x2) & (!x1 | x3) & (!x2 | !x3 | y)" };
        yield return new object[] { "SipserReduceToCliqueStandard", "(x1) & (!x1)" };
        yield return new object[] { "sipserReductionVertexCover", DefaultInstance("sipserReductionVertexCover") };
        yield return new object[] { "sipserReductionVertexCover", "(({a,b,c,d},{{a,b},{b,c},{a,c},{c,d}}),3)" };
        yield return new object[] { "sipserReductionVertexCover", "(({a,b,c,d},{{a,b},{b,c},{a,c},{c,d}}),2)" };
        yield return new object[] { "sipserReductionVertexCover", "(({a,b,c,d},{{a,b},{b,c},{c,d}}),3)" };
        yield return new object[] { "reduceToCLIQUE", DefaultInstance("reduceToCLIQUE") };
        yield return new object[] { "reduceToCLIQUE", "(({a,b,c,d},{{a,b},{b,c},{c,d}}),2)" };
        yield return new object[] { "reduceToCLIQUE", "(({a,b,c,d},{{a,b},{b,c},{c,d}}),1)" };
        yield return new object[] { "reduceToCLIQUE", "(({a,b,c},{{a,b},{b,c},{a,c}}),2)" };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void SolveTarget_MapBack_IsAcceptedBySourceVerifier(string reduction, string instance) {
        IReduction red = Build(reduction, instance);
        Assert.True(red.hasBackwardMap());
        string bAnswer = red.reductionTo.defaultSolver.solve(red.reductionTo.instance);
        string aAnswer = red.mapSolutionBack(bAnswer)!;

        if (ReductionBack.IsNoAnswer(bAnswer)) {
            Assert.Equal("{}", aAnswer);
        } else {
            Assert.True(red.reductionFrom.defaultVerifier.verify(red.reductionFrom.instance, aAnswer),
                $"{reduction} on {instance}: B answered {bAnswer}, mapped back to {aAnswer}, which A's verifier rejects");
        }
    }

    [Fact]
    public void Cases_IncludeBothSolvableAndUnsolvableTargets() {
        int solvable = 0, unsolvable = 0;
        foreach (object[] c in Cases()) {
            IReduction red = Build((string)c[0], (string)c[1]);
            if (ReductionBack.IsNoAnswer(red.reductionTo.defaultSolver.solve(red.reductionTo.instance))) unsolvable++; else solvable++;
        }
        Assert.True(solvable >= 6 && unsolvable >= 2, $"solvable={solvable} unsolvable={unsolvable}");
    }

    [Fact]
    public void ThreeSat_UnmentionedVariablesDefaultToFalse_AndAreListedInFormulaOrder() {
        // The clique only mentions x1 and x2, so y is free.
        IReduction red = Build("SipserReduceToCliqueStandard", "(x1 | x2) & (!x2 | y)");
        string answer = red.mapSolutionBack("{x1_0,!x2_1}")!;
        Assert.Equal("(x1:True,x2:False,y:False)", answer);
        Assert.True(red.reductionFrom.defaultVerifier.verify(red.reductionFrom.instance, answer));
    }

    [Fact]
    public void ThreeSat_CliqueWithBothAVariableAndItsNegation_RejectsTheLaterNodeAndIsNotOk() {
        IReduction red = Build("SipserReduceToCliqueStandard", "(x1 | x2) & (!x1 | x2)");
        SolveRun run = red.MapBackRun("{x1_0,!x1_1}", true)!;
        Assert.Equal("(x1:True,x2:False)", run.Answer);
        var steps = run.Steps.Cast<SolverStep<Assignment>>().ToList();
        Assert.Contains(steps, s => s.Event == StepEvent.Reject);
        Assert.False(steps[^1].Ok);
    }

    [Fact]
    public void Steps_AreRecordedOnlyWhenAsked_AndEndWithDone() {
        IReduction red = Build("sipserReductionVertexCover", "(({a,b,c,d},{{a,b},{b,c},{a,c},{c,d}}),3)");
        string cover = red.reductionTo.defaultSolver.solve(red.reductionTo.instance);

        SolveRun without = red.MapBackRun(cover, false)!;
        Assert.Empty(without.Steps);

        SolveRun with = red.MapBackRun(cover, true)!;
        Assert.Equal(without.Answer, with.Answer);
        Assert.Equal(typeof(NodeSet), with.StepShape);
        var steps = with.Steps.Cast<SolverStep<NodeSet>>().ToList();
        Assert.Equal(StepEvent.Done, steps[^1].Event);
        Assert.True(steps[^1].Ok);
        // One step per node of A, then Done; captions use both problems' words.
        Assert.Equal(4 + 1, steps.Count);
        Assert.All(steps.Take(4), s => Assert.Contains("vertex cover", s.Caption));
    }

    [Fact]
    public void NoAnswer_WithSteps_IsOneDoneStep() {
        IReduction red = Build("reduceToCLIQUE", "(({a,b,c},{{a,b},{b,c},{a,c}}),2)");
        SolveRun run = red.MapBackRun("{}", true)!;
        Assert.Equal("{}", run.Answer);
        var step = Assert.Single(run.Steps.Cast<SolverStep<NodeSet>>());
        Assert.Equal(StepEvent.Done, step.Event);
        Assert.False(step.Ok);
    }

    [Fact]
    public void MapBack_UndoesMapSolutions_ForTheSameAnswer() {
        // Forward then back through the graph reductions returns the node set you started with.
        IReduction red = Build("reduceToCLIQUE", "(({a,b,c,d},{{a,b},{b,c},{c,d}}),2)");
        Assert.Equal("{a,c}", red.mapSolutionBack(red.mapSolutions("{a,c}")));
    }

    [Fact]
    public void ReductionsWithoutABackwardMap_ReportNoneAndDoNotMap() {
        IReduction red = Build("KarpVertexCoverToNodeSet", DefaultInstance("KarpVertexCoverToNodeSet"));
        Assert.False(red.hasBackwardMap());
        Assert.Null(red.mapSolutionBack("{a}"));
        Assert.Null(red.MapBackRun("{a}", true));
    }

    // ---- endpoint ----

    private async Task<(HttpStatusCode Status, string Body)> Post(string query, string instance) {
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/mapSolutionBack?{query}", content, TestContext.Current.CancellationToken);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Endpoint_DefaultResponse_IsABareJsonString() {
        var (status, body) = await Post("reduction=reduceToCLIQUE&solution=" + Uri.EscapeDataString("{a,c}"), "(({a,b,c,d},{{a,b},{b,c},{c,d}}),2)");
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("\"{a,c}\"", body);
    }

    [Fact]
    public async Task Endpoint_WithSteps_ReturnsAnswerAndSteps_EndingInDone() {
        var (status, body) = await Post("reduction=SipserReduceToCliqueStandard&steps=true&solution=" + Uri.EscapeDataString("{x1_0,!x2_1}"), "(x1 | x2) & (!x2 | y)");
        Assert.Equal(HttpStatusCode.OK, status);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("(x1:True,x2:False,y:False)", doc.RootElement.GetProperty("answer").GetString());
        var steps = doc.RootElement.GetProperty("steps");
        Assert.Equal("Node x1_0 is in the clique, so x1 = True.", steps[0].GetProperty("caption").GetString());
        Assert.Equal("Accept", steps[0].GetProperty("event").GetString());
        Assert.Equal("x1", steps[0].GetProperty("partial").GetProperty("values").EnumerateObject().Single().Name);
        var last = steps[steps.GetArrayLength() - 1];
        Assert.Equal("Done", last.GetProperty("event").GetString());
        Assert.True(last.GetProperty("ok").GetBoolean());
        // The JSON carries no recorder or type objects.
        Assert.DoesNotContain("StepRecorder", body);
        Assert.DoesNotContain("System.", body);
    }

    [Fact]
    public async Task Endpoint_NoAnswer_MapsToNoAnswer() {
        var (status, body) = await Post("reduction=sipserReductionVertexCover&solution=" + Uri.EscapeDataString("{}"), "(({a,b,c},{{a,b}}),3)");
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("\"{}\"", body);
    }

    [Theory]
    [InlineData("SipserReduceToCliqueStandard", "(x1 | x2) & (!x2 | y)", "not a set")]
    [InlineData("SipserReduceToCliqueStandard", "(x1 | x2) & (!x2 | y)", "{x1_0,nope_7}")]
    [InlineData("sipserReductionVertexCover", "(({a,b,c},{{a,b}}),2)", "{a,,b}")]
    [InlineData("sipserReductionVertexCover", "(({a,b,c},{{a,b}}),2)", "{z}")]
    [InlineData("reduceToCLIQUE", "(({a,b,c},{{a,b}}),2)", "a,b")]
    public async Task Endpoint_MalformedTargetCertificate_Is400WithTheTargetsFormat(string reduction, string instance, string solution) {
        var (status, body) = await Post($"reduction={reduction}&solution=" + Uri.EscapeDataString(solution), instance);
        Assert.Equal(HttpStatusCode.BadRequest, status);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("reduction_input_parse_error", doc.RootElement.GetProperty("error").GetString());
        Assert.Equal(solution, doc.RootElement.GetProperty("received").GetString());

        IReduction red = Build(reduction, instance);
        Assert.Equal(red.reductionTo.certificateFormat, doc.RootElement.GetProperty("expected").GetString());
        Assert.Equal(red.reductionTo.problemName, doc.RootElement.GetProperty("problem").GetString());
    }

    [Fact]
    public async Task Endpoint_ReductionWithoutBackwardMap_Is400NoBackwardMap() {
        string instance = DefaultInstance("KarpVertexCoverToNodeSet");
        var (status, body) = await Post("reduction=KarpVertexCoverToNodeSet&solution=" + Uri.EscapeDataString("{a}"), instance);
        Assert.Equal(HttpStatusCode.BadRequest, status);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("no_backward_map", doc.RootElement.GetProperty("error").GetString());
        Assert.Equal("KarpVertexCoverToNodeSet", doc.RootElement.GetProperty("reduction").GetString());
    }

    [Fact]
    public async Task Endpoint_UnknownReduction_Is400() {
        var (status, body) = await Post("reduction=nope&solution=%7B%7D", "x");
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("unknown_reduction", body);
    }

    [Fact]
    public async Task Endpoint_MalformedSourceInstance_Is400() {
        var (status, _) = await Post("reduction=reduceToCLIQUE&solution=%7B%7D", "not a graph");
        Assert.Equal(HttpStatusCode.BadRequest, status);
    }

    [Theory]
    [InlineData("SipserReduceToCliqueStandard")]
    [InlineData("sipserReductionVertexCover")]
    [InlineData("reduceToCLIQUE")]
    public async Task Endpoint_ReduceAndInfoJson_DoNotMentionTheBackwardMap(string reduction) {
        string instance = DefaultInstance(reduction);
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        string reduce = await (await _client.PostAsync($"/ProblemProvider/reduce?reduction={reduction}", content, TestContext.Current.CancellationToken))
            .Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        string info = await (await _client.GetAsync($"/ProblemProvider/info?interface={reduction}", TestContext.Current.CancellationToken))
            .Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        foreach (string json in new[] { reduce, info }) {
            Assert.DoesNotContain("ackward", json);
            Assert.DoesNotContain("EmptyAnswer", json);
            Assert.DoesNotContain("MapBack", json);
        }
    }
}
