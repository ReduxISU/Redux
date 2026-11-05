using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using API.Problems.NPComplete.NPC_PARTITION.ReduceTo.NPC_WEIGHTEDCUT;
using API.Problems.NPComplete.NPC_WEIGHTEDCUT;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Partition to Weighted Cut: mapSolutions returned "" (#740); an odd total gave a K that a split one apart reaches, one number
// gave text Weighted Cut cannot read, and large numbers overflowed K without a word (#830).
public class PartitionToWeightedCut_Tests : IClassFixture<AppFactory> {
    // Partition's default instance before #938 changed it: it has two 12s (#628).
    private const string OldPartitionDefault = "{1,7,12,15,33,12,11,5,6,9,21,18}";

    private readonly HttpClient _client;

    public PartitionToWeightedCut_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private async Task<(HttpStatusCode Status, string Body)> Post(string path, string instance) {
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/" + path, content, TestContext.Current.CancellationToken);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private static bool Verifies(IProblem problem, string certificate) {
        try { return problem.defaultVerifier.verify(problem.instance, certificate); } catch { return false; }
    }

    [Theory]
    [InlineData("{1,2,3}", "(3),(1,2)", "{({1,3},3),({2,3},6)}")]
    [InlineData("{4,5,6,7}", "{(4,7),(5,6)}", "{({1,2},20),({1,3},24),({2,4},35),({3,4},42)}")]
    [InlineData("{30000,20000,10000}", "(30000),(20000,10000)", "{({1,2},600000000),({1,3},300000000)}")]
    public void CorrectPartitionAnswer_MapsToACorrectWeightedCutAnswer(string instance, string answer, string expected) {
        var red = new WEIGHTEDCUTReduction(instance);
        Assert.True(Verifies(red.reductionFrom, answer), answer);
        string mapped = red.mapSolutions(answer);
        Assert.Equal(expected, mapped);
        Assert.True(Verifies(red.reductionTo, mapped), mapped);
    }

    [Fact]
    public void DefaultInstance_KeepsItsTarget() {
        // Partition keeps both 12s of the old default (#628), so 12 numbers summing to 150: K = 150^2 / 4 = 5625. (Before #628 the
        // second 12 was dropped: 11 numbers, 138, K = 4761.)
        var red = new WEIGHTEDCUTReduction(OldPartitionDefault);
        Assert.StartsWith("(({1,2,3,4,5,6,7,8,9,10,11,12},{({1,2},7),({1,3},12),", red.reductionTo.instance);
        Assert.EndsWith(",({11,12},378)}),5625)", red.reductionTo.instance);
        Assert.Equal(66, red.reductionTo.edges.Count);
    }

    // #830: with an odd total no split is even, and K is one more than the best split's cut, so no cut reaches it.
    [Theory]
    [InlineData("{1,2}", "(({1,2},{({1,2},2)}),3)")]
    [InlineData("{1,2,4}", "(({1,2,3},{({1,2},2),({1,3},4),({2,3},8)}),13)")]
    public void OddTotal_GivesAKNoCutReaches(string instance, string target) {
        var red = new WEIGHTEDCUTReduction(instance);
        Assert.Equal(target, red.reductionTo.instance);
        var parsed = new WEIGHTEDCUT(target);
        Assert.Equal("{}", parsed.defaultSolver.solve(parsed));
    }

    [Fact]
    public void OneNumber_GivesOneNodeAndNoEdges() {
        var red = new WEIGHTEDCUTReduction("{4}");
        Assert.Equal("(({1},{}),4)", red.reductionTo.instance);
        var parsed = new WEIGHTEDCUT(red.reductionTo.instance);
        Assert.Equal(new[] { "1" }, parsed.nodes);
        Assert.Empty(parsed.edges);
    }

    [Theory]
    [InlineData("{60000,50000}", "weighs 60000 * 50000")]
    [InlineData("{2000000000,2000000000,1}", "does not fit")]
    [InlineData("{2147483647,2147483646,2147483645,-1}", "does not fit")]
    public async Task NumbersTooBigForWeightedCut_Are400(string instance, string detail) {
        var (status, body) = await Post("reduce?reduction=weightedcutreduction", instance);
        Assert.True(status == HttpStatusCode.BadRequest, body);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("reduction_input_parse_error", doc.RootElement.GetProperty("error").GetString());
        Assert.Contains(detail, doc.RootElement.GetProperty("detail").GetString());
    }

    [Theory]
    [InlineData("(1,2)")]
    [InlineData("(1),(2)")]
    [InlineData("(1,2),(3,3)")]
    [InlineData("(1,2),(9)")]
    [InlineData("nonsense")]
    public async Task PartitionAnswerThatIsNotASplitOfS_Is400(string answer) {
        var (status, body) = await Post("mapSolution?reduction=weightedcutreduction&solution=" + Uri.EscapeDataString(answer), "{1,2,3}");
        Assert.True(status == HttpStatusCode.BadRequest, body);
    }
}
