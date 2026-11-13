using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using API.Problems.NPHard.NPH_WEIGHTEDMAX3SAT;
using API.Problems.NPHard.NPH_WEIGHTEDMAX3SAT.Verifiers;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

public class WeightedMax3Sat_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;
    private const string Instance = "(((x) & (!x)),(5,4))";
    private static readonly string LargeInstance =
        "((" + string.Join(" & ", Enumerable.Range(0, 21).Select(i => $"(x{i})")) + "),(" +
        string.Join(",", Enumerable.Repeat(1, 21)) + "))";
    private static readonly string LargeCertificate =
        "((" + string.Join(",", Enumerable.Range(0, 21).Select(i => $"x{i}:True")) + "),21)";

    public WeightedMax3Sat_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("ALL_ProblemsRefactor")]
    [InlineData("NPHard_ProblemsRefactor")]
    public async Task Navigation_ListsNewProblem(string endpoint) {
        var response = await _client.GetAsync($"/Navigation/{endpoint}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var names = await response.Content.ReadFromJsonAsync<string[]>(TestContext.Current.CancellationToken);
        Assert.NotNull(names);
        Assert.Contains("WEIGHTEDMAX3SAT", names);
    }

    [Theory]
    [InlineData("Problem_SolversRefactor", "WeightedMax3SatBruteForceSolver")]
    [InlineData("Problem_SolversRefactor", "WeightedMax3SatBranchAndBoundSolver")]
    [InlineData("Problem_SolversRefactor", "WeightedMax3SatConditionalExpectationsSolver")]
    [InlineData("Problem_VerifiersRefactor", "WeightedMax3SatSolutionVerifier")]
    [InlineData("Problem_VerifiersRefactor", "WeightedMax3SatOptimalityVerifier")]
    [InlineData("Problem_VisualizationsRefactor", "WeightedMax3SatVisualization")]
    public async Task Navigation_DiscoversTypedComponents(string endpoint, string component) {
        var response = await _client.GetAsync(
            $"/Navigation/{endpoint}?chosenProblem=WEIGHTEDMAX3SAT", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var names = await response.Content.ReadFromJsonAsync<string[]>(TestContext.Current.CancellationToken);
        Assert.NotNull(names);
        Assert.Contains(component, names);
        Assert.DoesNotContain(names, name => name.StartsWith("WeightedMaxSat", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Info_PublishesMetadataAndAttributedDefaults() {
        var response = await _client.GetAsync("/ProblemProvider/info?interface=WEIGHTEDMAX3SAT",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("Weighted Max-3SAT", body.GetProperty("problemName").GetString());
        Assert.Contains("1–3 literal occurrences", body.GetProperty("instanceFormat").GetString());
        Assert.Equal("Weighted Max-3SAT Exact Brute Force",
            body.GetProperty("defaultSolver").GetProperty("solverName").GetString());
        Assert.Equal("Weighted Max-3SAT Solution Verifier",
            body.GetProperty("defaultVerifier").GetProperty("verifierName").GetString());
        Assert.Equal("Weighted Max-3SAT Visualization",
            body.GetProperty("defaultVisualization").GetProperty("visualizationName").GetString());
    }

    [Theory]
    [InlineData("weightedmax3satbruteforcesolver")]
    [InlineData("weightedmax3satbranchandboundsolver")]
    [InlineData("weightedmax3satconditionalexpectationssolver")]
    public async Task Solve_ReturnsVerifiableOptimalCertificateForKnownInstance(string solver) {
        var response = await PostInstance($"/ProblemProvider/solve?solver={solver}", Instance);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string? certificate = await response.Content.ReadFromJsonAsync<string>(TestContext.Current.CancellationToken);
        Assert.Equal("((x:True),5)", certificate);
        Assert.True(new WeightedMax3SatSolutionVerifier().verify(new WEIGHTEDMAX3SAT(Instance), certificate!));
    }

    [Theory]
    [InlineData("/ProblemProvider/problemInstance?problem=WEIGHTEDMAX3SAT")]
    [InlineData("/ProblemProvider/solve?solver=weightedmax3satbruteforcesolver")]
    [InlineData("/ProblemProvider/solve?solver=weightedmax3satbranchandboundsolver")]
    [InlineData("/ProblemProvider/solve?solver=weightedmax3satconditionalexpectationssolver")]
    [InlineData("/ProblemProvider/visualize?visualization=weightedmax3satvisualization")]
    public async Task MalformedInstance_ReturnsOuterProblemAndFormat(string path) {
        const string input = "(((x | x | x | x)),(1))";
        var response = await PostInstance(path, input);
        await AssertParseResponse(response, "instance_parse_error", input, new WEIGHTEDMAX3SAT().instanceFormat);
    }

    [Theory]
    [InlineData("weightedmax3satsolutionverifier")]
    [InlineData("weightedmax3satoptimalityverifier")]
    public async Task Verify_MalformedCertificate_ReturnsOuterProblemAndFormat(string verifier) {
        var response = await Verify(verifier, Instance, "((x:yes),5)");
        await AssertParseResponse(response, "certificate_parse_error", "((x:yes),5)",
            new WEIGHTEDMAX3SAT().certificateFormat);
    }

    [Theory]
    [InlineData("weightedmax3satsolutionverifier", "((x:True),5)", "True")]
    [InlineData("weightedmax3satoptimalityverifier", "((x:True),5)", "True")]
    [InlineData("weightedmax3satsolutionverifier", "((x:False),4)", "True")]
    [InlineData("weightedmax3satoptimalityverifier", "((x:False),4)", "False")]
    [InlineData("weightedmax3satsolutionverifier", "((x:True),4)", "False")]
    [InlineData("weightedmax3satoptimalityverifier", "((),0)", "False")]
    public async Task Verify_ReturnsVerdictForReadableCertificates(string verifier, string certificate, string expected) {
        var response = await Verify(verifier, Instance, certificate);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expected, await response.Content.ReadFromJsonAsync<string>(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("/ProblemProvider/solve?solver=weightedmax3satbruteforcesolver")]
    [InlineData("/ProblemProvider/solve?solver=weightedmax3satbranchandboundsolver")]
    [InlineData("/ProblemProvider/visualize?visualization=weightedmax3satvisualization")]
    [InlineData("/ProblemProvider/visualize?visualization=weightedmax3satvisualization&solver=weightedmax3satbranchandboundsolver")]
    public async Task ExactSearch_OverLimit_ReturnsStructuredErrorWithOuterIdentity(string path) {
        var response = await PostInstance(path, LargeInstance);
        await AssertLimitResponse(response);
    }

    [Fact]
    public async Task Verify_OptimalityOverLimit_ReturnsStructuredError() {
        var response = await Verify("weightedmax3satoptimalityverifier", LargeInstance, LargeCertificate);
        await AssertLimitResponse(response);
    }

    [Fact]
    public async Task ApproximationAndSolutionVerification_AcceptTwentyOneVariables() {
        var response = await PostInstance(
            "/ProblemProvider/solve?solver=weightedmax3satconditionalexpectationssolver", LargeInstance);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string? certificate = await response.Content.ReadFromJsonAsync<string>(TestContext.Current.CancellationToken);
        Assert.NotNull(certificate);
        var verification = await Verify("weightedmax3satsolutionverifier", LargeInstance, certificate);
        Assert.Equal(HttpStatusCode.OK, verification.StatusCode);
        Assert.Equal("True", await verification.Content.ReadFromJsonAsync<string>(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Visualize_ReturnsWeightedSatFramesWithSolutionHighlights() {
        var response = await PostInstance(
            "/ProblemProvider/visualize?visualization=weightedmax3satvisualization", Instance);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement frames = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(JsonValueKind.Array, frames.ValueKind);
        Assert.Equal(2, frames.GetArrayLength());
        Assert.Equal("sat", frames[0].GetProperty("kind").GetString());
        Assert.Equal(5, frames[0].GetProperty("clauses")[0].GetProperty("weight").GetInt64());
        Assert.Equal(4, frames[0].GetProperty("clauses")[1].GetProperty("weight").GetInt64());
        Assert.Equal("Solution", frames[1].GetProperty("clauses")[0].GetProperty("literals")[0].GetProperty("color").GetString());
        Assert.Equal("Background", frames[1].GetProperty("clauses")[1].GetProperty("literals")[0].GetProperty("color").GetString());
    }

    private Task<HttpResponseMessage> PostInstance(string path, string instance) =>
        _client.PostAsync(path, new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);

    private Task<HttpResponseMessage> Verify(string verifier, string instance, string certificate) =>
        _client.PostAsJsonAsync($"/ProblemProvider/verify?verifier={verifier}",
            new { Certificate = certificate, ProblemInstance = instance }, TestContext.Current.CancellationToken);

    private static async Task AssertParseResponse(HttpResponseMessage response, string error, string received, string expected) {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(error, body.GetProperty("error").GetString());
        Assert.Equal("Weighted Max-3SAT", body.GetProperty("problem").GetString());
        Assert.Equal(received, body.GetProperty("received").GetString());
        Assert.Equal(expected, body.GetProperty("expected").GetString());
    }

    private static async Task AssertLimitResponse(HttpResponseMessage response) {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("exact_search_limit_exceeded", body.GetProperty("error").GetString());
        Assert.Equal(21, body.GetProperty("variableCount").GetInt32());
        Assert.Equal(20, body.GetProperty("maxVariables").GetInt32());
        Assert.Equal("Weighted Max-3SAT exact search supports at most 20 variables; received 21.",
            body.GetProperty("detail").GetString());
    }
}
