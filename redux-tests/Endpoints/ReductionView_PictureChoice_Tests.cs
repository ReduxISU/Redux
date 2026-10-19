using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Choosing each side's picture in the Reduction View (#912): sourceVisualization and targetVisualization, the 400s for a
// picture the view cannot use, the echo of what was drawn, and how the build shows a target picture other than the default.
public class ReductionView_PictureChoice_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public ReductionView_PictureChoice_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private const string Sat = "SipserReduceToCliqueStandard", Vc = "sipserReductionVertexCover";

    private async Task<(HttpStatusCode Status, string Body)> Post(string reduction, string instance, string query = "") {
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/reductionView?reduction={reduction}{query}", content,
            TestContext.Current.CancellationToken);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private async Task<JsonElement> View(string reduction, string instance, string query = "") {
        var (status, body) = await Post(reduction, instance, query);
        Assert.True(status == HttpStatusCode.OK, body);
        return JsonDocument.Parse(body).RootElement;
    }

    private async Task<JsonElement> Refused(string reduction, string query, string error) {
        var (status, body) = await Post(reduction, DefaultInstance(reduction), query);
        Assert.True(status == HttpStatusCode.BadRequest, body);
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal(error, root.GetProperty("error").GetString());
        return root;
    }

    private static IReduction Build(string reduction) =>
        (IReduction)Activator.CreateInstance(ProblemProvider.Reductions[reduction.ToLower()])!;

    private static string DefaultInstance(string reduction) => Build(reduction).reductionFrom.defaultInstance;

    private static string DefaultPicture(IProblem problem) => problem.defaultVisualization.GetType().Name;

    // Every id a payload names, at any depth.
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

    private static List<string> BuildOrder(JsonElement view) {
        var order = view.GetProperty("target").GetProperty("buildOrder");
        return order.GetProperty("nodes").EnumerateArray().Concat(order.GetProperty("edges").EnumerateArray())
            .Select(e => e.GetString()!).ToList();
    }

    // ---- Defaults ----

    [Theory]
    [InlineData(Sat)]
    [InlineData(Vc)]
    public async Task Omitted_DrawsTheDefaultsAndSaysSo(string reduction) {
        string instance = DefaultInstance(reduction);
        var red = Build(reduction);
        var (_, omitted) = await Post(reduction, instance);
        var (_, named) = await Post(reduction, instance,
            $"&sourceVisualization={DefaultPicture(red.reductionFrom)}&targetVisualization={DefaultPicture(red.reductionTo)}");
        // Naming the defaults is the same request as naming nothing.
        Assert.Equal(omitted, named);

        var view = JsonDocument.Parse(omitted).RootElement;
        Assert.Equal(DefaultPicture(red.reductionFrom), view.GetProperty("source").GetProperty("visualization").GetString());
        Assert.Equal(DefaultPicture(red.reductionTo), view.GetProperty("target").GetProperty("visualization").GetString());
        Assert.Equal("pieces", view.GetProperty("target").GetProperty("buildReveal").GetString());
        Assert.NotEmpty(BuildOrder(view));
        Assert.All(view.GetProperty("frames").EnumerateArray().Where(f => f.GetProperty("phase").GetString() == "build"),
            f => Assert.True(f.GetProperty("target").TryGetProperty("builtNodes", out _)));
    }

    [Fact]
    public async Task Echo_IsTheClassName_WhateverTheCaseAsked() {
        var view = await View(Sat, DefaultInstance(Sat), "&sourceVisualization=sat3defaultvisualization&targetVisualization=CLIQUEDEFAULTVISUALIZATION");
        Assert.Equal("Sat3DefaultVisualization", view.GetProperty("source").GetProperty("visualization").GetString());
        Assert.Equal("CliqueDefaultVisualization", view.GetProperty("target").GetProperty("visualization").GetString());
    }

    // ---- Every picture each side can choose ----

    /// <summary>The reductions the Reduction View can show, by class name.</summary>
    public static TheoryData<string> Viewable() {
        var data = new TheoryData<string>();
        foreach (var type in ProblemProvider.Reductions.Values.Distinct().OrderBy(t => t.Name, StringComparer.Ordinal)) {
            IReduction red;
            try { red = (IReduction)Activator.CreateInstance(type)!; } catch { continue; }
            bool records = type.GetConstructor([typeof(string), typeof(StepRecorder<BuildDelta>)]) != null || red.hasBuildSteps();
            if (records && red.hasBackwardMap() && red.reductionFrom.defaultVisualization is IFramesVisualization
                && red.reductionTo.defaultVisualization is IFramesVisualization)
                data.Add(type.Name);
        }
        return data;
    }

    /// <summary>
    /// Every frames picture of each side's problem is drawn when asked for, and the response says so; a picture the view cannot
    /// use is refused with its side's error. A target picture without every piece the steps build is shown whole during Build.
    /// </summary>
    [Theory]
    [MemberData(nameof(Viewable))]
    public async Task EveryPicture_OfEachSide_IsDrawnOrRefused(string reduction) {
        string instance = DefaultInstance(reduction);
        var (status, body) = await Post(reduction, instance);
        Assert.True(status == HttpStatusCode.OK, body);
        var red = Build(reduction);
        foreach (var (side, problem) in new[] { ("source", red.reductionFrom), ("target", red.reductionTo) }) {
            foreach (string name in ProblemProvider.VisualizationsOf(problem.GetType())) {
                var vis = (IVisualization)Activator.CreateInstance(ProblemProvider.Visualizers[name.ToLower()])!;
                var (st, b) = await Post(reduction, instance, $"&{side}Visualization={name}");
                bool usable = vis is IFramesVisualization fv && vis.StepShape != null && (side == "target" || fv.DrawsAnswerAlone());
                if (!usable) {
                    Assert.True(st == HttpStatusCode.BadRequest, $"{reduction} {side} {name}: {b}");
                    Assert.Equal($"no_{side}_picture", JsonDocument.Parse(b).RootElement.GetProperty("error").GetString());
                    continue;
                }
                Assert.True(st == HttpStatusCode.OK, $"{reduction} {side} {name}: {b}");
                var view = JsonDocument.Parse(b).RootElement;
                var pane = view.GetProperty(side);
                Assert.Equal(name, pane.GetProperty("visualization").GetString());
                Assert.False(string.IsNullOrEmpty(pane.GetProperty("picture").GetProperty("type").GetString()));
                var frames = view.GetProperty("frames").EnumerateArray().ToList();
                Assert.Equal(new[] { "build", "solve", "map" }, frames.Select(f => f.GetProperty("phase").GetString()).Distinct());
                Assert.Equal("Done", frames[^1].GetProperty("event").GetString());

                var target = view.GetProperty("target");
                var builds = frames.Where(f => f.GetProperty("phase").GetString() == "build").ToList();
                if (target.GetProperty("buildReveal").GetString() == "whole") {
                    Assert.NotEqual(DefaultPicture(red.reductionTo), target.GetProperty("visualization").GetString());
                    Assert.Empty(BuildOrder(view));
                    Assert.All(builds, f => Assert.False(f.GetProperty("target").TryGetProperty("builtNodes", out _)));
                } else {
                    Assert.Equal("pieces", target.GetProperty("buildReveal").GetString());
                    // Pieces are revealed only by ids the picture has.
                    var ids = Ids(target.GetProperty("picture").GetProperty("payload"));
                    Assert.All(BuildOrder(view), id => Assert.Contains(id, ids));
                    Assert.All(builds, f => Assert.True(f.GetProperty("target").TryGetProperty("builtNodes", out _)));
                }
            }
        }
    }

    // ---- Refused ----

    [Theory]
    [InlineData("source")]
    [InlineData("target")]
    public async Task UnknownPicture_Is400(string side) {
        var error = await Refused(Sat, $"&{side}Visualization=NoSuchVisualization", "unknown_visualization");
        Assert.Equal("NoSuchVisualization", error.GetProperty("received").GetString());
        Assert.Equal(side, error.GetProperty("side").GetString());
    }

    [Theory]
    [InlineData("source", "VertexCoverDefaultVisualization", "3SAT")]
    [InlineData("target", "Sat3DefaultVisualization", "Clique")]
    public async Task AnotherProblemsPicture_Is400(string side, string picture, string problem) {
        var error = await Refused(Sat, $"&{side}Visualization={picture}", "visualization_mismatch");
        Assert.Equal(picture, error.GetProperty("received").GetString());
        Assert.Equal(side, error.GetProperty("side").GetString());
        Assert.Equal(problem, error.GetProperty("problem").GetString());
    }

    [Fact]
    public async Task ListOnlyPicture_OnTheTarget_IsNoTargetPicture() {
        var error = await Refused(Sat, "&targetVisualization=CliqueLatexVisualization", "no_target_picture");
        Assert.Equal("CliqueLatexVisualization", error.GetProperty("received").GetString());
    }

    [Fact]
    public async Task ListOnlyPicture_OnTheSource_IsNoSourcePicture() {
        var error = await Refused(Vc, "&sourceVisualization=CliqueLatexVisualization", "no_source_picture");
        Assert.Equal("CliqueLatexVisualization", error.GetProperty("received").GetString());
    }
}
