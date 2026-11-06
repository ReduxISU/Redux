using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_ARCSET.Solvers;
using API.Problems.NPComplete.NPC_NODESET.Solvers;
using API.Problems.NPComplete.NPC_STRONGLYCONNECTEDCOMPONENTS.Solvers;
using API.Problems.NPComplete.NPC_TOPOLOGICALSORT.Solvers;
using API.Problems.P.P_SPSP.Solvers;
using API.Problems.P.P_SSSP.Solvers;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The six Layered Graph solvers record steps for the picture. Recording must never change an answer.
// Endpoints/Snapshots/layered_solver_answers.tsv holds what each solver returned (solver, instance, answer as a JSON string;
// tab separated) BEFORE the picture existed (with the #781-#784 fixes in), on its default instance, a few hand-picked
// instances and seeded random ones. Two rows changed since, on purpose: with #789 the Arc Set and Node Set brute forces
// try removing nothing first, so the acyclic (({1,...,6},{(2,6),(1,2),(2,3),(1,3)}),1) now answers {} for both.
public class LayeredSolvers_Tests {
    private static string SnapshotPath(string file) =>
        Path.Combine(AppContext.BaseDirectory, "Endpoints", "Snapshots", file);

    internal static IEnumerable<(string Solver, string Instance, string Answer)> Rows() =>
        File.ReadAllLines(SnapshotPath("layered_solver_answers.tsv")).Where(l => l.Length > 0).Select(l => {
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
        ["kahnsalgorithm"] = "topologicalsortdefaultvisualization",
        ["arcsetbruteforce"] = "arcsetdefaultvisualization",
        ["nodesetbruteforce"] = "nodesetdefaultvisualization",
        ["kosarajusolver"] = "sccvisualization",
        ["ssspsolver"] = "ssspvisualization",
        ["spspsolver"] = "spspvisualization",
    };

    internal static readonly HashSet<string> Incremental = ["kahnsalgorithm", "kosarajusolver", "ssspsolver", "spspsolver"];

    /// <summary>Whether a solver's answer is an answer the picture shows as found (ok): see the Done frame of each shape.</summary>
    internal static bool AnswerIsOk(string solver, string instance, string answer) {
        var verifier = solver switch {
            "arcsetbruteforce" => "arcsetverifier",
            "nodesetbruteforce" => "nodesetverifier",
            _ => null,
        };
        return solver switch {
            "kahnsalgorithm" => answer != "()",
            "spspsolver" => answer != "{}",
            "kosarajusolver" or "ssspsolver" => true,
            // Removing nothing is an answer when the graph has no cycle; the verifier decides.
            _ => ((IVerifier)Activator.CreateInstance(ProblemProvider.Verifiers[verifier!])!).verify(instance, answer),
        };
    }

    private static HashSet<string> PictureIds(string solver, string instance) {
        var vis = (IFramesVisualization)Activator.CreateInstance(ProblemProvider.Visualizers[VisualizationOf[solver]])!;
        var payload = (LayeredPayload)vis.BuildFrames(instance, new SolveRun("", [], null) { TimedOut = true }).Payload;
        return payload.Nodes.Select(n => n.Id).Concat(payload.Edges.Select(e => e.Id)).ToHashSet();
    }

    private static (object Partial, StepEvent Event, string[] Focus, string Caption, bool? Ok) Read(object step) => step switch {
        SolverStep<Tour> s => (s.Partial, s.Event, s.Focus, s.Caption, s.Ok),
        SolverStep<EdgeSet> s => (s.Partial, s.Event, s.Focus, s.Caption, s.Ok),
        SolverStep<NodeSet> s => (s.Partial, s.Event, s.Focus, s.Caption, s.Ok),
        SolverStep<Components> s => (s.Partial, s.Event, s.Focus, s.Caption, s.Ok),
        SolverStep<Distances> s => (s.Partial, s.Event, s.Focus, s.Caption, s.Ok),
        _ => throw new InvalidOperationException(step.GetType().Name),
    };

    [Theory]
    [MemberData(nameof(RecordedAnswers))]
    public void Solve_ReturnsWhatItReturnedBeforeThePicture_AndItsStepsDrawTheAnswer(string solverName, string instance, string answer) {
        var solver = (ISolver)Activator.CreateInstance(ProblemProvider.Solvers[solverName])!;
        var on = solver.Run(instance, withSteps: true);
        var off = solver.Run(instance, withSteps: false);
        Assert.Equal(answer, on.Answer);
        Assert.Equal(answer, off.Answer);
        Assert.Equal(answer, solver.solve(instance));
        Assert.Empty(off.Steps);
        Assert.Equal(Incremental.Contains(solverName), on.Incremental);

        var steps = on.Steps.Select(Read).ToList();
        Assert.Equal(StepEvent.Done, steps[^1].Event);
        Assert.Single(steps, s => s.Event == StepEvent.Done);
        Assert.Equal(AnswerIsOk(solverName, instance, answer), steps[^1].Ok);
        Assert.All(steps, s => Assert.False(string.IsNullOrWhiteSpace(s.Caption)));

        // Every id a step names is an id of the picture.
        var ids = PictureIds(solverName, instance);
        Assert.All(steps, s => Assert.All(s.Focus, id => Assert.Contains(id, ids)));

        // One frame per step; Solution only where something is committed or found: never in a brute force's tries.
        var vis = (IFramesVisualization)Activator.CreateInstance(ProblemProvider.Visualizers[VisualizationOf[solverName]])!;
        var frames = vis.BuildFrames(instance, on).Frames.Cast<LayeredFrame>().ToList();
        Assert.Equal(steps.Count, frames.Count);
        if (!on.Incremental)
            Assert.All(frames.Where(f => f.Event is StepEvent.Try or StepEvent.Reject),
                f => Assert.DoesNotContain(f.Nodes.Values.Concat(f.Edges.Values), s => s.State == PictureState.Solution));
        Assert.Equal(steps[^1].Ok, frames[^1].Ok);
    }

    private static List<(object Partial, StepEvent Event, string[] Focus, string Caption, bool? Ok)> Steps(ISolver solver, string instance) =>
        solver.Run(instance, withSteps: true).Steps.Select(Read).ToList();

    // ---- Topological Sort ----

    [Fact]
    public void Kahn_PlacesOneNodePerStep_AndSaysWhatItFrees() {
        var steps = Steps(new KahnsAlgorithm(), "({1,2,3,4,5,6},{(1,2),(1,3),(2,4),(3,4),(4,5),(3,6)})");
        Assert.Equal(
            ["Ready to place (no incoming edges): 1.",
             "Place 1 at position 1. That frees 2 and 3. Ready: 2 and 3.",
             "Place 2 at position 2. Ready: 3.",
             "Place 3 at position 3. That frees 4 and 6. Ready: 4 and 6.",
             "Place 4 at position 4. That frees 5. Ready: 6 and 5.",
             "Place 6 at position 5. Ready: 5.",
             "Place 5 at position 6.",
             "Every node is placed and every edge points forward: 1, 2, 3, 4, 6, 5."],
            steps.Select(s => s.Caption));
        Assert.Equal([StepEvent.Try, .. Enumerable.Repeat(StepEvent.Accept, 6), StepEvent.Done], steps.Select(s => s.Event));
        Assert.Equal(["2"], steps[2].Focus);
        Assert.Equal(new Tour(["1", "2"]), steps[2].Partial);
    }

    [Fact]
    public void Kahn_OnACycle_RejectsTheNodesItCannotPlace() {
        var steps = Steps(new KahnsAlgorithm(), "({1,2,3,4,5},{(1,2),(2,3),(3,4),(4,2),(3,5)})");
        var reject = steps[^2];
        Assert.Equal(StepEvent.Reject, reject.Event);
        Assert.Equal("2, 3, 4 and 5 never lose their last incoming edge, because of a cycle.", reject.Caption);
        Assert.Equal(["2", "3", "4", "5"], reject.Focus);
        Assert.Equal(new Tour(["1"]), reject.Partial);
        Assert.Equal((new Tour([]), false), ((Tour)steps[^1].Partial, steps[^1].Ok!.Value));
    }

    // ---- Feedback Arc Set and Node Set ----

    private const string FeedbackDefault = "(({1,2,3,4,5},{(1,2),(2,3),(3,1),(4,5),(5,2),(3,4)}),1)";

    [Fact]
    public void ArcSet_TriesEachEdge_NamingTheCycleThatSurvives() {
        var steps = Steps(new ArcSetBruteForce(), FeedbackDefault);
        // #789: removing nothing is the first try.
        Assert.Equal((StepEvent.Reject, "Try removing nothing: the cycle 1 → 2 → 3 → 1 is there."), (steps[0].Event, steps[0].Caption));
        Assert.Equal(new EdgeSet([]), steps[0].Partial);
        Assert.Empty(steps[0].Focus);
        Assert.Equal("Try removing {(1,2)}: the cycle 2 → 3 → 4 → 5 → 2 is still there.", steps[1].Caption);
        Assert.Equal(StepEvent.Reject, steps[1].Event);
        Assert.Equal(["1>2"], steps[1].Focus);
        Assert.Equal((StepEvent.Accept, "Try removing {(2,3)}. No cycle is left."), (steps[^2].Event, steps[^2].Caption));
        Assert.Equal(new EdgeSet(["2>3"]), steps[^1].Partial);
        Assert.Equal("Removing {(2,3)} leaves no cycle, within K = 1. Found after 3 tries.", steps[^1].Caption);
    }

    [Fact]
    public void ArcSet_WithNoCycleAndKZero_RemovesNothing() {
        var steps = Steps(new ArcSetBruteForce(), "(({1,2,3},{(1,2),(2,3)}),0)");
        Assert.Equal(2, steps.Count);
        Assert.Equal((StepEvent.Accept, "Try removing nothing. No cycle is left."), (steps[0].Event, steps[0].Caption));
        Assert.Equal(new EdgeSet([]), steps[0].Partial);
        Assert.Empty(steps[0].Focus);
        var done = steps[1];
        Assert.Equal((new EdgeSet([]), true, "The graph has no cycle, so removing nothing is enough. Found after 1 try."), ((EdgeSet)done.Partial, done.Ok!.Value, done.Caption));
    }

    // #789: with K >= 1 an acyclic graph used to get an edge or node it did not need to remove; removing nothing is tried
    // first and accepted.
    [Theory]
    [InlineData("arcsetbruteforce")]
    [InlineData("nodesetbruteforce")]
    public void FeedbackSets_WithNoCycleAndKOne_AcceptRemovingNothingFirst(string solverName) {
        const string instance = "(({1,2,3,4,5,6},{(2,6),(1,2),(2,3),(1,3)}),1)";
        var steps = Steps(solverName == "arcsetbruteforce" ? new ArcSetBruteForce() : new NodeSetBruteForce(), instance);
        Assert.Equal(2, steps.Count);
        Assert.Equal((StepEvent.Accept, "Try removing nothing. No cycle is left."), (steps[0].Event, steps[0].Caption));
        Assert.Empty(steps[0].Focus);
        Assert.Equal((StepEvent.Done, true), (steps[1].Event, steps[1].Ok!.Value));
    }

    [Fact]
    public void NodeSet_TriesEachNode_NamingTheCycleThatSurvives() {
        var steps = Steps(new NodeSetBruteForce(), FeedbackDefault);
        Assert.Equal((StepEvent.Reject, "Try removing nothing: the cycle 1 → 2 → 3 → 1 is there."), (steps[0].Event, steps[0].Caption));
        Assert.Equal(new NodeSet([]), steps[0].Partial);
        Assert.Equal("Try removing {1}: the cycle 2 → 3 → 4 → 5 → 2 is still there.", steps[1].Caption);
        Assert.Equal(new NodeSet(["2"]), steps[^1].Partial);
        Assert.Equal(["2"], steps[^2].Focus);
        Assert.True(steps[^1].Ok);
    }

    [Fact]
    public void NodeSet_ACandidateThatFailsEverywhere_EndsNotOk() {
        var steps = Steps(new NodeSetBruteForce(), "(({1,2,3,4,5,6},{(1,2),(2,3),(3,1),(4,5),(5,6),(6,4),(3,4)}),1)");
        Assert.Equal("Checked every way to remove up to K = 1 node (7 tries). A cycle always survives.", steps[^1].Caption);
        Assert.False(steps[^1].Ok);
    }

    // ---- Strongly Connected Components ----

    [Fact]
    public void Kosaraju_FirstNumbersTheNodes_ThenCollectsEachComponent() {
        var steps = Steps(new KosarajuSolver(), "({1,2,3,4,5},{(1,2),(2,3),(3,1),(3,4),(4,5),(5,4)})");
        Assert.Equal(
            ["In pass 1, visit 1.", "In pass 1, visit 2.", "In pass 1, visit 3.", "In pass 1, visit 4.", "In pass 1, visit 5.",
             "In pass 1, 5 is finished (number 1).", "In pass 1, 4 is finished (number 2).", "In pass 1, 3 is finished (number 3).",
             "In pass 1, 2 is finished (number 4).", "In pass 1, 1 is finished (number 5).",
             "In pass 2, 1 has the highest finish number left, so it starts component 1.",
             "In pass 2, follow the edge 3 → 1 backward, so 3 joins component 1.",
             "In pass 2, follow the edge 2 → 3 backward, so 2 joins component 1.",
             "In pass 2, 4 has the highest finish number left, so it starts component 2.",
             "In pass 2, follow the edge 5 → 4 backward, so 5 joins component 2.",
             "2 strongly connected components: {1,2,3}, {4,5}."],
            steps.Select(s => s.Caption));
        Assert.Equal(["3", "3>1"], steps[11].Focus);
        var done = (Components)steps[^1].Partial;
        Assert.Equal(new Dictionary<string, int> { ["1"] = 0, ["2"] = 0, ["3"] = 0, ["4"] = 1, ["5"] = 1 }, done.Of);
        Assert.Equal(["5", "4", "3", "2", "1"], done.Finished);
    }

    // ---- Shortest paths ----

    private const string ShortestDefault = "({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)},1";

    [Fact]
    public void Sssp_SettlesOneNodePerStep_AndRecordsTheEdgesItRelaxed() {
        var steps = Steps(new SSSPSolver(), ShortestDefault + ")");
        Assert.Equal("Start at 1 with distance 0. Every other node starts at ∞.", steps[0].Caption);
        Assert.Equal("Settle 1 at distance 0. Shorter routes found: 2: ∞ → 4, 3: ∞ → 2.", steps[1].Caption);
        Assert.Equal(["1", "1>2", "1>3"], steps[1].Focus);
        var afterFirst = (Distances)steps[1].Partial;
        Assert.Equal(["1"], afterFirst.Settled);
        Assert.Equal(new Dictionary<string, int> { ["1"] = 0, ["2"] = 4, ["3"] = 2 }, afterFirst.Dist);
        var done = (Distances)steps[^1].Partial;
        Assert.Equal(new Dictionary<string, int> { ["1"] = 0, ["2"] = 4, ["3"] = 2, ["4"] = 7, ["5"] = 9 }, done.Dist);
        Assert.Equal("Every node that can be reached is settled; the shortest paths from 1 form a tree.", steps[^1].Caption);
    }

    [Fact]
    public void Sssp_Undirected_NamesTheUndirectedEdge() {
        var steps = Steps(new SSSPSolver(), "({1,2,3},{({1,2},1),({2,3},1)},3)");
        Assert.Equal(["3", "2-3"], steps[1].Focus);
        Assert.Equal(["2", "1-2"], steps[2].Focus);
    }

    [Fact]
    public void Sssp_ParallelEdges_RelaxTheMergedEdgeOnce() {
        var steps = Steps(new SSSPSolver(), "({1,2,3},{((1,2),5),((1,2),2),((2,3),1),((2,1),7)},1)");
        Assert.Equal(["1", "1>2"], steps[1].Focus);
        Assert.Equal(2, ((Distances)steps[1].Partial).Dist["2"]);
    }

    [Fact]
    public void Spsp_StopsWhenTheTargetIsSettled() {
        var steps = Steps(new SPSPSolver(), ShortestDefault + ",5)");
        Assert.Equal("Settle 5 at distance 9. It is the target, so the search stops.", steps[^2].Caption);
        Assert.Equal("The shortest path is 1 → 3 → 5, with total 9.", steps[^1].Caption);
        Assert.True(steps[^1].Ok);

        var lost = Steps(new SPSPSolver(), "({1,2,3,4},{((1,2),1),((3,4),1)},1,4)");
        Assert.Equal("4 can't be reached from 1, so there is no path.", lost[^1].Caption);
        Assert.False(lost[^1].Ok);
    }
}
