using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Tests for the Navigation/Batch endpoints. The batch responses are projected
// from the same reflected data the singular Navigation controllers use, so the
// central guarantee under test is that a batch payload never drifts from its
// per-problem counterpart.
public class Batch_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public Batch_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private async Task<string[]> GetArray(string url) {
        var response = await _client.GetAsync(url, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var arr = JsonSerializer.Deserialize<string[]>(body);
        Assert.NotNull(arr);
        return arr!;
    }

    private async Task<Dictionary<string, List<string>>> GetMap(string url) {
        var response = await _client.GetAsync(url, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var map = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(body);
        Assert.NotNull(map);
        return map!;
    }

    // ── allProblems ───────────────────────────────────────────────────────────

    [Fact]
    public async Task AllProblems_Returns200AndNonEmpty() {
        var problems = await GetArray("/Navigation/Batch/allProblems");
        Assert.NotEmpty(problems);
        Assert.Contains("SAT3", problems);
    }

    [Fact]
    public async Task AllProblems_MatchesSingularAllEndpoint() {
        // Batch must be exactly the reflected problem set the singular endpoint returns.
        var batch = await GetArray("/Navigation/Batch/allProblems");
        var singular = await GetArray("/Navigation/ALL_ProblemsRefactor");
        Assert.Equal(
            new SortedSet<string>(singular, StringComparer.Ordinal),
            new SortedSet<string>(batch, StringComparer.Ordinal));
    }

    [Fact]
    public async Task AllProblems_ExcludesInheritedHelperProblems() {
        // SipserClique lives in NPC_CLIQUE.Inherited; it is a nested helper variant,
        // not a top-level problem, so it must not appear in the listing.
        var problems = await GetArray("/Navigation/Batch/allProblems");
        Assert.DoesNotContain(problems, p => string.Equals(p, "SipserClique", StringComparison.OrdinalIgnoreCase));
    }

    // ── allSolvers / allVerifiers / allVisualizations ─────────────────────────

    [Fact]
    public async Task AllSolvers_MapsSat3ToItsSolver() {
        var map = await GetMap("/Navigation/Batch/allSolvers");
        Assert.True(map.ContainsKey("SAT3"));
        Assert.Contains("Sat3BacktrackingSolver", map["SAT3"]);
    }

    [Fact]
    public async Task AllSolvers_Sat3EntryMatchesSingularEndpoint() {
        // The whole point of the batch endpoint: no drift from the per-problem lookup.
        var map = await GetMap("/Navigation/Batch/allSolvers");
        var singular = await GetArray("/Navigation/Problem_SolversRefactor?chosenProblem=SAT3");
        Assert.Equal(singular.OrderBy(x => x, StringComparer.Ordinal),
                     map["SAT3"].OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public async Task AllVerifiers_MapsSat3ToItsVerifier() {
        var map = await GetMap("/Navigation/Batch/allVerifiers");
        Assert.True(map.ContainsKey("SAT3"));
        Assert.Contains("SAT3Verifier", map["SAT3"]);
    }

    [Fact]
    public async Task AllVerifiers_Sat3EntryMatchesSingularEndpoint() {
        var map = await GetMap("/Navigation/Batch/allVerifiers");
        var singular = await GetArray("/Navigation/Problem_VerifiersRefactor?chosenProblem=SAT3");
        Assert.Equal(singular.OrderBy(x => x, StringComparer.Ordinal),
                     map["SAT3"].OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public async Task AllVisualizations_MapsSat3ToDefaultVisualization() {
        var map = await GetMap("/Navigation/Batch/allVisualizations");
        Assert.True(map.ContainsKey("SAT3"));
        Assert.Contains("Sat3DefaultVisualization", map["SAT3"]);
    }

    // ── allInfo ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task AllInfo_Returns200AndContainsProblemObject() {
        var response = await _client.GetAsync("/Navigation/Batch/allInfo", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var map = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(body);
        Assert.NotNull(map);
        Assert.NotEmpty(map!);
        Assert.True(map!.ContainsKey("SAT3"));
        Assert.Equal(JsonValueKind.Object, map["SAT3"].ValueKind);
    }

    [Fact]
    public async Task AllInfo_RepeatedCalls_ReturnIdenticalPayload() {
        // Cached via Lazy<string>: every call after the first returns the same bytes.
        var first = await (await _client.GetAsync("/Navigation/Batch/allInfo", TestContext.Current.CancellationToken))
            .Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var second = await (await _client.GetAsync("/Navigation/Batch/allInfo", TestContext.Current.CancellationToken))
            .Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(first, second);
    }

    // ── method contract ───────────────────────────────────────────────────────

    [Fact]
    public async Task AllProblems_RejectsPost() {
        // These are reads: GET only (PR #168 used POST; this reimplementation does not).
        var response = await _client.PostAsync("/Navigation/Batch/allProblems", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }
}
