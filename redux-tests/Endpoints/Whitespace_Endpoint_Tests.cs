using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// A multi-line instance or certificate (real line breaks, as pasted from a text area) must give the same
// answer through the HTTP endpoints as the single-line form (Redux_GUI#327).
public class Whitespace_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public Whitespace_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private async Task<string> VerifyAsync(string verifier, string instance, string certificate) {
        var response = await _client.PostAsJsonAsync($"/ProblemProvider/verify?verifier={verifier}",
            new { Certificate = certificate, ProblemInstance = instance }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonSerializer.Deserialize<string>(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!;
    }

    private async Task<string> SolveAsync(string solver, string instance) {
        var response = await _client.PostAsync($"/ProblemProvider/solve?solver={solver}",
            new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonSerializer.Deserialize<string>(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!;
    }

    private const string CliqueInstance = "(({1,2,3,4,5,6},{{4,1},{1,2},{4,3},{3,2},{2,4},{5,2},{3,5},{5,4},{3,6},{6,4},{1,6}}),4)";
    private const string TspInstance = "(({New York,Chicago,Denver,Los Angeles,Miami},{({New York,Chicago},790),({New York,Denver},1770),({New York,Los Angeles},2450),({New York,Miami},1280),({Chicago,Denver},1000),({Chicago,Los Angeles},2015),({Chicago,Miami},1370),({Denver,Los Angeles},1015),({Denver,Miami},2060),({Los Angeles,Miami},2745)}),8000)";
    private const string KnapsackInstance = "({(10,60),(20,100),(30,120)},50,220)";

    [Theory]
    [InlineData("cliqueverifier", CliqueInstance, "{2,3,4,5}", "{\n  2,\n  3,\n  4,\n  5\n}")]
    [InlineData("cliqueverifier", CliqueInstance, "{2,3,4,5}", "{2,\r\n3,\r\n4,\r\n5}\r\n")]
    [InlineData("tspverifier", TspInstance, "{New York,Chicago,Denver,Los Angeles,Miami}", "{New York,\n Chicago,\n Denver,\n Los Angeles,\n Miami}")]
    [InlineData("knapsackverifier", KnapsackInstance, "{(20,100),(30,120)}", "{\n  (20,100),\n  (30,120)\n}")]
    public async Task Verify_MultiLineCertificate_MatchesSingleLine(string verifier, string instance, string certificate, string multiLine) {
        Assert.Equal("True", await VerifyAsync(verifier, instance, certificate));
        Assert.Equal("True", await VerifyAsync(verifier, instance, multiLine));
    }

    [Theory]
    [InlineData("cliqueverifier", CliqueInstance, "{2,3,4,5}", "(({1,2,3,4,5,6},\n  {{4,1},{1,2},{4,3},{3,2},{2,4},{5,2},\n   {3,5},{5,4},{3,6},{6,4},{1,6}}),\n 4)")]
    [InlineData("knapsackverifier", KnapsackInstance, "{(20,100),(30,120)}", "(\n  {(10,60),\n   (20,100),\n   (30,120)},\n  50,\n  220\n)\n")]
    public async Task Verify_MultiLineInstance_MatchesSingleLine(string verifier, string instance, string certificate, string multiLine) {
        Assert.Equal("True", await VerifyAsync(verifier, instance, certificate));
        Assert.Equal("True", await VerifyAsync(verifier, multiLine, certificate));
    }

    [Fact]
    public async Task Verify_MultiLineCertificate_ThatIsWrong_StillFalse() {
        Assert.Equal("False", await VerifyAsync("cliqueverifier", CliqueInstance, "{\n  1,\n  2,\n  6\n}"));
    }

    [Theory]
    [InlineData("CliqueBruteForce", CliqueInstance, "(({1,2,3,4,5,6},\n  {{4,1},{1,2},{4,3},{3,2},{2,4},{5,2},\n   {3,5},{5,4},{3,6},{6,4},{1,6}}),\n 4)")]
    [InlineData("KnapsackBruteForce", KnapsackInstance, "(\r\n  {(10,60),\r\n   (20,100),\r\n   (30,120)},\r\n  50,\r\n  220\r\n)\r\n")]
    [InlineData("TSPBruteForce", TspInstance, "(({New York,Chicago,Denver,Los Angeles,Miami},\n {({New York,Chicago},790),({New York,Denver},1770),\n  ({New York,Los Angeles},2450),({New York,Miami},1280),\n  ({Chicago,Denver},1000),({Chicago,Los Angeles},2015),\n  ({Chicago,Miami},1370),({Denver,Los Angeles},1015),\n  ({Denver,Miami},2060),({Los Angeles,Miami},2745)}),\n 8000)")]
    public async Task Solve_MultiLineInstance_MatchesSingleLine(string solver, string instance, string multiLine) {
        string expected = await SolveAsync(solver, instance);
        Assert.Equal(expected, await SolveAsync(solver, multiLine));
    }
}
