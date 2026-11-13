using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using API.Interfaces;
using API.Problems.NPHard.NPH_WEIGHTEDMAXCLIQUE;
using API.Problems.NPHard.NPH_WEIGHTEDMAXCLIQUE.Solvers;
using API.Problems.NPHard.NPH_WEIGHTEDMAXCLIQUE.Verifiers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

public class WeightedMaxCliqueIndependent_Endpoint_Tests(AppFactory factory) : IClassFixture<AppFactory> {
    private static readonly string[] SolverNames = ["weightedmaxcliquebruteforcesolver", "weightedmaxcliquebranchandboundsolver", "weightedmaxcliquegreedylocalsearchsolver"];
    private static readonly string[] VerifierNames = ["weightedmaxcliquesolutionverifier", "weightedmaxcliqueoptimalityverifier"];

    [Fact]
    public async Task RandomApiResults_MatchDirectCallsAndIndependentOracle() {
        using var client = factory.CreateClient();
        var random = new Random(1008860);
        for (int trial = 0; trial < 30; trial++) {
            var graph = IndependentCliqueGraph.Generate(random, trial % 9, (trial % 5) / 4.0, trial % 6);
            string input = graph.Input();
            var problem = new WEIGHTEDMAXCLIQUE(input);
            long optimum = graph.Optimum();
            var solvers = WeightedMaxCliqueIndependent_Tests.Solvers();
            for (int i = 0; i < solvers.Length; i++) {
                using var response = await client.PostAsJsonAsync($"/ProblemProvider/solve?solver={SolverNames[i]}", input, TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                string certificate = (await response.Content.ReadFromJsonAsync<string>(TestContext.Current.CancellationToken))!;
                Assert.Equal(solvers[i].solve(problem), certificate);
                long weight = graph.CheckCertificate(certificate);
                Assert.True(i < 2 ? weight == optimum : weight <= optimum);
                foreach (string verifier in VerifierNames) {
                    using var verified = await client.PostAsJsonAsync($"/ProblemProvider/verify?verifier={verifier}",
                        new { ProblemInstance = input, Certificate = certificate }, TestContext.Current.CancellationToken);
                    Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
                    bool direct = verifier == VerifierNames[0]
                        ? new WeightedMaxCliqueSolutionVerifier().verify(problem, certificate)
                        : new WeightedMaxCliqueOptimalityVerifier().verify(problem, certificate);
                    string? verdict = await verified.Content.ReadFromJsonAsync<string>(TestContext.Current.CancellationToken);
                    Assert.Equal((verifier == VerifierNames[0] || weight == optimum).ToString(), verdict);
                    Assert.Equal(direct.ToString(), verdict);
                }
            }
        }
    }

    [Fact]
    public async Task InfoAndDiscovery_ExposeProblemSolversAndVerifiers() {
        using var client = factory.CreateClient();
        foreach (string component in SolverNames.Concat(VerifierNames).Prepend("weightedmaxclique")) {
            using var response = await client.GetAsync($"/ProblemProvider/info?interface={component}", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
            Assert.Equal(JsonValueKind.Object, body.ValueKind);
            if (component == "weightedmaxclique") {
                Assert.Equal("Weighted Maximum Clique Exact Brute Force", body.GetProperty("defaultSolver").GetProperty("solverName").GetString());
                Assert.Equal("Weighted Maximum Clique Solution Verifier", body.GetProperty("defaultVerifier").GetProperty("verifierName").GetString());
            }
        }
        Assert.IsType<WeightedMaxCliqueBruteForceSolver>(((IProblem)new WEIGHTEDMAXCLIQUE()).defaultSolver);
        Assert.IsType<WeightedMaxCliqueSolutionVerifier>(((IProblem)new WEIGHTEDMAXCLIQUE()).defaultVerifier);
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("(({a},{}),((a,-1)))")]
    [InlineData("(({a},{}),((a,9223372036854775808)))")]
    [InlineData("(({a,b},{}),((a,9223372036854775807),(b,1)))")]
    [InlineData("(({a},{{a,a}}),((a,1)))")]
    [InlineData("(({a},{}),((a,1)))trailing")]
    [InlineData("(({a},{}),((a,4\u0000)))")]
    public async Task MalformedInstances_ReturnStructuredErrorsFromSolveAndVerify(string input) {
        using var client = factory.CreateClient();
        foreach (string solver in SolverNames) {
            using var response = await client.PostAsJsonAsync($"/ProblemProvider/solve?solver={solver}", input, TestContext.Current.CancellationToken);
            await Error(response, HttpStatusCode.BadRequest, "instance_parse_error");
        }
        foreach (string verifier in VerifierNames) {
            using var response = await client.PostAsJsonAsync($"/ProblemProvider/verify?verifier={verifier}",
                new { ProblemInstance = input, Certificate = "({},0)" }, TestContext.Current.CancellationToken);
            await Error(response, HttpStatusCode.BadRequest, "instance_parse_error");
        }
    }

    [Theory]
    [InlineData("({a,a},8)")]
    [InlineData("({a},4)()")]
    [InlineData("({a},4,5)")]
    [InlineData("({a},٤)")]
    [InlineData("({a,b,c},12\u0000)")]
    public async Task MalformedCertificates_ReturnStructuredErrorsFromBothVerifiers(string certificate) {
        using var client = factory.CreateClient();
        foreach (string verifier in VerifierNames) {
            using var response = await client.PostAsJsonAsync($"/ProblemProvider/verify?verifier={verifier}",
                new { ProblemInstance = WEIGHTEDMAXCLIQUE.DefaultInstance, Certificate = certificate }, TestContext.Current.CancellationToken);
            await Error(response, HttpStatusCode.BadRequest, "certificate_parse_error");
        }
    }

    [Fact]
    public async Task AboveTwentyVertices_OnlyExhaustiveOperationsReturnLimitErrors() {
        using var client = factory.CreateClient();
        var graph = IndependentCliqueGraph.Generate(new Random(860), 24, 1, 1);
        string input = graph.Input();
        foreach (string solver in SolverNames) {
            using var response = await client.PostAsJsonAsync($"/ProblemProvider/solve?solver={solver}", input, TestContext.Current.CancellationToken);
            if (solver == SolverNames[0]) await Error(response, HttpStatusCode.BadRequest, "exact_search_limit_exceeded");
            else {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                long weight = graph.CheckCertificate((await response.Content.ReadFromJsonAsync<string>(TestContext.Current.CancellationToken))!);
                Assert.True(solver == SolverNames[1] ? weight == 24 : weight <= 24);
            }
        }
        foreach (string verifier in VerifierNames) {
            using var response = await client.PostAsJsonAsync($"/ProblemProvider/verify?verifier={verifier}",
                new { ProblemInstance = input, Certificate = graph.Certificate(Enumerable.Range(0, 24).ToArray()) }, TestContext.Current.CancellationToken);
            if (verifier == VerifierNames[1]) await Error(response, HttpStatusCode.BadRequest, "exact_search_limit_exceeded");
            else {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal("True", await response.Content.ReadFromJsonAsync<string>(TestContext.Current.CancellationToken));
            }
        }
    }

    [Fact]
    public async Task ExhaustiveSolveDeadline_ReturnsTimeLimitRatherThanCertificate() {
        using var timed = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { [ComputeLimits.TimeLimitKey] = "0.001" })));
        using var client = timed.CreateClient();
        string input = IndependentCliqueGraph.Generate(new Random(860), 20, 1, 1).Input();
        using var response = await client.PostAsJsonAsync($"/ProblemProvider/solve?solver={SolverNames[0]}", input, TestContext.Current.CancellationToken);
        await Error(response, HttpStatusCode.UnprocessableEntity, "time_limit");
    }

    private static async Task Error(HttpResponseMessage response, HttpStatusCode status, string code) {
        Assert.Equal(status, response.StatusCode);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(code, body.GetProperty("error").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("detail").GetString()));
    }
}
