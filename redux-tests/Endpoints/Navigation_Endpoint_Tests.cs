using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

public class Navigation_Endpoint_Tests : IClassFixture<AppFactory>
{
    private readonly HttpClient _client;

    public Navigation_Endpoint_Tests(AppFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ── ALL_ProblemsRefactor ──────────────────────────────────────────────────

    [Fact]
    public async Task AllProblems_Returns200()
    {
        var response = await _client.GetAsync("/Navigation/ALL_ProblemsRefactor", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AllProblems_ReturnsNonEmptyJsonArray()
    {
        var response = await _client.GetAsync("/Navigation/ALL_ProblemsRefactor", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var arr = JsonSerializer.Deserialize<JsonElement[]>(body);
        Assert.NotNull(arr);
        Assert.NotEmpty(arr);
    }

    // ── NPC_ProblemsRefactor ──────────────────────────────────────────────────

    [Fact]
    public async Task NpcProblems_Returns200()
    {
        var response = await _client.GetAsync("/Navigation/NPC_ProblemsRefactor", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task NpcProblems_ReturnsNonEmptyJsonArray()
    {
        var response = await _client.GetAsync("/Navigation/NPC_ProblemsRefactor", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var arr = JsonSerializer.Deserialize<JsonElement[]>(body);
        Assert.NotNull(arr);
        Assert.NotEmpty(arr);
    }

    // ── All_Verifiers (requires chosenProblem param) ──────────────────────────

    [Fact]
    public async Task AllVerifiers_WithProblem_Returns200()
    {
        var response = await _client.GetAsync("/Navigation/All_Verifiers?chosenProblem=NPC_SAT3", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AllVerifiers_WithProblem_ReturnsNonEmptyJsonArray()
    {
        var response = await _client.GetAsync("/Navigation/All_Verifiers?chosenProblem=NPC_SAT3", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var arr = JsonSerializer.Deserialize<JsonElement[]>(body);
        Assert.NotNull(arr);
        Assert.NotEmpty(arr);
    }

    // ── All_Visualizations (requires chosenProblem param) ────────────────────

    [Fact]
    public async Task AllVisualizations_WithProblem_Returns200()
    {
        var response = await _client.GetAsync("/Navigation/All_Visualizations?chosenProblem=NPC_SAT3", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AllVisualizations_WithProblem_ReturnsNonEmptyJsonArray()
    {
        var response = await _client.GetAsync("/Navigation/All_Visualizations?chosenProblem=NPC_SAT3", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var arr = JsonSerializer.Deserialize<JsonElement[]>(body);
        Assert.NotNull(arr);
        Assert.NotEmpty(arr);
    }

    // ── Reductions ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reductions_Returns200()
    {
        var response = await _client.GetAsync("/Navigation/Reductions", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Reductions_ReturnsNonEmptyGraph()
    {
        var response = await _client.GetAsync("/Navigation/Reductions", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        // Returns a nested adjacency map, not an array
        var graph = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(body);
        Assert.NotNull(graph);
        Assert.NotEmpty(graph);
    }
}
