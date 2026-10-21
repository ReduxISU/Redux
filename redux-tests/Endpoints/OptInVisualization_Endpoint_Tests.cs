using System.Net;
using System.Text.Json;
using API.Interfaces;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The opt-in visualizations (IOptInVisualization: the picture-type views Redux_GUI cannot draw yet) are left out of every
// listing Redux_GUI reads, and listed only when a client asks with includePictures=true. A client that names one still gets it.
// The branches that add opt-in views add their own cases; these hold for any set of them, empty included.
public class OptInVisualization_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public OptInVisualization_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private async Task<JsonElement> Get(string url) {
        var response = await _client.GetAsync(url, TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static readonly HashSet<string> OptIn = ProblemProvider.Visualizers.Values
        .Where(OptInVisualizations.IsOptIn).Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> All = ProblemProvider.Visualizers.Values
        .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IVisualization<>)))
        .Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static HashSet<string> Names(IEnumerable<string> names) => names.ToHashSet(StringComparer.OrdinalIgnoreCase);

    [Fact]
    public async Task AllVisualizations_LeavesTheOptInViewsOut_UnlessAskedFor() {
        var plain = Names((await Get("/Navigation/Batch/allVisualizations")).EnumerateObject().SelectMany(p => p.Value.EnumerateArray().Select(e => e.GetString()!)));
        var withPictures = Names((await Get("/Navigation/Batch/allVisualizations?includePictures=true")).EnumerateObject().SelectMany(p => p.Value.EnumerateArray().Select(e => e.GetString()!)));
        Assert.Empty(plain.Intersect(OptIn));
        Assert.Equal(All.Except(OptIn).OrderBy(x => x), plain.OrderBy(x => x));
        Assert.Equal(All.OrderBy(x => x), withPictures.OrderBy(x => x));
        Assert.Equal((await Get("/Navigation/Batch/allVisualizations?includePictures=false")).GetRawText(), (await Get("/Navigation/Batch/allVisualizations")).GetRawText());
    }

    [Fact]
    public async Task AllVisualizationTypes_LeavesTheOptInViewsOut_UnlessAskedFor() {
        var plain = Names((await Get("/Navigation/Batch/allVisualizationTypes")).EnumerateObject().Select(p => p.Name));
        var withPictures = Names((await Get("/Navigation/Batch/allVisualizationTypes?includePictures=true")).EnumerateObject().Select(p => p.Name));
        Assert.Empty(plain.Intersect(OptIn));
        Assert.Superset(OptIn, withPictures);
        Assert.Equal(withPictures.Except(OptIn).OrderBy(x => x), plain.OrderBy(x => x));
    }

    [Fact]
    public async Task AllInfo_LeavesTheOptInViewsOut_UnlessAskedFor() {
        var plain = Names((await Get("/Navigation/Batch/allInfo")).EnumerateObject().Select(p => p.Name));
        var withPictures = Names((await Get("/Navigation/Batch/allInfo?includePictures=true")).EnumerateObject().Select(p => p.Name));
        Assert.Empty(plain.Intersect(OptIn));
        Assert.Superset(OptIn, withPictures);
        Assert.Equal(withPictures.Except(OptIn).OrderBy(x => x), plain.OrderBy(x => x));
    }
}
