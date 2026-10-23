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
}
