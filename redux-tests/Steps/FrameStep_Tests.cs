using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Steps;
using API.Interfaces.Graphs;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Every frame of every format=frames picture says which solver step it was drawn from (#895): "step", the 0-based index in
// the run's full step list, always written, null only on a frame drawn from no step (an added solved or stopped frame).
// Checked for every visualization with a frames hook, on its default instance and solver, so every picture type's builder is
// covered by name.
public class FrameStep_Tests {
    public static TheoryData<string> FramesVisualizations {
        get {
            var data = new TheoryData<string>();
            foreach (var (_, type) in ProblemProvider.Visualizers.OrderBy(v => v.Key, StringComparer.Ordinal))
                if (type.Assembly == typeof(IVisualization).Assembly && typeof(IFramesVisualization).IsAssignableFrom(type))
                    data.Add(type.Name);
            return data;
        }
    }

    private static IFramesVisualization Vis(string name) =>
        (IFramesVisualization)Activator.CreateInstance(ProblemProvider.Visualizers[name.ToLower()])!;

    internal static string DefaultInstance(IVisualization vis) {
        Type problem = ProblemProvider.ProblemOfVisualization(vis.GetType())!;
        return ((IProblem)Activator.CreateInstance(problem)!).defaultInstance;
    }

    private static List<JsonElement> Frames(FramesResponse response) {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(response, FramesResponse.JsonOptions));
        return doc.RootElement.GetProperty("frames").EnumerateArray().Select(f => f.Clone()).ToList();
    }

    private static int? StepOf(JsonElement frame) {
        Assert.True(frame.TryGetProperty("step", out var step), $"A frame has no \"step\": {frame}");
        return step.ValueKind == JsonValueKind.Null ? null : step.GetInt32();
    }

    /// <summary>
    /// The frames' steps are 0, 1, ... in order (every builder draws every step it can draw), then at most one null (the
    /// frame the builder adds); returns how many frames have a step.
    /// </summary>
    internal static int AssertStepsInOrder(IReadOnlyList<JsonElement> frames) {
        var steps = frames.Select(StepOf).ToList();
        int drawn = steps.TakeWhile(s => s != null).Count();
        for (int i = 0; i < drawn; i++) Assert.Equal(i, steps[i]);
        Assert.True(steps.Count - drawn <= 1, $"More than one frame drawn from no step: [{string.Join(",", steps)}]");
        Assert.All(steps.Skip(drawn), s => Assert.Null(s));
        return drawn;
    }

    [Theory]
    [MemberData(nameof(FramesVisualizations))]
    public void DefaultRun_EveryStepIsAFrame_AndEveryFrameSaysItsStep(string visualization) {
        var vis = Vis(visualization);
        string instance = DefaultInstance(vis);
        var run = vis.solver.Run(instance, withSteps: true, TimeSpan.FromSeconds(10));
        var frames = Frames(vis.BuildFrames(instance, run));
        Assert.NotEmpty(frames);
        int drawn = AssertStepsInOrder(frames);
        // A picture that can draw the run's steps draws every one of them, and only those carry an index.
        int drawable = run.StepShape == vis.StepShape ? run.Steps.Count(s => s.GetType().IsGenericType && s.GetType().GetGenericTypeDefinition() == typeof(SolverStep<>)) : 0;
        Assert.Equal(drawable, drawn);
    }

    [Theory]
    [MemberData(nameof(FramesVisualizations))]
    public void RunOfAnotherShape_OnlyAnAddedFrame_WithNoStep(string visualization) {
        var vis = Vis(visualization);
        string instance = DefaultInstance(vis);
        string answer = vis.solver.Run(instance, withSteps: false).Answer;
        // A legacy run: one untyped step that no frames picture can draw.
        var run = new SolveRun(answer, [new object()], null);
        var frames = Frames(vis.BuildFrames(instance, run));
        Assert.Single(frames);
        Assert.Null(StepOf(frames[0]));
    }

    [Theory]
    [MemberData(nameof(FramesVisualizations))]
    public void StoppedRunWithNoSteps_ItsAddedStoppedFrameHasNoStep(string visualization) {
        var vis = Vis(visualization);
        var run = new SolveRun(SolveRun.TimedOutAnswer, [], vis.StepShape) { TimedOut = true };
        var frames = Frames(vis.BuildFrames(DefaultInstance(vis), run));
        Assert.Single(frames);
        Assert.Null(StepOf(frames[0]));
    }

    // ---- The Graph subset builder, by hand ----

    private static readonly GraphSubsetSpec Triangle = new(["a", "b", "c"],
        [new("a", "b"), new("b", "c"), new("a", "c")], 2, SubsetRule.Clique);

    private static FramesResponse BuildTriangle(SolveRun run) =>
        GraphSubsetFrames.Build(Triangle, run, a => new NodeSet(a.Trim('{', '}').Split(',', StringSplitOptions.RemoveEmptyEntries)), _ => true);

    [Fact]
    public void GraphSubset_StepsIndexTheRun_AndTheAddedSolvedFrameHasNone() {
        var run = new SolveRun("{a,b}", [
            new SolverStep<NodeSet>(new NodeSet(["a"]), StepEvent.Try, ["a"], "Try a."),
            new SolverStep<NodeSet>(new NodeSet(["a", "b"]), StepEvent.Accept, ["b"], "Take b."),
        ], typeof(NodeSet));
        var frames = Frames(BuildTriangle(run));
        Assert.Equal([0, 1, null], frames.Select(StepOf).ToArray());
        Assert.Equal("Take b.", frames[1].GetProperty("caption").GetString());
    }

    [Fact]
    public void GraphSubset_StoppedRun_ItsOwnDoneKeepsItsIndex() {
        var run = new SolveRun(SolveRun.TimedOutAnswer, [
            new SolverStep<NodeSet>(new NodeSet(["a"]), StepEvent.Try, ["a"], "Try a."),
            new SolverStep<NodeSet>(new NodeSet([]), StepEvent.Done, [], "Stopped after 1 s and 2 steps; the solver had not finished.") { Ok = false },
        ], typeof(NodeSet)) { TimedOut = true };
        var frames = Frames(BuildTriangle(run));
        Assert.Equal([0, 1], frames.Select(StepOf).ToArray());
    }

    [Fact]
    public void Step_IsWrittenEvenWhenNull_WithAnyOptions() {
        var frame = GraphSubsetFrames.StoppedFrame("x");
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(frame, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull }));
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("step").ValueKind);
    }
}
