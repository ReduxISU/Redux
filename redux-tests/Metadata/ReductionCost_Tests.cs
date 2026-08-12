using System.Net;
using System.Text.Json;
using API.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Guards for the ReductionCost vocabulary (plan: Redux Tag System, Section 3 / Part C).
//
// Risk 1 (same class of regression VisualizationType_Tests.cs guards against): Newtonsoft
// serializes enums as integers by default. ReductionEdge.cost is already a plain string
// field by construction (same mitigation fromComplexity/toComplexity already rely on), so
// it is safe regardless — but ReductionCost is ALSO reachable directly off a raw IReduction
// instance via /ProblemProvider/info?interface=<ReductionClassName> and
// /Navigation/Batch/allInfo (both reflect every ProblemProvider.Interfaces / .Reductions
// type, including reductions, and Newtonsoft-serialize the constructed instance as-is —
// see ProblemProvider.info and Nav_Batch.InfoJson). The [JsonConverter]/[StringEnumConverter]
// attributes pinned on the ReductionCost enum type (Interfaces/ReductionCost.cs) are the
// actual fix for that path; the facts below are the regression guard for it. Must never go
// back to failing.
public class ReductionCost_Tests : IClassFixture<AppFactory> {
        private readonly HttpClient _client;

        public ReductionCost_Tests(AppFactory factory) {
                _client = factory.CreateClient();
        }

        // ── Risk 1: enums must serialize as strings, not integers ─────────────────

        [Fact]
        public async Task Reductions_SerializesCostAsStringOnEveryEdge() {
                var response = await _client.GetAsync("/Navigation/Reductions", TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                using var doc = JsonDocument.Parse(body);

                int checkedCount = 0;
                foreach (var fromProp in doc.RootElement.EnumerateObject()) {
                        foreach (var toProp in fromProp.Value.EnumerateObject()) {
                                foreach (var edge in toProp.Value.EnumerateArray()) {
                                        Assert.True(edge.TryGetProperty("cost", out var costProp),
                                            $"Expected a cost property on every edge in /Navigation/Reductions. Edge:\n{edge}");
                                        checkedCount++;
                                        Assert.True(costProp.ValueKind == JsonValueKind.String,
                                            $"Edge {fromProp.Name}->{toProp.Name} ({edge.GetProperty("className").GetString()}).cost " +
                                            $"serialized as {costProp.ValueKind}, expected String.");
                                }
                        }
                }

                Assert.True(checkedCount > 0, "Found no edges in /Navigation/Reductions.");
        }

        // ProblemProvider.info reflects a raw IReduction instance directly (Interfaces
        // includes Reductions), so ReductionCost.cost is only safe here because of the
        // [JsonConverter]/[StringEnumConverter] attributes pinned on the enum type itself
        // (Interfaces/ReductionCost.cs) — not because of anything ReductionEdge does. This is
        // the real Newtonsoft-enum-as-int risk path; the ReductionEdge.cost string mirror
        // above is already safe by construction and doesn't exercise it.
        [Fact]
        public async Task Info_SerializesReductionCostAsString() {
                var response = await _client.GetAsync(
                    "/ProblemProvider/info?interface=KarpVertexCoverToSetCover",
                    TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                using var doc = JsonDocument.Parse(body);

                Assert.True(doc.RootElement.TryGetProperty("cost", out var costProp),
                    $"Expected a cost property in the /ProblemProvider/info response for a reduction. Body:\n{body}");
                Assert.Equal(JsonValueKind.String, costProp.ValueKind);
                Assert.Equal(nameof(ReductionCost.Linear), costProp.GetString());
        }

        // /Navigation/Batch/allInfo reflects every ProblemProvider.Interfaces type at once
        // (Nav_Batch.InfoJson), including reductions — same raw-instance risk path as
        // ProblemProvider.info above, exercised across every reduction class instead of one.
        [Fact]
        public async Task AllInfo_SerializesEveryReductionCostAsString() {
                var response = await _client.GetAsync("/Navigation/Batch/allInfo", TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                var map = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(body);
                Assert.NotNull(map);

                int checkedCount = 0;
                foreach (var (className, element) in map!) {
                        if (element.ValueKind != JsonValueKind.Object) continue;
                        if (!element.TryGetProperty("cost", out var costProp)) continue;
                        // reductionFrom/reductionTo are IProblem, not IReduction — they don't carry
                        // a `cost` property, but guard against a same-named property elsewhere by
                        // also requiring reductionName, which is IReduction-specific.
                        if (!element.TryGetProperty("reductionName", out _)) continue;

                        checkedCount++;
                        Assert.True(costProp.ValueKind == JsonValueKind.String,
                            $"{className}.cost serialized as {costProp.ValueKind}, expected String. This is the Risk-1 " +
                            "regression: Newtonsoft defaults to serializing enums as integers unless StringEnumConverter " +
                            "is pinned on ReductionCost.");
                }

                Assert.True(checkedCount > 0, "Found no reduction cost properties in /Navigation/Batch/allInfo.");
        }

        // ── Ratchet pair: cost != Unclassified ──────────────────────────────────────
        //
        // NoNewUndeclared:            actualUndeclared ⊆ Allowlist  — a new reduction cannot
        //                              be born undeclared.
        // AllowlistHasNoStaleEntries: Allowlist ⊆ actualUndeclared  — classifying a reduction
        //                              forces deleting its allowlist line.
        //
        // Empty by default: every concrete reduction class was classified directly by reading
        // its reduce() method (mechanical Big-O of output vs input instance size, not a
        // literature judgment call the way ComplexityClass sometimes needs) — see the cost
        // declarations and their one-line justifications on each ReduceTo/**/*.cs class.
        private static readonly string[] UnclassifiedAllowlist =
        {
    };

        private static HashSet<string> ActualUndeclared() =>
            ReductionCostCatalog.ByClassName.Value
                .Where(kv => kv.Value == nameof(ReductionCost.Unclassified))
                .Select(kv => kv.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        [Fact]
        public void NoNewUndeclared() {
                var actual = ActualUndeclared();
                var allowlist = new HashSet<string>(UnclassifiedAllowlist, StringComparer.OrdinalIgnoreCase);
                var unexpected = actual.Where(c => !allowlist.Contains(c)).OrderBy(c => c, StringComparer.Ordinal).ToList();

                Assert.True(unexpected.Count == 0,
                    $"Reduction(s) declared ReductionCost.Unclassified without being added to the allowlist: " +
                    $"{string.Join(", ", unexpected)}. Either declare a real ReductionCost by reading reduce() (see " +
                    "the header of this file), or add the class to UnclassifiedAllowlist in ReductionCost_Tests.cs.");
        }

        [Fact]
        public void AllowlistHasNoStaleEntries() {
                var actual = ActualUndeclared();
                var stale = UnclassifiedAllowlist.Where(c => !actual.Contains(c)).ToList();

                Assert.True(stale.Count == 0,
                    $"Allowlist entry no longer ReductionCost.Unclassified (already classified, or " +
                    $"renamed/removed/failed to instantiate) — delete from UnclassifiedAllowlist in " +
                    $"ReductionCost_Tests.cs: {string.Join(", ", stale)}.");
        }
}
