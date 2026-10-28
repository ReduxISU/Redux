using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The app with a 1 ms limit on every compute request (#935).
public class TinyComputeLimitAppFactory : AppFactory {
    public const string Seconds = "0.001";

    protected override void ConfigureWebHost(IWebHostBuilder builder) {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { [ComputeLimits.TimeLimitKey] = Seconds }));
    }
}

// Large inputs that take far longer than 1 ms (but well under the default limit), built once.
internal static class SlowInputs {
    // A 3SAT formula of 30 clauses over 12 variables. Sipser's reduction turns it into a 90-node CLIQUE graph with
    // about 3,600 edges, which takes far longer than 1 ms to build, serialize and parse, even on a fast CI runner (15
    // clauses took only 20 ms there), and well under a second (TimeLimit_NormalRequest_Tests checks both ends), so the
    // work abandoned at the limit soon ends.
    internal static readonly string Sat3 = string.Join(" & ", Enumerable.Range(0, 30).Select(i =>
        $"(x{i % 12 + 1} | !x{(i * 7 + 3) % 12 + 1} | x{(i * 13 + 5) % 12 + 1})"));

    internal static readonly Lazy<string> CliqueGraph = new(() =>
        new API.Problems.NPComplete.NPC_SAT3.ReduceTo.NPC_CLIQUE.SipserReduceToCliqueStandard(Sat3).reductionTo.instance);

    // Three nodes of the reduced graph (one per clause of the first three), a well-formed certificate.
    internal const string CliqueCertificate = "{x1_0,x2_1,x3_2}";
}

[Collection(TimingCollection.Name)]
public class TimeLimit_Endpoint_Tests : IClassFixture<TinyComputeLimitAppFactory> {
    private readonly HttpClient _client;

    public TimeLimit_Endpoint_Tests(TinyComputeLimitAppFactory factory) {
        _client = factory.CreateClient();
    }

    private async Task<(HttpStatusCode Status, JsonElement Body, TimeSpan Took)> Post(string path, object body) {
        var sw = Stopwatch.StartNew();
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync(path, content, TestContext.Current.CancellationToken);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        sw.Stop();
        return (response.StatusCode, JsonDocument.Parse(text).RootElement.Clone(), sw.Elapsed);
    }

    // The #933 error contract: a code, a plain message, the technical detail, the limit, and a hint; never an answer.
    private static void AssertTimeLimitBody(JsonElement body, string job) {
        Assert.Equal(JsonValueKind.Object, body.ValueKind);
        Assert.Equal("time_limit", body.GetProperty("error").GetString());
        Assert.Equal($"The {job} ran past the {TinyComputeLimitAppFactory.Seconds}-second time limit, so it was stopped.",
            body.GetProperty("message").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("detail").GetString()));
        Assert.Equal(0.001, body.GetProperty("limitSeconds").GetDouble());
        Assert.StartsWith("Try a smaller instance", body.GetProperty("hint").GetString());
    }

    [Fact]
    public async Task Solve_TypedSolverPastTheLimit_Returns422_TimeLimit() {
        var (status, body, took) = await Post("/ProblemProvider/solve?solver=NFASolver", StepTimeLimit_Tests.SlowNfa);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.True(took < TimeSpan.FromSeconds(5), $"took {took}");
        AssertTimeLimitBody(body, "solve");
        Assert.Equal(ComputeLimits.StoppedBySolverDetail, body.GetProperty("detail").GetString());
        Assert.Equal("NFASolver", body.GetProperty("solver").GetString());
        Assert.Equal(JsonValueKind.Array, body.GetProperty("options").ValueKind);
    }

    [Theory]
    [MemberData(nameof(SolverTimeLimit_Tests.HardInstances), MemberType = typeof(SolverTimeLimit_Tests))]
    public async Task Solve_LegacySolverPastTheLimit_Returns422_TimeLimit(string solver, string instance) {
        var (status, body, took) = await Post($"/ProblemProvider/solve?solver={solver}", instance);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.True(took < TimeSpan.FromSeconds(5), $"took {took}");
        AssertTimeLimitBody(body, "solve");
        Assert.Equal(ComputeLimits.StoppedBySolverDetail, body.GetProperty("detail").GetString());
        Assert.Equal(solver, body.GetProperty("solver").GetString());
        Assert.DoesNotContain(solver, body.GetProperty("options").EnumerateArray().Select(o => o.GetString()));
    }

    [Fact]
    public async Task Verify_PastTheLimit_Returns422_TimeLimit() {
        var verify = new { ProblemInstance = SlowInputs.CliqueGraph.Value, Certificate = SlowInputs.CliqueCertificate };
        var (status, body, took) = await Post("/ProblemProvider/verify?verifier=CliqueVerifier", verify);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.True(took < TimeSpan.FromSeconds(5), $"took {took}");
        AssertTimeLimitBody(body, "verification");
        Assert.Equal(ComputeLimits.BackstopDetail, body.GetProperty("detail").GetString());
        Assert.Equal("CliqueVerifier", body.GetProperty("verifier").GetString());
    }

    [Fact]
    public async Task Reduce_PastTheLimit_Returns422_TimeLimit() {
        var (status, body, took) = await Post("/ProblemProvider/reduce?reduction=SipserReduceToCliqueStandard", SlowInputs.Sat3);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.True(took < TimeSpan.FromSeconds(5), $"took {took}");
        AssertTimeLimitBody(body, "reduction");
        Assert.Equal(ComputeLimits.BackstopDetail, body.GetProperty("detail").GetString());
        Assert.Equal("SipserReduceToCliqueStandard", body.GetProperty("reduction").GetString());
    }

    [Fact]
    public async Task Visualize_StoppedSolve_StillDrawsTheStoppedRun() {
        // #885's behaviour is kept: a solver that stops itself is drawn as a stopped run (200), not an error.
        var (status, body, _) = await Post("/ProblemProvider/visualize?visualization=nfavisualization&format=frames", StepTimeLimit_Tests.SlowNfa);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(new[] { "type", "payload", "frames" }, body.EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public async Task ParseErrors_StillAnswer400() {
        var (status, body, _) = await Post("/ProblemProvider/solve?solver=CliqueBruteForce", "not a graph");
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("instance_parse_error", body.GetProperty("error").GetString());
    }
}

// With the default limit (10 s) a normal request is answered exactly as before the limit existed.
[Collection(TimingCollection.Name)]
public class TimeLimit_NormalRequest_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public TimeLimit_NormalRequest_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private async Task<(HttpStatusCode Status, string Body)> Post(string path, object body) {
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync(path, content, TestContext.Current.CancellationToken);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public static TheoryData<string> Solvers {
        get {
            var data = new TheoryData<string>();
            foreach (var name in new[] { "Sat3BacktrackingSolver", "CliqueBruteForce", "NFASolver", "SSSPSolver", "KnapsackDP", "SubsetSumBruteForce" })
                data.Add(name);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Solvers))]
    public async Task Solve_DefaultInstance_AnswersExactlyAsTheSolverDoes(string solver) {
        Type type = ProblemProvider.Solvers[solver.ToLower()];
        string instance = SolverTimeLimit_Tests.DefaultInstance(type);
        string expected = JsonSerializer.Serialize(((ISolver)Activator.CreateInstance(type)!).solve(instance), Indented);
        var (status, body) = await Post($"/ProblemProvider/solve?solver={solver}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(expected, body);
    }

    [Fact]
    public async Task Verify_DefaultInstance_AnswersAsBefore() {
        var (status, body) = await Post("/ProblemProvider/verify?verifier=CliqueVerifier",
            new { ProblemInstance = new API.Problems.NPComplete.NPC_CLIQUE.CLIQUE().defaultInstance, Certificate = "{1,2,4}" });
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains(body, new[] { JsonSerializer.Serialize("True", Indented), JsonSerializer.Serialize("False", Indented) });
    }

    // The slow inputs of TimeLimit_Endpoint_Tests finish within the default limit, and take far longer than its 1 ms:
    // at least 10 ms, ten times the limit. A fixed floor much above that fails on a fast runner without telling us
    // anything about the 1 ms tests.
    [Fact]
    public async Task SlowInputs_FinishWithinTheDefaultLimit_ButNotWithin1ms() {
        var sw = Stopwatch.StartNew();
        var (status, _) = await Post("/ProblemProvider/reduce?reduction=SipserReduceToCliqueStandard", SlowInputs.Sat3);
        var reduceTook = sw.Elapsed;
        Assert.Equal(HttpStatusCode.OK, status);
        var verify = new { ProblemInstance = SlowInputs.CliqueGraph.Value, Certificate = SlowInputs.CliqueCertificate };
        sw.Restart();
        (status, _) = await Post("/ProblemProvider/verify?verifier=CliqueVerifier", verify);
        var verifyTook = sw.Elapsed;
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True(reduceTook > TimeSpan.FromMilliseconds(10) && reduceTook < TimeSpan.FromSeconds(5), $"reduce took {reduceTook}");
        Assert.True(verifyTook > TimeSpan.FromMilliseconds(10) && verifyTook < TimeSpan.FromSeconds(5), $"verify took {verifyTook}");
    }

    [Fact]
    public async Task Reduce_DefaultInstance_AnswersAsBefore() {
        string instance = new API.Problems.NPComplete.NPC_SAT3.SAT3().defaultInstance;
        // Serialized as the endpoint does: by the declared type, IReduction.
        IReduction reduction = new API.Problems.NPComplete.NPC_SAT3.ReduceTo.NPC_CLIQUE.SipserReduceToCliqueStandard(instance);
        string expected = JsonSerializer.Serialize(reduction, new JsonSerializerOptions { WriteIndented = true });
        var (status, body) = await Post("/ProblemProvider/reduce?reduction=SipserReduceToCliqueStandard", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(expected, body);
    }
}
