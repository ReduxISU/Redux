using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

public class ProblemProvider_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public ProblemProvider_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    // ── /ProblemProvider/info ─────────────────────────────────────────────────

    [Theory]
    [InlineData("sat3")]
    [InlineData("clique")]
    [InlineData("vertexcover")]
    [InlineData("independentset")]
    public async Task Info_KnownProblem_Returns200(string name) {
        var response = await _client.GetAsync($"/ProblemProvider/info?interface={name}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("sat3")]
    [InlineData("clique")]
    public async Task Info_KnownProblem_ReturnsNonEmptyBody(string name) {
        var response = await _client.GetAsync($"/ProblemProvider/info?interface={name}", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.False(string.IsNullOrWhiteSpace(body));
    }

    // ── /ProblemProvider/verify ───────────────────────────────────────────────

    [Fact]
    public async Task Verify_ValidSAT3Certificate_ReturnsTrue() {
        var body = new { Certificate = "x1:True,x2:False,x3:True", ProblemInstance = "(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | x1)" };
        var response = await _client.PostAsJsonAsync("/ProblemProvider/verify?verifier=sat3verifier", body, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("True", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Verify_InvalidSAT3Certificate_ReturnsFalse() {
        var body = new { Certificate = "x1:False,x2:False,x3:False", ProblemInstance = "(x1 | x2 | x3) & (!x1 | !x2 | !x3) & (x1 | !x2 | x3)" };
        var response = await _client.PostAsJsonAsync("/ProblemProvider/verify?verifier=sat3verifier", body, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("False", result, StringComparison.OrdinalIgnoreCase);
    }

    // ── /ProblemProvider/solve ────────────────────────────────────────────────

    [Fact]
    public async Task Solve_SAT3BacktrackingSolver_Returns200() {
        var instance = "\"(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | x1)\"";
        var content = new StringContent(instance, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/solve?solver=sat3backtrackingsolver", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Solve_SAT3BacktrackingSolver_ReturnsNonEmptyResult() {
        var instance = "\"(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | x1)\"";
        var content = new StringContent(instance, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/solve?solver=sat3backtrackingsolver", content, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.False(string.IsNullOrWhiteSpace(body));
    }

    // ── /ProblemProvider/reduce ───────────────────────────────────────────────

    [Fact]
    public async Task Reduce_SAT3ToClique_Returns200() {
        var instance = "\"(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | x1)\"";
        var content = new StringContent(instance, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/reduce?reduction=sipserreducetocliquestandard", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Reduce_SAT3ToClique_ReturnsNonEmptyResult() {
        var instance = "\"(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | x1)\"";
        var content = new StringContent(instance, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/reduce?reduction=sipserreducetocliquestandard", content, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.False(string.IsNullOrWhiteSpace(body));
    }

    // ── /ProblemProvider/mapSolution ──────────────────────────────────────────

    [Fact]
    public async Task MapSolution_ValidSAT3Solution_Returns200() {
        var instance = "\"(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | x1)\"";
        var content = new StringContent(instance, Encoding.UTF8, "application/json");
        var solution = Uri.EscapeDataString("(x1:True,x2:False,x3:True)");
        var response = await _client.PostAsync($"/ProblemProvider/mapSolution?reduction=sipserreducetocliquestandard&solution={solution}", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MapSolution_CliqueShapedCertificate_Returns400WithParseError() {
        // The original gut-check: a CLIQUE-shaped certificate fed to the SAT3→CLIQUE
        // forward mapper used to throw IndexOutOfRangeException → HTTP 500. It must
        // now surface as a structured 400.
        var instance = "\"(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | x1)\"";
        var content = new StringContent(instance, Encoding.UTF8, "application/json");
        var solution = Uri.EscapeDataString("{x1_0,x2_1,!x3_3}");
        var response = await _client.PostAsync($"/ProblemProvider/mapSolution?reduction=sipserreducetocliquestandard&solution={solution}", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("reduction_input_parse_error", body);
    }

    [Fact]
    public async Task MapSolution_NonSipserInstance_Returns400WithParseError() {
        // SipserReduceToSAT3 requires Sipser-formatted CLIQUE node names. A plain
        // numeric-node CLIQUE instance is well-formed as a CLIQUE but invalid for
        // this reduction, and must surface as a 400 rather than a 500.
        var instance = "\"(({1,2,3,4,5,6},{{4,1},{1,2},{4,3},{3,2},{2,4},{5,2},{3,5},{5,4},{3,6},{6,4},{1,6}}),4)\"";
        var content = new StringContent(instance, Encoding.UTF8, "application/json");
        var solution = Uri.EscapeDataString("{x1_0}");
        var response = await _client.PostAsync($"/ProblemProvider/mapSolution?reduction=sipserreducetosat3&solution={solution}", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("reduction_input_parse_error", body);
    }

    [Fact]
    public async Task MapSolution_BadCertificate_Returns400WithParseError() {
        // Valid Sipser-formatted CLIQUE instance, but the certificate carries
        // non-Sipser node names — the certificate-side throw site.
        var instance = "\"(({x1_0,x2_1,x3_2},{{x1_0,x2_1},{x2_1,x3_2},{x1_0,x3_2}}),3)\"";
        var content = new StringContent(instance, Encoding.UTF8, "application/json");
        var solution = Uri.EscapeDataString("{a,b}");
        var response = await _client.PostAsync($"/ProblemProvider/mapSolution?reduction=sipserreducetosat3&solution={solution}", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("reduction_input_parse_error", body);
    }

    // ── unparsable-instance handling on the remaining reduction/visualization
    //    endpoints (issue #355) ─────────────────────────────────────────────
    // Each of these builds a SAT3 from the posted instance; a malformed SAT3
    // formula ("1" is not a valid literal) used to escape as an unhandled
    // exception → HTTP 500, and must now surface as a structured 400.

    // A CLIQUE-shaped string posted to a SAT3-sourced reduction — the exact
    // shape from the production stack trace that motivated issue #355.
    private const string InvalidSat3Instance = "\"(1 | 2 | 3)\"";

    [Fact]
    public async Task Reduce_InvalidInstance_Returns400WithParseError() {
        var content = new StringContent(InvalidSat3Instance, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/reduce?reduction=sipserreducetocliquestandard", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("instance_parse_error", body);
    }

    [Fact]
    public async Task Gadgets_InvalidInstance_Returns400WithParseError() {
        var content = new StringContent(InvalidSat3Instance, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/gadgets?reduction=sipserreducetocliquestandard", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("instance_parse_error", body);
    }

    [Fact]
    public async Task VisualizeReduction_InvalidInstance_Returns400WithParseError() {
        var content = new StringContent(InvalidSat3Instance, Encoding.UTF8, "application/json");
        var solution = Uri.EscapeDataString("(x1:True)");
        var response = await _client.PostAsync($"/ProblemProvider/visualizeReduction?reduction=sipserreducetocliquestandard&solution={solution}", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("instance_parse_error", body);
    }

    [Fact]
    public async Task Visualize_InvalidInstance_Returns400WithParseError() {
        var content = new StringContent(InvalidSat3Instance, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/visualize?visualization=sat3defaultvisualization", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("instance_parse_error", body);
    }

    [Fact]
    public async Task Visualize_KnownVisualization_Returns200WithNonEmptyResult() {
        var instance = "\"(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | x1)\"";
        var content = new StringContent(instance, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/visualize?visualization=sat3defaultvisualization", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.False(string.IsNullOrWhiteSpace(body));
    }

    [Fact]
    public async Task Visualize_UnknownVisualization_Returns400() {
        var instance = "\"(x1 | !x2 | x3)\"";
        var content = new StringContent(instance, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/visualize?visualization=nosuchvisualization", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("unknown_visualization", body);
    }

    // ── /ProblemProvider/visualizeReduction ───────────────────────────────────

    [Fact]
    public async Task VisualizeReduction_SAT3ToClique_Returns200WithNonEmptyResult() {
        var instance = "\"(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | x1)\"";
        var content = new StringContent(instance, Encoding.UTF8, "application/json");
        var solution = Uri.EscapeDataString("(x1:True,x2:False,x3:True)");
        var response = await _client.PostAsync($"/ProblemProvider/visualizeReduction?reduction=sipserreducetocliquestandard&solution={solution}", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.False(string.IsNullOrWhiteSpace(body));
    }

    [Fact]
    public async Task VisualizeReduction_UnknownReduction_Returns400() {
        var instance = "\"(x1 | !x2 | x3)\"";
        var content = new StringContent(instance, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/visualizeReduction?reduction=nosuchreduction&solution=x", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("unknown_reduction", body);
    }

    // ── /ProblemProvider/reduce, mapSolution, gadgets: unknown_reduction ──────

    [Fact]
    public async Task Reduce_UnknownReduction_Returns400() {
        var instance = "\"(x1 | !x2 | x3)\"";
        var content = new StringContent(instance, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/reduce?reduction=nosuchreduction", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("unknown_reduction", body);
    }

    [Fact]
    public async Task MapSolution_UnknownReduction_Returns400() {
        var instance = "\"(x1 | !x2 | x3)\"";
        var content = new StringContent(instance, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/mapSolution?reduction=nosuchreduction&solution=x", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("unknown_reduction", body);
    }

    [Fact]
    public async Task Gadgets_SAT3ToClique_Returns200WithNonEmptyResult() {
        var instance = "\"(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | x1)\"";
        var content = new StringContent(instance, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/gadgets?reduction=sipserreducetocliquestandard", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.False(string.IsNullOrWhiteSpace(body));
    }

    [Fact]
    public async Task Gadgets_UnknownReduction_Returns400() {
        var instance = "\"(x1 | !x2 | x3)\"";
        var content = new StringContent(instance, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/gadgets?reduction=nosuchreduction", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("unknown_reduction", body);
    }

    // ── /ProblemProvider/info: unknown interface ──────────────────────────────

    [Fact]
    public async Task Info_UnknownInterface_Returns400() {
        var response = await _client.GetAsync("/ProblemProvider/info?interface=nosuchinterface", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("unknown_interface", body);
    }

    // ── /ProblemProvider/solve: error paths ───────────────────────────────────

    [Fact]
    public async Task Solve_UnknownSolver_Returns400() {
        var instance = "\"(x1 | !x2 | x3)\"";
        var content = new StringContent(instance, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/solve?solver=nosuchsolver", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("unknown_solver", body);
    }

    [Fact]
    public async Task Solve_InvalidInstance_Returns400WithParseError() {
        var content = new StringContent(InvalidSat3Instance, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/solve?solver=sat3backtrackingsolver", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("instance_parse_error", body);
    }

    // ── /ProblemProvider/verify: error paths ──────────────────────────────────

    [Fact]
    public async Task Verify_InvalidInstance_Returns400WithParseError() {
        var body = new { Certificate = "x1:True", ProblemInstance = "(1 | 2 | 3)" };
        var response = await _client.PostAsJsonAsync("/ProblemProvider/verify?verifier=sat3verifier", body, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var result = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("instance_parse_error", result);
    }

    [Fact]
    public async Task Verify_EmptyCertificate_Returns400WithCertificateParseError() {
        var body = new { Certificate = "", ProblemInstance = "(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | x1)" };
        var response = await _client.PostAsJsonAsync("/ProblemProvider/verify?verifier=sat3verifier", body, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var result = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("certificate_parse_error", result);
    }

    // ── /ProblemProvider/problemInstance ──────────────────────────────────────

    [Fact]
    public async Task ProblemInstance_ValidSAT3Instance_Returns200WithNonEmptyResult() {
        var instance = "\"(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | x1)\"";
        var content = new StringContent(instance, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/problemInstance?problem=sat3", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.False(string.IsNullOrWhiteSpace(body));
    }

    [Fact]
    public async Task ProblemInstance_InvalidInstance_Returns400WithParseError() {
        var content = new StringContent(InvalidSat3Instance, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/problemInstance?problem=sat3", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("instance_parse_error", body);
    }
}
