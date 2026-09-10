using System.Net;
using System.Text.Json;
using API.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Guards for the ReductionType / ReductionComplexityBucket vocabularies (issue #376).
// Same "declared, not derived" pattern and same ratchet-pair structure as
// SolverType_Tests.cs / ReductionCost_Tests.cs — see those files' headers for the
// general rationale.
public class ReductionType_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    // ── Risk 1: enums must serialize as strings, not integers ─────────────────
    //
    // Same regression class ReductionCost_Tests.cs / SolverType_Tests.cs guard against:
    // Newtonsoft serializes enums as integers by default. The
    // [JsonConverter]/[StringEnumConverter] attributes pinned on ReductionType and
    // ReductionComplexityBucket (Interfaces/ReductionType.cs,
    // Interfaces/ReductionComplexityBucket.cs) are the actual fix; these are the
    // regression guard.

    [Fact]
    public async Task Reductions_SerializesTypeAndBucketAsStringOnEveryEdge() {
        var response = await _client.GetAsync("/Navigation/Reductions", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(body);

        int checkedCount = 0;
        foreach (var fromProp in doc.RootElement.EnumerateObject()) {
            foreach (var toProp in fromProp.Value.EnumerateObject()) {
                foreach (var edge in toProp.Value.EnumerateArray()) {
                    Assert.True(edge.TryGetProperty("reductionType", out var typeProp),
                        $"Expected a reductionType property on every edge in /Navigation/Reductions. Edge:\n{edge}");
                    Assert.True(edge.TryGetProperty("complexityBucket", out var bucketProp),
                        $"Expected a complexityBucket property on every edge in /Navigation/Reductions. Edge:\n{edge}");
                    checkedCount++;
                    Assert.True(typeProp.ValueKind == JsonValueKind.String,
                        $"Edge {fromProp.Name}->{toProp.Name} ({edge.GetProperty("className").GetString()}).reductionType " +
                        $"serialized as {typeProp.ValueKind}, expected String.");
                    Assert.True(bucketProp.ValueKind == JsonValueKind.String,
                        $"Edge {fromProp.Name}->{toProp.Name} ({edge.GetProperty("className").GetString()}).complexityBucket " +
                        $"serialized as {bucketProp.ValueKind}, expected String.");
                }
            }
        }

        Assert.True(checkedCount > 0, "Found no edges in /Navigation/Reductions.");
    }

    // ProblemProvider.info reflects a raw IReduction instance directly (Interfaces
    // includes Reductions), so ReductionType/ReductionComplexityBucket are only safe
    // here because of the [JsonConverter]/[StringEnumConverter] attributes pinned on
    // the enum types themselves — not because of anything ReductionEdge does. This is
    // the real Newtonsoft-enum-as-int risk path; the ReductionEdge mirror above is
    // already safe by construction and doesn't exercise it.
    [Fact]
    public async Task Info_SerializesReductionTypeAndBucketAsString() {
        var response = await _client.GetAsync(
            "/ProblemProvider/info?interface=KarpVertexCoverToSetCover",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(body);

        Assert.True(doc.RootElement.TryGetProperty("reductionType", out var typeProp),
            $"Expected a reductionType property in the /ProblemProvider/info response for a reduction. Body:\n{body}");
        Assert.Equal(JsonValueKind.String, typeProp.ValueKind);
        Assert.Equal(nameof(ReductionType.Restriction), typeProp.GetString());

        Assert.True(doc.RootElement.TryGetProperty("complexityBucket", out var bucketProp),
            $"Expected a complexityBucket property in the /ProblemProvider/info response for a reduction. Body:\n{body}");
        Assert.Equal(JsonValueKind.String, bucketProp.ValueKind);
        Assert.Equal(nameof(ReductionComplexityBucket.Polynomial), bucketProp.GetString());
    }

    // /Navigation/Batch/allInfo reflects every ProblemProvider.Interfaces type at once
    // (Nav_Batch.InfoJson), including reductions — same raw-instance risk path as
    // ProblemProvider.info above, exercised across every reduction class instead of one.
    [Fact]
    public async Task AllInfo_SerializesEveryReductionTypeAndBucketAsString() {
        var response = await _client.GetAsync("/Navigation/Batch/allInfo", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var map = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(body);
        Assert.NotNull(map);

        int checkedType = 0;
        int checkedBucket = 0;
        foreach (var (className, element) in map!) {
            if (element.ValueKind != JsonValueKind.Object) continue;
            // reductionFrom/reductionTo are IProblem, not IReduction — they don't carry
            // these properties, but guard against a same-named property elsewhere by
            // also requiring reductionName, which is IReduction-specific.
            if (!element.TryGetProperty("reductionName", out _)) continue;

            if (element.TryGetProperty("reductionType", out var typeProp)) {
                checkedType++;
                Assert.True(typeProp.ValueKind == JsonValueKind.String,
                    $"{className}.reductionType serialized as {typeProp.ValueKind}, expected String. This is " +
                    "the Risk-1 regression: Newtonsoft defaults to serializing enums as integers unless " +
                    "StringEnumConverter is pinned on ReductionType.");
            }

            if (element.TryGetProperty("complexityBucket", out var bucketProp)) {
                checkedBucket++;
                Assert.True(bucketProp.ValueKind == JsonValueKind.String,
                    $"{className}.complexityBucket serialized as {bucketProp.ValueKind}, expected String. This " +
                    "is the Risk-1 regression: Newtonsoft defaults to serializing enums as integers unless " +
                    "StringEnumConverter is pinned on ReductionComplexityBucket.");
            }
        }

        Assert.True(checkedType > 0, "Found no reductionType properties in /Navigation/Batch/allInfo.");
        Assert.True(checkedBucket > 0, "Found no complexityBucket properties in /Navigation/Batch/allInfo.");
    }

    // ── Ratchet pair #1: reductionType != Unclassified ──────────────────────────
    //
    // NoNewUndeclaredReductionType:            actualUndeclared ⊆ Allowlist
    // AllowlistHasNoStaleReductionTypeEntries:  Allowlist ⊆ actualUndeclared
    //
    // Empty by default: every concrete reduction class was classified directly by
    // reading its reduce() method (Garey & Johnson proof technique, not a literature
    // judgment call the way ComplexityClass sometimes needs) — see the reductionType
    // declarations and their one-line justifications on each ReduceTo/**/*.cs class.
    private static readonly string[] ReductionTypeUnclassifiedAllowlist =
    {
    };

    private static HashSet<string> ActualReductionTypeUndeclared() =>
        ReductionTypeCatalog.ReductionTypeByClassName.Value
            .Where(kv => kv.Value == nameof(ReductionType.Unclassified))
            .Select(kv => kv.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void NoNewUndeclaredReductionType() {
        var actual = ActualReductionTypeUndeclared();
        var allowlist = new HashSet<string>(ReductionTypeUnclassifiedAllowlist, StringComparer.OrdinalIgnoreCase);
        var unexpected = actual.Where(c => !allowlist.Contains(c)).OrderBy(c => c, StringComparer.Ordinal).ToList();

        Assert.True(unexpected.Count == 0,
            $"Reduction(s) declared ReductionType.Unclassified without being added to the allowlist: " +
            $"{string.Join(", ", unexpected)}. Either declare a real ReductionType by reading reduce() (see " +
            "the header of this file), or add the class to ReductionTypeUnclassifiedAllowlist in " +
            "ReductionType_Tests.cs.");
    }

    [Fact]
    public void AllowlistHasNoStaleReductionTypeEntries() {
        var actual = ActualReductionTypeUndeclared();
        var stale = ReductionTypeUnclassifiedAllowlist.Where(c => !actual.Contains(c)).ToList();

        Assert.True(stale.Count == 0,
            $"Allowlist entry no longer ReductionType.Unclassified (already classified, or " +
            $"renamed/removed/failed to instantiate) — delete from ReductionTypeUnclassifiedAllowlist in " +
            $"ReductionType_Tests.cs: {string.Join(", ", stale)}.");
    }

    // ── Ratchet pair #2: complexityBucket != Unclassified ───────────────────────
    //
    // NoNewUndeclaredComplexityBucket:            actualUndeclared ⊆ Allowlist
    // AllowlistHasNoStaleComplexityBucketEntries:  Allowlist ⊆ actualUndeclared
    private static readonly string[] ComplexityBucketUnclassifiedAllowlist =
    {
    };

    private static HashSet<string> ActualComplexityBucketUndeclared() =>
        ReductionTypeCatalog.ComplexityBucketByClassName.Value
            .Where(kv => kv.Value == nameof(ReductionComplexityBucket.Unclassified))
            .Select(kv => kv.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void NoNewUndeclaredComplexityBucket() {
        var actual = ActualComplexityBucketUndeclared();
        var allowlist = new HashSet<string>(ComplexityBucketUnclassifiedAllowlist, StringComparer.OrdinalIgnoreCase);
        var unexpected = actual.Where(c => !allowlist.Contains(c)).OrderBy(c => c, StringComparer.Ordinal).ToList();

        Assert.True(unexpected.Count == 0,
            $"Reduction(s) declared ReductionComplexityBucket.Unclassified without being added to the " +
            $"allowlist: {string.Join(", ", unexpected)}. Either declare a real ReductionComplexityBucket " +
            "by reading reduce() (see the header of this file), or add the class to " +
            "ComplexityBucketUnclassifiedAllowlist in ReductionType_Tests.cs.");
    }

    [Fact]
    public void AllowlistHasNoStaleComplexityBucketEntries() {
        var actual = ActualComplexityBucketUndeclared();
        var stale = ComplexityBucketUnclassifiedAllowlist.Where(c => !actual.Contains(c)).ToList();

        Assert.True(stale.Count == 0,
            $"Allowlist entry no longer ReductionComplexityBucket.Unclassified (already classified, or " +
            $"renamed/removed/failed to instantiate) — delete from ComplexityBucketUnclassifiedAllowlist " +
            $"in ReductionType_Tests.cs: {string.Join(", ", stale)}.");
    }

    // ── Ratchet pair #3: complexity != "" ────────────────────────────────────────
    //
    // NoNewUndeclaredComplexity:            actualUndeclared ⊆ Allowlist
    // AllowlistHasNoStaleComplexityEntries:  Allowlist ⊆ actualUndeclared
    //
    // complexity is a free-text Big-O string, not an enum -- "undeclared" here means
    // still an empty string (ReductionTypeCatalog.BuildComplexity's not-present
    // fallback). Every real reduction was read directly and given a confidently-known
    // Big-O string as part of the reduction Big-O backfill pass (see each reduction's
    // own "Declared, not derived" comment on its `cost`/`reductionType`/
    // `complexityBucket` block) -- this allowlist should stay empty.
    private static readonly string[] ComplexityUnclassifiedAllowlist =
    {
    };

    private static HashSet<string> ActualComplexityUndeclared() =>
        ReductionTypeCatalog.ComplexityByClassName.Value
            .Where(kv => string.IsNullOrEmpty(kv.Value))
            .Select(kv => kv.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void NoNewUndeclaredComplexity() {
        var actual = ActualComplexityUndeclared();
        var allowlist = new HashSet<string>(ComplexityUnclassifiedAllowlist, StringComparer.OrdinalIgnoreCase);
        var unexpected = actual.Where(c => !allowlist.Contains(c)).OrderBy(c => c, StringComparer.Ordinal).ToList();

        Assert.True(unexpected.Count == 0,
            $"Reduction(s) have an empty complexity string without being added to the allowlist: " +
            $"{string.Join(", ", unexpected)}. Either declare a confidently-known Big-O string by reading " +
            "reduce() (see the header of this file), or add the class to ComplexityUnclassifiedAllowlist " +
            "in ReductionType_Tests.cs.");
    }

    [Fact]
    public void AllowlistHasNoStaleComplexityEntries() {
        var actual = ActualComplexityUndeclared();
        var stale = ComplexityUnclassifiedAllowlist.Where(c => !actual.Contains(c)).ToList();

        Assert.True(stale.Count == 0,
            $"Allowlist entry no longer has an empty complexity string (already classified, or " +
            $"renamed/removed/failed to instantiate) — delete from ComplexityUnclassifiedAllowlist in " +
            $"ReductionType_Tests.cs: {string.Join(", ", stale)}.");
    }

    // ── MostEfficient ranking sanity check ──────────────────────────────────────
    //
    // Not a ratchet — a direct check that ReductionEfficiency.MostEfficient (issue
    // #376's "default to most efficient reduction") actually prefers a faster
    // complexityBucket over a cheaper cost, and that cost only breaks ties within the
    // same complexityBucket. Regression guard for the ranking rule itself, independent
    // of any specific reduction's classification.
    // Implements the plain (non-generic) IReduction directly rather than
    // IReduction&lt;T,U&gt;, which requires real IProblem type arguments this test has no
    // need to construct — reduce() is never called here, only the declared
    // cost/complexityBucket properties ReductionEfficiency ranks on.
    //
    // Non-public constructor is deliberate, not incidental: ProblemProvider.Reductions
    // (AdditionalControllers/ProblemProvider.cs) reflects over EVERY loaded assembly's
    // types assignable to IReduction, including this test assembly -- so without this,
    // FakeReduction would leak into ReductionTypeCatalog/ReductionCostCatalog and fail
    // the NoNewUndeclared* ratchet tests above. Activator.CreateInstance(type) (used by
    // those catalogs) only finds PUBLIC constructors by default, so `internal` (rather
    // than `private`, which C# doesn't expose to the enclosing type across a nested
    // class boundary) makes every catalog's per-type try/catch skip this class, same as
    // any other reduction that can't be default-constructed, while still being callable
    // from test methods in this same assembly.
    private sealed class FakeReduction : IReduction {
        internal FakeReduction() { }

        public string reductionName => "Fake";
        public string reductionDefinition => "";
        public string source => "";
        public string[] contributors => Array.Empty<string>();
        public IVisualization visualization => null!;
        public List<API.Interfaces.JSON_Objects.Gadget> gadgets => new();
        public IProblem reductionFrom => null!;
        public IProblem reductionTo => null!;
        public IProblem reduce() => throw new NotImplementedException();
        public string mapSolutions(string problemFromSolution) => "";
        public ReductionCost cost { get; init; } = ReductionCost.Unclassified;
        public ReductionComplexityBucket complexityBucket { get; init; } = ReductionComplexityBucket.Unclassified;
    }

    [Fact]
    public void MostEfficient_PrefersFasterComplexityBucketOverCheaperCost() {
        var slowButSmall = new FakeReduction { complexityBucket = ReductionComplexityBucket.Exponential, cost = ReductionCost.Linear };
        var fastButBig = new FakeReduction { complexityBucket = ReductionComplexityBucket.Linear, cost = ReductionCost.HigherPolynomial };

        var best = ReductionEfficiency.MostEfficient(new IReduction[] { slowButSmall, fastButBig });

        Assert.Same(fastButBig, best);
    }

    [Fact]
    public void MostEfficient_UsesCostAsTiebreakWithinSameComplexityBucket() {
        var cheaper = new FakeReduction { complexityBucket = ReductionComplexityBucket.Polynomial, cost = ReductionCost.Linear };
        var pricier = new FakeReduction { complexityBucket = ReductionComplexityBucket.Polynomial, cost = ReductionCost.HigherPolynomial };

        var best = ReductionEfficiency.MostEfficient(new IReduction[] { pricier, cheaper });

        Assert.Same(cheaper, best);
    }

    [Fact]
    public void MostEfficient_ClassifiedAlwaysBeatsUnclassified() {
        var unclassified = new FakeReduction();
        var classified = new FakeReduction { complexityBucket = ReductionComplexityBucket.Exponential, cost = ReductionCost.HigherPolynomial };

        var best = ReductionEfficiency.MostEfficient(new IReduction[] { unclassified, classified });

        Assert.Same(classified, best);
    }

    // ── Characterization report ─────────────────────────────────────────────────
    //
    // Always-passing gap-analysis dump, not an enforced check — "characterize rather
    // than count". Visible via `dotnet test -v n` and in CI logs. Deliberately NOT
    // written to a committed file: a committed report rots the moment anyone changes a
    // tag without regenerating it; this regenerates itself on every test run.

    private readonly Xunit.ITestOutputHelper _output;

    public ReductionType_Tests(AppFactory factory, Xunit.ITestOutputHelper output) {
        _client = factory.CreateClient();
        _output = output;
    }

    [Fact]
    public void CharacterizationReport_DumpsPerReductionMetadata() {
        var reductionTypeByClassName = ReductionTypeCatalog.ReductionTypeByClassName.Value;
        var complexityBucketByClassName = ReductionTypeCatalog.ComplexityBucketByClassName.Value;
        var complexityByClassName = ReductionTypeCatalog.ComplexityByClassName.Value;
        var costByClassName = ReductionCostCatalog.ByClassName.Value;

        var allClassNames = reductionTypeByClassName.Keys
            .Union(complexityBucketByClassName.Keys, StringComparer.OrdinalIgnoreCase)
            .Union(complexityByClassName.Keys, StringComparer.OrdinalIgnoreCase)
            .Union(costByClassName.Keys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        _output.WriteLine("== Redux Tag System — reduction classification characterization report ==");
        _output.WriteLine($"Reductions reflected: {allClassNames.Count}");
        _output.WriteLine("");

        foreach (var className in allClassNames) {
            string reductionType = reductionTypeByClassName.TryGetValue(className, out var rt) ? rt : "<not instantiated>";
            string complexityBucket = complexityBucketByClassName.TryGetValue(className, out var cb) ? cb : "<not instantiated>";
            string complexity = complexityByClassName.TryGetValue(className, out var cx)
                ? (string.IsNullOrEmpty(cx) ? "<empty>" : cx)
                : "<not instantiated>";
            string cost = costByClassName.TryGetValue(className, out var c) ? c : "<not instantiated>";

            _output.WriteLine($"--- {className} ---");
            _output.WriteLine($"  reductionType:     {reductionType}");
            _output.WriteLine($"  complexityBucket:  {complexityBucket}");
            _output.WriteLine($"  complexity:        {complexity}");
            _output.WriteLine($"  cost:              {cost}");
        }

        // Always passes — this Fact exists to produce the dump above, not to assert.
        Assert.True(true);
    }
}
