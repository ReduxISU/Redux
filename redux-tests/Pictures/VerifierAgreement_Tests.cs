using System.Text;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Every frames picture's final frame agrees with the problem's verifier, on real runs: each solver the picture draws, on the
// problem's default instance and the capture tool's corpus (PictureRuns). The verifier is the independent judge, so a wrong
// Describe (a swapped field, a K off by one, the wrong rule) shows up as a picture that calls a right answer wrong or a wrong
// answer right, even when there is no earlier correct output to compare with. What each picture type must show for each verdict
// is in VerdictRules.
//
// A run stopped by the time limit has no answer, and a solver that throws on an instance has nothing to draw; neither is judged.
// Every picture must still have at least one judged run.
//
// Set REDUX_AGREEMENT_DUMP to a file to also write one line per run (verdict, final event, ok, state counts and the final frame):
// the survey the per-type rules were written from.
public class VerifierAgreement_Tests {
    [Fact]
    public void Every_Final_Frame_Agrees_With_The_Verifier() {
        var runs = PictureRuns.All();
        var failures = new List<string>();
        foreach (var run in runs.Where(r => !r.TimedOut && !IsSolverFailure(r)))
            foreach (string problem in VerdictRules.Problems(run))
                failures.Add($"{run.Where} (verifier {(run.Verdict ? "accepts" : "rejects")}): {problem}.");
        Assert.True(failures.Count == 0, $"{failures.Count} final frames disagree with the verifier:\n" + string.Join("\n", failures.Take(40)));
    }

    [Fact]
    public void Every_Picture_Has_A_Judged_Run() {
        var judged = PictureRuns.All().Where(r => !r.TimedOut && r.Final != null).Select(r => r.Picture.Name).ToHashSet();
        var missing = PictureContract.Pictures.Select(p => p.Name).Where(n => !judged.Contains(n)).ToList();
        Assert.True(missing.Count == 0, "No run of these pictures could be judged (no solver finished on any instance): " + string.Join(", ", missing));
    }

    [Fact]
    public void Some_Runs_Are_Accepted_And_Some_Rejected() {
        // The corpus must exercise both verdicts, or a rule for one of them is never tested.
        var runs = PictureRuns.All().Where(r => !r.TimedOut && r.Final != null).ToList();
        Assert.Contains(runs, r => r.Verdict);
        Assert.Contains(runs, r => !r.Verdict);
    }

    [Fact]
    [Trait("Category", "Survey")]
    public void Survey() {
        string? dump = Environment.GetEnvironmentVariable("REDUX_AGREEMENT_DUMP");
        Assert.SkipWhen(string.IsNullOrEmpty(dump), "Set REDUX_AGREEMENT_DUMP to a file to write the survey.");
        var rows = PictureRuns.All().Select(run => string.Join("\t",
            run.Picture.Name, run.Solver.Name, run.InstanceIndex, run.ResponseType, $"verdict={run.Verdict}", $"event={run.FinalEvent}",
            $"ok={run.FinalOk?.ToString() ?? "-"}", $"step={run.FinalStep?.ToString() ?? "-"}", "answer=" + Flat(PictureRuns.Short(run.Answer)),
            string.Join(" ", run.FinalStates.Select(kv => $"{kv.Key}={kv.Value}")),
            run.Error != null ? "ERROR " + Flat(run.Error) : "", "FRAME " + Flat(run.Final?.GetRawText() ?? "")));
        File.WriteAllText(dump!, string.Join("\n", rows) + "\n", new UTF8Encoding(false));
    }

    private static bool IsSolverFailure(PictureRuns.PictureRun run) => run.Error?.StartsWith("solver threw", StringComparison.Ordinal) == true;

    private static string Flat(string s) => s.Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ');
}
