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

public class MaximumValueKnapsack_Endpoint_Tests(AppFactory factory) : IClassFixture<AppFactory> {
    private const string BruteForceSolver = "maximumvalueknapsackbruteforcesolver";
    private const string BranchAndBoundSolver = "maximumvalueknapsackbranchandboundsolver";
    private const string ModifiedGreedySolver = "maximumvalueknapsackmodifiedgreedysolver";
    private const string Optimality = "maximumvalueknapsackoptimalityverifier";

    // --- Problem discovery and metadata ---

    [Fact]
    public async Task Definition_MetadataAndNavigation_ExposeOptimizationProblem() {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/ProblemProvider/info?interface=MAXIMUMVALUEKNAPSACK", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var info = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("NPHard", info.GetProperty("complexityClass").GetString());
        Assert.Contains(info.GetProperty("problemVariants").EnumerateArray(), v => v.GetString() == "Optimization");
        Assert.Contains(info.GetProperty("problemTypes").EnumerateArray(), v => v.GetString() == "MathematicalProgramming");
        Assert.Equal(MAXIMUMVALUEKNAPSACK.DefaultInstance, info.GetProperty("defaultInstance").GetString());
        using var navigation = await client.GetAsync("/Navigation/NPHard_ProblemsRefactor", TestContext.Current.CancellationToken);
        navigation.EnsureSuccessStatusCode();
        var names = await navigation.Content.ReadFromJsonAsync<string[]>(TestContext.Current.CancellationToken);
        Assert.Contains("MAXIMUMVALUEKNAPSACK", names!);
    }

    [Fact]
    public async Task Solution_Info_ExposesRegisteredDefaultSolutionVerifier() {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/ProblemProvider/info?interface=MAXIMUMVALUEKNAPSACK", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var info = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var verifier = info.GetProperty("defaultVerifier");
        Assert.Equal(new MaximumValueKnapsackSolutionVerifier().verifierName, verifier.GetProperty("verifierName").GetString());
        Assert.Contains("does not check optimality", verifier.GetProperty("verifierDefinition").GetString());
    }

    [Fact]
    public async Task Exact_Info_ExposesExactSolverAndPreservesDefaultSolutionVerifier() {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/ProblemProvider/info?interface=MAXIMUMVALUEKNAPSACK", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var info = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("Maximum-Value 0-1 Knapsack Exact Brute Force", info.GetProperty("defaultSolver").GetProperty("solverName").GetString());
        Assert.Equal("Maximum-Value 0-1 Knapsack Solution Verifier", info.GetProperty("defaultVerifier").GetProperty("verifierName").GetString());
        using var verifierInfo = await client.GetAsync($"/ProblemProvider/info?interface={Optimality}", TestContext.Current.CancellationToken);
        verifierInfo.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task BranchAndBound_Info_ExposesSolverAndKeepsBruteForceDefault() {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/ProblemProvider/info?interface={BranchAndBoundSolver}", TestContext.Current.CancellationToken);
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
    public async Task ModifiedGreedy_Info_ExposesApproximationGuaranteeAndPreservesDefaults() {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/ProblemProvider/info?interface={ModifiedGreedySolver}", TestContext.Current.CancellationToken);
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

    // --- Instance parsing ---

    [Theory]
    [InlineData("(((2,3),(2,3)),4)")]
    [InlineData("((),0)")]
    [InlineData("(((0,9223372036854775807)),0)")]
    public async Task Definition_ProblemInstance_ParsesValidInputs(string input) {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/ProblemProvider/problemInstance?problem=MAXIMUMVALUEKNAPSACK", input,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(input, json.GetProperty("instance").GetString());
        Assert.Equal(new MAXIMUMVALUEKNAPSACK(input).items.Count, json.GetProperty("items").GetArrayLength());
    }

    // --- Solution verification ---

    [Theory]
    [InlineData("({2,3},220)", true)]
    [InlineData("({1},60)", true)]
    [InlineData("({},0)", true)]
    [InlineData("({1,2,3},280)", false)]
    [InlineData("({2,3},221)", false)]
    [InlineData("({0},0)", false)]
    [InlineData("({9223372036854775807},0)", false)]
    public async Task Solution_Verify_ReturnsFeasibilityAndValueVerdict(string certificate, bool expected) {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/ProblemProvider/verify?verifier=maximumvalueknapsacksolutionverifier",
            new { ProblemInstance = MAXIMUMVALUEKNAPSACK.DefaultInstance, Certificate = certificate }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expected.ToString(), await response.Content.ReadFromJsonAsync<string>(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("((),0)", "({},0)", true)]
    [InlineData("(((0,7),(0,0)),0)", "({2,1},7)", true)]
    [InlineData("(((2,3),(2,3)),4)", "({1,2},6)", true)]
    [InlineData("(((0,9223372036854775806),(0,1)),0)", "({1,2},9223372036854775807)", true)]
    [InlineData("(((9223372036854775807,1),(9223372036854775807,2)),9223372036854775807)", "({1,2},3)", false)]
    public async Task Solution_Verify_BoundaryInstances_ReturnExpectedVerdicts(string instance, string certificate, bool expected) {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/ProblemProvider/verify?verifier=maximumvalueknapsacksolutionverifier",
            new { ProblemInstance = instance, Certificate = certificate }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expected.ToString(), await response.Content.ReadFromJsonAsync<string>(TestContext.Current.CancellationToken));
    }

    // --- Optimality verification ---

    [Theory]
    [InlineData("({2,3},220)", true)]
    [InlineData("({1},60)", false)]
    [InlineData("({1,2,3},280)", false)]
    [InlineData("({2,3},219)", false)]
    public async Task Exact_Optimality_ReturnsCorrectVerdict(string certificate, bool expected) {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync($"/ProblemProvider/verify?verifier={Optimality}",
            new { ProblemInstance = MAXIMUMVALUEKNAPSACK.DefaultInstance, Certificate = certificate }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expected.ToString(), await response.Content.ReadFromJsonAsync<string>(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ModifiedGreedy_SuboptimalCertificate_PassesSolutionVerifierAndFailsOptimalityVerifier() {
        using var client = factory.CreateClient();
        var body = new { ProblemInstance = MAXIMUMVALUEKNAPSACK.DefaultInstance, Certificate = "({1,2},160)" };
        foreach (var (verifier, expected) in new[] {
            ("maximumvalueknapsacksolutionverifier", "True"), ("maximumvalueknapsackoptimalityverifier", "False") }) {
            using var response = await client.PostAsJsonAsync($"/ProblemProvider/verify?verifier={verifier}", body, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(expected, await response.Content.ReadFromJsonAsync<string>(TestContext.Current.CancellationToken));
        }
    }

    // --- Brute-force solving ---

    [Theory]
    [InlineData(MAXIMUMVALUEKNAPSACK.DefaultInstance, "({2,3},220)")]
    [InlineData("((),0)", "({},0)")]
    [InlineData("(((0,9223372036854775806),(0,1)),0)", "({1,2},9223372036854775807)")]
    public async Task Exact_Solve_ReturnsOptimalCertificateAsExistingStringResponse(string input, string expected) {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync($"/ProblemProvider/solve?solver={BruteForceSolver}", input, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expected, await response.Content.ReadFromJsonAsync<string>(TestContext.Current.CancellationToken));
    }

    // --- Branch-and-bound solving ---

    [Theory]
    [InlineData(MAXIMUMVALUEKNAPSACK.DefaultInstance, 220)]
    [InlineData("((),0)", 0)]
    [InlineData("(((0,7),(0,0)),0)", 7)]
    [InlineData("(((2,3),(3,4),(3,4)),6)", 8)]
    [InlineData("(((0,9223372036854775806),(0,1)),0)", long.MaxValue)]
    public async Task BranchAndBound_Solve_ReturnsOptimalFeasibleStringCertificate(string input, long optimum) {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync($"/ProblemProvider/solve?solver={BranchAndBoundSolver}", input, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string certificate = (await response.Content.ReadFromJsonAsync<string>(TestContext.Current.CancellationToken))!;
        var problem = new MAXIMUMVALUEKNAPSACK(input);
        Assert.True(new MaximumValueKnapsackSolutionVerifier().verify(problem, certificate));
        Assert.True(new MaximumValueKnapsackOptimalityVerifier().verify(problem, certificate));
        Assert.Equal(optimum, MaximumValueKnapsackSolutionVerifier.ParseCertificate(problem, certificate).Value);
    }

    // --- Modified greedy approximation ---

    [Theory]
    [InlineData(MAXIMUMVALUEKNAPSACK.DefaultInstance, "({1,2},160)")]
    [InlineData("((),0)", "({},0)")]
    [InlineData("(((0,7),(0,0)),0)", "({1},7)")]
    [InlineData("(((1,2),(100,100)),100)", "({2},100)")]
    [InlineData("(((0,9223372036854775806),(0,1)),0)", "({1,2},9223372036854775807)")]
    public async Task ModifiedGreedy_Solve_ReturnsExpectedFeasibleStringCertificate(string input, string expected) {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync($"/ProblemProvider/solve?solver={ModifiedGreedySolver}", input, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string certificate = (await response.Content.ReadFromJsonAsync<string>(TestContext.Current.CancellationToken))!;
        Assert.Equal(expected, certificate);
        Assert.True(new MaximumValueKnapsackSolutionVerifier().verify(new MAXIMUMVALUEKNAPSACK(input), certificate));
    }

    // --- Exact-search limits ---

    [Fact]
    public async Task Exact_ExhaustiveLimits_DoNotAffectParsingOrSolutionVerification() {
        using var client = factory.CreateClient();
        string instance = MaximumValueKnapsackBruteForce_Tests.UnitItems(21);
        using var parsed = await client.PostAsJsonAsync("/ProblemProvider/problemInstance?problem=MAXIMUMVALUEKNAPSACK", instance, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, parsed.StatusCode);
        var body = new { ProblemInstance = instance, Certificate = "({21},1)" };
        using var feasible = await client.PostAsJsonAsync("/ProblemProvider/verify?verifier=maximumvalueknapsacksolutionverifier", body, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, feasible.StatusCode);
        Assert.Equal("True", await feasible.Content.ReadFromJsonAsync<string>(TestContext.Current.CancellationToken));
        using var solved = await client.PostAsJsonAsync($"/ProblemProvider/solve?solver={BruteForceSolver}", instance, TestContext.Current.CancellationToken);
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
    public async Task BranchAndBound_MoreThanTwentyItems_SucceedsWithoutBruteForceLimit() {
        using var client = factory.CreateClient();
        string input = MaximumValueKnapsackBruteForce_Tests.UnitItems(40);
        using var response = await client.PostAsJsonAsync($"/ProblemProvider/solve?solver={BranchAndBoundSolver}", input, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string certificate = (await response.Content.ReadFromJsonAsync<string>(TestContext.Current.CancellationToken))!;
        var problem = new MAXIMUMVALUEKNAPSACK(input);
        Assert.True(new MaximumValueKnapsackSolutionVerifier().verify(problem, certificate));
        Assert.Equal(40, MaximumValueKnapsackSolutionVerifier.ParseCertificate(problem, certificate).Value);
    }

    [Fact]
    public async Task ModifiedGreedy_MoreThanTwentyItems_SucceedsWithoutBruteForceLimit() {
        using var client = factory.CreateClient();
        string input = MaximumValueKnapsackBruteForce_Tests.UnitItems(40);
        using var response = await client.PostAsJsonAsync($"/ProblemProvider/solve?solver={ModifiedGreedySolver}", input, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string certificate = (await response.Content.ReadFromJsonAsync<string>(TestContext.Current.CancellationToken))!;
        var problem = new MAXIMUMVALUEKNAPSACK(input);
        Assert.True(new MaximumValueKnapsackSolutionVerifier().verify(problem, certificate));
        Assert.Equal(40, MaximumValueKnapsackSolutionVerifier.ParseCertificate(problem, certificate).Value);
    }

    // --- Malformed inputs and error contracts ---

    [Theory]
    [InlineData("garbage")]
    [InlineData("(((1,2)),0)trailing")]
    [InlineData("(((0,0\0)),0)")]
    [InlineData("(((0,9223372036854775807),(0,1)),0)")]
    public async Task Definition_ProblemInstance_ReportsStructuredParseErrors(string input) {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/ProblemProvider/problemInstance?problem=MAXIMUMVALUEKNAPSACK", input,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("instance_parse_error", json.GetProperty("error").GetString());
        Assert.Equal(input, json.GetProperty("received").GetString());
        Assert.Equal(new MAXIMUMVALUEKNAPSACK().instanceFormat, json.GetProperty("expected").GetString());
    }

    [Theory]
    [MemberData(nameof(MaximumValueKnapsackVerifier_Tests.MalformedCertificates), MemberType = typeof(MaximumValueKnapsackVerifier_Tests))]
    public async Task Solution_Verify_MalformedCertificate_ReturnsBadRequestWithExistingErrorContract(string? certificate) {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/ProblemProvider/verify?verifier=maximumvalueknapsacksolutionverifier",
            new { ProblemInstance = MAXIMUMVALUEKNAPSACK.DefaultInstance, Certificate = certificate }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        if (certificate is null) {
            // ASP.NET rejects a null required request field before invoking the verifier.
            Assert.NotEmpty(json.GetProperty("errors").EnumerateObject());
            return;
        }
        Assert.Equal("certificate_parse_error", json.GetProperty("error").GetString());
        Assert.Equal(certificate, json.GetProperty("received").GetString());
        Assert.Equal("Maximum-Value 0-1 Knapsack", json.GetProperty("problem").GetString());
        Assert.Equal(new MAXIMUMVALUEKNAPSACK().certificateFormat, json.GetProperty("expected").GetString());
    }

    [Theory]
    [InlineData("({9223372036854775808},0)")]
    [InlineData("({},9223372036854775808)")]
    [InlineData("({1e0},60)")]
    [InlineData("({},-1)")]
    [InlineData("({1\0},60)")]
    public async Task Solution_Verify_InvalidNumbers_ReturnCertificateParseError(string certificate) {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/ProblemProvider/verify?verifier=maximumvalueknapsacksolutionverifier",
            new { ProblemInstance = MAXIMUMVALUEKNAPSACK.DefaultInstance, Certificate = certificate }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("certificate_parse_error", json.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Solution_Verify_MalformedInstance_ReturnsInstanceParseError() {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/ProblemProvider/verify?verifier=maximumvalueknapsacksolutionverifier",
            new { ProblemInstance = "garbage", Certificate = "({},0)" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("instance_parse_error", json.GetProperty("error").GetString());
    }

    [Theory]
    [InlineData("({1,1},120)")]
    [InlineData("({1},60)trailing")]
    [InlineData("({1},60\0)")]
    [InlineData("({},9223372036854775808)")]
    public async Task Exact_Optimality_MalformedCertificate_ReturnsStructuredParseError(string certificate) {
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
    public async Task Exact_MalformedInstances_ReturnParseErrorsFromBothComponents() {
        using var client = factory.CreateClient();
        using var solved = await client.PostAsJsonAsync($"/ProblemProvider/solve?solver={BruteForceSolver}", "garbage", TestContext.Current.CancellationToken);
        using var verified = await client.PostAsJsonAsync($"/ProblemProvider/verify?verifier={Optimality}",
            new { ProblemInstance = "garbage", Certificate = "({},0)" }, TestContext.Current.CancellationToken);
        foreach (var response in new[] { solved, verified }) {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
            Assert.Equal("instance_parse_error", json.GetProperty("error").GetString());
        }
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("(((1,2)),3)trailing")]
    [InlineData("(((1,2\0)),3)")]
    [InlineData("(((0,9223372036854775807),(0,1)),0)")]
    public async Task BranchAndBound_MalformedInstance_ReturnsExistingStructuredParseError(string input) {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync($"/ProblemProvider/solve?solver={BranchAndBoundSolver}", input, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("instance_parse_error", json.GetProperty("error").GetString());
        Assert.Equal(input, json.GetProperty("received").GetString());
        Assert.Equal(new MAXIMUMVALUEKNAPSACK().instanceFormat, json.GetProperty("expected").GetString());
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("(((1,2)),3)trailing")]
    [InlineData("(((1,2\0)),3)")]
    [InlineData("(((0,9223372036854775807),(0,1)),0)")]
    public async Task ModifiedGreedy_MalformedInstance_ReturnsExistingStructuredParseError(string input) {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync($"/ProblemProvider/solve?solver={ModifiedGreedySolver}", input, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("instance_parse_error", json.GetProperty("error").GetString());
        Assert.Equal(input, json.GetProperty("received").GetString());
        Assert.Equal(new MAXIMUMVALUEKNAPSACK().instanceFormat, json.GetProperty("expected").GetString());
    }

    // --- Timeouts ---

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Exact_TimeLimit_ReturnsErrorInsteadOfCertificateOrOptimalityProof(bool verify) {
        using var limited = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { [ComputeLimits.TimeLimitKey] = "0.001" })));
        using var client = limited.CreateClient();
        string instance = MaximumValueKnapsackBruteForce_Tests.UnitItems(20);
        using var response = verify
            ? await client.PostAsJsonAsync($"/ProblemProvider/verify?verifier={Optimality}",
                new { ProblemInstance = instance, Certificate = "({},0)" }, TestContext.Current.CancellationToken)
            : await client.PostAsJsonAsync($"/ProblemProvider/solve?solver={BruteForceSolver}", instance, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("time_limit", json.GetProperty("error").GetString());
    }

    [Fact]
    public async Task BranchAndBound_Timeout_ReturnsTimeLimitErrorRatherThanAnUnprovenIncumbent() {
        using var limited = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { [ComputeLimits.TimeLimitKey] = "0.001" })));
        using var client = limited.CreateClient();
        using var response = await client.PostAsJsonAsync($"/ProblemProvider/solve?solver={BranchAndBoundSolver}",
            MaximumValueKnapsackBranchAndBound_Tests.HardInstance(), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("time_limit", json.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ModifiedGreedy_Timeout_ReturnsTimeLimitErrorInsteadOfPartialCandidate() {
        using var limited = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { [ComputeLimits.TimeLimitKey] = "0.001" })));
        using var client = limited.CreateClient();
        using var response = await client.PostAsJsonAsync($"/ProblemProvider/solve?solver={ModifiedGreedySolver}",
            MaximumValueKnapsackBruteForce_Tests.UnitItems(20000), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("time_limit", json.GetProperty("error").GetString());
    }
}
