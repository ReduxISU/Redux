using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using API.Interfaces;
using API.Problems.NPHard.NPH_WEIGHTEDMAXCLIQUE;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

public class WeightedMaxClique_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly AppFactory _factory;
    private readonly HttpClient _client;

    public WeightedMaxClique_Endpoint_Tests(AppFactory factory) {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("weightedmaxcliquesolutionverifier", "({a,b,c},12)", true)]
    [InlineData("weightedmaxcliqueoptimalityverifier", "({a,b,c},12)", true)]
    [InlineData("weightedmaxcliquesolutionverifier", "({d},7)", true)]
    [InlineData("weightedmaxcliqueoptimalityverifier", "({d},7)", false)]
    [InlineData("weightedmaxcliquesolutionverifier", "({a,d},11)", false)]
    [InlineData("weightedmaxcliqueoptimalityverifier", "({unknown},0)", false)]
    [InlineData("weightedmaxcliquesolutionverifier", "({a},5)", false)]
    public async Task Verify_ReturnsFeasibilityOrOptimalityVerdict(string verifier, string certificate, bool expected) {
        var response = await Verify(verifier, WEIGHTEDMAXCLIQUE.DefaultInstance, certificate);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expected.ToString(), await response.Content.ReadFromJsonAsync<string>(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("weightedmaxcliquesolutionverifier", "({a,a},8)")]
    [InlineData("weightedmaxcliqueoptimalityverifier", "({a,b,c},12)trailing")]
    [InlineData("weightedmaxcliquesolutionverifier", "({a},4,5)")]
    [InlineData("weightedmaxcliqueoptimalityverifier", "({},9223372036854775808)")]
    public async Task Verify_MalformedCertificate_ReturnsStructuredBadRequest(string verifier, string certificate) {
        var response = await Verify(verifier, WEIGHTEDMAXCLIQUE.DefaultInstance, certificate);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("certificate_parse_error", body.GetProperty("error").GetString());
        Assert.Equal("Weighted Maximum Clique", body.GetProperty("problem").GetString());
        Assert.Equal(certificate, body.GetProperty("received").GetString());
        Assert.Equal(new WEIGHTEDMAXCLIQUE().certificateFormat, body.GetProperty("expected").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("detail").GetString()));
    }

    [Fact]
    public async Task Verify_OptimalityLimitIsDistinctFromSolutionVerification() {
        string instance = WeightedMaxCliqueVerifier_Tests.IsolatedInstance(21);
        var feasible = await Verify("weightedmaxcliquesolutionverifier", instance, "({v0},1)");
        Assert.Equal(HttpStatusCode.OK, feasible.StatusCode);
        Assert.Equal("True", await feasible.Content.ReadFromJsonAsync<string>(TestContext.Current.CancellationToken));
        var response = await Verify("weightedmaxcliqueoptimalityverifier", instance, "({v0},1)");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("exact_search_limit_exceeded", body.GetProperty("error").GetString());
        Assert.Equal(21, body.GetProperty("variableCount").GetInt32());
        Assert.Equal(20, body.GetProperty("maxVariables").GetInt32());
        Assert.Contains("Weighted Maximum Clique", body.GetProperty("detail").GetString());
    }

    [Theory]
    [InlineData("weightedmaxcliquesolutionverifier")]
    [InlineData("weightedmaxcliqueoptimalityverifier")]
    public async Task Verify_MalformedInstance_ReturnsInstanceParseError(string verifier) {
        var response = await Verify(verifier, "garbage", "({},0)");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("instance_parse_error", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Info_ExposesNewDefaultVerifier() {
        var response = await _client.GetAsync("/ProblemProvider/info?interface=WEIGHTEDMAXCLIQUE", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("Weighted Maximum Clique Solution Verifier", body.GetProperty("defaultVerifier").GetProperty("verifierName").GetString());
    }

    [Fact]
    public async Task Verify_TimeLimitReturnsErrorRatherThanOptimalityProof() {
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { [ComputeLimits.TimeLimitKey] = "0.001" })));
        using var client = factory.CreateClient();
        var body = new { ProblemInstance = WeightedMaxCliqueVerifier_Tests.IsolatedInstance(20), Certificate = "({v0},1)" };
        var response = await client.PostAsJsonAsync("/ProblemProvider/verify?verifier=weightedmaxcliqueoptimalityverifier", body,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        JsonElement error = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("time_limit", error.GetProperty("error").GetString());
    }

    private Task<HttpResponseMessage> Verify(string verifier, string instance, string certificate) =>
        _client.PostAsJsonAsync($"/ProblemProvider/verify?verifier={verifier}",
            new { ProblemInstance = instance, Certificate = certificate }, TestContext.Current.CancellationToken);
}
