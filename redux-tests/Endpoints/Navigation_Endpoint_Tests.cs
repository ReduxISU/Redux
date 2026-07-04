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

    // ── Problem_VerifiersRefactor: lookup must not depend on problemType ──────
    // Regression for #317/#318: the GUI pins problemType to "NPC" and never updates
    // it, so a P / NP-Hard problem arrives with the wrong prefix. The verifier lookup
    // must still find the verifier by problem name regardless of the prefix sent.

    [Fact]
    public async Task ProblemVerifiersRefactor_PProblem_WithWrongNpcProblemType_FindsVerifier()
    {
        // DFA lives under Problems/P, but mirror the GUI sending problemType=NPC.
        var response = await _client.GetAsync("/Navigation/Problem_VerifiersRefactor?chosenProblem=DFA&problemType=NPC", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var arr = JsonSerializer.Deserialize<string[]>(body);
        Assert.NotNull(arr);
        Assert.Contains("DFAVerifier", arr);
    }

    [Fact]
    public async Task ProblemVerifiersRefactor_PProblem_WithCorrectProblemType_FindsVerifier()
    {
        var response = await _client.GetAsync("/Navigation/Problem_VerifiersRefactor?chosenProblem=DFA&problemType=P", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var arr = JsonSerializer.Deserialize<string[]>(body);
        Assert.NotNull(arr);
        Assert.Contains("DFAVerifier", arr);
    }

    [Fact]
    public async Task ProblemVerifiersRefactor_UnknownProblem_ReturnsNotFoundString()
    {
        var response = await _client.GetAsync("/Navigation/Problem_VerifiersRefactor?chosenProblem=NoSuchProblem&problemType=NPC", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        // Ignoring problemType must not turn a genuinely missing problem into a match.
        var message = JsonSerializer.Deserialize<string>(body);
        Assert.Equal("entered a verifier that does not exist", message);
    }

    [Fact]
    public async Task ProblemVerifiersRefactor_OmittedProblemType_FindsVerifier()
    {
        // problemType is optional (#330); omitting it entirely must still resolve by name.
        var response = await _client.GetAsync("/Navigation/Problem_VerifiersRefactor?chosenProblem=DFA", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var arr = JsonSerializer.Deserialize<string[]>(body);
        Assert.NotNull(arr);
        Assert.Contains("DFAVerifier", arr);
    }

    [Fact]
    public async Task ProblemVerifiersRefactor_CaseInsensitiveInputs_ReturnsNonEmptyJsonArray()
    {
        var response = await _client.GetAsync("/Navigation/Problem_VerifiersRefactor?chosenProblem=sat3&problemType=npc", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var arr = JsonSerializer.Deserialize<JsonElement[]>(body);
        Assert.NotNull(arr);
        Assert.NotEmpty(arr);
    }

    // ── Problem_SolversRefactor: reflection-based, lookup ignores problemType ──
    // Mirrors the verifier navigation: the GUI pins problemType to "NPC", so the
    // solver lookup must find solvers by problem name regardless of the prefix sent.

    [Fact]
    public async Task ProblemSolversRefactor_NpcProblem_FindsSolver()
    {
        var response = await _client.GetAsync("/Navigation/Problem_SolversRefactor?chosenProblem=SAT3&problemType=NPC", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var arr = JsonSerializer.Deserialize<string[]>(body);
        Assert.NotNull(arr);
        Assert.NotEmpty(arr);
    }

    [Fact]
    public async Task ProblemSolversRefactor_SAT_ListsGroverSolver()
    {
        // SATGroverSolver declares ISolver<SAT>, so it resolves to SAT like any
        // other solver — guards against it regressing out of the listing.
        var response = await _client.GetAsync("/Navigation/Problem_SolversRefactor?chosenProblem=SAT&problemType=NPC", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var arr = JsonSerializer.Deserialize<string[]>(body);
        Assert.NotNull(arr);
        Assert.Contains("SATGroverSolver", arr);
    }

    [Fact]
    public async Task ProblemSolversRefactor_PProblem_WithWrongNpcProblemType_FindsSolver()
    {
        // CLIQUE solver lookup must succeed even when the GUI sends a mismatched prefix.
        var response = await _client.GetAsync("/Navigation/Problem_SolversRefactor?chosenProblem=Clique&problemType=P", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var arr = JsonSerializer.Deserialize<string[]>(body);
        Assert.NotNull(arr);
        Assert.Contains("CliqueBruteForce", arr);
    }

    [Fact]
    public async Task ProblemSolversRefactor_OmittedProblemType_FindsSolver()
    {
        // problemType is optional; omitting it entirely must still resolve by name.
        var response = await _client.GetAsync("/Navigation/Problem_SolversRefactor?chosenProblem=SAT3", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var arr = JsonSerializer.Deserialize<string[]>(body);
        Assert.NotNull(arr);
        Assert.NotEmpty(arr);
    }

    [Fact]
    public async Task ProblemSolversRefactor_UnknownProblem_ReturnsNotFoundString()
    {
        var response = await _client.GetAsync("/Navigation/Problem_SolversRefactor?chosenProblem=NoSuchProblem&problemType=NPC", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var message = JsonSerializer.Deserialize<string>(body);
        Assert.Equal("entered a solver that does not exist", message);
    }

    [Fact]
    public async Task ProblemSolversRefactor_CaseInsensitiveInputs_ReturnsNonEmptyJsonArray()
    {
        var response = await _client.GetAsync("/Navigation/Problem_SolversRefactor?chosenProblem=sat3&problemType=npc", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

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
