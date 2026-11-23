using System.Text.Json;
using API;
using API.Interfaces;
using API.Interfaces.Steps;

namespace redux_tests;
#pragma warning disable CS1591

// What every frames picture promises, found by reflection: the visualizations, the solvers of their problems and the step shape
// each solver records. The contract tests (StepShape_Tests, VerifierAgreement_Tests) and the picture doctor (PictureDoctor_Tests)
// all read these, so they agree on which pictures and which runs there are.
internal static class PictureContract {
    /// <summary>A frames visualization: its class, an instance of it, and the problem it draws.</summary>
    internal sealed record Picture(Type Type, IFramesVisualization Visualization, Type Problem) {
        public string Name => Type.Name;
        public override string ToString() => Name;
    }

    /// <summary>A solver of a picture's problem: its class and the step shape it records (null for one that records none).</summary>
    internal sealed record Solver(Type Type, Type? Shape) {
        public string Name => Type.Name;
    }

    private static readonly Lazy<IReadOnlyList<Picture>> AllPictures = new(() =>
        typeof(IFramesVisualization).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && !t.ContainsGenericParameters && typeof(IFramesVisualization).IsAssignableFrom(t))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .Select(t => new Picture(t, (IFramesVisualization)Activator.CreateInstance(t)!,
                ProblemProvider.ProblemOfVisualization(t) ?? throw new InvalidOperationException($"{t.Name} draws no problem.")))
            .ToList());

    /// <summary>Every non-abstract class that builds its own frames (<see cref="IFramesVisualization"/>), by name.</summary>
    internal static IReadOnlyList<Picture> Pictures => AllPictures.Value;

    /// <summary>The picture whose class is named <paramref name="name"/> (any case).</summary>
    internal static Picture? Find(string name) =>
        Pictures.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

    private static readonly Lazy<Dictionary<Type, Type>> RecordedShapes = new(() =>
        StepCapture_Tests.RecordingSolvers().GroupBy(s => s.Solver).ToDictionary(g => g.Key, g => g.First().Partial));

    /// <summary>
    /// The solvers of <paramref name="problem"/> that can run on it (as the endpoints decide, <see cref="ProblemProvider.SolverMatchesVisualization"/>
    /// with <paramref name="picture"/>), each with the step shape it records: the <c>TPartial</c> of its <c>ISolver&lt;TProblem, TPartial&gt;</c>,
    /// or one recorded through a Run of its own (the capture tool's list). By name.
    /// </summary>
    internal static IReadOnlyList<Solver> SolversOf(Picture picture) =>
        ProblemProvider.Solvers.Values.Distinct()
            .Where(t => ProblemProvider.ProblemTypeOf(t, typeof(ISolver<>)) == picture.Problem)
            .Where(t => !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null)
            .Where(t => ProblemProvider.SolverMatchesVisualization(t, picture.Visualization))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .Select(t => new Solver(t, RecordedShapes.Value.GetValueOrDefault(t)))
            .ToList();

    /// <summary>The solvers of the picture's problem whose recorded steps it draws (same shape as its <see cref="IVisualization.StepShape"/>).</summary>
    internal static IReadOnlyList<Solver> RecordersOf(Picture picture) =>
        SolversOf(picture).Where(s => s.Shape != null && s.Shape == picture.Visualization.StepShape).ToList();

    /// <summary>The problem's default instance.</summary>
    internal static string DefaultInstance(Type problem) => ((IProblem)Activator.CreateInstance(problem)!).defaultInstance;

    /// <summary>A solver ready to run: a randomized one with a fixed seed, so every run is the same.</summary>
    internal static ISolver NewSolver(Type solverType) {
        var seeded = StepCapture_Tests.SeedConstructor(solverType);
        var solver = seeded != null ? (ISolver)seeded.Invoke([1]) : (ISolver)Activator.CreateInstance(solverType)!;
        RandomizedSolvers.Apply(solver, 1);
        return solver;
    }

    /// <summary>The problem's default verifier on an answer; an answer it cannot read is not correct (as the builders judge it).</summary>
    internal static bool Verify(Type problem, string instance, string answer) {
        var p = (IProblem)Activator.CreateInstance(problem)!;
        try { return p.defaultVerifier.verify(instance, answer); } catch { return false; }
    }

    /// <summary>The response as JSON, as the endpoint writes it.</summary>
    internal static JsonElement Json(FramesResponse response) =>
        JsonSerializer.SerializeToElement(response, FramesResponse.JsonOptions);
}
