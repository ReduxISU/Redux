using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Problems.P.P_MINSTCUT;
using API.Problems.P.P_MINSTCUT.Solvers;
using API.Problems.P.P_MINSTCUT.Visualizations;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Minimum S-T Cut's Edmonds-Karp solver records steps for the Flow Network picture. Recording must never change an answer.
// Endpoints/Snapshots/flow_solver_answers.tsv holds what it returned (solver, instance, answer as a JSON string; tab
// separated) BEFORE the picture existed (with the #785-#787 fixes in), on its default instance, the mockup's examples,
// hand-picked edge cases and seeded random instances.
public class FlowSolvers_Tests {
    private static string SnapshotPath(string file) =>
        Path.Combine(AppContext.BaseDirectory, "Endpoints", "Snapshots", file);

    internal static IEnumerable<(string Solver, string Instance, string Answer)> Rows() =>
        File.ReadAllLines(SnapshotPath("flow_solver_answers.tsv")).Where(l => l.Length > 0).Select(l => {
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
    internal static readonly Dictionary<string, string> VisualizationOf = new() { ["minstcutsolver"] = "minstcutvisualization" };

    private static HashSet<string> Side(string answer) => answer.Trim('{', '}').Split(',').Where(x => x.Length > 0).ToHashSet();

    private static List<SolverStep<NetworkFlow>> Steps(string instance) =>
        ((ISolver)new MinSTCutSolver()).Run(instance, withSteps: true).Steps.Cast<SolverStep<NetworkFlow>>().ToList();

    [Theory]
    [MemberData(nameof(RecordedAnswers))]
    public void Solve_ReturnsWhatItReturnedBeforeThePicture_AndItsStepsAreAValidFlow(string solverName, string instance, string answer) {
        var solver = (ISolver)Activator.CreateInstance(ProblemProvider.Solvers[solverName])!;
        var on = solver.Run(instance, withSteps: true);
        var off = solver.Run(instance, withSteps: false);
        Assert.Equal(answer, on.Answer);
        Assert.Equal(answer, off.Answer);
        Assert.Equal(answer, solver.solve(instance));
        Assert.Empty(off.Steps);
        Assert.True(on.Incremental);

        var steps = on.Steps.Cast<SolverStep<NetworkFlow>>().ToList();
        Assert.Equal(StepEvent.Try, steps[0].Event);
        Assert.Equal(StepEvent.Done, steps[^1].Event);
        Assert.Single(steps, s => s.Event == StepEvent.Done);
        Assert.All(steps[1..^1], s => Assert.Equal(StepEvent.Accept, s.Event));
        Assert.True(steps[^1].Ok);
        Assert.All(steps, s => Assert.False(string.IsNullOrWhiteSpace(s.Caption)));

        var problem = new MINSTCUT(instance);
        var spec = MinSTCutVisualization.Spec(problem);
        var payload = FlowFrames.Payload(spec);
        var capacity = payload.Edges.ToDictionary(e => e.Id, e => e.Capacity);
        var ids = payload.Nodes.Select(n => n.Id).Concat(capacity.Keys).ToHashSet();
        string s = FlowFrames.NodeId(problem.sourceNode), t = FlowFrames.NodeId(problem.targetNode);

        IReadOnlyDictionary<string, int>? before = null;
        foreach (var step in steps) {
            var flow = step.Partial.Flow;
            // Every id a step names is an id of the picture.
            Assert.All(step.Focus, id => Assert.Contains(id, ids));
            Assert.All(flow.Keys, id => Assert.Contains(id, ids));
            Assert.All(step.Partial.Path, arc => Assert.Contains(arc.Edge, ids));
            // A flow: within capacity, conserved everywhere but the source and the sink, its value what leaves the source.
            Assert.All(flow, p => Assert.InRange(p.Value, 1, capacity[p.Key]));
            int Out(string node) => payload.Edges.Where(e => e.A == node).Sum(e => flow.GetValueOrDefault(e.Id))
                - payload.Edges.Where(e => e.B == node).Sum(e => flow.GetValueOrDefault(e.Id));
            foreach (var n in payload.Nodes.Select(n => n.Id).Where(n => n != s && n != t)) Assert.Equal(0, Out(n));
            Assert.Equal(step.Partial.Value, Out(s));
            // A path step moves exactly what its legs say, the same amount at every hop.
            if (step.Event == StepEvent.Accept) {
                var moved = capacity.Keys.ToDictionary(id => id, id => flow.GetValueOrDefault(id) - before!.GetValueOrDefault(id));
                var legs = capacity.Keys.ToDictionary(id => id, id => step.Partial.Path.Where(a => a.Edge == id).Sum(a => a.Forward ? a.Units : -a.Units));
                Assert.Equal(legs, moved);
                Assert.NotEmpty(step.Partial.Path);
            } else {
                Assert.Empty(step.Partial.Path);
            }
            before = flow;
        }

        // The S side is the answer, and the cut it makes is full and equals the maximum flow.
        var done = steps[^1].Partial;
        Assert.Equal(Side(answer).Order(StringComparer.Ordinal), done.Side);
        int cut = problem.edges.Where(e => done.Side.Contains(e.from) && !done.Side.Contains(e.to)).Sum(e => e.weight);
        Assert.Equal(done.Value, cut);
        Assert.Equal(MinSTCutSolver.GetMinCutCapacity(problem), cut);

        // One frame per step, the Done frame's verdict the verifier's.
        var vis = (IFramesVisualization)Activator.CreateInstance(ProblemProvider.Visualizers[VisualizationOf[solverName]])!;
        var frames = vis.BuildFrames(instance, on).Frames.Cast<FlowFrame>().ToList();
        Assert.Equal(steps.Count, frames.Count);
        Assert.Equal(problem.defaultVerifier.verify(problem, answer), frames[^1].Ok);
    }

    [Fact]
    public void Default_PushesOnePathPerStep_AndEndsWithTheCut() {
        var steps = Steps("({1,2,3,4},{((1,2),10),((1,3),2),((2,4),3),((3,4),5)},1,4)");
        Assert.Equal(
            ["No flow yet. Each step finds a path from 1 to 4 with room left and pushes as much as it can.",
             "Path 1: push 3 along 1 → 2 → 4. 2 → 4 is now full. Flow is now 3.",
             "Path 2: push 2 along 1 → 3 → 4. 1 → 3 is now full. Flow is now 5.",
             "No augmenting path is left. The nodes still reachable from 1 form S = {1,2}. The 2 edges from S to T are full and add up to 5, equal to the maximum flow."],
            steps.Select(s => s.Caption));
        Assert.Equal(["1", "1>2", "2", "2>4", "4"], steps[1].Focus);
        Assert.Equal([new FlowArc("1>2", true, 3), new FlowArc("2>4", true, 3)], steps[1].Partial.Path);
        Assert.Equal(new NetworkFlow([new("1>2", 3), new("2>4", 3), new("1>3", 2), new("3>4", 2)], [], 5, ["1", "2"]), steps[^1].Partial);
    }

    [Fact]
    public void APathThatRunsBackward_TakesBackFlowSentEarlier() {
        // The mockup's "Flow gets taken back": path 1 sends a → b, path 2 has to undo it to reach d.
        var steps = Steps("({s,a,b,c,d,t},{((s,a),1),((s,c),1),((a,b),1),((a,d),1),((c,b),1),((b,t),1),((d,t),1)},s,t)");
        Assert.Equal("Path 1: push 1 along s → a → b → t. s → a, a → b and b → t are now full. Flow is now 1.", steps[1].Caption);
        Assert.Equal("Path 2: push 1 along s → c → b ⇠ a → d → t. It runs backward over a → b, taking back flow sent there earlier. "
            + "s → c, c → b, a → d and d → t are now full. Flow is now 2.", steps[2].Caption);
        Assert.Equal(["s", "s>c", "c", "c>b", "b", "a>b", "a", "a>d", "d", "d>t", "t"], steps[2].Focus);
        Assert.Contains(new FlowArc("a>b", false, 1), steps[2].Partial.Path);
        Assert.False(steps[2].Partial.Flow.ContainsKey("a>b"));
        Assert.Equal("No augmenting path is left. The nodes still reachable from s form S = {s}. The 2 edges from S to T are full and add up to 2, equal to the maximum flow.", steps[^1].Caption);
    }

    [Fact]
    public void ParallelEdges_AreReadAsOneSummedEdge() {
        var steps = Steps("({1,2,3},{((1,2),2),((1,2),5),((2,3),4)},1,3)");
        Assert.Equal("Path 1: push 4 along 1 → 2 → 3. 2 → 3 is now full. Flow is now 4.", steps[1].Caption);
        Assert.Equal(new Dictionary<string, int> { ["1>2"] = 4, ["2>3"] = 4 }, steps[1].Partial.Flow);
    }

    [Fact]
    public void OppositeEdges_CarryTheirOwnFlow() {
        var steps = Steps("({a,b,c},{((a,b),2),((b,a),7),((b,c),9)},a,c)");
        Assert.Equal(new Dictionary<string, int> { ["a>b"] = 2, ["b>c"] = 2 }, steps[^1].Partial.Flow);
        Assert.Equal("No augmenting path is left. The nodes still reachable from a form S = {a}. The only edge from S to T is full, and its capacity, 2, equals the maximum flow.", steps[^1].Caption);
    }

    [Fact]
    public void NoPathAtAll_EndsAtOnce_WithAZeroCut() {
        var steps = Steps("({1,2,3,4},{((1,2),3),((3,4),3)},1,4)");
        Assert.Equal(2, steps.Count);
        Assert.Equal("There is no path from 1 to 4 with room, so the maximum flow and the minimum cut are both 0.", steps[^1].Caption);
        Assert.Equal(new NetworkFlow([], [], 0, ["1", "2"]), steps[^1].Partial);
        Assert.True(steps[^1].Ok);
    }

    [Fact]
    public void RecordingOff_RecordsNothing() {
        var problem = new MINSTCUT();
        Assert.Equal("{1,2}", new MinSTCutSolver().Solve(problem, StepRecorder<NetworkFlow>.Off));
        Assert.Empty(StepRecorder<NetworkFlow>.Off.Steps);
    }
}
