using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.JSON_Objects.Graphs;
using API.Problems.NPHard.NPH_WEIGHTEDMAXCLIQUE;
using API.Problems.NPHard.NPH_WEIGHTEDMAXCLIQUE.Visualizations;
using API.Problems.NPComplete.NPC_CLIQUE;
using API.Problems.NPComplete.NPC_CLIQUE.Visualizations;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

public class WeightedMaxCliqueVisualization_Tests(AppFactory factory) : IClassFixture<AppFactory> {
    [Fact]
    public void InitialGraph_PreservesIdentityConnectivityAndWeightsWithoutHighlights() {
        var problem = new WEIGHTEDMAXCLIQUE();
        var graph = new WeightedMaxCliqueVisualization().visualize(problem);
        Assert.Equal(4, graph.nodes.Count);
        Assert.Equal(4, graph.links.Count);
        Assert.Equal(new[] { "a", "b", "c", "d" }, graph.nodes.Select(n => n.name).Order());
        foreach (var node in graph.nodes) {
            Assert.Equal(node.name, node.id);
            Assert.Equal(problem.weights[node.name].ToString(System.Globalization.CultureInfo.InvariantCulture), node.weight);
            Assert.NotEqual("Solution", node.color);
        }
        foreach (var edge in graph.links) {
            Assert.Contains(problem.edges, e => e.Key == edge.source && e.Value == edge.target);
            Assert.False(edge.weighted);
            Assert.False(edge.directed);
            Assert.NotEqual("Solution", edge.color);
        }
    }

    [Theory]
    [InlineData("({a,b,c},12)", "a,b,c", 3)]
    [InlineData("({b,d},10)", "b,d", 1)]
    [InlineData("({d},7)", "d", 0)]
    [InlineData("({},0)", "", 0)]
    public void FeasibleCertificates_HighlightOnlySelectedVerticesAndTheirEdges(string certificate, string selectedText, int selectedEdges) {
        var selected = selectedText.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        var visualization = new WeightedMaxCliqueVisualization();
        var problem = new WEIGHTEDMAXCLIQUE();
        var graph = Assert.IsType<API_GraphJSON>(visualization.SolvedVisualization(problem, certificate));
        Assert.Equal(4, graph.nodes.Count);
        Assert.Equal(4, graph.links.Count);
        foreach (var node in graph.nodes) {
            Assert.Equal(selected.Contains(node.name) ? "Solution" : "Background", node.color);
            Assert.Equal(problem.weights[node.name].ToString(System.Globalization.CultureInfo.InvariantCulture), node.weight);
        }
        foreach (var edge in graph.links)
            Assert.Equal(selected.Contains(edge.source) && selected.Contains(edge.target) ? "Solution" : "Background", edge.color);
        Assert.Equal(selectedEdges, graph.links.Count(e => e.color == "Solution"));
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("({a,b,c},12)trailing")]
    [InlineData("({a,b,c},12\0)")]
    [InlineData("({a,a},8)")]
    [InlineData("({a,d},11)")]
    [InlineData("({unknown},0)")]
    [InlineData("({a,b,c},11)")]
    [InlineData("({},1)")]
    [InlineData(SolveRun.TimedOutAnswer)]
    public void InvalidAnswers_ReturnInitialGraphWithoutMisleadingHighlights(string certificate) {
        var visualization = new WeightedMaxCliqueVisualization();
        var problem = new WEIGHTEDMAXCLIQUE();
        Assert.Equal(JsonSerializer.Serialize(visualization.visualize(problem)),
            JsonSerializer.Serialize(Assert.IsType<API_GraphJSON>(visualization.SolvedVisualization(problem, certificate))));
    }

    [Theory]
    [InlineData("(({},{}),())", "({},0)", null)]
    [InlineData("(({a},{}),((a,0)))", "({a},0)", "0")]
    [InlineData("(({a},{}),((a,9223372036854775807)))", "({a},9223372036854775807)", "9223372036854775807")]
    [InlineData("(({a,b},{{a,b}}),((a,9223372036854775806),(b,1)))", "({a,b},9223372036854775807)", "9223372036854775806")]
    public void EmptyZeroAndInt64BoundaryGraphs_HaveLosslessStringWeights(string input, string certificate, string? firstWeight) {
        var visualization = new WeightedMaxCliqueVisualization();
        var problem = new WEIGHTEDMAXCLIQUE(input);
        foreach (API_GraphJSON graph in new[] { visualization.visualize(problem), Assert.IsType<API_GraphJSON>(visualization.SolvedVisualization(problem, certificate)) }) {
            using var json = JsonDocument.Parse(JsonSerializer.Serialize(graph));
            if (firstWeight == null) Assert.Empty(graph.nodes);
            else {
                JsonElement weight = json.RootElement.GetProperty("nodes").EnumerateArray().Single(n => n.GetProperty("name").GetString() == "a").GetProperty("weight");
                Assert.Equal(JsonValueKind.String, weight.ValueKind);
                Assert.Equal(firstWeight, weight.GetString());
            }
        }
    }

    [Fact]
    public void UnweightedNodeSerialization_IsUnchangedAndOmitsWeight() {
        Assert.Equal("{\"name\":\"a\",\"color\":\"\",\"outline\":\"\",\"delay\":\"\",\"dashed\":\"\",\"additional\":\"\",\"id\":\"a\"}",
            JsonSerializer.Serialize(new API_Node_Programmable_Small("a")));
        var problem = new CLIQUE();
        var visualization = new CliqueDefaultVisualization();
        foreach (API_GraphJSON graph in new[] { visualization.visualize(problem), Assert.IsType<API_GraphJSON>(visualization.SolvedVisualization(problem, "{2,3,4,5}")) }) {
            using var json = JsonDocument.Parse(JsonSerializer.Serialize(graph));
            foreach (JsonElement node in json.RootElement.GetProperty("nodes").EnumerateArray())
                Assert.False(node.TryGetProperty("weight", out _));
        }
    }

    [Fact]
    public async Task DiscoveryAndInfo_ExposeGraphD3DefaultVisualization() {
        IProblem problem = new WEIGHTEDMAXCLIQUE();
        Assert.IsType<WeightedMaxCliqueVisualization>(problem.defaultVisualization);
        Assert.Equal(typeof(WeightedMaxCliqueVisualization), ProblemProvider.Visualizers["weightedmaxcliquevisualization"]);
        Assert.Equal(VisualizationType.GraphD3, problem.defaultVisualization.visualizationType);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/ProblemProvider/info?interface=weightedmaxclique", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement info = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("Weighted Maximum Clique Visualization", info.GetProperty("defaultVisualization").GetProperty("visualizationName").GetString());
    }

    [Theory]
    [InlineData("weightedmaxcliquebruteforcesolver")]
    [InlineData("weightedmaxcliquebranchandboundsolver")]
    [InlineData("weightedmaxcliquegreedylocalsearchsolver")]
    public async Task VisualizationEndpoint_AllSolversPreserveWeightsAndHighlightDefaultOptimum(string solver) {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync($"/ProblemProvider/visualize?visualization=weightedmaxcliquevisualization&solver={solver}",
            WEIGHTEDMAXCLIQUE.DefaultInstance, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement pictures = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(2, pictures.GetArrayLength());
        foreach (JsonElement picture in pictures.EnumerateArray()) {
            Assert.Equal("graph", picture.GetProperty("kind").GetString());
            Assert.Equal(4, picture.GetProperty("nodes").GetArrayLength());
            Assert.Equal(4, picture.GetProperty("links").GetArrayLength());
            foreach (JsonElement node in picture.GetProperty("nodes").EnumerateArray())
                Assert.Equal(new WEIGHTEDMAXCLIQUE().weights[node.GetProperty("name").GetString()!].ToString(System.Globalization.CultureInfo.InvariantCulture), node.GetProperty("weight").GetString());
        }
        Assert.DoesNotContain(pictures[0].GetProperty("nodes").EnumerateArray(), n => n.GetProperty("color").GetString() == "Solution");
        Assert.Equal(new[] { "a", "b", "c" }, pictures[1].GetProperty("nodes").EnumerateArray().Where(n => n.GetProperty("color").GetString() == "Solution").Select(n => n.GetProperty("name").GetString()).Order());
        Assert.Equal(3, pictures[1].GetProperty("links").EnumerateArray().Count(n => n.GetProperty("color").GetString() == "Solution"));
    }

    [Theory]
    [InlineData("(({},{}),())", 0, null)]
    [InlineData("(({a},{}),((a,0)))", 1, "0")]
    [InlineData("(({a},{}),((a,9223372036854775807)))", 1, "9223372036854775807")]
    public async Task VisualizationEndpoint_HandlesEmptyZeroAndInt64Graphs(string input, int count, string? weight) {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/ProblemProvider/visualize?visualization=weightedmaxcliquevisualization", input, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement pictures = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(2, pictures.GetArrayLength());
        foreach (JsonElement picture in pictures.EnumerateArray()) {
            Assert.Equal(count, picture.GetProperty("nodes").GetArrayLength());
            if (count != 0) Assert.Equal(weight, picture.GetProperty("nodes")[0].GetProperty("weight").GetString());
        }
    }
}
