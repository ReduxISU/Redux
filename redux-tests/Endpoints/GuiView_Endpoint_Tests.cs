using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// A client that has not opted in with includePictures=true (Redux_GUI) is shown what the API answered before the visualization
// redesign (GuiView): a problem whose default is an opt-in view shows the placeholder default, a visualization with a
// LegacyVisualizationTypeAttribute reports that type, an opt-in view is not listed, and /Navigation/VisualizationTypes lists
// only the wire values Redux_GUI can draw. These hold for any set of opt-in views and legacy types, empty included; the branches
// that add them pin their own cases.
public class GuiView_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public GuiView_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private async Task<JsonElement> Get(string url) {
        var response = await _client.GetAsync(url, TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<(HttpStatusCode Status, string Body)> Post(string url, string instance) {
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync(url, content, TestContext.Current.CancellationToken);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private static IEnumerable<IProblem> Problems() {
        foreach (var (_, type) in ProblemProvider.Problems) {
            IProblem? problem = null;
            try { problem = Activator.CreateInstance(type) as IProblem; } catch { }
            if (problem != null) yield return problem;
        }
    }

    // What a client is told a visualization is called and what type it has.
    private static (string Name, string Type) Seen(IVisualization vis, bool includePictures) {
        var shown = GuiView.Shown(vis, includePictures);
        return (shown.visualizationName, GuiView.TypeOf(shown, includePictures).ToString());
    }

    private static (string Name, string Type) Read(JsonElement vis) =>
        (vis.GetProperty("visualizationName").GetString()!, vis.GetProperty("visualizationType").GetString()!);

    [Fact]
    public async Task VisualizationTypes_ListsWhatRedux_GUICanDraw_UnlessAskedForAll() {
        var plain = (await Get("/Navigation/VisualizationTypes")).EnumerateArray().Select(e => e.GetString()!).ToArray();
        var all = (await Get("/Navigation/VisualizationTypes?includePictures=true")).EnumerateArray().Select(e => e.GetString()!).ToArray();
        Assert.Equal(VisualizationTypeCatalog.Gui(), plain);
        Assert.Equal(VisualizationTypeCatalog.All(), all);
        Assert.All(VisualizationTypeCatalog.GuiWireValues, v => Assert.True(Enum.IsDefined(typeof(VisualizationType), v), v));
    }

    [Fact]
    public async Task AVisualizationRedux_GUISees_HasATypeItCanDraw() {
        var types = await Get("/Navigation/Batch/allVisualizationTypes");
        Assert.All(types.EnumerateObject(), p => Assert.Contains(p.Value.GetString()!, VisualizationTypeCatalog.GuiWireValues));
        foreach (var problem in Problems()) {
            if (problem.defaultVisualization is { } vis)
                Assert.Contains(Seen(vis, false).Type, VisualizationTypeCatalog.GuiWireValues);
        }
    }

    [Fact]
    public async Task Info_ShowsTheDefaultAndTypeRedux_GUISaw_UnlessAskedFor() {
        foreach (var problem in Problems()) {
            if (problem.defaultVisualization is not { } vis) continue;
            string name = problem.GetType().Name;
            var plain = await Get($"/ProblemProvider/info?interface={name}");
            var withPictures = await Get($"/ProblemProvider/info?interface={name}&includePictures=true");
            Assert.Equal(Seen(vis, false), Read(plain.GetProperty("defaultVisualization")));
            Assert.Equal(Seen(vis, true), Read(withPictures.GetProperty("defaultVisualization")));
            Assert.False(OptInVisualizations.IsOptIn(GuiView.Shown(vis, false).GetType()), $"{name} shows an opt-in default");
        }
    }

    [Fact]
    public async Task AllInfo_ShowsTheDefaultAndTypeRedux_GUISaw_UnlessAskedFor() {
        var plain = await Get("/Navigation/Batch/allInfo");
        var withPictures = await Get("/Navigation/Batch/allInfo?includePictures=true");
        foreach (var problem in Problems()) {
            if (problem.defaultVisualization is not { } vis) continue;
            string name = problem.GetType().Name;
            if (!plain.TryGetProperty(name, out var p)) continue;
            Assert.Equal(Seen(vis, false), Read(p.GetProperty("defaultVisualization")));
            Assert.Equal(Seen(vis, true), Read(withPictures.GetProperty(name).GetProperty("defaultVisualization")));
        }
        foreach (var type in ProblemProvider.Visualizers.Values) {
            if (!plain.TryGetProperty(type.Name, out var v)) continue;
            var vis = (IVisualization)Activator.CreateInstance(type)!;
            Assert.Equal(GuiView.TypeOf(vis, false).ToString(), v.GetProperty("visualizationType").GetString());
            Assert.Equal(vis.visualizationType.ToString(), withPictures.GetProperty(type.Name).GetProperty("visualizationType").GetString());
        }
    }

    [Fact]
    public async Task AVisualizationWithALegacyType_ReportsItToRedux_GUI_AndItsOwnOnRequest() {
        foreach (var type in ProblemProvider.Visualizers.Values) {
            if (GuiView.LegacyType(type) is not { } legacy) continue;
            var plain = await Get($"/ProblemProvider/info?interface={type.Name}");
            var withPictures = await Get($"/ProblemProvider/info?interface={type.Name}&includePictures=true");
            var types = await Get("/Navigation/Batch/allVisualizationTypes");
            var typesWithPictures = await Get("/Navigation/Batch/allVisualizationTypes?includePictures=true");
            var own = ((IVisualization)Activator.CreateInstance(type)!).visualizationType.ToString();
            Assert.Equal(legacy.ToString(), plain.GetProperty("visualizationType").GetString());
            Assert.Equal(own, withPictures.GetProperty("visualizationType").GetString());
            Assert.Equal(legacy.ToString(), types.GetProperty(type.Name).GetString());
            Assert.Equal(own, typesWithPictures.GetProperty(type.Name).GetString());
        }
    }

    [Fact]
    public async Task ProblemInstance_ShowsTheDefaultRedux_GUISaw_UnlessAskedFor() {
        foreach (var problem in Problems()) {
            if (problem.defaultVisualization is not { } vis || !OptInVisualizations.IsOptIn(vis.GetType())) continue;
            string name = problem.GetType().Name;
            var plain = await Post($"/ProblemProvider/problemInstance?problem={name}", problem.defaultInstance);
            var withPictures = await Post($"/ProblemProvider/problemInstance?problem={name}&includePictures=true", problem.defaultInstance);
            Assert.True(plain.Status == HttpStatusCode.OK, plain.Body);
            Assert.Equal(Seen(vis, false), Read(JsonDocument.Parse(plain.Body).RootElement.GetProperty("defaultVisualization")));
            Assert.Equal(Seen(vis, true), Read(JsonDocument.Parse(withPictures.Body).RootElement.GetProperty("defaultVisualization")));
        }
    }

    [Fact]
    public async Task Reduce_ShowsTheTargetsDefaultRedux_GUISaw_UnlessAskedFor() {
        foreach (var (_, type) in ProblemProvider.Reductions) {
            IReduction? reduction = null;
            try { reduction = Activator.CreateInstance(type) as IReduction; } catch { }
            if (reduction?.reductionTo.defaultVisualization is not { } vis || !OptInVisualizations.IsOptIn(vis.GetType())) continue;
            var plain = await Post($"/ProblemProvider/reduce?reduction={type.Name}", reduction.reductionFrom.defaultInstance);
            var withPictures = await Post($"/ProblemProvider/reduce?reduction={type.Name}&includePictures=true", reduction.reductionFrom.defaultInstance);
            Assert.True(plain.Status == HttpStatusCode.OK, plain.Body);
            var plainRoot = JsonDocument.Parse(plain.Body).RootElement;
            var picturesRoot = JsonDocument.Parse(withPictures.Body).RootElement;
            Assert.Equal(Seen(vis, false), Read(plainRoot.GetProperty("reductionTo").GetProperty("defaultVisualization")));
            Assert.Equal(Seen(vis, false), Read(plainRoot.GetProperty("visualization")));
            Assert.Equal(Seen(vis, true), Read(picturesRoot.GetProperty("reductionTo").GetProperty("defaultVisualization")));
            Assert.Equal(Seen(vis, true), Read(picturesRoot.GetProperty("visualization")));
        }
    }

    [Fact]
    public async Task ProblemVisualizations_LeavesTheOptInViewsOut_UnlessAskedFor() {
        foreach (var problem in Problems()) {
            string name = problem.GetType().Name;
            var plain = (await Get($"/Navigation/Problem_VisualizationsRefactor?chosenProblem={name}")).EnumerateArray().Select(e => e.GetString()!).ToList();
            var withPictures = (await Get($"/Navigation/Problem_VisualizationsRefactor?chosenProblem={name}&includePictures=true")).EnumerateArray().Select(e => e.GetString()!).ToList();
            Assert.Equal(withPictures.Where(v => !OptInVisualizations.IsOptIn(ProblemProvider.Visualizers[v.ToLower()])), plain);
        }
    }
}
