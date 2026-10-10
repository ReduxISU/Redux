using System.Net;
using System.Text;
using System.Text.Json;
using API.Problems.NPComplete.NPC_SAT;
using API.Problems.NPComplete.NPC_SAT3;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Grouped Graph picture (the clause graph) on the actual /ProblemProvider/visualize?format=frames output, for SAT and
// 3SAT, plus the guards that keep everything old frontends read exactly as it was:
//   - the list format (snapshots in Snapshots/, captured before the picture existed),
//   - /info for every visualization, solver and reduction that was touched (info.<name>.json, captured the same way).
// The one deliberate exception is SAT's default visualization, which moved off DummyVisualization: see the "rewire" tests.
public class GroupedGraphFrames_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public GroupedGraphFrames_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private static string ReadSnapshot(string file) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Endpoints", "Snapshots", file)).Replace("\r\n", "\n");

    private async Task<(HttpStatusCode Status, string Body)> Visualize(string query, string instance) {
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/visualize?{query}", content, TestContext.Current.CancellationToken);
        return (response.StatusCode, (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Replace("\r\n", "\n"));
    }

    private static readonly HashSet<string> Vocabulary = new() {
        "Background", "ElementHighlight", "Solution", "Rejected", "Untraveled", "Covered", "Blocked", "False",
    };

    private const string SatVis = "satdefaultvisualization";
    private const string Sat3Vis = "sat3defaultvisualization";

    // visualization, its default solver
    private static readonly (string Vis, string Solver)[] CaseRows = {
        (SatVis, "satbruteforcesolver"),
        (Sat3Vis, "sat3backtrackingsolver"),
    };

    public static TheoryData<string> Visualizations => new(CaseRows.Select(c => c.Vis));
    public static TheoryData<string, string> Cases => new(CaseRows.Select(c => (c.Vis, c.Solver)));

    // Every visualization and solver whose /info must not change.
    public static TheoryData<string> TouchedInterfaces => new() {
        "sat3", SatVis, Sat3Vis, "satgrovervisualization", "satbruteforcesolver", "satgroversolver",
        "sat3backtrackingsolver", "ppz", "schoning", "walksat",
    };

    private static string DefaultInstance(string vis) => vis == SatVis ? new SAT().defaultInstance : new SAT3().defaultInstance;

    private static bool Verify(string vis, string instance, string certificate) =>
        vis == SatVis
            ? new API.Problems.NPComplete.NPC_SAT.Verifiers.SATVerifier().verify(new SAT(instance), certificate)
            : new API.Problems.NPComplete.NPC_SAT3.Verifiers.SAT3Verifier().verify(new SAT3(instance), certificate);

    // The formula's clauses, read here from the instance text (not by the code under test): ["!x1", "x2", "x3"] per clause.
    private static List<string[]> ClausesOf(string instance) =>
        instance.Split('&').Select(c => c.Replace("(", "").Replace(")", "").Split('|').Select(l => l.Trim()).ToArray()).ToList();

    [Theory]
    [MemberData(nameof(TouchedInterfaces))]
    public async Task Info_IsUnchangedByTheGroupedGraph(string name) {
        var response = await _client.GetAsync($"/ProblemProvider/info?interface={name}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ReadSnapshot($"info.{name}.json"), body.Replace("\r\n", "\n"));
    }

    [Theory]
    [MemberData(nameof(Visualizations))]
    public async Task Info_StillReportsTheLegacyType_NotGroupedGraph(string vis) {
        var response = await _client.GetAsync($"/ProblemProvider/info?interface={vis}", TestContext.Current.CancellationToken);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement;
        Assert.Equal("BooleanSatisfiability", json.GetProperty("visualizationType").GetString());
    }

    // ---- SAT's default visualization moved off DummyVisualization: the only thing that may change. ----

    // What embeds SAT's defaultVisualization: SAT itself, the reduction into SAT and the reduction out of SAT (its reductionFrom).
    // Their snapshots were retaken after the rewire; the diff against the pre-change capture is only that nested object.
    public static TheoryData<string> EmbedsSatsVisualization => new() { "sat", "karpreducesat", "karpsattosat3" };

    [Theory]
    [MemberData(nameof(EmbedsSatsVisualization))]
    public async Task Rewire_Info_ShowsSatsRealVisualization_AndNothingElse(string name) {
        var response = await _client.GetAsync($"/ProblemProvider/info?interface={name}", TestContext.Current.CancellationToken);
        var body = (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Replace("\r\n", "\n");
        Assert.Equal(ReadSnapshot($"info.{name}.json"), body);
        Assert.Contains("\"visualizationName\":\"SAT Visualization\"", body);
        Assert.DoesNotContain("DummyVisualization", body);
    }

    [Fact]
    public async Task Rewire_SatInfo_ReportsTheSatVisualizationAndItsSolver() {
        var response = await _client.GetAsync("/ProblemProvider/info?interface=sat", TestContext.Current.CancellationToken);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement;
        var vis = json.GetProperty("defaultVisualization");
        Assert.Equal("SAT Visualization", vis.GetProperty("visualizationName").GetString());
        Assert.Equal("BooleanSatisfiability", vis.GetProperty("visualizationType").GetString());
        Assert.Equal("SAT Brute Force", vis.GetProperty("solver").GetProperty("solverName").GetString());
        // The same object the visualization reports about itself.
        var own = JsonDocument.Parse(ReadSnapshot($"info.{SatVis}.json")).RootElement;
        Assert.Equal(own.GetProperty("visualizationName").GetString(), vis.GetProperty("visualizationName").GetString());
        Assert.Equal(own.GetProperty("sourceFile").GetString(), vis.GetProperty("sourceFile").GetString());
    }

    [Theory]
    [InlineData("karpreducesat")]
    [InlineData("karpsattosat3")]
    public async Task Rewire_Reduce_ChangesOnlyTheNestedVisualization(string reduction) {
        string instance = ReadSnapshot($"{reduction}.instance.txt");
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/reduce?reduction={reduction}", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ReadSnapshot($"{reduction}.reduce.json"), (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Replace("\r\n", "\n"));
    }

    [Fact]
    public async Task Rewire_VisualizeReduction_IntoSat_NowReturnsTheSatPicture() {
        // Before the rewire the reduction's target visualization was DummyVisualization, which cannot parse a problem, so
        // the endpoint failed. Now it answers with SAT's list items.
        string instance = ReadSnapshot("karpreducesat.instance.txt");
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/visualizeReduction?reduction=karpreducesat&solution=" + Uri.EscapeDataString("{{c,e,g,i},{b,d,f,h},{a}}"), content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Replace("\r\n", "\n");
        Assert.Equal(ReadSnapshot("karpreducesat.visualizereduction.json"), body);
        var items = JsonDocument.Parse(body).RootElement.EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.All(items, i => Assert.Equal("sat", i.GetProperty("kind").GetString()));
    }

    [Fact]
    public async Task Rewire_VisualizeReduction_OutOfSat_IsUnchanged() {
        string instance = ReadSnapshot("karpsattosat3.instance.txt");
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/visualizeReduction?reduction=karpsattosat3&solution=" + Uri.EscapeDataString("(x1:True,x2:True,x3:False,x4:False)"), content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ReadSnapshot("karpsattosat3.visualizereduction.json"), (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Replace("\r\n", "\n"));
    }

    [Fact]
    public async Task Rewire_SatsVisualization_AnUnsatisfiableFormulaNoLongerThrows() {
        // SolvedVisualization used to index past the end of "No solution exists".split(":"); the list items now come back
        // with no literal highlighted.
        var (status, body) = await Visualize($"visualization={SatVis}", "(x1) & (!x1)");
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.DoesNotContain("Solution", body);
        Assert.Equal(2, JsonDocument.Parse(body).RootElement.GetArrayLength());
    }

    // ---- The list format is what old frontends read: it must not change. ----

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task List_DefaultResponse_MatchesPreChangeSnapshot(string vis, string solver) {
        var instance = ReadSnapshot($"{vis}.instance.txt");
        Assert.Equal(DefaultInstance(vis), instance);
        var (status, body) = await Visualize($"visualization={vis}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(ReadSnapshot($"{vis}.json"), body);
        var (_, withSolver) = await Visualize($"visualization={vis}&solver={solver}&format=list", instance);
        Assert.Equal(body, withSolver);
        Assert.Equal(2, JsonDocument.Parse(body).RootElement.GetArrayLength());
    }

    // ---- frames ----

    private static List<JsonElement> Frames(string body) => JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().ToList();

    private static Dictionary<string, string> States(JsonElement frame, string collection) =>
        frame.GetProperty(collection).EnumerateObject().ToDictionary(e => e.Name, e => e.Value.GetProperty("state").GetString()!);

    private static Dictionary<string, bool> Assignment(JsonElement frame) =>
        frame.GetProperty("assignment").EnumerateObject().ToDictionary(e => e.Name, e => e.Value.GetBoolean());

    // The certificate the frame's assignment makes, written here (not by the code under test): "(x1:True,x2:False)".
    private static string Certificate(JsonElement frame) =>
        "(" + string.Join(",", Assignment(frame).Select(p => $"{p.Key["v:".Length..]}:{(p.Value ? "True" : "False")}")) + ")";

    // True when the assignment gives every clause a true literal, judged here from the instance text.
    private static bool Satisfies(string instance, Dictionary<string, bool> assignment) =>
        ClausesOf(instance).All(clause => clause.Any(l => {
            bool negated = l.StartsWith('!');
            return assignment.TryGetValue("v:" + l.TrimStart('!'), out bool v) && v != negated;
        }));

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Frames_DefaultSolver_HasGroupedGraphTypePayloadAndFrames(string vis, string solver) {
        string instance = DefaultInstance(vis);
        var (status, body) = await Visualize($"visualization={vis}&format=frames&solver={solver}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var root = JsonDocument.Parse(body).RootElement;

        Assert.Equal("GroupedGraph", root.GetProperty("type").GetString());
        var payload = root.GetProperty("payload");
        Assert.Equal("groupedGraph", payload.GetProperty("kind").GetString());
        Assert.Equal("ring", payload.GetProperty("layout").GetString());
        Assert.Equal("assignment", payload.GetProperty("shape").GetString());
        Assert.Equal("satisfiability", payload.GetProperty("rule").GetString());

        // The ids follow the contract: clause i is c{i}, its j-th literal c{i}-{j}, variable v is v:{name}.
        var clauses = ClausesOf(instance);
        var groups = payload.GetProperty("groups").EnumerateArray().ToList();
        Assert.Equal(clauses.Select((_, i) => $"c{i}"), groups.Select(g => g.GetProperty("id").GetString()));
        Assert.Equal(clauses.Select((_, i) => $"C{i + 1}"), groups.Select(g => g.GetProperty("label").GetString()));
        var nodes = payload.GetProperty("nodes").EnumerateArray().ToList();
        var expectedNodes = clauses.SelectMany((c, i) => c.Select((l, j) => (Id: $"c{i}-{j}", Literal: l, Group: $"c{i}"))).ToList();
        Assert.Equal(expectedNodes.Select(n => n.Id), nodes.Select(n => n.GetProperty("id").GetString()));
        for (int k = 0; k < nodes.Count; k++) {
            var (_, literal, group) = expectedNodes[k];
            bool positive = !literal.StartsWith('!');
            Assert.Equal(group, nodes[k].GetProperty("group").GetString());
            Assert.Equal("v:" + literal.TrimStart('!'), nodes[k].GetProperty("variable").GetString());
            Assert.Equal(positive, nodes[k].GetProperty("positive").GetBoolean());
            Assert.Equal((positive ? "" : "¬") + literal.TrimStart('!'), nodes[k].GetProperty("label").GetString());
        }
        for (int i = 0; i < clauses.Count; i++)
            Assert.Equal(clauses[i].Select((_, j) => $"c{i}-{j}"), groups[i].GetProperty("members").EnumerateArray().Select(m => m.GetString()));
        var variables = clauses.SelectMany(c => c).Select(l => l.TrimStart('!')).Distinct().ToList();
        Assert.Equal(variables.Order().Select(v => "v:" + v), payload.GetProperty("variables").EnumerateArray().Select(v => v.GetProperty("id").GetString()));

        // Complement edges, worked out here from the instance text: a literal and its negation in different clauses.
        var expectedComplement = new List<string>();
        var expectedWithin = new List<string>();
        for (int a = 0; a < expectedNodes.Count; a++)
            for (int b = a + 1; b < expectedNodes.Count; b++) {
                var x = expectedNodes[a];
                var y = expectedNodes[b];
                if (x.Group == y.Group) expectedWithin.Add($"{x.Id}~{y.Id}");
                else if (x.Literal.TrimStart('!') == y.Literal.TrimStart('!') && x.Literal.StartsWith('!') != y.Literal.StartsWith('!')) expectedComplement.Add($"{x.Id}~{y.Id}");
            }
        Assert.NotEmpty(expectedComplement);
        Assert.Equal(expectedComplement, payload.GetProperty("edges").EnumerateArray().Select(e => e.GetProperty("id").GetString()));
        Assert.Equal(expectedWithin, payload.GetProperty("clauseEdges").EnumerateArray().Select(e => e.GetProperty("id").GetString()));
        foreach (var e in payload.GetProperty("edges").EnumerateArray().Concat(payload.GetProperty("clauseEdges").EnumerateArray()))
            Assert.Equal(e.GetProperty("id").GetString(), $"{e.GetProperty("a").GetString()}~{e.GetProperty("b").GetString()}");
        // The payload is static and stateless: no state names anywhere in it.
        Assert.DoesNotContain("\"state\"", payload.GetRawText());

        var nodeIds = nodes.Select(n => n.GetProperty("id").GetString()!).ToHashSet();
        var groupIds = groups.Select(g => g.GetProperty("id").GetString()!).ToHashSet();
        var variableIds = payload.GetProperty("variables").EnumerateArray().Select(v => v.GetProperty("id").GetString()!).ToHashSet();
        var frames = Frames(body);
        Assert.True(frames.Count >= 2, "the default solver records steps");
        foreach (var f in frames) {
            Assert.Contains(f.GetProperty("event").GetString(), new[] { "Try", "Accept", "Reject", "Backtrack", "Done" });
            Assert.False(string.IsNullOrWhiteSpace(f.GetProperty("caption").GetString()));
            foreach (var id in f.GetProperty("focus").EnumerateArray())
                Assert.True(variableIds.Contains(id.GetString()!) || groupIds.Contains(id.GetString()!), id.GetString());
            foreach (var (collection, ids) in new[] { ("nodes", nodeIds), ("groups", groupIds) })
                foreach (var entry in f.GetProperty(collection).EnumerateObject()) {
                    Assert.Contains(entry.Name, ids);
                    string state = entry.Value.GetProperty("state").GetString()!;
                    Assert.Contains(state, Vocabulary);
                    Assert.NotEqual("Background", state);   // Background is omitted
                }
            Assert.All(Assignment(f).Keys, k => Assert.Contains(k, variableIds));
            AssertLiteralStatesFollowTheAssignment(f, clauses, typeof(API.Interfaces.IIncrementalSolver).IsAssignableFrom(ProblemProvider.Solvers[solver]));
            bool isDone = f.GetProperty("event").GetString() == "Done";
            Assert.Equal(isDone, f.TryGetProperty("ok", out _));
            // A clause is Rejected exactly when the assignment makes every one of its literals false, and Covered when one is true.
            var assigned = Assignment(f);
            for (int i = 0; i < clauses.Count; i++) {
                var values = clauses[i].Select(l => assigned.TryGetValue("v:" + l.TrimStart('!'), out bool v) ? (bool?)(v != l.StartsWith('!')) : null).ToList();
                var state = States(f, "groups").GetValueOrDefault($"c{i}");
                if (values.Any(v => v == true)) Assert.True(state is "Covered" or "ElementHighlight", $"clause {i}: {state}");
                if (values.All(v => v == false)) Assert.Equal("Rejected", state);
                if (state == "Rejected" && !f.GetProperty("focus").EnumerateArray().Any(x => x.GetString() == $"c{i}")) Assert.All(values, v => Assert.Equal(false, v));
            }
        }

        // Exactly one Done, and it is last; its ok agrees with the problem's verifier on the assignment it shows.
        Assert.Single(frames, f => f.GetProperty("event").GetString() == "Done");
        var last = frames[^1];
        Assert.Equal("Done", last.GetProperty("event").GetString());
        Assert.True(last.GetProperty("ok").GetBoolean());
        Assert.True(Verify(vis, instance, Certificate(last)), Certificate(last));
        Assert.True(Satisfies(instance, Assignment(last)));
        // A solved formula: every clause is Covered, none Rejected, and every cluster holds at least one Solution literal.
        Assert.Equal(clauses.Count, States(last, "groups").Count);
        Assert.All(States(last, "groups").Values, s => Assert.Equal("Covered", s));
        var solved = States(last, "nodes");
        Assert.DoesNotContain("Rejected", solved.Values);
        Assert.All(solved.Values, s => Assert.True(s is "Solution" or "False", s));
        for (int i = 0; i < clauses.Count; i++)
            Assert.Contains(solved, n => n.Key.StartsWith($"c{i}-") && n.Value == "Solution");
    }

    // Every literal's state, judged here from the instance text and the frame's own assignment: a true literal is Solution
    // only in a step that keeps the assignment (Accept, Backtrack, Done, and every step of an incremental solver, whose
    // assignment is the variables decided so far) and ElementHighlight in one that only tries a whole assignment (Try,
    // Reject), or while its variable is the one being tried; a false literal is False (or Rejected with its clause), even
    // while its variable is tried; a literal of an unassigned variable is Background unless it is being looked at.
    private static void AssertLiteralStatesFollowTheAssignment(JsonElement frame, List<string[]> clauses, bool incremental) {
        string ev = frame.GetProperty("event").GetString()!;
        bool onlyTried = !incremental && ev is "Try" or "Reject";
        var assigned = Assignment(frame);
        var nodes = States(frame, "nodes");
        for (int i = 0; i < clauses.Count; i++)
            for (int j = 0; j < clauses[i].Length; j++) {
                string literal = clauses[i][j];
                string state = nodes.GetValueOrDefault($"c{i}-{j}") ?? "Background";
                string where = $"c{i}-{j} ({literal}) in a {ev} step";
                if (!assigned.TryGetValue("v:" + literal.TrimStart('!'), out bool value))
                    Assert.True(state is "Background" or "ElementHighlight", $"{where}: {state}");
                else if (value != literal.StartsWith('!'))
                    Assert.True(onlyTried ? state is "ElementHighlight" : state is "Solution" or "ElementHighlight", $"{where}: {state}");
                else
                    Assert.True(state is "False" or "Rejected", $"{where}: {state}");
            }
        if (onlyTried) Assert.DoesNotContain("Solution", nodes.Values);
    }

    [Fact]
    public async Task Frames_Sat3_FirstFrameTriesTheFirstVariable_AndShowsItOnEveryOccurrence() {
        var (_, body) = await Visualize($"visualization={Sat3Vis}&format=frames", DefaultInstance(Sat3Vis));
        var frames = Frames(body);
        var first = frames[0];
        Assert.Equal("Try", first.GetProperty("event").GetString());
        Assert.Equal("Try x1 = True.", first.GetProperty("caption").GetString());
        Assert.Equal(["v:x1"], first.GetProperty("focus").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(new Dictionary<string, bool> { ["v:x1"] = true }, Assignment(first));
        // (x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | !x1): x1 occurs at c0-0, c1-0, c1-2 and c2-2.
        Assert.Equal(["c0-0", "c1-0", "c1-2", "c2-2"], States(first, "nodes").Keys.Order());
        // The true occurrences (x1) are highlighted; the false ones (!x1 at c1-0 and c2-2) stay False.
        Assert.Equal(new Dictionary<string, string> { ["c0-0"] = "ElementHighlight", ["c1-0"] = "False", ["c1-2"] = "ElementHighlight", ["c2-2"] = "False" },
            States(first, "nodes"));
        // Clauses c0 and c1 contain a true x1 already.
        Assert.Equal(new Dictionary<string, string> { ["c0"] = "Covered", ["c1"] = "Covered" }, States(first, "groups"));
        var last = frames[^1];
        Assert.Equal("Every clause has a true literal under x1 = True, x2 = True, x3 = False.", last.GetProperty("caption").GetString());
        Assert.Equal(new Dictionary<string, bool> { ["v:x1"] = true, ["v:x2"] = true, ["v:x3"] = false }, Assignment(last));
    }

    [Fact]
    public async Task Frames_Sat_FirstFrameIsTheFirstCandidate_AndTheFailedClauseIsRejected() {
        // The default instance is satisfied by the first candidate, all-false (#754), so use one whose third clause fails it.
        var (_, body) = await Visualize($"visualization={SatVis}&format=frames", "(!x1 | x2) & (!x2 | x3) & (x1 | x2 | x3)");
        var first = Frames(body)[0];
        Assert.Equal("Reject", first.GetProperty("event").GetString());
        Assert.Equal("Try x1 = False, x2 = False, x3 = False: clause C3 (x1 | x2 | x3) has no true literal.", first.GetProperty("caption").GetString());
        Assert.Equal(["c2"], first.GetProperty("focus").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal("Rejected", States(first, "groups")["c2"]);
        // Every literal of the failed clause is Rejected; the others hold true literals.
        Assert.Equal(["c2-0", "c2-1", "c2-2"], States(first, "nodes").Where(n => n.Value == "Rejected").Select(n => n.Key).Order());
        Assert.Equal(["c0", "c1"], States(first, "groups").Where(g => g.Value == "Covered").Select(g => g.Key).Order());
    }

    [Fact]
    public async Task Frames_Sat_DefaultInstance_FirstCandidateIsAllFalse_AndIsAccepted() {
        // #754: the brute force counter starts at all-false, which satisfies the default instance.
        var (_, body) = await Visualize($"visualization={SatVis}&format=frames", DefaultInstance(SatVis));
        var first = Frames(body)[0];
        Assert.Equal("Accept", first.GetProperty("event").GetString());
        Assert.Equal("Try x3 = False, x4 = False, x2 = False, x1 = False: every clause has a true literal.", first.GetProperty("caption").GetString());
        Assert.All(Assignment(first).Values, v => Assert.False(v));
    }

    [Theory]
    [InlineData(Sat3Vis, "walksat")]
    [InlineData(Sat3Vis, "schoning")]
    [InlineData(Sat3Vis, "ppz")]
    public async Task Frames_ASolverWithoutSteps_GivesOneDoneFrameBuiltFromItsAnswer(string vis, string solver) {
        // These solvers are randomized and record no steps: the frames are the solved picture alone, and ok is the verifier's word.
        string instance = DefaultInstance(vis);
        var (status, body) = await Visualize($"visualization={vis}&format=frames&solver={solver}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var frame = Assert.Single(Frames(body));
        Assert.Equal("Done", frame.GetProperty("event").GetString());
        bool ok = frame.GetProperty("ok").GetBoolean();
        Assert.Equal(Assignment(frame).Count > 0 && Verify(vis, instance, Certificate(frame)), ok);
        if (ok) Assert.True(Satisfies(instance, Assignment(frame)));
    }

    [Theory]
    [InlineData(SatVis, "satbruteforcesolver", "(x1 | x2) & (!x1) & (!x2)")]
    [InlineData(Sat3Vis, "sat3backtrackingsolver", "(x1 | x2) & (!x1 | x2) & (x1 | !x2) & (!x1 | !x2)")]
    [InlineData(Sat3Vis, "sat3backtrackingsolver", "(x1) & (!x1)")]
    public async Task Frames_WhenNoAssignmentExists_EndsNotOkWithNothingAssigned(string vis, string solver, string instance) {
        var (status, body) = await Visualize($"visualization={vis}&format=frames&solver={solver}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var frames = Frames(body);
        Assert.True(frames.Count >= 2);
        var last = frames[^1];
        Assert.Equal("Done", last.GetProperty("event").GetString());
        Assert.False(last.GetProperty("ok").GetBoolean());
        Assert.Empty(Assignment(last));
        Assert.Empty(last.GetProperty("nodes").EnumerateObject());
        Assert.Empty(last.GetProperty("groups").EnumerateObject());
        // Somewhere on the way a clause was rejected (a clause with every literal false).
        Assert.Contains(frames, f => States(f, "groups").ContainsValue("Rejected"));
    }

    [Fact]
    public async Task Frames_StepsAreCappedAndSayHowManyAreHidden() {
        // Ten variables, unsatisfiable: 1024 candidates, far more than the cap of 150.
        var (_, body) = await Visualize($"visualization={SatVis}&format=frames", "(x1) & (!x1) & (x2 | x3 | x4 | x5 | x6 | x7 | x8 | x9 | x10)");
        var frames = Frames(body);
        Assert.Equal(151, frames.Count);
        Assert.Contains("not shown", frames[^1].GetProperty("caption").GetString());
        Assert.Equal("Done", frames[^1].GetProperty("event").GetString());
    }

    [Theory]
    [InlineData(SatVis, "sat3backtrackingsolver")]
    [InlineData(Sat3Vis, "cliquebruteforce")]
    public async Task Frames_ASolverForAnotherProblem_Returns400SolverMismatch(string vis, string solver) {
        var (status, body) = await Visualize($"visualization={vis}&format=frames&solver={solver}", DefaultInstance(vis));
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("solver_mismatch", JsonDocument.Parse(body).RootElement.GetProperty("error").GetString());
    }

    [Theory]
    [InlineData(SatVis)]
    [InlineData(Sat3Vis)]
    public async Task Frames_InvalidInstance_Returns400WithParseError(string vis) {
        var (status, body) = await Visualize($"visualization={vis}&format=frames", "(1 | 2 | 3)");
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("instance_parse_error", body);
    }

    [Theory]
    [InlineData(SatVis, "frames")]
    [InlineData(SatVis, "list")]
    [InlineData(Sat3Vis, "frames")]
    [InlineData(Sat3Vis, "list")]
    public async Task Visualize_AFormulaWithAnEmptyClause_Returns400_Not500(string vis, string format) {
        // "& &" leaves an empty clause. The SAT brute force solves before the visualization parses the formula, and its own
        // parser crashed on the empty clause (500); it now checks the formula first, as the 3SAT solver always has.
        var (status, body) = await Visualize($"visualization={vis}&format={format}", "(x1 | x2) & & (x3)");
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("instance_parse_error", body);
    }

    [Fact]
    public async Task Frames_AFormulaWithRepeatedAndOpposedLiterals_KeepsOneNodePerOccurrence() {
        // (x1 | !x1) is a tautology: no complement edge inside it; (x1 | x1) is two nodes.
        var (_, body) = await Visualize($"visualization={Sat3Vis}&format=frames", "(x1 | x1) & (x1 | !x1)");
        var payload = JsonDocument.Parse(body).RootElement.GetProperty("payload");
        Assert.Equal(["c0-0", "c0-1", "c1-0", "c1-1"], payload.GetProperty("nodes").EnumerateArray().Select(n => n.GetProperty("id").GetString()));
        Assert.Equal(["c0-0~c1-1", "c0-1~c1-1"], payload.GetProperty("edges").EnumerateArray().Select(e => e.GetProperty("id").GetString()));
        Assert.Equal(["c0-0~c0-1", "c1-0~c1-1"], payload.GetProperty("clauseEdges").EnumerateArray().Select(e => e.GetProperty("id").GetString()));
        Assert.True(Frames(body).Last().GetProperty("ok").GetBoolean());
    }

    [Theory]
    [InlineData(SatVis)]
    [InlineData(Sat3Vis)]
    public async Task Frames_NamesWithTheSeparators_GiveUniqueEscapedIds_AndEdgesNameTheirEnds(string vis) {
        // Names SAT and 3SAT accept that hold this picture's separators ('-', '~', '\\'). (':' is left out here only
        // because the SAT verifiers cannot read a certificate whose names hold ':'.)
        string instance = @"(a-b | !c~d | e\f) & (!a-b | c~d) & (!e\f | a)";
        var (status, body) = await Visualize($"visualization={vis}&format=frames", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var payload = JsonDocument.Parse(body).RootElement.GetProperty("payload");
        var variables = payload.GetProperty("variables").EnumerateArray()
            .ToDictionary(v => v.GetProperty("id").GetString()!, v => v.GetProperty("label").GetString()!);
        Assert.Equal(new Dictionary<string, string> { ["v:a"] = "a", [@"v:a\-b"] = "a-b", [@"v:c\~d"] = "c~d", [@"v:e\\f"] = @"e\f" }, variables);
        var nodes = payload.GetProperty("nodes").EnumerateArray().ToList();
        Assert.All(nodes, n => Assert.Contains(n.GetProperty("variable").GetString()!, variables.Keys));
        var nodeIds = nodes.Select(n => n.GetProperty("id").GetString()!).ToHashSet();
        var edges = payload.GetProperty("edges").EnumerateArray().Concat(payload.GetProperty("clauseEdges").EnumerateArray()).ToList();
        Assert.All(edges, e => {
            Assert.Contains(e.GetProperty("a").GetString()!, nodeIds);
            Assert.Contains(e.GetProperty("b").GetString()!, nodeIds);
        });
        Assert.Equal(["c0-0~c1-0", "c0-1~c1-1", "c0-2~c2-0"], payload.GetProperty("edges").EnumerateArray().Select(e => e.GetProperty("id").GetString()));
        var all = variables.Keys.Concat(nodeIds).Concat(edges.Select(e => e.GetProperty("id").GetString()!)).ToList();
        Assert.Equal(all.Count, all.Distinct().Count());

        // The frames key the assignment by the same escaped ids, and the answer is right.
        var frames = Frames(body);
        Assert.All(frames, f => Assert.All(Assignment(f).Keys, k => Assert.Contains(k, variables.Keys)));
        var last = frames[^1];
        Assert.True(last.GetProperty("ok").GetBoolean());
        string certificate = "(" + string.Join(",", Assignment(last).Select(p => $"{variables[p.Key]}:{(p.Value ? "True" : "False")}")) + ")";
        Assert.True(Verify(vis, instance, certificate), certificate);
    }

    [Fact]
    public async Task Frames_Sat_ARejectFrame_DrawsTrueLiteralsHighlighted_FalseOnesFalse_AndLeavesNothingAsSolution() {
        // The first candidate, all false, fails C3 = (x1 | x2 | x3): !x1 and !x2 are true, x2 in C1 and x3 in C2 are false.
        var (_, body) = await Visualize($"visualization={SatVis}&format=frames", "(!x1 | x2) & (!x2 | x3) & (x1 | x2 | x3)");
        var first = Frames(body)[0];
        Assert.Equal("Reject", first.GetProperty("event").GetString());
        Assert.Equal(new Dictionary<string, string> {
            ["c0-0"] = "ElementHighlight", ["c0-1"] = "False",
            ["c1-0"] = "ElementHighlight", ["c1-1"] = "False",
            ["c2-0"] = "Rejected", ["c2-1"] = "Rejected", ["c2-2"] = "Rejected",
        }, States(first, "nodes"));
    }

    [Fact]
    public async Task Frames_Sat3_ALiteralOfAnUnassignedVariableStaysBackground() {
        // The first step tries x1 = True alone: x2 and x3 are unassigned, so their literals are not in the frame at all.
        var (_, body) = await Visualize($"visualization={Sat3Vis}&format=frames", "(x1 | !x2) & (!x1 | x3) & (x2 | x3)");
        var first = Frames(body)[0];
        Assert.Equal(new Dictionary<string, bool> { ["v:x1"] = true }, Assignment(first));
        Assert.Equal(["c0-0", "c1-0"], States(first, "nodes").Keys.Order());
    }

    [Fact]
    public async Task Frames_VariablesAreNumberedInNaturalOrder() {
        var (_, body) = await Visualize($"visualization={SatVis}&format=frames", "(x10 | !x2) & (x1 | x2)");
        var payload = JsonDocument.Parse(body).RootElement.GetProperty("payload");
        Assert.Equal(["v:x1", "v:x2", "v:x10"], payload.GetProperty("variables").EnumerateArray().Select(v => v.GetProperty("id").GetString()));
    }
}
