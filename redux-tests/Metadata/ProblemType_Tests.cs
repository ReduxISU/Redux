using System.Net;
using System.Text.Json;
using API.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Guards for the ProblemType vocabulary — same convention as ComplexityClass_Tests.cs.
public class ProblemType_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public ProblemType_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    // ── Risk: enums must serialize as strings, not integers ───────────────────
    [Fact]
    public async Task Info_SerializesProblemTypeAsString() {
        var response = await _client.GetAsync(
            "/ProblemProvider/info?interface=SAT3",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(body);

        Assert.True(doc.RootElement.TryGetProperty("problemType", out var typeProp),
            $"Expected a problemType property in the /ProblemProvider/info response. Body:\n{body}");
        Assert.Equal(JsonValueKind.String, typeProp.ValueKind);
        Assert.Equal(nameof(ProblemType.Logic), typeProp.GetString());
    }

    [Fact]
    public async Task AllInfo_SerializesEveryProblemTypeAsString() {
        var response = await _client.GetAsync("/Navigation/Batch/allInfo", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var map = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(body);
        Assert.NotNull(map);

        int checkedCount = 0;
        foreach (var (className, element) in map!) {
            if (element.ValueKind != JsonValueKind.Object) continue;
            if (!element.TryGetProperty("problemType", out var typeProp)) continue;

            checkedCount++;
            Assert.True(typeProp.ValueKind == JsonValueKind.String,
                $"{className}.problemType serialized as {typeProp.ValueKind}, expected String.");
        }

        Assert.True(checkedCount > 0, "Found no problemType properties in /Navigation/Batch/allInfo.");
    }

    // ── Ratchet pair: problemType != Unclassified ──────────────────────────────
    //
    // Every top-level problem was classified in one pass (Problem Type filter menu
    // work). Convex Hull and the 5 quantum-oracle problems landed on Miscellaneous —
    // deliberately, per the header of Interfaces/ProblemType.cs — not left Unclassified.
    private static readonly string[] UnclassifiedAllowlist =
    {
    };

    private static HashSet<string> ActualUndeclared() =>
        MetadataReflection.TopLevelClassNames
            .Where(name =>
                MetadataReflection.Instances.TryGetValue(name, out var instance)
                && instance.problemType == ProblemType.Unclassified)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void NoNewUndeclared() {
        var actual = ActualUndeclared();
        var allowlist = new HashSet<string>(UnclassifiedAllowlist, StringComparer.OrdinalIgnoreCase);
        var unexpected = actual.Where(c => !allowlist.Contains(c)).OrderBy(c => c, StringComparer.Ordinal).ToList();

        Assert.True(unexpected.Count == 0,
            $"Problem(s) declared ProblemType.Unclassified without being added to the allowlist: " +
            $"{string.Join(", ", unexpected)}. Either declare a real ProblemType, or add the class to " +
            "UnclassifiedAllowlist in ProblemType_Tests.cs.");
    }

    [Fact]
    public void AllowlistHasNoStaleEntries() {
        var actual = ActualUndeclared();
        var stale = UnclassifiedAllowlist.Where(c => !actual.Contains(c)).ToList();

        Assert.True(stale.Count == 0,
            $"Allowlist entry no longer ProblemType.Unclassified (already classified, or " +
            $"renamed/removed/failed to instantiate) — delete from UnclassifiedAllowlist in " +
            $"ProblemType_Tests.cs: {string.Join(", ", stale)}.");
    }
}
