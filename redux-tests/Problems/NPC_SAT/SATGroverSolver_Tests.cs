using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;
using API.Problems.NPComplete.NPC_SAT;
using API.Problems.NPComplete.NPC_SAT.Solvers;
using API.Problems.NPComplete.NPC_SAT.Verifiers;

namespace redux_tests;
#pragma warning disable CS1591

// Grover's SAT solver against a fake quantum service, installed for each test's own async flow (#809). The service
// (ReduxISU/quantumsolver, sat_quantum.py) sorts the formula's variable names by code point and sends answer_bitstring with
// character i for variable i; the solver reads that back as SAT's certificate (#832).
public class SATGroverSolver_Tests {

    [Theory]
    // b, then x10, then x2: code-point order, not the order they appear in, and not numeric order.
    [InlineData("(x2 | x10) & (!x2 | b)", "101", "(b:True,x10:False,x2:True)")]
    [InlineData("(x1 | !x2 | x3) & (!x1 | x2)", "110", "(x1:True,x2:True,x3:False)")]
    [InlineData("x1", "0", "(x1:False)")]
    public void Solve_ReadsTheBitString_InTheServicesVariableOrder(string formula, string bits, string expected) {
        var handler = new FakeSatService(_ => (HttpStatusCode.OK, Reply(bits)));
        using var fake = Install(handler);
        Assert.Equal(expected, new SATGroverSolver().solve(new SAT(formula)));
        Assert.Equal(formula, JsonDocument.Parse(handler.LastRequestBody!).RootElement.GetProperty("boolexpr").GetString());
    }

    [Theory]
    [InlineData("(x1 | !x2 | x3) & (!x1 | x2)")]
    [InlineData("(!x3 | x4 | !x2 | x1 | x2) & (!x4 | !x1) & (x4 | x3 | !x1)")]
    [InlineData("(x2 | x10) & (!x2 | b) & (!b | !x10)")]
    public void Solve_WithAServiceThatFindsAnAnswer_GivesOneTheVerifierAccepts(string formula) {
        // The fake answers as the service does: a satisfying assignment, its bits in sorted-name order.
        var handler = new FakeSatService(expr => {
            string known = new SATBruteForceSolver().solve(expr);
            var values = known.Trim('(', ')').Split(',').Select(p => p.Split(':')).ToDictionary(p => p[0], p => p[1] == "True");
            string bits = string.Concat(values.Keys.OrderBy(k => k, StringComparer.Ordinal).Select(k => values[k] ? '1' : '0'));
            return (HttpStatusCode.OK, Reply(bits));
        });
        using var fake = Install(handler);
        SAT problem = new SAT(formula);
        string answer = new SATGroverSolver().solve(problem);
        Assert.True(new SATVerifier().verify(problem, answer), answer);
        Assert.Equal("OPENQASM 2.0;", problem.circuit);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, "{}")]
    [InlineData(HttpStatusCode.OK, "{\"qasm\": \"OPENQASM 2.0;\"}")]
    [InlineData(HttpStatusCode.OK, "{\"answer_bitstring\": \"10\"}")]
    [InlineData(HttpStatusCode.OK, "{\"answer_bitstring\": \"1x0\"}")]
    [InlineData(HttpStatusCode.OK, "not json")]
    public void Solve_WithNoBitStringToRead_HasNoAnswer(HttpStatusCode status, string body) {
        // Unreachable, no bit string, one of the wrong length or not of bits, or no JSON at all: "{}", never the reply itself.
        using var fake = Install(new FakeSatService(_ => (status, body)));
        Assert.Equal("{}", new SATGroverSolver().solve(new SAT("(x1 | !x2 | x3) & (!x1 | x2)")));
    }

    [Fact]
    public void ServiceVariableOrder_SortsTheNamesByCodePoint_EachOnce() {
        Assert.Equal(["B", "_a", "a", "x1", "x10", "x2"], SATGroverSolver.ServiceVariableOrder("(x2 | !x10 | a) & (_a | B | !x2) & x1"));
    }

    private static string Reply(string bits) =>
        JsonSerializer.Serialize(new Dictionary<string, object> { ["answer_bitstring"] = bits, ["qasm"] = "OPENQASM 2.0;" });

    private static IDisposable Install(HttpMessageHandler handler) =>
        QuantumSolverSettingsGlobal.UseForThisFlow(new QuantumSolverSettings { BaseURL = "http://fake-quantum-server" }, new FakeHttpClientFactory(handler));

    // Answers /sat-quantum with whatever reply() gives for the posted expression.
    private sealed class FakeSatService(Func<string, (HttpStatusCode Status, string Body)> reply) : HttpMessageHandler {
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            LastRequestBody = request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            string expr = JsonDocument.Parse(LastRequestBody).RootElement.GetProperty("boolexpr").GetString()!;
            var (status, body) = reply(expr);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false) { BaseAddress = new Uri("http://fake-quantum-server") };
    }
}
