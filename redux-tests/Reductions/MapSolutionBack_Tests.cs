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

    // ---- review fixes (#749) ----

    private static SolverStep<TPartial> Done<TPartial>(SolveRun run) {
        var last = run.Steps.Cast<SolverStep<TPartial>>().Last();
        Assert.Equal(StepEvent.Done, last.Event);
        return last;
    }

    private static bool SourceAccepts(IReduction red, string answer) {
        try {
            return red.reductionFrom.defaultVerifier.verify(red.reductionFrom.instance, answer);
        } catch (Exception) {
            return false;
        }
    }

    private static int NodeCount(string nodeSet) => nodeSet.Trim('{', '}').Split(',', StringSplitOptions.RemoveEmptyEntries).Length;

    // Finding 1: "{}" is both the no-answer convention and the empty set. On a complete graph with K = N the
    // target is an edgeless graph with budget 0, whose only (and valid) cover is the empty set.
    [Fact]
    public void CompleteGraph_KEqualsN_EmptyCoverIsAnAnswer_AndMapsBackToTheWholeClique() {
        IReduction red = Build("sipserReductionVertexCover", "(({a,b,c},{{a,b},{b,c},{a,c}}),3)");
        Assert.Equal("{}", red.reductionTo.defaultSolver.solve(red.reductionTo.instance));

        SolveRun run = red.MapBackRun("{}", true)!;
        Assert.Equal("{a,b,c}", run.Answer);
        Assert.True(Done<NodeSet>(run).Ok);
        Assert.True(red.reductionFrom.defaultVerifier.verify(red.reductionFrom.instance, run.Answer));
    }

    // The forward map sends the whole clique to the empty cover; this change leaves mapSolutions alone.
    [Fact]
    public void CompleteGraph_KEqualsN_ForwardMapOfTheWholeClique_IsTheEmptyCover() {
        IReduction red = Build("sipserReductionVertexCover", "(({a,b,c},{{a,b},{b,c},{a,c}}),3)");
        Assert.Equal("{}", red.mapSolutions("{a,b,c}"));
    }

    // Same ambiguity for a Clique target with K = 0: the empty clique is a real answer, and so is the
    // empty independent set it maps back to.
    [Fact]
    public void IndependentSet_KZero_EmptyCliqueIsAnAnswer_AndMapsBackOk() {
        IReduction red = Build("reduceToCLIQUE", "(({a,b},{{a,b}}),0)");
        SolveRun run = red.MapBackRun("{}", true)!;
        Assert.Equal("{}", run.Answer);
        Assert.True(Done<NodeSet>(run).Ok);
    }

    // When B's own rules reject "{}" it still means "no answer".
    [Theory]
    [InlineData("sipserReductionVertexCover", "(({a,b,c},{{a,b}}),3)")]
    [InlineData("reduceToCLIQUE", "(({a,b,c},{{a,b},{b,c},{a,c}}),2)")]
    public void EmptyTargetAnswer_ThatBRejects_StillMeansNoAnswer(string reduction, string instance) {
        IReduction red = Build(reduction, instance);
        SolveRun run = red.MapBackRun("{}", true)!;
        Assert.Equal("{}", run.Answer);
        var step = Assert.Single(run.Steps.Cast<SolverStep<NodeSet>>());
        Assert.False(step.Ok);
        Assert.Contains("no answer", step.Caption);
    }

    [Fact]
    public void ThreeSat_EmptyClique_StillMeansNoAnswer() {
        IReduction red = Build("SipserReduceToCliqueStandard", "(x1) & (!x1)");
        SolveRun run = red.MapBackRun("{}", true)!;
        Assert.Equal("{}", run.Answer);
        Assert.False(Assert.Single(run.Steps.Cast<SolverStep<Assignment>>()).Ok);
    }

    [Fact]
    public async Task Endpoint_CompleteGraph_KEqualsN_EmptyCoverMapsBackToTheWholeClique() {
        var (status, body) = await Post("reduction=sipserReductionVertexCover&solution=" + Uri.EscapeDataString("{}"), "(({a,b,c},{{a,b},{b,c},{a,c}}),3)");
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("\"{a,b,c}\"", body);
    }

    // Finding 2: Done.ok is A's verifier on the mapped answer, and the caption does not claim success otherwise.
    [Fact]
    public void ThreeSat_InvalidClique_DoneIsNotOk() {
        IReduction red = Build("SipserReduceToCliqueStandard", "(x1 | x2 | x3) & (!x1 | x2 | x3)");
        SolveRun run = red.MapBackRun("{x1_0}", true)!;
        Assert.False(SourceAccepts(red, run.Answer));
        var done = Done<Assignment>(run);
        Assert.False(done.Ok);
        Assert.Contains("{x1_0} is not a valid Clique answer", done.Caption);
    }

    [Fact]
    public void VertexCover_InvalidCover_DoneIsNotOk() {
        IReduction red = Build("sipserReductionVertexCover", "(({a,b,c},{{a,b}}),2)");
        SolveRun run = red.MapBackRun("{a}", true)!;
        Assert.False(SourceAccepts(red, run.Answer));
        var done = Done<NodeSet>(run);
        Assert.False(done.Ok);
        Assert.Contains("{a} is not a valid Vertex Cover answer", done.Caption);
    }

    [Fact]
    public void IndependentSet_InvalidClique_DoneIsNotOk() {
        IReduction red = Build("reduceToCLIQUE", "(({a,b,c,d},{{a,b},{b,c},{c,d}}),2)");
        SolveRun run = red.MapBackRun("{a,b}", true)!;
        Assert.False(SourceAccepts(red, run.Answer));
        var done = Done<NodeSet>(run);
        Assert.False(done.Ok);
        Assert.Contains("{a,b} is not a valid Clique answer", done.Caption);
    }

    // Done.ok agrees with A's verifier for every case (B's answer from B's solver).
    [Theory]
    [MemberData(nameof(Cases))]
    public void DoneOk_EqualsSourceVerifierOnTheMappedAnswer(string reduction, string instance) {
        IReduction red = Build(reduction, instance);
        string bAnswer = red.reductionTo.defaultSolver.solve(red.reductionTo.instance);
        SolveRun run = red.MapBackRun(bAnswer, true)!;
        bool? ok = (bool?)run.Steps[^1].GetType().GetProperty("Ok")!.GetValue(run.Steps[^1]);
        bool expected = !ReductionBack.IsNoAnswer(run.Answer) && SourceAccepts(red, run.Answer);
        Assert.Equal(expected, ok);
    }

    // Finding 4: a B answer that leaves more nodes than A needs is cut down to K, which must still be valid.
    [Fact]
    public void VertexCover_CoverSmallerThanNeeded_KeepsExactlyKCliqueNodes() {
        IReduction red = Build("sipserReductionVertexCover", "(({a,b,c,d},{{a,b},{b,c},{a,c},{c,d}}),2)");
        string answer = red.mapSolutionBack("{d}")!;
        Assert.Equal(2, NodeCount(answer));
        Assert.True(red.reductionFrom.defaultVerifier.verify(red.reductionFrom.instance, answer), $"mapped to {answer}");
    }

    [Fact]
    public void IndependentSet_CliqueLargerThanK_KeepsExactlyKNodes() {
        // The complement of the single edge a-b has the 3-clique {a,c,d}; A only needs 2 independent nodes.
        IReduction red = Build("reduceToCLIQUE", "(({a,b,c,d},{{a,b}}),2)");
        string answer = red.mapSolutionBack("{a,c,d}")!;
        Assert.Equal("{a,c}", answer);
        Assert.True(red.reductionFrom.defaultVerifier.verify(red.reductionFrom.instance, answer), $"mapped to {answer}");
    }

    // Finding 5: no two backward-map members differ only by case, and the problem-name property says what it holds.
    [Fact]
    public void BackwardMapMembers_DoNotDifferOnlyByCase() {
        var names = typeof(IReduction).GetMethods().Select(m => m.Name)
            .Concat(typeof(IReversibleReduction<,,>).GetMethods().Select(m => m.Name))
            .Distinct().ToList();
        var clashes = names.GroupBy(n => n.ToLowerInvariant()).Where(g => g.Count() > 1).Select(g => string.Join("/", g)).ToList();
        Assert.True(clashes.Count == 0, "case-only clashes: " + string.Join(", ", clashes));
    }

    [Fact]
    public void ReductionInputException_NamesItsProblemLikeProblemParseException() {
        Assert.Equal(typeof(string), typeof(ReductionInputException).GetProperty("ProblemName")?.PropertyType);
        Assert.Null(typeof(ReductionInputException).GetProperty("Problem"));
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

    // Finding 3: the example Swagger shows for this endpoint must work as written.
    [Fact]
    public async Task Endpoint_DocumentedSwaggerExample_Is200() {
        string swagger = await _client.GetStringAsync("/swagger/v1/swagger.json", TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(swagger);
        var op = doc.RootElement.GetProperty("paths").GetProperty("/ProblemProvider/mapSolutionBack").GetProperty("post");
        var query = new List<string>();
        foreach (var p in op.GetProperty("parameters").EnumerateArray())
            query.Add(p.GetProperty("name").GetString() + "=" + Uri.EscapeDataString(ExampleText(p.GetProperty("example"))));
        string instance = ExampleText(op.GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("example"));

        var (status, body) = await Post(string.Join("&", query), instance);
        Assert.True(status == HttpStatusCode.OK, $"documented example {string.Join("&", query)} answered {(int)status}: {body}");
    }

    private static string ExampleText(JsonElement e) => e.ValueKind == JsonValueKind.String ? e.GetString()! : e.GetRawText();

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
