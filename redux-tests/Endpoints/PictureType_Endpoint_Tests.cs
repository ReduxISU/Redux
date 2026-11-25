using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// /info names the picture type each visualization serves under format=frames (pictureType, #841): for every visualization,
// it is exactly the type the visualize endpoint's format=frames response gives for the problem's default instance when the
// visualization has a frames hook, and null when it serves only the older formats (whose format=frames is the generic
// wrapping, typed with the legacy visualizationType). The field is additive: it sits right after visualizationType. It is
// written only for a client that opts in with includePictures=true (GuiView), so what Redux_GUI reads stays as it was.
public class PictureType_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public PictureType_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    public static TheoryData<string> Visualizations() {
        var data = new TheoryData<string>();
        foreach (var key in ProblemProvider.Visualizers.Keys.Order(StringComparer.Ordinal)) data.Add(key);
        return data;
    }

    private static string? DefaultInstance(Type visType) {
        var vis = (IVisualization)Activator.CreateInstance(visType)!;
        Type? problem = visType.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IVisualization<>))?.GetGenericArguments()[0]
            ?? vis.solver.GetType().GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISolver<>))?.GetGenericArguments()[0];
        return problem == null ? null : ((IProblem)Activator.CreateInstance(problem)!).defaultInstance;
    }

    [Theory]
    [MemberData(nameof(Visualizations))]
    public async Task PictureType_IsWhatFormatFramesReturns_OrNullWithoutAFramesHook(string key) {
        Type type = ProblemProvider.Visualizers[key];
        var info = JsonDocument.Parse(await _client.GetStringAsync($"/ProblemProvider/info?interface={key}&includePictures=true", TestContext.Current.CancellationToken)).RootElement;
        var names = info.EnumerateObject().Select(p => p.Name).ToList();
        // Right after the legacy type and its tag list (visualizationTypes, #914), which sits beside the single field.
        Assert.Equal(Math.Max(names.IndexOf("visualizationType"), names.IndexOf("visualizationTypes")) + 1, names.IndexOf("pictureType"));
        var pictureType = info.GetProperty("pictureType");
        bool hasHook = typeof(IFramesVisualization).IsAssignableFrom(type);
        if (!hasHook) {
            Assert.Equal(JsonValueKind.Null, pictureType.ValueKind);
            return;
        }
        Assert.Equal(JsonValueKind.String, pictureType.ValueKind);
        string? instance = DefaultInstance(type);
        Assert.NotNull(instance);
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/visualize?visualization={key}&format=frames", content, TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        Assert.Equal(JsonDocument.Parse(body).RootElement.GetProperty("type").GetString(), pictureType.GetString());
        Assert.True(Enum.TryParse<VisualizationType>(pictureType.GetString(), out _));
    }

    // A problem's own /info lists its default visualization with the same field, and allInfo lists every visualization with it.
    [Fact]
    public async Task ProblemInfo_AndAllInfo_CarryPictureType_ForEveryVisualization() {
        var problem = JsonDocument.Parse(await _client.GetStringAsync("/ProblemProvider/info?interface=CLIQUE&includePictures=true", TestContext.Current.CancellationToken)).RootElement;
        Assert.Equal("Graph", problem.GetProperty("defaultVisualization").GetProperty("pictureType").GetString());
        var all = JsonDocument.Parse(await _client.GetStringAsync("/Navigation/Batch/allInfo?includePictures=true", TestContext.Current.CancellationToken)).RootElement;
        foreach (var (key, type) in ProblemProvider.Visualizers) {
            if (!all.TryGetProperty(type.Name, out var entry)) continue;
            var expected = ((IVisualization)Activator.CreateInstance(type)!) is IFramesVisualization f ? f.PictureType() : null;
            Assert.Equal(expected, entry.GetProperty("pictureType").ValueKind == JsonValueKind.Null ? null : entry.GetProperty("pictureType").GetString());
        }
    }

    // Without includePictures (what Redux_GUI asks for) no visualization has the field: not in a visualization's /info, a
    // problem's defaultVisualization, allInfo or problemInstance.
    [Fact]
    public async Task WithoutIncludePictures_NoVisualizationHasPictureType() {
        var ct = TestContext.Current.CancellationToken;
        foreach (var key in ProblemProvider.Visualizers.Keys)
            Assert.DoesNotContain("\"pictureType\"", await _client.GetStringAsync($"/ProblemProvider/info?interface={key}", ct));
        Assert.DoesNotContain("\"pictureType\"", await _client.GetStringAsync("/ProblemProvider/info?interface=CLIQUE", ct));
        Assert.DoesNotContain("\"pictureType\"", await _client.GetStringAsync("/Navigation/Batch/allInfo", ct));
        var content = new StringContent(JsonSerializer.Serialize(new API.Problems.NPComplete.NPC_CLIQUE.CLIQUE().defaultInstance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/problemInstance?problem=CLIQUE", content, ct);
        string body = await response.Content.ReadAsStringAsync(ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        Assert.Contains("\"defaultVisualization\"", body);
        Assert.DoesNotContain("\"pictureType\"", body);
    }
}
