using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using API.Problems.NPComplete.NPC_SUBSETSUM.ReduceTo.NPC_PARTITION;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Subset Sum to Partition: mapSolutions answered S[0], which Partition's visualization could not read, so visualizeReduction
// answered 500 (#817); and a Partition whose added numbers repeat a number of S was read with the repeat dropped (#628), so
// its answer could differ from Subset Sum's (#831).
public class SubsetSumToPartition_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public SubsetSumToPartition_Tests(AppFactory factory) {
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
    [InlineData("({1,7,12,15},28)", "{1,12,15}", "{(1,12,15,8),(7,29)}")]
    [InlineData("({2,5,9},11)", "{2,9}", "{(2,9,6),(5,12)}")]
    [InlineData("({4,6},0)", "{}", "{(11),(4,6,1)}")]
    public void CorrectSubsetSumAnswer_MapsToACorrectPartitionAnswer(string instance, string answer, string expected) {
        var red = new SubsetSumToPartitionReduction(instance);
        Assert.True(Verifies(red.reductionFrom, answer), answer);
        string mapped = red.mapSolutions(answer);
        Assert.Equal(expected, mapped);
        Assert.True(Verifies(red.reductionTo, mapped), mapped);
    }

    [Fact]
    public void Reduce_DefaultInstance_IsUnchanged() {
        Assert.Equal("{1,7,12,15,29,8}", new SubsetSumToPartitionReduction("({1,7,12,15},28)").reductionTo.instance);
    }

    // #817: the default instance with a solution draws Partition's picture of the mapped split.
    [Fact]
    public async Task VisualizeReduction_WithASolution_Is200_AndColorsTheSplit() {
        var (status, body) = await Post("visualizeReduction?reduction=subsetsumtopartitionreduction&solution=" + Uri.EscapeDataString("{1,12,15}"), "({1,7,12,15},28)");
        Assert.True(status == HttpStatusCode.OK, body);
        Assert.Contains("Solution", body);
    }

    [Theory]
    [InlineData("{99}")]
    [InlineData("{1,99}")]
    [InlineData("1")]
    [InlineData("{1,12")]
    public async Task SubsetSumAnswerThatIsNotOne_Is400(string solution) {
        foreach (string endpoint in new[] { "visualizeReduction", "mapSolution" }) {
            var (status, body) = await Post($"{endpoint}?reduction=subsetsumtopartitionreduction&solution=" + Uri.EscapeDataString(solution), "({1,7,12,15},28)");
            Assert.True(status == HttpStatusCode.BadRequest, $"{endpoint}: {body}");
        }
    }

    // #831: an added number that repeats one of S (3 + 1 = 4 here) would be read once by Partition, so the reduction refuses
    // the instance with a 400 that says why, instead of building a Partition with a different answer.
    [Theory]
    [InlineData("({3,4},3)", "T + 1 = 4 is already a number of S")]
    [InlineData("({1,2,3},2)", "T + 1 = 3 is already a number of S")]
    [InlineData("({1,2,4},4)", "sum - T + 1 = 4 is already a number of S")]
    [InlineData("({1,2,3},3)", "T + 1 and sum - T + 1 are both 4")]
    [InlineData("({1},2147483647)", "must fit Partition's integers")]
    public async Task AnInstanceWhosePartitionWouldRepeatANumber_Is400(string instance, string detail) {
        var (status, body) = await Post("reduce?reduction=subsetsumtopartitionreduction", instance);
        Assert.True(status == HttpStatusCode.BadRequest, body);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("reduction_input_parse_error", doc.RootElement.GetProperty("error").GetString());
        Assert.Contains(detail, doc.RootElement.GetProperty("detail").GetString());
    }
}
