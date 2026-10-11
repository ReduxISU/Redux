using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_EXACTCOVER;
using API.Problems.NPComplete.NPC_EXACTCOVER.Solvers;
using API.Problems.NPComplete.NPC_HITTINGSET.Solvers;
using API.Problems.NPComplete.NPC_SETCOVER.Solvers;
using SPADE;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Set Cover, Exact Cover and Hitting Set solvers record steps for the Bipartite Graph picture. Recording must never
// change an answer. Endpoints/Snapshots/bipartite_solver_answers.tsv holds what each solver returned (solver, instance,
// answer; tab separated) BEFORE it recorded steps (with the #772-#776 fixes in), on its default instance and 12 seeded
// random instances, including ones with no answer.
public class BipartiteSolvers_Tests {
    private static string SnapshotPath(string file) =>
        Path.Combine(AppContext.BaseDirectory, "Endpoints", "Snapshots", file);

    public static TheoryData<string, string, string> RecordedAnswers {
        get {
            var data = new TheoryData<string, string, string>();
            foreach (var line in File.ReadAllLines(SnapshotPath("bipartite_solver_answers.tsv")).Where(l => l.Length > 0)) {
                var parts = line.Split('\t');
                data.Add(parts[0], parts[1], parts[2]);
            }
            return data;
        }
    }

    private static Type ProblemTypeOf(Type solver) =>
        solver.GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISolver<>)).GetGenericArguments()[0];

    // The visualization of a problem type, as the picture would describe it.
    private static BipartiteSpec SpecOf(Type problemType, string instance) => problemType.Name switch {
        "SETCOVER" => API.Problems.NPComplete.NPC_SETCOVER.Visualizations.SetCoverDefaultVisualization.Spec(new API.Problems.NPComplete.NPC_SETCOVER.SETCOVER(instance)),
        "EXACTCOVER" => API.Problems.NPComplete.NPC_EXACTCOVER.Visualizations.ExactCoverDefaultVisualization.Spec(new EXACTCOVER(instance)),
        "HITTINGSET" => API.Problems.NPComplete.NPC_HITTINGSET.Visualizations.HittingSetDefaultVisualization.Spec(new API.Problems.NPComplete.NPC_HITTINGSET.HITTINGSET(instance)),
        _ => throw new ArgumentException(problemType.Name),
    };

    // What an answer or a step names, compared by contents ("1,2,3" per set, sorted), so two sets with the same elements
    // compare equal whichever of them a solver took.
    private static string[] Contents(BipartiteSpec spec, IEnumerable<string> pickIds) => spec.Pick == BipartitePick.Sets
        ? pickIds.Select(id => string.Join(",", spec.Sets[int.Parse(id[1..]) - 1].Order(StringComparer.Ordinal))).Distinct().Order(StringComparer.Ordinal).ToArray()
        : pickIds.Order(StringComparer.Ordinal).ToArray();

    private static string[] AnswerContents(BipartiteSpec spec, string answer) {
        var parsed = new UtilCollection(answer).ToList();
        return spec.Pick == BipartitePick.Sets
            ? parsed.Select(s => string.Join(",", s.ToList().Select(x => x.ToString()).Order(StringComparer.Ordinal))).Distinct().Order(StringComparer.Ordinal).ToArray()
            : parsed.Select(x => BipartiteFrames.ElementId(x.ToString())).Order(StringComparer.Ordinal).ToArray();
    }

    private static readonly HashSet<string> Incremental = ["greedysetcover", "heuristicsolver", "dancinglinks", "exactcoverrecursive"];

    [Theory]
    [MemberData(nameof(RecordedAnswers))]
    public void Solve_ReturnsWhatItReturnedBeforeRecordingSteps_AndItsStepsEndWithThatAnswer(string solverName, string instance, string answer) {
        var type = ProblemProvider.Solvers[solverName];
        var solver = (ISolver)Activator.CreateInstance(type)!;
        var problemType = ProblemTypeOf(type);
        var spec = SpecOf(problemType, instance);

        var on = solver.Run(instance, withSteps: true);
        var off = solver.Run(instance, withSteps: false);
        Assert.Equal(answer, on.Answer);
        Assert.Equal(answer, off.Answer);
        Assert.Equal(answer, solver.solve(instance));
        Assert.Empty(off.Steps);
        Assert.Equal(BipartiteFrames.StepShape(spec.Pick), on.StepShape);
        Assert.Equal(Incremental.Contains(solverName), on.Incremental);

        var steps = on.Steps.Select(s => s switch {
            SolverStep<ChosenSets> c => (Picks: (IEnumerable<string>)c.Partial.Sets, c.Event, c.Focus, c.Caption, c.Ok),
            SolverStep<NodeSet> n => (Picks: BipartiteFrames.PickIds(n.Partial), n.Event, n.Focus, n.Caption, n.Ok),
            _ => throw new InvalidOperationException(s.GetType().Name),
        }).ToList();
        Assert.Equal(StepEvent.Done, steps[^1].Event);
        Assert.Single(steps, s => s.Event == StepEvent.Done);
        Assert.Equal(AnswerContents(spec, answer), Contents(spec, steps[^1].Picks));
        Assert.All(steps, s => Assert.False(string.IsNullOrWhiteSpace(s.Caption)));

        // Every id a step names is an id of the picture.
        var payload = BipartiteFrames.Payload(spec);
        var ids = payload.Elements.Select(e => e.Id).Concat(payload.Sets.Select(s => s.Id)).Concat(payload.Edges.Select(e => e.Id)).ToHashSet();
        Assert.All(steps, s => Assert.All(s.Focus.Concat(s.Picks), id => Assert.Contains(id, ids)));

        // Done.Ok is the verifier's verdict on the answer ("{}" included: it is the empty collection).
        var problem = (IProblem)Activator.CreateInstance(problemType)!;
        Assert.Equal(problem.defaultVerifier.verify(instance, answer), steps[^1].Ok);
    }

    // ---- Set Cover ----

    private static List<SolverStep<ChosenSets>> SetSteps(ISolver solver, string instance) =>
        solver.Run(instance, withSteps: true).Steps.Cast<SolverStep<ChosenSets>>().ToList();

    [Fact]
    public void GreedySetCover_PicksTheSetCoveringTheMostNewElements_AndSaysWhy() {
        var steps = SetSteps(new GreedySetCover(), "({1,2,3,4,5},{{1,2,3},{2,4},{3,4},{4,5}},3)");
        Assert.Equal(
            ["Pick S1 {1,2,3}: it covers 3 new elements, the most of any set left.",
             "Pick S4 {4,5}: it covers 2 new elements, the most of any set left.",
             "Every element is covered with 2 sets, within K = 3."],
            steps.Select(s => s.Caption));
        Assert.Equal([StepEvent.Accept, StepEvent.Accept, StepEvent.Done], steps.Select(s => s.Event));
        Assert.Equal(["S4"], steps[1].Focus);
        Assert.Equal(new ChosenSets(["S1", "S4"]), steps[1].Partial);
        Assert.True(steps[^1].Ok);
    }

    [Fact]
    public void GreedySetCover_OverK_RejectsTheSetsPastK_AndEndsWithNoAnswer() {
        // Greedy takes S1 (4 new), then S2 and S3 (1 new each): 3 sets. {S2,S3} would do with 2.
        var steps = SetSteps(new GreedySetCover(), "({1,2,3,4,5,6},{{1,2,3,4},{1,2,5},{3,4,6}},2)");
        var reject = Assert.Single(steps, s => s.Event == StepEvent.Reject);
        Assert.Equal("Greedy's cover uses 3 sets, more than K = 2.", reject.Caption);
        Assert.Equal(["S3"], reject.Focus);
        Assert.Equal(new ChosenSets(["S1", "S2", "S3"]), reject.Partial);
        Assert.False(steps[^1].Ok);
        Assert.Empty(steps[^1].Partial.Sets);
        Assert.Equal("{{1,2,5},{3,4,6}}", new SetCoverBruteForce().solve(new API.Problems.NPComplete.NPC_SETCOVER.SETCOVER("({1,2,3,4,5,6},{{1,2,3,4},{1,2,5},{3,4,6}},2)")));
    }

    [Fact]
    public void GreedySetCover_AnElementInNoSet_IsTheFocusOfTheReject() {
        var steps = SetSteps(new GreedySetCover(), "({1,2,3},{{1,2}},5)");
        var reject = Assert.Single(steps, s => s.Event == StepEvent.Reject);
        Assert.Equal("3 is in no set, so no cover exists.", reject.Caption);
        Assert.Equal(["e:3"], reject.Focus);
        Assert.Equal("No cover exists: 3 is in no set.", steps[^1].Caption);
    }

    [Fact]
    public void SetCoverBruteForce_TriesSmallerCombinationsFirst_AndNamesWhatEachMisses() {
        var steps = SetSteps(new SetCoverBruteForce(), "({1,2,3,4,5},{{1,2,3},{2,4},{3,4},{4,5}},3)");
        Assert.Equal("Try {S1}: 4 and 5 aren't covered.", steps[0].Caption);
        Assert.Equal(["S1"], steps[0].Focus);
        Assert.All(steps.Where(s => s.Event == StepEvent.Reject), s => Assert.Matches(@"^Try \{S\d(,S\d)*\}: .+ (isn't|aren't) covered\.$", s.Caption));
        Assert.Equal("Try {S1,S4}. It covers every element.", steps[^2].Caption);
        Assert.Equal(StepEvent.Accept, steps[^2].Event);
        Assert.Equal("{S1,S4} covers U with 2 sets, found after 7 tries. Smaller combinations were tried first, so no cover uses fewer sets.", steps[^1].Caption);
    }

    [Theory]
    [InlineData("({1,2,3},{{1,2},{3}},0)", "K = 0 allows no sets, so U can't be covered.", false)]
    [InlineData("({1},{},2)", "S has no sets, so U can't be covered.", false)]
    [InlineData("({1,2,3},{{1},{2},{3}},2)", "Checked all 6 combinations of up to K = 2 sets. None covers U.", false)]
    public void SetCoverBruteForce_WithNoCover_SaysWhy(string instance, string caption, bool ok) {
        var steps = SetSteps(new SetCoverBruteForce(), instance);
        Assert.Equal(caption, steps[^1].Caption);
        Assert.Equal(ok, steps[^1].Ok);
    }

    [Fact]
    public void SetCoverAlgorithmX_TakesTheElementInFewestSets_GoesOverBudget_AndUndoes() {
        // Every element is in two sets; with K = 3 the search has to back out of choices that need a fourth set.
        var steps = SetSteps(new HeuristicSolver(), "({1,2,3,4,5},{{1,2},{2,3},{3,4},{4,5},{1,5}},3)");
        Assert.Matches(@"^\d is the uncovered element in the fewest sets \(2\)\. Take S\d \{\d,\d\}\.$", steps[0].Caption);
        Assert.Contains(steps, s => s.Event == StepEvent.Reject && s.Caption == "That makes 4 sets, more than K = 3, so undo it.");
        Assert.Contains(steps, s => s.Event == StepEvent.Backtrack && s.Caption.StartsWith("Undo S"));
        // The over-budget Reject names the set that went over, and the sets taken stay in the partial.
        var over = steps.First(s => s.Event == StepEvent.Reject);
        Assert.Single(over.Focus);
        Assert.Contains(over.Focus[0], over.Partial.Sets);
        Assert.Equal(4, over.Partial.Sets.Length);
        Assert.True(steps[^1].Ok);
    }

    [Fact]
    public void SetCoverAlgorithmX_AnElementInNoSet_IsRejected() {
        var steps = SetSteps(new HeuristicSolver(), "({1,2,3},{{1,2}},5)");
        Assert.Contains(steps, s => s.Event == StepEvent.Reject && s.Caption == "No set contains 3, so no cover exists." && s.Focus.SequenceEqual(["e:3"]));
        Assert.False(steps[^1].Ok);
    }

    // ---- Exact Cover ----

    // Knuth's example: the only exact cover is {B, D, F} = {S2, S4, S6}, reached only after the S1 branch fails.
    private const string Knuth = "({1,2,3,4,5,6,7},{{1,4,7},{1,4},{4,5,7},{3,5,6},{2,3,6,7},{2,7}})";

    [Fact]
    public void DancingLinks_BranchesOnTheElementWithFewestOptions_AndUndoesDeadEnds() {
        var steps = SetSteps(new DancingLinks(), Knuth);
        Assert.Matches(@"^\d has the fewest options \(2\)\. Try S\d \{[\d,]+\}\.$", steps[0].Caption);
        Assert.Contains(steps, s => s.Event == StepEvent.Reject && s.Caption.StartsWith("No set left can cover ") && s.Caption.EndsWith(" without doubling up, so undo."));
        Assert.Contains(steps, s => s.Event == StepEvent.Backtrack);
        Assert.Equal(new ChosenSets(["S2", "S4", "S6"]), steps[^1].Partial);
        Assert.True(steps[^1].Ok);
        // A Reject names the element that failed.
        Assert.All(steps.Where(s => s.Event == StepEvent.Reject), s => Assert.StartsWith("e:", Assert.Single(s.Focus)));
    }

    [Fact]
    public void ExactCoverRecursive_TakesDisjointSets_AndUndoesDeadEnds() {
        var steps = SetSteps(new ExactCoverRecursive(), Knuth);
        Assert.Equal("Take S1 {1,4,7}: it shares no element with the sets taken.", steps[0].Caption);
        Assert.Contains(steps, s => s.Event == StepEvent.Reject && s.Caption.StartsWith("Every set left overlaps the sets taken, and "));
        Assert.Contains(steps, s => s.Event == StepEvent.Backtrack && s.Caption == "Undo S1: no exact cover follows from it.");
        Assert.Equal(new ChosenSets(["S2", "S4", "S6"]), steps[^1].Partial);
    }

    [Fact]
    public void ExactCoverBruteForce_NamesMissedAndDoubledElements() {
        var steps = SetSteps(new ExactCoverBruteForce(), "({1,2,3,4},{{1,2,3},{2,3},{4,1}})");
        Assert.Equal("Try {S1}: 4 isn't covered.", steps[0].Caption);
        Assert.Equal("Try {S1,S2}: 4 isn't covered; 2 and 3 are covered more than once.", steps[2].Caption);
        Assert.Equal("{S2,S3} covers every element exactly once, found after 6 tries.", steps[^1].Caption);
    }

    [Fact]
    public void ExactCover_EmptyUniverse_EveryIncrementalSolverEndsOkWithNothingPicked() {
        foreach (var solver in new ISolver[] { new DancingLinks(), new ExactCoverRecursive() }) {
            var steps = SetSteps(solver, "({},{})");
            var done = Assert.Single(steps);
            Assert.True(done.Ok);
            Assert.Empty(done.Partial.Sets);
            Assert.Equal("There are no elements, so the empty collection {} is an exact cover.", done.Caption);
        }
    }

    // ---- Hitting Set ----

    [Fact]
    public void HittingSetBruteForce_RecordsElementNames_AndNamesSetsMissedOrHitTwice() {
        var steps = ((ISolver)new HittingSetBruteForce()).Run("({1,2,3,4},{{1,3},{2,3,4},{1,4}})", withSteps: true).Steps.Cast<SolverStep<NodeSet>>().ToList();
        Assert.Equal("Try {}: S1, S2 and S3 aren't hit.", steps[0].Caption);
        Assert.Empty(steps[0].Focus);
        Assert.Equal("Try {1}: S2 isn't hit.", steps[1].Caption);
        Assert.Equal(["e:1"], steps[1].Focus);
        Assert.Equal("Try {2}: S1 and S3 aren't hit.", steps[2].Caption);
        Assert.Equal(new NodeSet(["1", "2"]), steps[^1].Partial);
        Assert.Equal("{1,2} hits every set exactly once, found after 4 tries.", steps[^1].Caption);
    }

    // ---- Stopped runs ----

    [Fact]
    public void ExactCoverBruteForce_StoppedByTheTimeLimit_EndsWithTheEmptyAnswer() {
        // 2^40 combinations of the sets {1}, ..., {40}, none an exact cover (z is in no set).
        string slow = "({" + string.Join(",", Enumerable.Range(1, 40)) + ",z},{" + string.Join(",", Enumerable.Range(1, 40).Select(i => $"{{{i}}}")) + "})";
        var run = ((ISolver)new ExactCoverBruteForce()).Run(slow, withSteps: true, TimeSpan.FromMilliseconds(50));
        Assert.True(run.TimedOut);
        Assert.Equal(SolveRun.TimedOutAnswer, run.Answer);
        var done = run.Steps.Cast<SolverStep<ChosenSets>>().Last();
        Assert.Equal(StepEvent.Done, done.Event);
        Assert.False(done.Ok);
        Assert.Empty(done.Partial.Sets);
        Assert.StartsWith("Stopped after 0.05 s and ", done.Caption);
    }
}
