using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Graph Coloring to SAT on a graph with no nodes answered 500 (joining no clauses read index -1), and with K = 0 a parse error
// about a SAT formula the caller never wrote (#827). Both are now refused as input the reduction cannot take.
public class ColoringToSatLimits_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public ColoringToSatLimits_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("(({},{}),1)")]
    [InlineData("(({a},{}),0)")]
    [InlineData("(({a,b},{{a,b}}),0)")]
    public async Task NoNodesOrNoColors_Is400_ThatNamesTheReduction(string instance) {
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/reduce?reduction=KarpReduceSAT", content, TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body);
        Assert.Equal("reduction_input_parse_error", JsonDocument.Parse(body).RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task OneNodeOneColor_StillReduces() {
        var content = new StringContent(JsonSerializer.Serialize("(({a},{}),1)"), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/reduce?reduction=KarpReduceSAT", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
