using System.Diagnostics;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Steps;
using API.Interfaces.Tripartite;
using API.Problems.NPComplete.NPC_DM3;
using API.Problems.NPComplete.NPC_DM3.Visualizations;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The 3-Dimensional Matching brute force records steps for the Tripartite Matching picture. Recording must never change an
// answer. Endpoints/Snapshots/tripartite_solver_answers.tsv holds what it returned (solver, instance and answer as JSON
// strings; tab separated) BEFORE the picture existed (with the #745, #805 and #807 fixes in), on its default instance,
// hand-picked edge cases and seeded random instances.
public class TripartiteSolvers_Tests {
    private static string SnapshotPath(string file) =>
        Path.Combine(AppContext.BaseDirectory, "Endpoints", "Snapshots", file);

    internal static IEnumerable<(string Solver, string Instance, string Answer)> Rows() =>
        File.ReadAllLines(SnapshotPath("tripartite_solver_answers.tsv")).Where(l => l.Length > 0).Select(l => {
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

    internal const string Solver = "threedimensionalmatchingbruteforce";
    internal const string Visualization = "dm3visualization";
    internal const string Default = "{Paul,Sally,Dave}{Madison,Austin,Bob}{Chloe,Frank,Jake}{Paul,Madison,Chloe}{Paul,Austin,Jake}{Sally,Bob,Chloe}{Sally,Madison,Frank}{Dave,Austin,Chloe}{Dave,Bob,Chloe}";
    // Every way to pick 3 of these 5 triples repeats a name (the mockup's "No perfect matching").
    internal const string NoMatching = "{a,b,c}{d,e,f}{g,h,i}{a,d,g}{b,d,h}{c,e,i}{a,f,h}{b,e,g}";
    // 12 triples over 4 elements each: the matching is the 273rd of 495 sets tried.
    internal const string Twelve = "{x1,x2,x3,x4}{y1,y2,y3,y4}{z1,z2,z3,z4}{x1,y1,z1}{x1,y2,z2}{x2,y1,z2}{x2,y2,z1}{x3,y3,z3}{x3,y4,z4}{x4,y3,z4}{x4,y4,z4}{x1,y3,z3}{x2,y4,z3}{x3,y1,z1}{x4,y2,z2}";

    private static SolveRun Run(string instance) =>
        ((ISolver)Activator.CreateInstance(ProblemProvider.Solvers[Solver])!).Run(instance, withSteps: true);

    private static List<SolverStep<ChosenTriples>> Steps(string instance) => Run(instance).Steps.Cast<SolverStep<ChosenTriples>>().ToList();

    private static List<TripartiteFrame> Frames(string instance) {
        var vis = (IFramesVisualization)Activator.CreateInstance(ProblemProvider.Visualizers[Visualization])!;
        var response = vis.BuildFrames(instance, Run(instance));
        Assert.Equal("TripartiteMatching", response.Type);
        return response.Frames.Cast<TripartiteFrame>().ToList();
    }

    private static bool Verifies(string instance, string answer) {
        try { var p = new DM3(instance); return p.defaultVerifier.verify(p, answer); } catch { return false; }
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

        var problem = new DM3(instance);
        var spec = DM3Visualization.Spec(problem);
        bool verified = Verifies(instance, answer);
        var steps = on.Steps.Cast<SolverStep<ChosenTriples>>().ToList();
        Assert.Equal(StepEvent.Done, steps[^1].Event);
        Assert.Single(steps, s => s.Event == StepEvent.Done);
        Assert.All(steps, s => Assert.False(string.IsNullOrWhiteSpace(s.Caption)));
        Assert.Equal(verified, steps[^1].Ok);
        // The Done step's verdict is the verifier's, and its partial is the answer (or nothing).
        Assert.Equal(verified ? TripartiteFrames.ParseMatching(spec, answer) : new ChosenTriples([]), steps[^1].Partial);
        Assert.Equal(answer == "{}", !verified);

        // Every other step judges a whole set of |X| triples of M: a Reject per set the verifier refuses, an Accept for the answer.
        var tripleIds = Enumerable.Range(0, spec.Triples.Count).Select(TripartiteFrames.TripleId).ToHashSet();
        var elementIds = spec.X.Select((_, i) => TripartiteFrames.XId(i)).Concat(spec.Y.Select((_, i) => TripartiteFrames.YId(i)))
            .Concat(spec.Z.Select((_, i) => TripartiteFrames.ZId(i))).ToHashSet();
        foreach (var s in steps.SkipLast(1)) {
            Assert.True(s.Event is StepEvent.Reject or StepEvent.Accept);
            Assert.Equal(problem.X.Count, s.Partial.Triples.Length);
            Assert.All(s.Partial.Triples, id => Assert.Contains(id, tripleIds));
            // The focus is what repeats.
            var uses = TripartiteFrames.Uses(spec, s.Partial.Triples.Select(id => int.Parse(id[1..]) - 1));
            Assert.Equal(uses.Where(u => u.Value > 1).Select(u => u.Key).Order(), s.Focus.Order());
            Assert.All(s.Focus, id => Assert.Contains(id, elementIds));
        }
        Assert.Equal(verified ? 1 : 0, steps.Count(s => s.Event == StepEvent.Accept));
        if (verified) Assert.Equal(steps[^1].Partial, steps[^2].Partial);
        // Fewer triples than X: nothing to try.
        if (problem.M.Count < problem.X.Count) Assert.Single(steps);

        var frames = Frames(instance);
        Assert.Equal(steps.Count, frames.Count);
        Assert.Equal(verified, frames[^1].Ok);
    }

    [Fact]
    public void Default_TriesFifteenSets_AndFindsTheMockupsMatching() {
        var frames = Frames(Default);
        Assert.Equal(16, frames.Count);
        Assert.Equal("Try T1, T2 and T3: Paul and Chloe are used twice.", frames[0].Caption);
        Assert.Equal(["x1", "z1"], frames[0].Focus);
        Assert.Equal("Try T1, T3 and T5: Chloe is used 3 times.", frames[5].Caption);
        Assert.Equal(new Dictionary<string, int> { ["z1"] = 3 }, frames[5].Repeats);
        Assert.Equal("Try T1, T2 and T5: Paul, Austin and Chloe are used twice.", frames[2].Caption);
        Assert.Equal((StepEvent.Accept, "Try T2, T4 and T6: every element of X, Y and Z is used exactly once, so the verifier accepts it."), (frames[14].Event, frames[14].Caption));
        var done = frames[^1];
        Assert.Equal("T2 (Paul, Austin, Jake), T4 (Sally, Madison, Frank) and T6 (Dave, Bob, Chloe) use every element of X, Y and Z exactly once. Found after 15 tries.", done.Caption);
        Assert.Equal(["T2", "T4", "T6"], done.Chosen);
        Assert.True(done.Ok);
    }

    [Fact]
    public void NoMatching_ChecksEverySet_AndEndsWithNothingChosen() {
        var frames = Frames(NoMatching);
        Assert.Equal(11, frames.Count);
        Assert.Equal("Try T1, T2 and T3: d is used twice.", frames[0].Caption);
        Assert.Equal("Try T1, T2 and T4: a, d and h are used twice.", frames[1].Caption);
        Assert.Equal("Checked all 10 ways to pick 3 of the 5 triples. The verifier rejects every one, so there is no perfect matching.", frames[^1].Caption);
        Assert.False(frames[^1].Ok);
        Assert.Empty(frames[^1].Chosen);
    }

    [Theory]
    [InlineData("{a}{b}{c}", "M has no triples, so nothing can match the 1 element of X.")]
    [InlineData("{a,b}{c,d}{e,f}{a,c,e}", "M has only 1 triple, but a matching needs 2, one for each element of X. There is nothing to try.")]
    public void FewerTriplesThanX_IsOneDoneStep(string instance, string caption) {
        var step = Assert.Single(Steps(instance));
        Assert.Equal((StepEvent.Done, false, caption), (step.Event, step.Ok, step.Caption));
        Assert.Empty(step.Partial.Triples);
    }

    [Fact]
    public void ANameInMoreThanOneSet_IsNamedWithItsSet() {
        // 1 is in X, Y and Z; T1 and T2 both use 1 in X.
        var frames = Frames("{1,2}{1,2}{1,2}{1,1,1}{1,2,2}{2,2,2}");
        Assert.Equal("Try T1 and T2: 1 in X is used twice.", frames[0].Caption);
        Assert.Equal(["x1"], frames[0].Focus);
    }

    [Fact]
    public void ManyRepeatedNames_AreShortened() {
        Assert.Equal("Try T1, T2, T3 and T4: x1, x2, y1, y2 and 2 more are used twice.", Frames(Twelve)[0].Caption);
    }

    // What the verifier says about a set that repeats no name depends on the verifier: today's accepts a set that leaves
    // elements of Y or Z unused (#710) and refuses a name that is in more than one of X, Y and Z; the fix in PR #742 does the
    // opposite. The captions say what the verifier decided either way.
    [Theory]
    [InlineData("{a}{b,c}{d,e}{a,b,d}")]
    [InlineData("{1,2}{1,2}{1,2}{1,1,1}{2,2,2}")]
    public void ASetThatRepeatsNoName_IsCaptionedByWhatTheVerifierDecides(string instance) {
        var steps = Steps(instance);
        var first = steps[0];
        if (first.Event == StepEvent.Accept) {
            Assert.EndsWith(": no element is used twice, so the verifier accepts it.", first.Caption);
            Assert.Equal("The verifier accepts T1 (a, b, d), found on the first try, though c and e are never used.", steps[^1].Caption);
        } else if (instance.StartsWith("{a}")) {
            Assert.Equal("Try T1: no element is used twice, but c and e are never used, so the verifier rejects it.", first.Caption);
        } else {
            Assert.Equal("Try T1 and T2: no element is used twice, but the verifier rejects it.", first.Caption);
            Assert.Equal("There is only one way to pick the triples, and the verifier rejects it, so there is no perfect matching.", steps[^1].Caption);
        }
        Assert.Empty(first.Focus);
    }

    [Fact]
    public void ALongRun_ShowsTheFirst150Tries_ThenTheMatching() {
        var frames = Frames(Twelve);
        Assert.Equal(StepRecorder<ChosenTriples>.DefaultCap + 2, frames.Count);
        Assert.All(frames.Take(150), f => Assert.Equal(StepEvent.Reject, f.Event));
        Assert.Equal(StepEvent.Accept, frames[^2].Event);
        Assert.Equal(["T2", "T7", "T10", "T11"], frames[^2].Chosen);
        Assert.Equal("T2 (x1, y2, z2), T7 (x4, y3, z4), T10 (x2, y4, z3) and T11 (x3, y1, z1) use every element of X, Y and Z exactly once. Found after 273 tries. (122 not shown)",
            frames[^1].Caption);
    }

    // Wall-clock assertions run alone, after the parallel tests (#837).
    [Collection(TimingCollection.Name)]
    public class Timing {
        [Fact]
        public void ABiggerInstance_StaysWellWithinTheTimeBudget() {
            // Six elements per set and 18 triples with no matching among the first: C(18, 6) = 18,564 sets in the worst case.
            string x = "{a1,a2,a3,a4,a5,a6}{b1,b2,b3,b4,b5,b6}{c1,c2,c3,c4,c5,c6}";
            var triples = new List<string>();
            for (int i = 1; i <= 6; i++)
                for (int k = 0; k < 3; k++) triples.Add($"{{a{i},b{(i + k - 1) % 6 + 1},c{(i + 2 * k - 1) % 6 + 1}}}");
            var sw = Stopwatch.StartNew();
            var frames = Frames(x + string.Concat(triples));
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), $"took {sw.Elapsed}");
            Assert.True(frames.Count <= StepRecorder<ChosenTriples>.DefaultCap + 2);
        }
    }
}
