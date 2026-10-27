using System.Net;
using System.Text.Json;
using API.Interfaces;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Guards for the ProblemVariant tag set (#616) — same ratchet-pair convention as ProblemType_Tests.cs.
//
// *** These declarations are public correctness claims. Decision means the instance carries a bound
// the verifier checks, or the answer is yes/no; Optimization means it asks for the best value. A class
// that genuinely supports both declares Decision | Optimization. Judgement calls carry a comment on the
// declaration for review. ***
public class ProblemVariant_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public ProblemVariant_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Info_WritesProblemVariantsAsAListOnly() {
        var response = await _client.GetAsync("/ProblemProvider/info?interface=MAXCUT", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(body);

        Assert.Equal(new[] { nameof(ProblemVariant.Optimization) },
            doc.RootElement.GetProperty("problemVariants").EnumerateArray().Select(n => n.GetString()));
        Assert.False(doc.RootElement.TryGetProperty("problemVariant", out _),
            $"problemVariant is new, so it has no deprecated single field. Body:\n{body}");
    }

    // ── Ratchet pair: problemVariant != Unclassified ───────────────────────────
    //
    // Every top-level problem was classified in one pass (#616); keep this list empty.
    private static readonly string[] UnclassifiedAllowlist =
    {
    };

    private static HashSet<string> ActualUndeclared() =>
        MetadataReflection.TopLevelClassNames
            .Where(name =>
                MetadataReflection.Instances.TryGetValue(name, out var instance)
                && instance.problemVariant == ProblemVariant.Unclassified)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void NoNewUndeclared() {
        var actual = ActualUndeclared();
        var allowlist = new HashSet<string>(UnclassifiedAllowlist, StringComparer.OrdinalIgnoreCase);
        var unexpected = actual.Where(c => !allowlist.Contains(c)).OrderBy(c => c, StringComparer.Ordinal).ToList();

        Assert.True(unexpected.Count == 0,
            $"Problem(s) declared ProblemVariant.Unclassified: {string.Join(", ", unexpected)}. Declare " +
            "Decision, Optimization, or both (see Interfaces/ProblemVariant.cs), or add the class to " +
            "UnclassifiedAllowlist in ProblemVariant_Tests.cs.");
    }

    [Fact]
    public void AllowlistHasNoStaleEntries() {
        var actual = ActualUndeclared();
        var stale = UnclassifiedAllowlist.Where(c => !actual.Contains(c)).ToList();

        Assert.True(stale.Count == 0,
            $"Allowlist entry no longer ProblemVariant.Unclassified — delete from UnclassifiedAllowlist in " +
            $"ProblemVariant_Tests.cs: {string.Join(", ", stale)}.");
    }
}
