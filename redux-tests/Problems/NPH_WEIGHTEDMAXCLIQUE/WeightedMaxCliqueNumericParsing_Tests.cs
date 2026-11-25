using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using API.Interfaces;
using API.Problems.NPHard.NPH_WEIGHTEDMAXCLIQUE;
using API.Problems.NPHard.NPH_WEIGHTEDMAXCLIQUE.Verifiers;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

public class WeightedMaxCliqueNumericParsing_Tests(AppFactory factory) : IClassFixture<AppFactory> {
    [Theory]
    [InlineData("")]
    [InlineData("\0")]
    [InlineData("0\0")]
    [InlineData("0\0\0")]
    [InlineData("\u00000")]
    [InlineData("1\u00002")]
    [InlineData("+0")]
    [InlineData("-1")]
    [InlineData("٤")]
    [InlineData("４")]
    [InlineData("1.0")]
    [InlineData("1e0")]
    [InlineData("9223372036854775808")]
    [InlineData("18446744073709551615")]
    [InlineData("0x0")]
    public void InvalidNumericTokens_ProduceTypedParseErrors(string token) {
        Assert.Throws<ProblemParseException>(() => new WEIGHTEDMAXCLIQUE($"(({{a}},{{}}),((a,{token})))"));
        var problem = new WEIGHTEDMAXCLIQUE("(({},{}),())");
        string certificate = $"({{}},{token})";
        foreach (IVerifier<WEIGHTEDMAXCLIQUE> verifier in new IVerifier<WEIGHTEDMAXCLIQUE>[] {
            new WeightedMaxCliqueSolutionVerifier(), new WeightedMaxCliqueOptimalityVerifier() }) {
            Assert.Throws<CertificateParseException>(() => verifier.verify(problem, certificate));
            Assert.Throws<CertificateParseException>(() => verifier.verify(problem.instance, certificate));
        }
    }

    [Theory]
    [InlineData("0", 0L)]
    [InlineData("000", 0L)]
    [InlineData("004", 4L)]
    [InlineData("9223372036854775807", long.MaxValue)]
    [InlineData(" 9223372036854775807 ", long.MaxValue)]
    public void ValidAsciiIntegers_PreserveWhitespaceAndBoundaryValues(string token, long value) {
        var problem = new WEIGHTEDMAXCLIQUE($" ( ( {{ a }} , {{ }} ) , ( ( a , {token} ) ) ) ");
        Assert.Equal(value, problem.weights["a"]);
        Assert.True(new WeightedMaxCliqueSolutionVerifier().verify(problem, $" ( {{ a }} , {token} ) "));
        Assert.True(new WeightedMaxCliqueOptimalityVerifier().verify(problem, $" ( {{ a }} , {token} ) "));
        Assert.True(new WeightedMaxCliqueSolutionVerifier().verify(problem, " ( { } , 0 ) "));
        Assert.Equal(value == 0, new WeightedMaxCliqueOptimalityVerifier().verify(problem, " ( { } , 0 ) "));
    }

    [Theory]
    [InlineData("0\0")]
    [InlineData("0\0\0")]
    [InlineData("\u00000")]
    [InlineData("1\u00002")]
    [InlineData("4\0")]
    public async Task NumericNuls_ReturnStructuredBadRequests(string token) {
        using var client = factory.CreateClient();
        string input = $"(({{a}},{{}}),((a,{token})))";
        string[] solvers = ["weightedmaxcliquebruteforcesolver", "weightedmaxcliquebranchandboundsolver", "weightedmaxcliquegreedylocalsearchsolver"];
        foreach (string solver in solvers) {
            using var response = await client.PostAsJsonAsync($"/ProblemProvider/solve?solver={solver}", input, TestContext.Current.CancellationToken);
            await AssertParseError(response, "instance_parse_error", input);
        }
        foreach (string verifier in new[] { "weightedmaxcliquesolutionverifier", "weightedmaxcliqueoptimalityverifier" }) {
            using var badInstance = await client.PostAsJsonAsync($"/ProblemProvider/verify?verifier={verifier}",
                new { ProblemInstance = input, Certificate = "({},0)" }, TestContext.Current.CancellationToken);
            await AssertParseError(badInstance, "instance_parse_error", input);
            string certificate = $"({{}},{token})";
            using var badCertificate = await client.PostAsJsonAsync($"/ProblemProvider/verify?verifier={verifier}",
                new { ProblemInstance = "(({},{}),())", Certificate = certificate }, TestContext.Current.CancellationToken);
            await AssertParseError(badCertificate, "certificate_parse_error", certificate);
        }
    }

    private static async Task AssertParseError(HttpResponseMessage response, string code, string received) {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(code, body.GetProperty("error").GetString());
        Assert.Equal("Weighted Maximum Clique", body.GetProperty("problem").GetString());
        Assert.Equal(received, body.GetProperty("received").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("detail").GetString()));
    }
}
