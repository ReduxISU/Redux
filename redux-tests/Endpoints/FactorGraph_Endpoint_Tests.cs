using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// SAT's and 3SAT's second picture, the factor graph (a Bipartite Graph), on the actual /ProblemProvider/visualize output.
// The two visualizations are new, so the guard for old frontends is that their list format is exactly the default SAT
// and 3SAT pictures', and that /info shows nothing of the frames plumbing.
public class FactorGraph_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public FactorGraph_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private const string SatVis = "satfactorgraphvisualization";
    private const string Sat3Vis = "sat3factorgraphvisualization";

    private async Task<(HttpStatusCode Status, string Body)> Visualize(string query, string instance) {
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/visualize?{query}", content, TestContext.Current.CancellationToken);
        return (response.StatusCode, (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Replace("\r\n", "\n"));
    }

    private static List<JsonElement> Frames(string body) => JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().ToList();

    private static Dictionary<string, string> States(JsonElement frame, string collection) =>
        frame.GetProperty(collection).EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetProperty("state").GetString()!);

    private static readonly HashSet<string> Vocabulary = new() {
        "Background", "ElementHighlight", "Solution", "Rejected", "Untraveled", "Covered", "Blocked", "False",
    };

    // the factor graph, the default picture whose list format it shares, the problem, and every solver with steps
    public static TheoryData<string, string, string, string> Cases => new() {
        { SatVis, "satdefaultvisualization", "sat", "satbruteforcesolver" },
        { Sat3Vis, "sat3defaultvisualization", "sat3", "sat3backtrackingsolver" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task List_IsExactlyTheDefaultPicturesList(string vis, string defaultVis, string problem, string solver) {
        var p = (API.Interfaces.IProblem)Activator.CreateInstance(ProblemProvider.Problems[problem])!;
        var (status, body) = await Visualize($"visualization={vis}", p.defaultInstance);
        Assert.Equal(HttpStatusCode.OK, status);
        var (_, expected) = await Visualize($"visualization={defaultVis}", p.defaultInstance);
        Assert.Equal(expected, body);
        var (_, withSolver) = await Visualize($"visualization={vis}&solver={solver}&format=list", p.defaultInstance);
        Assert.Equal(expected, withSolver);
    }

    [Theory]
    [InlineData(SatVis, "SAT Factor Graph")]
    [InlineData(Sat3Vis, "3SAT Factor Graph")]
    public async Task Info_ReportsTheLegacyType_AndNoFramesPlumbing(string vis, string name) {
        var body = await (await _client.GetAsync($"/ProblemProvider/info?interface={vis}", TestContext.Current.CancellationToken))
            .Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var json = JsonDocument.Parse(body).RootElement;
        Assert.Equal(name, json.GetProperty("visualizationName").GetString());
        Assert.Equal("BooleanSatisfiability", json.GetProperty("visualizationType").GetString());
        Assert.Equal(["visualizationName", "visualizationDefinition", "source", "sourceFile", "contributors", "visualizationType", "solver"],
            json.EnumerateObject().Select(p => p.Name));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Frames_HaveTheFactorGraphPayload_WithTheClauseGraphsIds_AndFramesThatAgreeWithTheVerifier(string vis, string defaultVis, string problem, string solver) {
        var p = (API.Interfaces.IProblem)Activator.CreateInstance(ProblemProvider.Problems[problem])!;
        var (status, body) = await Visualize($"visualization={vis}&format=frames&solver={solver}", p.defaultInstance);
        Assert.Equal(HttpStatusCode.OK, status);
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal("BipartiteGraph", root.GetProperty("type").GetString());
        var payload = root.GetProperty("payload");
        Assert.Equal(["kind", "layout", "pickSide", "rule", "elements", "sets", "edges"], payload.EnumerateObject().Select(x => x.Name));
        Assert.Equal("assignment", payload.GetProperty("pickSide").GetString());
        Assert.DoesNotContain("\"state\"", payload.GetRawText());

        // The same ids as the clause graph of the same formula: variables, clauses, and one edge per literal occurrence.
        var (_, clauseBody) = await Visualize($"visualization={defaultVis}&format=frames&solver={solver}", p.defaultInstance);
        var clause = JsonDocument.Parse(clauseBody).RootElement.GetProperty("payload");
        Assert.Equal(clause.GetProperty("variables").EnumerateArray().Select(v => v.GetProperty("id").GetString()),
            payload.GetProperty("elements").EnumerateArray().Select(v => v.GetProperty("id").GetString()));
        Assert.Equal(clause.GetProperty("groups").EnumerateArray().Select(g => g.GetProperty("id").GetString()),
            payload.GetProperty("sets").EnumerateArray().Select(s => s.GetProperty("id").GetString()));
        var literals = clause.GetProperty("nodes").EnumerateArray().ToList();
        var edges = payload.GetProperty("edges").EnumerateArray().ToList();
        Assert.Equal(literals.Select(n => n.GetProperty("id").GetString()), edges.Select(e => e.GetProperty("id").GetString()));
        Assert.Equal(literals.Select(n => (n.GetProperty("variable").GetString(), n.GetProperty("group").GetString(), n.GetProperty("positive").GetBoolean())),
            edges.Select(e => (e.GetProperty("a").GetString(), e.GetProperty("b").GetString(), e.GetProperty("positive").GetBoolean())));

        var nodeIds = payload.GetProperty("elements").EnumerateArray().Concat(payload.GetProperty("sets").EnumerateArray()).Select(n => n.GetProperty("id").GetString()!).ToHashSet();
        var edgeIds = edges.Select(e => e.GetProperty("id").GetString()!).ToHashSet();
        var frames = Frames(body);
        Assert.True(frames.Count >= 2);
        foreach (var f in frames) {
            bool isDone = f.GetProperty("event").GetString() == "Done";
            Assert.Equal(["event", "caption", "focus", "nodes", "edges", "hits", "assignment", .. isDone ? new[] { "ok" } : []], f.EnumerateObject().Select(x => x.Name));
            foreach (var (collection, ids) in new[] { ("nodes", nodeIds), ("edges", edgeIds) })
                foreach (var (id, state) in States(f, collection)) {
                    Assert.Contains(id, ids);
                    Assert.Contains(state, Vocabulary);
                }
            foreach (var id in f.GetProperty("focus").EnumerateArray()) Assert.Contains(id.GetString()!, nodeIds);
        }
        // The clause graph and the factor graph draw the same run: same events, captions and assignments.
        var clauseFrames = Frames(clauseBody);
        Assert.Equal(clauseFrames.Select(f => f.GetProperty("caption").GetString()), frames.Select(f => f.GetProperty("caption").GetString()));
        Assert.Equal(clauseFrames.Select(f => f.GetProperty("assignment").GetRawText()), frames.Select(f => f.GetProperty("assignment").GetRawText()));

        var last = frames[^1];
        Assert.True(last.GetProperty("ok").GetBoolean());
        // Every clause of the answer has a true literal.
        Assert.All(payload.GetProperty("sets").EnumerateArray(), s => Assert.Equal("Covered", States(last, "nodes")[s.GetProperty("id").GetString()!]));
    }

    [Fact]
    public async Task Frames_Sat_ATriedAssignment_HighlightsTrueEdges_KeepsFalseOnesFalse_AndRejectsTheFailedClauseWithItsEdges() {
        // The SAT brute force's first candidate is all false; it fails C3 = (x1 | x2 | x3), whose edges are Rejected with it.
        var (_, body) = await Visualize($"visualization={SatVis}&format=frames", "(!x1 | x2) & (!x2 | x3) & (x1 | x2 | x3)");
        var first = Frames(body)[0];
        Assert.Equal("Reject", first.GetProperty("event").GetString());
        Assert.Equal(new Dictionary<string, string> {
            ["c0-0"] = "ElementHighlight", ["c0-1"] = "False",
            ["c1-0"] = "ElementHighlight", ["c1-1"] = "False",
            ["c2-0"] = "Rejected", ["c2-1"] = "Rejected", ["c2-2"] = "Rejected",
        }, States(first, "edges"));
        var nodes = States(first, "nodes");
        Assert.Equal("Rejected", nodes["c2"]);
        Assert.Equal("Covered", nodes["c0"]);
        Assert.Equal("False", nodes["v:x1"]);
    }

    [Fact]
    public async Task Frames_WhenNoAssignmentExists_EndNotOkWithNothingAssigned() {
        var (status, body) = await Visualize($"visualization={Sat3Vis}&format=frames", "(x1 | x2) & (!x1 | x2) & (x1 | !x2) & (!x1 | !x2)");
        Assert.Equal(HttpStatusCode.OK, status);
        var last = Frames(body)[^1];
        Assert.False(last.GetProperty("ok").GetBoolean());
        Assert.Empty(last.GetProperty("assignment").EnumerateObject());
        Assert.Contains(Frames(body), f => f.GetProperty("event").GetString() == "Backtrack");
    }

    [Theory]
    [InlineData("walksat")]
    [InlineData("ppz")]
    public async Task Frames_ASolverWithoutSteps_GivesOneDoneFrameBuiltFromItsAnswer(string solver) {
        var (status, body) = await Visualize($"visualization={Sat3Vis}&format=frames&solver={solver}", new API.Problems.NPComplete.NPC_SAT3.SAT3().defaultInstance);
        Assert.Equal(HttpStatusCode.OK, status);
        var done = Assert.Single(Frames(body));
        Assert.Equal("Done", done.GetProperty("event").GetString());
        Assert.True(done.GetProperty("ok").GetBoolean());
    }
}
