using System.Net;
using System.Text;
using System.Text.Json;

namespace redux_tests;
#pragma warning disable CS1591

// A fake quantum server for tests: it answers each endpoint the quantum solvers call the way a noise-free one would (with
// counts, which the real server may or may not send), or with a canned response. Install it for the current test only
// (QuantumSolverSettingsGlobal.UseForThisFlow, #809); an endpoint test also needs its TestServer to keep the test's
// execution context (PreserveExecutionContext) for the fake to reach the request.
public sealed class FakeQuantumServer : HttpMessageHandler {
    public const int Shots = 1024;

    /// <summary>Every request, in order: its path and body.</summary>
    public List<(string Path, string Body)> Requests { get; } = new();

    /// <summary>How long the server takes to answer.</summary>
    public TimeSpan Delay { get; set; } = TimeSpan.Zero;

    /// <summary>A canned reply (status, body) instead of the ideal one, when set.</summary>
    public (HttpStatusCode Status, string Body)? Canned { get; set; }

    public static IDisposable Install(out FakeQuantumServer server) {
        server = new FakeQuantumServer();
        return QuantumSolverSettingsGlobal.UseForThisFlow(new QuantumSolverSettings { BaseURL = "http://fake-quantum-server" }, new Factory(server));
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        string path = request.RequestUri?.AbsolutePath ?? "";
        string body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        lock (Requests) Requests.Add((path, body));
        if (Delay > TimeSpan.Zero) await Task.Delay(Delay, cancellationToken);
        var (status, reply) = Canned ?? (HttpStatusCode.OK, Ideal(path, body));
        return new HttpResponseMessage(status) { Content = new StringContent(reply, Encoding.UTF8, "application/json") };
    }

    private static string Reply(object answer, string? measured) =>
        JsonSerializer.Serialize(new Dictionary<string, object?> {
            ["answer"] = answer,
            ["qasm"] = "OPENQASM 2.0;",
            ["counts"] = measured == null ? null : new Dictionary<string, int> { [measured] = Shots },
        });

    private static int Dot(int a, int b) => System.Numerics.BitOperations.PopCount((uint)(a & b)) % 2;

    // What a noise-free server answers, worked out classically.
    private static string Ideal(string path, string body) {
        switch (path) {
            case "/deutsch-quantum": {
                    var f = JsonSerializer.Deserialize<bool[]>(body)!;
                    return Reply(f[0] == f[1] ? "constant" : "balanced", f[0] == f[1] ? "0" : "1");
                }
            case "/deutsch-jozsa-quantum": {
                    var f = JsonSerializer.Deserialize<bool[]>(body)!;
                    int n = System.Numerics.BitOperations.Log2((uint)f.Length);
                    bool constant = f.All(v => v == f[0]);
                    // The likeliest outcome: the y with the largest |sum over x of (-1)^(f(x) + x.y)|.
                    int y = Enumerable.Range(0, f.Length).OrderByDescending(c => Math.Abs(Enumerable.Range(0, f.Length).Sum(x => (f[x] ? 1 : 0) + Dot(x, c) == 1 ? -1 : 1))).ThenBy(c => c).First();
                    return Reply(constant ? "constant" : "balanced", n == 0 ? null : Convert.ToString(y, 2).PadLeft(n, '0'));
                }
            case "/bernstein-vazirani-quantum": {
                    var f = JsonSerializer.Deserialize<bool[]>(body)!;
                    int n = System.Numerics.BitOperations.Log2((uint)f.Length);
                    string s = string.Concat(Enumerable.Range(0, n).Select(k => f[1 << (n - 1 - k)] ? '1' : '0'));
                    return Reply(s, s.Length == 0 ? null : s);
                }
            case "/sat-quantum": {
                    // The server's order (sat_quantum.py): the variable names sorted by code point, character i for variable i.
                    // A noise-free Grover search lands on a satisfying assignment: the first one counting up, variable 0 the
                    // low bit (for Unstructured Search's "(x0 & !x1) | (...)", the smallest marked item; #832).
                    string expr = JsonDocument.Parse(body).RootElement.GetProperty("boolexpr").GetString()!;
                    var names = System.Text.RegularExpressions.Regex.Matches(expr, "[A-Za-z0-9_]+").Select(m => m.Value).Distinct().OrderBy(v => v, StringComparer.Ordinal).ToList();
                    long found = 0;
                    for (long k = 0; names.Count > 0 && k < 1L << names.Count; k++)
                        if (Evaluate(expr, names.Select((v, i) => (v, ((k >> i) & 1) == 1)).ToDictionary(p => p.v, p => p.Item2))) {
                            found = k;
                            break;
                        }
                    string bits = names.Count == 0 ? "0" : string.Concat(Enumerable.Range(0, names.Count).Select(i => ((found >> i) & 1) == 1 ? '1' : '0'));
                    return JsonSerializer.Serialize(new Dictionary<string, object> {
                        ["qasm"] = "OPENQASM 2.0;",
                        ["answer_bitstring"] = bits,
                        ["counts"] = new Dictionary<string, int> { [bits] = Shots },
                    });
                }
            case "/prime-factorization-quantum": {
                    long n = long.Parse(body);
                    var factors = new List<long>();
                    for (long d = 2; d * d <= n; d++)
                        while (n % d == 0) {
                            factors.Add(d);
                            n /= d;
                        }
                    if (n > 1) factors.Add(n);
                    return JsonSerializer.Serialize(new { answer = factors });
                }
            default:
                return "{}";
        }
    }

    // A boolean expression of names, '!', '&', '|' and parentheses ('&' binds tighter than '|'), as the server reads it.
    private static bool Evaluate(string expr, IReadOnlyDictionary<string, bool> values) {
        int at = 0;
        void Skip() { while (at < expr.Length && char.IsWhiteSpace(expr[at])) at++; }
        bool Or() {
            bool v = And();
            for (Skip(); at < expr.Length && expr[at] == '|'; Skip()) { at++; v |= And(); }
            return v;
        }
        bool And() {
            bool v = Not();
            for (Skip(); at < expr.Length && expr[at] == '&'; Skip()) { at++; v &= Not(); }
            return v;
        }
        bool Not() {
            Skip();
            if (expr[at] == '!') { at++; return !Not(); }
            if (expr[at] == '(') {
                at++;
                bool v = Or();
                Skip();
                at++;
                return v;
            }
            int start = at;
            while (at < expr.Length && (char.IsLetterOrDigit(expr[at]) || expr[at] == '_')) at++;
            return values[expr[start..at]];
        }
        return Or();
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false) { BaseAddress = new Uri("http://fake-quantum-server") };
    }
}
