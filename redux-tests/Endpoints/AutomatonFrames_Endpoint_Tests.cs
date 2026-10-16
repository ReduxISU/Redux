using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Automaton picture on the actual /ProblemProvider/visualize?format=frames output, for DFA and NFA with every one of
// their solvers, plus the guards that keep everything old frontends read exactly as it was:
//   - the list format (Snapshots/automaton_list_snapshots.tsv and <vis>.json, captured before the picture existed),
//   - /info for every visualization and solver that was touched (info.<name>.json, captured the same way).
public class AutomatonFrames_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public AutomatonFrames_Endpoint_Tests(AppFactory factory) {
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

    private static readonly HashSet<string> Vocabulary = new() {
        "Background", "ElementHighlight", "Solution", "Rejected", "Untraveled", "Covered", "Blocked",
    };

    // visualization, solver, problem
    private static readonly (string Vis, string Solver, string Problem)[] SolverRows = {
        ("dfavisualization", "dfasolver", "dfa"),
        ("nfavisualization", "nfasolver", "nfa"),
        ("nfavisualization", "nfasubsetsolver", "nfa"),
    };

    // The instances of the snapshot, by problem.
    private static IEnumerable<string> Instances(string problem) =>
        ReadSnapshot("automaton_solver_answers.tsv").Split('\n').Where(l => l.Length > 0).Select(l => l.Split('\t'))
            .Where(p => p[0] == problem + "solver").Select(p => p[1]);

    public static TheoryData<string, string, string> FramesCases {
        get {
            var data = new TheoryData<string, string, string>();
            foreach (var r in SolverRows)
                foreach (var instance in Instances(r.Problem)) data.Add(r.Vis, r.Solver, instance);
            return data;
        }
    }

    public static TheoryData<string, string, string> ListCases {
        get {
            var data = new TheoryData<string, string, string>();
            foreach (var line in ReadSnapshot("automaton_list_snapshots.tsv").Split('\n').Where(l => l.Length > 0)) {
                var parts = line.Split('\t');
                data.Add(parts[0], parts[1], parts[2]);
            }
            return data;
        }
    }

    public static TheoryData<string> TouchedInterfaces => new([
        "dfavisualization", "nfavisualization", "dfatablevisualization", "nfatablevisualization", "dfasolver", "nfasolver",
    ]);

    // ---- What old frontends read must not change. ----

    [Theory]
    [MemberData(nameof(TouchedInterfaces))]
    public async Task Info_IsUnchangedByTheAutomatonPicture(string name) {
        var response = await _client.GetAsync($"/ProblemProvider/info?interface={name}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ReadSnapshot($"info.{name}.json"), body.Replace("\r\n", "\n"));
        Assert.DoesNotContain("ListReadsTypedSteps", body);
        Assert.DoesNotContain("\"Automaton\"", body);
    }

    [Theory]
    [MemberData(nameof(ListCases))]
    public async Task List_MatchesPreChangeSnapshot_WithItsOwnSolverOrTheSubsetSolver(string vis, string instance, string expected) {
        var (status, body) = await Visualize($"visualization={vis}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(expected, Compact(body));
        // The NFA pictures draw the same list whichever NFA solver runs: neither list method draws the solver's steps.
        if (vis.StartsWith("nfa")) {
            var (_, withSubset) = await Visualize($"visualization={vis}&solver=nfasubsetsolver&format=list", instance);
            Assert.Equal(expected, Compact(withSubset));
        }
    }

    [Fact]
    public async Task List_DfaDefault_IsByteForByteTheSnapshot() {
        var (_, body) = await Visualize("visualization=dfavisualization", ReadSnapshot("dfavisualization.instance.txt"));
        Assert.Equal(ReadSnapshot("dfavisualization.json"), body);
    }

    // ---- The picture ----

    [Theory]
    [MemberData(nameof(FramesCases))]
    public async Task Frames_EverySolver_HasTheAutomatonPayloadAndFramesThatAgreeWithTheAnswer(string vis, string solver, string instance) {
        var (status, body) = await Visualize($"visualization={vis}&format=frames&solver={solver}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal(new[] { "type", "payload", "frames" }, root.EnumerateObject().Select(p => p.Name));
        Assert.Equal("Automaton", root.GetProperty("type").GetString());

        var payload = root.GetProperty("payload");
        bool nfa = vis.StartsWith("nfa");
        bool hasGarbage = payload.TryGetProperty("garbage", out var garbage);
        Assert.Equal(
            ["kind", "layout", "states", "start", "accepting", "alphabet", "transitions", "input", .. hasGarbage ? new[] { "garbage" } : [], .. nfa ? new[] { "paths", "pathsTruncated" } : []],
            payload.EnumerateObject().Select(p => p.Name));
        Assert.Equal(nfa ? "nfa" : "dfa", payload.GetProperty("kind").GetString());
        Assert.Equal("layered", payload.GetProperty("layout").GetString());
        Assert.False(nfa && hasGarbage);

        var stateIds = payload.GetProperty("states").EnumerateArray().Select(s => s.GetProperty("id").GetString()!).ToHashSet();
        Assert.All(stateIds, id => Assert.StartsWith("q:", id));
        Assert.Contains(payload.GetProperty("start").GetString()!, stateIds);
        Assert.All(payload.GetProperty("accepting").EnumerateArray(), a => Assert.Contains(a.GetString()!, stateIds));
        var nodeIds = new HashSet<string>(stateIds);
        if (hasGarbage) {
            Assert.Equal("garbage", garbage.GetProperty("id").GetString());
            nodeIds.Add("garbage");
        }
        var edgeIds = new HashSet<string>();
        foreach (var t in payload.GetProperty("transitions").EnumerateArray()) {
            string a = t.GetProperty("a").GetString()!, b = t.GetProperty("b").GetString()!;
            Assert.Contains(a, stateIds);
            Assert.Contains(b, stateIds);
            Assert.Equal($"{a}>{b}", t.GetProperty("id").GetString());
            Assert.True(edgeIds.Add(t.GetProperty("id").GetString()!), "one arrow per (from, to)");
            Assert.Equal(string.Join(",", t.GetProperty("symbols").EnumerateArray().Select(x => x.GetString())), t.GetProperty("label").GetString());
        }
        int inputLength = payload.GetProperty("input").GetArrayLength();

        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        void CheckFrame(JsonElement f) {
            Assert.Equal(["event", "caption", "focus", "position", "active", "nodes", "edges", .. f.TryGetProperty("ok", out _) ? new[] { "ok" } : []],
                f.EnumerateObject().Select(p => p.Name));
            Assert.InRange(f.GetProperty("position").GetInt32(), 0, inputLength);
            Assert.All(f.GetProperty("active").EnumerateArray(), id => Assert.Contains(id.GetString()!, nodeIds));
            Assert.All(f.GetProperty("focus").EnumerateArray(), id => Assert.True(nodeIds.Contains(id.GetString()!) || edgeIds.Contains(id.GetString()!), id.GetString()));
            foreach (var n in f.GetProperty("nodes").EnumerateObject()) {
                Assert.Contains(n.Name, nodeIds);
                Assert.Contains(n.Value.GetProperty("state").GetString()!, Vocabulary);
            }
            foreach (var e in f.GetProperty("edges").EnumerateObject()) {
                Assert.Contains(e.Name, edgeIds);
                Assert.Contains(e.Value.GetProperty("state").GetString()!, Vocabulary);
            }
            // Solution only in an Accept step or the answer.
            if (f.GetProperty("event").GetString() is not ("Accept" or "Done"))
                Assert.DoesNotContain(f.GetProperty("nodes").EnumerateObject(), n => n.Value.GetProperty("state").GetString() == "Solution");
        }
        Assert.All(frames, CheckFrame);

        // One Done frame, last, and its verdict is the solver's.
        var done = Assert.Single(frames, f => f.GetProperty("event").GetString() == "Done");
        Assert.Equal(done, frames[^1]);
        var s = (API.Interfaces.ISolver)Activator.CreateInstance(ProblemProvider.Solvers[solver])!;
        bool accepts = s.solve(instance).StartsWith("The sequence of states to accept is:");
        Assert.Equal(accepts, done.GetProperty("ok").GetBoolean());
        if (accepts)
            Assert.Contains(done.GetProperty("nodes").EnumerateObject(), n => n.Value.GetProperty("state").GetString() == "Solution");

        // The NFA's runs for the path picker: frames of the same shape, a Done last whose ok is the run's verdict.
        if (nfa) {
            var paths = payload.GetProperty("paths").EnumerateArray().ToList();
            Assert.NotEmpty(paths);
            Assert.Equal(accepts, paths.Any(p => p.GetProperty("accepted").GetBoolean()));
            foreach (var p in paths) {
                Assert.Equal(["id", "label", "accepted", "outcome", "frames"], p.EnumerateObject().Select(x => x.Name));
                var pf = p.GetProperty("frames").EnumerateArray().ToList();
                Assert.All(pf, CheckFrame);
                Assert.Equal("Done", pf[^1].GetProperty("event").GetString());
                Assert.Equal(p.GetProperty("accepted").GetBoolean(), pf[^1].GetProperty("ok").GetBoolean());
            }
        }
    }

    [Fact]
    public async Task Frames_Dfa_WithAMissingMove_ShowsTheGarbageState() {
        var (_, body) = await Visualize("visualization=dfavisualization&format=frames", "(({1,2},{a,b},{(1,a,2)},1,{2}),ab)");
        var root = JsonDocument.Parse(body).RootElement;
        var garbage = root.GetProperty("payload").GetProperty("garbage");
        Assert.Equal("Garbage", garbage.GetProperty("label").GetString());
        Assert.Equal(3, garbage.GetProperty("missing").GetArrayLength());
        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        Assert.Equal(["Try", "Try", "Reject", "Done"], frames.Select(f => f.GetProperty("event").GetString()));
        Assert.Equal("q:1>q:2", frames[1].GetProperty("focus")[0].GetString());
        Assert.Equal("ElementHighlight", frames[1].GetProperty("edges").GetProperty("q:1>q:2").GetProperty("state").GetString());
        foreach (var f in frames.Skip(2)) {
            Assert.Equal(["garbage"], f.GetProperty("active").EnumerateArray().Select(a => a.GetString()));
            Assert.Equal("Rejected", f.GetProperty("nodes").GetProperty("garbage").GetProperty("state").GetString());
            Assert.Equal(1, f.GetProperty("position").GetInt32());
        }
        Assert.False(frames[^1].GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task Frames_Dfa_ASymbolOutsideTheAlphabet_FallsIntoTheGarbageState() {
        // The bug report: the DFA's default instance on "abc" ended Rejected in 2. 'c' is a missing move of 2, so the run
        // falls into Garbage there and ends in it, as at any missing move. The JSON as the API writes it.
        const string instance = "(({1,2,3},{a,b},{(1,a,2),(1,b,3),(2,a,2),(2,b,2),(3,a,2),(3,b,3)},1,{2}),abc)";
        var (status, body) = await Visualize("visualization=DFAVisualization&format=frames", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var root = JsonDocument.Parse(body).RootElement;
        var payload = root.GetProperty("payload");
        Assert.Equal(["a", "b"], payload.GetProperty("alphabet").EnumerateArray().Select(a => a.GetString()));
        Assert.Equal(["a", "b", "c"], payload.GetProperty("input").EnumerateArray().Select(a => a.GetString()));
        Assert.Equal("""{"id":"garbage","label":"Garbage","missing":[{"state":"q:2","symbol":"c"}]}""",
            Compact(payload.GetProperty("garbage").GetRawText()));
        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        Assert.Equal(["Try", "Try", "Try", "Reject", "Done"], frames.Select(f => f.GetProperty("event").GetString()));
        Assert.Equal("'c' is not in the alphabet, so 2 has no move on it, and the DFA falls into the Garbage state and stays there.",
            frames[3].GetProperty("caption").GetString());
        Assert.Equal(["garbage"], frames[3].GetProperty("focus").EnumerateArray().Select(a => a.GetString()));
        foreach (var f in frames.Skip(3)) {
            Assert.Equal(["garbage"], f.GetProperty("active").EnumerateArray().Select(a => a.GetString()));
            Assert.Equal("Rejected", f.GetProperty("nodes").GetProperty("garbage").GetProperty("state").GetString());
            Assert.Equal(2, f.GetProperty("position").GetInt32());
            Assert.False(f.GetProperty("nodes").TryGetProperty("q:2", out var q2) && q2.GetProperty("state").GetString() == "Rejected");
        }
        Assert.False(frames[^1].GetProperty("ok").GetBoolean());
        Assert.Equal("Untraveled", frames[^1].GetProperty("nodes").GetProperty("q:3").GetProperty("state").GetString());

        // The verifier keeps its answer.
        var request = new StringContent(JsonSerializer.Serialize(new { Certificate = "1,2,2,2", ProblemInstance = instance }), Encoding.UTF8, "application/json");
        var verify = await _client.PostAsync("/ProblemProvider/verify?verifier=dfaverifier", request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        Assert.Contains("false", await verify.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Frames_Nfa_ASymbolOutsideTheAlphabet_KillsEveryBranch_WithoutAGarbageState() {
        const string instance = "(({1,2,3},{a,b},{(1,a,2),(1,ε,2),(1,b,3),(2,a,2),(2,ε,3),(2,b,2),(3,a,2),(3,b,3)},1,{2}),ac)";
        foreach (var solver in new[] { "nfasolver", "nfasubsetsolver" }) {
            var (status, body) = await Visualize($"visualization=nfavisualization&format=frames&solver={solver}", instance);
            Assert.Equal(HttpStatusCode.OK, status);
            var root = JsonDocument.Parse(body).RootElement;
            Assert.False(root.GetProperty("payload").TryGetProperty("garbage", out _));
            var frames = root.GetProperty("frames").EnumerateArray().ToList();
            Assert.Contains(frames, f => f.GetProperty("event").GetString() == "Reject"
                && f.GetProperty("caption").GetString()!.Contains("'c' is not in the alphabet"));
            Assert.False(frames[^1].GetProperty("ok").GetBoolean());
        }
    }

    [Fact]
    public async Task Frames_StateNamesWithSeparators_KeepEveryIdUnique() {
        // State names that hold the id separators (":" and ">") are escaped, not refused.
        var (status, body) = await Visualize("visualization=dfavisualization&format=frames",
            "(({q:0,a>b,x~y},{a},{(q:0,a,a>b),(a>b,a,x~y),(x~y,a,x~y)},q:0,{x~y}),aa)");
        Assert.Equal(HttpStatusCode.OK, status);
        var root = JsonDocument.Parse(body).RootElement;
        var payload = root.GetProperty("payload");
        Assert.Equal([@"q:q\:0", @"q:a\>b", "q:x~y"], payload.GetProperty("states").EnumerateArray().Select(s => s.GetProperty("id").GetString()));
        Assert.Equal(["q:0", "a>b", "x~y"], payload.GetProperty("states").EnumerateArray().Select(s => s.GetProperty("label").GetString()));
        var arrow = payload.GetProperty("transitions")[0];
        Assert.Equal((@"q:q\:0>q:a\>b", @"q:q\:0", @"q:a\>b"),
            (arrow.GetProperty("id").GetString(), arrow.GetProperty("a").GetString(), arrow.GetProperty("b").GetString()));
        var done = root.GetProperty("frames").EnumerateArray().Last();
        Assert.True(done.GetProperty("ok").GetBoolean());
        Assert.Equal("Solution", done.GetProperty("nodes").GetProperty("q:x~y").GetProperty("state").GetString());
    }

    [Fact]
    public async Task Frames_NfaSubsetSolver_ShowsEveryBranchAtOnce() {
        var (_, body) = await Visualize("visualization=nfavisualization&format=frames&solver=nfasubsetsolver",
            "(({1,2,3},{a,b},{(1,a,2),(1,ε,2),(1,b,3),(2,a,2),(2,ε,3),(2,b,2),(3,a,2),(3,b,3)},1,{2}),a)");
        var frames = JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().ToList();
        Assert.Equal(["q:1", "q:2", "q:3"], frames[0].GetProperty("active").EnumerateArray().Select(a => a.GetString()));
        Assert.Equal(["q:2", "q:3"], frames[1].GetProperty("active").EnumerateArray().Select(a => a.GetString()));
        // The ε arrows followed at the start are highlighted with the start state.
        Assert.Equal("ElementHighlight", frames[0].GetProperty("edges").GetProperty("q:2>q:3").GetProperty("state").GetString());
        Assert.Equal("Solution", frames[^1].GetProperty("nodes").GetProperty("q:2").GetProperty("state").GetString());
    }

    [Fact]
    public async Task Frames_NfaPaths_FollowOneRunEach_LabelledWithTheirOutcome() {
        var (_, body) = await Visualize("visualization=nfavisualization&format=frames", "(({1,2,3},{a},{(1,a,2),(1,a,3)},1,{3}),a)");
        var paths = JsonDocument.Parse(body).RootElement.GetProperty("payload").GetProperty("paths").EnumerateArray().ToList();
        Assert.Equal(["Path 1: 1 → 3, accepts", "Path 2: 1 → 2, rejects"], paths.Select(p => p.GetProperty("label").GetString()));
        Assert.Equal(["accepts", "rejects"], paths.Select(p => p.GetProperty("outcome").GetString()));
        Assert.False(JsonDocument.Parse(body).RootElement.GetProperty("payload").GetProperty("pathsTruncated").GetBoolean());
    }

    [Fact]
    public async Task Frames_DfaTableAndNfaTable_AreTheTablePicture_NotTheAutomaton() {
        // The trace tables are the Table picture's second view of the same runs (#671, TableAutomatonFrames_Endpoint_Tests).
        foreach (var vis in new[] { "dfatablevisualization", "nfatablevisualization" }) {
            var (_, body) = await Visualize($"visualization={vis}&format=frames", ReadSnapshot($"{vis}.instance.txt"));
            Assert.Equal("Table", JsonDocument.Parse(body).RootElement.GetProperty("type").GetString());
        }
    }
}

// A stopped run, under the 0.05 s budget of TinyBudgetAppFactory.
[Collection(TimingCollection.Name)]
public class AutomatonFrames_StoppedRun_Endpoint_Tests : IClassFixture<TinyBudgetAppFactory> {
    private readonly HttpClient _client;

    public AutomatonFrames_StoppedRun_Endpoint_Tests(TinyBudgetAppFactory factory) {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Frames_StoppedNfaRun_EndsWithABlankNotOkDone() {
        var sw = Stopwatch.StartNew();
        var content = new StringContent(JsonSerializer.Serialize(StepTimeLimit_Tests.SlowNfa), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/visualize?visualization=nfavisualization&format=frames&solver=nfasolver", content, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), $"took {sw.Elapsed}");

        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal("Automaton", root.GetProperty("type").GetString());
        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        Assert.True(frames.Count >= 2);
        var done = frames[^1];
        Assert.Equal("Done", done.GetProperty("event").GetString());
        Assert.False(done.GetProperty("ok").GetBoolean());
        Assert.Matches(@"^Stopped after 0\.05 s and [\d,]+ steps?; the solver had not finished\.", done.GetProperty("caption").GetString());
        Assert.Empty(done.GetProperty("active").EnumerateArray());
        Assert.Empty(done.GetProperty("nodes").EnumerateObject());
        Assert.Empty(done.GetProperty("edges").EnumerateObject());
        Assert.Single(frames, f => f.GetProperty("event").GetString() == "Done");
        Assert.DoesNotContain(frames, f => f.GetProperty("nodes").EnumerateObject().Any(n => n.Value.GetProperty("state").GetString() == "Solution"));
        // 2^40 runs: the path picker lists the first 40 and says there are more.
        var payload = root.GetProperty("payload");
        Assert.Equal(40, payload.GetProperty("paths").GetArrayLength());
        Assert.True(payload.GetProperty("pathsTruncated").GetBoolean());

    }
}
