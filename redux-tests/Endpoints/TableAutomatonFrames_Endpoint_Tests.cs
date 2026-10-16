using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Table picture's trace table for DFA and NFA, the second view of the runs the Automaton picture draws, on the actual
// /ProblemProvider/visualize?format=frames output of DFATableVisualization and NFATableVisualization. Their list format and
// /info are held to the snapshots taken before the Automaton picture (AutomatonFrames_Endpoint_Tests), so that is not
// repeated here.
public class TableAutomatonFrames_Endpoint_Tests : IClassFixture<AppFactory> {
    private readonly HttpClient _client;

    public TableAutomatonFrames_Endpoint_Tests(AppFactory factory) {
        _client = factory.CreateClient();
    }

    private async Task<(HttpStatusCode Status, string Body)> Visualize(string query, string instance) {
        var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/ProblemProvider/visualize?{query}", content, TestContext.Current.CancellationToken);
        return (response.StatusCode, (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Replace("\r\n", "\n"));
    }

    private async Task<List<JsonElement>> Frames(string query, string instance) {
        var (status, body) = await Visualize(query, instance);
        Assert.Equal(HttpStatusCode.OK, status);
        return JsonDocument.Parse(body).RootElement.GetProperty("frames").EnumerateArray().ToList();
    }

    private static string Readable(JsonElement element) =>
        JsonSerializer.Serialize(element, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    private static List<string> Column(JsonElement frame, int column) =>
        frame.GetProperty("values").EnumerateArray().Select(r => r[column].GetString()!).ToList();

    private static string RowStates(JsonElement frame) =>
        string.Join(" ", frame.GetProperty("rows").EnumerateObject().Select(r => $"{r.Name}:{r.Value.GetProperty("state").GetString()}"));

    private static readonly HashSet<string> Vocabulary = new() {
        "Background", "ElementHighlight", "Solution", "Rejected", "Untraveled", "Covered", "Blocked",
    };

    // Every automaton instance the Automaton picture's snapshot holds, with each solver that runs it.
    public static TheoryData<string, string, string, bool> FramesCases {
        get {
            var data = new TheoryData<string, string, string, bool>();
            var path = Path.Combine(AppContext.BaseDirectory, "Endpoints", "Snapshots", "automaton_solver_answers.tsv");
            foreach (var line in File.ReadAllLines(path).Where(l => l.Length > 0)) {
                var parts = line.Split('\t');
                bool accepts = JsonSerializer.Deserialize<string>(parts[2])!.StartsWith("The sequence of states to accept is:");
                if (parts[0] == "dfasolver") data.Add("dfatablevisualization", "dfasolver", parts[1], accepts);
                else {
                    data.Add("nfatablevisualization", "nfasolver", parts[1], accepts);
                    data.Add("nfatablevisualization", "nfasubsetsolver", parts[1], accepts);
                }
            }
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(FramesCases))]
    public async Task Frames_EveryRun_IsATraceTable_ThatEndsOnTheVerdict(string vis, string solver, string instance, bool accepts) {
        var (status, body) = await Visualize($"visualization={vis}&format=frames&solver={solver}", instance);
        Assert.Equal(HttpStatusCode.OK, status);
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal("Table", root.GetProperty("type").GetString());
        var payload = root.GetProperty("payload");
        Assert.Equal(["kind", "shape", "columns", "rows", "blank"], payload.EnumerateObject().Select(p => p.Name));
        Assert.Equal("trace", payload.GetProperty("shape").GetString());
        Assert.Equal(
            "[{\"id\":\"c0\",\"label\":\"Step\",\"key\":\"step\"},{\"id\":\"c1\",\"label\":\"Read\",\"key\":\"read\"},"
            + "{\"id\":\"c2\",\"label\":\"From\",\"key\":\"from\"},{\"id\":\"c3\",\"label\":\"To\",\"key\":\"to\"},{\"id\":\"c4\",\"label\":\"Accepting?\",\"key\":\"accepting\"}]",
            Readable(payload.GetProperty("columns")));
        var rows = payload.GetProperty("rows").EnumerateArray().ToList();
        for (int i = 0; i < rows.Count; i++) Assert.Equal($"{{\"id\":\"r{i}\"}}", Readable(rows[i]));

        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        // The blank is the start row alone, nothing read, as the run's first row writes it.
        var blank = payload.GetProperty("blank").GetProperty("values");
        Assert.Equal(1, blank.GetArrayLength());
        Assert.Equal(["0", "—", "—"], blank[0].EnumerateArray().Take(3).Select(c => c.GetString()));
        if (frames[0].GetProperty("values").GetArrayLength() > 0 && frames[0].GetProperty("values")[0][0].GetString() == "0")
            Assert.Equal(Readable(frames[0].GetProperty("values")[0]), Readable(blank[0]));
        foreach (var f in frames) {
            Assert.Equal(["event", "caption", "focus", "values", "rows", "cells", "changed", "step", .. f.TryGetProperty("ok", out _) ? new[] { "ok" } : []],
                f.EnumerateObject().Select(p => p.Name));
            var values = f.GetProperty("values").EnumerateArray().ToList();
            Assert.InRange(values.Count, 0, rows.Count);
            Assert.All(values, r => Assert.Equal(5, r.GetArrayLength()));
            Assert.All(values, r => Assert.All(r.EnumerateArray(), c => Assert.Equal(JsonValueKind.String, c.ValueKind)));
            foreach (var r in f.GetProperty("rows").EnumerateObject()) {
                Assert.InRange(int.Parse(r.Name[1..]), 0, values.Count - 1);
                Assert.Contains(r.Value.GetProperty("state").GetString()!, Vocabulary);
            }
            Assert.Empty(f.GetProperty("cells").EnumerateObject());
            Assert.All(f.GetProperty("focus").EnumerateArray(), id => Assert.InRange(int.Parse(id.GetString()![1..]), 0, values.Count - 1));
            // Solution only for an Accept or the answer.
            if (f.GetProperty("event").GetString() is "Try" or "Reject" or "Backtrack")
                Assert.DoesNotContain("Solution", RowStates(f));
        }
        var done = Assert.Single(frames, f => f.GetProperty("event").GetString() == "Done");
        Assert.Equal(done, frames[^1]);
        Assert.Equal(accepts, done.GetProperty("ok").GetBoolean());
        int last = done.GetProperty("values").GetArrayLength() - 1;
        Assert.True(last >= 0);
        Assert.Equal(accepts ? "Solution" : "Rejected", done.GetProperty("rows").GetProperty($"r{last}").GetProperty("state").GetString());
        if (accepts) Assert.Equal("yes", Column(done, 4)[last]);
    }

    [Fact]
    public async Task Frames_Dfa_AddsARowPerSymbol_AsTheMockupDoes() {
        var frames = await Frames("visualization=dfatablevisualization&format=frames", "(({1,2,3},{a,b},{(1,a,2),(1,b,1),(2,a,2),(2,b,3),(3,a,2),(3,b,1)},1,{3}),abaab)");
        Assert.Equal(7, frames.Count);
        Assert.Equal([1, 2, 3, 4, 5, 6, 6], frames.Select(f => f.GetProperty("values").GetArrayLength()));
        Assert.Equal(
            "{\"event\":\"Try\",\"caption\":\"Read 'b': move from 2 to 3.\",\"focus\":[\"r2\"],"
            + "\"values\":[[\"0\",\"—\",\"—\",\"1\",\"no\"],[\"1\",\"a\",\"1\",\"2\",\"no\"],[\"2\",\"b\",\"2\",\"3\",\"yes\"]],"
            + "\"rows\":{\"r2\":{\"state\":\"ElementHighlight\"}},\"cells\":{},\"changed\":[\"r2c3\"],\"step\":2}",
            Readable(frames[2]));
        Assert.Equal(["1", "2", "3", "2", "2", "3"], Column(frames[^1], 3));
        Assert.Equal("r5:Solution", RowStates(frames[^1]));
    }

    [Fact]
    public async Task Frames_Dfa_AMissingMove_IsARowIntoGarbage() {
        var frames = await Frames("visualization=dfatablevisualization&format=frames", "(({1,2},{a,b},{(1,a,2),(2,b,2)},1,{2}),abba)");
        var reject = frames[^2];
        Assert.Equal("Reject", reject.GetProperty("event").GetString());
        Assert.Equal("[\"4\",\"a\",\"2\",\"Garbage\",\"no\"]", Readable(reject.GetProperty("values")[4]));
        Assert.Equal("r4:Rejected", RowStates(reject));
        Assert.Equal("r4:Rejected", RowStates(frames[^1]));
        Assert.False(frames[^1].GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task Frames_NfaSubsetSimulation_ShowsEveryActiveStateAtOnce() {
        var frames = await Frames("visualization=nfatablevisualization&format=frames&solver=nfasubsetsolver",
            "(({1,2,3,4},{a,b},{(1,a,1),(1,b,1),(1,a,2),(2,b,3),(3,ε,4),(4,a,4),(4,b,4)},1,{4}),babb)");
        var done = frames[^1];
        Assert.Equal(["{1}", "{1}", "{1, 2}", "{1, 3, 4}", "{1, 4}"], Column(done, 3));
        Assert.Equal(["—", "{1}", "{1}", "{1, 2}", "{1, 3, 4}"], Column(done, 2));
        Assert.Equal("r4:Solution", RowStates(done));
        // The Accept's row is the answer, not a highlight.
        Assert.Equal("r4:Solution", RowStates(frames[^2]));

        var dead = await Frames("visualization=nfatablevisualization&format=frames&solver=nfasubsetsolver", "(({1,2},{a,b},{(1,a,2)},1,{2}),ab)");
        Assert.Equal("[\"2\",\"b\",\"2\",\"∅\",\"no\"]", Readable(dead[^1].GetProperty("values")[2]));
        Assert.Equal("r2:Rejected", RowStates(dead[^1]));
    }

    [Fact]
    public async Task Frames_NfaBacktracking_ShowsTheBranchBeingExplored_AndEndsOnTheFirstThatAccepted() {
        var frames = await Frames("visualization=nfatablevisualization&format=frames",
            "(({1,2,3},{a,b},{(1,a,2),(1,ε,2),(1,b,3),(2,a,2),(2,ε,3),(2,b,2),(3,a,2),(3,b,3)},1,{2}),a)");
        var events = frames.Select(f => f.GetProperty("event").GetString()).ToList();
        int back = events.IndexOf("Backtrack");
        // A Backtrack takes the last move's row away.
        Assert.Equal(frames[back - 1].GetProperty("values").GetArrayLength() - 1, frames[back].GetProperty("values").GetArrayLength());
        Assert.Equal(["ε", "ε", "a"], Column(frames[^1], 1).Skip(1));
        Assert.Equal(["1", "2", "3", "2"], Column(frames[^1], 3));
        Assert.Equal("r3:Solution", RowStates(frames[^1]));
        Assert.Equal("Accepted: 3 runs accept the input.", frames[^1].GetProperty("caption").GetString());
    }

    [Fact]
    public async Task Frames_PastTheStepCap_ARowOfDotsStandsForTheStepsNotShown() {
        string input = new('a', 200);
        var dfa = await Frames("visualization=dfatablevisualization&format=frames", $"(({{1}},{{a}},{{(1,a,1)}},1,{{1}}),{input})");
        var done = dfa[^1];
        Assert.Equal(152, done.GetProperty("values").GetArrayLength());
        Assert.Equal("[\"…\",\"…\",\"…\",\"…\",\"…\"]", Readable(done.GetProperty("values")[150]));
        Assert.Equal("[\"200\",\"a\",\"…\",\"1\",\"yes\"]", Readable(done.GetProperty("values")[151]));
        Assert.Equal("r151:Solution", RowStates(done));
        Assert.Contains("not shown", done.GetProperty("caption").GetString());

        // The search goes all the way down through 1 first, so its first (and only) accepting run, ending 1 -> 2 on the last
        // symbol, comes after the cap. The branch at the cap is not shown as its start: its row stands after the start row
        // and a row of dots.
        string many = new('a', 200);
        var nfa = await Frames("visualization=nfatablevisualization&format=frames", $"(({{1,2}},{{a}},{{(1,a,1),(1,a,2)}},1,{{2}}),{many})");
        var accept = nfa[^2];
        Assert.Equal("Accept", accept.GetProperty("event").GetString());
        Assert.Equal(["0", "…", "200"], Column(accept, 0));
        Assert.Equal("[\"200\",\"a\",\"…\",\"2\",\"yes\"]", Readable(accept.GetProperty("values")[2]));
        Assert.Equal("r2:Solution", RowStates(accept));
        Assert.Equal(Readable(accept.GetProperty("values")), Readable(nfa[^1].GetProperty("values")));
    }

    // Before Run the trace shows its starting point: the start row alone, nothing read, nothing marked.
    [Fact]
    public async Task Frames_PayloadBlank_IsTheStartRow_AsTheRunWritesIt() {
        var (_, dfa) = await Visualize("visualization=dfatablevisualization&format=frames", "(({1,2,3},{a,b},{(1,a,2),(1,b,1),(2,a,2),(2,b,3),(3,a,2),(3,b,1)},1,{3}),abaab)");
        Assert.Equal("{\"values\":[[\"0\",\"—\",\"—\",\"1\",\"no\"]]}", Readable(JsonDocument.Parse(dfa).RootElement.GetProperty("payload").GetProperty("blank")));

        // An NFA whose start state has an ε move: subset simulation starts in the ε-closure, the backtracking search in the
        // start state alone, and each blank is that run's own first row.
        const string closure = "(({1,2},{a},{(1,ε,2),(2,a,2)},1,{2}),a)";
        var (_, subset) = await Visualize("visualization=nfatablevisualization&format=frames&solver=nfasubsetsolver", closure);
        Assert.Equal("{\"values\":[[\"0\",\"—\",\"—\",\"{1, 2}\",\"yes\"]]}", Readable(JsonDocument.Parse(subset).RootElement.GetProperty("payload").GetProperty("blank")));
        var (_, search) = await Visualize("visualization=nfatablevisualization&format=frames", closure);
        Assert.Equal("{\"values\":[[\"0\",\"—\",\"—\",\"1\",\"no\"]]}", Readable(JsonDocument.Parse(search).RootElement.GetProperty("payload").GetProperty("blank")));
    }

    // A symbol outside the alphabet is a missing move like any other: the DFA's row reading it goes into Garbage, as the
    // Automaton picture falls into Garbage there.
    [Fact]
    public async Task Frames_Dfa_ASymbolNotInTheAlphabet_IsARowIntoGarbage() {
        var frames = await Frames("visualization=dfatablevisualization&format=frames",
            "(({1,2,3},{a,b},{(1,a,2),(1,b,3),(2,a,2),(2,b,2),(3,a,2),(3,b,3)},1,{2}),abc)");
        Assert.Equal(["Try", "Try", "Try", "Reject", "Done"], frames.Select(f => f.GetProperty("event").GetString()));
        var reject = frames[3];
        Assert.Equal("[\"3\",\"c\",\"2\",\"Garbage\",\"no\"]", Readable(reject.GetProperty("values")[3]));
        Assert.Equal(4, reject.GetProperty("values").GetArrayLength());
        Assert.Equal("r3:Rejected", RowStates(reject));
        Assert.Equal(Readable(reject.GetProperty("values")), Readable(frames[^1].GetProperty("values")));
        Assert.Equal("r3:Rejected", RowStates(frames[^1]));
        Assert.False(frames[^1].GetProperty("ok").GetBoolean());
    }

    // An NFA has no Garbage: every branch dies at the symbol, as at any missing move. The backtracking search's branch
    // ends in a dead end; subset simulation's set empties into ∅.
    [Fact]
    public async Task Frames_Nfa_ASymbolNotInTheAlphabet_KillsEveryBranch() {
        const string instance = "(({1,2},{a,b},{(1,a,2)},1,{2}),ac)";
        var search = await Frames("visualization=nfatablevisualization&format=frames", instance);
        Assert.Equal(["Try", "Try", "Reject", "Backtrack", "Done"], search.Select(f => f.GetProperty("event").GetString()));
        Assert.Equal("[[\"0\",\"—\",\"—\",\"1\",\"no\"],[\"1\",\"a\",\"1\",\"2\",\"yes\"]]", Readable(search[2].GetProperty("values")));
        Assert.Equal("r1:Rejected", RowStates(search[2]));
        Assert.Equal(Readable(search[2].GetProperty("values")), Readable(search[^1].GetProperty("values")));
        Assert.Equal("r1:Rejected", RowStates(search[^1]));
        Assert.False(search[^1].GetProperty("ok").GetBoolean());

        var subset = await Frames("visualization=nfatablevisualization&format=frames&solver=nfasubsetsolver", instance);
        Assert.Equal(["Try", "Try", "Reject", "Done"], subset.Select(f => f.GetProperty("event").GetString()));
        Assert.Equal("[\"2\",\"c\",\"2\",\"∅\",\"no\"]", Readable(subset[^1].GetProperty("values")[2]));
        Assert.Equal("r2:Rejected", RowStates(subset[^1]));
        Assert.False(subset[^1].GetProperty("ok").GetBoolean());
    }
}

// A stopped run, under the 0.05 s budget of TinyBudgetAppFactory.
[Collection(TimingCollection.Name)]
public class TableAutomatonFrames_StoppedRun_Endpoint_Tests : IClassFixture<TinyBudgetAppFactory> {
    private readonly HttpClient _client;

    public TableAutomatonFrames_StoppedRun_Endpoint_Tests(TinyBudgetAppFactory factory) {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Frames_StoppedRun_EndsWithTheStartRow_NotOk() {
        var sw = Stopwatch.StartNew();
        var content = new StringContent(JsonSerializer.Serialize(StepTimeLimit_Tests.SlowNfa), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/ProblemProvider/visualize?visualization=nfatablevisualization&format=frames", content, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(20), $"took {sw.Elapsed}");

        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal("Table", root.GetProperty("type").GetString());
        var frames = root.GetProperty("frames").EnumerateArray().ToList();
        Assert.True(frames.Count >= 2);
        var done = frames[^1];
        Assert.Equal("Done", done.GetProperty("event").GetString());
        Assert.False(done.GetProperty("ok").GetBoolean());
        Assert.Matches(@"^Stopped after 0\.05 s and [\d,]+ steps?; the solver had not finished\.", done.GetProperty("caption").GetString());
        // The start row alone, nothing marked: the same as the payload's blank.
        Assert.Equal(1, done.GetProperty("values").GetArrayLength());
        Assert.Equal("0", done.GetProperty("values")[0][0].GetString());
        Assert.Equal(root.GetProperty("payload").GetProperty("blank").GetProperty("values").GetRawText(), done.GetProperty("values").GetRawText());
        Assert.Empty(done.GetProperty("rows").EnumerateObject());
        Assert.Single(frames, f => f.GetProperty("event").GetString() == "Done");
    }
}
