using API.Interfaces;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The picture doctor: one place to ask "why is this picture wrong?" for a frames visualization, with every finding in plain
// words saying what to fix and where. It runs over every frames visualization in the normal suite; to look at one, name it:
//
//   REDUX_DOCTOR=SetCoverDefaultVisualization dotnet test redux-tests --filter "FullyQualifiedName~PictureDoctor"
//
// For each picture it checks, in order (it reuses the contract tests' pieces, so they agree):
//   1. it declares a step shape (StepShape_Tests);
//   2. some solver of its problem records that shape, or it is on StepShape_Tests.NoRecordingSolver;
//   3. its Describe gives a valid spec on the problem's default instance (the builder's Validate, and nothing else throws);
//   4. on real runs, its final frame agrees with the problem's verifier (VerifierAgreement_Tests, VerdictRules).
public class PictureDoctor_Tests {
    private const string Variable = "REDUX_DOCTOR";

    public static TheoryData<string> Patients() {
        string? one = Environment.GetEnvironmentVariable(Variable);
        return string.IsNullOrWhiteSpace(one)
            ? new TheoryData<string>(PictureContract.Pictures.Select(p => p.Name))
            : new TheoryData<string>(one.Trim());
    }

    [Theory]
    [MemberData(nameof(Patients))]
    public void Examine(string name) {
        var findings = Findings(name, alone: !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Variable)));
        Assert.True(findings.Count == 0, $"The picture doctor found {findings.Count} problem(s) with {name}:\n- " + string.Join("\n- ", findings));
    }

    /// <summary>
    /// Every finding for the frames visualization named <paramref name="name"/>, in plain words; empty when it is healthy.
    /// <paramref name="alone"/> builds its runs on their own (one picture asked for) instead of reading the shared ones.
    /// </summary>
    internal static List<string> Findings(string name, bool alone) {
        var findings = new List<string>();
        var picture = PictureContract.Find(name);
        if (picture == null) {
            findings.Add($"There is no frames visualization named {name}. Name the class (for example SetCoverDefaultVisualization); " +
                         "a visualization without a picture interface (IFramesVisualization) has no frames to examine.");
            return findings;
        }
        var vis = picture.Visualization;
        string file = $"the {picture.Problem.Name} folder's Visualizations/{name}.cs";

        // 1. A step shape.
        if (vis.StepShape == null) {
            findings.Add($"{name} declares no StepShape, so every solver's steps are dropped and it only ever shows the answer. " +
                         $"Implement its picture interface, which declares it, or declare Type? IVisualization.StepShape in {file}.");
        }

        // 2. A solver that records it.
        var recorders = PictureContract.RecordersOf(picture);
        if (vis.StepShape != null && recorders.Count == 0 && !StepShape_Tests.NoRecordingSolver.ContainsKey(name)) {
            var solvers = PictureContract.SolversOf(picture);
            findings.Add($"No solver of {picture.Problem.Name} records {vis.StepShape.Name} steps, so {name} only shows answers. Its solvers " +
                         $"record: {(solvers.Count == 0 ? "nothing (it has none)" : string.Join(", ", solvers.Select(s => $"{s.Name} ({s.Shape?.Name ?? "no typed steps"})")))}. " +
                         "Give a solver ISolver<TProblem, TPartial> with that shape (adding-a-solver.md), or list the picture in " +
                         "StepShape_Tests.NoRecordingSolver with the reason.");
        }

        // 3. A valid spec on the default instance: build its frames for an answer-only run, which runs Describe and the
        // builder's checks of the spec.
        string instance = PictureContract.DefaultInstance(picture.Problem);
        try {
            vis.BuildFrames(instance, new SolveRun("", [], null));
        } catch (Exception e) {
            findings.Add($"Building {name}'s frames for the default instance {PictureRuns.Short(instance)} threw {e.GetType().Name}: {e.Message} " +
                         $"Look at its Describe in {file}: which problem fields it passes, and in which place.");
        }

        // 4. Agreement with the verifier, on real runs.
        var runs = (alone ? PictureRuns.For([picture]) : PictureRuns.All()).Where(r => r.Picture.Name == name).ToList();
        var judged = runs.Where(r => !r.TimedOut && r.Error?.StartsWith("solver threw", StringComparison.Ordinal) != true).ToList();
        if (judged.Count == 0)
            findings.Add($"No run of {name} could be judged: no solver it draws finished on any instance ({runs.Count} tried).");
        foreach (var run in judged)
            foreach (string problem in VerdictRules.Problems(run))
                findings.Add($"{run.Where}: {problem}. " +
                             $"The picture disagrees with the verifier: check Describe in {file} (a field in the wrong place, the " +
                             $"wrong rule or K), then the picture type's state rules (VerdictRules lists what each type must show).");
        return findings;
    }
}
