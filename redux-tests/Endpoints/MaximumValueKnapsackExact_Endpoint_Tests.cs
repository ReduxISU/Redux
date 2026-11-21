using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using API.Interfaces;
using API.Problems.NPHard.NPH_MAXIMUMVALUEKNAPSACK;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

public class MaximumValueKnapsackExact_Endpoint_Tests(AppFactory factory) : IClassFixture<AppFactory> {
    private const string Solver = "maximumvalueknapsackbruteforcesolver";
    private const string Optimality = "maximumvalueknapsackoptimalityverifier";

    [Theory]
    [InlineData(MAXIMUMVALUEKNAPSACK.DefaultInstance, "({2,3},220)")]
    [InlineData("((),0)", "({},0)")]
    [InlineData("(((0,9223372036854775806),(0,1)),0)", "({1,2},9223372036854775807)")]
    public async Task Solve_ReturnsOptimalCertificateAsExistingStringResponse(string input, string expected) {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync($"/ProblemProvider/solve?solver={Solver}", input, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expected, await response.Content.ReadFromJsonAsync<string>(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("({2,3},220)", true)]
    [InlineData("({1},60)", false)]
    [InlineData("({1,2,3},280)", false)]
    [InlineData("({2,3},219)", false)]
    public async Task Optimality_ReturnsCorrectVerdict(string certificate, bool expected) {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync($"/ProblemProvider/verify?verifier={Optimality}",
            new { ProblemInstance = MAXIMUMVALUEKNAPSACK.DefaultInstance, Certificate = certificate }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expected.ToString(), await response.Content.ReadFromJsonAsync<string>(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("({1,1},120)")]
    [InlineData("({1},60)trailing")]
    [InlineData("({1},60\0)")]
    [InlineData("({},9223372036854775808)")]
    public async Task Optimality_MalformedCertificate_ReturnsStructuredParseError(string certificate) {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync($"/ProblemProvider/verify?verifier={Optimality}",
            new { ProblemInstance = MAXIMUMVALUEKNAPSACK.DefaultInstance, Certificate = certificate }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("certificate_parse_error", json.GetProperty("error").GetString());
        Assert.Equal(certificate, json.GetProperty("received").GetString());
        Assert.Equal(new MAXIMUMVALUEKNAPSACK().certificateFormat, json.GetProperty("expected").GetString());
    }

    [Fact]
    public async Task ExhaustiveLimits_DoNotAffectParsingOrSolutionVerification() {
        using var client = factory.CreateClient();
        string instance = MaximumValueKnapsackBruteForce_Tests.UnitItems(21);
        using var parsed = await client.PostAsJsonAsync("/ProblemProvider/problemInstance?problem=MAXIMUMVALUEKNAPSACK", instance, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, parsed.StatusCode);
        var body = new { ProblemInstance = instance, Certificate = "({21},1)" };
        using var feasible = await client.PostAsJsonAsync("/ProblemProvider/verify?verifier=maximumvalueknapsacksolutionverifier", body, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, feasible.StatusCode);
        Assert.Equal("True", await feasible.Content.ReadFromJsonAsync<string>(TestContext.Current.CancellationToken));
        using var solved = await client.PostAsJsonAsync($"/ProblemProvider/solve?solver={Solver}", instance, TestContext.Current.CancellationToken);
        using var optimal = await client.PostAsJsonAsync($"/ProblemProvider/verify?verifier={Optimality}", body, TestContext.Current.CancellationToken);
        foreach (var response in new[] { solved, optimal }) {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
            Assert.Equal("exact_search_limit_exceeded", json.GetProperty("error").GetString());
            Assert.Equal(21, json.GetProperty("variableCount").GetInt32());
            Assert.Equal(20, json.GetProperty("maxVariables").GetInt32());
            Assert.Contains("Maximum-Value 0-1 Knapsack", json.GetProperty("detail").GetString());
        }
    }

    [Fact]
    public async Task MalformedInstances_ReturnParseErrorsFromBothComponents() {
        using var client = factory.CreateClient();
        using var solved = await client.PostAsJsonAsync($"/ProblemProvider/solve?solver={Solver}", "garbage", TestContext.Current.CancellationToken);
        using var verified = await client.PostAsJsonAsync($"/ProblemProvider/verify?verifier={Optimality}",
            new { ProblemInstance = "garbage", Certificate = "({},0)" }, TestContext.Current.CancellationToken);
        foreach (var response in new[] { solved, verified }) {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
            Assert.Equal("instance_parse_error", json.GetProperty("error").GetString());
        }
    }

    [Fact]
    public async Task Info_ExposesExactSolverAndPreservesDefaultSolutionVerifier() {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/ProblemProvider/info?interface=MAXIMUMVALUEKNAPSACK", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var info = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("Maximum-Value 0-1 Knapsack Exact Brute Force", info.GetProperty("defaultSolver").GetProperty("solverName").GetString());
        Assert.Equal("Maximum-Value 0-1 Knapsack Solution Verifier", info.GetProperty("defaultVerifier").GetProperty("verifierName").GetString());
        using var verifierInfo = await client.GetAsync($"/ProblemProvider/info?interface={Optimality}", TestContext.Current.CancellationToken);
        verifierInfo.EnsureSuccessStatusCode();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TimeLimit_ReturnsErrorInsteadOfCertificateOrOptimalityProof(bool verify) {
        using var limited = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { [ComputeLimits.TimeLimitKey] = "0.001" })));
        using var client = limited.CreateClient();
        string instance = MaximumValueKnapsackBruteForce_Tests.UnitItems(20);
        using var response = verify
            ? await client.PostAsJsonAsync($"/ProblemProvider/verify?verifier={Optimality}",
                new { ProblemInstance = instance, Certificate = "({},0)" }, TestContext.Current.CancellationToken)
            : await client.PostAsJsonAsync($"/ProblemProvider/solve?solver={Solver}", instance, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("time_limit", json.GetProperty("error").GetString());
    }
}
