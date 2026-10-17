using System.Diagnostics;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.CodeTree;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_LOSSLESSDATACOMPRESSION;
using API.Problems.NPComplete.NPC_LOSSLESSDATACOMPRESSION.Visualizations;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Lossless Data Compression solver (Huffman) records steps for the Code Tree picture. Recording must never change an
// answer. Endpoints/Snapshots/codetree_solver_answers.tsv holds what it returned (solver, instance and answer as JSON strings;
// tab separated) BEFORE the picture existed, on its default instance, hand-picked edge cases and seeded random texts.
public class CodeTreeSolvers_Tests {
    private static string SnapshotPath(string file) =>
        Path.Combine(AppContext.BaseDirectory, "Endpoints", "Snapshots", file);

    internal static IEnumerable<(string Solver, string Instance, string Answer)> Rows() =>
        File.ReadAllLines(SnapshotPath("codetree_solver_answers.tsv")).Where(l => l.Length > 0).Select(l => {
            var parts = l.Split('\t');
            return (parts[0], JsonSerializer.Deserialize<string>(parts[1])!, JsonSerializer.Deserialize<string>(parts[2])!);
        });

    public static TheoryData<string, string, string> RecordedAnswers {
        get {
            var data = new TheoryData<string, string, string>();
            foreach (var (solver, instance, answer) in Rows()) data.Add(solver, instance, answer);
            return data;
        }
    }

    internal const string Solver = "losslessdatacompressionsolver";
    internal const string Visualization = "losslessdatacompressionvisualization";
    internal const string Default = "this is an example of lossless data compression using huffman encoding";

    private static SolveRun Run(string instance) =>
        ((ISolver)Activator.CreateInstance(ProblemProvider.Solvers[Solver])!).Run(instance, withSteps: true);

    private static List<SolverStep<HuffmanForest>> Steps(string instance) => Run(instance).Steps.Cast<SolverStep<HuffmanForest>>().ToList();

    private static List<CodeTreeFrame> Frames(string instance) {
        var vis = (IFramesVisualization)Activator.CreateInstance(ProblemProvider.Visualizers[Visualization])!;
        var response = vis.BuildFrames(instance, Run(instance));
        Assert.Equal("CodeTree", response.Type);
        return response.Frames.Cast<CodeTreeFrame>().ToList();
    }

    [Theory]
    [MemberData(nameof(RecordedAnswers))]
    public void Solve_ReturnsWhatItReturnedBeforeThePicture_AndItsStepsFitThePicture(string solverName, string instance, string answer) {
        var solver = (ISolver)Activator.CreateInstance(ProblemProvider.Solvers[solverName])!;
        var on = solver.Run(instance, withSteps: true);
        var off = solver.Run(instance, withSteps: false);
        Assert.Equal(answer, on.Answer);
        Assert.Equal(answer, off.Answer);
        Assert.Equal(answer, solver.solve(instance));
        Assert.Empty(off.Steps);
        Assert.False(on.TimedOut);
        Assert.Equal(IncrementalSolvers_Tests.Classification[solverName], on.Incremental);

        var problem = new LOSSLESSDATACOMPRESSION(instance);
        var spec = LosslessDataCompressionVisualization.Spec(problem);
        var symbols = CodeTreeFrames.Symbols(spec);
        var steps = on.Steps.Cast<SolverStep<HuffmanForest>>().ToList();
        Assert.Equal(StepEvent.Done, steps[^1].Event);
        Assert.Single(steps, s => s.Event == StepEvent.Done);
        Assert.All(steps, s => Assert.False(string.IsNullOrWhiteSpace(s.Caption)));
        // Huffman always has an answer, and the verifier accepts it.
        Assert.True(problem.defaultVerifier.verify(problem, answer));
        Assert.True(steps[^1].Ok);
        if (instance.Length == 0) {
            Assert.Single(steps);
            return;
        }

        // The count, a Try per merge (k - 1 of them), the Accept with the codes, and Done; past the cap, the first 150 of them,
        // the Accept and Done.
        int k = symbols.Count;
        int shown = Math.Min(k, StepRecorder<HuffmanForest>.DefaultCap);
        Assert.Equal([.. Enumerable.Repeat(StepEvent.Try, shown), StepEvent.Accept, StepEvent.Done], steps.Select(s => s.Event));
        var final = steps[^1].Partial;
        Assert.Equal(CodeTreeFrames.ParseCodes(spec, answer).Codes, final.Codes);
        Assert.Equal(k - 1, final.Merges.Length);
        // Each merge joins the two lightest trees left, as the frames list them, into the next node.
        for (int m = 0; m < shown - 1; m++) {
            var step = steps[1 + m];
            var merge = final.Merges[m];
            Assert.Equal(final.Merges.Take(m + 1), step.Partial.Merges);
            Assert.Empty(step.Partial.Codes);
            Assert.Equal([merge.Left, merge.Right, merge.Node], step.Focus);
            Assert.Equal(CodeTreeFrames.NodeId(m + 1), merge.Node);
            Assert.Equal(CodeTreeFrames.Roots(spec, final.Merges.Take(m).ToList()).Take(2), [merge.Left, merge.Right]);
        }
        // A character's code is its path from the root: left is 0, right is 1.
        var parent = final.Merges.SelectMany(x => new[] { (x.Left, x.Node, '0'), (x.Right, x.Node, '1') }).ToDictionary(e => e.Item1, e => (e.Node, Bit: e.Item3));
        foreach (var (leaf, code) in final.Codes) {
            string path = "";
            for (string v = leaf; parent.TryGetValue(v, out var up); v = up.Node) path = up.Bit + path;
            Assert.Equal(k == 1 ? "0" : path, code);
        }

        var frames = Frames(instance);
        Assert.Equal(steps.Count, frames.Count);
        Assert.True(frames[^1].Ok);
        Assert.Equal(answer[(answer.LastIndexOf(',') + 1)..^1].Length, frames[^1].Bits);
    }

    [Fact]
    public void Default_MergesNineteenCharactersInEighteenSteps() {
        var frames = Frames(Default);
        Assert.Equal(21, frames.Count);
        Assert.Equal("Count each character: 70 characters, 19 different. Each starts as its own one-node tree, weighted by how often it appears.", frames[0].Caption);
        Assert.Equal("Merge r (1) and x (1), the two lightest trees left, into a node of weight 2.", frames[1].Caption);
        Assert.Equal("Merge c (2) and d (2), the two lightest trees left, into a node of weight 4. (A tie: the tree holding the earliest character goes first.)", frames[2].Caption);
        Assert.Equal("Merge p (2) and the tree of weight 2 (r and x), the two lightest trees left, into a node of weight 4. (A tie: the tree holding the earliest character goes first.)", frames[4].Caption);
        Assert.Equal("Merge s (9) and ␣ (10), the two lightest trees left, into a node of weight 19. (A tie: the tree holding the earliest character goes first.)", frames[14].Caption);
        Assert.Equal("Merge the tree of weight 30 (12 characters) and the tree of weight 40 (7 characters), the two lightest trees left, into a node of weight 70.", frames[18].Caption);
        Assert.Equal(StepEvent.Accept, frames[19].Event);
        Assert.Equal("Encoded: 279 bits, versus 560 bits at 8 bits per character, 50% smaller. No code is the start of another, so the bits decode back to exactly the text.", frames[^1].Caption);
        Assert.Equal(279, frames[^1].Bits);
    }

    [Fact]
    public void TheIssuesExample_MergesEAndF() {
        // The classic table: a 45, b 13, c 12, d 16, e 9, f 5. The first merge is f (5) and e (9) into 14.
        string text = new string('a', 45) + new string('b', 13) + new string('c', 12) + new string('d', 16) + new string('e', 9) + new string('f', 5);
        var frames = Frames(text);
        Assert.Equal("Merge f (5) and e (9), the two lightest trees left, into a node of weight 14.", frames[1].Caption);
        Assert.Equal(["c6", "c5", "n1"], frames[1].Focus);
        Assert.Equal(224, frames[^1].Bits);
    }

    [Theory]
    [InlineData("", 1, "The text is empty, so there is nothing to encode.")]
    [InlineData("aaaa", 3, "Encoded: 4 bits, versus 32 bits at 8 bits per character, 88% smaller. No code is the start of another, so the bits decode back to exactly the text.")]
    public void TinyTexts_SayWhatTheCodeIs(string text, int count, string caption) {
        var frames = Frames(text);
        Assert.Equal(count, frames.Count);
        Assert.Equal(caption, frames[^1].Caption);
        Assert.True(frames[^1].Ok);
        if (text.Length > 0) {
            Assert.Equal("Count each character: 4 characters, all a. A single tree needs no merging.", frames[0].Caption);
            Assert.Equal("Only one character, so its code is just 0.", frames[1].Caption);
        }
    }

    [Fact]
    public void SpacesAndLineBreaks_AreCaptionedWithTheirLabels() {
        var frames = Frames("a a a\na");
        Assert.Equal("Merge ↵ (1) and ␣ (2), the two lightest trees left, into a node of weight 3.", frames[1].Caption);
    }

    [Fact]
    public void ALongRun_ShowsTheFirst150Steps_ThenTheCodes() {
        var (_, instance, answer) = Rows().Last();
        var frames = Frames(instance);
        Assert.Equal(StepRecorder<HuffmanForest>.DefaultCap + 2, frames.Count);
        Assert.Equal(StepEvent.Accept, frames[^2].Event);
        Assert.Matches(@"^Encoded: [\d,]+ bits, .* \(\d+ not shown\)\.$", frames[^1].Caption);
        Assert.Equal(170, frames[^1].Codes.Count);
    }

    // Wall-clock assertions run alone, after the parallel tests (#837).
    [Collection(TimingCollection.Name)]
    public class Timing {
        [Fact]
        public void ALongText_StaysWellWithinTheTimeBudget() {
            var rnd = new Random(676);
            string text = new string(Enumerable.Range(0, 200_000).Select(_ => (char)('a' + (int)Math.Sqrt(rnd.Next(26 * 26)))).ToArray());
            var sw = Stopwatch.StartNew();
            var frames = Frames(text);
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), $"took {sw.Elapsed}");
            Assert.True(frames[^1].Ok);
        }
    }
}
