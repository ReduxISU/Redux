using System.Text.RegularExpressions;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_MAXCUT;
using API.Problems.NPComplete.NPC_MINIMUMSPANNINGTREE;
using API.Problems.NPComplete.NPC_STEINERTREE;
using API.Problems.P.P_MINCUT;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Minimum Spanning Tree and Steiner Tree solvers record EdgeSet steps; the Cut, Weighted Cut, Max Cut and Minimum
// Cut solvers record Groups steps (the split of the nodes). Recording must never change an answer.
// Endpoints/Snapshots/edgeset_solver_answers.tsv holds what each solver returned (solver, instance, answer; tab
// separated) BEFORE it recorded steps, on its default instance and on 12 seeded random graphs (including ones with no
// answer). A row changes only when a bug fix changes that answer (#750: a Steiner tree the brute force skipped).
public class EdgeSetSolvers_Tests {
    private static string SnapshotPath(string file) =>
        Path.Combine(AppContext.BaseDirectory, "Endpoints", "Snapshots", file);

    public static TheoryData<string, string, string> RecordedAnswers {
        get {
            var data = new TheoryData<string, string, string>();
            foreach (var line in File.ReadAllLines(SnapshotPath("edgeset_solver_answers.tsv")).Where(l => l.Length > 0)) {
                var parts = line.Split('\t');
                data.Add(parts[0], parts[1], parts[2]);
            }
            return data;
        }
    }

    private static Type ProblemTypeOf(Type solver) =>
        solver.GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISolver<>)).GetGenericArguments()[0];

    private static Type ShapeOf(Type solver) =>
        solver.GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISolver<,>)).GetGenericArguments()[1];

    private static object Prop(object step, string name) => step.GetType().GetProperty(name)!.GetValue(step)!;

    [Theory]
    [MemberData(nameof(RecordedAnswers))]
    public void Solve_ReturnsWhatItReturnedBeforeRecordingSteps(string solverName, string instance, string answer) {
        var type = ProblemProvider.Solvers[solverName];
        var solver = (ISolver)Activator.CreateInstance(type)!;
        var on = solver.Run(instance, withSteps: true);
        var off = solver.Run(instance, withSteps: false);

        Assert.Equal(answer, on.Answer);
        Assert.Equal(answer, off.Answer);
        Assert.Equal(answer, solver.solve(instance));
        Assert.Empty(off.Steps);
        Assert.Equal(ShapeOf(type), on.StepShape);

        // The last step is the only Done, every caption is a sentence, and Done.Ok is what the problem's verifier says.
        var steps = on.Steps.Select(s => (Event: (StepEvent)Prop(s, "Event"), Caption: (string)Prop(s, "Caption"), Ok: (bool?)s.GetType().GetProperty("Ok")!.GetValue(s))).ToList();
        Assert.Equal(StepEvent.Done, steps[^1].Event);
        Assert.Single(steps, s => s.Event == StepEvent.Done);
        Assert.All(steps, s => Assert.False(string.IsNullOrWhiteSpace(s.Caption)));
        var problem = (IProblem)Activator.CreateInstance(ProblemTypeOf(type))!;
        bool verified;
        try { verified = problem.defaultVerifier.verify(instance, answer); } catch { verified = false; }
        Assert.Equal(verified, steps[^1].Ok);
    }

    // The last step's partial answer says the same thing as the answer string.
    [Theory]
    [MemberData(nameof(RecordedAnswers))]
    public void TheDoneStep_ShowsTheAnswerTheSolverGave(string solverName, string instance, string answer) {
        var type = ProblemProvider.Solvers[solverName];
        var run = ((ISolver)Activator.CreateInstance(type)!).Run(instance, withSteps: true);
        var partial = Prop(run.Steps[^1], "Partial");
        if (partial is EdgeSet edges) {
            Assert.Equal(GraphEdgeFrames.ParseEdges(answer), edges);
            return;
        }
        var groups = Assert.IsType<Groups>(partial);
        if (solverName == "maxcutsolver") {
            // Max Cut's answer is the S side itself, which is group 0.
            var s = answer.Replace("{", "").Replace("}", "").Split(',').Where(x => x.Length > 0).ToHashSet();
            Assert.Equal(s, groups.Of.Where(p => p.Value == 0).Select(p => p.Key).ToHashSet());
        } else if (answer == "{}") {
            // No answer (no split is shown), or, for Minimum Cut, a graph that is already in pieces: its split is shown, and no
            // edge crosses it.
            if (solverName == "mincutstoerwagner")
                Assert.DoesNotContain(GraphEdgeFrames.ParseEdges(instance).Edges, id => {
                    int dash = id.IndexOf('-');
                    return groups.Of.TryGetValue(id[..dash], out int a) && groups.Of.TryGetValue(id[(dash + 1)..], out int b) && a != b;
                });
            else
                Assert.Empty(groups.Of);
        } else {
            // The other cuts answer with their crossing edges: the final split must cross exactly those.
            var cut = GraphEdgeFrames.ParseEdges(answer).Edges.ToHashSet();
            var crossing = GraphEdgeFrames.ParseEdges(instance).Edges.Where(id => {
                int dash = id.IndexOf('-');
                return groups.Of[id[..dash]] != groups.Of[id[(dash + 1)..]];
            }).ToHashSet();
            Assert.Equal(cut, crossing);
        }
    }

    [Fact]
    public void Kruskal_StepsAreEdgesInWeightOrder_AcceptedOrRejected() {
        // The default instance: 1-2 (1), 3-4 (1), 2-3 (2), 1-4 (2), 1-3 (3). Kruskal adds 1-2, 3-4, then breaks the 2-3 / 1-4
        // tie by key (1-4 first) and stops with a tree of 3 edges.
        var run = ((ISolver)new API.Problems.NPComplete.NPC_MINIMUMSPANNINGTREE.Solvers.KruskalSolver()).Run(new MINIMUMSPANNINGTREE().defaultInstance, true);
        var steps = run.Steps.Cast<SolverStep<EdgeSet>>().ToList();
        Assert.Equal(["Accept", "Accept", "Accept", "Done"], steps.Select(s => s.Event.ToString()));
        Assert.Equal("Edge 1-2 joins two trees, so add it (weight 1).", steps[0].Caption);
        Assert.Equal(["1-2"], steps[0].Focus);
        Assert.Equal(new EdgeSet(["1-2", "3-4", "1-4"]), steps[^1].Partial);
    }

    [Fact]
    public void Kruskal_ARejectedEdgeWouldCloseACycle() {
        // Path 1-2-3 plus 1-3 (weight 3): 1-2 and 2-3 join, then 1-3 would close a cycle.
        var run = ((ISolver)new API.Problems.NPComplete.NPC_MINIMUMSPANNINGTREE.Solvers.KruskalSolver())
            .Run("({1,2,3,4},{({1,2},1),({2,3},1),({1,3},3),({3,4},5)})", true);
        var steps = run.Steps.Cast<SolverStep<EdgeSet>>().ToList();
        Assert.Equal(["Accept", "Accept", "Reject", "Accept", "Done"], steps.Select(s => s.Event.ToString()));
        Assert.Equal("Edge 1-3 would close a cycle, so skip it (weight 3).", steps[2].Caption);
        Assert.Equal(new EdgeSet(["1-2", "2-3"]), steps[2].Partial);   // the rejected edge is not in the answer so far
    }

    [Fact]
    public void Prim_GrowsOneTreeFromTheFirstNode() {
        var run = ((ISolver)new API.Problems.NPComplete.NPC_MINIMUMSPANNINGTREE.Solvers.PrimSolver())
            .Run("({1,2,3,4},{({1,2},1),({2,3},1),({1,3},3),({3,4},5)})", true);
        var steps = run.Steps.Cast<SolverStep<EdgeSet>>().ToList();
        Assert.Equal(["Try", "Accept", "Accept", "Accept", "Done"], steps.Select(s => s.Event.ToString()));
        Assert.Equal("Start the tree at node 1.", steps[0].Caption);
        Assert.Equal(["1"], steps[0].Focus);
        Assert.Equal("Edge 1-2 is the lightest edge leaving the tree, so add it (weight 1). Node 2 joins.", steps[1].Caption);
    }

    [Fact]
    public void MaxCut_RecordsOnlySplitsThatBeatEveryEarlierOne() {
        var run = ((ISolver)new API.Problems.NPComplete.NPC_MAXCUT.Solvers.MaxCutSolver()).Run(new MAXCUT().defaultInstance, true);
        var steps = run.Steps.Cast<SolverStep<Groups>>().ToList();
        Assert.All(steps.Take(steps.Count - 1), s => Assert.Equal(StepEvent.Accept, s.Event));
        Assert.True(steps.Count < 20, "2^5 - 2 splits were tried, but only the improvements are steps");
        var weights = steps.Take(steps.Count - 1).Select(s => int.Parse(Regex.Match(s.Caption, @"weigh (\d+)").Groups[1].Value)).ToList();
        Assert.Equal(weights.OrderBy(w => w).Distinct(), weights);   // strictly increasing
        Assert.Contains("weighs 15", steps[^1].Caption);
    }

    [Fact]
    public void MinCut_HasOneStepPerPhase_TheFirstIsTheBestSoFar() {
        var run = ((ISolver)new API.Problems.P.P_MINCUT.Solvers.MinCutStoerWagner()).Run(new MINCUT().defaultInstance, true);
        var steps = run.Steps.Cast<SolverStep<Groups>>().ToList();
        Assert.Equal(5 - 1, steps.Count - 1);   // n - 1 phases, then Done
        Assert.Equal(StepEvent.Accept, steps[0].Event);
        Assert.All(steps.Take(steps.Count - 1), s => Assert.StartsWith("Phase ", s.Caption));
        Assert.All(steps.Take(steps.Count - 1), s => Assert.True(s.Event is StepEvent.Accept or StepEvent.Reject));
    }

    [Fact]
    public void SteinerBruteForce_RejectionsNameTheTerminalOrPieceThatFails() {
        var run = ((ISolver)new API.Problems.NPComplete.NPC_STEINERTREE.Solvers.SteinerTreeBruteForce()).Run(new STEINERTREE().defaultInstance, true);
        var rejects = run.Steps.Cast<SolverStep<EdgeSet>>().Where(s => s.Event == StepEvent.Reject).ToList();
        Assert.NotEmpty(rejects);
        Assert.All(rejects, s => Assert.Matches(@"^Try edges [\d, -]+: (terminals? [\d, ]+ (isn't|aren't) reached|the edges form more than one piece)\.$", s.Caption));
    }

    [Fact]
    public void SteinerBruteForce_HonoursTheStepCap_AndKeepsTheWinningAccept() {
        var run = ((ISolver)new API.Problems.NPComplete.NPC_STEINERTREE.Solvers.SteinerTreeBruteForce()).Run(new STEINERTREE().defaultInstance, true);
        var steps = run.Steps.Cast<SolverStep<EdgeSet>>().ToList();
        // The default instance is found past the cap: the first 150 tries, the winning Accept, then Done.
        Assert.Equal(StepRecorder<EdgeSet>.DefaultCap + 2, steps.Count);
        Assert.Equal(StepEvent.Accept, steps[^2].Event);
        Assert.Equal(StepEvent.Done, steps[^1].Event);
        Assert.True(steps[^1].Ok);
        Assert.Equal(steps[^1].Partial, steps[^2].Partial);
        Assert.Matches(@"found after (\d+) tries\. \(\d+ not shown\)$", steps[^1].Caption);
        int tried = int.Parse(Regex.Match(steps[^1].Caption, @"after (\d+) tries").Groups[1].Value);
        Assert.EndsWith($"({tried - StepRecorder<EdgeSet>.DefaultCap - 1} not shown)", steps[^1].Caption);
        // Each combination once (#750): C(10,2) + C(10,3) + C(10,4) = 45 + 120 + 210 sets of 2 to 4 edges, then the
        // tree {2,1},{1,3},{3,5},{6,8},{6,1} is the 36th set of 5 (21 start 2,1 / 1,3 / 2,3, then 3,5 with the 15th
        // pair of the last six edges). Re-trying the last combination of each size made it 618.
        Assert.Equal(45 + 120 + 210 + 36, tried);
    }

    [Fact]
    public void MinCut_WithParallelEdges_DoneIsOkOnlyIfTheVerifierAcceptsTheAnswer() {
        // Stoer-Wagner adds the parallel 1-2 edges up (a cut of 8), but the answer lists one of them (weight 3), which the
        // verifier refuses. The answer is what it always was; the Done step must not claim it is right.
        const string instance = "({1,2},{({1,2},3),({2,1},5)})";
        var solver = new API.Problems.P.P_MINCUT.Solvers.MinCutStoerWagner();
        var run = ((ISolver)solver).Run(instance, true);
        Assert.Equal("{({1,2},3)}", run.Answer);
        Assert.False(((API.Interfaces.IProblem)new MINCUT()).defaultVerifier.verify(instance, run.Answer));
        var done = run.Steps.Cast<SolverStep<Groups>>().Last();
        Assert.False(done.Ok);
        Assert.Equal("The minimum cut weighs 8, but the answer lists one edge per pair of nodes, weighing 3, so it does not show that cut.", done.Caption);
    }

    [Fact]
    public void CutBruteForce_GivingUp_SaysWhichSidesItTried_NotThatNoCutExists() {
        // Two triangles joined by 3-4: S = {1,2,3} cuts exactly one edge, but the solver only tries S sides of at most K = 1
        // node, so it gives up. Its caption must say what it searched, not that no such cut exists.
        const string instance = "(({1,2,3,4,5,6},{{1,2},{2,3},{1,3},{4,5},{5,6},{4,6},{3,4}}),1)";
        var run = ((ISolver)new API.Problems.NPComplete.NPC_CUT.Solvers.CutBruteForce()).Run(instance, true);
        Assert.Equal("{}", run.Answer);
        var done = run.Steps.Cast<SolverStep<Groups>>().Last();
        Assert.False(done.Ok);
        Assert.Equal("Checked 6 S sides of at most 1 nodes (this solver tries no larger ones). None cuts exactly 1 edges.", done.Caption);
    }
}
