using System.Net;
using System.Text.Json;
using API.Interfaces;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Everything Redux_GUI reads about visualizations, without includePictures=true, is what origin/CSharpAPI answered before the
// visualization redesign (combo branch only). Snapshots/gui-view.csharpapi.json was captured from a build of origin/CSharpAPI
// (f6021880, Oct 14 2026): the visualization listings, the type map and type list, every problem's and every reduction side's
// default visualization, every visualization's name and type, and each contributor's visualizationsCreated.
//
// Documented exceptions:
// - Set Cover's and SAT's defaults are their real visualizations (SetD3, BooleanSatisfiability), not the placeholder. That is
//   open PR #923 (fix/922), made on purpose to fix Redux_GUI; the same change is on feat/664-grouped-graph and
//   feat/665-bipartite-graph. It awaits the user's confirmation.
// - Problems, solvers, verifiers and reductions added since (Minimum TSP, the backtracking solvers, NFA Subset Simulation,
//   Sipser's Clique to 3SAT once its default instance builds) are not visualizations and are not hidden; the test only checks
//   that they bring no visualization Redux_GUI could see.
// - "Hitting Set Visualization" is "Exact Hitting Set Visualization" in the snapshot: open PR #742 (fix/661) renames Hitting
//   Set to Exact Hitting Set (#698) and ships that name to Redux_GUI itself.
// - Fields only added (pictureType, #841) and text (definitions) are not compared.
public class GuiSnapshot_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public GuiSnapshot_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private static readonly JsonElement Snapshot = JsonDocument.Parse(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Endpoints", "Snapshots", "gui-view.csharpapi.json"))).RootElement.Clone();

    // #923: the defaults Redux_GUI is shown for SAT and Set Cover instead of the placeholder.
    private static readonly Dictionary<string, (string Name, string Type, string SourceFile)> Issue923 = new(StringComparer.OrdinalIgnoreCase) {
        ["SAT"] = ("SAT Visualization", "BooleanSatisfiability", "Problems/NPComplete/NPC_SAT/Visualizations/SatDefaultVisualization.cs"),
        ["SETCOVER"] = ("Set Cover Visualization", "SetD3", "Problems/NPComplete/NPC_SETCOVER/Visualizations/SetCoverDefaultVisualization.cs"),
    };

    private async Task<JsonElement> Get(string url) {
        var response = await _client.GetAsync(url, TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static string Canonical(JsonElement e) => e.ValueKind switch {
        JsonValueKind.Object => "{" + string.Join(",", e.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal)
            .Select(p => JsonSerializer.Serialize(p.Name) + ":" + Canonical(p.Value))) + "}",
        JsonValueKind.Array => "[" + string.Join(",", e.EnumerateArray().Select(Canonical)) + "]",
        _ => e.GetRawText(),
    };

    private static (string?, string?) NameAndType(JsonElement vis) =>
        (vis.GetProperty("visualizationName").GetString(), vis.GetProperty("visualizationType").GetString());

    private static (string?, string?) Expected(JsonElement snap, string problemClass) =>
        Issue923.TryGetValue(problemClass, out var fix) ? (fix.Name, fix.Type) : NameAndType(snap);

    [Fact]
    public async Task VisualizationListings_AreCSharpAPIs() {
        Assert.Equal(Canonical(Snapshot.GetProperty("allVisualizations")), Canonical(await Get("/Navigation/Batch/allVisualizations")));
        Assert.Equal(Canonical(Snapshot.GetProperty("allVisualizationTypes")), Canonical(await Get("/Navigation/Batch/allVisualizationTypes")));
        Assert.Equal(Canonical(Snapshot.GetProperty("visualizationTypes")), Canonical(await Get("/Navigation/VisualizationTypes")));
        var all = Snapshot.GetProperty("allVisualizations");
        foreach (var (_, type) in ProblemProvider.Problems) {
            var listed = await Get($"/Navigation/Problem_VisualizationsRefactor/?chosenProblem={type.Name}");
            string expected = all.TryGetProperty(type.Name, out var list) ? Canonical(list) : "[]";
            Assert.True(expected == Canonical(listed), $"{type.Name}: {Canonical(listed)}, CSharpAPI listed {expected}");
        }
    }

    [Fact]
    public async Task EveryDefaultAndVisualizationInAllInfo_IsCSharpAPIs() {
        var allInfo = await Get("/Navigation/Batch/allInfo");
        foreach (var p in Snapshot.GetProperty("defaults").EnumerateObject()) {
            var shown = allInfo.GetProperty(p.Name).GetProperty("defaultVisualization");
            Assert.Equal(Expected(p.Value, p.Name), NameAndType(shown));
            string sourceFile = Issue923.TryGetValue(p.Name, out var fix) ? fix.SourceFile : p.Value.GetProperty("sourceFile").GetString()!;
            Assert.Equal(sourceFile, shown.GetProperty("sourceFile").GetString());
            var info = await Get($"/ProblemProvider/info?interface={p.Name}");
            Assert.Equal(Expected(p.Value, p.Name), NameAndType(info.GetProperty("defaultVisualization")));
        }
        foreach (var v in Snapshot.GetProperty("visualizations").EnumerateObject())
            Assert.Equal(NameAndType(v.Value), NameAndType(allInfo.GetProperty(v.Name)));

        // What CSharpAPI did not have: no visualization, and nothing that shows Redux_GUI a new one as a default.
        var known = Snapshot.GetProperty("visualizations").EnumerateObject().Select(v => v.Name)
            .Concat(Snapshot.GetProperty("defaults").EnumerateObject().Select(p => p.Name)).ToHashSet();
        foreach (var entry in allInfo.EnumerateObject().Where(e => !known.Contains(e.Name))) {
            Assert.False(ProblemProvider.Visualizers.ContainsKey(entry.Name.ToLower()), $"{entry.Name} is a visualization CSharpAPI did not list");
            if (entry.Value.ValueKind == JsonValueKind.Object && entry.Value.TryGetProperty("defaultVisualization", out var newDefault))
                Assert.Equal(("", "Unimplemented"), NameAndType(newDefault));
        }
    }

    [Fact]
    public async Task EveryReductionSidesDefault_IsCSharpAPIs() {
        var allInfo = await Get("/Navigation/Batch/allInfo");
        foreach (var r in Snapshot.GetProperty("reductionDefaults").EnumerateObject()) {
            var red = (IReduction)Activator.CreateInstance(ProblemProvider.Reductions[r.Name.ToLower()])!;
            foreach (var side in r.Value.EnumerateObject()) {
                string problemClass = (side.Name == "reductionFrom" ? red.reductionFrom : red.reductionTo).GetType().Name;
                var shown = allInfo.GetProperty(r.Name).GetProperty(side.Name).GetProperty("defaultVisualization");
                Assert.Equal(Expected(side.Value, problemClass), NameAndType(shown));
            }
        }
    }

    [Fact]
    public async Task EveryContributorsVisualizations_AreCSharpAPIs() {
        foreach (var c in Snapshot.GetProperty("visualizationsCreated").EnumerateObject()) {
            var profile = await Get($"/Navigation/ContributorProfile/{c.Name}");
            var created = profile.GetProperty("visualizationsCreated").EnumerateArray().Select(e => e.GetString()).OrderBy(s => s, StringComparer.Ordinal);
            Assert.Equal(c.Value.EnumerateArray().Select(e => e.GetString()), created);
        }
    }
}
