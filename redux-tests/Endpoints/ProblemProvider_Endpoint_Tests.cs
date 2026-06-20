using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

public class ProblemProvider_Endpoint_Tests : IClassFixture<AppFactory>
{
    private readonly HttpClient _client;

    public ProblemProvider_Endpoint_Tests(AppFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ── /ProblemProvider/info ─────────────────────────────────────────────────

    [Theory]
    [InlineData("sat3")]
    [InlineData("clique")]
    [InlineData("vertexcover")]
    [InlineData("independentset")]
    public async Task Info_KnownProblem_Returns200(string name)
    {
        var response = await _client.GetAsync($"/ProblemProvider/info?interface={name}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("sat3")]
    [InlineData("clique")]
    public async Task Info_KnownProblem_ReturnsNonEmptyBody(string name)
    {
        var response = await _client.GetAsync($"/ProblemProvider/info?interface={name}", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.False(string.IsNullOrWhiteSpace(body));
    }

    // ── /ProblemProvider/verify ───────────────────────────────────────────────

    [Fact]
    public async Task Verify_ValidSAT3Certificate_ReturnsTrue()
    {
        var body = new { Certificate = "x1:True,x2:False,x3:True", ProblemInstance = "(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | x1)" };
        var response = await _client.PostAsJsonAsync("/ProblemProvider/verify?verifier=sat3verifier", body, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("True", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Verify_InvalidSAT3Certificate_ReturnsFalse()
    {
        var body = new { Certificate = "x1:False,x2:False,x3:False", ProblemInstance = "(x1 | x2 | x3) & (!x1 | !x2 | !x3) & (x1 | !x2 | x3)" };
        var response = await _client.PostAsJsonAsync("/ProblemProvider/verify?verifier=sat3verifier", body, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("False", result, StringComparison.OrdinalIgnoreCase);
    }

    // ── /ProblemProvider/solve ────────────────────────────────────────────────

    [Fact]
    public async Task Solve_SAT3BacktrackingSolver_Returns200()
    {
        var instance = "\"(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | x1)\"";
        var content = new StringContent(instance, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/solve?solver=sat3backtrackingsolver", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Solve_SAT3BacktrackingSolver_ReturnsNonEmptyResult()
    {
        var instance = "\"(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | x1)\"";
        var content = new StringContent(instance, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/solve?solver=sat3backtrackingsolver", content, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.False(string.IsNullOrWhiteSpace(body));
    }

    // ── /ProblemProvider/reduce ───────────────────────────────────────────────

    [Fact]
    public async Task Reduce_SAT3ToClique_Returns200()
    {
        var instance = "\"(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | x1)\"";
        var content = new StringContent(instance, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/reduce?reduction=sipserreducetocliquestandard", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Reduce_SAT3ToClique_ReturnsNonEmptyResult()
    {
        var instance = "\"(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | x1)\"";
        var content = new StringContent(instance, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/reduce?reduction=sipserreducetocliquestandard", content, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.False(string.IsNullOrWhiteSpace(body));
    }
}
