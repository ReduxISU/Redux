using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_SAT;
using API.Problems.NPComplete.NPC_SAT.Solvers;
using API.Problems.NPComplete.NPC_SAT.Verifiers;
using API.Problems.NPComplete.NPC_SAT3;
using API.Problems.NPComplete.NPC_SAT3.Solvers;
using API.Problems.NPComplete.NPC_SAT3.Verifiers;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The SAT brute force and 3SAT backtracking solvers record Assignment steps. Recording must never change an answer.
// Endpoints/Snapshots/sat_solver_answers.tsv holds what each solver returned (solver, instance, answer; tab separated) BEFORE
// it recorded steps, on its default instance and on hand-made and 12 seeded random formulas (satisfiable and not). Five brute
// force rows changed with #754, which made the solver try the all-false assignment first: each now holds that all-false answer,
// which those formulas accept but the old counter skipped. The three
// randomized 3SAT solvers (PPZ, Schoning, WalkSAT) are not touched, and give a different answer on every run.
public class SatSolvers_Tests {
    private static string SnapshotPath(string file) =>
        Path.Combine(AppContext.BaseDirectory, "Endpoints", "Snapshots", file);

    public static TheoryData<string, string, string> RecordedAnswers {
        get {
            var data = new TheoryData<string, string, string>();
            foreach (var line in File.ReadAllLines(SnapshotPath("sat_solver_answers.tsv")).Where(l => l.Length > 0)) {
                var parts = line.Split('\t');
                data.Add(parts[0], parts[1], parts[2]);
            }
            return data;
        }
    }

    private static ISolver Create(string name) => (ISolver)Activator.CreateInstance(ProblemProvider.Solvers[name])!;

    private static List<SolverStep<Assignment>> StepsOf(SolveRun run) => run.Steps.Cast<SolverStep<Assignment>>().ToList();

    private static bool NoAnswer(string answer) => answer.StartsWith("No ", StringComparison.OrdinalIgnoreCase);

    // The formula's own variable names, read here from the instance text (not by the code under test).
    private static HashSet<string> VariablesOf(string instance) =>
        instance.Split(['(', ')', ' ', '|', '&', '!'], StringSplitOptions.RemoveEmptyEntries).ToHashSet();

    [Fact]
    public void TheSnapshotCoversBothSolvers_WithAnswersAndWithout() {
        var rows = RecordedAnswers.Select(d => d.Data).GroupBy(d => d.Item1).ToDictionary(g => g.Key, g => g.ToList());
        Assert.Equal(["sat3backtrackingsolver", "satbruteforcesolver"], rows.Keys.Order());
        foreach (var list in rows.Values) {
            Assert.Contains(list, d => NoAnswer(d.Item3));
            Assert.Contains(list, d => !NoAnswer(d.Item3));
            Assert.True(list.Count >= 16);
        }
    }

    [Theory]
    [MemberData(nameof(RecordedAnswers))]
    public void Solve_ReturnsWhatItReturnedBeforeRecordingSteps(string solverName, string instance, string answer) {
        var solver = Create(solverName);
        var on = solver.Run(instance, withSteps: true);
        var off = solver.Run(instance, withSteps: false);

        Assert.Equal(answer, on.Answer);
        Assert.Equal(answer, off.Answer);
        Assert.Equal(answer, solver.solve(instance));
        Assert.Empty(off.Steps);
        Assert.Equal(typeof(Assignment), on.StepShape);
        Assert.Equal(typeof(Assignment), off.StepShape);

        // The last step is the only Done, every caption is a sentence, and Done.Ok is what the problem's verifier says.
        var steps = StepsOf(on);
        Assert.Equal(StepEvent.Done, steps[^1].Event);
        Assert.Single(steps, s => s.Event == StepEvent.Done);
        Assert.All(steps, s => Assert.False(string.IsNullOrWhiteSpace(s.Caption)));
        Assert.Equal(!NoAnswer(answer), steps[^1].Ok);
        bool verified;
        try {
            verified = solverName.StartsWith("sat3")
                ? new SAT3Verifier().verify(new SAT3(instance), answer)
                : new SATVerifier().verify(new SAT(instance), answer);
        } catch { verified = false; }
        Assert.Equal(verified, steps[^1].Ok);
    }

    // The last step's partial answer says the same thing as the answer string, and every step names only the formula's variables.
    [Theory]
    [MemberData(nameof(RecordedAnswers))]
    public void EveryStep_NamesOnlyTheFormulasVariables_AndTheDoneStepShowsTheAnswer(string solverName, string instance, string answer) {
        var steps = StepsOf(Create(solverName).Run(instance, withSteps: true));
        var variables = VariablesOf(instance);
        Assert.Equal(GroupedGraphFrames.ParseAssignment(answer), steps[^1].Partial);
        var spec = GroupedGraphSpec.FromLiterals(solverName.StartsWith("sat3") ? new SAT3(instance).clauses : new SAT(instance).clauses);
        var ids = GroupedGraphFrames.Payload(spec);
        var valid = ids.Variables.Select(v => v.Id).Concat(ids.Groups.Select(g => g.Id)).ToHashSet();
        foreach (var step in steps) {
            Assert.All(step.Partial.Values.Keys, k => Assert.Contains(k, variables));
            Assert.All(step.Partial.Values.Values, v => Assert.Contains(v, new[] { "True", "False" }));
            Assert.All(step.Focus, f => Assert.Contains(f, valid));
        }
    }

    // ---- SAT brute force ----

    [Fact]
    public void BruteForce_OneStepPerCandidate_NamesTheFirstClauseWithNoTrueLiteral() {
        var run = Create("satbruteforcesolver").Run("(x1 | x2) & (!x1 | !x2) & (x1 | !x2)", withSteps: true);
        var steps = StepsOf(run);
        Assert.Equal("(x1:True,x2:False)", run.Answer);
        // The counter starts at all-false: clause 1, (x1 | x2), is the first with no true literal.
        Assert.Equal(StepEvent.Reject, steps[0].Event);
        Assert.Equal("Try x1 = False, x2 = False: clause C1 (x1 | x2) has no true literal.", steps[0].Caption);
        Assert.Equal(["c0"], steps[0].Focus);
        Assert.Equal(new Assignment([new("x1", "False"), new("x2", "False")]), steps[0].Partial);
        // Next x1 = False, x2 = True: clause 3, (x1 | !x2), is the first with no true literal.
        Assert.Equal(StepEvent.Reject, steps[1].Event);
        Assert.Equal("Try x1 = False, x2 = True: clause C3 (x1 | !x2) has no true literal.", steps[1].Caption);
        Assert.Equal(["c2"], steps[1].Focus);
        Assert.Equal(new Assignment([new("x1", "False"), new("x2", "True")]), steps[1].Partial);
        // Then x1 = True, x2 = False satisfies every clause: an Accept, then the Done.
        Assert.Equal(StepEvent.Accept, steps[^2].Event);
        Assert.Equal("Try x1 = True, x2 = False: every clause has a true literal.", steps[^2].Caption);
        Assert.Equal("The formula is satisfied by x1 = True, x2 = False.", steps[^1].Caption);
        Assert.True(steps[^1].Ok);
        Assert.Equal(4, steps.Count);
    }

    [Fact]
    public void BruteForce_RecordedRun_TriesAllFalseFirst() {
        // #754: the recorded path shares the solver's counter, so it must also try all-false, and here that is the answer.
        var run = Create("satbruteforcesolver").Run("(!x1 | !x2) & (!x1) & (!x2)", withSteps: true);
        var steps = StepsOf(run);
        Assert.Equal("(x1:False,x2:False)", run.Answer);
        Assert.Equal(2, steps.Count);
        Assert.Equal(StepEvent.Accept, steps[0].Event);
        Assert.Equal("Try x1 = False, x2 = False: every clause has a true literal.", steps[0].Caption);
        Assert.Equal(new Assignment([new("x1", "False"), new("x2", "False")]), steps[0].Partial);
        Assert.Equal(StepEvent.Done, steps[1].Event);
        Assert.True(steps[1].Ok);
    }

    [Fact]
    public void BruteForce_WhenNothingSatisfiesTheFormula_EndsWithADoneThatSaysSo() {
        var steps = StepsOf(Create("satbruteforcesolver").Run("(x1) & (!x1)", withSteps: true));
        Assert.All(steps.Take(steps.Count - 1), s => Assert.Equal(StepEvent.Reject, s.Event));
        Assert.Equal(StepEvent.Done, steps[^1].Event);
        Assert.False(steps[^1].Ok);
        Assert.Empty(steps[^1].Partial.Values);
        Assert.Equal("No assignment makes every clause true, so no solution exists.", steps[^1].Caption);
        Assert.Equal("Try x1 = False: clause C1 (x1) has no true literal.", steps[0].Caption);
        Assert.Equal("Try x1 = True: clause C2 (!x1) has no true literal.", steps[1].Caption);
        Assert.Equal(3, steps.Count);
    }

    [Fact]
    public void BruteForce_StepsAreCappedAndSayHowManyAreHidden() {
        // Ten variables, unsatisfiable: 1024 candidates, far more than the cap of 150.
        var run = Create("satbruteforcesolver").Run("(x1) & (!x1) & (x2 | x3 | x4 | x5 | x6 | x7 | x8 | x9 | x10)", withSteps: true);
        Assert.Equal("No solution exists", run.Answer);
        Assert.Equal(StepRecorder<Assignment>.DefaultCap + 1, run.Steps.Count);
        Assert.Contains("not shown", ((SolverStep<Assignment>)run.Steps[^1]).Caption);
    }

    [Fact]
    public void BruteForce_OwnRun_HonoursTheTimeLimit() {
        // Unsatisfiable with 40 variables: 2^40 candidates, so only the time limit ends the recording run.
        string instance = "(x1) & (!x1) & (" + string.Join(" | ", Enumerable.Range(2, 39).Select(i => $"x{i}")) + ")";
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var run = Create("satbruteforcesolver").Run(instance, withSteps: true, TimeSpan.FromMilliseconds(50));
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), $"took {sw.Elapsed}");
        Assert.True(run.TimedOut);
        Assert.Equal(SolveRun.TimedOutAnswer, run.Answer);
        var done = (SolverStep<Assignment>)run.Steps[^1];
        Assert.Equal(StepEvent.Done, done.Event);
        Assert.False(done.Ok);
        Assert.StartsWith("Stopped after 0.05 s", done.Caption);
    }

    [Fact]
    public void BruteForce_StoppedByItsOwnTimer_EndsWithADoneThatSaysItDidNotFinish() {
        // timerHasExpired is the solver's own stop flag (ISolver.TimerExpired), separate from the recording time limit.
        var solver = new SATBruteForceSolver();
        ((ISolver)solver).TimerExpired();
        var run = ((ISolver)solver).Run("(x1 | x2) & (!x1)", withSteps: true);
        Assert.False(run.TimedOut);
        Assert.Equal("timeout", run.Answer);   // what solve has always answered
        var done = Assert.Single(StepsOf(run));
        Assert.Equal((StepEvent.Done, (bool?)false), (done.Event, done.Ok));
        Assert.Equal(GroupedGraphFrames.SolverTimeoutCaption, done.Caption);
        Assert.Empty(done.Partial.Values);   // no candidate is shown as an answer
        Assert.Equal("timeout", solver.solve("(x1 | x2) & (!x1)"));

        // The frames end on that stopped Done, and the answer "timeout" is never read as an assignment.
        var frames = GroupedGraphFrames.Build(GroupedGraphSpec.FromLiterals(new SAT("(x1 | x2) & (!x1)").clauses), run,
            _ => throw new InvalidOperationException("not read"), _ => throw new InvalidOperationException("not verified")).Frames;
        var last = Assert.IsType<GroupedGraphFrame>(Assert.Single(frames));
        Assert.Equal((StepEvent.Done, (bool?)false, GroupedGraphFrames.SolverTimeoutCaption), (last.Event, last.Ok, last.Caption));
        Assert.Empty(last.Assignment);
    }

    [Fact]
    public void BruteForce_IsNotOfferedAsAGenericSolverForSat() {
        // It records typed steps by overriding Run, not by implementing ISolver<SAT, Assignment>: that interface would add
        // it to the navigation lists, which this change must not alter.
        var type = typeof(SATBruteForceSolver);
        Assert.DoesNotContain(type.GetInterfaces(), i => i.IsGenericType);
    }

    // ---- 3SAT backtracking ----

    [Fact]
    public void Backtracking_TriesEachValue_AcceptsWhatKeepsEveryClauseAlive_AndEndsOnTheAssignment() {
        var run = Create("sat3backtrackingsolver").Run(new SAT3().defaultInstance, withSteps: true);
        var steps = StepsOf(run);
        Assert.Equal("(x1:True,x2:True,x3:False)", run.Answer);
        Assert.Equal(StepEvent.Try, steps[0].Event);
        Assert.Equal("Try x1 = True.", steps[0].Caption);
        Assert.Equal(["v:x1"], steps[0].Focus);
        Assert.Equal(StepEvent.Accept, steps[1].Event);
        Assert.Equal("Keep x1 = True: no clause has every literal false yet, so this branch is queued.", steps[1].Caption);
        Assert.Equal("Try x1 = False.", steps[2].Caption);
        Assert.Equal("Try x2 = True after x1 = True.", steps[4].Caption);
        Assert.Equal(new Assignment([new("x1", "True"), new("x2", "True")]), steps[4].Partial);
        Assert.Equal("Keep x2 = True: every clause now has a true literal.", steps[5].Caption);
        // The variable nobody needed is filled in as False, as the answer says, on the Done step.
        Assert.Equal(new Assignment([new("x1", "True"), new("x2", "True"), new("x3", "False")]), steps[^1].Partial);
        Assert.Equal("Every clause has a true literal under x1 = True, x2 = True, x3 = False.", steps[^1].Caption);
    }

    [Fact]
    public void Backtracking_ARejectNamesTheClauseThatHasNoTrueLiteralLeft_AndABacktrackSaysWhereItWentBack() {
        var steps = StepsOf(Create("sat3backtrackingsolver").Run("(x1 | x2) & (!x1 | x2) & (x1 | !x2) & (!x1 | !x2)", withSteps: true));
        var reject = steps.First(s => s.Event == StepEvent.Reject);
        Assert.Equal("x2 = True fails: clause C4 (!x1 | !x2) has no true literal left.", reject.Caption);
        Assert.Equal(["v:x2", "c3"], reject.Focus);
        Assert.Equal(new Assignment([new("x1", "True"), new("x2", "True")]), reject.Partial);   // the value tried is in the partial, so the clause shows
        var back = steps.First(s => s.Event == StepEvent.Backtrack);
        Assert.Equal("Neither value of the last variable works. Go back to x1 = False, the most promising branch still open: x2 is next.", back.Caption);
        Assert.Equal(new Assignment([new("x1", "False")]), back.Partial);
        Assert.Equal(["v:x2"], back.Focus);
        Assert.Equal(StepEvent.Done, steps[^1].Event);
        Assert.False(steps[^1].Ok);
        Assert.Equal("Every branch was rejected, so there is no solution.", steps[^1].Caption);
        Assert.Equal(4, steps.Count(s => s.Event == StepEvent.Reject));
    }

    [Fact]
    public void Backtracking_LeavesTheProblemAlone_AndTheOverloadsAgree() {
        var problem = new SAT3("(x1 | !x2 | x3) & (!x1 | x3 | x1)");
        var literals = problem.literals.ToList();
        var clauses = problem.clauses.Select(c => c.ToList()).ToList();
        var solver = new Sat3BacktrackingSolver();
        var recorded = solver.Solve(problem, new StepRecorder<Assignment>());
        Assert.Equal(literals, problem.literals);
        Assert.Equal(clauses, problem.clauses);
        Assert.Equal(solver.solve(problem), recorded);
        Assert.Equal(solver.findSolution(problem), solver.findSolution(problem, new StepRecorder<Assignment>()));
    }

    [Fact]
    public void Backtracking_WhenRecordingIsOff_NoCaptionOrPartialIsBuilt() {
        var problem = new SAT3(new SAT3().defaultInstance);
        // The shared Off recorder never invokes a lambda; a throwing lambda would show if one were built.
        var off = StepRecorder<Assignment>.Off;
        off.Try(() => throw new InvalidOperationException(), () => throw new InvalidOperationException(), "v:x1");
        Assert.Equal("(x1:True,x2:True,x3:False)", new Sat3BacktrackingSolver().Solve(problem, off));
        Assert.Empty(off.Steps);
    }
}
