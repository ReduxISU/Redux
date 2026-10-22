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

    // #811: "no solution" is the problem's own answer when nothing is marked; an index outside the table is just wrong.
    [Theory]
    [InlineData("(0,0,0,0)", "no solution", true)]
    [InlineData("(0,1,0,0)", "no solution", false)]
    [InlineData("(0,1,0,0)", "7", false)]
    [InlineData("(0,1,0,0)", "4", false)]
    [InlineData("(0,1,0,0)", "-1", false)]
    public void UNSTRUCTUREDSEARCH_verifier_NoSolutionAndOutOfRange(string instance, string certificate, bool expected) {
        Assert.Equal(expected, new UnstructuredSearchVerifier().verify(new UNSTRUCTUREDSEARCH(instance), certificate));
    }

    [Theory]
    [InlineData("(0,0,0,0)")]
    [InlineData("(0,0,1,0)")]
    public void UNSTRUCTUREDSEARCH_solver_AnswerIsVerified(string instance) {
        var problem = new UNSTRUCTUREDSEARCH(instance);
        Assert.True(new UnstructuredSearchVerifier().verify(problem, new UnstructuredSearchSolver().solve(problem)));
    }

    [Fact]
    public void UNSTRUCTUREDSEARCH_verifier_NotAnIndex_IsACertificateError() {
        Assert.Throws<API.Interfaces.CertificateParseException>(() => ((API.Interfaces.IVerifier)new UnstructuredSearchVerifier()).verify("(0,1,0,0)", "one"));
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
        Assert.True(verifier.verify(problem, UnstructuredSearchVerifier.CertificateExample));
    }

    // -------------------------------------------------------------------------
    // UnstructuredGroverSolver
    //
    // solve() delegates to an external quantum-simulation HTTP service (QuantumServerAPI,
    // reading QuantumSolverSettingsGlobal). Those statics are populated by the real app's
    // Program.cs startup, which every endpoint test class runs in parallel with this one,
    // so tests install a fake IHttpClientFactory for their own async flow only
    // (QuantumSolverSettingsGlobal.UseForThisFlow is `internal`, exposed to this test
    // project via [assembly: InternalsVisibleTo("redux-tests")]) to exercise the solver's
    // SAT-expression building and response handling deterministically, without any real
    // network call and without leaking the fake into other tests (#809).
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
        // whose first character is x0 in the service's order -> item 1.
        var problem = new UNSTRUCTUREDSEARCH("(0, 1, 0, 0)");
        var handler = new FakeQuantumHandler("{\"qasm\":\"mock-circuit-xyz\",\"answer_bitstring\":\"10\"}");
        using var fake = InstallFakeQuantumServer(handler);
        var solver = new UnstructuredGroverSolver();

        string result = solver.solve(problem);

        Assert.Equal("1", result);
        Assert.Equal("mock-circuit-xyz", problem.circuit);
        // System.Text.Json's default encoder escapes '&' as &.
        Assert.Contains("\"boolexpr\":\"(x0 \\u0026 !x1)\"", handler.LastRequestBody);
    }

    // #883: the quantum service orders a formula's variables by sorting their names by code point, so from 11 variables
    // up x10 comes before x2, and the bits it measures pair with the names in that order, not x0, x1, ...
    [Fact]
    public void UnstructuredGroverSolver_ServiceVariableOrder_SortsNamesByCodePoint() {
        string formula = "(" + string.Join(" & ", Enumerable.Range(0, 12).Select(b => $"x{b}")) + ")";
        Assert.Equal(new[] { "x0", "x1", "x10", "x11", "x2", "x3", "x4", "x5", "x6", "x7", "x8", "x9" },
            UnstructuredGroverSolver.ServiceVariableOrder(formula));
    }

    [Theory]
    // 12 variables: character 2 is x10 and character 3 is x11, not x2 and x3.
    [InlineData(12, "001000000000", 1024)]
    [InlineData(12, "000100000000", 2048)]
    [InlineData(12, "000010000000", 4)]
    [InlineData(12, "111111111111", 4095)]
    // 11 variables: item 1027 is x0, x1 and x10, the service's first three characters.
    [InlineData(11, "11100000000", 1027)]
    // Below 11 the service's order is x0, x1, ...: character i is bit i, as before.
    [InlineData(4, "1010", 5)]
    [InlineData(2, "10", 1)]
    public void UnstructuredGroverSolver_Item_ReadsEachBitAsTheVariableTheServiceNamed(int variables, string bits, int item) {
        string formula = "(" + string.Join(" & ", Enumerable.Range(0, variables).Select(b => $"x{b}")) + ")";
        Assert.Equal(item, UnstructuredGroverSolver.Item(formula, bits));
    }

    [Fact]
    public void UnstructuredGroverSolver_Solve_ElevenVariables_AnswersTheMarkedItem() {
        // 2^11 items with only 1027 (x0, x1 and x10) marked. The fake answers in the service's order, x10 third, so
        // reading the bits as x0, x1, x2, ... gave item 7.
        var values = new int[2048];
        values[1027] = 1;
        var problem = new UNSTRUCTUREDSEARCH("(" + string.Join(",", values) + ")");
        using var fake = FakeQuantumServer.Install(out var server);

        Assert.Equal("1027", new UnstructuredGroverSolver().solve(problem));
        Assert.True(problem.defaultVerifier.verify(problem, "1027"));
    }

    [Fact]
    public void UnstructuredGroverSolver_Solve_Missing_Qasm_Field_Leaves_Circuit_Empty() {
        // When the response has no "qasm" field, problem.circuit is never assigned and
        // keeps its default value ("").
        var problem = new UNSTRUCTUREDSEARCH("(0, 0, 1, 0)");
        var handler = new FakeQuantumHandler("{\"answer_bitstring\":\"01\"}");
        using var fake = InstallFakeQuantumServer(handler);
        var solver = new UnstructuredGroverSolver();

        string result = solver.solve(problem);

        // "01": x1 is set -> 2.
        Assert.Equal("2", result);
        Assert.Equal("", problem.circuit);
    }

    [Fact]
    public void UnstructuredGroverSolver_Solve_AllZero_FuncValues_Sends_Empty_BoolExpr() {
        // No index has a non-zero funcValue, so the exprs list stays empty and
        // String.Join(" | ", exprs) produces an empty boolexpr string.
        var problem = new UNSTRUCTUREDSEARCH("(0, 0, 0, 0)");
        var handler = new FakeQuantumHandler("{\"qasm\":\"c\",\"answer_bitstring\":\"00\"}");
        using var fake = InstallFakeQuantumServer(handler);
        var solver = new UnstructuredGroverSolver();

        solver.solve(problem);

        Assert.Contains("\"boolexpr\":\"\"", handler.LastRequestBody);
    }

    [Fact]
    public void UnstructuredGroverSolver_Solve_Missing_AnswerBitstring_ThrowsSolverUnavailable() {
        // Regression test for GitHub issue #538. When the quantum server's response has no
        // "answer_bitstring" field, the raw JSON response is not misread as bitstring data
        // (which used to throw a confusing FormatException). #838 made the answer an error naming
        // the missing field; since #625 no error is an answer: SolveAsSat throws
        // SolverUnavailableException, which the endpoints answer with 503 solver_unavailable.
        var problem = new UNSTRUCTUREDSEARCH("(0, 1, 0, 0)");
        var handler = new FakeQuantumHandler("{\"qasm\":\"c\"}");
        using var fake = InstallFakeQuantumServer(handler);
        var solver = new UnstructuredGroverSolver();

        var ex = Assert.Throws<API.Interfaces.SolverUnavailableException>(() => solver.solve(problem));
        Assert.Contains("answer_bitstring", ex.Message);
    }

    [Fact]
    public void UnstructuredGroverSolver_FakeServer_IsThisTestsOnly() {
        // Regression test for #809. Test classes run in parallel, and the quantum settings were one process-wide pair that
        // both this class's fake and every app an endpoint test starts (Program.cs) wrote: an app starting between installing
        // the fake and solving took the fake away, and an endpoint test running meanwhile was answered by the fake. Code
        // running outside this test (here, a thread that does not inherit its context) must not see the fake.
        var handler = new FakeQuantumHandler("{\"answer_bitstring\":\"11\"}");
        using var fake = InstallFakeQuantumServer(handler);
        var mine = (QuantumSolverSettingsGlobal.QuantumSolver, QuantumSolverSettingsGlobal.HttpClientFactory);

        (QuantumSolverSettings, IHttpClientFactory) elsewhere = default;
        using (ExecutionContext.SuppressFlow()) {
            var other = new Thread(() => elsewhere = (QuantumSolverSettingsGlobal.QuantumSolver, QuantumSolverSettingsGlobal.HttpClientFactory));
            other.Start();
            other.Join();
        }

        Assert.NotSame(mine.Item1, elsewhere.Item1);
        Assert.NotSame(mine.Item2, elsewhere.Item2);
        Assert.Equal("3", new UnstructuredGroverSolver().solve(new UNSTRUCTUREDSEARCH("(0, 0, 0, 1)")));
        Assert.NotNull(handler.LastRequestBody);
    }

    // -------------------------------------------------------------------------
    // Fakes for the quantum HTTP dependency
    // -------------------------------------------------------------------------

    // The fake serves this test only, until disposed (see #809).
    private static IDisposable InstallFakeQuantumServer(HttpMessageHandler handler) =>
        QuantumSolverSettingsGlobal.UseForThisFlow(new QuantumSolverSettings { BaseURL = "http://fake-quantum-server" }, new FakeHttpClientFactory(handler));

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