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
// its answer could differ from Subset Sum's (#831). Partition keeps repeated numbers now, so such an instance is reduced, with
// the same answer.
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

    // Added numbers that do not fit an int are still a 400.
    [Fact]
    public async Task AddedNumbersPastAnInt_Are400() {
        var (status, body) = await Post("reduce?reduction=subsetsumtopartitionreduction", "({1},2147483647)");
        Assert.True(status == HttpStatusCode.BadRequest, body);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("reduction_input_parse_error", doc.RootElement.GetProperty("error").GetString());
        Assert.Contains("must fit Partition's integers", doc.RootElement.GetProperty("detail").GetString());
    }

    // #831, #628: an added number that repeats one of S (or two equal added numbers, when T = sum / 2) is a second copy of that
    // number in Partition, so the Partition is the one built and answers as Subset Sum does: yes to yes, no to no, and a
    // Subset Sum answer maps to a Partition answer and back.
    [Theory]
    [InlineData("({3,4},3)", "{3,4,4,5}", "{3}")]                 // T + 1 = 4 is in S
    [InlineData("({3,4,5,6},9)", "{3,4,5,6,10,10}", "{3,6}")]     // T = sum / 2: both added numbers are 10
    [InlineData("({1,2,3},2)", "{1,2,3,3,5}", "{2}")]             // T + 1 = 3 is in S
    [InlineData("({1,2,4},4)", "{1,2,4,5,4}", "{4}")]             // sum - T + 1 = 4 is in S
    [InlineData("({1,2,3},3)", "{1,2,3,4,4}", "{1,2}")]           // T + 1 = sum - T + 1 = 4
    [InlineData("({7,7,1},14)", "{7,7,1,15,2}", "{7,7}")]         // S itself repeats 7
    [InlineData("({3,4},5)", "{3,4,6,3}", null)]                  // sum - T + 1 = 3 is in S; nothing adds to 5
    [InlineData("({2,4},3)", "{2,4,4,4}", null)]                  // T = sum / 2, both 4, and 4 is in S; nothing adds to 3
    public async Task AnInstanceWhosePartitionRepeatsANumber_IsReduced_WithTheSameAnswer(string instance, string partition, string? answer) {
        var (status, body) = await Post("reduce?reduction=subsetsumtopartitionreduction", instance);
        Assert.True(status == HttpStatusCode.OK, body);
        var red = new SubsetSumToPartitionReduction(instance);
        Assert.Equal(partition, red.reductionTo.instance);
        // The target as anything that reads its text reads it: every repeat kept.
        var reread = new API.Problems.NPComplete.NPC_PARTITION.PARTITION(red.reductionTo.instance);
        Assert.Equal(red.reductionTo.S, reread.S);

        string sourceAnswer = red.reductionFrom.defaultSolver.solve(red.reductionFrom);
        string targetAnswer = reread.defaultSolver.solve(reread);
        bool yes = answer != null;
        Assert.Equal(yes, Verifies(red.reductionFrom, sourceAnswer));
        Assert.Equal(yes, Verifies(reread, targetAnswer));
        if (!yes) return;

        Assert.True(Verifies(red.reductionFrom, answer!), answer);
        string mapped = red.mapSolutions(answer!);
        Assert.True(Verifies(reread, mapped), mapped);
        // And back, from the forward map's answer and from the solver's own.
        foreach (string target in new[] { mapped, targetAnswer }) {
            var (backStatus, backBody) = await Post("mapSolutionBack?reduction=subsetsumtopartitionreduction&solution=" + Uri.EscapeDataString(target), instance);
            Assert.True(backStatus == HttpStatusCode.OK, backBody);
            string back = JsonSerializer.Deserialize<string>(backBody)!;
            Assert.True(Verifies(red.reductionFrom, back), $"{target} -> {back}");
        }
    }

    // A Subset Sum answer that uses a number more times than S has it is a 400, not a Partition answer (#628).
    [Theory]
    [InlineData("({7,1},14)", "{7,7}")]
    [InlineData("({7,7,1},21)", "{7,7,7}")]
    public async Task SubsetSumAnswerThatReusesANumber_Is400(string instance, string solution) {
        var (status, body) = await Post("mapSolution?reduction=subsetsumtopartitionreduction&solution=" + Uri.EscapeDataString(solution), instance);
        Assert.True(status == HttpStatusCode.BadRequest, body);
    }
}
