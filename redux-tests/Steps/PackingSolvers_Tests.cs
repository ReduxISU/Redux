using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Packing;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_BINPACKING;
using API.Problems.NPComplete.NPC_BINPACKING.Visualizations;
using API.Problems.NPComplete.NPC_KNAPSACK;
using API.Problems.NPComplete.NPC_KNAPSACK.Visualizations;
using API.Problems.NPComplete.NPC_PARTITION;
using API.Problems.NPComplete.NPC_PARTITION.Visualizations;
using API.Problems.NPComplete.NPC_SUBSETSUM;
using API.Problems.NPComplete.NPC_SUBSETSUM.Visualizations;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The packing solvers record steps for the Packing picture. Recording must never change an answer.
// Endpoints/Snapshots/packing_solver_answers.tsv holds what each returned (solver, instance, answer as a JSON string; tab
// separated) BEFORE the picture existed (with the #791-#796 fixes in), on its default instance, the mockup's examples,
// hand-picked edge cases and seeded random instances.
public class PackingSolvers_Tests {
    private static string SnapshotPath(string file) =>
        Path.Combine(AppContext.BaseDirectory, "Endpoints", "Snapshots", file);

    internal static IEnumerable<(string Solver, string Instance, string Answer)> Rows() =>
        File.ReadAllLines(SnapshotPath("packing_solver_answers.tsv")).Where(l => l.Length > 0).Select(l => {
            var parts = l.Split('\t');
            return (parts[0], parts[1], JsonSerializer.Deserialize<string>(parts[2])!);
        });

    public static TheoryData<string, string, string> RecordedAnswers {
        get {
            var data = new TheoryData<string, string, string>();
            foreach (var (solver, instance, answer) in Rows()) data.Add(solver, instance, answer);
            return data;
        }
    }

    // The visualization that draws each solver's steps.
    internal static readonly Dictionary<string, string> VisualizationOf = new() {
        ["partitionbruteforce"] = "partitiondefaultvisualization",
        ["subsetsumbruteforce"] = "subsetsumvisualization",
        ["fastapproximation"] = "subsetsumvisualization",
        ["knapsackbruteforce"] = "knapsackvisualization",
        ["knapsackdp"] = "knapsackvisualization",
        ["binpackingbruteforce"] = "binpackingvisualization",
        ["binpackingffd"] = "binpackingvisualization",
    };

    private static List<SolverStep<Placement>> Steps(string solver, string instance) =>
        ((ISolver)Activator.CreateInstance(ProblemProvider.Solvers[solver])!).Run(instance, withSteps: true).Steps.Cast<SolverStep<Placement>>().ToList();

    // The sizes in each container, sorted: placements are compared by what each container holds, since equal numbers are
    // interchangeable in an answer string.
    private static List<string> Contents(PackingPayload payload, Placement placement) =>
        Enumerable.Range(0, payload.Containers.Count)
            .Select(c => string.Join(",", payload.Items.Where((_, i) => placement.Of(i) == c).Select(i => i.Label).Order(StringComparer.Ordinal)))
            .ToList();

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
        Assert.Equal(IncrementalSolvers_Tests.Classification[solverName], on.Incremental);

        var steps = on.Steps.Cast<SolverStep<Placement>>().ToList();
        Assert.Equal(StepEvent.Done, steps[^1].Event);
        Assert.Single(steps, s => s.Event == StepEvent.Done);
        Assert.All(steps, s => Assert.False(string.IsNullOrWhiteSpace(s.Caption)));
        // A whole-candidate solver accepts at most once: the candidate that works.
        if (!on.Incremental) Assert.True(steps.Count(s => s.Event == StepEvent.Accept) <= 1);

        var vis = (IFramesVisualization)Activator.CreateInstance(ProblemProvider.Visualizers[VisualizationOf[solverName]])!;
        var response = vis.BuildFrames(instance, on);
        var payload = Assert.IsType<PackingPayload>(response.Payload);
        var ids = payload.Items.Select(i => i.Id).Concat(payload.Containers.Select(c => c.Id)).ToHashSet();
        foreach (var step in steps) {
            // Every id a step names is an id of the picture, and every placed item is in a container the picture has.
            Assert.All(step.Focus, id => Assert.Contains(id, ids));
            Assert.True(step.Partial.Assign.Length <= payload.Items.Count);
            Assert.All(step.Partial.Assign, c => Assert.InRange(c, -1, payload.Containers.Count - 1));
        }

        // The Done step holds the answer, and its verdict is the verifier's.
        var problem = (IProblem)Activator.CreateInstance(ProblemProvider.Problems[payload.Rule.ToLowerInvariant()], [instance])!;
        bool verified;
        try { verified = problem.defaultVerifier.verify(instance, answer); } catch { verified = false; }
        Assert.Equal(verified, steps[^1].Ok);
        var hook = typeof(IPackingVisualization<>).MakeGenericType(problem.GetType());
        var expected = (Placement)hook.GetMethod("ParseAnswer")!.Invoke(vis, [problem, answer])!;
        Assert.Equal(Contents(payload, expected), Contents(payload, steps[^1].Partial));

        // One frame per step, the Done frame's verdict the verifier's.
        var frames = response.Frames.Cast<PackingFrame>().ToList();
        Assert.Equal(steps.Count, frames.Count);
        Assert.Equal(verified, frames[^1].Ok);
    }

    [Fact]
    public void Partition_TriesSplitsInTheCountersOrder_AndAcceptsTheOneThatBalances() {
        var steps = Steps("partitionbruteforce", "{1,2,3}");
        Assert.Equal(
            ["Try S1 = {2}: 2 against 4, off by 2.",
             "Try S1 = {1,2}: 3 against 3. Equal.",
             "Both sides add up to 3, found after 2 splits."],
            steps.Select(s => s.Caption));
        Assert.Equal([StepEvent.Reject, StepEvent.Accept, StepEvent.Done], steps.Select(s => s.Event));
        Assert.Equal(["I1", "I2"], steps[1].Focus);
        Assert.Equal([0, 0, 1], steps[^1].Partial.Assign);
    }

    [Fact]
    public void Partition_OddTotal_SaysNoSplitCanBalance() {
        var steps = Steps("partitionbruteforce", "{3,5,7}");
        Assert.Equal(9, steps.Count);
        Assert.Equal("The total is odd (15), so no split can balance. Checked all 8 splits.", steps[^1].Caption);
        Assert.Equal(new Placement([]), steps[^1].Partial);
        Assert.False(steps[^1].Ok);
    }

    [Fact]
    public void SubsetSum_BruteForce_TriesSubsetsInMaskOrder() {
        var steps = Steps("subsetsumbruteforce", "({1,7,12,15},28)");
        Assert.Equal("Try {1}: 1, not 28.", steps[0].Caption);
        Assert.Equal("Try {1,7}: 1 + 7 = 8, not 28.", steps[2].Caption);
        Assert.Equal("Try {1,12,15}: 1 + 12 + 15 = 28. That's T.", steps[^2].Caption);
        Assert.Equal("{1,12,15} adds up to exactly 28, found after 13 subsets.", steps[^1].Caption);
        Assert.Equal(["I1", "I3", "I4"], steps[^2].Focus);
        Assert.Equal([0, -1, 0, 0], steps[^1].Partial.Assign);
    }

    [Fact]
    public void SubsetSum_TargetZero_EndsWithTheEmptySetAsTheAnswer() {
        var steps = Steps("subsetsumbruteforce", "({1,2},0)");
        Assert.True(steps[^1].Ok);
        Assert.Equal(new Placement([]), steps[^1].Partial);
        Assert.Equal("Checked all 3 subsets that hold something. None adds up to 0, but the empty set does, so it is the answer.", steps[^1].Caption);
        var approx = Steps("fastapproximation", "({1,2},0)");
        Assert.Equal(("T is 0, so the empty set is the answer.", true), (Assert.Single(approx).Caption, approx[0].Ok));
    }

    [Fact]
    public void FastApproximation_ShowsTheBestSumKeptAfterEachNumber() {
        var steps = Steps("fastapproximation", "({1,7,12,15},28)");
        Assert.Equal(
            ["Add 1 to every sum kept so far that stays within 28. 2 sums kept. Best so far: 1.",
             "Add 7 to every sum kept so far that stays within 28. 4 sums kept. Best so far: 8.",
             "Add 12 to every sum kept so far that stays within 28. 8 sums kept. Best so far: 20.",
             "Add 15 to every sum kept so far that stays within 28. 14 sums kept. Best so far: 28.",
             "The best sum kept is exactly 28: {1,12,15}."],
            steps.Select(s => s.Caption));
        Assert.Equal(["I3"], steps[2].Focus);
        Assert.Equal([0, 0, 0], steps[2].Partial.Assign);
        Assert.Equal([0, -1, 0, 0], steps[3].Partial.Assign);
    }

    // Fast Approximation's steps keep the best sum so far, which the run goes on from: its numbers are Committed and Chosen
    // is never Rejected before the Done, which alone says whether T was hit (#898).
    [Theory]
    [InlineData("({1,7,12,15},28)", true)]
    [InlineData("({1000,1001,5},1006)", false)]
    public void FastApproximation_Frames_KeepTheBestSumSoFar_AndOnlyTheDoneJudgesIt(string instance, bool ok) {
        var run = ((ISolver)Activator.CreateInstance(ProblemProvider.Solvers["fastapproximation"])!).Run(instance, withSteps: true);
        Assert.True(run.Incremental);
        var vis = (IFramesVisualization)Activator.CreateInstance(ProblemProvider.Visualizers["subsetsumvisualization"])!;
        var frames = vis.BuildFrames(instance, run).Frames.Cast<PackingFrame>().ToList();
        var steps = run.Steps.Cast<SolverStep<Placement>>().ToList();
        // A frame lists only what is not Background.
        static PictureState Of(IReadOnlyDictionary<string, API.Interfaces.Graphs.StateRef> states, string id) => states.TryGetValue(id, out var s) ? s.State : PictureState.Background;
        for (int i = 0; i < frames.Count - 1; i++) {
            var frame = frames[i];
            Assert.Equal(StepEvent.Try, frame.Event);
            Assert.NotEqual(PictureState.Rejected, Of(frame.Containers, "C1"));
            // The best sum so far is kept (Committed); the number just added is the one in focus.
            var focus = Assert.Single(frame.Focus);
            for (int item = 0; item < frame.Assign.Count; item++) {
                string id = PackingFrames.ItemId(item);
                if (id == focus) Assert.Equal(PictureState.ElementHighlight, Of(frame.Items, id));
                else if (frame.Assign[item] >= 0) Assert.Equal(PictureState.Committed, Of(frame.Items, id));
                else Assert.Equal(PictureState.Background, Of(frame.Items, id));
            }
            // Chosen is Covered once the best sum is T, and neutral before.
            bool hit = frame.Totals[0].Size == int.Parse(instance[(instance.LastIndexOf(',') + 1)..^1]);
            Assert.Equal(hit ? PictureState.Covered : PictureState.Background, Of(frame.Containers, "C1"));
        }
        Assert.Equal((StepEvent.Done, (bool?)ok), (frames[^1].Event, frames[^1].Ok));
        Assert.Equal(steps.Count, frames.Count);
        if (ok) Assert.All(frames[^1].Items.Where(p => frames[^1].Assign[int.Parse(p.Key[1..]) - 1] >= 0), p => Assert.Equal(PictureState.Solution, p.Value.State));
    }

    [Fact]
    public void FastApproximation_ThatMissesT_SaysSo_AndHasNoAnswer() {
        var steps = Steps("fastapproximation", "({1000,1001,5},1006)");
        Assert.Contains("1 trimmed as too close to another", steps[1].Caption);
        Assert.Equal("The best sum kept is 1000, not 1006, so the approximation answers no, even if an exact subset exists.", steps[^1].Caption);
        Assert.Equal((new Placement([]), (bool?)false), (steps[^1].Partial, steps[^1].Ok));
    }

    [Fact]
    public void Knapsack_BruteForce_StartsWithTheEmptySet_AndSaysWhyEachSetFails() {
        var steps = Steps("knapsackbruteforce", "({(10,60),(20,100),(30,120)},50,220)");
        Assert.Equal("Try {}: weight 0, value 0, short of 220.", steps[0].Caption);
        Assert.Equal("Try {(20,100),(30,120)}: weight 50, value 220. It works.", steps[^2].Caption);
        Assert.Equal("{(20,100),(30,120)} fits in 50 and is worth at least 220, found after 7 sets.", steps[^1].Caption);
        var over = Steps("knapsackbruteforce", "({(10,60),(20,100),(30,120)},50,250)");
        Assert.Contains("Try {(10,60),(20,100),(30,120)}: weight 60 is over 50.", over.Select(s => s.Caption));
        Assert.Equal("Checked all 8 sets. None is worth 250 within weight 50.", over[^1].Caption);
    }

    [Fact]
    public void Knapsack_DP_FillsARowPerItem_ThenTracesBackTheItemsItTakes() {
        var steps = Steps("knapsackdp", "({(10,60),(20,100),(30,120)},50,220)");
        Assert.Equal(
            ["Row 1 of the table, item (10,60): taking it raises the best value within 50 to 60.",
             "Row 2 of the table, item (20,100): taking it raises the best value within 50 to 160.",
             "Row 3 of the table, item (30,120): taking it raises the best value within 50 to 220.",
             "Trace back: take (30,120). Without it the best value within 50 would be 160, not 220. 20 of 50 left.",
             "Trace back: take (20,100). Without it the best value within 20 would be 60, not 100. 0 of 50 left.",
             "The table's best value within 50 is 220, at least 220. The items taken weigh 50."],
            steps.Select(s => s.Caption));
        Assert.Equal([StepEvent.Try, StepEvent.Try, StepEvent.Try, StepEvent.Accept, StepEvent.Accept, StepEvent.Done], steps.Select(s => s.Event));
        // Nothing is taken while the table is filled; the trace-back commits one item at a time.
        Assert.All(steps.Take(3), s => Assert.Equal(new Placement([]), s.Partial));
        Assert.Equal([-1, -1, 0], steps[3].Partial.Assign);
        Assert.Equal([-1, 0, 0], steps[4].Partial.Assign);
        var short_ = Steps("knapsackdp", "({(10,60),(20,100),(30,120)},50,250)");
        Assert.Equal("The table's best value within 50 is 220, short of 250.", short_[^1].Caption);
    }

    [Fact]
    public void BinPacking_BruteForce_PlacesTakesBackAndRejects() {
        var steps = Steps("binpackingbruteforce", "((4,7,3,6,2,8),10,3)");
        Assert.Equal("Put 4 in bin 1: 4 of 10 used.", steps[0].Caption);
        Assert.Equal(["I1", "C1"], steps[0].Focus);
        var reject = steps.First(s => s.Event == StepEvent.Reject);
        Assert.Equal("No bin has room for 8, so take back the last choice.", reject.Caption);
        Assert.Equal(["I6"], reject.Focus);
        var back = steps.First(s => s.Event == StepEvent.Backtrack);
        Assert.Equal("Take 2 back out of bin 1 and try the next bin.", back.Caption);
        Assert.Equal([0, 1, 0, 2], back.Partial.Assign);
        Assert.Equal("All 6 items fit in 3 bins of 10, within K = 3.", steps[^1].Caption);
        Assert.Equal([0, 1, 1, 0, 2, 2], steps[^1].Partial.Assign);
    }

    [Fact]
    public void BinPacking_BruteForce_NoPacking_EndsBlank() {
        var steps = Steps("binpackingbruteforce", "((6,6,6),10,2)");
        Assert.Equal("Every bin with room for 6 leaves no room for the items after it.", steps[^2].Caption);
        Assert.Equal("Every arrangement overflows: the 3 items don't fit in 2 bins of 10.", steps[^1].Caption);
        Assert.Equal((new Placement([]), (bool?)false), (steps[^1].Partial, steps[^1].Ok));
        var big = Steps("binpackingbruteforce", "((11,5),10,3)");
        Assert.Equal("11 is bigger than a whole bin (10), so nothing can work.", big[0].Caption);
        Assert.Equal(["I1"], big[0].Focus);
    }

    [Fact]
    public void BinPacking_FirstFitDecreasing_SortsThenFillsTheFirstBinWithRoom() {
        var steps = Steps("binpackingffd", "((4,7,3,6,2,8),10,3)");
        Assert.Equal(
            ["Sort largest first: 8, 7, 6, 4, 3, 2. Each goes in the first bin with room.",
             "8 goes in bin 1: 8 of 10 used.",
             "No open bin has room for 7, so open bin 2: 7 of 10 used.",
             "No open bin has room for 6, so open bin 3: 6 of 10 used.",
             "4 doesn't fit in bins 1 to 2, so it goes in bin 3: 10 of 10 used.",
             "3 doesn't fit in bin 1, so it goes in bin 2: 10 of 10 used.",
             "2 fits in bin 1: 10 of 10 used.",
             "Everything is packed into 3 bins of 10, within K = 3."],
            steps.Select(s => s.Caption));
        Assert.Equal(["I6", "C1"], steps[1].Focus);
        Assert.Equal([2, 1, 1, 2, 0, 0], steps[^1].Partial.Assign);
    }

    [Fact]
    public void BinPacking_FirstFitDecreasing_ThatFallsShort_RejectsTheItemWithNoBin() {
        var steps = Steps("binpackingffd", "((3,3,3,3,2,2,2,2),10,2)");
        Assert.Equal(StepEvent.Reject, steps[^2].Event);
        Assert.Equal("No open bin has room for 2, and opening another would need 3 bins, more than K = 2.", steps[^2].Caption);
        Assert.Equal(["I8"], steps[^2].Focus);
        Assert.Equal([0, 0, 0, 1, 1, 1, 1], steps[^2].Partial.Assign);
        Assert.Equal("First Fit Decreasing needs more than 2 bins. It is a heuristic, so the brute force may still find a packing.", steps[^1].Caption);
    }

    [Fact]
    public void EqualItems_StayDistinct_InTheStepsAndThePicture() {
        // Three 4s: the steps say which 4 went where, by position.
        var steps = Steps("binpackingbruteforce", "((4,4,4),8,2)");
        Assert.Equal([0, 0, 1], steps[^1].Partial.Assign);
        var frame = PackingFrames.Frame(BinPackingVisualization.Spec(new BINPACKING("((4,4,4),8,2)")), steps[^1].Partial, StepEvent.Done, [], "c", true, true);
        Assert.Equal(["I1", "I2", "I3"], frame.Items.Keys);
        Assert.Equal([8, 4], frame.Totals.Select(t => t.Size));
    }

    // ---- Frames from real runs ----

    [Fact]
    public void IncrementalRun_KeepsWhatIsPlacedCommitted_AndMarksTheItemWithNoBinRejected() {
        string instance = "((4,7,3,6,2,8),10,3)";
        var run = ((ISolver)new API.Problems.NPComplete.NPC_BINPACKING.Solvers.BinPackingBruteForce()).Run(instance, withSteps: true);
        var frames = ((IFramesVisualization)new BinPackingVisualization()).BuildFrames(instance, run).Frames.Cast<PackingFrame>().ToList();
        var steps = run.Steps.Cast<SolverStep<Placement>>().ToList();
        int r = steps.FindIndex(s => s.Event == StepEvent.Reject);
        Assert.Equal("Rejected", frames[r].Items["I6"].State.ToString());
        Assert.All(new[] { "I1", "I2", "I3", "I4", "I5" }, id => Assert.Equal("Committed", frames[r].Items[id].State.ToString()));
        Assert.All(frames.Take(frames.Count - 1), f => Assert.DoesNotContain(f.Containers.Values, s => s.State == PictureState.Rejected));
    }

    [Fact]
    public void WholeCandidateRun_HighlightsTheCandidate_AndOnlyTheAnswerIsSolution() {
        string instance = "({1,7,12,15},28)";
        var run = ((ISolver)new API.Problems.NPComplete.NPC_SUBSETSUM.Solvers.SubsetSumBruteForce()).Run(instance, withSteps: true);
        var frames = ((IFramesVisualization)new SubsetSumVisualization()).BuildFrames(instance, run).Frames.Cast<PackingFrame>().ToList();
        Assert.All(frames.Take(frames.Count - 1), f => Assert.DoesNotContain(f.Items.Values, s => s.State == PictureState.Solution));
        Assert.Equal("ElementHighlight", frames[0].Items["I1"].State.ToString());
        Assert.Equal("Rejected", frames[0].Containers["C1"].State.ToString());
        var done = frames[^1];
        Assert.Equal("I1:Solution I2:Untraveled I3:Solution I4:Solution", string.Join(" ", done.Items.Select(p => $"{p.Key}:{p.Value.State}")));
        Assert.Equal("Covered", done.Containers["C1"].State.ToString());
    }

    [Fact]
    public void Specs_DescribeEachProblemsItemsAndLines() {
        Assert.Equal(11, PartitionDefaultVisualization.Spec(new PARTITION()).Items.Count); // the second 12 collapses (#628)
        var sum = SubsetSumVisualization.Spec(new SUBSETSUM());
        Assert.Equal((PackingRule.SubsetSum, 28, 4), (sum.Rule, sum.Target, sum.Items.Count));
        var knapsack = KnapsackVisualization.Spec(new KNAPSACK());
        Assert.Equal((50, 220), (knapsack.Limit, knapsack.AtLeast));
        Assert.Equal(["(10,60) 10 60", "(20,100) 20 100", "(30,120) 30 120"], knapsack.Items.Select(i => $"{i.Label} {i.Size} {i.Value}"));
        var bins = BinPackingVisualization.Spec(new BINPACKING());
        Assert.Equal((10, 3, 6), (bins.Limit, bins.Bins, bins.Items.Count));
    }

    [Fact]
    public void RecordingOff_RecordsNothing() {
        Assert.Equal("((4,6),(7,3),(2,8))", new API.Problems.NPComplete.NPC_BINPACKING.Solvers.BinPackingBruteForce().Solve(new BINPACKING(), StepRecorder<Placement>.Off));
        Assert.Empty(StepRecorder<Placement>.Off.Steps);
    }
}
