using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Problems.P.P_DFA;
using API.Problems.P.P_DFA.Solvers;
using API.Problems.P.P_DFA.Visualizations;
using API.Problems.P.P_NFA;
using API.Problems.P.P_NFA.Solvers;
using API.Problems.P.P_NFA.Verifiers;
using API.Problems.P.P_NFA.Visualizations;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The DFA and NFA solvers record steps for the Automaton picture. Recording must never change an answer.
// Endpoints/Snapshots/automaton_solver_answers.tsv holds what each solver returned (solver, instance, answer as a JSON
// string; tab separated) BEFORE the picture existed (with the #777-#780 fixes in), on its default instance, three
// hand-picked instances and 12 seeded random ones.
public class AutomatonSolvers_Tests {
    private static string SnapshotPath(string file) =>
        Path.Combine(AppContext.BaseDirectory, "Endpoints", "Snapshots", file);

    // Line breaks in a multi-run NFA answer are Environment.NewLine, so compare with them normalized.
    private static string Lines(string answer) => answer.Replace("\r\n", "\n");

    private static IEnumerable<(string Solver, string Instance, string Answer)> Rows() =>
        File.ReadAllLines(SnapshotPath("automaton_solver_answers.tsv")).Where(l => l.Length > 0).Select(l => {
            var parts = l.Split('\t');
            return (parts[0], parts[1], Lines(JsonSerializer.Deserialize<string>(parts[2])!));
        });

    public static TheoryData<string, string, string> RecordedAnswers {
        get {
            var data = new TheoryData<string, string, string>();
            foreach (var (solver, instance, answer) in Rows()) data.Add(solver, instance, answer);
            return data;
        }
    }

    public static TheoryData<string> NfaInstances => new(Rows().Where(r => r.Solver == "nfasolver").Select(r => r.Instance));

    private static AutomatonSpec SpecOf(string solver, string instance) => solver == "dfasolver"
        ? DFAVisualization.Spec(new DFA(instance))
        : NFAVisualization.Spec(new NFA(instance));

    private static List<SolverStep<ActiveStates>> Steps(ISolver solver, string instance) =>
        solver.Run(instance, withSteps: true).Steps.Cast<SolverStep<ActiveStates>>().ToList();

    private static HashSet<string> PictureIds(AutomatonSpec spec) {
        var p = AutomatonFrames.Payload(spec);
        var ids = p.States.Select(s => s.Id).Concat(p.Transitions.Select(t => t.Id)).ToHashSet();
        if (p.Garbage != null) ids.Add(p.Garbage.Id);
        return ids;
    }

    private static bool Accepts(string answer) => answer.StartsWith("The sequence of states to accept is:");

    // The end state of each run in an answer ("The sequence of states to accept is: 1, 2" -> 2).
    private static string[] Ends(string answer) => Lines(answer).Split('\n').Where(l => Accepts(l)).Select(l => l.Split(',')[^1].Split(':')[^1].Trim()).ToArray();

    [Theory]
    [MemberData(nameof(RecordedAnswers))]
    public void Solve_ReturnsWhatItReturnedBeforeThePicture_AndItsStepsNamePictureIds(string solverName, string instance, string answer) {
        var solver = (ISolver)Activator.CreateInstance(ProblemProvider.Solvers[solverName])!;
        var on = solver.Run(instance, withSteps: true);
        var off = solver.Run(instance, withSteps: false);
        Assert.Equal(answer, Lines(on.Answer));
        Assert.Equal(answer, Lines(off.Answer));
        Assert.Equal(answer, Lines(solver.solve(instance)));
        Assert.Empty(off.Steps);
        Assert.Equal(typeof(ActiveStates), on.StepShape);
        Assert.False(on.Incremental);

        var steps = on.Steps.Cast<SolverStep<ActiveStates>>().ToList();
        Assert.Equal(StepEvent.Done, steps[^1].Event);
        Assert.Single(steps, s => s.Event == StepEvent.Done);
        Assert.Equal(Accepts(answer), steps[^1].Ok);
        Assert.All(steps, s => Assert.False(string.IsNullOrWhiteSpace(s.Caption)));

        // Every id a step names is an id of the picture, and every state it is in is a state of the machine.
        var spec = SpecOf(solverName, instance);
        var ids = PictureIds(spec);
        Assert.All(steps, s => Assert.All(s.Focus, id => Assert.Contains(id, ids)));
        Assert.All(steps, s => Assert.All(s.Partial.States, st => Assert.Contains(st, spec.States)));
        // Solution only where the solver says it accepts: in the frames, no Solution before an Accept or an accepting Done.
        var frames = AutomatonFrames.Build(spec, on).Frames.Cast<AutomatonFrame>().ToList();
        Assert.All(frames.Where(f => f.Event is StepEvent.Try or StepEvent.Reject or StepEvent.Backtrack),
            f => Assert.DoesNotContain(f.Nodes.Values, n => n.State == PictureState.Solution));
        Assert.Equal(steps.Count, frames.Count);
    }

    // ---- DFA ----

    private const string DefaultDfa = "(({1,2,3},{a,b},{(1,a,2),(1,b,3),(2,a,2),(2,b,2),(3,a,2),(3,b,3)},1,{2}),";

    [Fact]
    public void Dfa_RecordsTheStartAndOneTryPerSymbol_WithTheArrowTaken() {
        var steps = Steps(new DFASolver(), DefaultDfa + "ba)");
        Assert.Equal(
            ["Start in 1.", "Read 'b': move from 1 to 3.", "Read 'a': move from 3 to 2.",
             "The input is used up in 2, an accept state, so the DFA accepts."],
            steps.Select(s => s.Caption));
        Assert.Equal([StepEvent.Try, StepEvent.Try, StepEvent.Try, StepEvent.Done], steps.Select(s => s.Event));
        Assert.Equal(["q:1"], steps[0].Focus);
        Assert.Equal(["q:1>q:3"], steps[1].Focus);
        Assert.Equal(new ActiveStates(["2"], 2), steps[^1].Partial);
        Assert.True(steps[^1].Ok);
    }

    [Fact]
    public void Dfa_AMissingMove_FallsIntoGarbage() {
        var steps = Steps(new DFASolver(), "(({1,2},{a,b},{(1,a,2)},1,{2}),ab)");
        Assert.Equal([StepEvent.Try, StepEvent.Try, StepEvent.Reject, StepEvent.Done], steps.Select(s => s.Event));
        var fall = steps[2];
        Assert.Equal("2 has no move on 'b', so the DFA falls into the Garbage state and stays there.", fall.Caption);
        Assert.Equal(["garbage"], fall.Focus);
        // No state: the machine is in Garbage, with the head on the symbol it could not read.
        Assert.Equal(new ActiveStates([], 1), fall.Partial);
        Assert.Equal(new ActiveStates([], 1), steps[^1].Partial);
        Assert.Equal("Rejected: 2 has no move on 'b', so the run ends in the Garbage state.", steps[^1].Caption);
        Assert.False(steps[^1].Ok);

        var frames = AutomatonFrames.Build(DFAVisualization.Spec(new DFA("(({1,2},{a,b},{(1,a,2)},1,{2}),ab)")),
            ((ISolver)new DFASolver()).Run("(({1,2},{a,b},{(1,a,2)},1,{2}),ab)", withSteps: true)).Frames.Cast<AutomatonFrame>().ToList();
        Assert.Equal(["garbage"], frames[2].Active);
        Assert.Equal(PictureState.Rejected, frames[2].Nodes["garbage"].State);
        Assert.Equal(PictureState.Rejected, frames[^1].Nodes["garbage"].State);
    }

    [Fact]
    public void Dfa_InputEndingInANonAcceptState_IsRejectedOnThatState() {
        var steps = Steps(new DFASolver(), DefaultDfa + "b)");
        var reject = Assert.Single(steps, s => s.Event == StepEvent.Reject);
        Assert.Equal("The input is used up in 3, which is not an accept state.", reject.Caption);
        Assert.Equal(["q:3"], reject.Focus);
        Assert.Equal(new ActiveStates(["3"], 1), steps[^1].Partial);
    }

    [Fact]
    public void Dfa_EmptyInput_StartsAndStops_WithoutReadingASymbol() {
        var steps = Steps(new DFASolver(), DefaultDfa + "ε)");
        Assert.Equal(["Start in 1.", "The input is used up in 1, which is not an accept state.",
            "Rejected: the run ended in 1, which is not an accept state."], steps.Select(s => s.Caption));
        Assert.All(steps, s => Assert.Equal(0, s.Partial.Position));
    }

    [Fact]
    public void Dfa_ASymbolOutsideTheAlphabet_IsRejectedWhereTheMachineIs() {
        var steps = Steps(new DFASolver(), DefaultDfa + "ac)");
        var reject = Assert.Single(steps, s => s.Event == StepEvent.Reject);
        Assert.Equal("'c' is not in the alphabet, so the DFA cannot read it.", reject.Caption);
        Assert.Equal(["q:2"], reject.Focus);
        Assert.Equal(new ActiveStates(["2"], 1), reject.Partial);
    }

    // ---- NFA, one run at a time ----

    [Fact]
    public void NfaBacktracking_FocusesTheArrowOfEachMove_AndTheStateOfEachAcceptOrDeadEnd() {
        var steps = Steps(new NFASolver(), "(({1,2,3},{a},{(1,a,2),(1,a,3)},1,{3}),a)");
        Assert.Equal(
            [(StepEvent.Try, "q:1"), (StepEvent.Try, "q:1>q:2"), (StepEvent.Reject, "q:2"), (StepEvent.Backtrack, "q:1>q:2"),
             (StepEvent.Try, "q:1>q:3"), (StepEvent.Accept, "q:3"), (StepEvent.Backtrack, "q:1>q:3")],
            steps.SkipLast(1).Select(s => (s.Event, s.Focus.Single())));
        Assert.Equal(new ActiveStates(["3"], 1), steps[^1].Partial);
        var accept = AutomatonFrames.Frame(NFAVisualization.Spec(new NFA("(({1,2,3},{a},{(1,a,2),(1,a,3)},1,{3}),a)")),
            steps[5].Partial.States, steps[5].Partial.Position, steps[5].Event, steps[5].Focus, steps[5].Caption);
        Assert.Equal(PictureState.Solution, accept.Nodes["q:3"].State);
    }

    [Theory]
    [MemberData(nameof(NfaInstances))]
    public void NfaPaths_InThePayload_AreTheRunsNFASolverFinds(string instance) {
        var nfa = new NFA(instance);
        var runs = new NFASolver().GetPathRuns(nfa);
        var paths = NFAVisualization.Spec(nfa) is var spec ? AutomatonFrames.Payload(spec).Paths! : null!;
        Assert.Equal(runs.Count, paths.Count);
        for (int i = 0; i < runs.Count; i++) {
            Assert.Equal(runs[i].accepted, paths[i].Accepted);
            // The states each path's frames are in: the start, then one per move.
            var states = paths[i].Frames.SkipLast(1).Select(f => f.Active.Single()).ToList();
            Assert.Equal(runs[i].states.Select(AutomatonFrames.StateId), states);
        }
    }

    // ---- NFA, every branch at once ----

    private const string DefaultNfa = "(({1,2,3},{a,b},{(1,a,2),(1,ε,2),(1,b,3),(2,a,2),(2,ε,3),(2,b,2),(3,a,2),(3,b,3)},1,{2}),a)";

    [Fact]
    public void NfaSubset_RecordsTheActiveSetAfterEachSymbol() {
        var steps = Steps(new NFASubsetSolver(), DefaultNfa);
        Assert.Equal(
            ["Start in 1, and follow ε to 2 and 3. The active states are {1, 2, 3}.",
             "Read 'a'. The active states are now {2, 3}, including ε moves.",
             "The input is used up with {2, 3} active; 2 is an accept state, so a run accepts.",
             "Accepted: a run ends in an accept state."],
            steps.Select(s => s.Caption));
        Assert.Equal([StepEvent.Try, StepEvent.Try, StepEvent.Accept, StepEvent.Done], steps.Select(s => s.Event));
        Assert.Equal(new ActiveStates(["1", "2", "3"], 0), steps[0].Partial);
        Assert.Equal(["q:1", "q:1>q:2", "q:2>q:3"], steps[0].Focus);
        Assert.Equal(new ActiveStates(["2", "3"], 1), steps[1].Partial);
        // Every arrow taken on 'a' (1 -> 2, 2 -> 2, 3 -> 2) and the ε move 2 -> 3 after it.
        Assert.Equal(["q:1>q:2", "q:2>q:2", "q:3>q:2", "q:2>q:3"], steps[1].Focus);
        Assert.Equal(["q:2"], steps[2].Focus);
        Assert.Equal(new ActiveStates(["2"], 1), steps[^1].Partial);
        Assert.Equal("The sequence of states to accept is: 1, 2", new NFASubsetSolver().solve(new NFA(DefaultNfa)));
    }

    [Fact]
    public void NfaSubset_WhenEveryBranchDies_RejectsTheLastActiveStates() {
        var steps = Steps(new NFASubsetSolver(), "(({1,2},{a,b},{(1,a,2)},1,{2}),ba)");
        Assert.Equal([StepEvent.Try, StepEvent.Reject, StepEvent.Done], steps.Select(s => s.Event));
        Assert.Equal("Read 'b': no active state has a 'b' transition, so every branch dies.", steps[1].Caption);
        Assert.Equal(["q:1"], steps[1].Focus);
        Assert.Equal(new ActiveStates([], 0), steps[1].Partial);
        Assert.False(steps[^1].Ok);
    }

    [Theory]
    [MemberData(nameof(NfaInstances))]
    public void NfaSubset_AgreesWithTheBacktrackingSolver_AndItsRunsPassTheVerifier(string instance) {
        var nfa = new NFA(instance);
        string subset = Lines(new NFASubsetSolver().solve(nfa));
        string backtracking = Lines(new NFASolver().solve(nfa));
        Assert.Equal(Accepts(backtracking), Accepts(subset));
        if (!Accepts(subset)) {
            Assert.Equal(backtracking, subset);
            return;
        }
        // The same accept states end a run, and every run it reports is one the backtracking solver finds too.
        Assert.Equal(Ends(backtracking).ToHashSet(), Ends(subset).ToHashSet());
        Assert.Equal(Ends(subset).Distinct().Count(), Ends(subset).Length);
        var verifier = new NFAVerifier();
        foreach (var line in subset.Split('\n')) {
            Assert.Contains(line, backtracking.Split('\n'));
            Assert.True(verifier.verify(nfa, line.Split(':')[1].Replace(" ", "")));
        }
        var done = Steps(new NFASubsetSolver(), instance)[^1];
        Assert.Equal(new ActiveStates(Ends(subset), done.Partial.Position), done.Partial);
    }

    [Fact]
    public void NfaSubset_ASymbolOutsideTheAlphabet_AnswersAsTheBacktrackingSolverDoes() {
        const string instance = "(({1,2},{a},{(1,a,2)},1,{2}),ac)";
        Assert.Equal(new NFASolver().solve(new NFA(instance)), new NFASubsetSolver().solve(new NFA(instance)));
    }

    [Fact]
    public void NfaSubset_IsFastWhereTheBacktrackingSolverIsNot() {
        // 2^40 runs for the backtracking solver; 40 steps for the subset simulation.
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var run = ((ISolver)new NFASubsetSolver()).Run(StepTimeLimit_Tests.SlowNfa, withSteps: true, TimeSpan.FromSeconds(5));
        Assert.False(run.TimedOut);
        Assert.Equal("No Solution Exists: No run accepts the input", run.Answer);
        Assert.Equal(43, run.Steps.Count); // start, 40 symbols, the closing Reject, Done
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5));
    }
}
