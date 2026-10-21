using System.Net;
using System.Numerics;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_INTPROGRAMMING01;
using API.Problems.NPComplete.NPC_PRIMEFACTOR;
using API.Problems.NPComplete.NPC_PRIMEFACTOR.Solvers;
using API.Problems.NPComplete.NPC_SUBSETSUM;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The payload-driven Formula views of Prime Factorization (equation), 0-1 Integer Programming (stacked constraint rows) and
// Subset Sum (equation) (#929), on the actual endpoint output. They are extra, opt-in views: the problems' defaults, their
// list formats and everything Redux_GUI reads stay as they were.
public class FormulaProblems_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public FormulaProblems_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private const string PrimeVis = "PrimeFactorFormulaVisualization";
    private const string IlpVis = "IntProgrammingFormulaVisualization";
    private const string SubsetVis = "SubsetSumFormulaVisualization";

    private async Task<(HttpStatusCode Status, string Body)> Post(string path, string query, string instance) {
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/{path}?{query}", content, TestContext.Current.CancellationToken);
        return (response.StatusCode, (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Replace("\r\n", "\n"));
    }

    private async Task<JsonElement> Frames(string vis, string instance, string extra = "") {
        var (status, body) = await Post("visualize", $"visualization={vis}&format=frames{extra}", instance);
        Assert.True(status == HttpStatusCode.OK, body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static List<JsonElement> FrameList(JsonElement response) => response.GetProperty("frames").EnumerateArray().ToList();

    private static Dictionary<string, string> States(JsonElement frame, string collection) =>
        frame.GetProperty(collection).EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetProperty("state").GetString()!);

    private static readonly HashSet<string> Vocabulary = new() {
        "Background", "ElementHighlight", "Solution", "Rejected", "Untraveled", "Covered", "Blocked", "False", "Committed",
    };

    private static IEnumerable<JsonElement> Pieces(JsonElement expr) {
        yield return expr;
        if (expr.TryGetProperty("args", out var args))
            foreach (var arg in args.EnumerateArray())
                foreach (var p in Pieces(arg)) yield return p;
    }

    private static string DefaultInstance(string problem) => ((IProblem)Activator.CreateInstance(ProblemProvider.Problems[problem])!).defaultInstance;

    // vis, problem, its default visualization, instance (null: the problem's default), layout, legacy visualizationType
    public static TheoryData<string, string, string, string?, string, string> Cases => new() {
        { PrimeVis, "primefactor", "ShorsDefaultVisualization", null, "equation", "QuantumCircuitQjs" },
        { PrimeVis, "primefactor", "ShorsDefaultVisualization", "60", "equation", "QuantumCircuitQjs" },
        { PrimeVis, "primefactor", "ShorsDefaultVisualization", "97", "equation", "QuantumCircuitQjs" },
        { IlpVis, "intprogramming01", "IntProgrammingVisualization", null, "rows", "DynamicTable" },
        { IlpVis, "intprogramming01", "IntProgrammingVisualization", "(2 3 -1),(1 1 0)<=(1 1)", "rows", "DynamicTable" },
        { IlpVis, "intprogramming01", "IntProgrammingVisualization", "(1 1),(-1 -1)<=(1 -2)", "rows", "DynamicTable" },
        { SubsetVis, "subsetsum", "SubsetSumVisualization", null, "equation", "SetD3" },
        { SubsetVis, "subsetsum", "SubsetSumVisualization", "({1,7,12,15},28)", "equation", "SetD3" },
        { SubsetVis, "subsetsum", "SubsetSumVisualization", "({1,7},5)", "equation", "SetD3" },
    };

    // ---- format=frames: the contract every frame keeps ----

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Frames_AreAFormula_WhoseEveryStateNamesAPieceOfIt(string vis, string problem, string defaultVis, string? instance, string layout, string legacy) {
        _ = (defaultVis, legacy);
        instance ??= DefaultInstance(problem);
        var root = await Frames(vis, instance);
        Assert.Equal("Formula", root.GetProperty("type").GetString());
        var payload = root.GetProperty("payload");
        Assert.Equal("formula", payload.GetProperty("kind").GetString());
        Assert.Equal(layout, payload.GetProperty("layout").GetString());
        Assert.False(payload.TryGetProperty("nodes", out _));
        var groups = payload.GetProperty("groups").EnumerateArray().ToList();
        var groupIds = groups.Select(g => g.GetProperty("id").GetString()!).ToHashSet();
        var variableIds = payload.GetProperty("variables").EnumerateArray().Select(v => v.GetProperty("id").GetString()!).ToHashSet();
        Assert.All(payload.GetProperty("variables").EnumerateArray(), v => Assert.Equal("int", v.GetProperty("type").GetString()));
        var payloadPieces = groups.SelectMany(g => Pieces(g.GetProperty("expr"))).ToList();
        AssertWellFormed(payloadPieces, variableIds);

        var frames = FrameList(root);
        Assert.NotEmpty(frames);
        foreach (var f in frames) {
            var exprs = f.TryGetProperty("exprs", out var e) ? e.EnumerateObject().ToList() : [];
            Assert.All(exprs, p => Assert.Contains(p.Name, groupIds));
            var framePieces = exprs.SelectMany(p => Pieces(p.Value)).ToList();
            AssertWellFormed(framePieces, variableIds);
            var pieceIds = payloadPieces.Concat(framePieces).Where(p => p.TryGetProperty("id", out _)).Select(p => p.GetProperty("id").GetString()!).ToHashSet();
            bool isDone = f.GetProperty("event").GetString() == "Done";
            Assert.Equal(["event", "caption", "focus", "nodes", "groups", "assignment", "variables", "step", .. isDone ? new[] { "ok" } : [], .. exprs.Count > 0 ? new[] { "exprs" } : []],
                f.EnumerateObject().Select(p => p.Name));
            foreach (var (id, state) in States(f, "nodes")) { Assert.Contains(id, pieceIds); Assert.Contains(state, Vocabulary); }
            foreach (var (id, state) in States(f, "groups")) { Assert.Contains(id, groupIds); Assert.Contains(state, Vocabulary); }
            foreach (var (id, state) in States(f, "variables")) { Assert.Contains(id, variableIds); Assert.Contains(state, Vocabulary); }
            foreach (var p in f.GetProperty("assignment").EnumerateObject()) {
                Assert.Contains(p.Name, variableIds);
                Assert.Equal(JsonValueKind.Number, p.Value.ValueKind);
            }
            foreach (var id in f.GetProperty("focus").EnumerateArray().Select(x => x.GetString()!))
                Assert.True(pieceIds.Contains(id) || groupIds.Contains(id) || variableIds.Contains(id), $"focus {id} names nothing");
            // A note is only for stacked rows and equations, as every one of these views is.
            foreach (var g in f.GetProperty("groups").EnumerateObject())
                if (g.Value.TryGetProperty("note", out var note)) Assert.False(string.IsNullOrEmpty(note.GetString()));
        }
        // Green only on the confirmed answer.
        var last = frames[^1];
        Assert.Equal("Done", last.GetProperty("event").GetString());
        if (!last.GetProperty("ok").GetBoolean())
            Assert.DoesNotContain("Solution", frames.SelectMany(f => States(f, "nodes").Values.Concat(States(f, "variables").Values)));
    }

    private static void AssertWellFormed(List<JsonElement> pieces, HashSet<string> variableIds) {
        var ids = new List<string>();
        foreach (var p in pieces) {
            var kinds = new[] { "op", "var", "num", "label" }.Where(k => p.TryGetProperty(k, out _)).ToList();
            Assert.Single(kinds);
            if (p.TryGetProperty("op", out var op)) {
                Assert.Contains(op.GetString()!, FormulaOps.Names);
                Assert.NotEmpty(p.GetProperty("args").EnumerateArray());
            }
            if (p.TryGetProperty("var", out var v)) Assert.Contains(v.GetString()!, variableIds);
            if (p.TryGetProperty("variable", out var tied)) Assert.Contains(tied.GetString()!, variableIds);
            Assert.True(p.TryGetProperty("id", out var id), "every piece has an id");
            ids.Add(id.GetString()!);
        }
        // Ids are unique within one expression tree set.
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    // ---- each problem's solver, step by step ----

    [Theory]
    [InlineData("60", "(2,2,3,5)")]
    [InlineData("97", "(97)")]
    [InlineData("100", "(2,2,5,5)")]
    [InlineData("6", "(2,3)")]
    public async Task PrimeFactorization_FollowsTrialDivision_TheFactorsTimesWhatIsLeftAlwaysMakeN(string n, string answer) {
        var root = await Frames(PrimeVis, n);
        var frames = FrameList(root);
        var steps = SolverSteps(n);
        Assert.Equal(steps.Count, frames.Count);
        BigInteger target = BigInteger.Parse(n);
        for (int k = 0; k < frames.Count; k++) {
            var f = frames[k];
            Assert.Equal(k, f.GetProperty("step").GetInt32());
            Assert.Equal(steps[k].Event.ToString(), f.GetProperty("event").GetString());
            Assert.Equal(steps[k].Caption, f.GetProperty("caption").GetString());
            var right = f.GetProperty("exprs").GetProperty("e0").GetProperty("args")[1];
            var numbers = (right.TryGetProperty("args", out var a) ? a.EnumerateArray().ToList() : [right]).Select(x => BigInteger.Parse(x.GetProperty("num").GetRawText())).ToList();
            Assert.Equal(target, numbers.Aggregate(BigInteger.One, (x, y) => x * y));
            var assignment = f.GetProperty("assignment");
            if (steps[k].Partial.Divisor is string d) Assert.Equal(d, assignment.GetProperty("v:i").GetRawText());
            Assert.Equal(steps[k].Partial.Remaining, assignment.GetProperty("v:rest").GetRawText());
            string ev = f.GetProperty("event").GetString()!;
            var vars = States(f, "variables");
            if (ev == "Reject") Assert.Equal("Rejected", vars["v:i"]);
            if (ev == "Accept") {
                // The new factor is highlighted, and it is the one in the focus.
                string fresh = Assert.Single(f.GetProperty("focus").EnumerateArray()).GetString()!;
                Assert.Equal("ElementHighlight", States(f, "nodes")[fresh]);
            }
        }
        var done = frames[^1];
        Assert.True(done.GetProperty("ok").GetBoolean());
        Assert.Equal("Covered", States(done, "groups")["e0"]);
        var factors = answer.Trim('(', ')').Split(',');
        Assert.All(Enumerable.Range(0, factors.Length), k => Assert.Equal("Solution", States(done, "nodes")[$"f{k}"]));
        Assert.Equal("1", done.GetProperty("assignment").GetProperty("v:rest").GetRawText());
    }

    private static List<SolverStep<QuantumProgress>> SolverSteps(string n) =>
        ((ISolver)new PrimeFactorSolver()).Run(n, withSteps: true).Steps.Cast<SolverStep<QuantumProgress>>().ToList();

    [Fact]
    public void PrimeFactorization_StepsRecordTheDivisorAndWhatIsLeft() {
        var steps = SolverSteps("60");
        Assert.Equal(["Accept", "Accept", "Accept", "Accept", "Done"], steps.Select(s => s.Event.ToString()));
        Assert.Equal([("2", "30"), ("2", "15"), ("3", "5"), (null, "1"), (null, "1")], steps.Select(s => (s.Partial.Divisor, s.Partial.Remaining)));
        var odd = SolverSteps("15");
        Assert.Equal("Reject", odd[0].Event.ToString());
        Assert.Equal(("2", "15"), (odd[0].Partial.Divisor, odd[0].Partial.Remaining));
        // The answer is unchanged, and the recorded result too.
        Assert.Equal("(2,2,3,5)", steps[^1].Partial.Result);
        Assert.Equal("(2,2,3,5)", new PrimeFactorSolver().solve(new PRIMEFACTOR("60")));
    }

    [Fact]
    public void QuantumProgress_WritesTheDivisorAndWhatIsLeft_OnlyWhenSet() {
        var options = new JsonSerializerOptions(FramesResponse.JsonOptions) { WriteIndented = false };
        Assert.Equal("{\"applied\":0,\"queries\":[],\"outcome\":null,\"counts\":{},\"result\":\"(2)\"}",
            JsonSerializer.Serialize(new QuantumProgress(result: "(2)"), options));
        Assert.Equal("{\"applied\":0,\"queries\":[],\"outcome\":null,\"counts\":{},\"result\":\"(2)\",\"divisor\":\"2\",\"remaining\":\"30\"}",
            JsonSerializer.Serialize(new QuantumProgress(result: "(2)") { Divisor = "2", Remaining = "30" }, options));
        Assert.NotEqual(new QuantumProgress(result: "(2)"), new QuantumProgress(result: "(2)") { Remaining = "1" });
    }

    [Fact]
    public async Task IntegerProgramming_WritesTheConstraintsOut_AndNotesEachRowsTotal() {
        // x1 + x2 <= 1 and -x1 - x2 <= -2 (at least two): no 0-1 assignment works.
        var root = await Frames(IlpVis, "(1 1),(-1 -1)<=(1 -2)");
        var payload = root.GetProperty("payload");
        Assert.Equal(("and", "stack", "constraint"), (payload.GetProperty("outer").GetString(), payload.GetProperty("arrange").GetString(), payload.GetProperty("noun").GetString()));
        Assert.Equal(["R1", "R2"], payload.GetProperty("groups").EnumerateArray().Select(g => g.GetProperty("label").GetString()));
        var r0 = payload.GetProperty("groups")[0].GetProperty("expr");
        Assert.Equal("{\"id\":\"r0-rel\",\"op\":\"le\",\"args\":[{\"id\":\"r0-s0\",\"op\":\"plus\",\"args\":[{\"id\":\"r0-x0\",\"var\":\"v:x1\"},{\"id\":\"r0-x1\",\"var\":\"v:x2\"}]},{\"id\":\"r0-d\",\"num\":1}]}",
            JsonSerializer.Serialize(r0));
        var r1 = payload.GetProperty("groups")[1].GetProperty("expr");
        Assert.Equal("{\"id\":\"r1-rel\",\"op\":\"le\",\"args\":[{\"id\":\"r1-s0\",\"op\":\"minus\",\"args\":[{\"id\":\"r1-n0\",\"op\":\"neg\",\"args\":[{\"id\":\"r1-x0\",\"var\":\"v:x1\"}]},{\"id\":\"r1-x1\",\"var\":\"v:x2\"}]},{\"id\":\"r1-d\",\"num\":-2}]}",
            JsonSerializer.Serialize(r1));
        var frames = FrameList(root);
        Assert.Equal(["Reject", "Reject", "Reject", "Reject", "Done"], frames.Select(f => f.GetProperty("event").GetString()));
        // The first try, all zeros, breaks row 2 only.
        var first = frames[0];
        Assert.Equal(["r1"], first.GetProperty("focus").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal("total 0 ≤ 1", first.GetProperty("groups").GetProperty("r0").GetProperty("note").GetString());
        Assert.Equal("total 0 > -2", first.GetProperty("groups").GetProperty("r1").GetProperty("note").GetString());
        Assert.Equal(("Covered", "Rejected"), (States(first, "groups")["r0"], States(first, "groups")["r1"]));
        Assert.All(States(first, "variables").Values, s => Assert.Equal("False", s));
        // The last try, (1 1), breaks row 1: its 1s are Rejected inside it; the Done keeps that try, not ok (#905).
        var done = frames[^1];
        Assert.False(done.GetProperty("ok").GetBoolean());
        Assert.Equal("Rejected", States(done, "groups")["r0"]);
        Assert.Equal("Rejected", States(done, "nodes")["r0-x0"]);
        Assert.Equal(1, done.GetProperty("assignment").GetProperty("v:x1").GetInt32());
    }

    [Fact]
    public async Task SubsetSum_StrikesTheNumbersLeftOut_AndNotesTheSum() {
        var frames = FrameList(await Frames(SubsetVis, "({1,7,12,15},28)"));
        Assert.Equal(14, frames.Count);
        var first = frames[0];
        Assert.Equal(("ElementHighlight", "False"), (States(first, "nodes")["s0"], States(first, "nodes")["s1"]));
        Assert.Equal("sum 1 ≠ 28", first.GetProperty("groups").GetProperty("e0").GetProperty("note").GetString());
        var done = frames[^1];
        Assert.True(done.GetProperty("ok").GetBoolean());
        Assert.Equal("sum 28 = 28", done.GetProperty("groups").GetProperty("e0").GetProperty("note").GetString());
        Assert.Equal(["Solution", "False", "Solution", "Solution"], new[] { "s0", "s1", "s2", "s3" }.Select(id => States(done, "nodes")[id]));
        // No subset adds up to T: a blank Done.
        var none = FrameList(await Frames(SubsetVis, "({1,7},5)"))[^1];
        Assert.False(none.GetProperty("ok").GetBoolean());
        foreach (var key in new[] { "nodes", "groups", "assignment", "variables" })
            Assert.Empty(none.GetProperty(key).EnumerateObject());
    }

    [Fact]
    public async Task SubsetSum_AnIncrementalSolver_KeepsItsNumbersCommitted() {
        var frames = FrameList(await Frames(SubsetVis, "({1,7,12,15},28)", "&solver=FastApproximation"));
        var midRun = frames.Take(frames.Count - 1).SelectMany(f => States(f, "nodes").Values).ToList();
        Assert.DoesNotContain("Solution", frames.Take(frames.Count - 2).SelectMany(f => States(f, "nodes").Values));
        Assert.Contains("Committed", midRun);
    }

    // ---- What Redux_GUI reads stays as it was ----

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task List_IsExactlyTheDefaultPicturesList(string vis, string problem, string defaultVis, string? instance, string layout, string legacy) {
        _ = (layout, legacy);
        instance ??= DefaultInstance(problem);
        var (status, body) = await Post("visualize", $"visualization={vis}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var (_, expected) = await Post("visualize", $"visualization={defaultVis}", instance);
        Assert.Equal(expected, body);
    }

    [Theory]
    [InlineData(PrimeVis, "Prime Factorization Formula", "QuantumCircuitQjs", "PrimeFactorSolver")]
    [InlineData(IlpVis, "0-1 Integer Programming Formula", "DynamicTable", "IntegerProgrammingBruteForce")]
    [InlineData(SubsetVis, "Subset Sum Formula", "SetD3", "SubsetSumBruteForce")]
    public async Task Info_ReportsTheDefaultsLegacyType_AndTheFormulaPictureType_AndNoFramesPlumbing(string vis, string name, string legacy, string solver) {
        var json = JsonDocument.Parse(await _client.GetStringAsync($"/ProblemProvider/info?interface={vis}", TestContext.Current.CancellationToken)).RootElement;
        Assert.Equal(name, json.GetProperty("visualizationName").GetString());
        Assert.Equal(["visualizationName", "visualizationDefinition", "source", "sourceFile", "contributors", "visualizationType", "pictureType", "solver"],
            json.EnumerateObject().Select(p => p.Name));
        Assert.Equal((legacy, "Formula"), (json.GetProperty("visualizationType").GetString(), json.GetProperty("pictureType").GetString()));
        var own = (ISolver)Activator.CreateInstance(ProblemProvider.Solvers[solver.ToLower()])!;
        Assert.Equal(own.solverName, json.GetProperty("solver").GetProperty("solverName").GetString());
        // allInfo has it only for clients that opt in.
        var gui = JsonDocument.Parse(await _client.GetStringAsync("/Navigation/Batch/allInfo", TestContext.Current.CancellationToken)).RootElement;
        Assert.False(gui.TryGetProperty(vis, out _));
        var all = JsonDocument.Parse(await _client.GetStringAsync("/Navigation/Batch/allInfo?includePictures=true", TestContext.Current.CancellationToken)).RootElement;
        Assert.Equal((legacy, "Formula"), (all.GetProperty(vis).GetProperty("visualizationType").GetString(), all.GetProperty(vis).GetProperty("pictureType").GetString()));
    }

    [Theory]
    [InlineData("PRIMEFACTOR", "ShorsDefaultVisualization", PrimeVis)]
    [InlineData("INTPROGRAMMING01", "IntProgrammingVisualization", IlpVis)]
    [InlineData("SUBSETSUM", "SubsetSumVisualization", SubsetVis)]
    public async Task TheProblemsDefault_IsUnchanged_AndTheFormulaIsListedOnlyForClientsThatOptIn(string problem, string defaultVis, string vis) {
        var p = (IProblem)Activator.CreateInstance(ProblemProvider.Problems[problem.ToLower()])!;
        Assert.Equal(defaultVis, p.defaultVisualization.GetType().Name);
        async Task<List<string?>> Listed(string extra) => JsonDocument.Parse(await _client.GetStringAsync($"/Navigation/Problem_VisualizationsRefactor?chosenProblem={problem}{extra}", TestContext.Current.CancellationToken))
            .RootElement.EnumerateArray().Select(e => e.GetString()).ToList();
        var listed = await Listed("");
        // The default is listed for Redux_GUI only if it is not an opt-in view itself (0-1 ILP's and Subset Sum's are).
        Assert.Equal(!OptInVisualizations.IsOptIn(p.defaultVisualization.GetType()), listed.Contains(defaultVis));
        Assert.DoesNotContain(vis, listed);
        var withPictures = await Listed("&includePictures=true");
        Assert.Contains(defaultVis, withPictures);
        Assert.Contains(vis, withPictures);
    }

    [Fact]
    public async Task ShorsSteps_DoNotCarryTheDivisorFields() {
        var (status, body) = await Post("visualize", "visualization=ShorsDefaultVisualization&format=frames&solver=ShorsQuantumSolver", new PRIMEFACTOR().defaultInstance);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.DoesNotContain("\"divisor\"", body);
        Assert.DoesNotContain("\"remaining\"", body);
    }

    [Fact]
    public async Task VisualizeRun_DrawsTheSubsetSumFormula_FromTheSameRunAsThePackingPicture() {
        var (status, body) = await Post("visualizeRun", "solver=SubsetSumBruteForce", "({1,7,12,15},28)");
        Assert.True(status == HttpStatusCode.OK, body);
        var pictures = JsonDocument.Parse(body).RootElement.GetProperty("pictures").EnumerateArray().ToDictionary(p => p.GetProperty("visualization").GetString()!);
        Assert.Equal("Formula", pictures[SubsetVis].GetProperty("type").GetString());
        Assert.Equal(FrameList(pictures["SubsetSumVisualization"]).Select(f => f.GetProperty("step").GetRawText()),
            FrameList(pictures[SubsetVis]).Select(f => f.GetProperty("step").GetRawText()));
    }

    [Fact]
    public void ThePayloadIds_MatchTheMockup() {
        var ilp = API.Problems.NPComplete.NPC_INTPROGRAMMING01.Visualizations.IntProgrammingFormulaVisualization.Payload(new INTPROGRAMMING01("(2 3 -1)<=(5)"));
        Assert.Equal(["r0-rel", "r0-s1", "r0-s0", "r0-m0", "r0-c0", "r0-x0", "r0-m1", "r0-c1", "r0-x1", "r0-x2", "r0-d"],
            ilp.Groups[0].Expr!.Pieces().Select(p => p.Id));
        var subset = API.Problems.NPComplete.NPC_SUBSETSUM.Visualizations.SubsetSumFormulaVisualization.Payload(new SUBSETSUM("({1,7},8)"));
        Assert.Equal(["eq", "sum", "s0", "s1", "T"], subset.Groups[0].Expr!.Pieces().Select(p => p.Id));
        var prime = PrimeFactorFormulaVisualization.Payload(new PRIMEFACTOR("60"));
        Assert.Equal(["eq", "n", "rest"], prime.Groups[0].Expr!.Pieces().Select(p => p.Id));
        Assert.Equal([2L, 8L], prime.Variables[0].Range!);
    }
}
