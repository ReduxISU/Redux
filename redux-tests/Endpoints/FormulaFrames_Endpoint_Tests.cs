using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using API.Problems.NPComplete.NPC_SAT;
using API.Problems.NPComplete.NPC_SAT3;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// SAT's and 3SAT's Formula picture (#925) on the actual endpoint output: /ProblemProvider/visualize?format=frames, visualizeRun
// and the Reduction View. The Formula is a second view of the clause graph (the Grouped Graph), so most checks compare it with
// that picture of the same run: the same ids, the same literal and clause states, the same assignments. The two visualizations
// are new, so the guard for old frontends is that their list format is exactly the default SAT and 3SAT pictures', that /info
// shows nothing of the frames plumbing, and that the problems' defaults are unchanged.
public class FormulaFrames_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public FormulaFrames_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private const string SatVis = "SatFormulaVisualization";
    private const string Sat3Vis = "Sat3FormulaVisualization";

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

    private static List<JsonElement> Frames(JsonElement response) => response.GetProperty("frames").EnumerateArray().ToList();

    private static Dictionary<string, string> States(JsonElement frame, string collection) =>
        frame.GetProperty(collection).EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetProperty("state").GetString()!);

    private static Dictionary<string, bool> Assignment(JsonElement frame) =>
        frame.GetProperty("assignment").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetBoolean());

    private static readonly HashSet<string> Vocabulary = new() {
        "Background", "ElementHighlight", "Solution", "Rejected", "Untraveled", "Covered", "Blocked", "False", "Committed",
    };

    // the Formula, the clause graph whose list format and frames it shares, the problem, every solver that records steps
    public static TheoryData<string, string, string, string> Cases => new() {
        { SatVis, "SatDefaultVisualization", "sat", "SATBruteForceSolver" },
        { Sat3Vis, "Sat3DefaultVisualization", "sat3", "Sat3BacktrackingSolver" },
    };

    private static string DefaultInstance(string problem) => ((IProblem)Activator.CreateInstance(ProblemProvider.Problems[problem])!).defaultInstance;

    // ---- What old clients read ----

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task List_IsExactlyTheDefaultPicturesList(string vis, string defaultVis, string problem, string solver) {
        string instance = DefaultInstance(problem);
        var (status, body) = await Post("visualize", $"visualization={vis}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var (_, expected) = await Post("visualize", $"visualization={defaultVis}", instance);
        Assert.Equal(expected, body);
        var (_, withSolver) = await Post("visualize", $"visualization={vis}&solver={solver}&format=list", instance);
        Assert.Equal(expected, withSolver);
    }

    [Theory]
    [InlineData(SatVis, "SAT Formula", "SATBruteForceSolver")]
    [InlineData(Sat3Vis, "3SAT Formula", "Sat3BacktrackingSolver")]
    public async Task Info_ReportsTheLegacyType_AndNoFramesPlumbing(string vis, string name, string solver) {
        var body = await (await _client.GetAsync($"/ProblemProvider/info?interface={vis}", TestContext.Current.CancellationToken))
            .Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var json = JsonDocument.Parse(body).RootElement;
        Assert.Equal(name, json.GetProperty("visualizationName").GetString());
        Assert.Equal("BooleanSatisfiability", json.GetProperty("visualizationType").GetString());
        Assert.Equal(["visualizationName", "visualizationDefinition", "source", "sourceFile", "contributors", "visualizationType", "visualizationTypes", "pictureType", "solver"],
            json.EnumerateObject().Select(p => p.Name));
        var own = (ISolver)Activator.CreateInstance(ProblemProvider.Solvers[solver.ToLower()])!;
        Assert.Equal(own.solverName, json.GetProperty("solver").GetProperty("solverName").GetString());
    }

    [Theory]
    [InlineData("SAT", "SatDefaultVisualization", SatVis)]
    [InlineData("SAT3", "Sat3DefaultVisualization", Sat3Vis)]
    public async Task TheProblemsDefault_IsUnchanged_AndTheFormulaIsListedBesideIt_OnlyForClientsThatOptIn(string problem, string defaultVis, string vis) {
        var info = JsonDocument.Parse(await _client.GetStringAsync($"/ProblemProvider/info?interface={problem}", TestContext.Current.CancellationToken)).RootElement;
        var own = (IVisualization)Activator.CreateInstance(ProblemProvider.Visualizers[defaultVis.ToLower()])!;
        Assert.Equal(own.visualizationName, info.GetProperty("defaultVisualization").GetProperty("visualizationName").GetString());
        async Task<List<string?>> Listed(string extra) => JsonDocument.Parse(await _client.GetStringAsync($"/Navigation/Problem_VisualizationsRefactor?chosenProblem={problem}{extra}", TestContext.Current.CancellationToken))
            .RootElement.EnumerateArray().Select(e => e.GetString()).ToList();
        // Redux_GUI's list (no opt-in) does not show the Formula: it has no renderer for it (#929).
        var listed = await Listed("");
        Assert.Contains(defaultVis, listed);
        Assert.DoesNotContain(vis, listed);
        var withPictures = await Listed("&includePictures=true");
        Assert.Contains(defaultVis, withPictures);
        Assert.Contains(vis, withPictures);
    }

    // ---- format=frames ----

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Frames_HaveTheFormulaPayload_AndTheClauseGraphsIdsAndStates_PlusTheVariables(string vis, string defaultVis, string problem, string solver) {
        string instance = DefaultInstance(problem);
        var root = await Json("visualize", $"visualization={vis}&format=frames&solver={solver}", instance);
        var graph = await Json("visualize", $"visualization={defaultVis}&format=frames&solver={solver}", instance);
        Assert.Equal("Formula", root.GetProperty("type").GetString());

        var payload = root.GetProperty("payload");
        Assert.Equal(["kind", "layout", "shape", "rule", "groups", "nodes", "variables"], payload.EnumerateObject().Select(p => p.Name));
        Assert.Equal(("formula", "cnf", "assignment", "satisfiability"),
            (payload.GetProperty("kind").GetString(), payload.GetProperty("layout").GetString(), payload.GetProperty("shape").GetString(), payload.GetProperty("rule").GetString()));
        Assert.DoesNotContain("\"state\"", payload.GetRawText());
        // The clause graph's groups, nodes and variables exactly, and none of its edges.
        var graphPayload = graph.GetProperty("payload");
        foreach (var key in new[] { "groups", "nodes", "variables" })
            Assert.Equal(graphPayload.GetProperty(key).GetRawText(), payload.GetProperty(key).GetRawText());

        var literalIds = payload.GetProperty("nodes").EnumerateArray().Select(n => n.GetProperty("id").GetString()!).ToHashSet();
        var clauseIds = payload.GetProperty("groups").EnumerateArray().Select(n => n.GetProperty("id").GetString()!).ToHashSet();
        var variableIds = payload.GetProperty("variables").EnumerateArray().Select(n => n.GetProperty("id").GetString()!).ToHashSet();

        var frames = Frames(root);
        var graphFrames = Frames(graph);
        Assert.True(frames.Count >= 2);
        Assert.Equal(graphFrames.Count, frames.Count);
        for (int i = 0; i < frames.Count; i++) {
            var f = frames[i];
            var g = graphFrames[i];
            bool isDone = f.GetProperty("event").GetString() == "Done";
            Assert.Equal(["event", "caption", "focus", "nodes", "groups", "assignment", "variables", "step", .. isDone ? new[] { "ok" } : []],
                f.EnumerateObject().Select(p => p.Name));
            // Everything the clause graph's frame says, the Formula's says the same way.
            foreach (var key in new[] { "event", "caption", "focus", "nodes", "groups", "assignment", "step" })
                Assert.Equal(g.GetProperty(key).GetRawText(), f.GetProperty(key).GetRawText());
            if (isDone) Assert.Equal(g.GetProperty("ok").GetBoolean(), f.GetProperty("ok").GetBoolean());
            foreach (var (id, state) in States(f, "nodes")) { Assert.Contains(id, literalIds); Assert.Contains(state, Vocabulary); }
            foreach (var (id, state) in States(f, "groups")) { Assert.Contains(id, clauseIds); Assert.Contains(state, Vocabulary); }
            // The strip: every assigned variable has a chip state, False exactly when it is false (or in the focus); the rest
            // are unassigned variables in the focus.
            var assignment = Assignment(f);
            var variables = States(f, "variables");
            var focus = f.GetProperty("focus").EnumerateArray().Select(e => e.GetString()!).ToHashSet();
            foreach (var (id, state) in variables) {
                Assert.Contains(id, variableIds);
                Assert.Contains(state, Vocabulary);
                Assert.True(assignment.ContainsKey(id) || (focus.Contains(id) && state == "ElementHighlight"), $"{id} is {state} but unassigned");
            }
            foreach (var (id, value) in assignment) {
                Assert.True(variables.ContainsKey(id), $"assigned {id} has no chip state");
                if (focus.Contains(id)) Assert.Equal("ElementHighlight", variables[id]);
                else if (!value) Assert.Equal("False", variables[id]);
                else Assert.NotEqual("False", variables[id]);
            }
        }
        // The confirmed answer: every true variable Solution, every false one False, every clause Covered.
        var last = frames[^1];
        Assert.True(last.GetProperty("ok").GetBoolean());
        Assert.All(Assignment(last), p => Assert.Equal(p.Value ? "Solution" : "False", States(last, "variables")[p.Key]));
        Assert.All(clauseIds, c => Assert.Equal("Covered", States(last, "groups")[c]));
        Assert.True(VerifyAnswer(problem, instance, last));
    }

    private static bool VerifyAnswer(string problem, string instance, JsonElement done) {
        string certificate = "(" + string.Join(",", Assignment(done).Select(p => $"{p.Key[2..]}:{p.Value}")) + ")";
        return problem == "sat"
            ? new API.Problems.NPComplete.NPC_SAT.Verifiers.SATVerifier().verify(new SAT(instance), certificate)
            : new API.Problems.NPComplete.NPC_SAT3.Verifiers.SAT3Verifier().verify(new SAT3(instance), certificate);
    }

    [Fact]
    public async Task Frames_Sat3Backtracking_KeepsDecidedVariablesCommitted_AndHighlightsTheOneBeingTried() {
        var frames = Frames(await Json("visualize", $"visualization={Sat3Vis}&format=frames", new SAT3().defaultInstance));
        var first = frames[0];
        Assert.Equal("Try", first.GetProperty("event").GetString());
        string tried = Assert.Single(first.GetProperty("focus").EnumerateArray()).GetString()!;
        Assert.Equal("ElementHighlight", States(first, "variables")[tried]);
        // Mid-run, before the answer: a true variable that is not being tried is kept so far, never green.
        var midRun = frames.Take(frames.Count - 1)
            .Where(f => f.GetProperty("event").GetString() != "Done")
            .SelectMany(f => Assignment(f).Where(p => p.Value && !f.GetProperty("focus").EnumerateArray().Any(e => e.GetString() == p.Key))
                .Select(p => States(f, "variables")[p.Key]))
            .ToList();
        Assert.NotEmpty(midRun);
        Assert.All(midRun, s => Assert.True(s is "Committed" or "Solution", s));
        Assert.Contains("Committed", midRun);
    }

    [Fact]
    public async Task Frames_SatBruteForce_ATriedCandidate_HighlightsItsTrueVariables_AndRejectsTheFailedClause() {
        // The brute force's first candidate is all false; it fails C3 = (x1 | x2 | x3).
        var first = Frames(await Json("visualize", $"visualization={SatVis}&format=frames", "(!x1 | x2) & (!x2 | x3) & (x1 | x2 | x3)"))[0];
        Assert.Equal("Reject", first.GetProperty("event").GetString());
        Assert.Equal(new Dictionary<string, string> { ["v:x1"] = "False", ["v:x2"] = "False", ["v:x3"] = "False" }, States(first, "variables"));
        Assert.Equal("Rejected", States(first, "groups")["c2"]);
        Assert.Equal("Rejected", States(first, "nodes")["c2-0"]);
    }

    [Theory]
    [InlineData(SatVis, "SATBruteForceSolver", "(x1 | x2) & (!x1) & (!x2)")]
    [InlineData(Sat3Vis, "Sat3BacktrackingSolver", "(x1 | x2) & (!x1 | x2) & (x1 | !x2) & (!x1 | !x2)")]
    [InlineData(Sat3Vis, "Sat3BacktrackingSolver", "(x1) & (!x1)")]
    public async Task Frames_WhenNoAssignmentExists_TheDoneIsNotOk_AndBlankApartFromItsCaption(string vis, string solver, string instance) {
        var last = Frames(await Json("visualize", $"visualization={vis}&format=frames&solver={solver}", instance))[^1];
        Assert.Equal("Done", last.GetProperty("event").GetString());
        Assert.False(last.GetProperty("ok").GetBoolean());
        Assert.NotEqual("", last.GetProperty("caption").GetString());
        foreach (var key in new[] { "nodes", "groups", "assignment", "variables" })
            Assert.Empty(last.GetProperty(key).EnumerateObject());
        Assert.DoesNotContain("Solution", Frames(await Json("visualize", $"visualization={vis}&format=frames&solver={solver}", instance))
            .SelectMany(f => States(f, "nodes").Values.Concat(States(f, "variables").Values)));
    }


    [Fact]
    public async Task Frames_ARepeatedLiteral_IsDrawnAsWritten_OnePillPerOccurrence() {
        var root = await Json("visualize", $"visualization={Sat3Vis}&format=frames", "(x1 | x1 | x2) & (!x1 | x2 | x2)");
        var nodes = root.GetProperty("payload").GetProperty("nodes").EnumerateArray().ToList();
        Assert.Equal(["c0-0", "c0-1", "c0-2", "c1-0", "c1-1", "c1-2"], nodes.Select(n => n.GetProperty("id").GetString()));
        Assert.Equal(["x1", "x1", "x2", "¬x1", "x2", "x2"], nodes.Select(n => n.GetProperty("label").GetString()));
    }

    // ---- One run, every picture ----

    // 3SAT only: the SAT brute force writes its own ISolver.Run and solves no one problem by its type, so visualizeRun refuses it
    // (solver_mismatch) for every SAT picture alike.
    [Theory]
    [InlineData(Sat3Vis, "Sat3DefaultVisualization", "sat3", "Sat3BacktrackingSolver")]
    public async Task VisualizeRun_DrawsTheFormula_FromTheSameRunAsTheClauseGraph(string vis, string defaultVis, string problem, string solver) {
        string instance = DefaultInstance(problem);
        var body = await Json("visualizeRun", $"solver={solver}", instance);
        var pictures = body.GetProperty("pictures").EnumerateArray().ToDictionary(p => p.GetProperty("visualization").GetString()!);
        Assert.Equal("Formula", pictures[vis].GetProperty("type").GetString());
        Assert.Equal("GroupedGraph", pictures[defaultVis].GetProperty("type").GetString());
        // The default comes first; the Formula is an extra one.
        Assert.Equal(defaultVis, body.GetProperty("pictures")[0].GetProperty("visualization").GetString());
        var formulaFrames = Frames(pictures[vis]);
        var graphFrames = Frames(pictures[defaultVis]);
        Assert.Equal(graphFrames.Select(f => f.GetProperty("step").GetRawText()), formulaFrames.Select(f => f.GetProperty("step").GetRawText()));
        for (int i = 0; i < formulaFrames.Count; i++)
            foreach (var key in new[] { "event", "nodes", "groups", "assignment" })
                Assert.Equal(graphFrames[i].GetProperty(key).GetRawText(), formulaFrames[i].GetProperty(key).GetRawText());
        Assert.True(body.GetProperty("ok").GetBoolean());
    }

    // ---- The Reduction View ----

    private const string SatToClique = "SipserReduceToCliqueStandard";

    [Fact]
    public async Task ReductionView_TheFormulaCanDrawTheSourceSide_AndAgreesWithTheClauseGraphOnEveryFrame() {
        string instance = new SAT3().defaultInstance;
        var formula = await Json("reductionView", $"reduction={SatToClique}&sourceVisualization={Sat3Vis}", instance);
        var graph = await Json("reductionView", $"reduction={SatToClique}", instance);
        Assert.Equal(Sat3Vis, formula.GetProperty("source").GetProperty("visualization").GetString());
        Assert.Equal("Formula", formula.GetProperty("source").GetProperty("picture").GetProperty("type").GetString());
        Assert.Equal("formula", formula.GetProperty("source").GetProperty("picture").GetProperty("payload").GetProperty("kind").GetString());
        // Asking for the Formula changes the source pane only: the target, the gadgets and every frame's envelope are as before.
        Assert.Equal(graph.GetProperty("target").GetRawText(), formula.GetProperty("target").GetRawText());
        Assert.Equal(graph.GetProperty("gadgets").GetRawText(), formula.GetProperty("gadgets").GetRawText());
        var f = Frames(formula);
        var g = Frames(graph);
        Assert.Equal(g.Count, f.Count);
        for (int i = 0; i < f.Count; i++) {
            foreach (var key in new[] { "phase", "event", "caption", "step", "sourceFocus", "targetFocus", "target" })
                Assert.Equal(g[i].GetProperty(key).GetRawText(), f[i].GetProperty(key).GetRawText());
            Assert.Equal(g[i].TryGetProperty("ok", out var gOk) ? gOk.GetBoolean() : (bool?)null, f[i].TryGetProperty("ok", out var fOk) ? fOk.GetBoolean() : null);
            // The source pane is the clause graph's, plus the variables' states.
            var pane = f[i].GetProperty("source");
            Assert.Equal(["nodes", "groups", "assignment", "variables"], pane.EnumerateObject().Select(p => p.Name));
            foreach (var key in new[] { "nodes", "groups", "assignment" })
                Assert.Equal(g[i].GetProperty("source").GetProperty(key).GetRawText(), pane.GetProperty(key).GetRawText());
        }
        // The map back: what is mapped so far stays Committed, and the last frame is a Done with the confirmed answer.
        var map = f.Where(x => x.GetProperty("phase").GetString() == "map").ToList();
        Assert.NotEmpty(map);
        var done = map[^1];
        Assert.Equal("Done", done.GetProperty("event").GetString());
        Assert.True(done.GetProperty("ok").GetBoolean());
        var pane0 = done.GetProperty("source");
        Assert.All(Assignment(pane0), p => Assert.Equal(p.Value ? "Solution" : "False", States(pane0, "variables")[p.Key]));
        // (The Accept just before that Done ends the run, so it is the confirmed answer too.)
        Assert.True(map.Count > 2);
        foreach (var step in map.Take(map.Count - 2)) {
            var source = step.GetProperty("source");
            var focus = step.GetProperty("sourceFocus").EnumerateArray().Select(e => e.GetString()!).ToHashSet();
            foreach (var (id, value) in Assignment(source))
                if (value && !focus.Contains(id)) Assert.Equal("Committed", States(source, "variables")[id]);
        }
    }

    [Theory]
    [InlineData("source", SatVis)]
    [InlineData("target", Sat3Vis)]
    public async Task ReductionView_AFormulaOfAnotherProblem_IsAVisualizationMismatch(string side, string vis) {
        var (status, body) = await Post("reductionView", $"reduction={SatToClique}&{side}Visualization={vis}", new SAT3().defaultInstance);
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("visualization_mismatch", JsonDocument.Parse(body).RootElement.GetProperty("error").GetString());
    }
}
