using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

public class WeightedMaxSat_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;
    private const string Instance = "(((x) & (!x)),(5,4))";
    private static readonly string LargeInstance =
        "((" + "(" + string.Join(" | ", Enumerable.Range(0, 21).Select(i => $"x{i}")) + ")" + "),(1))";
    private static readonly string LargeCertificate =
        "((" + string.Join(",", Enumerable.Range(0, 21).Select(i => $"x{i}:True")) + "),1)";

    public WeightedMaxSat_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Solve_ReturnsOptimalWeightedCertificate() {
        var content = new StringContent(JsonSerializer.Serialize(Instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync(
            "/ProblemProvider/solve?solver=weightedmaxsatbranchandboundsolver", content,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string? certificate = await response.Content.ReadFromJsonAsync<string>(TestContext.Current.CancellationToken);
        Assert.NotNull(certificate);
        Assert.EndsWith(",5)", certificate);
    }

    [Fact]
    public async Task Verify_AcceptsValidSolutionAndValue() {
        var body = new { Certificate = "((x:True),5)", ProblemInstance = Instance };
        var response = await _client.PostAsJsonAsync(
            "/ProblemProvider/verify?verifier=weightedmaxsatsolutionverifier", body,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string result = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("True", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Visualize_ReturnsWeightedSatPayload() {
        var content = new StringContent(JsonSerializer.Serialize(Instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync(
            "/ProblemProvider/visualize?visualization=weightedmaxsatvisualization", content,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement frames = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(JsonValueKind.Array, frames.ValueKind);
        JsonElement firstClause = frames[0].GetProperty("clauses")[0];
        Assert.Equal(5, firstClause.GetProperty("weight").GetInt64());
    }

    [Fact]
    public async Task Solve_ExactSolverOverLimit_ReturnsStructuredBadRequest() {
        var response = await PostInstance(
            "/ProblemProvider/solve?solver=weightedmaxsatbranchandboundsolver", LargeInstance);

        await AssertLimitResponse(response);
    }

    [Fact]
    public async Task Verify_OptimalityVerifierOverLimit_ReturnsStructuredBadRequest() {
        var body = new { Certificate = LargeCertificate, ProblemInstance = LargeInstance };
        var response = await _client.PostAsJsonAsync(
            "/ProblemProvider/verify?verifier=weightedmaxsatoptimalityverifier", body,
            TestContext.Current.CancellationToken);

        await AssertLimitResponse(response);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("weightedmaxsatbranchandboundsolver")]
    public async Task Visualize_ExactSolverOverLimit_ReturnsStructuredBadRequest(string? solver) {
        string url = "/ProblemProvider/visualize?visualization=weightedmaxsatvisualization";
        if (solver != null) url += $"&solver={solver}";

        var response = await PostInstance(url, LargeInstance);

        await AssertLimitResponse(response);
    }

    [Fact]
    public async Task Solve_ApproximationSolverOverLimit_StillSucceeds() {
        var response = await PostInstance(
            "/ProblemProvider/solve?solver=weightedmaxsatconditionalexpectationssolver", LargeInstance);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private Task<HttpResponseMessage> PostInstance(string url, string instance) {
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        return _client.PostAsync(url, content, TestContext.Current.CancellationToken);
    }

    private static async Task AssertLimitResponse(HttpResponseMessage response) {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("exact_search_limit_exceeded", body.GetProperty("error").GetString());
        Assert.Equal(21, body.GetProperty("variableCount").GetInt32());
        Assert.Equal(20, body.GetProperty("maxVariables").GetInt32());
        Assert.Contains("21", body.GetProperty("detail").GetString());
        Assert.Contains("20", body.GetProperty("detail").GetString());
    }
}
