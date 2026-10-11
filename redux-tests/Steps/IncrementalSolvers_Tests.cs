using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Which typed solvers build their answer piece by piece (IIncrementalSolver), and what that does to their pictures: the
// part they have committed stays Solution in every step, and only the piece under test (the focus) is highlighted or,
// in a Reject, Rejected. Solvers that judge whole candidates keep the candidate highlighted (see the frames tests).
public class IncrementalSolvers_Tests {
    // Every typed solver whose steps a Graph picture draws, and whether it is incremental. Declared here on purpose: a new
    // solver must be classified (see EveryTypedGraphSolver_IsClassified), never left to a default.
    public static readonly Dictionary<string, bool> Classification = new() {
        // NodeSet (subset pictures)
        ["cliquebruteforce"] = false,
        ["carraghanpardalos"] = true,
        ["chibanishizeki"] = true,
        ["independentsetbruteforce"] = false,
        ["vertexcoverbruteforce"] = false,
        ["vertexcoverbusskernelization"] = false,
        ["vertexcoverboundedsearchtree"] = true,
        ["greedyvertexcover"] = true,
        ["twoapproximationvertexcover"] = true,
        ["bruteforceminimumvertexcover"] = false,
        ["greedyminimumvertexcover"] = true,
        ["twoapproximationminimumvertexcover"] = true,
        ["dominatingsetforcedvertex"] = true,
        ["dominatingsetnaiveinclusionexclusion"] = true,
        // EdgeSet (edges pictures) and Groups (cut pictures)
        ["kruskalsolver"] = true,
        ["primsolver"] = true,
        ["steinertreebruteforce"] = false,
        ["cutbruteforce"] = false,
        ["weightedcutbruteforce"] = false,
        ["maxcutsolver"] = false,
        ["mincutstoerwagner"] = false,
        // Groups (groups pictures)
        ["graphcoloringbruteforce"] = false,
        ["graphcoloringgreedy"] = true,
        ["cliquecoverbruteforce"] = false,
        // Tour (tour pictures)
        ["hamiltonianbruteforce"] = true,
        ["directedhamiltonianbruteforce"] = true,
        ["tspbruteforce"] = false,
        ["tspbranchandbound"] = true,
        ["tspgreedy"] = true,
        ["bruteforceminimumtsp"] = false,
        // Assignment (the clause picture). The SAT brute force records Assignment steps through its own Run, without the
        // typed solver interface.
        ["satbruteforcesolver"] = false,
        ["sat3backtrackingsolver"] = true,
        // ChosenSets and NodeSet (Bipartite pictures)
        ["setcoverbruteforce"] = false,
        ["greedysetcover"] = true,
        ["heuristicsolver"] = true,
        ["exactcoverbruteforce"] = false,
        ["dancinglinks"] = true,
        ["exactcoverrecursive"] = true,
        ["hittingsetbruteforce"] = false,
        // Layered Graph pictures
        ["kahnsalgorithm"] = true,
        ["arcsetbruteforce"] = false,
        ["nodesetbruteforce"] = false,
        ["kosarajusolver"] = true,
        ["ssspsolver"] = true,
        ["spspsolver"] = true,
        // Flow Network pictures
        ["minstcutsolver"] = true,
        // Placement (Packing pictures)
        ["partitionbruteforce"] = false,
        ["subsetsumbruteforce"] = false,
        ["fastapproximation"] = false,
        ["knapsackbruteforce"] = false,
        ["knapsackdp"] = true,
        ["binpackingbruteforce"] = true,
        ["binpackingffd"] = true,
        // TableCells and Assignment (Table pictures)
        ["editdistancedpsolver"] = true,
        ["integerprogrammingbruteforce"] = false,
        // BoardCells (Board pictures)
        ["sudokusolver"] = true,
        ["nqueensbacktracking"] = true,
        ["nqueensconstructive"] = true,
    };

    // The answer shapes a Graph, Grouped Graph, Bipartite, Layered Graph, Flow Network, Packing, Table or Board picture draws.
    private static readonly Type[] GraphShapes = [typeof(NodeSet), typeof(EdgeSet), typeof(Groups), typeof(Tour), typeof(Assignment), typeof(ChosenSets), typeof(Components), typeof(Distances), typeof(NetworkFlow), typeof(Placement), typeof(TableCells), typeof(BoardCells)];

    private static Type? StepShapeOf(Type solver) =>
        solver.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISolver<,>))?.GetGenericArguments()[1];

    public static TheoryData<string, bool> Classified {
        get {
            var data = new TheoryData<string, bool>();
            foreach (var (name, incremental) in Classification) data.Add(name, incremental);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Classified))]
    public void Solver_DeclaresWhetherItIsIncremental_AndItsRunSaysSo(string solverName, bool incremental) {
        var type = ProblemProvider.Solvers[solverName];
        Assert.Equal(incremental, typeof(IIncrementalSolver).IsAssignableFrom(type));
        var solver = (ISolver)Activator.CreateInstance(type)!;
        var problemType = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISolver<>))?.GetGenericArguments()[0]
            ?? typeof(API.Problems.NPComplete.NPC_SAT.SAT);   // the SAT brute force is the one untyped solver listed
        string instance = ((IProblem)Activator.CreateInstance(problemType)!).defaultInstance;
        Assert.Equal(incremental, solver.Run(instance, withSteps: true).Incremental);
        Assert.Equal(incremental, solver.Run(instance, withSteps: false).Incremental);
    }

    [Fact]
    public void EveryTypedGraphSolver_IsClassified() {
        var typed = ProblemProvider.Solvers.Where(s => StepShapeOf(s.Value) is { } shape && GraphShapes.Contains(shape)).Select(s => s.Key);
        Assert.Empty(typed.Except(Classification.Keys));
    }

    [Fact]
    public void ALegacyRun_AndAHandBuiltRun_AreNotIncremental() {
        Assert.False(new SolveRun("{}", [], null).Incremental);
        Assert.False(new SolveRun("{}", [], typeof(NodeSet)).Incremental);
    }

    // ---- Subset pictures, from real runs ----

    private static (List<SolverStep<NodeSet>> Steps, List<GraphFrame> Frames) SubsetRun(string solverName, string visualizationName, string instance) {
        var solver = (ISolver)Activator.CreateInstance(ProblemProvider.Solvers[solverName])!;
        var run = solver.Run(instance, withSteps: true);
        Assert.True(run.Incremental);
        var vis = (IFramesVisualization)Activator.CreateInstance(ProblemProvider.Visualizers[visualizationName])!;
        var frames = vis.BuildFrames(instance, run).Frames.Cast<GraphFrame>().ToList();
        return (run.Steps.Cast<SolverStep<NodeSet>>().ToList(), frames);
    }

    [Theory]
    // Bound-skips a vertex (Reject) with the clique so far as the partial.
    [InlineData("carraghanpardalos", "cliquedefaultvisualization", "(({1,2,3,4,5,6},{{4,1},{1,2},{4,3},{3,2},{2,4},{5,2},{3,5},{5,4},{3,6},{6,4},{1,6}}),4)")]
    // Skips a vertex the count rules out (Reject) with the clique so far as the partial.
    [InlineData("chibanishizeki", "cliquedefaultvisualization", "(({a,b,c,d,e},{{a,b},{a,c},{a,d},{a,e},{b,c},{c,d}}),4)")]
    // Runs out of budget (Reject) with the cover so far as the partial.
    [InlineData("vertexcoverboundedsearchtree", "vertexcoverdefaultvisualization", "(({a,b,c,d,e},{{a,b},{a,c},{a,e},{b,e},{c,d}}),2)")]
    // Leaves vertices out (Try) and rejects complete subsets that miss a vertex (Reject).
    [InlineData("dominatingsetnaiveinclusionexclusion", "dominatingsetdefaultvisualization", "(({a,b,c,d},{{a,b},{b,c},{c,d}}),1)")]
    [InlineData("dominatingsetforcedvertex", "dominatingsetdefaultvisualization", "(({a,b,c,d,e},{{a,b},{c,d}}),2)")]
    public void Subset_TriedAndRejectedSteps_KeepTheCommittedSetSolution_AndMarkOnlyTheFocus(string solverName, string visualizationName, string instance) {
        var (steps, frames) = SubsetRun(solverName, visualizationName, instance);
        var checkedSteps = steps.Select((s, i) => (Step: s, Frame: frames[i])).Where(p => p.Step.Event is StepEvent.Try or StepEvent.Reject).ToList();
        Assert.Contains(checkedSteps, p => p.Step.Event == StepEvent.Reject);
        foreach (var (step, frame) in checkedSteps) {
            foreach (var node in step.Partial.Nodes.Except(step.Focus))
                Assert.Equal(PictureState.Solution, frame.Nodes[node].State);
            var mark = step.Event == StepEvent.Reject ? PictureState.Rejected : PictureState.ElementHighlight;
            foreach (var id in step.Focus)
                Assert.Equal(mark, frame.Nodes[id].State);
        }
    }

    // ---- Edges pictures, from real runs ----

    [Theory]
    // 1-3 would close a cycle once 1-2 and 2-3 are in the tree.
    [InlineData("({1,2,3,4},{({1,2},1),({2,3},1),({1,3},3),({3,4},5)})")]
    [InlineData("({a,b,c,d,e},{({a,b},1),({b,c},1),({a,c},1),({c,d},2),({b,d},2),({d,e},3),({a,e},4)})")]
    public void Kruskal_ASkippedEdge_IsRejected_AndTheTreeSoFarStaysSolution(string instance) {
        var run = ((ISolver)new API.Problems.NPComplete.NPC_MINIMUMSPANNINGTREE.Solvers.KruskalSolver()).Run(instance, withSteps: true);
        Assert.True(run.Incremental);
        var vis = (IFramesVisualization)Activator.CreateInstance(ProblemProvider.Visualizers["minimumspanningtreevisualization"])!;
        var frames = vis.BuildFrames(instance, run).Frames.Cast<GraphFrame>().ToList();
        var steps = run.Steps.Cast<SolverStep<EdgeSet>>().ToList();
        var rejects = steps.Select((s, i) => (Step: s, Frame: frames[i])).Where(p => p.Step.Event == StepEvent.Reject).ToList();
        Assert.NotEmpty(rejects);
        foreach (var (step, frame) in rejects) {
            Assert.NotEmpty(step.Partial.Edges);
            foreach (var edge in step.Partial.Edges) Assert.Equal(PictureState.Solution, frame.Edges[edge].State);
            Assert.Equal(PictureState.Rejected, frame.Edges[Assert.Single(step.Focus)].State);
        }
    }

    // ---- Groups pictures, from real runs ----

    [Fact]
    public void GreedyColoring_ANodeThatNeedsTooManyColors_IsRejected_AndTheColoringSoFarIsKept() {
        // A triangle with K = 2: the greedy colors a and b, then c needs color 2.
        const string instance = "(({a,b,c},{{a,b},{b,c},{a,c}}),2)";
        var run = ((ISolver)new API.Problems.NPComplete.NPC_GRAPHCOLORING.Solvers.GraphColoringGreedy()).Run(instance, withSteps: true);
        Assert.True(run.Incremental);
        var vis = (IFramesVisualization)Activator.CreateInstance(ProblemProvider.Visualizers["graphcoloringdefaultvisualization"])!;
        var frames = vis.BuildFrames(instance, run).Frames.Cast<GraphFrame>().ToList();
        var steps = run.Steps.Cast<SolverStep<Groups>>().ToList();
        int at = steps.FindIndex(s => s.Event == StepEvent.Reject);
        Assert.True(at >= 0);
        Assert.Equal(PictureState.Rejected, frames[at].Nodes[Assert.Single(steps[at].Focus)].State);
        // The nodes colored before it keep their groups and are not marked as tried.
        foreach (var node in steps[at].Partial.Of.Keys.Except(steps[at].Focus)) {
            Assert.True(frames[at].Groups!.ContainsKey(node));
            Assert.False(frames[at].Nodes.TryGetValue(node, out var state) && state.State == PictureState.ElementHighlight);
        }
    }

    // ---- Tour pictures, from real runs ----

    // Every Try and Reject frame of an incremental tour run: the hops of the path so far that the focus does not name are
    // Solution, the nodes on it that the focus does not name are Covered, and the focus is marked (Rejected in a Reject).
    [Theory]
    [InlineData("hamiltonianbruteforce", "hamiltoniandefaultvisualization", "({1,2,3,4,5},{{1,2},{1,3},{2,4},{3,4},{4,5},{3,5}})")]
    [InlineData("directedhamiltonianbruteforce", "directedhamiltoniandefaultvisualization", null)]
    [InlineData("tspgreedy", "tspdefaultvisualization", null)]
    [InlineData("tspbranchandbound", "tspdefaultvisualization", null)]
    public void Tour_TriedAndRejectedSteps_KeepThePathSoFar_AndMarkOnlyTheFocus(string solverName, string visualizationName, string? instance) {
        var type = ProblemProvider.Solvers[solverName];
        var problemType = type.GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISolver<>)).GetGenericArguments()[0];
        instance ??= ((IProblem)Activator.CreateInstance(problemType)!).defaultInstance;
        var run = ((ISolver)Activator.CreateInstance(type)!).Run(instance, withSteps: true);
        Assert.True(run.Incremental);
        var vis = (IFramesVisualization)Activator.CreateInstance(ProblemProvider.Visualizers[visualizationName])!;
        var frames = vis.BuildFrames(instance, run).Frames.Cast<GraphFrame>().ToList();
        var steps = run.Steps.Cast<SolverStep<Tour>>().ToList();
        var payloadEdges = ((GraphPayload)vis.BuildFrames(instance, run).Payload).Edges.Select(e => e.Id).ToHashSet();
        bool directed = ((GraphPayload)vis.BuildFrames(instance, run).Payload).Subtype == "directed";
        var checkedSteps = steps.Select((s, i) => (Step: s, Frame: frames[i])).Where(p => p.Step.Event is StepEvent.Try or StepEvent.Reject).ToList();
        Assert.Contains(checkedSteps, p => p.Step.Event == StepEvent.Reject);
        foreach (var (step, frame) in checkedSteps) {
            var order = step.Partial.Order;
            for (int i = 0; i + 1 < order.Length; i++) {
                string id = GraphTourFrames.EdgeId(directed, order[i], order[i + 1]);
                if (payloadEdges.Contains(id) && !step.Focus.Contains(id)) Assert.Equal(PictureState.Solution, frame.Edges[id].State);
            }
            foreach (var node in order.Except(step.Focus)) Assert.Equal(PictureState.Covered, frame.Nodes[node].State);
            var mark = step.Event == StepEvent.Reject ? PictureState.Rejected : PictureState.ElementHighlight;
            foreach (var id in step.Focus) Assert.Equal(mark, (frame.Nodes.TryGetValue(id, out var n) ? n : frame.Edges[id]).State);
        }
    }

    // ---- The clause picture, from a real run ----

    [Fact]
    public void Sat3Backtracking_TheDecidedVariablesStaySolution_AndOnlyTheTriedOneIsHighlighted() {
        // Unsatisfiable, so the search tries and rejects values with earlier variables decided.
        const string instance = "(x1 | x2) & (!x1 | x2) & (x1 | !x2) & (!x1 | !x2)";
        var run = ((ISolver)new API.Problems.NPComplete.NPC_SAT3.Solvers.Sat3BacktrackingSolver()).Run(instance, withSteps: true);
        Assert.True(run.Incremental);
        var vis = (IFramesVisualization)Activator.CreateInstance(ProblemProvider.Visualizers["sat3defaultvisualization"])!;
        var response = vis.BuildFrames(instance, run);
        var payload = (GroupedGraphPayload)response.Payload;
        var frames = response.Frames.Cast<GroupedGraphFrame>().ToList();
        var steps = run.Steps.Cast<SolverStep<Assignment>>().ToList();
        var checkedSteps = steps.Select((s, i) => (Step: s, Frame: frames[i]))
            .Where(p => p.Step.Event is StepEvent.Try or StepEvent.Reject && p.Step.Partial.Values.Count > 1).ToList();
        Assert.Contains(checkedSteps, p => p.Step.Event == StepEvent.Reject);
        foreach (var (step, frame) in checkedSteps) {
            string tried = step.Focus.Single(f => f.StartsWith("v:"));
            foreach (var node in payload.Nodes) {
                if (!frame.Assignment.TryGetValue(node.Variable, out bool value)) continue;
                bool isTrue = value == node.Positive;
                frame.Nodes.TryGetValue(node.Id, out var state);
                if (state?.State == PictureState.Rejected) continue;   // a literal of the clause that failed
                if (!isTrue) Assert.Equal(PictureState.False, state?.State);
                else if (node.Variable == tried) Assert.Equal(PictureState.ElementHighlight, state?.State);
                else Assert.Equal(PictureState.Solution, state?.State);
            }
        }
    }

    // ---- Bipartite pictures, from real runs ----

    [Theory]
    // Picks the set covering the most new elements (Accept), then is over K (Reject naming the sets past K).
    [InlineData("greedysetcover", "setcoverdefaultvisualization", "({1,2,3,4,5,6},{{1,2,3,4},{1,2,5},{3,4,6}},2)")]
    // Takes a set (Accept), goes over budget (Reject naming the set), undoes it (Backtrack).
    [InlineData("heuristicsolver", "setcoverdefaultvisualization", "({1,2,3,4,5},{{1,2},{2,3},{3,4},{4,5},{1,5}},3)")]
    // Reaches an element no set can cover (Reject naming the element) and backs out.
    [InlineData("dancinglinks", "exactcoverdefaultvisualization", "({1,2,3,4,5,6,7},{{1,4,7},{1,4},{4,5,7},{3,5,6},{2,3,6,7},{2,7}})")]
    [InlineData("exactcoverrecursive", "exactcoverdefaultvisualization", "({1,2,3,4,5,6,7},{{1,4,7},{1,4},{4,5,7},{3,5,6},{2,3,6,7},{2,7}})")]
    public void Bipartite_Steps_KeepTheCommittedSetsSolution_AndMarkOnlyTheFocus(string solverName, string visualizationName, string instance) {
        var solver = (ISolver)Activator.CreateInstance(ProblemProvider.Solvers[solverName])!;
        var run = solver.Run(instance, withSteps: true);
        Assert.True(run.Incremental);
        var vis = (IFramesVisualization)Activator.CreateInstance(ProblemProvider.Visualizers[visualizationName])!;
        var frames = vis.BuildFrames(instance, run).Frames.Cast<BipartiteFrame>().ToList();
        var steps = run.Steps.Cast<SolverStep<ChosenSets>>().ToList();
        Assert.Contains(steps, s => s.Event == StepEvent.Reject);
        foreach (var (step, frame) in steps.Zip(frames).Where(p => p.First.Event != StepEvent.Done)) {
            foreach (var set in step.Partial.Sets.Except(step.Focus))
                Assert.Equal(PictureState.Solution, frame.Nodes[set].State);
            var mark = step.Event == StepEvent.Reject ? PictureState.Rejected : PictureState.ElementHighlight;
            foreach (var id in step.Focus)
                Assert.Equal(mark, frame.Nodes[id].State);
        }
    }
}
