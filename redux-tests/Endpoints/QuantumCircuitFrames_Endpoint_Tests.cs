using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Quantum Circuit picture on the actual /ProblemProvider/visualize?format=frames output, for the six quantum-algorithm
// problems and all eleven of their solvers, plus the guards that keep everything old frontends read exactly as it was:
//   - the list format of the eight visualizations that existed (Snapshots/quantum_list_snapshots.tsv, captured before the
//     picture existed, with no quantum server reachable),
//   - /info for the six problems, their solvers, verifiers and visualizations (info.<name>.json, captured the same way).
// A test that needs a quantum server installs a fake one for its own flow (#809); the TestServer keeps the test's execution
// context so the fake reaches the request.
public class QuantumCircuitFrames_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public QuantumCircuitFrames_Endpoint_Tests(AppFactory factory) {
        // Before the client is made: its handler copies the setting.
        factory.Server.PreserveExecutionContext = true;
        _client = factory.CreateClient();
    }

    private static string ReadSnapshot(string file) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Endpoints", "Snapshots", file)).Replace("\r\n", "\n");

    private async Task<(HttpStatusCode Status, string Body)> Visualize(string query, string instance) {
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/visualize?{query}", content, TestContext.Current.CancellationToken);
        return (response.StatusCode, (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Replace("\r\n", "\n"));
    }

    private static string Compact(string json) => JsonSerializer.Serialize(JsonDocument.Parse(json).RootElement);

    private static string Readable(JsonElement element) =>
        JsonSerializer.Serialize(element, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    private static Dictionary<string, string> StatesOf(JsonElement map) =>
        map.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetProperty("state").GetString()!);

    private static readonly HashSet<string> Vocabulary = new() {
        "Background", "ElementHighlight", "Solution", "Rejected", "Untraveled", "Covered", "Blocked",
    };

    // The five solvers that call the quantum server.
    private static readonly HashSet<string> ServerSolvers = new() {
        "deutschquantumsolver", "deutschjozsaquantumsolver", "bernsteinvaziraniquantumsolver", "unstructuredgroversolver", "shorsquantumsolver",
    };

    // Every solver on each of its snapshot instances, plus the server solvers on a few (against an ideal fake server).
    public static TheoryData<string, string> FramesCases {
        get {
            var data = new TheoryData<string, string>();
            foreach (var (solver, instance, _) in QuantumSolvers_Tests.Rows()) data.Add(solver, instance);
            foreach (var (solver, instance) in new[] {
                ("deutschquantumsolver", "(0,1)"), ("deutschquantumsolver", "(1,1)"),
                ("deutschjozsaquantumsolver", "(1, 1, 1, 1)"), ("deutschjozsaquantumsolver", "(0,1,1,0,1,0,0,1)"),
                ("bernsteinvaziraniquantumsolver", "(0,1,0,1,1,0,1,0)"), ("bernsteinvaziraniquantumsolver", "(0,1,1,1,0,0,0,0)"),
                ("unstructuredgroversolver", "(0, 1, 0, 0)"), ("unstructuredgroversolver", "(0,0,0,0,0,0,0,0,0,0,0,1,0,0,0,0)"),
                ("shorsquantumsolver", "12"), ("shorsquantumsolver", "15"), ("shorsquantumsolver", "21"),
            }) data.Add(solver, instance);
            return data;
        }
    }

    public static TheoryData<string, string, string> ListCases {
        get {
            var data = new TheoryData<string, string, string>();
            foreach (var line in ReadSnapshot("quantum_list_snapshots.tsv").Split('\n').Where(l => l.Length > 0)) {
                var parts = line.Split('\t');
                data.Add(parts[0], JsonSerializer.Deserialize<string>(parts[1])!, parts[2]);
            }
            return data;
        }
    }

    public static TheoryData<string> TouchedInterfaces => new(
        Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Endpoints", "Snapshots"), "info.*.json")
            .Select(f => Path.GetFileName(f)["info.".Length..^".json".Length])
            .Where(n => new[] { "deutsch", "bernsteinvazirani", "simon", "unstructured", "primefactor", "shors" }.Any(n.StartsWith))
            .Order());

    // ---- What old frontends read must not change. ----

    [Fact]
    public void TouchedInterfaces_AreTheThirtyOneSnapshotted() => Assert.Equal(31, TouchedInterfaces.Count());

    [Theory]
    [MemberData(nameof(TouchedInterfaces))]
    public async Task Info_IsUnchangedByTheQuantumCircuitPicture(string name) {
        var response = await _client.GetAsync($"/ProblemProvider/info?interface={name}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ReadSnapshot($"info.{name}.json"), body.Replace("\r\n", "\n"));
        Assert.DoesNotContain("\"QuantumCircuit\"", InfoJson.WithoutPictureType(body));
        Assert.DoesNotContain("StepShape", body);
        Assert.DoesNotContain("Describe", body);
    }

    [Fact]
    public async Task Info_SimonVisualization_IsALegacyQuantumCircuit() {
        var response = await _client.GetAsync("/ProblemProvider/info?interface=simonvisualization", TestContext.Current.CancellationToken);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement;
        Assert.Equal("QuantumCircuitQjs", body.GetProperty("visualizationType").GetString());
        Assert.Equal("Simon Classical", body.GetProperty("solver").GetProperty("solverName").GetString());
        Assert.False(body.TryGetProperty("StepShape", out _));
        // Simon's Problem keeps its default visualization.
        var problem = await _client.GetAsync("/ProblemProvider/info?interface=simon", TestContext.Current.CancellationToken);
        var defaultVisualization = JsonDocument.Parse(await problem.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement.GetProperty("defaultVisualization");
        Assert.Equal("Unimplemented", defaultVisualization.GetProperty("visualizationType").GetString());
    }

    [Theory]
    [MemberData(nameof(ListCases))]
    public async Task List_MatchesPreChangeSnapshot(string vis, string instance, string expected) {
        // The snapshots were taken with no quantum server reachable. A fake one that refuses every call (503) gives the
        // list format the same failure, quickly, whether or not something listens on this machine's quantum port.
        using var fake = FakeQuantumServer.Install(out var server);
        server.Canned = (HttpStatusCode.ServiceUnavailable, "{}");
        var (status, body) = await Visualize($"visualization={vis}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(Compact(expected), Compact(body));
    }

    [Fact]
    public async Task List_SimonVisualization_IsTheEmptyQasmPayload() {
        var (status, body) = await Visualize("visualization=simonvisualization", "(5, 6, 5, 6, 3, 2, 3, 2)");
        Assert.Equal(HttpStatusCode.OK, status);
        // The unsolved circuit carries nothing, so the list drops it and keeps the solved one.
        Assert.Equal("[{\"kind\":\"quantumCircuit\",\"solution\":\"010\",\"format\":0,\"qasm\":\"\",\"d3\":null,\"metadata\":null}]", Compact(body));
    }

    // ---- The picture ----

    [Theory]
    [MemberData(nameof(FramesCases))]
    public async Task Frames_EverySolver_HasTheQuantumCircuitPayloadAndFramesThatAgreeWithTheAnswer(string solver, string instance) {
        using var fake = FakeQuantumServer.Install(out var server);
        string vis = QuantumSolvers_Tests.VisualizationOf[solver];
        var (status, body) = await Visualize($"visualization={vis}&format=frames&solver={solver}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(ServerSolvers.Contains(solver) ? 1 : 0, server.Requests.Count);
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal(["type", "payload", "frames"], root.EnumerateObject().Select(p => p.Name));
        Assert.Equal("QuantumCircuit", root.GetProperty("type").GetString());

        // The payload: the circuit, with ids by position.
        var payload = root.GetProperty("payload");
        bool hasNote = payload.TryGetProperty("note", out _), hasNumber = payload.TryGetProperty("number", out _);
        Assert.Equal(["kind", "problem", "qubits", "columns", "stages", "measure", "readout", "inputBits", "simulated", .. hasNote ? new[] { "note" } : [], .. hasNumber ? new[] { "number" } : []],
            payload.EnumerateObject().Select(p => p.Name));
        Assert.Equal("quantumCircuit", payload.GetProperty("kind").GetString());
        var qubits = payload.GetProperty("qubits").EnumerateArray().ToList();
        Assert.All(qubits, (q, k) => {
            Assert.Equal(["id", "name", "role"], q.EnumerateObject().Select(p => p.Name));
            Assert.Equal($"q{k}", q.GetProperty("id").GetString());
        });
        var columns = payload.GetProperty("columns").EnumerateArray().ToList();
        Assert.Equal(qubits.Count == 0, columns.Count == 0);
        Assert.Equal(columns.Count == 0, hasNote && payload.GetProperty("problem").GetString() != "primeFactorization" || columns.Count == 0);
        var gateIds = new HashSet<string>();
        Assert.All(columns, (c, i) => {
            Assert.Equal($"t{i}", c.GetProperty("id").GetString());
            var used = new HashSet<int>();
            foreach (var g in c.GetProperty("gates").EnumerateArray()) {
                var on = g.GetProperty("qubits").EnumerateArray().Select(x => x.GetInt32()).ToList();
                Assert.Equal($"g{i}_{on.Min()}", g.GetProperty("id").GetString());
                Assert.True(gateIds.Add(g.GetProperty("id").GetString()!));
                Assert.All(on, q => { Assert.InRange(q, 0, qubits.Count - 1); Assert.True(used.Add(q)); });
                Assert.Contains(g.GetProperty("type").GetString(), new[] { "h", "x", "z", "cx", "measure", "box" });
            }
        });
        Assert.All(payload.GetProperty("measure").EnumerateArray(), q => Assert.InRange(q.GetInt32(), 0, qubits.Count - 1));
        bool simulated = payload.GetProperty("simulated").GetBoolean();
        int measured = payload.GetProperty("measure").GetArrayLength();

        // The frames.
        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        var done = Assert.Single(frames, f => f.GetProperty("event").GetString() == "Done");
        Assert.Equal(done, frames[^1]);
        Assert.All(frames.SkipLast(1), f => Assert.False(f.TryGetProperty("ok", out _)));
        var columnIds = Enumerable.Range(0, columns.Count).Select(i => $"t{i}").ToHashSet();
        var qubitIds = qubits.Select(q => q.GetProperty("id").GetString()!).ToHashSet();
        foreach (var f in frames) {
            var optional = new[] { "bars", "outcome", "counts", "result", "ok" }.Where(n => f.TryGetProperty(n, out _));
            Assert.Equal(["event", "caption", "focus", "applied", "queries", .. new[] { "columns", "gates", "qubits" }, .. optional], f.EnumerateObject().Select(p => p.Name));
            Assert.False(string.IsNullOrWhiteSpace(f.GetProperty("caption").GetString()));
            int applied = f.GetProperty("applied").GetInt32();
            Assert.InRange(applied, 0, columns.Count);
            Assert.All(StatesOf(f.GetProperty("columns")), s => { Assert.Contains(s.Key, columnIds); Assert.Contains(s.Value, Vocabulary); });
            Assert.All(StatesOf(f.GetProperty("gates")), s => { Assert.Contains(s.Key, gateIds); Assert.Contains(s.Value, Vocabulary); });
            Assert.All(StatesOf(f.GetProperty("qubits")), s => { Assert.Contains(s.Key, qubitIds); Assert.Contains(s.Value, Vocabulary); });
            Assert.All(f.GetProperty("queries").EnumerateArray(), q => Assert.Equal(["input", "output"], q.EnumerateObject().Select(p => p.Name)));
            // Bars only once a simulated circuit has run; with few measured qubits they hold every outcome, summing to 1.
            Assert.Equal(simulated && applied > 0, f.TryGetProperty("bars", out var bars));
            if (simulated && applied > 0 && measured <= 5)
                Assert.Equal(1, bars.EnumerateArray().Sum(b => b.GetProperty("probability").GetDouble()), 4);
        }

        // The Done frame agrees with the answer and the verifier.
        var problem = (API.Interfaces.IProblem)Activator.CreateInstance(ProblemProvider.Problems.Values.First(t => t.Name == ProblemProvider.Solvers[solver].GetInterfaces()
            .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(API.Interfaces.ISolver<>)).GetGenericArguments()[0].Name))!;
        string answer = done.GetProperty("result").GetString()!;
        bool verified;
        try { verified = problem.defaultVerifier.verify(instance, answer); } catch { verified = false; }
        Assert.Equal(verified, done.GetProperty("ok").GetBoolean());
        var row = QuantumSolvers_Tests.Rows().Where(r => r.Solver == solver && r.Instance == instance).ToList();
        if (row.Count > 0) Assert.Equal(row[0].Answer, answer);
        // A classical run never ran the circuit: its columns are Untraveled at the end. A server run applied them all.
        bool classical = !ServerSolvers.Contains(solver) || solver == "shorsquantumsolver";
        if (classical) Assert.All(columnIds, id => Assert.Equal("Untraveled", StatesOf(done.GetProperty("columns"))[id]));
        else Assert.Equal(columns.Count, done.GetProperty("applied").GetInt32());
        // The measured answer is Solution when it is right.
        if (done.TryGetProperty("outcome", out _) && verified)
            Assert.All(payload.GetProperty("measure").EnumerateArray(), q => Assert.Equal("Solution", StatesOf(done.GetProperty("qubits"))[$"q{q.GetInt32()}"]));
    }

    [Fact]
    public async Task Frames_BernsteinVazirani_MatchesTheMockup() {
        using var fake = FakeQuantumServer.Install(out var server);
        var (_, body) = await Visualize("visualization=bernsteinvaziranidefaultvisualization&format=frames&solver=bernsteinvaziraniquantumsolver", "(0,1,0,1,1,0,1,0)");
        var frames = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().ToList();
        Assert.Equal(7, frames.Count);
        // After the oracle: every input equally likely, the sign of x flipped where s · x = 1 (the mockup's blue and orange).
        Assert.Equal(
            "{\"event\":\"Try\",\"caption\":\"One call to the oracle U_f. Because y is |−⟩, every input x with f(x) = 1 (4 of 8) gets its sign flipped: phase kickback.\","
            + "\"focus\":[\"t2\",\"g2_0\"],\"applied\":3,\"queries\":[],\"columns\":{\"t2\":{\"state\":\"ElementHighlight\"}},\"gates\":{\"g2_0\":{\"state\":\"ElementHighlight\"}},\"qubits\":{},"
            + "\"bars\":[{\"outcome\":0,\"probability\":0.125,\"sign\":1},{\"outcome\":1,\"probability\":0.125,\"sign\":-1},{\"outcome\":2,\"probability\":0.125,\"sign\":1},"
            + "{\"outcome\":3,\"probability\":0.125,\"sign\":-1},{\"outcome\":4,\"probability\":0.125,\"sign\":-1},{\"outcome\":5,\"probability\":0.125,\"sign\":1},"
            + "{\"outcome\":6,\"probability\":0.125,\"sign\":-1},{\"outcome\":7,\"probability\":0.125,\"sign\":1}]}",
            Readable(frames[2]));
        // The mockup's solved view: 101 measured with probability 1, the measured qubits and meters green.
        Assert.Equal(
            "{\"event\":\"Done\",\"caption\":\"s = 101, found with one call to the oracle.\",\"focus\":[],\"applied\":5,\"queries\":[],\"columns\":{},"
            + "\"gates\":{\"g4_0\":{\"state\":\"Solution\"},\"g4_1\":{\"state\":\"Solution\"},\"g4_2\":{\"state\":\"Solution\"}},"
            + "\"qubits\":{\"q0\":{\"state\":\"Solution\"},\"q1\":{\"state\":\"Solution\"},\"q2\":{\"state\":\"Solution\"}},"
            + "\"bars\":[{\"outcome\":5,\"probability\":1,\"sign\":1}],\"outcome\":5,\"counts\":{\"101\":1024},\"result\":\"(1,0,1)\",\"ok\":true}",
            Readable(frames[^1]));
    }

    [Fact]
    public async Task Frames_DefaultSolver_IsTheClassicalStory() {
        // No solver given: each visualization's own solver, which never needs the quantum server.
        using var fake = FakeQuantumServer.Install(out var server);
        var (_, body) = await Visualize("visualization=deutschd3visualization&format=frames", "(0,1)");
        var frames = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().ToList();
        Assert.Empty(server.Requests);
        Assert.Equal(["Try", "Try", "Accept", "Done"], frames.Select(f => f.GetProperty("event").GetString()));
        Assert.Equal("[{\"input\":0,\"output\":0},{\"input\":1,\"output\":1}]", Readable(frames[^1].GetProperty("queries")));
        Assert.Equal("balanced", frames[^1].GetProperty("result").GetString());
    }

    [Fact]
    public async Task Frames_ServerDown_StillDrawTheCircuit_AndEndNotOk() {
        using var fake = FakeQuantumServer.Install(out var server);
        server.Canned = (HttpStatusCode.ServiceUnavailable, "{}");
        var (status, body) = await Visualize("visualization=deutschjozsad3visualization&format=frames&solver=deutschjozsaquantumsolver", "(0,1,1,0)");
        Assert.Equal(HttpStatusCode.OK, status);
        var frames = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().ToList();
        Assert.Equal(["Try", "Try", "Try", "Try", "Try", "Reject", "Done"], frames.Select(f => f.GetProperty("event").GetString()));
        Assert.All(frames.Take(5), f => Assert.True(f.TryGetProperty("bars", out _)));
        Assert.False(frames[^1].GetProperty("ok").GetBoolean());
        // The result is the solver's answer as it has always been: the server's error.
        Assert.StartsWith("{\"error\":", frames[^1].GetProperty("result").GetString());
        Assert.Empty(frames[^1].GetProperty("qubits").EnumerateObject());
    }

    // #838: Grover's solver threw when the quantum service was down, so solve and both visualize formats answered an empty 500.
    // Now it ends like the other server solvers: a Reject, then Done not ok, with the server's error as its answer.
    [Fact]
    public async Task Frames_Grover_ServerDown_EndsNotOk_SayingTheServiceCouldNotBeReached() {
        using var fake = FakeQuantumServer.Install(out var server);
        server.Canned = (HttpStatusCode.ServiceUnavailable, "{}");
        var (status, body) = await Visualize("visualization=unstructuredsearchvisualization&format=frames&solver=unstructuredgroversolver", "(0, 1, 0, 0)");
        Assert.Equal(HttpStatusCode.OK, status);
        var frames = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().ToList();
        Assert.Equal("Reject", frames[^2].GetProperty("event").GetString());
        Assert.Equal("Done", frames[^1].GetProperty("event").GetString());
        Assert.False(frames[^1].GetProperty("ok").GetBoolean());
        Assert.Equal("No answer: the quantum service could not be reached.", frames[^1].GetProperty("caption").GetString());
        Assert.StartsWith("{\"error\":", frames[^1].GetProperty("result").GetString());
        Assert.Empty(frames[^1].GetProperty("qubits").EnumerateObject());
    }

    [Fact]
    public async Task Frames_Grover_NoBitString_EndsNotOk_SayingTheServerGaveNoAnswer() {
        using var fake = FakeQuantumServer.Install(out var server);
        server.Canned = (HttpStatusCode.OK, "{\"qasm\": \"OPENQASM 2.0;\"}");
        var (status, body) = await Visualize("visualization=unstructuredsearchvisualization&format=frames&solver=unstructuredgroversolver", "(0, 1, 0, 0)");
        Assert.Equal(HttpStatusCode.OK, status);
        var frames = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().ToList();
        Assert.Equal(["Reject", "Done"], frames.TakeLast(2).Select(f => f.GetProperty("event").GetString()));
        Assert.False(frames[^1].GetProperty("ok").GetBoolean());
        Assert.Equal("No answer: the quantum server did not give one.", frames[^1].GetProperty("caption").GetString());
    }

    [Fact]
    public async Task Grover_ServerDown_SolveAndListVisualize_AnswerTheServersError() {
        using var fake = FakeQuantumServer.Install(out var server);
        server.Canned = (HttpStatusCode.ServiceUnavailable, "{}");
        var content = new StringContent(JsonSerializer.Serialize("(0, 1, 0, 0)"), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/solve?solver=unstructuredgroversolver", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("error", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var (status, body) = await Visualize("visualization=unstructuredsearchvisualization&solver=unstructuredgroversolver", "(0, 1, 0, 0)");
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.NotEmpty(body);
    }

    [Theory]
    [InlineData("deutschjozsadefaultvisualization", "(1,1,1)")]
    [InlineData("simonvisualization", "(1,2,3)")]
    [InlineData("deutschjozsadefaultvisualization", "(0,0,2,2)")]
    public async Task Frames_AnInstanceThatDoesNotParse_Is400(string vis, string instance) {
        var (status, body) = await Visualize($"visualization={vis}&format=frames", instance);
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("instance_parse_error", JsonDocument.Parse(body).RootElement.GetProperty("error").GetString());
    }
}

// Stopped runs, under the 0.05 s budget of TinyBudgetAppFactory.
[Collection(TimingCollection.Name)]
public class QuantumCircuitFrames_StoppedRun_Endpoint_Tests : IClassFixture<TinyBudgetAppFactory> {
    private readonly HttpClient _client;

    public QuantumCircuitFrames_StoppedRun_Endpoint_Tests(TinyBudgetAppFactory factory) {
        // Before the client is made: its handler copies the setting.
        factory.Server.PreserveExecutionContext = true;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Frames_Stopped_ByASlowQuantumServer_EndsBlank_NotOk() {
        // The circuit is drawn at once, then the server takes 0.3 s to answer: the step after it is past the 0.05 s budget,
        // so the run stops there. (The call itself is not cut short; the server client's own timeout is 30 s.)
        using var fake = FakeQuantumServer.Install(out var server);
        const string query = "/ProblemProvider/visualize?visualization=bernsteinvaziranidefaultvisualization&format=frames&solver=bernsteinvaziraniquantumsolver";
        async Task<(HttpStatusCode, string)> Post() {
            var content = new StringContent(JsonSerializer.Serialize("(0,1,0,1,1,0,1,0)"), Encoding.UTF8, "application/json");
            var response = await _client.PostAsync(query, content, TestContext.Current.CancellationToken);
            return (response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }
        // Once with a quick server, so that a fresh app's first-call warm-up is not what runs past the budget.
        await Post();
        int asked = server.Requests.Count;
        server.Delay = TimeSpan.FromMilliseconds(300);
        var sw = Stopwatch.StartNew();
        var (status, body) = await Post();
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(20), $"took {sw.Elapsed}");
        Assert.Equal(asked + 1, server.Requests.Count);
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal("QuantumCircuit", root.GetProperty("type").GetString());
        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        Assert.Equal(["Try", "Try", "Try", "Try", "Try", "Accept", "Done"], frames.Select(f => f.GetProperty("event").GetString()));
        var done = frames[^1];
        Assert.False(done.GetProperty("ok").GetBoolean());
        Assert.Matches(@"^Stopped after 0\.05 s and 6 steps; the solver had not finished\.$", done.GetProperty("caption").GetString());
        Assert.Equal(0, done.GetProperty("applied").GetInt32());
        Assert.Empty(done.GetProperty("focus").EnumerateArray());
        Assert.Empty(done.GetProperty("queries").EnumerateArray());
        Assert.Empty(done.GetProperty("columns").EnumerateObject());
        Assert.Empty(done.GetProperty("gates").EnumerateObject());
        Assert.Empty(done.GetProperty("qubits").EnumerateObject());
        Assert.False(done.TryGetProperty("bars", out _));
        Assert.False(done.TryGetProperty("outcome", out _));
        Assert.False(done.TryGetProperty("result", out _));
    }
}
