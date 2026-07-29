using System.Net;
using System.Text.Json;
using API.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Guards for the ComplexityClass vocabulary (plan: Redux Tag System, Phase 4.3-4.5).
//
// *** These declarations are public correctness claims the API begins serving the
// moment this merges (Navigation/{NPC,P,NPHard}_ProblemsRefactor membership changes as
// a direct result — see the membership tests in Navigation_Endpoint_Tests.cs). This
// file — specifically which problems were moved off Unclassified and onto which class
// — needs advisor sign-off before merge, not after. ***
public class ComplexityClass_Tests : IClassFixture<AppFactory>
{
    private readonly HttpClient _client;

    // ── Risk 1: enums must serialize as strings, not integers ─────────────────
    //
    // Same regression class ReductionCost_Tests.cs / VisualizationType_Tests.cs guard
    // against: Newtonsoft serializes enums as integers by default. The
    // [JsonConverter]/[StringEnumConverter] attributes pinned on ComplexityClass
    // (Interfaces/ComplexityClass.cs) are the actual fix; these are the regression guard.

    [Fact]
    public async Task Info_SerializesComplexityClassAsString()
    {
        var response = await _client.GetAsync(
            "/ProblemProvider/info?interface=SAT3",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(body);

        Assert.True(doc.RootElement.TryGetProperty("complexityClass", out var classProp),
            $"Expected a complexityClass property in the /ProblemProvider/info response. Body:\n{body}");
        Assert.Equal(JsonValueKind.String, classProp.ValueKind);
        Assert.Equal(nameof(ComplexityClass.NPComplete), classProp.GetString());
    }

    [Fact]
    public async Task AllInfo_SerializesEveryComplexityClassAsString()
    {
        var response = await _client.GetAsync("/Navigation/Batch/allInfo", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var map = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(body);
        Assert.NotNull(map);

        int checkedCount = 0;
        foreach (var (className, element) in map!)
        {
            if (element.ValueKind != JsonValueKind.Object) continue;
            if (!element.TryGetProperty("complexityClass", out var classProp)) continue;

            checkedCount++;
            Assert.True(classProp.ValueKind == JsonValueKind.String,
                $"{className}.complexityClass serialized as {classProp.ValueKind}, expected String. This " +
                "is the Risk-1 regression: Newtonsoft defaults to serializing enums as integers unless " +
                "StringEnumConverter is pinned on ComplexityClass.");
        }

        Assert.True(checkedCount > 0, "Found no complexityClass properties in /Navigation/Batch/allInfo.");
    }

    // ── Ratchet pair: complexityClass != Unclassified ──────────────────────────
    //
    // NoNewUndeclared:            actualUndeclared ⊆ Allowlist  — a new problem cannot
    //                              be born undeclared.
    // AllowlistHasNoStaleEntries: Allowlist ⊆ actualUndeclared  — classifying a problem
    //                              forces deleting its allowlist line.
    //
    // Scoped to top-level problems only (MetadataReflection.TopLevelClassNames):
    // nested helper problems (…Inherited.SipserClique, …ReduceTo.*) are not listed by
    // any Navigation endpoint and were never in scope for Phase 4.3's declaration pass.

    // Empty as of the solver Big-O backfill pass: the last four entries (CUT,
    // WEIGHTEDCUT, NQUEENS, LOSSLESSDATACOMPRESSION) were resolved by reading what
    // their code actually models -- see each problem class's own "Declared, not
    // derived" comment for the reasoning. Add a name here only when resolving it
    // genuinely requires reading the code (not a literature lookup), per the header
    // of Interfaces/ComplexityClass.cs and the plan, Phase 4.3.
    private static readonly string[] UnclassifiedAllowlist =
    {
    };

    private static HashSet<string> ActualUndeclared() =>
        MetadataReflection.TopLevelClassNames
            .Where(name =>
                MetadataReflection.Instances.TryGetValue(name, out var instance)
                && instance.complexityClass == ComplexityClass.Unclassified)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void NoNewUndeclared()
    {
        var actual = ActualUndeclared();
        var allowlist = new HashSet<string>(UnclassifiedAllowlist, StringComparer.OrdinalIgnoreCase);
        var unexpected = actual.Where(c => !allowlist.Contains(c)).OrderBy(c => c, StringComparer.Ordinal).ToList();

        Assert.True(unexpected.Count == 0,
            $"Problem(s) declared ComplexityClass.Unclassified without being added to the allowlist: " +
            $"{string.Join(", ", unexpected)}. Either declare a real ComplexityClass (needs advisor " +
            "sign-off — see the header of this file), or add the class to UnclassifiedAllowlist in " +
            "ComplexityClass_Tests.cs.");
    }

    [Fact]
    public void AllowlistHasNoStaleEntries()
    {
        var actual = ActualUndeclared();
        var stale = UnclassifiedAllowlist.Where(c => !actual.Contains(c)).ToList();

        Assert.True(stale.Count == 0,
            $"Allowlist entry no longer ComplexityClass.Unclassified (already classified, or " +
            $"renamed/removed/failed to instantiate) — delete from UnclassifiedAllowlist in " +
            $"ComplexityClass_Tests.cs: {string.Join(", ", stale)}.");
    }

    // ── Characterization report ─────────────────────────────────────────────────
    //
    // Always-passing gap-analysis dump, not an enforced check — "characterize rather
    // than count". Visible via `dotnet test -v n` and in CI logs. Deliberately NOT
    // written to a committed file: a committed report rots the moment anyone changes
    // a tag without regenerating it; this regenerates itself on every test run.

    private readonly Xunit.ITestOutputHelper _output;

    public ComplexityClass_Tests(AppFactory factory, Xunit.ITestOutputHelper output)
    {
        _client = factory.CreateClient();
        _output = output;
    }

    [Fact]
    public void CharacterizationReport_DumpsPerProblemMetadata()
    {
        var solversByProblem = SolverNavigationData.Entries
            .GroupBy(e => e.problemName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(e => e.className).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList(), StringComparer.OrdinalIgnoreCase);

        var visualizationsByProblem = VisualizationNavigationData.Entries
            .GroupBy(e => e.problemName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(e => e.className).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList(), StringComparer.OrdinalIgnoreCase);

        var visTypeByClassName = VisualizationTypeCatalog.ByClassName.Value;

        _output.WriteLine("== Redux Tag System — Phase 4 characterization report ==");
        _output.WriteLine($"Top-level problems reflected: {MetadataReflection.TopLevelClassNames.Count}");
        _output.WriteLine($"Instantiation failures: {MetadataReflection.Failures.Count}");
        _output.WriteLine("");

        foreach (var name in MetadataReflection.TopLevelClassNames.OrderBy(n => n, StringComparer.Ordinal))
        {
            _output.WriteLine($"--- {name} ---");

            if (!MetadataReflection.Instances.TryGetValue(name, out var instance))
            {
                var ex = MetadataReflection.Failures.TryGetValue(name, out var failure) ? failure.Message : "(unknown)";
                _output.WriteLine($"  complexityClass: <could not instantiate: {ex}>");
                continue;
            }

            _output.WriteLine($"  complexityClass: {instance.complexityClass}");

            if (visualizationsByProblem.TryGetValue(name, out var visClasses) && visClasses.Count > 0)
            {
                foreach (var visClass in visClasses)
                {
                    string type = visTypeByClassName.TryGetValue(visClass, out var t) ? t : "<unknown>";
                    bool renderable = type != nameof(VisualizationType.Unimplemented) && type != "<unknown>";
                    _output.WriteLine($"  visualization: {visClass} (type={type}, renderable={renderable})");
                }
            }
            else
            {
                _output.WriteLine("  visualization: <none>");
            }

            if (solversByProblem.TryGetValue(name, out var solverClasses) && solverClasses.Count > 0)
            {
                foreach (var solverClass in solverClasses)
                {
                    _output.WriteLine($"  solver: {solverClass}");
                }
            }
            else
            {
                _output.WriteLine("  solver: <none>");
            }
        }

        // Always passes — this Fact exists to produce the dump above, not to assert.
        Assert.True(true);
    }
}
