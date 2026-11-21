using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using API.Interfaces;
using API.Problems.NPHard.NPH_MAXIMUMVALUEKNAPSACK;
using API.Problems.NPHard.NPH_MAXIMUMVALUEKNAPSACK.Verifiers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

public class MaximumValueKnapsackModifiedGreedy_Endpoint_Tests(AppFactory factory) : IClassFixture<AppFactory> {
    private const string Solver = "maximumvalueknapsackmodifiedgreedysolver";

    [Theory]
    [InlineData(MAXIMUMVALUEKNAPSACK.DefaultInstance, "({1,2},160)")]
    [InlineData("((),0)", "({},0)")]
    [InlineData("(((0,7),(0,0)),0)", "({1},7)")]
    [InlineData("(((1,2),(100,100)),100)", "({2},100)")]
    [InlineData("(((0,9223372036854775806),(0,1)),0)", "({1,2},9223372036854775807)")]
    public async Task Solve_ReturnsExpectedFeasibleStringCertificate(string input, string expected) {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync($"/ProblemProvider/solve?solver={Solver}", input, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string certificate = (await response.Content.ReadFromJsonAsync<string>(TestContext.Current.CancellationToken))!;
        Assert.Equal(expected, certificate);
        Assert.True(new MaximumValueKnapsackSolutionVerifier().verify(new MAXIMUMVALUEKNAPSACK(input), certificate));
    }

    [Fact]
    public async Task SuboptimalCertificate_PassesSolutionVerifierAndFailsOptimalityVerifier() {
        using var client = factory.CreateClient();
        var body = new { ProblemInstance = MAXIMUMVALUEKNAPSACK.DefaultInstance, Certificate = "({1,2},160)" };
        foreach (var (verifier, expected) in new[] {
            ("maximumvalueknapsacksolutionverifier", "True"), ("maximumvalueknapsackoptimalityverifier", "False") }) {
            using var response = await client.PostAsJsonAsync($"/ProblemProvider/verify?verifier={verifier}", body, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(expected, await response.Content.ReadFromJsonAsync<string>(TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task MoreThanTwentyItems_SucceedsWithoutBruteForceLimit() {
        using var client = factory.CreateClient();
        string input = MaximumValueKnapsackBruteForce_Tests.UnitItems(40);
        using var response = await client.PostAsJsonAsync($"/ProblemProvider/solve?solver={Solver}", input, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string certificate = (await response.Content.ReadFromJsonAsync<string>(TestContext.Current.CancellationToken))!;
        var problem = new MAXIMUMVALUEKNAPSACK(input);
        Assert.True(new MaximumValueKnapsackSolutionVerifier().verify(problem, certificate));
        Assert.Equal(40, MaximumValueKnapsackSolutionVerifier.ParseCertificate(problem, certificate).Value);
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("(((1,2)),3)trailing")]
    [InlineData("(((1,2\0)),3)")]
    [InlineData("(((0,9223372036854775807),(0,1)),0)")]
    public async Task MalformedInstance_ReturnsExistingStructuredParseError(string input) {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync($"/ProblemProvider/solve?solver={Solver}", input, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("instance_parse_error", json.GetProperty("error").GetString());
        Assert.Equal(input, json.GetProperty("received").GetString());
        Assert.Equal(new MAXIMUMVALUEKNAPSACK().instanceFormat, json.GetProperty("expected").GetString());
    }

    [Fact]
    public async Task Info_ExposesApproximationGuaranteeAndPreservesDefaults() {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/ProblemProvider/info?interface={Solver}", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var info = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Contains("ALG >= OPT / 2", info.GetProperty("solverDefinition").GetString());
        Assert.Equal(new[] { "Greedy", "Approximation" }, info.GetProperty("solverTypes").EnumerateArray().Select(t => t.GetString()));
        using var problemResponse = await client.GetAsync("/ProblemProvider/info?interface=MAXIMUMVALUEKNAPSACK", TestContext.Current.CancellationToken);
        problemResponse.EnsureSuccessStatusCode();
        var problem = await problemResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("Maximum-Value 0-1 Knapsack Exact Brute Force", problem.GetProperty("defaultSolver").GetProperty("solverName").GetString());
        Assert.Equal("Maximum-Value 0-1 Knapsack Solution Verifier", problem.GetProperty("defaultVerifier").GetProperty("verifierName").GetString());
    }

    [Fact]
    public async Task Timeout_ReturnsTimeLimitErrorInsteadOfPartialCandidate() {
        using var limited = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { [ComputeLimits.TimeLimitKey] = "0.001" })));
        using var client = limited.CreateClient();
        using var response = await client.PostAsJsonAsync($"/ProblemProvider/solve?solver={Solver}",
            MaximumValueKnapsackBruteForce_Tests.UnitItems(20000), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("time_limit", json.GetProperty("error").GetString());
    }
}
