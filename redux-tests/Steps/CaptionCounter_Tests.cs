using System.Text.RegularExpressions;
using API;
using API.Interfaces;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// No step caption starts with a counter of its own ("Try 2: ...", "Flip 3: ...", "Tries 2–37: ...", "Step 5: ..."). The step
// bar already shows "Step N of M", and a caption's own count would not match it: a capped or summarized run skips numbers
// (concern #17). A count that is real information can stay inside the sentence ("found on try 37 of 64", "In pass 2, ...").
//
// Every solver that records typed steps (found as the capture tool finds them) runs on its problem's default instance, with
// steps at the default cap and at a small one, and with fixed seeds when it is random.
public class CaptionCounter_Tests {
    // A counter at the start, optionally after one short sentence ("Restart. Try 2: ..."): a counting word and a number (not
    // a tour like "Try 1 → 2 → 1"), "#3", "(3):" or "3. ".
    private static readonly Regex LeadingCounter = new(
        @"^(?:\w+\.\s+)?(?:(?:Steps?|Try|Tries|Flips?|Rounds?|Pass(?:es)?|Iterations?|Moves?|Quer(?:y|ies)|Calls?|Paths?|Phases?|Restarts?|Attempts?|Trials?|Runs?|Stages?|Turns?)\s+#?\d+(?!\s*→)|#\d|\(\d+\)[:.]|\d+[.:)]\s)",
        RegexOptions.CultureInvariant);

    private static readonly int?[] Caps = [null, 5];
    private static readonly int[] Seeds = [1, 7, 4473];

    [Theory]
    [InlineData("Try 2: start from a new random assignment.")]
    [InlineData("Restart. Try 2: take the variables in a new random order.")]
    [InlineData("Tries 2–37: no satisfying assignment.")]
    [InlineData("Flip 3: C2 has no true literal.")]
    [InlineData("Flips 3–12 of try 1 are not shown one by one.")]
    [InlineData("Step 5: set x1.")]
    [InlineData("Steps 2–4 of try 1 are not shown one by one.")]
    [InlineData("Round 1: reflect every amplitude about the average.")]
    [InlineData("Pass 2: follow the edge 3 → 1 backward.")]
    [InlineData("Phase 1: node 2 is added last.")]
    [InlineData("Path 1: push 3 along 1 → 2 → 4.")]
    [InlineData("#3: try x1.")]
    [InlineData("3. Try x1.")]
    public void ThePattern_CatchesALeadingCounter(string caption) => Assert.Matches(LeadingCounter, caption);

    [Theory]
    [InlineData("Start try 1 from a random assignment, x1 = True.")]
    [InlineData("No satisfying assignment in tries 2–37 (each ends after 3 flips).")]
    [InlineData("Try 1 → 2 → 3 → 1: cost 28, the first tour found, so the best so far.")]
    [InlineData("Try x1 = False after x2 = False.")]
    [InlineData("Try {1,2}: 1 + 2 = 3, not 5.")]
    [InlineData("In pass 2, follow the edge 3 → 1 backward.")]
    [InlineData("10 goes in bin 1: 10 of 15 used.")]
    [InlineData("(0) satisfies every row, found on try 1 of 2.")]
    [InlineData("R1C3 can still hold 5 or 8. Try 5.")]
    public void ThePattern_LetsAnOrdinaryCaptionThrough(string caption) => Assert.DoesNotMatch(LeadingCounter, caption);

    [Fact]
    public void NoStepCaption_StartsWithACounter() {
        using var quantum = FakeQuantumServer.Install(out _);
        var found = new List<string>();
        int runs = 0;
        foreach (var (solverType, problemType, _, _) in StepCapture_Tests.RecordingSolvers()) {
            string instance = ((IProblem)Activator.CreateInstance(problemType)!).defaultInstance;
            var seeded = StepCapture_Tests.SeedConstructor(solverType);
            bool random = seeded != null || typeof(IRandomizedSolver).IsAssignableFrom(solverType);
            foreach (int? seed in random ? Seeds.Select(s => (int?)s) : [null]) {
                foreach (int? cap in Caps) {
                    var solver = seeded != null ? (ISolver)seeded.Invoke([seed!.Value]) : (ISolver)Activator.CreateInstance(solverType)!;
                    RandomizedSolvers.Apply(solver, seed);
                    var run = solver.Run(instance, withSteps: true, null, cap);
                    runs++;
                    foreach (object step in run.Steps) {
                        string? caption = step.GetType().GetProperty("Caption")?.GetValue(step) as string;
                        if (caption != null && LeadingCounter.IsMatch(caption)) found.Add($"{solverType.Name}: {caption}");
                    }
                }
            }
        }
        Assert.True(runs > 100, $"only {runs} runs");
        Assert.True(found.Count == 0, "Captions that start with a counter:\n" + string.Join("\n", found.Distinct().Take(40)));
    }
}
