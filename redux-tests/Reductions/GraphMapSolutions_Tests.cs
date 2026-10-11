using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Six graph reductions' mapSolutions returned "" for every answer (#823). Each now maps a correct answer of its source to a
// correct answer of its target, checked here by the two problems' own verifiers. Three of them also kept a target object
// whose graph or K was the default instance's; the target's verifier is run on that object, so it must be this one.
public class GraphMapSolutions_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public GraphMapSolutions_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private static IReduction Build(string reduction, string instance) =>
        (IReduction)Activator.CreateInstance(ProblemProvider.Reductions[reduction.ToLower()], instance)!;

    // The problem's verifier on the problem object itself (IVerifier<T>), so a target object that disagrees with its text fails.
    private static bool Verifies(IProblem problem, string certificate) {
        object verifier = problem.GetType().GetProperty("defaultVerifier")!.GetValue(problem)!;
        var typed = verifier.GetType().GetMethod("verify", [problem.GetType(), typeof(string)])!;
        return (bool)typed.Invoke(verifier, [problem, certificate])!;
    }

    [Theory]
    [InlineData("KarpVertexCoverToNodeSet", "(({a,b,c,d,e},{{a,b},{a,c},{a,e},{b,e},{c,d}}),3)", "{a,b,c}", "{a,b,c}")]
    [InlineData("KarpVertexCoverToNodeSet", "(({1,2,3},{{1,2},{2,3}}),1)", "{2}", "{2}")]
    [InlineData("KarpVertexCoverToSetCover", "(({a,b,c,d,e},{{a,b},{a,c},{a,e},{b,e},{c,d}}),3)", "{a,b,c}", "{{a_b,a_c,a_e},{a_b,b_e},{a_c,c_d}}")]
    [InlineData("KarpVertexCoverToSetCover", "(({1,2,3,4},{{1,2},{2,3}}),1)", "{2}", "{{1_2,2_3}}")]
    [InlineData("KarpDirectedHamiltonianToUndirectedHamiltonian", "({1,2,3,4,5},{(2,1),(1,3),(2,3),(3,5),(4,2),(5,4)})", "{2,1,3,5,4,2}", "{21,22,23,11,12,13,31,32,33,51,52,53,41,42,43}")]
    [InlineData("KarpDirectedHamiltonianToUndirectedHamiltonian", "({a,b,c},{(a,b),(b,c),(c,a)})", "{a,b,c,a}", "{a1,a2,a3,b1,b2,b3,c1,c2,c3}")]
    [InlineData("GraphColoringToCliqueCover", "(({a,b,c},{{a,b},{b,c}}),2)", "{{a,c},{b}}", "{{a,c},{b}}")]
    [InlineData("GraphColoringToCliqueCover", "(({1,2,3,4},{{1,2},{2,3},{3,4},{4,1}}),2)", "{{1,3},{2,4}}", "{{1,3},{2,4}}")]
    [InlineData("KarpGraphColorToExactCover", "(({a,b,c},{{a,b},{b,c}}),2)", "{{a,c},{b}}", "{{a,a_a_b_1},{b,b_a_b_2,b_b_c_2},{c,c_b_c_1},{a_b,a_a_b_2,b_a_b_1},{b_c,b_b_c_1,c_b_c_2}}")]
    [InlineData("KarpGraphColorToExactCover", "(({a,b,c},{{a,b},{b,c},{a,c}}),3)", "{{a},{b},{c}}", null)]
    [InlineData("KarpReduceSAT", "(({a,b,c},{{a,b},{b,c}}),2)", "{{a,c},{b}}", "(a0:True,a1:False,b0:False,b1:True,c0:True,c1:False)")]
    [InlineData("KarpReduceSAT", "(({a,b,c,d,e,f,g,h,i},{{a,b},{b,c},{a,c},{d,a},{d,e},{a,e},{a,f},{f,g},{g,a},{a,h},{h,i},{i,a}}),3)", "{{a},{b,d,f,h},{c,e,g,i}}", null)]
    public void CorrectSourceAnswer_MapsToACorrectTargetAnswer(string reduction, string instance, string answer, string? expected) {
        IReduction red = Build(reduction, instance);
        Assert.True(Verifies(red.reductionFrom, answer), $"{answer} should be a correct {red.reductionFrom.problemName} answer");
        string mapped = red.mapSolutions(answer);
        if (expected != null) Assert.Equal(expected, mapped);
        Assert.True(Verifies(red.reductionTo, mapped), $"{mapped} should be a correct {red.reductionTo.problemName} answer");
    }

    // The target object is the one its text describes (#823): its K and edges are this instance's, not the default's.
    [Theory]
    [InlineData("KarpVertexCoverToNodeSet", "(({1,2,3},{{1,2},{2,3}}),1)")]
    [InlineData("KarpVertexCoverToSetCover", "(({1,2,3},{{1,2},{2,3}}),1)")]
    [InlineData("GraphColoringToCliqueCover", "(({1,2,3,4},{{1,2},{2,3},{3,4},{4,1}}),2)")]
    public void TargetObject_MatchesItsInstanceText(string reduction, string instance) {
        IReduction red = Build(reduction, instance);
        IProblem fromText = (IProblem)Activator.CreateInstance(red.reductionTo.GetType(), red.reductionTo.instance)!;
        foreach (string field in new[] { "K", "nodes", "edges", "universal", "subsets" }) {
            var property = red.reductionTo.GetType().GetProperty(field);
            if (property == null) continue;
            Assert.Equal(JsonSerializer.Serialize(property.GetValue(fromText)), JsonSerializer.Serialize(property.GetValue(red.reductionTo)));
        }
    }

    // A coloring with an edge inside one class, too many colors or a node missing has no target answer to map to: a 400.
    [Theory]
    [InlineData("GraphColoringToCliqueCover", "{{a,b},{c}}")]
    [InlineData("KarpGraphColorToExactCover", "{{a},{b},{c}}")]
    [InlineData("KarpReduceSAT", "{{a,c}}")]
    [InlineData("KarpReduceSAT", "{{a,c},{b},{zz}}")]
    [InlineData("KarpDirectedHamiltonianToUndirectedHamiltonian", "{a,b,zz,a}")]
    public async Task AnswerThatIsNotOne_Is400(string reduction, string answer) {
        string instance = reduction == "KarpDirectedHamiltonianToUndirectedHamiltonian" ? "({a,b,c},{(a,b),(b,c),(c,a)})" : "(({a,b,c},{{a,b},{b,c}}),2)";
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/mapSolution?reduction={reduction}&solution={Uri.EscapeDataString(answer)}", content, TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body);
    }
}
