using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_SAT3;
using API.Problems.NPComplete.NPC_SAT3.Solvers;
using API.Problems.NPComplete.NPC_SAT3.Verifiers;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// #932: PPZ records its steps (Assignment) and takes a seed, like WalkSAT (see WalkSat_Tests). Each try takes the variables in
// a random order and sets them one at a time: forced by a clause whose other literals are all false, else at random. The partial
// holds only the variables set so far (PPZ is incremental). Once every variable is set the try is checked: an Accept, or a
// Reject. The same seed gives the same run, and a run that gives up never claims the formula has no answer.
public class Ppz_Tests {
    private const string Satisfiable = "(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | x1)";
    private const string Bigger = "(x1 | x2 | !x3) & (!x1 | x4 | x5) & (!x2 | !x4 | x6) & (x3 | !x5 | !x6) & (!x1 | !x2 | !x6) & (x2 | x5 | x6) & (!x3 | x4 | !x5)";
    private const string Unsatisfiable = "(x1 | x2) & (!x1 | x2) & (x1 | !x2) & (!x1 | !x2)";
    // Every clause on x1, x2, x3 but (!x1 | !x2 | !x3): one answer (all True), so some tries fail.
    private const string OneAnswer = "(x1 | x2 | x3) & (!x1 | x2 | x3) & (x1 | !x2 | x3) & (x1 | x2 | !x3) & (!x1 | !x2 | x3) & (!x1 | x2 | !x3) & (x1 | !x2 | !x3)";

    private static SolveRun Run(ISolver solver, string instance, int? seed, bool withSteps = true, int? cap = null) {
        RandomizedSolvers.Apply(solver, seed);
        return solver.Run(instance, withSteps, stepCap: cap);
    }

    private static List<SolverStep<Assignment>> StepsOf(SolveRun run) => run.Steps.Cast<SolverStep<Assignment>>().ToList();

    private static Dictionary<string, bool> Values(Assignment a) => a.Values.ToDictionary(p => p.Key, p => p.Value == "True");

    // Whether a literal is true under the values (read here, not by the code under test); an unset variable makes it not true.
    private static bool True(string literal, IReadOnlyDictionary<string, bool> values) =>
        values.TryGetValue(literal.TrimStart('!'), out bool v) && v != literal.StartsWith('!');

    private static bool Holds(IEnumerable<string> clause, IReadOnlyDictionary<string, bool> values) => clause.Any(l => True(l, values));

    private static string Fingerprint(SolveRun run) => string.Join("\n", StepsOf(run).Select(s =>
        $"{s.Event}|{s.Caption}|{string.Join(",", s.Focus)}|{string.Join(",", s.Partial.Values.Select(p => p.Key + "=" + p.Value))}|{s.Ok}"));

    // A formula of many variables: every sign combination of x1..x3 (so no answer) and a clause (xi | !xi | x1) for each other.
    private static string ManyVariables(int extra) => string.Join(" & ",
        Enumerable.Range(0, 8).Select(m => $"({((m & 1) == 0 ? "x1" : "!x1")} | {((m & 2) == 0 ? "x2" : "!x2")} | {((m & 4) == 0 ? "x3" : "!x3")})")
            .Concat(Enumerable.Range(4, extra).Select(i => $"(x{i} | !x{i} | x1)")));

    public static TheoryData<string, int> SeededRuns => new() {
        { Satisfiable, 1 }, { Satisfiable, 42 }, { Bigger, 7 }, { Bigger, 12345 }, { Bigger, -3 }, { OneAnswer, 5 },
    };

    [Fact]
    public void Ppz_IsATypedRandomizedSolver_ThatIsIncremental() {
        ISolver solver = new PPZ();
        Assert.IsAssignableFrom<IRandomizedSolver>(solver);
        Assert.IsAssignableFrom<ISolver<SAT3, Assignment>>(solver);
        var run = Run(solver, Satisfiable, 5);
        Assert.Equal(typeof(Assignment), run.StepShape);
        Assert.True(run.Incremental);
        Assert.Equal(5, run.Seed);
    }

    [Theory]
    [MemberData(nameof(SeededRuns))]
    public void TheSameSeed_GivesTheSameStepsAndAnswer_WithStepsOrWithout(string instance, int seed) {
        var a = Run(new PPZ(), instance, seed);
        var b = Run(new PPZ(), instance, seed);
        var off = Run(new PPZ(), instance, seed, withSteps: false);
        Assert.Equal(a.Answer, b.Answer);
        Assert.Equal(a.Answer, off.Answer);
        Assert.Equal(Fingerprint(a), Fingerprint(b));
        Assert.Empty(off.Steps);
        Assert.Equal(seed, off.Seed);
    }

    [Fact]
    public void WithoutASeed_ARunChoosesOne_AndThatSeedRepeatsIt() {
        var solver = new PPZ();
        var first = Run(solver, Bigger, null);
        Assert.NotNull(first.Seed);
        Assert.Null(((IRandomizedSolver)solver).Seed);
        var again = Run(new PPZ(), Bigger, first.Seed);
        Assert.Equal(first.Answer, again.Answer);
        Assert.Equal(Fingerprint(first), Fingerprint(again));
    }

    // Every seed from 0 to 59: a run that finds an answer ends on it, a run that gives up says so; each step sets one variable,
    // forced exactly when a clause forces it.
    [Theory]
    [InlineData(Satisfiable)]
    [InlineData(Bigger)]
    [InlineData(OneAnswer)]
    public void TheSteps_MatchTheAnswer(string instance) {
        var sat3 = new SAT3(instance);
        int variables = sat3.literals.Select(l => l.TrimStart('!')).Distinct().Count();
        int found = 0;
        for (int seed = 0; seed < 60; seed++) {
            var run = Run(new PPZ(), instance, seed);
            var steps = StepsOf(run);
            var done = steps[^1];
            Assert.Equal(StepEvent.Done, done.Event);
            Assert.Single(steps, s => s.Event == StepEvent.Done);
            Assert.All(steps, s => Assert.False(string.IsNullOrWhiteSpace(s.Caption)));
            Assert.StartsWith("Try 1: take the variables in a random order: ", steps[0].Caption);
            Assert.Empty(steps[0].Partial.Values);
            if (run.Answer == "{}") {
                Assert.False(done.Ok);
                Assert.StartsWith("Gave up after ", done.Caption);
                Assert.DoesNotContain(steps, s => s.Event == StepEvent.Accept);
            } else {
                found++;
                Assert.True(new SAT3Verifier().verify(sat3, run.Answer), run.Answer);
                Assert.True(done.Ok);
                Assert.Equal(Values(GroupedGraphFrames.ParseAssignment(run.Answer)), Values(done.Partial));
                var accept = steps[^2];
                Assert.Equal(StepEvent.Accept, accept.Event);
                Assert.EndsWith("has set every variable: every clause has a true literal.", accept.Caption);
                Assert.Equal(Values(done.Partial), Values(accept.Partial));
                Assert.Single(steps, s => s.Event == StepEvent.Accept);
            }

            for (int i = 1; i < steps.Count; i++) {
                var step = steps[i];
                if (step.Caption.StartsWith("Step ")) {
                    // One more variable than before, the one in focus (last), and nothing else changes.
                    var before = Values(steps[i - 1].Partial);
                    var after = Values(step.Partial);
                    string variable = step.Focus[^1]["v:".Length..];
                    Assert.Equal(before.Count + 1, after.Count);
                    Assert.False(before.ContainsKey(variable));
                    Assert.All(before, p => Assert.Equal(p.Value, after[p.Key]));
                    Assert.Equal(StepEvent.Try, step.Event);
                    // Forced exactly when some clause with the variable has every other literal false; the clause in focus is
                    // such a clause, and the value makes its literal true.
                    bool forcedHere = sat3.clauses.Any(c => c.Any(l => l.TrimStart('!') == variable)
                        && c.Where(l => l.TrimStart('!') != variable).All(l => before.ContainsKey(l.TrimStart('!')) && !True(l, before)));
                    if (step.Focus.Length == 2) {
                        int clause = int.Parse(step.Focus[0][1..]);
                        Assert.Contains("forces", step.Caption);
                        Assert.True(forcedHere, step.Caption);
                        Assert.True(Holds(sat3.clauses[clause], after), step.Caption);
                    } else {
                        Assert.Single(step.Focus);
                        Assert.Contains("No clause forces it", step.Caption);
                        Assert.False(forcedHere, step.Caption);
                    }
                }
                if (step.Event == StepEvent.Reject && step.Caption.Contains("has set every variable")) {
                    // The check of a failed try: every variable is set, and the clauses in focus are the false ones.
                    var values = Values(step.Partial);
                    Assert.Equal(variables, values.Count);
                    var falseOnes = sat3.clauses.Select((c, k) => (c, k)).Where(x => !Holds(x.c, values)).Select(x => GroupedGraphFrames.ClauseId(x.k));
                    Assert.Equal(falseOnes, step.Focus);
                }
            }
        }
        Assert.True(found > 0, "no seed found an answer");
    }

    [Fact]
    public void GivingUp_SaysSo_AndNeverThatTheFormulaIsUnsatisfiable() {
        // Two variables: 2^(4/3) rounds down to two tries.
        var run = Run(new PPZ(), Unsatisfiable, 3);
        var steps = StepsOf(run);
        Assert.Equal("{}", run.Answer);
        var done = steps[^1];
        Assert.False(done.Ok);
        Assert.Empty(done.Partial.Values);
        Assert.Equal("Gave up after 2 tries: no answer found. PPZ can miss an answer, so the formula may still have one.", done.Caption);
        Assert.Contains(steps, s => s.Event == StepEvent.Reject && s.Caption.StartsWith("Try 1 has set every variable, but "));
        Assert.Contains(steps, s => s.Event == StepEvent.Reject && s.Caption == "Try 2 sets every variable: no satisfying assignment.");

        var many = StepsOf(Run(new PPZ(trials: 50), Unsatisfiable, 3));
        Assert.StartsWith("Gave up after 50 tries: no answer found.", many[^1].Caption);
        Assert.Contains(many, s => s.Event == StepEvent.Reject && s.Caption == "Tries 2–50: no satisfying assignment (each sets every variable).");
        foreach (var s in steps.Concat(many)) {
            Assert.DoesNotContain("unsatisfiable", s.Caption, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("no solution", s.Caption, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData(StepRecorder<Assignment>.DefaultCap)]
    [InlineData(20)]
    [InlineData(7)]
    public void ALongRun_FitsTheCap_AndSaysWhatItSkipped(int cap) {
        var run = Run(new PPZ(trials: 5), ManyVariables(200), 11, cap: cap);
        var steps = StepsOf(run);
        Assert.True(steps.Count <= cap + 1, $"{steps.Count} steps for a cap of {cap}");
        Assert.Equal(0, StepCaptions.NotShown(steps[^1].Caption));
        var skipped = Assert.Single(steps, s => s.Caption.Contains("not shown one by one"));
        Assert.Matches(@"^Steps \d+–\d+ of try 1 are not shown one by one\. After them, \d+ of the 203 variables are set\.$", skipped.Caption);
        Assert.StartsWith("Try 1: take the 203 variables in a random order.", steps[0].Caption);
        Assert.StartsWith("Gave up after 5 tries", steps[^1].Caption);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void ARunThatRestarts_KeepsTheFirstAndTheSuccessfulTry_AndSummarizesTheRest(int wanted) {
        int? found = null;
        int successful = 0;
        for (int seed = 0; seed < 5000 && found == null; seed++) {
            var probe = StepsOf(Run(new PPZ(trials: 100), OneAnswer, seed));
            var restart = probe.LastOrDefault(s => s.Caption.StartsWith("Restart. Try "));
            if (probe[^1].Ok == true && restart != null) {
                int number = int.Parse(restart.Caption["Restart. Try ".Length..].Split(':')[0]);
                if (wanted == 2 ? number == 2 : number >= wanted) { found = seed; successful = number; }
            }
        }
        Assert.NotNull(found);

        var run = Run(new PPZ(trials: 100), OneAnswer, found);
        var steps = StepsOf(run);
        Assert.True(new SAT3Verifier().verify(new SAT3(OneAnswer), run.Answer));
        Assert.StartsWith("Try 1: take the variables in a random order", steps[0].Caption);
        Assert.Contains(steps, s => s.Event == StepEvent.Reject && s.Caption.StartsWith("Try 1 has set every variable, but "));
        var restartStep = Assert.Single(steps, s => s.Caption.StartsWith("Restart."));
        Assert.Empty(restartStep.Partial.Values);
        string between = successful == 3 ? "Try 2 sets every variable: no satisfying assignment." : $"Tries 2–{successful - 1}: no satisfying assignment";
        if (successful > 2) Assert.Contains(steps, s => s.Event == StepEvent.Reject && s.Caption.StartsWith(between));
        else Assert.DoesNotContain(steps, s => s.Caption.StartsWith("Tries ") || s.Caption.StartsWith("Try 2 sets"));
        Assert.EndsWith($"found by try {successful}.", steps[^1].Caption);

        var capped = StepsOf(Run(new PPZ(trials: 100), OneAnswer, found, cap: 6));
        Assert.True(capped.Count <= 6 + 2);
        Assert.Equal(StepEvent.Accept, capped[^2].Event);
        Assert.True(capped[^1].Ok);
    }
}
