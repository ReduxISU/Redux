using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Tests for /ProblemProvider/visualize: the optional `solver` and `format` parameters, plus byte-for-byte
// backward compatibility of the default (flat list) response. Assertions are on the actual JSON bodies.
// Snapshots in Snapshots/ were captured from the endpoint before the `solver`/`format` parameters existed,
// using each problem's default instance (<name>.instance.txt).
public class Visualize_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public Visualize_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private const string InvalidSat3Instance = "(1 | 2 | 3)";

    private static string SnapshotPath(string file) =>
        Path.Combine(AppContext.BaseDirectory, "Endpoints", "Snapshots", file);

    // Normalize line endings so a CRLF checkout (autocrlf) still compares equal to the LF response.
    private static string ReadSnapshot(string file) =>
        File.ReadAllText(SnapshotPath(file)).Replace("\r\n", "\n");

    private static string Compact(JsonElement e) =>
        JsonSerializer.Serialize(e, new JsonSerializerOptions { WriteIndented = false });

    private async Task<(HttpStatusCode Status, string Body)> Visualize(string query, string instance) {
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/visualize?{query}", content, TestContext.Current.CancellationToken);
        var body = (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Replace("\r\n", "\n");
        return (response.StatusCode, body);
    }

    // visualization name, the visualization's own default solver, expected type name
    private static readonly (string Vis, string Solver, string Type)[] SnapshotCases = {
        ("sat3defaultvisualization", "sat3backtrackingsolver", "BooleanSatisfiability"),
        ("dfavisualization", "dfasolver", "GraphLaTeX"),
        ("cliquedefaultvisualization", "cliquebruteforce", "GraphD3"),
        ("minimumspanningtreevisualization", "kruskalsolver", "GraphD3"),
        ("vertexcoverdefaultvisualization", "vertexcoverbruteforce", "GraphD3"),
    };

    public static TheoryData<string> SnapshotNames => new(SnapshotCases.Select(c => c.Vis));
    public static TheoryData<string, string> SnapshotSolvers => new(SnapshotCases.Select(c => (c.Vis, c.Solver)));
    public static TheoryData<string, string> SnapshotTypes => new(SnapshotCases.Select(c => (c.Vis, c.Type)));

    [Theory]
    [MemberData(nameof(SnapshotNames))]
    public async Task Visualize_NoSolverNoFormat_MatchesSnapshotByteForByte(string vis) {
        var instance = ReadSnapshot($"{vis}.instance.txt");
        var (status, body) = await Visualize($"visualization={vis}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(ReadSnapshot($"{vis}.json"), body);
    }

    [Theory]
    [MemberData(nameof(SnapshotSolvers))]
    public async Task Visualize_OwnDefaultSolver_MatchesSnapshotByteForByte(string vis, string solver) {
        var instance = ReadSnapshot($"{vis}.instance.txt");
        var (status, body) = await Visualize($"visualization={vis}&solver={solver}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(ReadSnapshot($"{vis}.json"), body);
    }

    [Theory]
    [InlineData("list")]
    [InlineData("LIST")]
    [InlineData("")]
    public async Task Visualize_ListFormat_MatchesSnapshot(string format) {
        var instance = ReadSnapshot("sat3defaultvisualization.instance.txt");
        var (status, body) = await Visualize($"visualization=sat3defaultvisualization&format={format}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(ReadSnapshot("sat3defaultvisualization.json"), body);
    }

    [Fact]
    public async Task Visualize_DifferentCompatibleSolver_UsesChosenSolver() {
        // VERTEXCOVER has five solvers. The default (brute force) and the 2-approximation return
        // different (deterministic) covers, so the solved frame differs while the initial visual does not.
        const string vis = "vertexcoverdefaultvisualization";
        var instance = ReadSnapshot($"{vis}.instance.txt");
        var (status, body) = await Visualize($"visualization={vis}&solver=twoapproximationvertexcover", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var (_, again) = await Visualize($"visualization={vis}&solver=twoapproximationvertexcover", instance);
        Assert.Equal(body, again);

        var baseline = JsonDocument.Parse(ReadSnapshot($"{vis}.json")).RootElement;
        var chosen = JsonDocument.Parse(body).RootElement;
        Assert.Equal(baseline.GetArrayLength(), chosen.GetArrayLength());
        Assert.Equal(baseline[0].GetRawText(), chosen[0].GetRawText());
        Assert.NotEqual(baseline[baseline.GetArrayLength() - 1].GetRawText(), chosen[chosen.GetArrayLength() - 1].GetRawText());
        Assert.NotEqual(ReadSnapshot($"{vis}.json"), body);
    }

    [Fact]
    public async Task Visualize_UnknownSolver_Returns400() {
        var (status, body) = await Visualize("visualization=sat3defaultvisualization&solver=nosuchsolver", "(x1 | !x2 | x3)");
        Assert.Equal(HttpStatusCode.BadRequest, status);
        var json = JsonDocument.Parse(body).RootElement;
        Assert.Equal("unknown_solver", json.GetProperty("error").GetString());
        Assert.Equal("nosuchsolver", json.GetProperty("received").GetString());
    }

    [Fact]
    public async Task Visualize_SolverForDifferentProblem_Returns400SolverMismatch() {
        var (status, body) = await Visualize("visualization=sat3defaultvisualization&solver=cliquebruteforce", "(x1 | !x2 | x3)");
        Assert.Equal(HttpStatusCode.BadRequest, status);
        var json = JsonDocument.Parse(body).RootElement;
        Assert.Equal("solver_mismatch", json.GetProperty("error").GetString());
        Assert.Equal("cliquebruteforce", json.GetProperty("received").GetString());
        Assert.Equal("sat3defaultvisualization", json.GetProperty("visualization").GetString());
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
    [MemberData(nameof(SnapshotTypes))]
    public async Task Visualize_FramesFormat_IsListReshaped(string vis, string expectedType) {
        var instance = ReadSnapshot($"{vis}.instance.txt");
        var (status, body) = await Visualize($"visualization={vis}&format=frames", instance);
        Assert.Equal(HttpStatusCode.OK, status);

        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.Equal(JsonValueKind.String, root.GetProperty("type").ValueKind);
        Assert.Equal(expectedType, root.GetProperty("type").GetString());
        Assert.Equal(JsonValueKind.Object, root.GetProperty("payload").ValueKind);
        var frames = root.GetProperty("frames");
        Assert.Equal(JsonValueKind.Array, frames.ValueKind);

        var list = JsonDocument.Parse(ReadSnapshot($"{vis}.json")).RootElement;
        // Nesting changes the indentation of each item, so compare compacted text (same properties, same order).
        var reshaped = new List<string> { Compact(root.GetProperty("payload")) };
        reshaped.AddRange(frames.EnumerateArray().Select(Compact));
        Assert.Equal(list.EnumerateArray().Select(Compact).ToList(), reshaped);
    }

    [Fact]
    public async Task Visualize_FramesFormat_DfaHasStepFrames() {
        // DFA's solver implements GetSteps, so frames holds steps plus the solved frame.
        var instance = ReadSnapshot("dfavisualization.instance.txt");
        var (_, body) = await Visualize("visualization=dfavisualization&format=frames", instance);
        var frames = JsonDocument.Parse(body).RootElement.GetProperty("frames");
        Assert.True(frames.GetArrayLength() >= 2);
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
