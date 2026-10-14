using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Graph picture's tour shape on the actual /ProblemProvider/visualize?format=frames output, for Hamiltonian Cycle,
// Directed Hamiltonian Cycle, Traveling Salesperson and Minimum Traveling Salesperson, plus the guards that keep everything old frontends read exactly as
// it was:
//   - the list format (snapshots in Snapshots/, captured before the tour shape existed),
//   - /info for every visualization and solver that was touched (info.<name>.json, captured the same way).
public class GraphTourFrames_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public GraphTourFrames_Endpoint_Tests(AppFactory factory) {
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
        "Background", "ElementHighlight", "Solution", "Rejected", "Untraveled", "Covered", "Blocked", "Committed",
    };

    private const string HamiltonianVis = "hamiltoniandefaultvisualization";
    private const string DirectedVis = "directedhamiltoniandefaultvisualization";
    private const string TspVis = "tspdefaultvisualization";
    private const string MinTspVis = "minimumtspdefaultvisualization";

    // visualization, its default solver, rule, subtype
    private static readonly (string Vis, string Solver, string Rule, string Subtype)[] CaseRows = {
        (HamiltonianVis, "hamiltonianbruteforce", "hamiltonianCycle", "undirected"),
        (DirectedVis, "directedhamiltonianbruteforce", "hamiltonianCycle", "directed"),
        (TspVis, "tspbruteforce", "tsp", "undirected"),
        (MinTspVis, "bruteforceminimumtsp", "minTsp", "undirected"),
    };

    public static TheoryData<string, string, string, string> Cases {
        get {
            var data = new TheoryData<string, string, string, string>();
            foreach (var c in CaseRows) data.Add(c.Vis, c.Solver, c.Rule, c.Subtype);
            return data;
        }
    }
    public static TheoryData<string> Visualizations => new(CaseRows.Select(c => c.Vis));
    public static TheoryData<string, string> VisualizationsAndSolvers => new(CaseRows.Select(c => (c.Vis, c.Solver)));

    // Every visualization and solver touched by the tour shape and the step recording.
    public static TheoryData<string> TouchedInterfaces => new(CaseRows.Select(c => c.Vis)
        .Concat(["hamiltonianbruteforce", "directedhamiltonianbruteforce", "tspbruteforce", "tspgreedy", "tspbranchandbound", "bruteforceminimumtsp"]));

    // Every (visualization, solver) pair a frontend can ask for.
    public static TheoryData<string, string> EverySolver => new() {
        { HamiltonianVis, "hamiltonianbruteforce" },
        { DirectedVis, "directedhamiltonianbruteforce" },
        { TspVis, "tspbruteforce" },
        { TspVis, "tspgreedy" },
        { TspVis, "tspbranchandbound" },
        { MinTspVis, "bruteforceminimumtsp" },
    };

    private static API.Interfaces.IProblem ProblemOf(string vis) =>
        (API.Interfaces.IProblem)Activator.CreateInstance(ProblemProvider.Problems[vis.Replace("defaultvisualization", "")])!;

    private static string DefaultInstance(string vis) => ProblemOf(vis).defaultInstance;

    [Theory]
    [MemberData(nameof(TouchedInterfaces))]
    public async Task Info_IsUnchangedByTheTourShape(string name) {
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

    // ---- The list format is what old frontends read: it must not change. ----

    [Theory]
    [MemberData(nameof(VisualizationsAndSolvers))]
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

    [Theory]
    [InlineData("tspgreedy")]
    [InlineData("tspbranchandbound")]
    public async Task List_WithAnotherTspSolver_HasNoStepItems(string solver) {
        var (status, body) = await Visualize($"visualization={TspVis}&solver={solver}", ReadSnapshot($"{TspVis}.instance.txt"));
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(2, JsonDocument.Parse(body).RootElement.GetArrayLength());
    }

    // ---- frames ----

    private static List<JsonElement> Frames(string body) => JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().ToList();

    private static Dictionary<string, string> States(JsonElement frame, string collection) =>
        frame.GetProperty(collection).EnumerateObject().ToDictionary(e => e.Name, e => e.Value.GetProperty("state").GetString()!);

    private static string[] Order(JsonElement frame) => frame.GetProperty("order").EnumerateArray().Select(o => o.GetString()!).ToArray();

    // The tour as a certificate, written here from the frame's order (not by the code under test).
    private static string Certificate(string[] order) => "{" + string.Join(",", order) + "}";

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Frames_DefaultSolver_HasGraphTypePayloadAndFrames(string vis, string solver, string rule, string subtype) {
        string instance = DefaultInstance(vis);
        var problem = ProblemOf(vis);
        var (status, body) = await Visualize($"visualization={vis}&format=frames&solver={solver}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var root = JsonDocument.Parse(body).RootElement;

        Assert.Equal("Graph", root.GetProperty("type").GetString());
        var payload = root.GetProperty("payload");
        Assert.Equal("graph", payload.GetProperty("kind").GetString());
        Assert.Equal(subtype, payload.GetProperty("subtype").GetString());
        Assert.Equal("circle", payload.GetProperty("layout").GetString());
        Assert.Equal("tour", payload.GetProperty("shape").GetString());
        Assert.Equal(rule, payload.GetProperty("rule").GetString());
        bool weighted = rule is "tsp" or "minTsp";
        bool hasK = rule == "tsp";   // Minimum Traveling Salesperson has no K
        Assert.Equal(hasK, payload.TryGetProperty("k", out var k));
        if (hasK) Assert.Equal(8000, k.GetInt32());

        var nodes = payload.GetProperty("nodes").EnumerateArray().ToList();
        var nodeIds = nodes.Select(n => n.GetProperty("id").GetString()!).ToHashSet();
        Assert.All(nodes, n => Assert.Equal(n.GetProperty("id").GetString(), n.GetProperty("label").GetString()));
        var edges = payload.GetProperty("edges").EnumerateArray().ToList();
        Assert.NotEmpty(edges);
        bool directed = subtype == "directed";
        foreach (var e in edges) {
            string a = e.GetProperty("a").GetString()!, b = e.GetProperty("b").GetString()!;
            if (directed) Assert.Equal($"{a}>{b}", e.GetProperty("id").GetString());
            else {
                Assert.True(string.CompareOrdinal(a, b) < 0, $"edge {a},{b} is not canonical (a < b)");
                Assert.Equal($"{a}-{b}", e.GetProperty("id").GetString());
            }
            Assert.Contains(a, nodeIds);
            Assert.Contains(b, nodeIds);
            Assert.Equal(weighted, e.TryGetProperty("weight", out _));
        }
        Assert.Equal(edges.Count, edges.Select(e => e.GetProperty("id").GetString()).Distinct().Count());
        // The payload is static and colorless: no state names anywhere in it.
        Assert.DoesNotContain("\"state\"", payload.GetRawText());
        Assert.DoesNotContain("\"color\"", payload.GetRawText());

        var edgeIds = edges.Select(e => e.GetProperty("id").GetString()!).ToHashSet();
        var frames = Frames(body);
        Assert.True(frames.Count >= 2, "the default solver records steps");
        foreach (var f in frames) {
            Assert.Contains(f.GetProperty("event").GetString(), new[] { "Try", "Accept", "Reject", "Backtrack", "Done" });
            Assert.False(string.IsNullOrWhiteSpace(f.GetProperty("caption").GetString()));
            foreach (var id in f.GetProperty("focus").EnumerateArray()) Assert.True(nodeIds.Contains(id.GetString()!) || edgeIds.Contains(id.GetString()!), id.GetString());
            foreach (var (collection, ids) in new[] { ("nodes", nodeIds), ("edges", edgeIds) })
                foreach (var entry in f.GetProperty(collection).EnumerateObject()) {
                    Assert.Contains(entry.Name, ids);
                    string state = entry.Value.GetProperty("state").GetString()!;
                    Assert.Contains(state, Vocabulary);
                    Assert.NotEqual("Background", state); // Background is omitted
                }
            foreach (var p in f.GetProperty("phantoms").EnumerateArray()) {
                Assert.Contains(p.GetProperty("a").GetString()!, nodeIds);
                Assert.Contains(p.GetProperty("b").GetString()!, nodeIds);
                Assert.Equal("Rejected", p.GetProperty("state").GetString());
            }
            var order = Order(f);
            Assert.All(order, n => Assert.Contains(n, nodeIds));
            Assert.False(f.TryGetProperty("groups", out _));
            bool isDone = f.GetProperty("event").GetString() == "Done";
            Assert.Equal(isDone, f.TryGetProperty("ok", out _));
        }

        // Exactly one Done, and it is last; its ok agrees with the problem's verifier on the tour it shows.
        Assert.Single(frames, f => f.GetProperty("event").GetString() == "Done");
        var last = frames[^1];
        Assert.Equal("Done", last.GetProperty("event").GetString());
        Assert.True(last.GetProperty("ok").GetBoolean());
        var tour = Order(last);
        Assert.Equal(nodeIds.Count + 1, tour.Length);   // every node once, then the start again
        Assert.Equal(tour[0], tour[^1]);
        Assert.True(problem.defaultVerifier.verify(instance, Certificate(tour)), Certificate(tour));
        // A solved tour: every step of it is a Solution edge, every node Solution, nothing rejected, no phantom.
        Assert.Equal(nodeIds.Count, States(last, "edges").Count);
        Assert.All(States(last, "edges").Values, s => Assert.Equal("Solution", s));
        Assert.Equal(nodeIds.Count, States(last, "nodes").Count);
        Assert.All(States(last, "nodes").Values, s => Assert.Equal("Solution", s));
        Assert.Equal(0, last.GetProperty("phantoms").GetArrayLength());
    }

    [Fact]
    public async Task Frames_Hamiltonian_FirstFrameStartsTheTour_AMissingEdgeIsADashedPhantom() {
        var (_, body) = await Visualize($"visualization={HamiltonianVis}&format=frames", DefaultInstance(HamiltonianVis));
        var frames = Frames(body);
        var first = frames[0];
        Assert.Equal("Try", first.GetProperty("event").GetString());
        Assert.Equal("Start the tour at 1.", first.GetProperty("caption").GetString());
        Assert.Equal(["1"], Order(first));
        Assert.Equal("ElementHighlight", States(first, "nodes")["1"]);
        Assert.Empty(States(first, "edges"));
        // 3 and 4 are not joined: the rejected step names the node and draws the edge that is missing.
        var missing = frames.First(f => f.GetProperty("caption").GetString() == "There is no edge between 3 and 4, so 4 can't come after 3.");
        Assert.Equal("Reject", missing.GetProperty("event").GetString());
        Assert.Equal(["1", "2", "3", "4"], Order(missing));
        Assert.Equal("Rejected", States(missing, "nodes")["4"]);
        var phantom = Assert.Single(missing.GetProperty("phantoms").EnumerateArray());
        Assert.Equal(("3", "4", "Rejected"), (phantom.GetProperty("a").GetString(), phantom.GetProperty("b").GetString(), phantom.GetProperty("state").GetString()));
        Assert.Equal(["1-2", "2-3"], States(missing, "edges").Keys.Order());
        // A backtrack takes a node off the path again.
        var back = frames.First(f => f.GetProperty("event").GetString() == "Backtrack");
        Assert.True(Order(back).Length < 5);
        // The last frame is the cycle the solver has always returned.
        Assert.Equal(["1", "2", "4", "5", "3", "1"], Order(frames[^1]));
    }

    [Fact]
    public async Task Frames_DirectedHamiltonian_UsesDirectedEdgeIds_AndAStepAgainstAnArrowIsAPhantom() {
        var (_, body) = await Visualize($"visualization={DirectedVis}&format=frames", DefaultInstance(DirectedVis));
        var root = JsonDocument.Parse(body).RootElement;
        var payload = root.GetProperty("payload");
        Assert.Equal("directed", payload.GetProperty("subtype").GetString());
        // The default instance is ({1..5},{(2,1),(1,3),(2,3),(3,5),(4,2),(5,4)}): ids keep the direction, a is the tail.
        Assert.Equal(["2>1", "1>3", "2>3", "3>5", "4>2", "5>4"], payload.GetProperty("edges").EnumerateArray().Select(e => e.GetProperty("id").GetString()));
        var first = payload.GetProperty("edges")[0];
        Assert.Equal(("2", "1"), (first.GetProperty("a").GetString(), first.GetProperty("b").GetString()));
        var frames = Frames(body);
        // 1 -> 2 would go against the arrow 2 -> 1: the node is rejected and the missing arrow is drawn.
        var against = frames.First(f => f.GetProperty("caption").GetString() == "There is no edge from 1 to 2, so 2 can't come after 1.");
        Assert.Equal("Reject", against.GetProperty("event").GetString());
        var phantom = Assert.Single(against.GetProperty("phantoms").EnumerateArray());
        Assert.Equal(("1", "2"), (phantom.GetProperty("a").GetString(), phantom.GetProperty("b").GetString()));
        Assert.Empty(States(against, "edges"));
        var step = frames.First(f => f.GetProperty("caption").GetString() == "Go 1 → 3: the path is 1 → 3.");
        Assert.Equal(["3", "1>3"], step.GetProperty("focus").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal("ElementHighlight", States(step, "edges")["1>3"]);
        // The solved frame: the cycle 1 3 5 4 2 1 along the arrows.
        var last = frames[^1];
        Assert.Equal(["1", "3", "5", "4", "2", "1"], Order(last));
        Assert.Equal(["1>3", "2>1", "3>5", "4>2", "5>4"], States(last, "edges").Keys.Order());
    }

    [Fact]
    public async Task Frames_Tsp_PayloadCarriesWeights_AndTheCaptionsCarryTheCost() {
        var (_, body) = await Visualize($"visualization={TspVis}&format=frames&solver=tspgreedy", DefaultInstance(TspVis));
        var root = JsonDocument.Parse(body).RootElement;
        var edges = root.GetProperty("payload").GetProperty("edges").EnumerateArray().ToList();
        Assert.Equal(10, edges.Count);
        var ny = edges.First(e => e.GetProperty("id").GetString() == "Chicago-New York");
        Assert.Equal(790, ny.GetProperty("weight").GetDouble());
        var frames = Frames(body);
        Assert.Equal("Go New York → Chicago (weight 790, path cost 790): the nearest city not yet visited.", frames[1].GetProperty("caption").GetString());
        Assert.Equal("Close the tour Miami → New York: total 6830, the best so far.", frames[5].GetProperty("caption").GetString());
        Assert.Equal(["New York", "Chicago", "Denver", "Los Angeles", "Miami", "New York"], Order(frames[5]));
        // A worse tour is rejected with the best so far named. The greedy built the route hop by hop, so the route stays the
        // path so far (Committed, #886) and only the closing hop that was tested is Rejected.
        var worse = frames.First(f => f.GetProperty("caption").GetString()!.StartsWith("Close the tour Los Angeles → Chicago"));
        Assert.Equal("Reject", worse.GetProperty("event").GetString());
        Assert.Contains("total 7160 is no better than the best so far (6830)", worse.GetProperty("caption").GetString());
        var worseEdges = States(worse, "edges");
        Assert.Equal(5, worseEdges.Count);
        Assert.Equal("Rejected", worseEdges["Chicago-Los Angeles"]);
        Assert.All(worseEdges.Where(e => e.Key != "Chicago-Los Angeles"), e => Assert.Equal("Committed", e.Value));
        Assert.Equal("Done", frames[^1].GetProperty("event").GetString());
        Assert.Equal(["New York", "Chicago", "Denver", "Los Angeles", "Miami", "New York"], Order(frames[^1]));
    }

    // Minimum Traveling Salesperson's brute force judges whole tours: a tour it only tries is highlighted, never drawn as
    // Solution; a new best tour (Accept) and the answer (Done) are.
    [Fact]
    public async Task Frames_MinimumTsp_TriesAreHighlighted_OnlyAcceptsAndDoneAreSolution() {
        var (status, body) = await Visualize($"visualization={MinTspVis}&format=frames", DefaultInstance(MinTspVis));
        Assert.Equal(HttpStatusCode.OK, status);
        var root = JsonDocument.Parse(body).RootElement;
        var payload = root.GetProperty("payload");
        Assert.Equal("minTsp", payload.GetProperty("rule").GetString());
        Assert.False(payload.TryGetProperty("k", out _));
        var ab = payload.GetProperty("edges").EnumerateArray().First(e => e.GetProperty("id").GetString() == "a-b");
        Assert.Equal(3, ab.GetProperty("weight").GetDouble());

        var frames = Frames(body);
        Assert.Equal(24 + 1, frames.Count);
        foreach (var f in frames) {
            string ev = f.GetProperty("event").GetString()!;
            var nodes = States(f, "nodes");
            var edges = States(f, "edges");
            Assert.Equal(5, Order(f).Length - 1);   // every frame is a whole closed tour
            switch (ev) {
                case "Accept":
                    Assert.Equal(5, edges.Count);
                    Assert.All(edges.Values, s => Assert.Equal("Solution", s));
                    Assert.All(nodes.Values, s => Assert.Equal("Covered", s));
                    break;
                case "Reject":
                    Assert.DoesNotContain("Solution", nodes.Values.Concat(edges.Values));
                    Assert.Contains("Rejected", nodes.Values.Concat(edges.Values));
                    break;
                case "Done":
                    Assert.All(nodes.Values.Concat(edges.Values), s => Assert.Equal("Solution", s));
                    break;
                default:
                    Assert.Fail($"unexpected {ev}");
                    break;
            }
        }
        Assert.Equal("Try a → c → b → e → d → a: cost 19, a new best tour (it beats 21).",
            frames.Last(f => f.GetProperty("event").GetString() == "Accept").GetProperty("caption").GetString());
        Assert.Equal(["a", "c", "b", "e", "d", "a"], Order(frames[^1]));
        Assert.True(frames[^1].GetProperty("ok").GetBoolean());
    }

    // a and b are joined twice, 10 then 1 (#769). Minimum TSP uses the lightest, so the cheapest tour a-b-c-d-a costs 4: the
    // payload draws one a-b edge weighing 1 and labelled "10,1", and the captions and the answer use the 1. Before #769
    // the instance was refused ("more than one edge joins a and b").
    [Fact]
    public async Task Frames_MinimumTsp_ParallelEdges_AreOneEdge_WeighingTheLightest() {
        const string instance = "({a,b,c,d},{({a,b},10),({a,b},1),({b,c},1),({c,d},1),({d,a},1),({a,c},5),({b,d},5)})";
        var (status, body) = await Visualize($"visualization={MinTspVis}&format=frames", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var root = JsonDocument.Parse(body).RootElement;
        var edges = root.GetProperty("payload").GetProperty("edges").EnumerateArray().ToDictionary(e => e.GetProperty("id").GetString()!, e => e);
        Assert.Equal(6, edges.Count);
        Assert.Equal(1, edges["a-b"].GetProperty("weight").GetInt32());
        Assert.Equal("10,1", edges["a-b"].GetProperty("label").GetString());
        Assert.Single(edges.Values, e => e.TryGetProperty("label", out _));
        var frames = Frames(body);
        Assert.All(frames, f => Assert.All(States(f, "edges").Keys, id => Assert.Contains(id, edges.Keys)));
        Assert.StartsWith("Try a → b → c → d → a: cost 4, the first tour found", frames[0].GetProperty("caption").GetString());
        Assert.Equal(["a", "b", "c", "d", "a"], Order(frames[^1]));
        Assert.True(frames[^1].GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task Frames_MinimumTsp_PastTheCap_TheLastAcceptIsKeptAndDrawnAsSolution() {
        // K7 whose only cheapest tour, a-f-b-d-c-e-g-a (cost 7), is found long after the first 150 orderings.
        string[] cities = ["a", "b", "c", "d", "e", "f", "g"];
        var cheap = new HashSet<string> { "a-f", "b-f", "b-d", "c-d", "c-e", "e-g", "a-g" };
        var edges = new List<string>();
        for (int i = 0; i < 7; i++)
            for (int j = i + 1; j < 7; j++)
                edges.Add($"({{{cities[i]},{cities[j]}}},{(cheap.Contains($"{cities[i]}-{cities[j]}") ? 1 : 10)})");
        string instance = $"({{{string.Join(",", cities)}}},{{{string.Join(",", edges)}}})";

        var (status, body) = await Visualize($"visualization={MinTspVis}&format=frames", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var frames = Frames(body);
        Assert.Equal(152, frames.Count);
        var lastAccept = frames[^2];
        Assert.Equal("Accept", lastAccept.GetProperty("event").GetString());
        Assert.StartsWith("Try a → f → b → d → c → e → g → a: cost 7, a new best tour", lastAccept.GetProperty("caption").GetString());
        Assert.Equal(cheap.Order(), States(lastAccept, "edges").Keys.Order());
        Assert.All(States(lastAccept, "edges").Values, s => Assert.Equal("Solution", s));
        Assert.Equal("Done", frames[^1].GetProperty("event").GetString());
        Assert.Contains("not shown", frames[^1].GetProperty("caption").GetString());
        Assert.Equal(["a", "f", "b", "d", "c", "e", "g", "a"], Order(frames[^1]));
    }

    // 1 and 2 are joined by two edges, 5 and 1 (#769). A tour uses the lightest, so 1-2-3-1 costs 3 = K: the payload draws
    // one 1-2 edge weighing 1 and labelled "5,1", and the captions' costs use the 1. Before #769 the payload and the
    // Greedy's captions took the first edge listed (5), and every solver answered {} because the verifier did too.
    [Theory]
    [InlineData("tspbruteforce")]
    [InlineData("tspgreedy")]
    [InlineData("tspbranchandbound")]
    public async Task Frames_Tsp_ParallelEdges_AreOneEdge_WeighingTheLightest(string solver) {
        const string instance = "(({1,2,3},{({1,2},5),({1,2},1),({2,3},1),({1,3},1)}),3)";
        var (status, body) = await Visualize($"visualization={TspVis}&format=frames&solver={solver}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var root = JsonDocument.Parse(body).RootElement;
        var edges = root.GetProperty("payload").GetProperty("edges").EnumerateArray().ToDictionary(e => e.GetProperty("id").GetString()!, e => e);
        Assert.Equal(["1-2", "2-3", "1-3"], edges.Keys);
        Assert.Equal(1, edges["1-2"].GetProperty("weight").GetInt32());
        Assert.Equal("5,1", edges["1-2"].GetProperty("label").GetString());
        var frames = Frames(body);
        Assert.All(frames, f => Assert.All(States(f, "edges").Keys, id => Assert.Contains(id, edges.Keys)));
        Assert.DoesNotContain(frames, f => f.GetProperty("caption").GetString()!.Contains("weight 5"));
        var done = frames[^1];
        Assert.True(done.GetProperty("ok").GetBoolean());
        Assert.Equal(4, Order(done).Length);
        var order = Order(done);
        int cost = Enumerable.Range(0, order.Length - 1).Sum(i => edges[string.CompareOrdinal(order[i], order[i + 1]) < 0 ? $"{order[i]}-{order[i + 1]}" : $"{order[i + 1]}-{order[i]}"].GetProperty("weight").GetInt32());
        Assert.Equal(3, cost);
    }

    [Theory]
    [MemberData(nameof(EverySolver))]
    public async Task Frames_EveryCompatibleSolver_EndsInAVerifiedDoneFrame(string vis, string solver) {
        var instance = ReadSnapshot($"{vis}.instance.txt");
        var (status, body) = await Visualize($"visualization={vis}&format=frames&solver={solver}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var frames = Frames(body);
        Assert.True(frames.Count >= 2);
        Assert.Single(frames, f => f.GetProperty("event").GetString() == "Done");
        Assert.Equal("Done", frames[^1].GetProperty("event").GetString());
        Assert.True(frames[^1].GetProperty("ok").GetBoolean());
        Assert.True(ProblemOf(vis).defaultVerifier.verify(instance, Certificate(Order(frames[^1]))));
    }

    [Fact]
    public async Task Frames_TheChosenSolverChangesTheFrames_NotThePayload() {
        string instance = DefaultInstance(TspVis);
        var bodies = new Dictionary<string, string>();
        foreach (var solver in new[] { "tspbruteforce", "tspgreedy", "tspbranchandbound" })
            bodies[solver] = (await Visualize($"visualization={TspVis}&format=frames&solver={solver}", instance)).Body;
        var payloads = bodies.Values.Select(b => JsonDocument.Parse(b).RootElement.GetProperty("payload").GetRawText()).Distinct().ToList();
        Assert.Single(payloads);
        Assert.Equal(3, bodies.Values.Distinct().Count());
        Assert.StartsWith("Start from the greedy tour", Frames(bodies["tspbranchandbound"])[0].GetProperty("caption").GetString());
        Assert.StartsWith("Start at New York", Frames(bodies["tspgreedy"])[0].GetProperty("caption").GetString());
    }

    [Theory]
    [InlineData(HamiltonianVis, "hamiltonianbruteforce", "({1,2,3,4},{{1,2},{2,3},{3,4}})")]
    [InlineData(DirectedVis, "directedhamiltonianbruteforce", "({1,2,3},{(1,2),(2,3),(1,3)})")]
    [InlineData(TspVis, "tspbruteforce", "(({a,b,c},{({a,b},1),({b,c},2),({a,c},3)}),5)")]
    [InlineData(TspVis, "tspgreedy", "(({a,b,c},{({a,b},1),({b,c},2),({a,c},3)}),5)")]
    [InlineData(TspVis, "tspbranchandbound", "(({a,b,c},{({a,b},1),({b,c},2),({a,c},3)}),5)")]
    [InlineData(MinTspVis, "bruteforceminimumtsp", "({a,b,c},{({a,b},1),({b,c},2)})")]
    public async Task Frames_WhenThereIsNoAnswer_EndsNotOkWithAnEmptyTour(string vis, string solver, string instance) {
        var (status, body) = await Visualize($"visualization={vis}&format=frames&solver={solver}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var last = Frames(body).Last();
        Assert.Equal("Done", last.GetProperty("event").GetString());
        Assert.False(last.GetProperty("ok").GetBoolean());
        Assert.Empty(Order(last));
        Assert.Empty(last.GetProperty("edges").EnumerateObject());
        Assert.Empty(last.GetProperty("nodes").EnumerateObject());
        Assert.Equal(0, last.GetProperty("phantoms").GetArrayLength());
    }

    [Fact]
    public async Task Frames_StepsAreCappedAndSayHowManyAreHidden() {
        // K7 with every weight 10 and K = 69: all 5040 permutations are rejected, far more than the cap of 150.
        var nodes = Enumerable.Range(1, 7).Select(i => i.ToString()).ToList();
        var edges = new List<string>();
        for (int a = 0; a < 7; a++) for (int b = a + 1; b < 7; b++) edges.Add($"({{{nodes[a]},{nodes[b]}}},10)");
        string instance = $"(({{{string.Join(",", nodes)}}},{{{string.Join(",", edges)}}}),69)";
        var (status, body) = await Visualize($"visualization={TspVis}&format=frames&solver=tspbruteforce", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var frames = Frames(body);
        Assert.True(frames.Count <= 151, $"{frames.Count} frames");
        Assert.Equal("Done", frames[^1].GetProperty("event").GetString());
        Assert.Contains("not shown", frames[^1].GetProperty("caption").GetString());
    }
}
