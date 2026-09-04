using System.Net;
using System.Text;
using Xunit;
using Microsoft.Extensions.Http;
using API.Problems.NPComplete.NPC_UNSTRUCTUREDSEARCH;
using API.Problems.NPComplete.NPC_UNSTRUCTUREDSEARCH.Solvers;
using API.Problems.NPComplete.NPC_UNSTRUCTUREDSEARCH.Verifiers;

namespace redux_tests;
#pragma warning disable CS1591

public class UNSTRUCTUREDSEARCH_tests {
    [Fact]
    public void DEUTSCH_Default_Instantiation() {
        var problem = new UNSTRUCTUREDSEARCH();
        Assert.Equal("(0, 1, 0, 0)", problem.instance);
        Assert.Equal("(0, 1, 0, 0)", problem.defaultInstance);
    }

    [Fact]
    public void DEUTSCH_Custom_Instantiation() {
        string instance = "(0, 0, 0, 1)";
        var problem = new UNSTRUCTUREDSEARCH(instance);
        Assert.Equal(instance, problem.instance);
    }

    [Theory] //tests verifier
    [InlineData("(1,0,0,0)", 0, true)]
    [InlineData("(1,0,0,0)", 1, false)]
    [InlineData("(1,0,0,0)", 2, false)]
    [InlineData("(1,0,0,0)", 3, false)]
    [InlineData("(0,1,0,0)", 1, true)]
    [InlineData("(0,0,1,0)", 2, true)]
    [InlineData("(0,0,0,1)", 3, true)]
    public void DEUTSCH_verifier(string instance, int certificate, bool expected) {
        var problem = new UNSTRUCTUREDSEARCH(instance);
        var verifier = problem.defaultVerifier;
        bool result = verifier.verify(problem, certificate.ToString());
        Assert.Equal(expected, result);

    }

    [Theory] //tests solver
    [InlineData("(1,0,0,0)", 0)]
    [InlineData("(0,1,0,0)", 1)]
    [InlineData("(0,0,1,0)", 2)]
    [InlineData("(0,0,0,1)", 3)]
    public void UNSTRUCTUREDSEARCH_solver(string instance, int certificate) {
        var problem = new UNSTRUCTUREDSEARCH(instance);
        var solver = new UnstructuredSearchSolver();
        string solvedString = solver.solve(problem);
        Assert.Equal($"{certificate}", solvedString);
    }

    // -------------------------------------------------------------------------
    // Format declarations
    // -------------------------------------------------------------------------

    [Fact]
    public void UNSTRUCTUREDSEARCH_Instance_Format_Described() {
        var problem = new UNSTRUCTUREDSEARCH();
        Assert.NotNull(problem.instanceFormat);
        Assert.NotEmpty(problem.instanceFormat);
        Assert.Contains("f(0)", problem.instanceFormat);
    }

    [Fact]
    public void UNSTRUCTUREDSEARCH_Certificate_Format_Described() {
        var problem = new UNSTRUCTUREDSEARCH();
        Assert.NotNull(problem.certificateFormat);
        Assert.NotEmpty(problem.certificateFormat);
        Assert.Contains("index", problem.certificateFormat);
    }

    [Fact]
    public void UNSTRUCTUREDSEARCH_Certificate_Format_Example_Is_Actually_Valid() {
        // The "Example: 1" quoted in certificateFormat must be a real, verifiable
        // certificate for defaultInstance — not just descriptive prose.
        var problem = new UNSTRUCTUREDSEARCH();
        var verifier = new UnstructuredSearchVerifier();
        Assert.True(verifier.verify(problem, "1"));
    }

    // -------------------------------------------------------------------------
    // UnstructuredGroverSolver
    //
    // solve() delegates to an external quantum-simulation HTTP service (QuantumServerAPI,
    // reading QuantumSolverSettingsGlobal). These statics are only populated by the real
    // app's Program.cs startup, so tests install a fake IHttpClientFactory directly
    // (QuantumSolverSettingsGlobal's setters are `internal`, exposed to this test project
    // via [assembly: InternalsVisibleTo("redux-tests")]) to exercise the solver's SAT-
    // expression building and response handling deterministically, without any real
    // network call.
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(4, 2)]
    [InlineData(8, 3)]
    [InlineData(16, 4)]
    public void UnstructuredGroverSolver_PowerOfTwo_Returns_Log2(int n, int expected) {
        Assert.Equal(expected, UnstructuredGroverSolver.PowerOfTwo(n));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void UnstructuredGroverSolver_PowerOfTwo_Throws_For_NonPowerOfTwo(int n) {
        Assert.Throws<ArithmeticException>(() => UnstructuredGroverSolver.PowerOfTwo(n));
    }

    [Fact]
    public void UnstructuredGroverSolver_Solve_Returns_Decoded_Index_From_Mocked_Server() {
        // funcValues = (0,1,0,0): only index 1 is non-zero, encoding to the 2-bit SAT
        // expression "(x0 & !x1)". The mocked quantum server answers with bitstring "10",
        // which ReverseString flips to "01" -> Convert.ToInt32(_, 2) = 1.
        var problem = new UNSTRUCTUREDSEARCH("(0, 1, 0, 0)");
        var handler = new FakeQuantumHandler("{\"qasm\":\"mock-circuit-xyz\",\"answer_bitstring\":\"10\"}");
        InstallFakeQuantumServer(handler);
        var solver = new UnstructuredGroverSolver();

        string result = solver.solve(problem);

        Assert.Equal("1", result);
        Assert.Equal("mock-circuit-xyz", problem.circuit);
        // System.Text.Json's default encoder escapes '&' as &.
        Assert.Contains("\"boolexpr\":\"(x0 \\u0026 !x1)\"", handler.LastRequestBody);
    }

    [Fact]
    public void UnstructuredGroverSolver_Solve_Missing_Qasm_Field_Leaves_Circuit_Empty() {
        // When the response has no "qasm" field, problem.circuit is never assigned and
        // keeps its default value ("").
        var problem = new UNSTRUCTUREDSEARCH("(0, 0, 1, 0)");
        var handler = new FakeQuantumHandler("{\"answer_bitstring\":\"01\"}");
        InstallFakeQuantumServer(handler);
        var solver = new UnstructuredGroverSolver();

        string result = solver.solve(problem);

        // "01" reversed is "10" -> 2.
        Assert.Equal("2", result);
        Assert.Equal("", problem.circuit);
    }

    [Fact]
    public void UnstructuredGroverSolver_Solve_AllZero_FuncValues_Sends_Empty_BoolExpr() {
        // No index has a non-zero funcValue, so the exprs list stays empty and
        // String.Join(" | ", exprs) produces an empty boolexpr string.
        var problem = new UNSTRUCTUREDSEARCH("(0, 0, 0, 0)");
        var handler = new FakeQuantumHandler("{\"qasm\":\"c\",\"answer_bitstring\":\"00\"}");
        InstallFakeQuantumServer(handler);
        var solver = new UnstructuredGroverSolver();

        solver.solve(problem);

        Assert.Contains("\"boolexpr\":\"\"", handler.LastRequestBody);
    }

    [Fact(Skip = "BUG: when the quantum server's response has no \"answer_bitstring\" field, " +
        "SolveAsSat's fallback `return response;` hands the *entire raw JSON response* back " +
        "as if it were the answer bitstring, instead of signalling a clear error. solve() " +
        "then reverses that JSON text and feeds it to Convert.ToInt32(_, 2), which throws an " +
        "unhandled FormatException -- a confusing crash instead of a meaningful \"no answer\" " +
        "result. (The same outcome occurs, via the SolveAsSat try/catch swallowing a real " +
        "network failure into an error-JSON string, whenever the quantum service is simply " +
        "unreachable -- e.g. when QuantumSolverSettingsGlobal is never populated, as happens " +
        "for solve() called outside the full app startup.)")]
    public void UnstructuredGroverSolver_Solve_Missing_AnswerBitstring_Throws_Confusing_Exception() {
        var problem = new UNSTRUCTUREDSEARCH("(0, 1, 0, 0)");
        var handler = new FakeQuantumHandler("{\"qasm\":\"c\"}");
        InstallFakeQuantumServer(handler);
        var solver = new UnstructuredGroverSolver();

        Assert.Throws<FormatException>(() => solver.solve(problem));
    }

    // -------------------------------------------------------------------------
    // Fakes for the quantum HTTP dependency
    // -------------------------------------------------------------------------

    private static void InstallFakeQuantumServer(HttpMessageHandler handler) {
        QuantumSolverSettingsGlobal.QuantumSolver = new QuantumSolverSettings { BaseURL = "http://fake-quantum-server" };
        QuantumSolverSettingsGlobal.HttpClientFactory = new FakeHttpClientFactory(handler);
    }

    private sealed class FakeQuantumHandler : HttpMessageHandler {
        private readonly string _responseJson;
        private readonly HttpStatusCode _statusCode;
        public string? LastRequestBody { get; private set; }

        public FakeQuantumHandler(string responseJson, HttpStatusCode statusCode = HttpStatusCode.OK) {
            _responseJson = responseJson;
            _statusCode = statusCode;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            if (request.Content != null) {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }
            return new HttpResponseMessage(_statusCode) {
                Content = new StringContent(_responseJson, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory {
        private readonly HttpMessageHandler _handler;
        public FakeHttpClientFactory(HttpMessageHandler handler) {
            _handler = handler;
        }
        public HttpClient CreateClient(string? name) {
            return new HttpClient(_handler) { BaseAddress = new Uri("http://fake-quantum-server") };
        }
    }
}