using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Graph picture on the actual /ProblemProvider/visualize?format=frames output, for the five subset-of-nodes
// visualizations, plus the guards that keep everything old frontends read exactly as it was:
//   - the list format (snapshots in Snapshots/, captured before the Graph picture existed),
//   - /info for every visualization and solver that was touched (info.<name>.json, captured the same way).
public class GraphFrames_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public GraphFrames_Endpoint_Tests(AppFactory factory) {
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
        "Background", "ElementHighlight", "Solution", "Rejected", "Untraveled", "Covered", "Blocked",
    };

    // visualization, its default solver, the rule it declares, whether the instance has a K
    private static readonly (string Vis, string Solver, string Rule, bool HasK)[] CaseRows = {
        ("cliquedefaultvisualization", "cliquebruteforce", "clique", true),
        ("vertexcoverdefaultvisualization", "vertexcoverbruteforce", "vertexCover", true),
        ("minimumvertexcoverdefaultvisualization", "bruteforceminimumvertexcover", "minVertexCover", false),
        ("independentsetdefaultvisualization", "independentsetbruteforce", "independentSet", true),
        ("dominatingsetdefaultvisualization", "dominatingsetforcedvertex", "dominatingSet", true),
    };

    public static TheoryData<string, string, string, bool> Cases {
        get {
            var data = new TheoryData<string, string, string, bool>();
            foreach (var c in CaseRows) data.Add(c.Vis, c.Solver, c.Rule, c.HasK);
            return data;
        }
    }
    public static TheoryData<string> Visualizations => new(CaseRows.Select(c => c.Vis));
    public static TheoryData<string, string> VisualizationsAndSolvers => new(CaseRows.Select(c => (c.Vis, c.Solver)));

    // Every visualization and solver touched by the Graph picture and the step recording.
    public static TheoryData<string> TouchedInterfaces => new(new[] {
        "cliquedefaultvisualization", "vertexcoverdefaultvisualization", "minimumvertexcoverdefaultvisualization",
        "independentsetdefaultvisualization", "dominatingsetdefaultvisualization",
        "carraghanpardalos", "chibanishizeki", "cliquebruteforce",
        "greedyvertexcover", "twoapproximationvertexcover", "vertexcoverboundedsearchtree", "vertexcoverbruteforce", "vertexcoverbusskernelization",
        "bruteforceminimumvertexcover", "greedyminimumvertexcover", "twoapproximationminimumvertexcover",
        "independentsetbruteforce", "dominatingsetforcedvertex", "dominatingsetnaiveinclusionexclusion",
    });

    [Theory]
    [MemberData(nameof(TouchedInterfaces))]
    public async Task Info_IsUnchangedByTheGraphPicture(string name) {
        var response = await _client.GetAsync($"/ProblemProvider/info?interface={name}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ReadSnapshot($"info.{name}.json"), body.Replace("\r\n", "\n"));
    }

    [Theory]
    [MemberData(nameof(Visualizations))]
    public async Task Info_StillReportsTheLegacyType_NotGraph(string vis) {
        var response = await _client.GetAsync($"/ProblemProvider/info?interface={vis}", TestContext.Current.CancellationToken);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement;
        Assert.Equal("GraphD3", json.GetProperty("visualizationType").GetString());
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Frames_DefaultSolver_HasGraphTypePayloadAndFrames(string vis, string solver, string rule, bool hasK) {
        var problem = (API.Interfaces.IProblem)Activator.CreateInstance(ProblemProvider.Problems[vis.Replace("defaultvisualization", "")])!;
        var (status, body) = await Visualize($"visualization={vis}&format=frames&solver={solver}", problem.defaultInstance);
        Assert.Equal(HttpStatusCode.OK, status);
        var root = JsonDocument.Parse(body).RootElement;

        Assert.Equal("Graph", root.GetProperty("type").GetString());
        var payload = root.GetProperty("payload");
        Assert.Equal("graph", payload.GetProperty("kind").GetString());
        Assert.Equal("undirected", payload.GetProperty("subtype").GetString());
        Assert.Equal("force", payload.GetProperty("layout").GetString());
        Assert.Equal("subset", payload.GetProperty("shape").GetString());
        Assert.Equal(rule, payload.GetProperty("rule").GetString());
        Assert.Equal(hasK, payload.TryGetProperty("k", out var k) && k.ValueKind == JsonValueKind.Number);

        var nodes = payload.GetProperty("nodes").EnumerateArray().ToList();
        Assert.NotEmpty(nodes);
        Assert.All(nodes, n => Assert.Equal(n.GetProperty("id").GetString(), n.GetProperty("label").GetString()));
        var nodeIds = nodes.Select(n => n.GetProperty("id").GetString()!).ToHashSet();
        var edges = payload.GetProperty("edges").EnumerateArray().ToList();
        Assert.NotEmpty(edges);
        foreach (var e in edges) {
            string a = e.GetProperty("a").GetString()!, b = e.GetProperty("b").GetString()!;
            Assert.True(string.CompareOrdinal(a, b) < 0, $"edge {a},{b} is not canonical (a < b)");
            Assert.Equal($"{a}-{b}", e.GetProperty("id").GetString());
            Assert.Contains(a, nodeIds);
            Assert.Contains(b, nodeIds);
            Assert.False(e.TryGetProperty("weight", out _), "unweighted problems carry no weight");
        }
        Assert.Equal(edges.Count, edges.Select(e => e.GetProperty("id").GetString()).Distinct().Count());
        // The payload is static and colorless: no state names anywhere in it.
        Assert.DoesNotContain("\"state\"", payload.GetRawText());
        Assert.DoesNotContain("\"color\"", payload.GetRawText());

        var edgeIds = edges.Select(e => e.GetProperty("id").GetString()!).ToHashSet();
        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        Assert.True(frames.Count >= 2, "the default solver records steps");
        foreach (var f in frames) {
            Assert.Contains(f.GetProperty("event").GetString(), new[] { "Try", "Accept", "Reject", "Backtrack", "Done" });
            Assert.False(string.IsNullOrWhiteSpace(f.GetProperty("caption").GetString()));
            Assert.Equal(JsonValueKind.Array, f.GetProperty("focus").ValueKind);
            foreach (var id in f.GetProperty("focus").EnumerateArray()) Assert.True(nodeIds.Contains(id.GetString()!) || edgeIds.Contains(id.GetString()!));
            foreach (var (collection, ids) in new[] { ("nodes", nodeIds), ("edges", edgeIds) })
                foreach (var entry in f.GetProperty(collection).EnumerateObject()) {
                    Assert.Contains(entry.Name, ids);
                    string state = entry.Value.GetProperty("state").GetString()!;
                    Assert.Contains(state, Vocabulary);
                    Assert.NotEqual("Background", state); // Background is omitted
                }
            foreach (var p in f.GetProperty("phantoms").EnumerateArray()) {
                Assert.Contains(p.GetProperty("a").GetString()!, nodeIds);
                Assert.Contains(p.GetProperty("state").GetString()!, Vocabulary);
            }
            bool isDone = f.GetProperty("event").GetString() == "Done";
            Assert.Equal(isDone, f.TryGetProperty("ok", out _));
        }

        // Exactly one Done, and it is last; its ok agrees with the problem's verifier on the answer.
        Assert.Single(frames, f => f.GetProperty("event").GetString() == "Done");
        var last = frames[^1];
        Assert.Equal("Done", last.GetProperty("event").GetString());
        var chosen = last.GetProperty("nodes").EnumerateObject()
            .Where(n => n.Value.GetProperty("state").GetString() == "Solution").Select(n => n.Name).ToList();
        string answer = "{" + string.Join(",", chosen) + "}";
        bool verified = problem.defaultVerifier.verify(problem.defaultInstance, answer);
        Assert.True(last.GetProperty("ok").GetBoolean());
        Assert.Equal(verified, last.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task Frames_Clique_CarriesPhantomsOnlyForRejectedCandidates() {
        // Brute force tries sets of 4 nodes. A failed candidate is a Reject frame whose chosen nodes are all
        // Solution (tentative) and which draws a Rejected phantom for every unjoined pair; the answer has none.
        var instance = ReadSnapshot("cliquedefaultvisualization.instance.txt");
        var (_, body) = await Visualize("visualization=cliquedefaultvisualization&format=frames", instance);
        var frames = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().ToList();

        var rejects = frames.Where(f => f.GetProperty("event").GetString() == "Reject").ToList();
        Assert.NotEmpty(rejects);
        Assert.All(rejects, f => Assert.True(f.GetProperty("phantoms").GetArrayLength() > 0, "a rejected clique candidate has an unjoined pair"));
        Assert.All(rejects, f => Assert.All(f.GetProperty("phantoms").EnumerateArray(), p => Assert.Equal("Rejected", p.GetProperty("state").GetString())));
        Assert.Equal(0, frames[^1].GetProperty("phantoms").GetArrayLength());
    }

    [Fact]
    public async Task Frames_IndependentSet_BlocksNeighboursOfChosenNodes() {
        var instance = ReadSnapshot("independentsetdefaultvisualization.instance.txt");
        var (_, body) = await Visualize("visualization=independentsetdefaultvisualization&format=frames", instance);
        var root = JsonDocument.Parse(body).RootElement;
        var last = root.GetProperty("frames").EnumerateArray().Last();

        var chosen = last.GetProperty("nodes").EnumerateObject().Where(n => n.Value.GetProperty("state").GetString() == "Solution").Select(n => n.Name).ToHashSet();
        var blocked = last.GetProperty("nodes").EnumerateObject().Where(n => n.Value.GetProperty("state").GetString() == "Blocked").Select(n => n.Name).ToHashSet();
        Assert.NotEmpty(blocked);
        var edges = root.GetProperty("payload").GetProperty("edges").EnumerateArray()
            .Select(e => (A: e.GetProperty("a").GetString()!, B: e.GetProperty("b").GetString()!)).ToList();
        var neighbours = edges.Where(e => chosen.Contains(e.A) != chosen.Contains(e.B)).Select(e => chosen.Contains(e.A) ? e.B : e.A).ToHashSet();
        Assert.Equal(neighbours, blocked);
    }

    [Fact]
    public async Task Frames_VertexCoverWithAnotherSolver_UsesThatSolversSteps() {
        const string vis = "vertexcoverdefaultvisualization";
        const string instance = "(({a,b,c,d},{{a,b},{b,c},{c,d}}),2)";   // taking a then b fails, so the tree backs out of b
        var (_, own) = await Visualize($"visualization={vis}&format=frames", instance);
        var (_, tree) = await Visualize($"visualization={vis}&format=frames&solver=vertexcoverboundedsearchtree", instance);

        string[] Events(string json) => JsonDocument.Parse(json).RootElement.GetProperty("frames").EnumerateArray()
            .Select(f => f.GetProperty("event").GetString()!).ToArray();
        string[] Captions(string json) => JsonDocument.Parse(json).RootElement.GetProperty("frames").EnumerateArray()
            .Select(f => f.GetProperty("caption").GetString()!).ToArray();

        // Brute force only tries candidate sets; the search tree takes ends of edges and backs out.
        Assert.DoesNotContain("Backtrack", Events(own));
        Assert.Contains("Backtrack", Events(tree));
        Assert.Contains(Captions(tree), c => c.Contains("Take "));
        Assert.Equal("Done", Events(tree)[^1]);
        Assert.Equal(JsonDocument.Parse(own).RootElement.GetProperty("payload").GetRawText(),
            JsonDocument.Parse(tree).RootElement.GetProperty("payload").GetRawText());
    }

    [Theory]
    [InlineData("vertexcoverdefaultvisualization", "greedyvertexcover")]
    [InlineData("vertexcoverdefaultvisualization", "vertexcoverbusskernelization")]
    [InlineData("minimumvertexcoverdefaultvisualization", "greedyminimumvertexcover")]
    [InlineData("minimumvertexcoverdefaultvisualization", "twoapproximationminimumvertexcover")]
    [InlineData("cliquedefaultvisualization", "carraghanpardalos")]
    [InlineData("cliquedefaultvisualization", "chibanishizeki")]
    [InlineData("dominatingsetdefaultvisualization", "dominatingsetnaiveinclusionexclusion")]
    public async Task Frames_EveryCompatibleSolver_EndsInADoneFrame(string vis, string solver) {
        var instance = ReadSnapshot($"{vis}.instance.txt");
        var (status, body) = await Visualize($"visualization={vis}&format=frames&solver={solver}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var frames = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().ToList();
        Assert.True(frames.Count >= 2);
        Assert.Equal("Done", frames[^1].GetProperty("event").GetString());
        Assert.True(frames[^1].GetProperty("ok").GetBoolean());
        Assert.Single(frames, f => f.GetProperty("event").GetString() == "Done");
    }

    [Fact]
    public async Task Frames_VertexCoverWhereTheSolverFindsNoCover_EndsRejectedAndNotOk() {
        // The 2-approximation always uses 4 nodes on the default instance, more than K = 3, so it answers "{}".
        // The final frame is not ok, nothing is chosen, and every edge is Rejected (none is covered).
        const string vis = "vertexcoverdefaultvisualization";
        var (status, body) = await Visualize($"visualization={vis}&format=frames&solver=twoapproximationvertexcover", ReadSnapshot($"{vis}.instance.txt"));
        Assert.Equal(HttpStatusCode.OK, status);
        var root = JsonDocument.Parse(body).RootElement;
        var last = root.GetProperty("frames").EnumerateArray().Last();
        Assert.Equal("Done", last.GetProperty("event").GetString());
        Assert.False(last.GetProperty("ok").GetBoolean());
        Assert.Empty(last.GetProperty("nodes").EnumerateObject());
        var rejected = last.GetProperty("edges").EnumerateObject().Select(e => e.Value.GetProperty("state").GetString()).ToList();
        Assert.Equal(root.GetProperty("payload").GetProperty("edges").GetArrayLength(), rejected.Count);
        Assert.All(rejected, s => Assert.Equal("Rejected", s));
    }

    [Fact]
    public async Task Frames_VertexCoverTwoApproximation_TakesBothEndsOfEachEdgeAndEndsOk() {
        // With K large enough the random 2-approximation always fits.
        const string instance = "(({a,b,c,d,e},{{a,b},{a,c},{a,e},{b,e},{c,d}}),5)";
        var (_, body) = await Visualize("visualization=vertexcoverdefaultvisualization&format=frames&solver=twoapproximationvertexcover", instance);
        var frames = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().ToList();
        Assert.All(frames.Take(frames.Count - 1), f => Assert.Equal("Accept", f.GetProperty("event").GetString()));
        Assert.All(frames.Take(frames.Count - 1), f => Assert.EndsWith("Take both ends.", f.GetProperty("caption").GetString()));
        Assert.True(frames[^1].GetProperty("ok").GetBoolean());
        Assert.All(frames[^1].GetProperty("edges").EnumerateObject(), e => Assert.Equal("Covered", e.Value.GetProperty("state").GetString()));
    }

    [Fact]
    public async Task Reduce_FromAGraphPictureProblem_StillSerializes_WithoutLeakingStepShape() {
        // IVisualization.StepShape is a System.Type. Written through an interface-typed property (as reduce does for a
        // reduction's visualizations) it made the endpoint return 500 until it was marked [JsonIgnore].
        var content = new StringContent(JsonSerializer.Serialize("(({a,b,c,d,e},{{a,b},{a,c},{a,e},{b,e},{c,d}}),3)"), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/reduce?reduction=karpvertexcovertosetcover", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("StepShape", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Frames_StepsAreCappedAndSayHowManyAreHidden() {
        // 12 nodes, K = 6, no edges: every size-6 set is a candidate (924 of them), far more than the cap of 150.
        const string instance = "(({a,b,c,d,e,f,g,h,i,j,k,l},{{a,b}}),6)";
        var (status, body) = await Visualize("visualization=independentsetdefaultvisualization&format=frames&solver=independentsetbruteforce", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var frames = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().ToList();
        Assert.True(frames.Count <= 151, $"{frames.Count} frames");
        Assert.Equal("Done", frames[^1].GetProperty("event").GetString());
        Assert.Contains("not shown", frames[^1].GetProperty("caption").GetString());
    }

    // ---- The list format is what old frontends read: it must not change. ----

    [Theory]
    [MemberData(nameof(VisualizationsAndSolvers))]
    public async Task List_DefaultResponse_MatchesPreChangeSnapshot(string vis, string solver) {
        var instance = ReadSnapshot($"{vis}.instance.txt");
        var (status, body) = await Visualize($"visualization={vis}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(ReadSnapshot($"{vis}.json"), body);
        var (_, withSolver) = await Visualize($"visualization={vis}&solver={solver}&format=list", instance);
        Assert.Equal(body, withSolver);
    }

    [Theory]
    [InlineData("cliquedefaultvisualization", "carraghanpardalos")]
    [InlineData("cliquedefaultvisualization", "chibanishizeki")]
    [InlineData("vertexcoverdefaultvisualization", "greedyvertexcover")]
    [InlineData("vertexcoverdefaultvisualization", "vertexcoverboundedsearchtree")]
    [InlineData("vertexcoverdefaultvisualization", "vertexcoverbusskernelization")]
    [InlineData("minimumvertexcoverdefaultvisualization", "greedyminimumvertexcover")]
    [InlineData("dominatingsetdefaultvisualization", "dominatingsetnaiveinclusionexclusion")]
    public async Task List_WithATypedStepSolver_HasNoStepItems(string vis, string solver) {
        // The solver now records typed NodeSet steps, but the list format is [initial, solved] exactly as before.
        var instance = ReadSnapshot($"{vis}.instance.txt");
        var (status, body) = await Visualize($"visualization={vis}&solver={solver}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(2, JsonDocument.Parse(body).RootElement.GetArrayLength());
    }
}
