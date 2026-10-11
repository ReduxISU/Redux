using System.Text.Json;
using API.Interfaces;
using API.Interfaces.CodeTree;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Code Tree picture's base on its own: payload, ids and labels, the states of each frame, the order of the trees left,
// codes and bits, the answer-only Done frame, a stopped run and a capped one. The solver's steps and the endpoint are tested
// in CodeTreeSolvers_Tests and CodeTreeFrames_Endpoint_Tests.
public class CodeTreeFrames_Tests {
    // a 3, b 1, c 1 (ids c1, c2, c3): merge b and c into n1 (2), then n1 and a into n2 (5).
    private static readonly CodeTreeSpec Abc = new("abaca");

    private static readonly HuffmanMerge N1 = new("n1", "c2", "c3", 2);
    private static readonly HuffmanMerge N2 = new("n2", "n1", "c1", 5);

    private static string Json(object value) => JsonSerializer.Serialize(value, FramesResponse.JsonOptions);

    private static string States(IReadOnlyDictionary<string, StateRef> map) => string.Join(" ", map.Select(p => $"{p.Key}:{p.Value.State}"));

    private static HuffmanForest F(HuffmanMerge[] merges, params (string Id, string Code)[] codes) =>
        new(merges, codes.Select(c => new KeyValuePair<string, string>(c.Id, c.Code)));

    // ---- Ids, labels and payload ----

    [Fact]
    public void Ids_ArePositions() {
        Assert.Equal(("c1", "c12", "n1", "n3.0", "n3.1"), (CodeTreeFrames.LeafId(0), CodeTreeFrames.LeafId(11), CodeTreeFrames.NodeId(1), CodeTreeFrames.EdgeId("n3", '0'), CodeTreeFrames.EdgeId("n3", '1')));
    }

    [Theory]
    [InlineData(' ', "␣")]
    [InlineData('\n', "↵")]
    [InlineData('\t', "⇥")]
    [InlineData('\r', "U+000D")]
    [InlineData('\uD83D', "U+D83D")]
    [InlineData('é', "é")]
    [InlineData('-', "-")]
    public void Label_IsSafeToDraw(char c, string label) => Assert.Equal(label, CodeTreeFrames.Label(c));

    [Fact]
    public void Payload_Json_HasTheTextAndItsCharactersInCharacterOrder() {
        Assert.Equal(
            "{\"kind\":\"codeTree\",\"text\":\"abaca\",\"symbols\":[{\"id\":\"c1\",\"symbol\":\"a\",\"label\":\"a\",\"count\":3},"
            + "{\"id\":\"c2\",\"symbol\":\"b\",\"label\":\"b\",\"count\":1},{\"id\":\"c3\",\"symbol\":\"c\",\"label\":\"c\",\"count\":1}]}",
            JsonSerializer.Serialize(JsonDocument.Parse(Json(CodeTreeFrames.Payload(Abc))).RootElement));
        var spaced = CodeTreeFrames.Payload(new CodeTreeSpec("b a\n"));
        Assert.Equal(["c1 ↵ 1", "c2 ␣ 1", "c3 a 1", "c4 b 1"], spaced.Symbols.Select(s => $"{s.Id} {s.Label} {s.Count}"));
        Assert.Empty(CodeTreeFrames.Payload(new CodeTreeSpec("")).Symbols);
    }

    // ---- Frames ----

    [Fact]
    public void Frame_Json_HasTheContractFieldsInOrder_BitsOnlyWithCodes_AndOkOnlyOnDone() {
        using var doc = JsonDocument.Parse(Json(CodeTreeFrames.Frame(Abc, F([N1]), StepEvent.Try, ["c2", "c3", "n1"], "c", incremental: true)));
        Assert.Equal(["event", "caption", "focus", "merged", "roots", "codes", "nodes", "edges"], doc.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal("[{\"id\":\"n1\",\"weight\":2,\"left\":\"c2\",\"right\":\"c3\"}]", JsonSerializer.Serialize(doc.RootElement.GetProperty("merged")));
        var done = CodeTreeFrames.Frame(Abc, F([N1, N2], ("c1", "1"), ("c2", "00"), ("c3", "01")), StepEvent.Done, [], "c", ok: true, incremental: true);
        using var doneDoc = JsonDocument.Parse(Json(done));
        Assert.Equal(["event", "caption", "focus", "merged", "roots", "codes", "nodes", "edges", "bits", "ok"], doneDoc.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal(7, done.Bits);   // a 3 x 1 bit, b and c 2 bits each
    }

    [Fact]
    public void Frame_AMerge_HighlightsTheTwoTreesAndTheirParent_AndKeepsEarlierMergesSolution() {
        var first = CodeTreeFrames.Frame(Abc, F([N1]), StepEvent.Try, ["c2", "c3", "n1"], "c", incremental: true);
        Assert.Equal("c2:ElementHighlight c3:ElementHighlight n1:ElementHighlight", States(first.Nodes));
        Assert.Equal("n1.0:ElementHighlight n1.1:ElementHighlight", States(first.Edges));
        Assert.Equal(["n1", "c1"], first.Roots);   // n1 (2) is lighter than a (3)
        var second = CodeTreeFrames.Frame(Abc, F([N1, N2]), StepEvent.Try, ["n1", "c1", "n2"], "c", incremental: true);
        Assert.Equal("c1:ElementHighlight c2:Solution c3:Solution n1:ElementHighlight n2:ElementHighlight", States(second.Nodes));
        Assert.Equal("n1.0:Solution n1.1:Solution n2.0:ElementHighlight n2.1:ElementHighlight", States(second.Edges));
        Assert.Equal(["n2"], second.Roots);
        Assert.Null(second.Bits);
        Assert.Empty(second.Codes);
    }

    [Fact]
    public void Frame_BeforeAnyMerge_IsEveryCharacterAlone_LightestFirst() {
        var start = CodeTreeFrames.Frame(Abc, F([]), StepEvent.Try, [], "c", incremental: true);
        Assert.Empty(start.Nodes);
        Assert.Empty(start.Edges);
        // b and c weigh 1 (b first: the earlier character), a weighs 3.
        Assert.Equal(["c2", "c3", "c1"], start.Roots);
    }

    [Fact]
    public void Frame_WithCodes_IsTheWholeTreeSolution_WithTheEncodedLength() {
        var codes = F([N1, N2], ("c1", "1"), ("c2", "00"), ("c3", "01"));
        var accepted = CodeTreeFrames.Frame(Abc, codes, StepEvent.Accept, [], "c", incremental: true);
        Assert.Equal("c1:Solution c2:Solution c3:Solution n1:Solution n2:Solution", States(accepted.Nodes));
        Assert.Equal(["c1=1", "c2=00", "c3=01"], accepted.Codes.Select(p => $"{p.Key}={p.Value}"));
        Assert.Equal(7, accepted.Bits);
        // A one-character text has no merge, only its leaf and its code.
        var one = CodeTreeFrames.Frame(new CodeTreeSpec("aaa"), F([], ("c1", "0")), StepEvent.Done, [], "c", ok: true, incremental: true);
        Assert.Equal("c1:Solution", States(one.Nodes));
        Assert.Equal(3, one.Bits);
    }

    [Fact]
    public void Frame_WholeCandidate_IsOnlyHighlightedWhileTried_AndAWrongAnswerIsRejected() {
        var tree = F([N1, N2]);
        Assert.Equal("c1:ElementHighlight c2:ElementHighlight c3:ElementHighlight n1:ElementHighlight n2:ElementHighlight",
            States(CodeTreeFrames.Frame(Abc, tree, StepEvent.Try, [], "c").Nodes));
        Assert.Equal("c1:Rejected c2:Rejected c3:Rejected n1:Rejected n2:Rejected", States(CodeTreeFrames.Frame(Abc, tree, StepEvent.Done, [], "c", ok: false).Nodes));
        Assert.Equal("Rejected", CodeTreeFrames.Frame(Abc, F([N1]), StepEvent.Reject, ["n1"], "c", incremental: true).Nodes["n1"].State.ToString());
    }

    [Fact]
    public void Frame_MergesThatDoNotFitTheText_AreIgnored() {
        // An unknown child, a child used twice, a node made twice: only n1 is drawn.
        var frame = CodeTreeFrames.Frame(Abc, F([N1, new("n2", "c9", "c1", 4), new("n3", "c2", "c1", 4), new("n1", "c1", "c1", 6)],
            ("c1", "1"), ("c7", "0")), StepEvent.Try, ["n3", "x"], "c", incremental: true);
        Assert.Equal(["n1"], frame.Merged.Select(m => m.Id));
        Assert.Equal(["c1=1"], frame.Codes.Select(p => $"{p.Key}={p.Value}"));
        Assert.Null(frame.Bits);   // b and c have no code
    }

    // ---- Answers ----

    [Theory]
    [InlineData("({(97,1),(98,00),(99,01)},1001101)", "c1=1 c2=00 c3=01")]
    [InlineData("()", "")]
    [InlineData("({(97,1),(120,0)},10)", "")]   // x is not in the text
    [InlineData("nonsense", "")]
    public void ParseCodes_ReadsTheCodeTable(string answer, string codes) =>
        Assert.Equal(codes, string.Join(" ", CodeTreeFrames.ParseCodes(Abc, answer).Codes.Select(p => $"{p.Key}={p.Value}")));

    [Fact]
    public void ParseCodes_ReadsCharactersThatAreSeparators() {
        var spec = new CodeTreeSpec("(,)");
        Assert.Equal("c1=10 c2=11 c3=0", string.Join(" ", CodeTreeFrames.ParseCodes(spec, "({(40,10),(41,11),(44,0)},10011)").Codes.Select(p => $"{p.Key}={p.Value}")));
    }

    // ---- Build ----

    private static SolveRun Run(bool timedOut, params SolverStep<HuffmanForest>[] steps) =>
        new(timedOut ? SolveRun.TimedOutAnswer : "({(97,1),(98,00),(99,01)},1001101)", steps, typeof(HuffmanForest)) { Incremental = true, TimedOut = timedOut };

    [Fact]
    public void Build_AFramePerStep_EndingWithTheDoneStep() {
        var answer = F([N1, N2], ("c1", "1"), ("c2", "00"), ("c3", "01"));
        var run = Run(false,
            new SolverStep<HuffmanForest>(F([N1]), StepEvent.Try, ["c2", "c3", "n1"], "Merge b (1) and c (1) into a node of weight 2."),
            new SolverStep<HuffmanForest>(answer, StepEvent.Done, [], "Encoded.") { Ok = true });
        var response = CodeTreeFrames.Build(Abc, run, _ => throw new InvalidOperationException("not read"), _ => throw new InvalidOperationException("not read"));
        Assert.Equal("CodeTree", response.Type);
        var frames = response.Frames.Cast<CodeTreeFrame>().ToList();
        Assert.Equal([StepEvent.Try, StepEvent.Done], frames.Select(f => f.Event));
        Assert.Equal(7, frames[1].Bits);
        Assert.True(frames[1].Ok);
    }

    [Fact]
    public void Build_WithoutDrawableSteps_AddsTheCodes_OrABlankFrame() {
        var legacy = new SolveRun("({(97,1),(98,00),(99,01)},1001101)", ["legacy"], null);
        var done = (CodeTreeFrame)Assert.Single(CodeTreeFrames.Build(Abc, legacy, a => CodeTreeFrames.ParseCodes(Abc, a), _ => true).Frames);
        Assert.Equal((StepEvent.Done, true, "The answer is ({(97,1),(98,00),(99,01)},1001101)."), (done.Event, done.Ok, done.Caption));
        Assert.Equal(3, done.Codes.Count);
        Assert.Empty(done.Merged);
        var blank = (CodeTreeFrame)Assert.Single(CodeTreeFrames.Build(Abc, new SolveRun("x", [], null), _ => throw new InvalidOperationException(), _ => false).Frames);
        Assert.Equal(("No answer was found.", false), (blank.Caption, blank.Ok));
        Assert.Empty(blank.Nodes);
        Assert.Empty(blank.Codes);
    }

    [Fact]
    public void Build_AStoppedRun_EndsBlank_NotOk_AndNeverReadsItsAnswer() {
        var run = Run(true,
            new SolverStep<HuffmanForest>(F([N1]), StepEvent.Try, ["c2", "c3", "n1"], "Merge."),
            new SolverStep<HuffmanForest>(F([]), StepEvent.Done, [], "Stopped after 0.05 s and 1 step; the solver had not finished.") { Ok = false });
        var frames = CodeTreeFrames.Build(Abc, run, _ => throw new InvalidOperationException("read"), _ => throw new InvalidOperationException("read"))
            .Frames.Cast<CodeTreeFrame>().ToList();
        Assert.Equal(2, frames.Count);
        var done = frames[^1];
        Assert.Equal((StepEvent.Done, false), (done.Event, done.Ok));
        Assert.StartsWith("Stopped after", done.Caption);
        Assert.Empty(done.Merged);
        Assert.Empty(done.Roots);
        Assert.Empty(done.Codes);
        Assert.Empty(done.Nodes);
        Assert.Empty(done.Edges);
        Assert.Null(done.Bits);

        var other = new SolveRun(SolveRun.TimedOutAnswer, [], typeof(NodeSet)) { TimedOut = true };
        var added = (CodeTreeFrame)Assert.Single(CodeTreeFrames.Build(Abc, other, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException()).Frames);
        Assert.Equal(GraphSubsetFrames.StoppedCaption, added.Caption);
        Assert.Equal(false, added.Ok);
    }

    [Fact]
    public void Build_ACappedRun_KeepsTheFirstMerges_ThenTheCodes() {
        var rec = new StepRecorder<HuffmanForest>();
        for (int i = 0; i < 400; i++) {
            int k = i;
            rec.Try(() => F([N1]), () => $"Merge {k}.", () => ["n1"]);
        }
        var answer = F([N1, N2], ("c1", "1"), ("c2", "00"), ("c3", "01"));
        rec.Accept(() => answer, () => "Read each code.", () => []);
        rec.Done(answer, true, "Encoded.");
        var run = new SolveRun("({(97,1),(98,00),(99,01)},1001101)", rec.Steps.Cast<object>().ToList(), typeof(HuffmanForest)) { Incremental = true };
        var frames = CodeTreeFrames.Build(Abc, run, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException())
            .Frames.Cast<CodeTreeFrame>().ToList();
        Assert.Equal(StepRecorder<HuffmanForest>.DefaultCap + 2, frames.Count);
        Assert.Equal("Merge 149.", frames[^3].Caption);
        Assert.Equal(StepEvent.Accept, frames[^2].Event);
        Assert.Equal("Encoded. (250 not shown)", frames[^1].Caption);
        Assert.Equal(7, frames[^1].Bits);
    }

    // ---- The shape ----

    [Fact]
    public void HuffmanForest_HasValueEquality_AndAnEmptyAnswer() {
        Assert.Equal(F([N1], ("c1", "1"), ("c2", "0")), F([N1], ("c2", "0"), ("c1", "1")));
        Assert.NotEqual(F([N1]), F([N1, N2]));
        Assert.NotEqual(F([N1], ("c1", "1")), F([N1], ("c1", "0")));
        Assert.Equal(F([]), Shapes.EmptyAnswer(typeof(HuffmanForest)));
        Assert.Equal("{\"merges\":[{\"node\":\"n1\",\"left\":\"c2\",\"right\":\"c3\",\"weight\":2}],\"codes\":{\"c1\":\"1\"}}", JsonSerializer.Serialize(F([N1], ("c1", "1"))));
    }
}
