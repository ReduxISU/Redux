using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_SAT3;
using API.Problems.NPComplete.NPC_SAT3.Solvers;
using API.Problems.NPComplete.NPC_SAT3.Verifiers;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// #932: WalkSAT records its steps (Assignment, a whole candidate per step) and takes a seed. The steps come from the run that
// gives the answer, the same seed gives the same run, and a run that gives up never claims the formula has no answer.
public class WalkSat_Tests {
    private const string Satisfiable = "(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | x1)";
    private const string Bigger = "(x1 | x2 | !x3) & (!x1 | x4 | x5) & (!x2 | !x4 | x6) & (x3 | !x5 | !x6) & (!x1 | !x2 | !x6) & (x2 | x5 | x6) & (!x3 | x4 | !x5)";
    private const string Unsatisfiable = "(x1 | x2) & (!x1 | x2) & (x1 | !x2) & (!x1 | !x2)";

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
        { Satisfiable, 1 }, { Satisfiable, 42 }, { Bigger, 7 }, { Bigger, 12345 }, { Bigger, -3 },
    };

    [Fact]
    public void WalkSat_IsATypedRandomizedSolver_ThatIsNotIncremental() {
        ISolver solver = new WalkSAT();
        Assert.IsAssignableFrom<IRandomizedSolver>(solver);
        Assert.IsAssignableFrom<ISolver<SAT3, Assignment>>(solver);
        var run = Run(solver, Satisfiable, 5);
        Assert.Equal(typeof(Assignment), run.StepShape);
        Assert.False(run.Incremental);
        Assert.Equal(5, run.Seed);
    }

    [Theory]
    [MemberData(nameof(SeededRuns))]
    public void TheSameSeed_GivesTheSameStepsAndAnswer_WithStepsOrWithout(string instance, int seed) {
        var a = Run(new WalkSAT(), instance, seed);
        var b = Run(new WalkSAT(), instance, seed);
        var off = Run(new WalkSAT(), instance, seed, withSteps: false);
        Assert.Equal(a.Answer, b.Answer);
        Assert.Equal(a.Answer, off.Answer);
        Assert.Equal(Fingerprint(a), Fingerprint(b));
        Assert.Empty(off.Steps);
        Assert.Equal(seed, off.Seed);
    }

    [Fact]
    public void WithoutASeed_ARunChoosesOne_AndThatSeedRepeatsIt() {
        var solver = new WalkSAT();
        var first = Run(solver, Bigger, null);
        Assert.NotNull(first.Seed);
        // Choosing a seed for one run does not fix the next one: the solver's own seed stays unset.
        Assert.Null(((IRandomizedSolver)solver).Seed);
        var again = Run(new WalkSAT(), Bigger, first.Seed);
        Assert.Equal(first.Answer, again.Answer);
        Assert.Equal(Fingerprint(first), Fingerprint(again));
    }

    [Fact]
    public void OtherSolvers_ReportNoSeed() {
        var backtracking = (ISolver)new Sat3BacktrackingSolver();
        RandomizedSolvers.Apply(backtracking, 9);
        Assert.Null(backtracking.Run(Satisfiable, withSteps: true).Seed);
    }

    [Theory]
    [MemberData(nameof(SeededRuns))]
    public void TheSteps_MatchTheAnswer(string instance, int seed) {
        var run = Run(new WalkSAT(), instance, seed);
        var steps = StepsOf(run);
        var sat3 = new SAT3(instance);
        Assert.True(new SAT3Verifier().verify(sat3, run.Answer), run.Answer);

        // The last step is the only Done; it holds the answer, and the Accept just before it is the same assignment.
        var done = steps[^1];
        Assert.Equal(StepEvent.Done, done.Event);
        Assert.Single(steps, s => s.Event == StepEvent.Done);
        Assert.True(done.Ok);
        Assert.Equal(Values(GroupedGraphFrames.ParseAssignment(run.Answer)), Values(done.Partial));
        Assert.Equal(StepEvent.Accept, steps[^2].Event);
        Assert.Equal(Values(done.Partial), Values(steps[^2].Partial));
        Assert.Single(steps, s => s.Event == StepEvent.Accept);
        Assert.All(steps, s => Assert.False(string.IsNullOrWhiteSpace(s.Caption)));
        Assert.StartsWith("Try 1: start from a random assignment", steps[0].Caption);

        // Every flip: the clause in focus had no true literal before it, the variable in focus is one of its variables, and the
        // flip changes that variable alone.
        for (int i = 1; i < steps.Count; i++) {
            if (!steps[i].Caption.StartsWith("Flip ") || steps[i].Caption.Contains("not shown")) continue;
            var before = Values(steps[i - 1].Partial);
            var after = Values(steps[i].Partial);
            Assert.Equal(2, steps[i].Focus.Length);
            int clause = int.Parse(steps[i].Focus[0][1..]);
            string variable = steps[i].Focus[1]["v:".Length..];
            Assert.False(Holds(sat3.clauses[clause], before), steps[i].Caption);
            Assert.True(Holds(sat3.clauses[clause], after), steps[i].Caption);
            Assert.Contains(sat3.clauses[clause], l => l.TrimStart('!') == variable);
            Assert.Equal([variable], after.Where(p => before[p.Key] != p.Value).Select(p => p.Key));
        }
    }

    [Fact]
    public void GivingUp_SaysSo_AndNeverThatTheFormulaIsUnsatisfiable() {
        var run = Run(new WalkSAT(), Unsatisfiable, 3);
        var steps = StepsOf(run);
        Assert.Equal("{}", run.Answer);
        var done = steps[^1];
        Assert.Equal(StepEvent.Done, done.Event);
        Assert.False(done.Ok);
        Assert.Empty(done.Partial.Values);
        Assert.StartsWith("Gave up after 100 tries: no answer found.", done.Caption);
        Assert.All(steps, s => Assert.DoesNotContain("unsatisfiable", s.Caption, StringComparison.OrdinalIgnoreCase));
        Assert.All(steps, s => Assert.DoesNotContain("no solution", s.Caption, StringComparison.OrdinalIgnoreCase));
        // The tries after the first are one step.
        Assert.Contains(steps, s => s.Event == StepEvent.Reject && s.Caption.StartsWith("Tries 2–100: no satisfying assignment"));
        Assert.Contains(steps, s => s.Event == StepEvent.Reject && s.Caption.StartsWith("Try 1 ends after 600 flips"));
    }

    [Theory]
    [InlineData(StepRecorder<Assignment>.DefaultCap)]
    [InlineData(20)]
    [InlineData(7)]
    public void ALongRun_FitsTheCap_AndSaysWhatItSkipped(int cap) {
        var run = Run(new WalkSAT(), Unsatisfiable, 11, cap: cap);
        var steps = StepsOf(run);
        Assert.True(steps.Count <= cap + 1, $"{steps.Count} steps for a cap of {cap}");
        // Nothing was cut by the recorder: the solver chose what to show, and says so itself.
        Assert.Equal(0, StepCaptions.NotShown(steps[^1].Caption));
        Assert.Contains(steps, s => s.Caption.Contains("not shown one by one"));
        Assert.StartsWith("Gave up after 100 tries", steps[^1].Caption);
    }

    // A run that restarts: with few flips per try, find seeds whose answer comes on the second try and on a later one.
    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void ARunThatRestarts_KeepsTheFirstAndTheSuccessfulTry_AndSummarizesTheRest(int wanted) {
        // Every clause on x1, x2, x3 but (!x1 | !x2 | !x3): one answer (all True), and three flips a try, so some tries fail.
        const string instance = "(x1 | x2 | x3) & (!x1 | x2 | x3) & (x1 | !x2 | x3) & (x1 | x2 | !x3) & (!x1 | !x2 | x3) & (!x1 | x2 | !x3) & (x1 | !x2 | !x3)";
        int? found = null;
        int successful = 0;
        for (int seed = 0; seed < 5000 && found == null; seed++) {
            var probe = StepsOf(Run(new WalkSAT(maxTries: 100, flipsPerVariable: 1), instance, seed));
            var restart = probe.LastOrDefault(s => s.Caption.StartsWith("Restart. Try "));
            if (probe[^1].Ok == true && restart != null) {
                int number = int.Parse(restart.Caption["Restart. Try ".Length..].Split(':')[0]);
                if (wanted == 2 ? number == 2 : number >= wanted) { found = seed; successful = number; }
            }
        }
        Assert.NotNull(found);

        var run = Run(new WalkSAT(maxTries: 100, flipsPerVariable: 1), instance, found);
        var steps = StepsOf(run);
        Assert.True(new SAT3Verifier().verify(new SAT3(instance), run.Answer));
        Assert.StartsWith("Try 1: start from a random assignment", steps[0].Caption);
        Assert.Contains(steps, s => s.Event == StepEvent.Reject && s.Caption.StartsWith("Try 1 ends after 3 flips"));
        Assert.Single(steps, s => s.Caption.StartsWith("Restart."));
        string between = successful == 3 ? "Try 2 ends after 3 flips: no satisfying assignment." : $"Tries 2–{successful - 1}: no satisfying assignment";
        if (successful > 2) Assert.Contains(steps, s => s.Event == StepEvent.Reject && s.Caption.StartsWith(between));
        else Assert.DoesNotContain(steps, s => s.Caption.StartsWith("Tries ") || s.Caption.StartsWith("Try 2 ends"));
        Assert.Contains($"try {successful}", steps[^1].Caption);

        // Under a small cap the successful try still ends the steps, with its Accept just before the Done.
        var capped = StepsOf(Run(new WalkSAT(maxTries: 100, flipsPerVariable: 1), instance, found, cap: 6));
        Assert.True(capped.Count <= 6 + 2);
        Assert.Equal(StepEvent.Accept, capped[^2].Event);
        Assert.True(capped[^1].Ok);
    }
}
