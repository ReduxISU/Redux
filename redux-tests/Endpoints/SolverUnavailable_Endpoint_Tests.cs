using System.Net;
using System.Text;
using System.Text.Json;
using API.Interfaces;
using API.Problems.NPComplete.NPC_PRIMEFACTOR;
using API.Problems.NPComplete.NPC_PRIMEFACTOR.Solvers;
using API.Problems.NPComplete.NPC_PRIMEFACTOR.Verifiers;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// When the quantum service fails (down, not configured, an error status, or a response with no answer), a quantum
// solver throws SolverUnavailableException and every endpoint answers 503 solver_unavailable, instead of the solver
// returning {"error": "..."} as if it were the answer for verify, visualize or a reduction to use (#625). No test here
// touches the network: FakeQuantumService answers every request.
[Collection(QuantumServiceCollection.Name)]
public class SolverUnavailable_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public SolverUnavailable_Endpoint_Tests(AppFactory factory) {
        // Starts the app now, so Program.cs has set the real quantum settings before a test replaces them.
        _client = factory.CreateClient();
    }

    // Every solver that calls the quantum service, with an instance its problem accepts.
    public static TheoryData<string, string> QuantumSolvers() => new() {
        { "ShorsQuantumSolver", "15" },
        { "SATGroverSolver", "(x1 | !x2) & (x2 | x3)" },
        { "DeutschQuantumSolver", "(0,1)" },
        { "DeutschJozsaQuantumSolver", "(1, 1, 1, 1)" },
        { "BernsteinVaziraniQuantumSolver", "(0,1,0,1,1,0,1,0)" },
        { "UnstructuredGroverSolver", "(0, 1, 0, 0)" },
    };

    [Fact]
    public void QuantumSolvers_ListsEverySolverThatCallsTheQuantumService() {
        var callers = ProblemProvider.Solvers.Values
            .Where(t => File.ReadAllText(Path.Combine(ProjectSourcePath.Value,
                ((ISolver)Activator.CreateInstance(t)!).sourceFile)).Contains("QuantumServerAPI"))
            .Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal);
        var listed = QuantumSolvers().Select(row => (string)row.Data.Item1).OrderBy(n => n, StringComparer.Ordinal);
        Assert.Equal(listed, callers);
    }

    public static IEnumerable<object[]> Failures() {
        foreach (var row in QuantumSolvers())
            foreach (string failure in new[] { "down", "not configured", "error status", "no answer", "not json" })
                yield return new object[] { row.Data.Item1, row.Data.Item2, failure };
    }

    private static FakeQuantumService Fail(string failure) => failure switch {
        "down" => FakeQuantumService.Down(),
        "not configured" => FakeQuantumService.NotConfigured(),
        "error status" => FakeQuantumService.Answering("{\"detail\":\"boom\"}", HttpStatusCode.InternalServerError),
        "no answer" => FakeQuantumService.Answering("{\"qasm\":\"OPENQASM 2.0;\"}"),
        "not json" => FakeQuantumService.Answering("<html>Bad Gateway</html>"),
        _ => throw new ArgumentException(failure),
    };

    [Theory]
    [MemberData(nameof(Failures))]
    public void Solver_ServiceFails_ThrowsSolverUnavailable_NeverReturnsAnAnswer(string solver, string instance, string failure) {
        using var service = Fail(failure);
        var s = (ISolver)Activator.CreateInstance(ProblemProvider.Solvers[solver.ToLower()])!;

        var ex = Assert.Throws<SolverUnavailableException>(() => s.solve(instance));

        Assert.Equal(solver, ex.Solver);
        Assert.False(string.IsNullOrWhiteSpace(ex.ProblemName));
        Assert.StartsWith(failure == "not configured" ? "The quantum service is not configured" : "The quantum service did not give an answer", ex.Message);
    }

    [Theory]
    [MemberData(nameof(QuantumSolvers))]
    public async Task Solve_ServiceDown_Returns503SolverUnavailable(string solver, string instance) {
        using var service = FakeQuantumService.Down();

        var response = await _client.PostAsync($"/ProblemProvider/solve?solver={solver}", JsonBody(instance), TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("solver_unavailable", doc.RootElement.GetProperty("error").GetString());
        Assert.Equal(solver, doc.RootElement.GetProperty("solver").GetString());
        Assert.False(string.IsNullOrWhiteSpace(doc.RootElement.GetProperty("problem").GetString()));
        Assert.Contains("actively refused", doc.RootElement.GetProperty("detail").GetString());
        Assert.True(service.Handler.Requests >= 1);

        // The #933 error contract: a plain sentence, what to do next, and the other solvers to choose from.
        var s = (ISolver)Activator.CreateInstance(ProblemProvider.Solvers[solver.ToLower()])!;
        Assert.Equal($"{s.solverName} couldn't get an answer from the quantum service, so {doc.RootElement.GetProperty("problem").GetString()} wasn't solved.",
            doc.RootElement.GetProperty("message").GetString());
        string[] options = doc.RootElement.GetProperty("options").EnumerateArray().Select(o => o.GetString()!).ToArray();
        Assert.NotEmpty(options);
        Assert.DoesNotContain(solver, options);
        Assert.Contains("runs without the quantum service", doc.RootElement.GetProperty("hint").GetString());
    }

    // options are the problem's solvers as the navigation endpoints list them, minus the one that failed.
    [Theory]
    [MemberData(nameof(QuantumSolvers))]
    public async Task Solve_ServiceDown_OptionsAreTheProblemsOtherSolvers_AsNavigationListsThem(string solver, string instance) {
        using var service = FakeQuantumService.Down();
        var ex = Assert.Throws<SolverUnavailableException>(() =>
            ((ISolver)Activator.CreateInstance(ProblemProvider.Solvers[solver.ToLower()])!).solve(instance));
        string problemClass = SolverNavigationData.Entries.Single(e => e.className == solver).problemName;

        var nav = await _client.GetAsync($"/Navigation/Problem_SolversRefactor?chosenProblem={problemClass}&problemType=NPC", TestContext.Current.CancellationToken);
        var listed = JsonSerializer.Deserialize<string[]>(await nav.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!;

        var body = JsonSerializer.SerializeToElement(ex.Body());
        Assert.Equal(listed.Where(c => c != solver), body.GetProperty("options").EnumerateArray().Select(o => o.GetString()));
    }

    [Fact]
    public async Task Solve_Shor_ServiceDown_HintNamesTrialDivision() {
        using var service = FakeQuantumService.Down();

        var response = await _client.PostAsync("/ProblemProvider/solve?solver=ShorsQuantumSolver", JsonBody("15"), TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal("Shor's Algorithm couldn't get an answer from the quantum service, so Prime Factorization wasn't solved.",
            doc.RootElement.GetProperty("message").GetString());
        Assert.Equal("PrimeFactorSolver (Prime Factorization Trial Division) runs without the quantum service; or try again later.",
            doc.RootElement.GetProperty("hint").GetString());
        Assert.Equal(new[] { "PrimeFactorSolver" }, doc.RootElement.GetProperty("options").EnumerateArray().Select(o => o.GetString()));
    }

    // With no solver that runs without the quantum service, the hint says how to get the service back instead.
    [Fact]
    public void Body_NoLocalAlternative_HintPointsAtTheQuantumService() {
        var body = JsonSerializer.SerializeToElement(new SolverUnavailableException("NoSuchQuantumSolver", "Some Solver", "Some Problem", "down").Body());

        Assert.Equal("Try again later, or check that the quantum service is running and configured (see Tools/QUANTUM_API_README.md).",
            body.GetProperty("hint").GetString());
        Assert.Empty(body.GetProperty("options").EnumerateArray());
        Assert.Equal("down", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Solve_Shor_ServiceSendsNoAnswer_Returns503_NotTheRawResponse() {
        using var service = FakeQuantumService.Answering("{\"status\":\"busy\"}");

        var response = await _client.PostAsync("/ProblemProvider/solve?solver=ShorsQuantumSolver", JsonBody("15"), TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("solver_unavailable", doc.RootElement.GetProperty("error").GetString());
        Assert.Equal("Prime Factorization", doc.RootElement.GetProperty("problem").GetString());
        Assert.Contains("'answer'", doc.RootElement.GetProperty("detail").GetString());
    }

    // An answer the service did send is still passed on as before, even one the verifier will reject: only no answer is
    // an error.
    [Theory]
    [InlineData("15", "{\"answer\":[3,5]}", "(3,5)")]
    [InlineData("15", "{\"answer\":\"(3,5)\"}", "(3,5)")]
    [InlineData("21", "{\"answer\":21}", "21")]
    public void Shor_ServiceAnswers_PassesTheAnswerOn(string instance, string reply, string answer) {
        using var service = FakeQuantumService.Answering(reply);
        Assert.Equal(answer, new ShorsQuantumSolver().solve(new PRIMEFACTOR(instance)));
    }

    // #934 review: a reply with the field but no usable bit string is no answer either. It's checked inside Ask, so the
    // endpoint answers 503 as for a missing answer, not 500 from converting it afterwards.
    [Theory]
    [InlineData("{\"answer_bitstring\":\"xyz\"}")]
    [InlineData("{\"answer_bitstring\":\"\"}")]
    [InlineData("{\"answer_bitstring\":\"1\"}")]     // (0, 1, 0, 0) is 4 values, so 2 bits
    [InlineData("{\"answer_bitstring\":\"101\"}")]
    [InlineData("{\"answer_bitstring\":\"12\"}")]
    public async Task Solve_UnstructuredGrover_MalformedBitString_Returns503(string reply) {
        using var service = FakeQuantumService.Answering(reply);

        var response = await _client.PostAsync("/ProblemProvider/solve?solver=UnstructuredGroverSolver", JsonBody("(0, 1, 0, 0)"), TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("solver_unavailable", doc.RootElement.GetProperty("error").GetString());
        Assert.Equal("UnstructuredGroverSolver", doc.RootElement.GetProperty("solver").GetString());
        Assert.Contains("'answer_bitstring' is not 2 binary digits", doc.RootElement.GetProperty("detail").GetString());
    }

    // The same for Bernstein-Vazirani, which used to wrap any characters, or a bit string of the wrong length, into a
    // certificate and pass that on.
    [Theory]
    [InlineData("{\"answer\":\"xyz\"}")]
    [InlineData("{\"answer\":\"10a1\"}")]
    [InlineData("{\"answer\":\"1\"}")]       // (0,1,0,1,1,0,1,0) is 8 values, so 3 bits
    [InlineData("{\"answer\":\"10\"}")]
    [InlineData("{\"answer\":\"1011\"}")]
    public async Task Solve_BernsteinVazirani_MalformedAnswer_Returns503(string reply) {
        using var service = FakeQuantumService.Answering(reply);

        var response = await _client.PostAsync("/ProblemProvider/solve?solver=BernsteinVaziraniQuantumSolver", JsonBody("(0,1,0,1,1,0,1,0)"), TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("solver_unavailable", doc.RootElement.GetProperty("error").GetString());
        Assert.Contains("'answer' is not 3 binary digits", doc.RootElement.GetProperty("detail").GetString());
    }

    // The solver throws before it has an answer, so nothing after it runs: the visualization's solved picture (which
    // would ask the service again for the circuit, and which receives the answer as the certificate) is never built.
    [Fact]
    public async Task Visualize_Shor_ServiceDown_Returns503_AndStopsAtTheSolver() {
        using var service = FakeQuantumService.Down();

        var response = await _client.PostAsync(
            "/ProblemProvider/visualize?visualization=ShorsDefaultVisualization&solver=ShorsQuantumSolver",
            JsonBody("15"), TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("\"solver_unavailable\"", body);
        Assert.Equal(1, service.Handler.Requests);
    }

    [Fact]
    public async Task Visualize_GroverDefault_ServiceDown_Returns503() {
        using var service = FakeQuantumService.Down();

        var response = await _client.PostAsync(
            "/ProblemProvider/visualize?visualization=SATGroverVisualization&format=frames",
            JsonBody("(x1 | !x2) & (x2 | x3)"), TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("\"SATGroverSolver\"", body);
    }

    // The verifier would have been handed {"error": ...} before #625; now no certificate exists to hand it.
    [Fact]
    public void Shor_ServiceDown_VerifierNeverSeesAnError() {
        using var service = FakeQuantumService.Down();
        var problem = new PRIMEFACTOR();
        string? certificate = null;

        Assert.Throws<SolverUnavailableException>(() => certificate = new ShorsQuantumSolver().solve(problem));
        Assert.Null(certificate);
    }

    // --- The default solver is classical (#625) ----------------------------------------------------------------

    [Fact]
    public void PrimeFactor_DefaultSolver_IsClassical_Solves15_AndVerifies() {
        var problem = new PRIMEFACTOR();
        Assert.IsType<PrimeFactorSolver>(problem.defaultSolver);

        string answer = problem.defaultSolver.solve(problem);

        Assert.Equal("(3,5)", answer);
        Assert.True(new PrimeFactorVerifier().verify(problem, answer));
    }

    [Fact]
    public async Task PrimeFactor_DefaultSolver_WorksWithTheQuantumServiceDown() {
        using var service = FakeQuantumService.Down();
        string solver = new PRIMEFACTOR().defaultSolver.GetType().Name;

        var solved = await _client.PostAsync($"/ProblemProvider/solve?solver={solver}", JsonBody("15"), TestContext.Current.CancellationToken);
        string answer = JsonSerializer.Deserialize<string>(await solved.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!;
        Assert.Equal(HttpStatusCode.OK, solved.StatusCode);
        Assert.Equal("(3,5)", answer);

        string verifyJson = JsonSerializer.Serialize(new { Certificate = answer, ProblemInstance = "15" });
        var verified = await _client.PostAsync("/ProblemProvider/verify?verifier=PrimeFactorVerifier",
            new StringContent(verifyJson, Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);
        Assert.Equal("True", JsonSerializer.Deserialize<string>(await verified.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)));
        Assert.Equal(0, service.Handler.Requests);
    }

    // The default visualization stays Shor's circuit, and it still runs with the service down: its own solver has
    // been the classical one since it was chosen per visualization, so the default-solver change does not reach it.
    [Fact]
    public async Task PrimeFactor_DefaultVisualization_IsShorsCircuit_AndRunsWithTheServiceDown() {
        using var service = FakeQuantumService.Down();
        var problem = new PRIMEFACTOR();
        Assert.IsType<ShorsDefaultVisualization>(problem.defaultVisualization);

        var response = await _client.PostAsync("/ProblemProvider/visualize?visualization=ShorsDefaultVisualization",
            JsonBody("15"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("(3,5)", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private static StringContent JsonBody(string value) =>
        new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");
}
