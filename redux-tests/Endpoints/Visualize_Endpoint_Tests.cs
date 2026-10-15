using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Tests for /ProblemProvider/visualize: the optional `solver` and `format` parameters. Leaving both out must give
// the same picture as naming the defaults (the visualization's own solver, format=list), which is what the endpoint
// returned before the parameters existed. Responses are compared as parsed JSON: object keys in any order, arrays
// in order.
public class Visualize_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public Visualize_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private const string InvalidSat3Instance = "(1 | 2 | 3)";

    private async Task<(HttpStatusCode Status, string Body)> Visualize(string query, string instance) {
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/visualize?{query}", content, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return (response.StatusCode, body);
    }

    private async Task<JsonElement> VisualizeOk(string query, string instance) {
        var (status, body) = await Visualize(query, instance);
        Assert.Equal(HttpStatusCode.OK, status);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    /// <summary>
    /// Structural JSON equality: objects match when they have the same keys with equal values, in any order;
    /// arrays match element by element, in order; numbers compare by value.
    /// </summary>
    internal static bool JsonEquivalent(JsonElement a, JsonElement b) {
        if (a.ValueKind != b.ValueKind)
            return false;
        switch (a.ValueKind) {
            case JsonValueKind.Object:
                var aProps = a.EnumerateObject().ToList();
                var bProps = b.EnumerateObject().ToList();
                if (aProps.Count != bProps.Count)
                    return false;
                var bByName = new Dictionary<string, JsonElement>();
                foreach (var p in bProps) {
                    if (!bByName.TryAdd(p.Name, p.Value))
                        return false; // duplicate keys are never expected in our output
                }
                foreach (var p in aProps) {
                    if (!bByName.TryGetValue(p.Name, out var other) || !JsonEquivalent(p.Value, other))
                        return false;
                }
                return true;
            case JsonValueKind.Array:
                if (a.GetArrayLength() != b.GetArrayLength())
                    return false;
                return a.EnumerateArray().Zip(b.EnumerateArray()).All(pair => JsonEquivalent(pair.First, pair.Second));
            case JsonValueKind.String:
                return a.GetString() == b.GetString();
            case JsonValueKind.Number:
                return a.GetDecimal() == b.GetDecimal();
            default:
                return true; // true, false, null: the kinds already matched
        }
    }

    private static void AssertJsonEquivalent(JsonElement expected, JsonElement actual) =>
        Assert.True(JsonEquivalent(expected, actual),
            $"JSON differs.\nExpected: {expected.GetRawText()}\nActual: {actual.GetRawText()}");

    // visualization name, the visualization's own solver, expected type name, the problem's default instance
    private static readonly (string Vis, string Solver, string Type, string Instance)[] Cases = {
        ("sat3defaultvisualization", "sat3backtrackingsolver", "BooleanSatisfiability",
            "(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | !x1)"),
        ("dfavisualization", "dfasolver", "GraphLaTeX",
            "(({1,2,3},{a,b},{(1,a,2),(1,b,3),(2,a,2),(2,b,2),(3,a,2),(3,b,3)},1,{2}),a)"),
        ("cliquedefaultvisualization", "cliquebruteforce", "GraphD3",
            "(({1,2,3,4,5,6},{{4,1},{1,2},{4,3},{3,2},{2,4},{5,2},{3,5},{5,4},{3,6},{6,4},{1,6}}),4)"),
        ("minimumspanningtreevisualization", "kruskalsolver", "GraphD3",
            "({1,2,3,4},{({1,2},1),({2,3},2),({3,4},1),({1,4},2),({1,3},3)})"),
        ("vertexcoverdefaultvisualization", "vertexcoverbruteforce", "GraphD3",
            "(({a,b,c,d,e},{{a,b},{a,c},{a,e},{b,e},{c,d}}),3)"),
    };

    private static string InstanceFor(string vis) => Cases.Single(c => c.Vis == vis).Instance;

    public static TheoryData<string, string> CaseSolvers => new(Cases.Select(c => (c.Vis, c.Solver)));
    public static TheoryData<string, string> CaseTypes => new(Cases.Select(c => (c.Vis, c.Type)));

    [Fact]
    public void JsonEquivalent_IgnoresKeyOrderButNotArrayOrder() {
        JsonElement Parse(string s) => JsonDocument.Parse(s).RootElement;
        Assert.True(JsonEquivalent(Parse("{\"a\":1,\"b\":[1,2]}"), Parse("{\"b\":[1,2],\"a\":1.0}")));
        Assert.False(JsonEquivalent(Parse("{\"a\":1,\"b\":[1,2]}"), Parse("{\"a\":1,\"b\":[2,1]}")));
        Assert.False(JsonEquivalent(Parse("{\"a\":1}"), Parse("{\"a\":1,\"b\":2}")));
        Assert.False(JsonEquivalent(Parse("{\"a\":\"1\"}"), Parse("{\"a\":1}")));
    }

    [Fact]
    public void Cases_UseTheVisualizationsOwnSolver() {
        foreach (var c in Cases) {
            var vis = (IVisualization)Activator.CreateInstance(ProblemProvider.Visualizers[c.Vis])!;
            Assert.Equal(c.Solver, vis.solver.GetType().Name.ToLower());
        }
    }

    [Theory]
    [MemberData(nameof(CaseSolvers))]
    public async Task Visualize_NoSolverNoFormat_MatchesExplicitDefaults(string vis, string solver) {
        var instance = InstanceFor(vis);
        var implicitDefaults = await VisualizeOk($"visualization={vis}", instance);
        var explicitDefaults = await VisualizeOk($"visualization={vis}&solver={solver}&format=list", instance);
        Assert.Equal(JsonValueKind.Array, implicitDefaults.ValueKind);
        AssertJsonEquivalent(explicitDefaults, implicitDefaults);
    }

    [Theory]
    [MemberData(nameof(CaseSolvers))]
    public async Task Visualize_OwnSolverOnly_MatchesNoParameters(string vis, string solver) {
        var instance = InstanceFor(vis);
        var noParameters = await VisualizeOk($"visualization={vis}", instance);
        var ownSolver = await VisualizeOk($"visualization={vis}&solver={solver}", instance);
        AssertJsonEquivalent(noParameters, ownSolver);
    }

    [Theory]
    [InlineData("list")]
    [InlineData("LIST")]
    [InlineData("")]
    public async Task Visualize_ListFormat_MatchesNoFormat(string format) {
        const string vis = "sat3defaultvisualization";
        var instance = InstanceFor(vis);
        var noFormat = await VisualizeOk($"visualization={vis}", instance);
        var listFormat = await VisualizeOk($"visualization={vis}&format={format}", instance);
        AssertJsonEquivalent(noFormat, listFormat);
    }

    [Fact]
    public async Task Visualize_DifferentCompatibleSolver_UsesChosenSolver() {
        // VERTEXCOVER has five solvers. The default (brute force) and the 2-approximation return
        // different (deterministic) covers, so the solved frame differs while the initial visual does not.
        const string vis = "vertexcoverdefaultvisualization";
        var instance = InstanceFor(vis);
        var chosen = await VisualizeOk($"visualization={vis}&solver=twoapproximationvertexcover", instance);
        var again = await VisualizeOk($"visualization={vis}&solver=twoapproximationvertexcover", instance);
        AssertJsonEquivalent(chosen, again);

        var baseline = await VisualizeOk($"visualization={vis}", instance);
        Assert.Equal(baseline.GetArrayLength(), chosen.GetArrayLength());
        AssertJsonEquivalent(baseline[0], chosen[0]);
        int last = baseline.GetArrayLength() - 1;
        Assert.False(JsonEquivalent(baseline[last], chosen[last]));
    }

    [Fact]
    public async Task Visualize_UnknownSolver_Returns400() {
        var (status, body) = await Visualize("visualization=sat3defaultvisualization&solver=nosuchsolver", "(x1 | !x2 | x3)");
        Assert.Equal(HttpStatusCode.BadRequest, status);
        var json = JsonDocument.Parse(body).RootElement;
        Assert.Equal("unknown_solver", json.GetProperty("error").GetString());
        Assert.Equal("nosuchsolver", json.GetProperty("received").GetString());
    }

    [Theory]
    [InlineData("sat3defaultvisualization", "cliquebruteforce", "(x1 | !x2 | x3)")]
    // SATBruteForceSolver used to implement only the non-generic ISolver, so its problem could not be
    // determined and it was let through for any visualization.
    [InlineData("cliquedefaultvisualization", "satbruteforcesolver", "(({1,2,3},{{1,2},{2,3}}),2)")]
    [InlineData("sat3defaultvisualization", "satbruteforcesolver", "(x1 | !x2 | x3)")]
    public async Task Visualize_SolverForDifferentProblem_Returns400SolverMismatch(string vis, string solver, string instance) {
        var (status, body) = await Visualize($"visualization={vis}&solver={solver}", instance);
        Assert.Equal(HttpStatusCode.BadRequest, status);
        var json = JsonDocument.Parse(body).RootElement;
        Assert.Equal("solver_mismatch", json.GetProperty("error").GetString());
        Assert.Equal(solver, json.GetProperty("received").GetString());
        Assert.Equal(vis, json.GetProperty("visualization").GetString());
    }

    [Fact]
    public async Task Visualize_SatBruteForceOnSatVisualization_IsAccepted() {
        var (status, _) = await Visualize("visualization=satdefaultvisualization&solver=satbruteforcesolver", "(x1 | !x2) & (x2)");
        Assert.Equal(HttpStatusCode.OK, status);
    }

    [Fact]
    public void SolverMatchesVisualization_RejectsSolverWithUnknownProblem() {
        var vis = (IVisualization)Activator.CreateInstance(ProblemProvider.Visualizers["sat3defaultvisualization"])!;
        // A type with no ISolver<T> stands in for a solver whose problem cannot be determined. (A fake ISolver class
        // here would be picked up by the solver registry, which scans every loaded assembly.)
        Assert.False(ProblemProvider.SolverMatchesVisualization(typeof(object), vis));
        Assert.True(ProblemProvider.SolverMatchesVisualization(vis.solver.GetType(), vis));
    }

    // Every registered solver must declare its problem through ISolver<T>, or be some visualization's own solver
    // (which the endpoint always accepts for that visualization). Otherwise it could never be chosen.
    [Fact]
    public void EverySolver_IsGenericOrAVisualizationsOwnSolver() {
        var ownSolvers = new HashSet<Type>();
        foreach (var (_, visType) in ProblemProvider.Visualizers) {
            if (Activator.CreateInstance(visType) is IVisualization vis)
                ownSolvers.Add(vis.solver.GetType());
        }
        var untyped = ProblemProvider.Solvers.Values
            .Where(t => ProblemProvider.ProblemTypeOf(t, typeof(ISolver<>)) == null && !ownSolvers.Contains(t))
            .Select(t => t.Name)
            .ToList();
        Assert.Empty(untyped);
    }

    [Fact]
    public async Task Visualize_UnknownFormat_Returns400() {
        var (status, body) = await Visualize("visualization=sat3defaultvisualization&format=xml", "(x1 | !x2 | x3)");
        Assert.Equal(HttpStatusCode.BadRequest, status);
        var json = JsonDocument.Parse(body).RootElement;
        Assert.Equal("unknown_format", json.GetProperty("error").GetString());
        Assert.Equal("xml", json.GetProperty("received").GetString());
    }

    [Theory]
    [MemberData(nameof(CaseTypes))]
    public async Task Visualize_FramesFormat_IsListReshaped(string vis, string expectedType) {
        var instance = InstanceFor(vis);
        var root = await VisualizeOk($"visualization={vis}&format=frames", instance);
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.Equal(JsonValueKind.String, root.GetProperty("type").ValueKind);
        Assert.Equal(expectedType, root.GetProperty("type").GetString());
        Assert.Equal(JsonValueKind.Object, root.GetProperty("payload").ValueKind);
        var frames = root.GetProperty("frames");
        Assert.Equal(JsonValueKind.Array, frames.ValueKind);

        var list = await VisualizeOk($"visualization={vis}", instance);
        var reshaped = new List<JsonElement> { root.GetProperty("payload") };
        reshaped.AddRange(frames.EnumerateArray());
        Assert.Equal(list.GetArrayLength(), reshaped.Count);
        for (int i = 0; i < reshaped.Count; i++)
            AssertJsonEquivalent(list[i], reshaped[i]);
    }

    [Fact]
    public async Task Visualize_FramesFormat_DfaHasStepFrames() {
        // DFA's solver implements GetSteps, so frames holds steps plus the solved frame.
        var root = await VisualizeOk("visualization=dfavisualization&format=frames", InstanceFor("dfavisualization"));
        Assert.True(root.GetProperty("frames").GetArrayLength() >= 2);
    }

    [Theory]
    [InlineData("")]
    [InlineData("&format=frames")]
    [InlineData("&format=list")]
    public async Task Visualize_InvalidInstance_Returns400WithParseError(string extra) {
        var (status, body) = await Visualize($"visualization=sat3defaultvisualization{extra}", InvalidSat3Instance);
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("instance_parse_error", body);
    }

    [Fact]
    public async Task Visualize_InvalidInstanceWithChosenSolver_Returns400WithParseError() {
        var (status, body) = await Visualize("visualization=sat3defaultvisualization&solver=walksat&format=frames", InvalidSat3Instance);
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("instance_parse_error", body);
    }
}
