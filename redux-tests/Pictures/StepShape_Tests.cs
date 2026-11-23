using System.Text.Json;
using API.Interfaces;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The step shape contract of every frames picture. The endpoints decide which runs a picture can draw by its declared
// IVisualization.StepShape, and hand it no steps when the run's shape differs, so a picture with a wrong or missing shape does
// not fail: it quietly shows only the answer. These tests make that loud:
//
// - every picture declares a shape (most picture interfaces declare it for their visualizations; Bipartite, Nested Set and
//   Layered leave it abstract, so forgetting it is a compile error);
// - the builder reads the shape that is declared: a solver recording it gets its steps drawn;
// - some solver of the picture's problem records that shape, unless the picture is on the allow-list below with a reason.
public class StepShape_Tests {
    // Pictures with no solver that records their step shape yet. Each is allowed only for the reason given; remove an entry once
    // a solver records the shape (Every_Picture_Has_A_Solver_That_Records_Its_Shape fails on a stale entry).
    internal static readonly Dictionary<string, string> NoRecordingSolver = new(StringComparer.Ordinal) {
    };

    public static TheoryData<string> PictureNames() => new(PictureContract.Pictures.Select(p => p.Name));

    [Theory]
    [MemberData(nameof(PictureNames))]
    public void Every_Picture_Declares_A_Step_Shape(string name) {
        var picture = PictureContract.Find(name)!;
        Assert.True(picture.Visualization.StepShape != null,
            $"{name} declares no StepShape, so every run's steps are dropped and it shows only the answer. " +
            "Implement its picture interface (which declares the shape), or declare Type? IVisualization.StepShape.");
    }

    [Theory]
    [MemberData(nameof(PictureNames))]
    public void Every_Picture_Has_A_Solver_That_Records_Its_Shape(string name) {
        var picture = PictureContract.Find(name)!;
        var recorders = PictureContract.RecordersOf(picture);
        if (NoRecordingSolver.ContainsKey(name)) {
            Assert.True(recorders.Count == 0,
                $"{name} is on StepShape_Tests.NoRecordingSolver, but {string.Join(", ", recorders.Select(r => r.Name))} now records " +
                $"its {picture.Visualization.StepShape?.Name} steps. Remove the entry.");
            return;
        }
        Assert.True(recorders.Count > 0,
            $"No solver of {picture.Problem.Name} records {picture.Visualization.StepShape?.Name} steps, so {name} only ever shows the answer. " +
            $"Its solvers record: {Describe(PictureContract.SolversOf(picture))}. Record that shape in a solver " +
            "(ISolver<TProblem, TPartial>, see adding-a-solver.md), or add the picture to StepShape_Tests.NoRecordingSolver with the reason.");
    }

    [Theory]
    [MemberData(nameof(PictureNames))]
    public void The_Builder_Draws_The_Steps_Of_The_Declared_Shape(string name) {
        var picture = PictureContract.Find(name)!;
        var recorders = PictureContract.RecordersOf(picture);
        Assert.SkipWhen(recorders.Count == 0, $"No solver records {name}'s shape (see the allow-list).");
        using var quantum = FakeQuantumServer.Install(out _);
        string instance = PictureContract.DefaultInstance(picture.Problem);
        var tried = new List<string>();
        foreach (var recorder in recorders) {
            var run = PictureContract.NewSolver(recorder.Type).Run(instance, withSteps: true);
            if (run.Steps.Count == 0) {
                tried.Add($"{recorder.Name} (no steps)");
                continue;
            }
            Assert.Equal(picture.Visualization.StepShape, run.StepShape);
            var frames = PictureContract.Json(picture.Visualization.BuildFrames(instance, run)).GetProperty("frames");
            bool drewAStep = frames.EnumerateArray().Any(f => f.GetProperty("step").ValueKind == JsonValueKind.Number);
            Assert.True(drewAStep,
                $"{name} declares {picture.Visualization.StepShape?.Name} steps and {recorder.Name} recorded {run.Steps.Count} of them on the " +
                "default instance, but no frame was drawn from a step: its builder reads another shape than the one declared. " +
                "Make the builder read the declared StepShape.");
            return;
        }
        Assert.Fail($"No solver of {name}'s shape recorded a step on the default instance: {string.Join(", ", tried)}.");
    }

    private static string Describe(IEnumerable<PictureContract.Solver> solvers) {
        var list = solvers.Select(s => $"{s.Name} ({s.Shape?.Name ?? "no steps"})").ToList();
        return list.Count == 0 ? "none (the problem has no solver)" : string.Join(", ", list);
    }
}
