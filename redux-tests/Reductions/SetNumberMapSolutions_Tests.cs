using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using API.Problems.NPComplete.NPC_EXACTCOVER.ReduceTo.NPC_SUBSETSUM;
using API.Problems.NPComplete.NPC_SUBSETSUM;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Exact Cover to Subset Sum, Hitting Set to Exact Cover and Subset Sum to Knapsack returned "" from mapSolutions (#828); and
// Exact Cover to Subset Sum answered 500 when its numbers outgrew an int, or when there were no sets (#829).
public class SetNumberMapSolutions_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public SetNumberMapSolutions_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private static IReduction Build(string reduction, string instance) =>
        (IReduction)Activator.CreateInstance(ProblemProvider.Reductions[reduction.ToLower()], instance)!;

    private async Task<(HttpStatusCode Status, string Body)> Post(string path, string instance) {
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/" + path, content, TestContext.Current.CancellationToken);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    // The problem's verifier on the problem object itself (IVerifier<T>), so a target object that disagrees with its text fails.
    private static bool Verifies(IProblem problem, string certificate) {
        object verifier = problem.GetType().GetProperty("defaultVerifier")!.GetValue(problem)!;
        var typed = verifier.GetType().GetMethod("verify", [problem.GetType(), typeof(string)])!;
        try { return (bool)typed.Invoke(verifier, [problem, certificate])!; } catch { return false; }
    }

    [Theory]
    [InlineData("KarpExactCoverToSubsetSum", "({1,2,3,4},{{1,2,3},{2,3},{4,1}})", "{{2,3},{4,1}}", "{20,65}")]
    [InlineData("KarpExactCoverToSubsetSum", "({a,b,c},{{a},{b,c},{a,b}})", "{{a},{b,c}}", "{1,20}")]
    [InlineData("reduceToEXACTCOVER", "({1,2,3,4},{{1,3},{2,3,4},{1,4}})", "{1,2}", "{{1,3},{2}}")]
    [InlineData("reduceToEXACTCOVER", "({a,b,c},{{a,b},{c}})", "{a,c}", "{{1},{2}}")]
    [InlineData("FengReduction", "({1,7,12,15},28)", "{1,12,15}", "{(1,1),(12,12),(15,15)}")]
    [InlineData("FengReduction", "({3,5,9},8)", "{3,5}", "{(3,3),(5,5)}")]
    public void CorrectSourceAnswer_MapsToACorrectTargetAnswer(string reduction, string instance, string answer, string expected) {
        IReduction red = Build(reduction, instance);
        Assert.True(Verifies(red.reductionFrom, answer), $"{answer} should be a correct {red.reductionFrom.problemName} answer");
        string mapped = red.mapSolutions(answer);
        Assert.Equal(expected, mapped);
        Assert.True(Verifies(red.reductionTo, mapped), $"{mapped} should be a correct {red.reductionTo.problemName} answer");
    }

    [Theory]
    [InlineData("KarpExactCoverToSubsetSum", "({1,2,3,4},{{1,2,3},{2,3},{4,1}})", "{{1,2}}")]
    [InlineData("KarpExactCoverToSubsetSum", "({1,2,3,4},{{1,2,3},{2,3},{4,1}})", "{2,3}")]
    [InlineData("KarpExactCoverToSubsetSum", "({1,2,3,4},{{1,2,3},{2,3},{4,1}})", "nonsense")]
    [InlineData("reduceToEXACTCOVER", "({1,2,3,4},{{1,3},{2,3,4},{1,4}})", "{9}")]
    [InlineData("reduceToEXACTCOVER", "({1,2,3,4},{{1,3},{2,3,4},{1,4}})", "1,2")]
    [InlineData("FengReduction", "({1,7,12,15},28)", "{2}")]
    [InlineData("FengReduction", "({1,7,12,15},28)", "{1,12")]
    public async Task AnswerThatIsNotOne_Is400(string reduction, string instance, string answer) {
        var (status, body) = await Post($"mapSolution?reduction={reduction}&solution={Uri.EscapeDataString(answer)}", instance);
        Assert.True(status == HttpStatusCode.BadRequest, body);
    }

    [Fact]
    public void ExactCoverToSubsetSum_DefaultInstance_KeepsItsTarget() {
        Assert.Equal("({21,20,65},85)", Build("KarpExactCoverToSubsetSum", "({1,2,3,4},{{1,2,3},{2,3},{4,1}})").reductionTo.instance);
    }

    // #829: 20 elements in base 4 make T = (4^20 - 1) / 3, past the largest int; it is a 400 that says so, not an overflow.
    [Theory]
    [InlineData("({1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20},{{1,2},{3},{20}})", "20 digits in base 4")]
    [InlineData("({1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25,26,27,28,29,30,31,32,33,34,35,36,37,38,39,40,41,42,43,44,45,46,47,48,49,50,51,52,53,54,55,56,57,58,59,60,61,62,63,64,65,66,67,68,69,70},{{1},{2},{3},{4},{5},{6},{7},{8},{9}})", "70 digits in base 10")]
    public async Task NumbersPastAnInt_Are400(string instance, string detail) {
        foreach (string endpoint in new[] { "reduce", "mapSolution" }) {
            var (status, body) = await Post($"{endpoint}?reduction=KarpExactCoverToSubsetSum&solution=%7B%7D", instance);
            Assert.True(status == HttpStatusCode.BadRequest, body);
            using var doc = JsonDocument.Parse(body);
            Assert.Equal("reduction_input_parse_error", doc.RootElement.GetProperty("error").GetString());
            Assert.Contains(detail, doc.RootElement.GetProperty("detail").GetString());
        }
    }

    [Fact]
    public void ExactCoverToSubsetSum_LargestThatFits_IsExact() {
        // 15 elements in base 4: T = (4^15 - 1) / 3 = 357913941, and the set {15} is 4^14 = 268435456.
        var red = Build("KarpExactCoverToSubsetSum", "({1,2,3,4,5,6,7,8,9,10,11,12,13,14,15},{{1,2},{3},{15}})");
        Assert.Equal("({5,16,268435456},357913941)", red.reductionTo.instance);
    }

    // #829: with no sets the base was 1 and T came out NaN; any base of 2 or more keeps the digits apart.
    [Theory]
    [InlineData("({1},{})", "({},1)", false)]
    [InlineData("({1,2},{})", "({},3)", false)]
    [InlineData("({},{})", "({},0)", true)]
    public void ExactCoverToSubsetSum_NoSets_Reduces(string instance, string target, bool hasAnswer) {
        var red = Build("KarpExactCoverToSubsetSum", instance);
        Assert.Equal(target, red.reductionTo.instance);
        // Only the empty subset is left, which sums to T exactly when X is empty.
        Assert.Equal(hasAnswer, Verifies(new SUBSETSUM(target), "{}"));
    }
}
