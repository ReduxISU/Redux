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
    };

    // The answer shapes a Graph picture draws.
    private static readonly Type[] GraphShapes = [typeof(NodeSet)];

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
        var problemType = type.GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISolver<>)).GetGenericArguments()[0];
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

    // #746 review: on the path a-b-c, a Try is a set still being built, so a vertex it doesn't dominate yet can still be
    // taken and must not be drawn as Rejected. Only Reject and Done steps judge the set.
    [Theory]
    // The reviewer's path a-b-c, and for the forced-vertex search a graph where it tries after it has taken a vertex.
    [InlineData("dominatingsetnaiveinclusionexclusion", "(({a,b,c},{{a,b},{b,c}}),1)")]
    [InlineData("dominatingsetforcedvertex", "(({a,b,c},{{a,b},{b,c}}),1)")]
    [InlineData("dominatingsetforcedvertex", "(({a,b,c,d,e},{{a,b},{c,d}}),2)")]
    [InlineData("dominatingsetforcedvertex", "(({a,b,c,d,e,f},{{a,b},{b,c},{d,e},{e,f}}),2)")]
    public void DominatingSet_Try_NeverRejectsAVertexThatCanStillBeTaken(string solverName, string instance) {
        var (steps, frames) = SubsetRun(solverName, "dominatingsetdefaultvisualization", instance);
        var tries = steps.Select((s, i) => (Step: s, Frame: frames[i])).Where(p => p.Step.Event == StepEvent.Try).ToList();
        Assert.NotEmpty(tries);
        foreach (var (_, frame) in tries)
            Assert.DoesNotContain(frame.Nodes.Values, s => s.State == PictureState.Rejected);
    }
}
