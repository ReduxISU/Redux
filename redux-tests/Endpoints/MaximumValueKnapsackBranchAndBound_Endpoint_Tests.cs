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

public class MaximumValueKnapsackBranchAndBound_Endpoint_Tests(AppFactory factory) : IClassFixture<AppFactory> {
    private const string Solver = "maximumvalueknapsackbranchandboundsolver";

    [Theory]
    [InlineData(MAXIMUMVALUEKNAPSACK.DefaultInstance, 220)]
    [InlineData("((),0)", 0)]
    [InlineData("(((0,7),(0,0)),0)", 7)]
    [InlineData("(((2,3),(3,4),(3,4)),6)", 8)]
    [InlineData("(((0,9223372036854775806),(0,1)),0)", long.MaxValue)]
    public async Task Solve_ReturnsOptimalFeasibleStringCertificate(string input, long optimum) {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync($"/ProblemProvider/solve?solver={Solver}", input, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string certificate = (await response.Content.ReadFromJsonAsync<string>(TestContext.Current.CancellationToken))!;
        var problem = new MAXIMUMVALUEKNAPSACK(input);
        Assert.True(new MaximumValueKnapsackSolutionVerifier().verify(problem, certificate));
        Assert.True(new MaximumValueKnapsackOptimalityVerifier().verify(problem, certificate));
        Assert.Equal(optimum, MaximumValueKnapsackSolutionVerifier.ParseCertificate(problem, certificate).Value);
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
    public async Task Info_ExposesSolverAndKeepsBruteForceDefault() {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/ProblemProvider/info?interface={Solver}", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var info = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("Maximum-Value 0-1 Knapsack Exact Branch and Bound", info.GetProperty("solverName").GetString());
        Assert.Contains(info.GetProperty("solverTypes").EnumerateArray(), t => t.GetString() == "Backtracking");
        using var problemResponse = await client.GetAsync("/ProblemProvider/info?interface=MAXIMUMVALUEKNAPSACK", TestContext.Current.CancellationToken);
        problemResponse.EnsureSuccessStatusCode();
        var problem = await problemResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("Maximum-Value 0-1 Knapsack Exact Brute Force", problem.GetProperty("defaultSolver").GetProperty("solverName").GetString());
    }

    [Fact]
    public async Task Timeout_ReturnsTimeLimitErrorRatherThanAnUnprovenIncumbent() {
        using var limited = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { [ComputeLimits.TimeLimitKey] = "0.001" })));
        using var client = limited.CreateClient();
        using var response = await client.PostAsJsonAsync($"/ProblemProvider/solve?solver={Solver}",
            MaximumValueKnapsackBranchAndBound_Tests.HardInstance(), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("time_limit", json.GetProperty("error").GetString());
    }
}
