using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_SAT3;
using API.Problems.NPComplete.NPC_SAT3.Solvers;
using API.Problems.NPComplete.NPC_SAT3.Verifiers;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// #932: Schöning's algorithm records its steps (Assignment, a whole candidate per step) and takes a seed, like WalkSAT (see
// WalkSat_Tests). Each try starts from a random assignment and walks up to 3n steps, each flipping the variable of a random
// literal of a random clause with no true literal. The steps come from the run that gives the answer, the same seed gives the
// same run, and a run that gives up never claims the formula has no answer.
public class Schoning_Tests {
    private const string Satisfiable = "(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | x1)";
    private const string Bigger = "(x1 | x2 | !x3) & (!x1 | x4 | x5) & (!x2 | !x4 | x6) & (x3 | !x5 | !x6) & (!x1 | !x2 | !x6) & (x2 | x5 | x6) & (!x3 | x4 | !x5)";
    private const string Unsatisfiable = "(x1 | x2) & (!x1 | x2) & (x1 | !x2) & (!x1 | !x2)";
    // Every clause on x1, x2, x3 but (!x1 | !x2 | !x3): one answer (all True), so short walks often fail.
    private const string OneAnswer = "(x1 | x2 | x3) & (!x1 | x2 | x3) & (x1 | !x2 | x3) & (x1 | x2 | !x3) & (!x1 | !x2 | x3) & (!x1 | x2 | !x3) & (x1 | !x2 | !x3)";

    private static SolveRun Run(ISolver solver, string instance, int? seed, bool withSteps = true, int? cap = null) {
        RandomizedSolvers.Apply(solver, seed);
        return solver.Run(instance, withSteps, stepCap: cap);
    }

    private static List<SolverStep<Assignment>> StepsOf(SolveRun run) => run.Steps.Cast<SolverStep<Assignment>>().ToList();

    private static Dictionary<string, bool> Values(Assignment a) => a.Values.ToDictionary(p => p.Key, p => p.Value == "True");

    // Whether a clause, as the instance writes it, has a true literal under the values (read here, not by the code under test).
    private static bool Holds(IEnumerable<string> clause, IReadOnlyDictionary<string, bool> values) =>
        clause.Any(l => values[l.TrimStart('!')] != l.StartsWith('!'));

    private static string Fingerprint(SolveRun run) => string.Join("\n", StepsOf(run).Select(s =>
        $"{s.Event}|{s.Caption}|{string.Join(",", s.Focus)}|{string.Join(",", s.Partial.Values.Select(p => p.Key + "=" + p.Value))}|{s.Ok}"));

    public static TheoryData<string, int> SeededRuns => new() {
        { Satisfiable, 1 }, { Satisfiable, 42 }, { Bigger, 7 }, { Bigger, 12345 }, { Bigger, -3 }, { OneAnswer, 5 },
    };

    [Fact]
    public void Schoning_IsATypedRandomizedSolver_ThatIsNotIncremental() {
        ISolver solver = new Schoning();
        Assert.IsAssignableFrom<IRandomizedSolver>(solver);
        Assert.IsAssignableFrom<ISolver<SAT3, Assignment>>(solver);
        Assert.IsNotAssignableFrom<IIncrementalSolver>(solver);
        var run = Run(solver, Satisfiable, 5);
        Assert.Equal(typeof(Assignment), run.StepShape);
        Assert.False(run.Incremental);
        Assert.Equal(5, run.Seed);
    }

    // (4/3)^n tries alone was 2 for 3 variables, so small formulas often came back without an answer.
    [Theory]
    [InlineData(1, 100)]
    [InlineData(3, 124)]
    [InlineData(6, 413)]
    [InlineData(8, 848)]
    [InlineData(10, 1_000)]
    [InlineData(40, 1_000)]
    public void Trials_AreThirtyTimesTheExpectedTries_BetweenAHundredAndAThousand(int n, int expected) {
        Assert.Equal(expected, Schoning.Trials(n));
    }

    // With the default tries, every seed finds OneAnswer's single answer.
    [Fact]
    public void DefaultTries_FindTheOneAnswer_ForEverySeed() {
        for (int seed = 0; seed < 60; seed++) {
            Assert.True(new SAT3Verifier().verify(new SAT3(OneAnswer), Run(new Schoning(), OneAnswer, seed, withSteps: false).Answer));
        }
    }

    [Theory]
    [MemberData(nameof(SeededRuns))]
    public void TheSameSeed_GivesTheSameStepsAndAnswer_WithStepsOrWithout(string instance, int seed) {
        var a = Run(new Schoning(), instance, seed);
        var b = Run(new Schoning(), instance, seed);
        var off = Run(new Schoning(), instance, seed, withSteps: false);
        Assert.Equal(a.Answer, b.Answer);
        Assert.Equal(a.Answer, off.Answer);
        Assert.Equal(Fingerprint(a), Fingerprint(b));
        Assert.Empty(off.Steps);
        Assert.Equal(seed, off.Seed);
    }

    [Fact]
    public void WithoutASeed_ARunChoosesOne_AndThatSeedRepeatsIt() {
        var solver = new Schoning();
        var first = Run(solver, Bigger, null);
        Assert.NotNull(first.Seed);
        Assert.Null(((IRandomizedSolver)solver).Seed);
        var again = Run(new Schoning(), Bigger, first.Seed);
        Assert.Equal(first.Answer, again.Answer);
        Assert.Equal(Fingerprint(first), Fingerprint(again));
    }

    // Every seed from 0 to 59: a run that finds an answer ends on it, a run that gives up says so; the flips are Schöning's.
    [Theory]
    [InlineData(Satisfiable)]
    [InlineData(Bigger)]
    [InlineData(OneAnswer)]
    public void TheSteps_MatchTheAnswer(string instance) {
        var sat3 = new SAT3(instance);
        int found = 0;
        for (int seed = 0; seed < 60; seed++) {
            var run = Run(new Schoning(), instance, seed);
            var steps = StepsOf(run);
            var done = steps[^1];
            Assert.Equal(StepEvent.Done, done.Event);
            Assert.Single(steps, s => s.Event == StepEvent.Done);
            Assert.All(steps, s => Assert.False(string.IsNullOrWhiteSpace(s.Caption)));
            Assert.StartsWith("Start try 1 from a random assignment", steps[0].Caption);
            if (run.Answer == "{}") {
                Assert.False(done.Ok);
                Assert.StartsWith("Gave up after ", done.Caption);
                Assert.DoesNotContain(steps, s => s.Event == StepEvent.Accept);
            } else {
                found++;
                Assert.True(new SAT3Verifier().verify(sat3, run.Answer), run.Answer);
                Assert.True(done.Ok);
                Assert.Equal(Values(GroupedGraphFrames.ParseAssignment(run.Answer)), Values(done.Partial));
                Assert.Equal(StepEvent.Accept, steps[^2].Event);
                Assert.Equal(Values(done.Partial), Values(steps[^2].Partial));
                Assert.Single(steps, s => s.Event == StepEvent.Accept);
            }

            // Every flip: the clause in focus had no true literal before it, the variable in focus is one of its variables, and
            // the flip changes that variable alone (so the clause has a true literal after it).
            for (int i = 1; i < steps.Count; i++) {
                if (!System.Text.RegularExpressions.Regex.IsMatch(steps[i].Caption, @"^C\d+ \(")) continue;
                var before = Values(steps[i - 1].Partial);
                var after = Values(steps[i].Partial);
                Assert.Equal(2, steps[i].Focus.Length);
                int clause = int.Parse(steps[i].Focus[0][1..]);
                string variable = steps[i].Focus[1]["v:".Length..];
                Assert.False(Holds(sat3.clauses[clause], before), steps[i].Caption);
                Assert.True(Holds(sat3.clauses[clause], after), steps[i].Caption);
                Assert.Contains(sat3.clauses[clause], l => l.TrimStart('!') == variable);
                Assert.Equal([variable], after.Where(p => before[p.Key] != p.Value).Select(p => p.Key));
                Assert.Contains("Pick one of its literals at random", steps[i].Caption);
            }
        }
        Assert.True(found > 0, "no seed found an answer");
    }

    [Fact]
    public void GivingUp_SaysSo_AndNeverThatTheFormulaIsUnsatisfiable() {
        // Two variables, one try of 6 flips.
        var run = Run(new Schoning(trials: 1, flipsPerVariable: 3), Unsatisfiable, 3);
        var steps = StepsOf(run);
        Assert.Equal("{}", run.Answer);
        var done = steps[^1];
        Assert.False(done.Ok);
        Assert.Empty(done.Partial.Values);
        Assert.Equal("Gave up after 1 try: no answer found. Schöning's algorithm can miss an answer, so the formula may still have one.", done.Caption);
        Assert.Contains(steps, s => s.Event == StepEvent.Reject && s.Caption.StartsWith("After 6 flips, try 1 ends"));
        Assert.Equal(1 + 6 + 1 + 1, steps.Count);

        // The default for two variables is the minimum, 100 tries.
        Assert.StartsWith("Gave up after 100 tries: no answer found.", StepsOf(Run(new Schoning(), Unsatisfiable, 3))[^1].Caption);

        // With more tries, the tries after the first are one step.
        var many = StepsOf(Run(new Schoning(trials: 50, flipsPerVariable: 3), Unsatisfiable, 3));
        Assert.StartsWith("Gave up after 50 tries: no answer found.", many[^1].Caption);
        Assert.Contains(many, s => s.Event == StepEvent.Reject && s.Caption == "No satisfying assignment in tries 2–50 (each ends after 6 flips).");
        foreach (var s in steps.Concat(many)) {
            Assert.DoesNotContain("unsatisfiable", s.Caption, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("no solution", s.Caption, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void TheLastFlipOfATry_IsChecked() {
        // The original solver checked the assignment before each of its 3n flips but not after the last one, so a try whose last
        // flip found the answer was thrown away. Find a run whose answer comes on the last flip of a try.
        bool seen = false;
        for (int seed = 0; seed < 3000 && !seen; seed++) {
            var steps = StepsOf(Run(new Schoning(trials: 20, flipsPerVariable: 1), OneAnswer, seed));
            if (steps[^1].Ok == true && steps[^1].Caption.EndsWith("after 3 flips.")) seen = true;
        }
        Assert.True(seen);
    }

    [Theory]
    [InlineData(StepRecorder<Assignment>.DefaultCap)]
    [InlineData(20)]
    [InlineData(7)]
    public void ALongRun_FitsTheCap_AndSaysWhatItSkipped(int cap) {
        var run = Run(new Schoning(trials: 30, flipsPerVariable: 100), Unsatisfiable, 11, cap: cap);
        var steps = StepsOf(run);
        Assert.True(steps.Count <= cap + 1, $"{steps.Count} steps for a cap of {cap}");
        Assert.Equal(0, StepCaptions.NotShown(steps[^1].Caption));
        Assert.Contains(steps, s => s.Caption.Contains("not shown one by one"));
        Assert.StartsWith("Gave up after 30 tries", steps[^1].Caption);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void ARunThatRestarts_KeepsTheFirstAndTheSuccessfulTry_AndSummarizesTheRest(int wanted) {
        int? found = null;
        int successful = 0;
        for (int seed = 0; seed < 5000 && found == null; seed++) {
            var probe = StepsOf(Run(new Schoning(trials: 100, flipsPerVariable: 1), OneAnswer, seed));
            var restart = probe.LastOrDefault(s => s.Caption.StartsWith("Restart with try "));
            if (probe[^1].Ok == true && restart != null) {
                int number = int.Parse(restart.Caption["Restart with try ".Length..].Split(' ')[0]);
                if (wanted == 2 ? number == 2 : number >= wanted) { found = seed; successful = number; }
            }
        }
        Assert.NotNull(found);

        var run = Run(new Schoning(trials: 100, flipsPerVariable: 1), OneAnswer, found);
        var steps = StepsOf(run);
        Assert.True(new SAT3Verifier().verify(new SAT3(OneAnswer), run.Answer));
        Assert.StartsWith("Start try 1 from a random assignment", steps[0].Caption);
        Assert.Contains(steps, s => s.Event == StepEvent.Reject && s.Caption.StartsWith("After 3 flips, try 1 ends"));
        Assert.Single(steps, s => s.Caption.StartsWith("Restart "));
        string between = successful == 3 ? "No satisfying assignment in try 2 (it ends after 3 flips)." : $"No satisfying assignment in tries 2–{successful - 1} ";
        if (successful > 2) Assert.Contains(steps, s => s.Event == StepEvent.Reject && s.Caption.StartsWith(between));
        else Assert.DoesNotContain(steps, s => s.Caption.StartsWith("No satisfying assignment in tr"));
        Assert.Contains($"try {successful}", steps[^1].Caption);

        var capped = StepsOf(Run(new Schoning(trials: 100, flipsPerVariable: 1), OneAnswer, found, cap: 6));
        Assert.True(capped.Count <= 6 + 2);
        Assert.Equal(StepEvent.Accept, capped[^2].Event);
        Assert.True(capped[^1].Ok);
    }
}
