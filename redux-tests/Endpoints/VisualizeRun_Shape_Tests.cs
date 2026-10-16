using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Steps;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// visualizeRun (#895) with a solver whose steps a picture cannot draw (another StepShape, or untyped steps): the picture draws
// the answer alone (one frame, step null) when IFramesVisualization.DrawsAnswerAlone() says it can, and is listed as
// shape_mismatch when it says it cannot (the Search Tree). Every such pair of a frames visualization and a solver of its
// problem is checked, so each builder's decision is covered by name.
public class VisualizeRun_Shape_Tests {
    // Solvers that need an outside service to run (the quantum ones) are left out; their pictures are checked elsewhere.
    private static bool Runnable(Type solver) => !solver.Name.Contains("Quantum", StringComparison.OrdinalIgnoreCase)
        && !solver.Name.Contains("Grover", StringComparison.OrdinalIgnoreCase);

    /// <summary>The TPartial a typed solver records, or null for one that records untyped steps.</summary>
    private static Type? ShapeOf(Type solver) => solver.GetInterfaces()
        .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISolver<,>))?.GetGenericArguments()[1];

    /// <summary>The problem a solver solves (its <c>ISolver&lt;P&gt;</c>).</summary>
    private static Type? ProblemOf(Type solver) => solver.GetInterfaces()
        .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISolver<>))?.GetGenericArguments()[0];

    private const string None = "(none)";

    /// <summary>
    /// Every (frames visualization, solver of its problem whose steps it cannot draw), the first such solver per visualization,
    /// with whether the visualization draws the answer alone. A placeholder row keeps the theory from being empty on a branch
    /// with no such pair.
    /// </summary>
    public static TheoryData<string, string, bool> MismatchedPairs {
        get {
            var data = new TheoryData<string, string, bool>();
            foreach (var (_, visType) in ProblemProvider.Visualizers.OrderBy(v => v.Key, StringComparer.Ordinal)) {
                if (visType.Assembly != typeof(IVisualization).Assembly) continue;
                if (Activator.CreateInstance(visType) is not IFramesVisualization vis) continue;
                Type? problem = ProblemProvider.ProblemOfVisualization(visType);
                if (problem == null) continue;
                var solver = ProblemProvider.Solvers.Values
                    .Where(s => s.Assembly == typeof(IVisualization).Assembly && Runnable(s))
                    .Where(s => ProblemOf(s) == problem && ShapeOf(s) != vis.StepShape)
                    .OrderBy(s => s.Name, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (solver != null) data.Add(visType.Name, solver.Name, vis.DrawsAnswerAlone());
            }
            if (data.Count == 0) data.Add(None, None, true);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(MismatchedPairs))]
    public void AnotherShape_DrawsTheAnswerAlone_OrIsShapeMismatch(string visualization, string solver, bool drawsAnswerAlone) {
        if (visualization == None) return;
        Type problem = ProblemProvider.ProblemOfVisualization(ProblemProvider.Visualizers[visualization.ToLower()])!;
        string instance = ((IProblem)Activator.CreateInstance(problem)!).defaultInstance;
        var result = Assert.IsType<ContentResult>(new ProblemProvider().visualizeRun(solver, instance, visualization));
        using var doc = JsonDocument.Parse(result.Content!);
        var body = doc.RootElement;
        if (drawsAnswerAlone) {
            Assert.Empty(body.GetProperty("unavailable").EnumerateArray());
            var picture = Assert.Single(body.GetProperty("pictures").EnumerateArray());
            Assert.Equal(visualization, picture.GetProperty("visualization").GetString());
            var frame = Assert.Single(picture.GetProperty("frames").EnumerateArray());
            Assert.Equal(JsonValueKind.Null, frame.GetProperty("step").ValueKind);
            Assert.Equal("Done", frame.GetProperty("event").GetString());
        } else {
            Assert.Empty(body.GetProperty("pictures").EnumerateArray());
            var u = Assert.Single(body.GetProperty("unavailable").EnumerateArray());
            Assert.Equal(visualization, u.GetProperty("visualization").GetString());
            Assert.Equal("shape_mismatch", u.GetProperty("reason").GetString());
            Assert.Contains("cannot show the answer alone", u.GetProperty("detail").GetString());
        }
    }

    [Fact]
    public void OnlyTheSearchTree_CannotDrawTheAnswerAlone() {
        var cannot = ProblemProvider.Visualizers.Values
            .Where(t => t.Assembly == typeof(IVisualization).Assembly)
            .Select(t => Activator.CreateInstance(t))
            .OfType<IFramesVisualization>()
            .Where(v => !v.DrawsAnswerAlone())
            .ToList();
        Assert.All(cannot, v => Assert.Equal("SearchTree", v.BuildFrames(FrameStep_Tests.DefaultInstance(v),
            new SolveRun(SolveRun.TimedOutAnswer, [], v.StepShape) { TimedOut = true }).Type));
    }
}
