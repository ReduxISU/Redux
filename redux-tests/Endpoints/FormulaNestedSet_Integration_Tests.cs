using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Formula (#925) and Nested Set (#926) pictures where they meet other branches' work: /info's pictureType (#841) and
// Karp's SAT to 3SAT Reduction View with the Formula on both sides (its construction steps and backward map, #693). Neither
// is on the picture branches' base, so these checks live here until both reach CSharpAPI.
public class FormulaNestedSet_Integration_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public FormulaNestedSet_Integration_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private async Task<JsonElement> Post(string path, string query, string instance) {
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/{path}?{query}", content, TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    // ---- /info ----

    [Theory]
    [InlineData("SatFormulaVisualization", "BooleanSatisfiability", "Formula")]
    [InlineData("Sat3FormulaVisualization", "BooleanSatisfiability", "Formula")]
    [InlineData("SetCoverNestedSetVisualization", "SetD3", "NestedSet")]
    [InlineData("ExactCoverNestedSetVisualization", "SetD3", "NestedSet")]
    [InlineData("HittingSetNestedSetVisualization", "SetD3", "NestedSet")]
    public async Task Info_AndAllInfo_ReportTheLegacyTypeAndThePictureType(string vis, string legacy, string picture) {
        var info = JsonDocument.Parse(await _client.GetStringAsync($"/ProblemProvider/info?interface={vis}", TestContext.Current.CancellationToken)).RootElement;
        Assert.Equal(["visualizationName", "visualizationDefinition", "source", "sourceFile", "contributors", "visualizationType", "pictureType", "solver"],
            info.EnumerateObject().Select(p => p.Name));
        Assert.Equal((legacy, picture), (info.GetProperty("visualizationType").GetString(), info.GetProperty("pictureType").GetString()));
        // allInfo lists these picture-type views only for clients that opt in (#929).
        var all = JsonDocument.Parse(await _client.GetStringAsync("/Navigation/Batch/allInfo?includePictures=true", TestContext.Current.CancellationToken)).RootElement;
        Assert.Equal((legacy, picture), (all.GetProperty(vis).GetProperty("visualizationType").GetString(), all.GetProperty(vis).GetProperty("pictureType").GetString()));
        var gui = JsonDocument.Parse(await _client.GetStringAsync("/Navigation/Batch/allInfo", TestContext.Current.CancellationToken)).RootElement;
        Assert.False(gui.TryGetProperty(vis, out _), $"{vis} is in the allInfo Redux_GUI reads");
    }

    // ---- SAT to 3SAT, both sides as formulas ----

    private const string Karp = "KarpSATToSAT3";
    // C1 has five literals, so it is split into a chain of three clauses joined by two new variables; C2 is padded, C3 tripled.
    private const string Mixed = "(x1 | !x2 | x3 | x4 | !x5) & (!x1 | x2) & (x3)";
    private const string BothFormulas = "&sourceVisualization=SatFormulaVisualization&targetVisualization=Sat3FormulaVisualization";

    private static List<JsonElement> Frames(JsonElement view) => view.GetProperty("frames").EnumerateArray().ToList();

    private static HashSet<string> Ids(JsonElement e, HashSet<string>? ids = null) {
        ids ??= new HashSet<string>(StringComparer.Ordinal);
        if (e.ValueKind == JsonValueKind.Object) {
            foreach (var p in e.EnumerateObject()) {
                if (p.Name == "id" && p.Value.ValueKind == JsonValueKind.String) ids.Add(p.Value.GetString()!);
                else Ids(p.Value, ids);
            }
        } else if (e.ValueKind == JsonValueKind.Array) {
            foreach (var item in e.EnumerateArray()) Ids(item, ids);
        }
        return ids;
    }

    private static Dictionary<string, bool> Assignment(JsonElement pane) =>
        pane.GetProperty("assignment").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetBoolean());

    [Theory]
    [InlineData(Mixed)]
    [InlineData("(x1 | !x2 | x3) & (!x1 | x3 | x4) & (x2 | !x3 | !x4 | x5)")]
    public async Task SatToSat3_BothSidesAsFormulas_RevealTheTargetByItsNodes_AndAgreeWithTheClauseGraphs(string instance) {
        var view = await Post("reductionView", $"reduction={Karp}{BothFormulas}", instance);
        var graphs = await Post("reductionView", $"reduction={Karp}", instance);
        Assert.Equal("SatFormulaVisualization", view.GetProperty("source").GetProperty("visualization").GetString());
        Assert.Equal("Sat3FormulaVisualization", view.GetProperty("target").GetProperty("visualization").GetString());
        Assert.Equal("Formula", view.GetProperty("source").GetProperty("picture").GetProperty("type").GetString());
        Assert.Equal("Formula", view.GetProperty("target").GetProperty("picture").GetProperty("type").GetString());
        // The defaults are unchanged: without the parameters both sides are the clause graph.
        Assert.Equal("GroupedGraph", graphs.GetProperty("source").GetProperty("picture").GetProperty("type").GetString());
        Assert.Equal("GroupedGraph", graphs.GetProperty("target").GetProperty("picture").GetProperty("type").GetString());
        // The gadgets name the Formula's own ids on both sides.
        var sourceIds = Ids(view.GetProperty("source").GetProperty("picture").GetProperty("payload"));
        var targetIds = Ids(view.GetProperty("target").GetProperty("picture").GetProperty("payload"));
        foreach (var g in view.GetProperty("gadgets").EnumerateArray()) {
            Assert.All(g.GetProperty("sourceIds").EnumerateArray(), id => Assert.Contains(id.GetString()!, sourceIds));
            Assert.All(g.GetProperty("targetIds").EnumerateArray(), id => Assert.Contains(id.GetString()!, targetIds));
        }

        // The build reveals the target's clauses, literals and variables step by step, and no edges: the Formula has none.
        var target = view.GetProperty("target");
        Assert.Equal("pieces", target.GetProperty("buildReveal").GetString());
        Assert.Empty(target.GetProperty("buildOrder").GetProperty("edges").EnumerateArray());
        var order = target.GetProperty("buildOrder").GetProperty("nodes").EnumerateArray().Select(e => e.GetString()!).ToList();
        Assert.Equal(order.Count, order.Distinct().Count());
        Assert.Equal(targetIds.OrderBy(i => i, StringComparer.Ordinal), order.OrderBy(i => i, StringComparer.Ordinal));
        var build = Frames(view).Where(f => f.GetProperty("phase").GetString() == "build").ToList();
        var built = build.Select(f => f.GetProperty("target").GetProperty("builtNodes").GetInt32()).ToList();
        Assert.Equal(built.Order(), built);
        Assert.Equal(order.Count, built[^1]);
        Assert.All(build, f => Assert.Equal(0, f.GetProperty("target").GetProperty("builtEdges").GetInt32()));
        // The clause graph's own view still reveals its edges.
        Assert.NotEmpty(graphs.GetProperty("target").GetProperty("buildOrder").GetProperty("edges").EnumerateArray());

        // Every frame says what the clause graphs' frame says; each pane is the clause graph's, plus the variables. The one
        // difference is the edges a build step adds: the clause graph focuses them, the Formula has none to focus (#929).
        var f = Frames(view);
        var g2 = Frames(graphs);
        Assert.Equal(g2.Count, f.Count);
        for (int i = 0; i < f.Count; i++) {
            foreach (var key in new[] { "phase", "event", "caption", "step", "sourceFocus" })
                Assert.Equal(g2[i].GetProperty(key).GetRawText(), f[i].GetProperty(key).GetRawText());
            Assert.Equal(g2[i].GetProperty("targetFocus").EnumerateArray().Select(e => e.GetString()!).Where(targetIds.Contains),
                f[i].GetProperty("targetFocus").EnumerateArray().Select(e => e.GetString()!));
            foreach (var side in new[] { "source", "target" })
                foreach (var key in new[] { "nodes", "groups", "assignment" })
                    Assert.Equal(g2[i].GetProperty(side).GetProperty(key).GetRawText(), f[i].GetProperty(side).GetProperty(key).GetRawText());
            Assert.True(f[i].GetProperty("source").TryGetProperty("variables", out _));
            Assert.True(f[i].GetProperty("target").TryGetProperty("variables", out _));
        }

        // Map back: one Done, the last frame, ok; the SAT answer agrees with the 3SAT answer on every original variable, and the
        // new variables of the splits stay on the 3SAT side.
        var done = Assert.Single(f, x => x.GetProperty("event").GetString() == "Done");
        Assert.Equal(f[^1].GetRawText(), done.GetRawText());
        Assert.True(done.GetProperty("ok").GetBoolean());
        var sat = Assignment(done.GetProperty("source"));
        var sat3 = Assignment(done.GetProperty("target"));
        var original = view.GetProperty("source").GetProperty("picture").GetProperty("payload").GetProperty("variables")
            .EnumerateArray().Select(v => v.GetProperty("id").GetString()!).ToList();
        Assert.Equal(original.Order(), sat.Keys.Order());
        Assert.All(original, v => Assert.Equal(sat3[v], sat[v]));
        Assert.All(sat3.Keys.Except(original), v => Assert.DoesNotContain(v, sat.Keys));
        // The confirmed answer on the SAT side: true variables Solution, false ones False.
        var chips = done.GetProperty("source").GetProperty("variables").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetProperty("state").GetString());
        Assert.All(sat, p => Assert.Equal(p.Value ? "Solution" : "False", chips[p.Key]));
    }

    [Fact]
    public async Task SatToSat3_TheFormulaOnOneSideOnly_LeavesTheOtherSidesPicture() {
        var view = await Post("reductionView", $"reduction={Karp}&targetVisualization=Sat3FormulaVisualization", Mixed);
        Assert.Equal("GroupedGraph", view.GetProperty("source").GetProperty("picture").GetProperty("type").GetString());
        Assert.Equal("Formula", view.GetProperty("target").GetProperty("picture").GetProperty("type").GetString());
        Assert.Equal("pieces", view.GetProperty("target").GetProperty("buildReveal").GetString());
        var source = await Post("reductionView", $"reduction={Karp}&sourceVisualization=SatFormulaVisualization", Mixed);
        Assert.Equal("Formula", source.GetProperty("source").GetProperty("picture").GetProperty("type").GetString());
        Assert.Equal("GroupedGraph", source.GetProperty("target").GetProperty("picture").GetProperty("type").GetString());
        Assert.NotEmpty(source.GetProperty("target").GetProperty("buildOrder").GetProperty("edges").EnumerateArray());
    }
}
