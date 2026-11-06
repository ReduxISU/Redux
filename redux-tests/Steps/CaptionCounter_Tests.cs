using System.Text.RegularExpressions;
using API;
using API.Interfaces;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// A step caption is a sentence and never opens with a label of its own: not a counter ("Try 2: ...", "Flip 3: ...",
// "Tries 2–37: ...", "Step 5: ...") and not a position ("Hour 0: ...", "Row 2: ...", "Cell (1,1): ..."). The step bar's
// "Step N of M" is the only label, and a caption's own count would not match it: a capped or summarized run skips numbers
// (concern #17). A count or a position that is real information stays inside the sentence ("found on try 37 of 64",
// "In pass 2, ...", "At hour 0, ...", "In row 2, ..."). A caption led by a verb that names a thing ("Put 3 in bin 1: ...",
// "Take 2: ...", "Go 1 → 3: ...") is already a sentence.
//
// Every solver that records typed steps (found as the capture tool finds them) runs on its problem's default instance, with
// steps at the default cap and at a small one, and with fixed seeds when it is random.
public class CaptionCounter_Tests {
    // A counter at the start, optionally after one short sentence ("Restart. Try 2: ..."): a counting word and a number (not
    // a tour like "Try 1 → 2 → 1"), "#3", "(3):" or "3. ".
    private static readonly Regex LeadingCounter = new(
        @"^(?:\w+\.\s+)?(?:(?:Steps?|Try|Tries|Flips?|Rounds?|Pass(?:es)?|Iterations?|Moves?|Quer(?:y|ies)|Calls?|Paths?|Phases?|Restarts?|Attempts?|Trials?|Runs?|Stages?|Turns?)\s+#?\d+(?!\s*→)|#\d|\(\d+\)[:.]|\d+[.:)]\s)",
        RegexOptions.CultureInvariant);

    // A position label at the start: a place word and its number or (row,column), optionally with a note in parentheses,
    // "of the table" or ", item (10,60)", then a colon. It may follow a phrase and a comma ("Forward pass, hour 0: ...") or
    // "Back in" ("Back in row 1: ...").
    private static readonly Regex LeadingPosition = new(
        @"^(?:[A-Z][a-z]+(?:\s[a-z]+)*,\s|Back\sin\s)?(?:[Hh]ours?|[Rr]ows?|[Cc]olumns?|[Cc]ells?|[Ii]tems?|[Bb]ins?|[Nn]odes?|[Vv]ertex|[Ee]dges?|[Ll]evels?|[Dd]ays?|[Ss]lots?|[Ss]quares?|[Pp]ositions?|[Ii]ndex|[Ll]ayers?)\s+(?:\d+|\(\d+,\s*\d+\))(?:\s*\([^)]*\)|\s+of\s+the\s+\w+|,\s*item\s+\S+)*:",
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

    [Theory]
    [InlineData("Hour 0: PumpA and PumpB on. In 550, out 600 gal.")]
    [InlineData("Hour 8 (peak): PumpA and PumpB on.")]
    [InlineData("Forward pass, hour 0: 8 reachable (tank level, pumps on) states.")]
    [InlineData("Row 2: every column is attacked, so back up and move the queen in row 1.")]
    [InlineData("Back in row 1: move the queen on to column 3.")]
    [InlineData("Cell (1,1): 'h' ≠ 'r'.")]
    [InlineData("Row 3 ('c'): each cell copies the diagonal when the letters match.")]
    [InlineData("Row 1 of the table, item (10,60): taking it raises the best value within 50 to 60.")]
    [InlineData("Column 2: no queen fits.")]
    [InlineData("Item 3: too heavy for what is left.")]
    [InlineData("Bin 1: 10 of 15 used.")]
    public void ThePositionPattern_CatchesALeadingPosition(string caption) => Assert.Matches(LeadingPosition, caption);

    [Theory]
    [InlineData("At hour 0, PumpA and PumpB are on. In 550, out 600 gal.")]
    [InlineData("At hour 0, the forward pass holds 8 reachable (tank level, pumps on) states.")]
    [InlineData("In row 2, every column is attacked, so back up and move the queen in row 1.")]
    [InlineData("Back in row 1, move the queen on to column 3.")]
    [InlineData("Row 1's queen was already in the last column, so back up and move the queen in row 0.")]
    [InlineData("Row 0 has no column left to try.")]
    [InlineData("At cell (1,1), 'h' ≠ 'r'.")]
    [InlineData("In row 1 of the table, taking item (10,60) raises the best value within 50 to 60.")]
    [InlineData("Put 3 in bin 1: 3 of 10 used.")]
    [InlineData("10 goes in bin 1: 10 of 15 used.")]
    [InlineData("Take 2: it covers the most edges left.")]
    [InlineData("Go 1 → 3: the cheapest edge out of 1.")]
    [InlineData("Node 3 needs color 2, but K = 2 allows only colors 0 to 1: no coloring.")]
    [InlineData("Color node 2 with color 1: no neighbor has it.")]
    public void ThePositionPattern_LetsAnOrdinaryCaptionThrough(string caption) => Assert.DoesNotMatch(LeadingPosition, caption);

    [Fact]
    public void NoStepCaption_StartsWithALabel() {
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
                        if (caption != null && (LeadingCounter.IsMatch(caption) || LeadingPosition.IsMatch(caption))) found.Add($"{solverType.Name}: {caption}");
                    }
                }
            }
        }
        Assert.True(runs > 100, $"only {runs} runs");
        Assert.True(found.Count == 0, "Captions that start with a counter or a position label:\n" + string.Join("\n", found.Distinct().Take(40)));
    }
}
