using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Guards for the typed-steps change. The snapshots in Snapshots/ were captured from the endpoints BEFORE
// DFA/NFA moved to typed steps, so a pass means the old frontends see exactly what they saw before:
//   <vis>.json              /visualize response for the default instance (<vis>.instance.txt)
//   info.<interface>.json   /info response
public class Steps_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public Steps_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private static string ReadSnapshot(string file) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Endpoints", "Snapshots", file)).Replace("\r\n", "\n");

    private async Task<(HttpStatusCode Status, string Body)> Visualize(string query, string instance) {
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/visualize?{query}", content, TestContext.Current.CancellationToken);
        return (response.StatusCode, (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Replace("\r\n", "\n"));
    }

    [Theory]
    [InlineData("nfavisualization")]
    [InlineData("dfatablevisualization")]
    [InlineData("nfatablevisualization")]
    public async Task Visualize_DefaultResponse_MatchesPreChangeSnapshot(string vis) {
        var (status, body) = await Visualize($"visualization={vis}", ReadSnapshot($"{vis}.instance.txt"));
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(ReadSnapshot($"{vis}.json"), body);
    }

    [Theory]
    [InlineData("nfavisualization", "nfasolver")]
    [InlineData("dfatablevisualization", "dfasolver")]
    [InlineData("nfatablevisualization", "nfasolver")]
    public async Task Visualize_OwnSolverChosenExplicitly_MatchesSnapshot(string vis, string solver) {
        var (_, body) = await Visualize($"visualization={vis}&solver={solver}", ReadSnapshot($"{vis}.instance.txt"));
        Assert.Equal(ReadSnapshot($"{vis}.json"), body);
    }

    [Theory]
    [InlineData("dfasolver")]
    [InlineData("nfasolver")]
    [InlineData("dfavisualization")]
    [InlineData("dfatablevisualization")]
    public async Task Info_IsUnchangedByTypedSteps(string name) {
        var response = await _client.GetAsync($"/ProblemProvider/info?interface={name}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ReadSnapshot($"info.{name}.json"), body.Replace("\r\n", "\n"));
    }

    [Fact]
    public async Task Info_OfNfaVisualizationsAndSolver_HasNoStepMembers() {
        // The typed-step plumbing must not leak into reflected /info output.
        foreach (var name in new[] { "dfasolver", "nfasolver", "dfavisualization", "nfavisualization", "dfatablevisualization", "nfatablevisualization" }) {
            var body = await (await _client.GetAsync($"/ProblemProvider/info?interface={name}", TestContext.Current.CancellationToken))
                .Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.DoesNotContain("StepShape", body);
            Assert.DoesNotContain("stepShape", body);
            Assert.DoesNotContain("Steps", body);
        }
    }

    [Fact]
    public async Task Visualize_DfaFramesFormat_HasOneFramePerStateEntered() {
        // Default instance: start state 1, then 2 -> two step frames plus the solved frame.
        var (_, body) = await Visualize("visualization=dfavisualization&format=frames", ReadSnapshot("dfavisualization.instance.txt"));
        var frames = JsonDocument.Parse(body).RootElement.GetProperty("frames");
        Assert.Equal(3, frames.GetArrayLength());
    }

    [Fact]
    public async Task Visualize_DfaRejectedInput_StillReturns200WithStepFrames() {
        var (status, body) = await Visualize("visualization=dfavisualization&format=frames",
            "(({1,2,3},{a,b},{(1,a,2),(1,b,3),(2,a,2),(2,b,2),(3,a,2),(3,b,3)},1,{2}),b)");
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(3, JsonDocument.Parse(body).RootElement.GetProperty("frames").GetArrayLength());
    }
}
