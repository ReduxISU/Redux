using System.Net;
using System.Text.Json;
using API.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Guards for the VisualizationType vocabulary (plan: Redux Tag System, Phase 1.6-1.8).
//
// Risk 1 (the highest-risk item in the plan): System.Text.Json serializes enums as integers
// by default. Ship without the JsonStringEnumConverter pinned on VisualizationType and every
// visualization in the app goes blank, because the GUI does
// Visualizations.get(visualizationType) on what it expects to be a string. The first two
// test groups below are the regression guard for that; they must never go back to failing.
public class VisualizationType_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public VisualizationType_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    // ── Risk 1: enums must serialize as strings, not integers ─────────────────

    [Fact]
    public async Task Info_SerializesVisualizationTypeAsString() {
        var response = await _client.GetAsync(
            "/ProblemProvider/info?interface=CliqueDefaultVisualization",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(body);

        Assert.True(doc.RootElement.TryGetProperty("visualizationType", out var typeProp),
            $"Expected a visualizationType property in the /ProblemProvider/info response. Body:\n{body}");
        Assert.Equal(JsonValueKind.String, typeProp.ValueKind);
        Assert.Equal("GraphD3", typeProp.GetString());
    }

    [Fact]
    public async Task AllInfo_SerializesEveryVisualizationTypeAsString() {
        var response = await _client.GetAsync("/Navigation/Batch/allInfo", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var map = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(body);
        Assert.NotNull(map);

        var manifest = new HashSet<string>(ReadManifest(), StringComparer.Ordinal);
        int checkedCount = 0;

        foreach (var (className, element) in map!) {
            if (element.ValueKind != JsonValueKind.Object) continue;
            if (!element.TryGetProperty("visualizationType", out var typeProp)) continue;

            checkedCount++;
            Assert.True(typeProp.ValueKind == JsonValueKind.String,
                $"{className}.visualizationType serialized as {typeProp.ValueKind}, expected String. " +
                "This is the Risk-1 regression: System.Text.Json serializes enums as integers by default " +
                "unless JsonStringEnumConverter<VisualizationType> is pinned on VisualizationType.");

            var value = typeProp.GetString();
            Assert.True(value is not null && manifest.Contains(value),
                $"{className}.visualizationType = \"{value}\" is not in the committed manifest " +
                $"(Documentation/visualization-types.json).");
        }

        // Guards against the property name silently disappearing (e.g. a rename) and
        // this test degrading into a no-op that always passes.
        Assert.True(checkedCount > 0, "Found no visualizationType properties in /Navigation/Batch/allInfo.");
    }

    // ── Manifest: enum is the source of truth, JSON is the committed mirror ───

    // The manifest mirrors the members Redux_GUI is shown (VisualizationTypeCatalog.GuiWireValues), not the picture types a
    // client sees only with includePictures=true, since Redux_GUI's manifest-drift job compares its copy against this file.
    [Fact]
    public void ManifestMatchesEnum() {
        string manifestJson = ReadManifestRaw();
        string[] manifest = ReadManifest();
        string[] actual = VisualizationTypeCatalog.Gui();

        bool matches = manifest.Length == actual.Length
            && manifest.SequenceEqual(actual, StringComparer.Ordinal);

        Assert.True(matches,
            "VisualizationType no longer matches the committed manifest at " +
            "Documentation/visualization-types.json.\n" +
            $"Expected manifest contents:\n{manifestJson}\n" +
            $"Actual VisualizationType catalog: {JsonSerializer.Serialize(actual)}\n" +
            "Adding (or removing) a VisualizationType member requires a companion Redux_GUI PR " +
            "updating components/Visualization/svgs/visualizationTypes.json and Visualizations.js, " +
            "and then updating Documentation/visualization-types.json to match.");
    }

    private static string ReadManifestRaw() =>
        File.ReadAllText(Path.Combine(ProjectSourcePath.Value, "Documentation", "visualization-types.json"));

    private static string[] ReadManifest() =>
        JsonSerializer.Deserialize<string[]>(ReadManifestRaw())
        ?? throw new InvalidOperationException("Documentation/visualization-types.json did not parse as a string array.");

    // ── Ratchet pair: visualizationType != Unimplemented ───────────────────────
    //
    // NoNewUndeclared:            actualUndeclared ⊆ Allowlist  — a new class cannot be
    //                              born undeclared.
    // AllowlistHasNoStaleEntries: Allowlist ⊆ actualUndeclared  — classifying something
    //                              forces deleting its allowlist line.

    private static readonly string[] UnimplementedAllowlist =
    {
        "ConvexHullVisualization",
        "SudokuVisualization",
        "LosslessDataCompressionVisualization",
        "DummyVisualization",
    };

    private static HashSet<string> ActualUndeclared() =>
        VisualizationTypeCatalog.ByClassName.Value
            .Where(kv => kv.Value == nameof(VisualizationType.Unimplemented))
            .Select(kv => kv.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void NoNewUndeclared() {
        var actual = ActualUndeclared();
        var allowlist = new HashSet<string>(UnimplementedAllowlist, StringComparer.OrdinalIgnoreCase);
        var unexpected = actual.Where(c => !allowlist.Contains(c)).OrderBy(c => c, StringComparer.Ordinal).ToList();

        Assert.True(unexpected.Count == 0,
            $"Class(es) declared VisualizationType.Unimplemented without being added to the allowlist: " +
            $"{string.Join(", ", unexpected)}. Either declare a real VisualizationType, or add the class " +
            "to UnimplementedAllowlist in VisualizationType_Tests.cs.");
    }

    [Fact]
    public void AllowlistHasNoStaleEntries() {
        var actual = ActualUndeclared();
        var stale = UnimplementedAllowlist.Where(c => !actual.Contains(c)).ToList();

        Assert.True(stale.Count == 0,
            $"Allowlist entry no longer VisualizationType.Unimplemented (already classified, or renamed/removed) " +
            $"— delete from UnimplementedAllowlist in VisualizationType_Tests.cs: {string.Join(", ", stale)}.");
    }

    // ── A problem that has a real visualization must default to one ───────────
    //
    // The API reports a problem's defaultVisualization in /ProblemProvider/info, and
    // visualizeReduction draws a reduction's target with it. A problem that defaults to
    // DummyVisualization shows "Unimplemented" there even when a working visualization
    // for it exists (#922: SAT and Set Cover).

    private static readonly string[] DefaultsToPlaceholderAllowlist =
    {
        // Both Deutsch visualizations ask the external quantum server for the circuit, so
        // making one the default would put that network call on /info's default path.
        // Choosing Deutsch's default is a separate decision from #922.
        "DEUTSCH",
    };

    [Fact]
    public void ProblemsWithARealVisualization_DoNotDefaultToUnimplemented() {
        var visTypeByClassName = VisualizationTypeCatalog.ByClassName.Value;
        var problemsWithARealVisualization = VisualizationNavigationData.Entries
            .Where(e => visTypeByClassName.TryGetValue(e.className, out var type)
                && type != nameof(VisualizationType.Unimplemented))
            .Select(e => e.problemName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var allowlist = new HashSet<string>(DefaultsToPlaceholderAllowlist, StringComparer.OrdinalIgnoreCase);

        var offenders = MetadataReflection.TopLevelClassNames
            .Where(problemsWithARealVisualization.Contains)
            .Where(name => !allowlist.Contains(name))
            .Where(name => MetadataReflection.Instances.TryGetValue(name, out var problem)
                && problem.defaultVisualization.visualizationType == VisualizationType.Unimplemented)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(offenders.Count == 0,
            $"Problem(s) with a real visualization still default to an Unimplemented one: " +
            $"{string.Join(", ", offenders)}. Point defaultVisualization at the real visualization.");
    }

    [Theory]
    [InlineData("SAT", "BooleanSatisfiability")]
    [InlineData("SETCOVER", "SetD3")]
    public async Task Info_ReportsARealDefaultVisualization(string problem, string expectedType) {
        var response = await _client.GetAsync(
            $"/ProblemProvider/info?interface={problem}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(body);
        string? type = doc.RootElement.GetProperty("defaultVisualization").GetProperty("visualizationType").GetString();

        Assert.Equal(expectedType, type);
    }
}
