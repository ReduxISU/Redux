using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using API.Problems.NPHard.NPH_MAXIMUMVALUEKNAPSACK;
using API.Problems.NPHard.NPH_MAXIMUMVALUEKNAPSACK.Verifiers;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

public class MaximumValueKnapsackVerifier_Endpoint_Tests(AppFactory factory) : IClassFixture<AppFactory> {
    [Theory]
    [InlineData("({2,3},220)", true)]
    [InlineData("({1},60)", true)]
    [InlineData("({},0)", true)]
    [InlineData("({1,2,3},280)", false)]
    [InlineData("({2,3},221)", false)]
    [InlineData("({0},0)", false)]
    [InlineData("({9223372036854775807},0)", false)]
    public async Task Verify_ReturnsFeasibilityAndValueVerdict(string certificate, bool expected) {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/ProblemProvider/verify?verifier=maximumvalueknapsacksolutionverifier",
            new { ProblemInstance = MAXIMUMVALUEKNAPSACK.DefaultInstance, Certificate = certificate }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expected.ToString(), await response.Content.ReadFromJsonAsync<string>(TestContext.Current.CancellationToken));
    }

    [Theory]
    [MemberData(nameof(MaximumValueKnapsackVerifier_Tests.MalformedCertificates), MemberType = typeof(MaximumValueKnapsackVerifier_Tests))]
    public async Task Verify_MalformedCertificate_ReturnsBadRequestWithExistingErrorContract(string? certificate) {
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
    [InlineData("((),0)", "({},0)", true)]
    [InlineData("(((0,7),(0,0)),0)", "({2,1},7)", true)]
    [InlineData("(((2,3),(2,3)),4)", "({1,2},6)", true)]
    [InlineData("(((0,9223372036854775806),(0,1)),0)", "({1,2},9223372036854775807)", true)]
    [InlineData("(((9223372036854775807,1),(9223372036854775807,2)),9223372036854775807)", "({1,2},3)", false)]
    public async Task Verify_BoundaryInstances_ReturnExpectedVerdicts(string instance, string certificate, bool expected) {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/ProblemProvider/verify?verifier=maximumvalueknapsacksolutionverifier",
            new { ProblemInstance = instance, Certificate = certificate }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expected.ToString(), await response.Content.ReadFromJsonAsync<string>(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("({9223372036854775808},0)")]
    [InlineData("({},9223372036854775808)")]
    [InlineData("({1e0},60)")]
    [InlineData("({},-1)")]
    [InlineData("({1\0},60)")]
    public async Task Verify_InvalidNumbers_ReturnCertificateParseError(string certificate) {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/ProblemProvider/verify?verifier=maximumvalueknapsacksolutionverifier",
            new { ProblemInstance = MAXIMUMVALUEKNAPSACK.DefaultInstance, Certificate = certificate }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("certificate_parse_error", json.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Verify_MalformedInstance_ReturnsInstanceParseError() {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/ProblemProvider/verify?verifier=maximumvalueknapsacksolutionverifier",
            new { ProblemInstance = "garbage", Certificate = "({},0)" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("instance_parse_error", json.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Info_ExposesRegisteredDefaultSolutionVerifier() {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/ProblemProvider/info?interface=MAXIMUMVALUEKNAPSACK", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var info = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var verifier = info.GetProperty("defaultVerifier");
        Assert.Equal(new MaximumValueKnapsackSolutionVerifier().verifierName, verifier.GetProperty("verifierName").GetString());
        Assert.Contains("does not check optimality", verifier.GetProperty("verifierDefinition").GetString());
    }
}
