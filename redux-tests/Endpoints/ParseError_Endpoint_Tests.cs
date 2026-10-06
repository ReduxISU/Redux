using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Malformed input must surface as a structured 400 (or be handled as a normal 200), never as an
// unhandled exception / HTTP 500 (#577). TestServer rethrows unhandled exceptions from the
// pipeline instead of producing a 500 response, so the helpers below map a thrown exception to 500.
public class ParseError_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public ParseError_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private static readonly string[] GarbageInstances = { "", "{{{", "not an instance", "{a,b}:::" };

    private static StringContent JsonBody(string value) =>
        new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

    private async Task<(HttpStatusCode Status, string Body)> PostAsync(string url, string jsonStringBody) {
        try {
            HttpResponseMessage response = await _client.PostAsync(url, JsonBody(jsonStringBody), TestContext.Current.CancellationToken);
            return (response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        } catch (Exception ex) {
            return (HttpStatusCode.InternalServerError, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private async Task<(HttpStatusCode Status, string Body)> PostVerifyAsync(string verifier, string instance, string certificate) {
        string json = JsonSerializer.Serialize(new { Certificate = certificate, ProblemInstance = instance });
        try {
            HttpResponseMessage response = await _client.PostAsync(
                $"/ProblemProvider/verify?verifier={verifier}",
                new StringContent(json, Encoding.UTF8, "application/json"),
                TestContext.Current.CancellationToken);
            return (response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        } catch (Exception ex) {
            return (HttpStatusCode.InternalServerError, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void AssertNot500(HttpStatusCode status, string body, string what) {
        Assert.True(status != HttpStatusCode.InternalServerError, $"{what} returned 500: {body}");
    }

    // -- The concrete repros from the issue ------------------------------------

    [Fact]
    public async Task Solve_MinCutStoerWagner_MalformedInstance_Returns400WithInstanceFormat() {
        var (status, body) = await PostAsync("/ProblemProvider/solve?solver=MinCutStoerWagner", "({1,2,3},{({1,2}");
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("instance_parse_error", body);
        Assert.Contains("Weighted undirected graph", body); // MINCUT.instanceFormat
    }

    [Fact]
    public async Task Visualize_DFA_MalformedInstance_Returns400WithInstanceFormat() {
        var (status, body) = await PostAsync("/ProblemProvider/visualize?visualization=DFAVisualization", "(({1,2},{a},{(1,a");
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("instance_parse_error", body);
        Assert.Contains("Format:", body); // DFA.instanceFormat
    }

    [Fact]
    public async Task Solve_Sudoku_BlankStyleBadInput_Returns400WithInstanceFormat() {
        var (status, body) = await PostAsync("/ProblemProvider/solve?solver=SudokuSolver", "blank");
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("instance_parse_error", body);
        Assert.Contains("Format:", body); // SUDOKU.instanceFormat
    }

    [Fact]
    public async Task ProblemInstance_ParseFailure_KeepsExistingResponseShape() {
        var (status, body) = await PostAsync("/ProblemProvider/problemInstance?problem=MINCUT", "garbage");
        Assert.Equal(HttpStatusCode.BadRequest, status);
        using JsonDocument doc = JsonDocument.Parse(body);
        foreach (string field in new[] { "error", "problem", "expected", "received", "detail" })
            Assert.True(doc.RootElement.TryGetProperty(field, out _), $"missing '{field}' in {body}");
        Assert.Equal("instance_parse_error", doc.RootElement.GetProperty("error").GetString());
        Assert.Equal("garbage", doc.RootElement.GetProperty("received").GetString());
    }

    // -- Never-500 sweeps over every problem / solver / verifier / reduction ---

    public static IEnumerable<object[]> AllProblemsWithGarbage() {
        foreach (var (_, type) in ProblemProvider.Problems.OrderBy(p => p.Key))
            if (!type.IsAbstract && type.GetConstructor(new[] { typeof(string) }) != null)
                foreach (string garbage in GarbageInstances)
                    yield return new object[] { type.Name, garbage };
    }

    [Theory]
    [MemberData(nameof(AllProblemsWithGarbage))]
    public async Task ProblemInstance_GarbageInstance_IsNever500(string problem, string garbage) {
        var (status, body) = await PostAsync($"/ProblemProvider/problemInstance?problem={problem}", garbage);
        AssertNot500(status, body, $"problemInstance {problem} with '{garbage}'");
    }

    public static IEnumerable<object[]> AllSolversWithGarbage() {
        foreach (var (_, type) in ProblemProvider.Solvers.OrderBy(p => p.Key))
            foreach (string garbage in GarbageInstances)
                yield return new object[] { type.Name, garbage };
    }

    [Theory]
    [MemberData(nameof(AllSolversWithGarbage))]
    public async Task Solve_GarbageInstance_IsNever500(string solver, string garbage) {
        var (status, body) = await PostAsync($"/ProblemProvider/solve?solver={solver}", garbage);
        AssertNot500(status, body, $"solve {solver} with '{garbage}'");
    }

    public static IEnumerable<object[]> AllVisualizationsWithGarbage() {
        foreach (var (_, type) in ProblemProvider.Visualizers.OrderBy(p => p.Key))
            foreach (string garbage in GarbageInstances)
                yield return new object[] { type.Name, garbage };
    }

    [Theory]
    [MemberData(nameof(AllVisualizationsWithGarbage))]
    public async Task Visualize_GarbageInstance_IsNever500(string visualization, string garbage) {
        var (status, body) = await PostAsync($"/ProblemProvider/visualize?visualization={visualization}", garbage);
        AssertNot500(status, body, $"visualize {visualization} with '{garbage}'");
    }

    // Each verifier's default instance comes from the problem it verifies (IVerifier<T>).
    public static IEnumerable<object[]> AllVerifiersWithGarbage() {
        foreach (var (_, type) in ProblemProvider.Verifiers.OrderBy(p => p.Key)) {
            Type? problemType = type.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IVerifier<>))
                ?.GetGenericArguments()[0];
            if (problemType == null) continue;
            string defaultInstance = ((IProblem)Activator.CreateInstance(problemType)!).defaultInstance;
            foreach (string garbage in GarbageInstances)
                yield return new object[] { type.Name, defaultInstance, garbage };
        }
    }

    [Theory]
    [MemberData(nameof(AllVerifiersWithGarbage))]
    public async Task Verify_GarbageCertificate_IsNever500(string verifier, string defaultInstance, string garbage) {
        var (status, body) = await PostVerifyAsync(verifier, defaultInstance, garbage);
        AssertNot500(status, body, $"verify {verifier} with certificate '{garbage}'");
    }

    [Theory]
    [MemberData(nameof(AllVerifiersWithGarbage))]
    public async Task Verify_GarbageInstance_IsNever500(string verifier, string defaultInstance, string garbage) {
        _ = defaultInstance; // the garbage string is used as the instance here
        var (status, body) = await PostVerifyAsync(verifier, garbage, "{a}");
        AssertNot500(status, body, $"verify {verifier} with instance '{garbage}'");
    }

    public static IEnumerable<object[]> AllReductionsWithGarbage() {
        foreach (var (_, type) in ProblemProvider.Reductions.OrderBy(p => p.Key)) {
            Type? fromType = type.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IReduction<,>))
                ?.GetGenericArguments()[0];
            if (fromType == null) continue;
            string defaultInstance = ((IProblem)Activator.CreateInstance(fromType)!).defaultInstance;
            foreach (string garbage in GarbageInstances)
                yield return new object[] { type.Name, defaultInstance, garbage };
        }
    }

    [Theory]
    [MemberData(nameof(AllReductionsWithGarbage))]
    public async Task Reduce_GarbageInstance_IsNever500(string reduction, string defaultInstance, string garbage) {
        _ = defaultInstance;
        var (status, body) = await PostAsync($"/ProblemProvider/reduce?reduction={reduction}", garbage);
        AssertNot500(status, body, $"reduce {reduction} with '{garbage}'");
    }

    [Theory]
    [MemberData(nameof(AllReductionsWithGarbage))]
    public async Task Gadgets_GarbageInstance_IsNever500(string reduction, string defaultInstance, string garbage) {
        _ = defaultInstance;
        var (status, body) = await PostAsync($"/ProblemProvider/gadgets?reduction={reduction}", garbage);
        AssertNot500(status, body, $"gadgets {reduction} with '{garbage}'");
    }

    // mapSolution: valid default instance for the reduction's FROM problem, garbage certificate.
    [Theory]
    [MemberData(nameof(AllReductionsWithGarbage))]
    public async Task MapSolution_GarbageCertificate_IsNever500(string reduction, string defaultInstance, string garbage) {
        var (status, body) = await PostAsync(
            $"/ProblemProvider/mapSolution?reduction={reduction}&solution={Uri.EscapeDataString(garbage)}", defaultInstance);
        AssertNot500(status, body, $"mapSolution {reduction} with certificate '{garbage}'");
    }
}
