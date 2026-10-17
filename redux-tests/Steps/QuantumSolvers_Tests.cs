using System.Net;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Quantum;
using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The eleven solvers of the six quantum-algorithm problems record steps for the Quantum Circuit picture. Recording must
// never change an answer, nor how often a quantum server is asked. Endpoints/Snapshots holds what they returned BEFORE the
// picture existed:
//   - quantum_solver_answers.tsv: the six that need no server (solver, instance, answer as JSON strings, tab separated);
//   - quantum_server_answers.tsv: the five that call the server, against a fake server's canned reply (solver, instance,
//     status, reply, then the path and body they sent and the answer, as JSON strings).
public class QuantumSolvers_Tests {
    private static string SnapshotPath(string file) =>
        Path.Combine(AppContext.BaseDirectory, "Endpoints", "Snapshots", file);

    internal static IEnumerable<(string Solver, string Instance, string Answer)> Rows() =>
        File.ReadAllLines(SnapshotPath("quantum_solver_answers.tsv")).Where(l => l.Length > 0).Select(l => {
            var parts = l.Split('\t');
            return (parts[0], JsonSerializer.Deserialize<string>(parts[1])!, JsonSerializer.Deserialize<string>(parts[2])!);
        });

    internal static IEnumerable<(string Solver, string Instance, int Status, string Reply, string Path, string Body, string Answer)> ServerRows() =>
        File.ReadAllLines(SnapshotPath("quantum_server_answers.tsv")).Where(l => l.Length > 0).Select(l => {
            var p = l.Split('\t');
            string S(int i) => JsonSerializer.Deserialize<string>(p[i])!;
            return (p[0], S(1), int.Parse(p[2]), S(3), S(4), S(5), S(6));
        });

    public static TheoryData<string, string, string> RecordedAnswers {
        get {
            var data = new TheoryData<string, string, string>();
            foreach (var (solver, instance, answer) in Rows()) data.Add(solver, instance, answer);
            return data;
        }
    }

    public static TheoryData<string, string, int, string, string, string, string> RecordedServerAnswers {
        get {
            var data = new TheoryData<string, string, int, string, string, string, string>();
            foreach (var r in ServerRows()) data.Add(r.Solver, r.Instance, r.Status, r.Reply, r.Path, r.Body, r.Answer);
            return data;
        }
    }

    // The visualization that draws each problem's picture in these tests (any of a problem's visualizations would do).
    internal static readonly Dictionary<string, string> VisualizationOf = new() {
        ["deutschclassicalsolver"] = "deutschdefaultvisualization",
        ["deutschquantumsolver"] = "deutschdefaultvisualization",
        ["deutschjozsaclassicalsolver"] = "deutschjozsadefaultvisualization",
        ["deutschjozsaquantumsolver"] = "deutschjozsadefaultvisualization",
        ["bernsteinvaziraniclassicalsolver"] = "bernsteinvaziranidefaultvisualization",
        ["bernsteinvaziraniquantumsolver"] = "bernsteinvaziranidefaultvisualization",
        ["simonsolver"] = "simonvisualization",
        ["unstructuredsearchsolver"] = "unstructuredsearchvisualization",
        ["unstructuredgroversolver"] = "unstructuredsearchvisualization",
        ["primefactorsolver"] = "shorsdefaultvisualization",
        ["shorsquantumsolver"] = "shorsdefaultvisualization",
    };

    private static ISolver Solver(string name) => (ISolver)Activator.CreateInstance(ProblemProvider.Solvers[name])!;

    private static List<SolverStep<QuantumProgress>> Steps(SolveRun run) => run.Steps.Cast<SolverStep<QuantumProgress>>().ToList();

    private static List<QuantumFrame> Frames(string solver, string instance, SolveRun run) {
        var vis = (IFramesVisualization)Activator.CreateInstance(ProblemProvider.Visualizers[VisualizationOf[solver]])!;
        var response = vis.BuildFrames(instance, run);
        Assert.Equal("QuantumCircuit", response.Type);
        return response.Frames.Cast<QuantumFrame>().ToList();
    }

    private static IProblem ProblemFor(string solver, string instance) {
        var problemType = ProblemProvider.Solvers[solver].GetInterfaces()
            .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISolver<>)).GetGenericArguments()[0];
        return API.Interfaces.ParseGuard.CreateProblem(problemType, instance);
    }

    private static bool Verified(string solver, string instance, string answer) {
        try { return ProblemFor(solver, instance).defaultVerifier.verify(instance, answer); } catch { return false; }
    }

    // What every run of these solvers must satisfy: one Done, last, with the verifier's verdict and the answer as its result.
    private static void AssertWellFormed(string solver, string instance, SolveRun on) {
        var steps = Steps(on);
        Assert.NotEmpty(steps);
        Assert.Equal(StepEvent.Done, steps[^1].Event);
        Assert.Single(steps, s => s.Event == StepEvent.Done);
        Assert.All(steps, s => Assert.False(string.IsNullOrWhiteSpace(s.Caption)));
        Assert.All(steps.SkipLast(1), s => Assert.Null(s.Ok));
        Assert.Equal(Verified(solver, instance, on.Answer), steps[^1].Ok);
        Assert.Equal(IncrementalSolvers_Tests.Classification[solver], on.Incremental);
        // Columns are applied in order, one per step, and never more than the circuit has.
        var spec = SpecFor(solver, instance);
        Assert.All(steps, s => Assert.InRange(s.Partial.Applied, 0, spec.Columns.Count));
        // The frames draw every step, and their focus names ids of the picture.
        var frames = Frames(solver, instance, on);
        Assert.Equal(steps.Count, frames.Count);
        var ids = Enumerable.Range(0, spec.Columns.Count).Select(QuantumCircuits.ColumnId)
            .Concat(spec.Columns.SelectMany((c, i) => c.Gates.Select(g => QuantumCircuits.GateId(i, g)))).ToHashSet();
        Assert.All(frames, f => Assert.All(f.Focus, id => Assert.Contains(id, ids)));
    }

    private static QuantumCircuitSpec SpecFor(string solver, string instance) {
        var vis = Activator.CreateInstance(ProblemProvider.Visualizers[VisualizationOf[solver]])!;
        var hook = vis.GetType().GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQuantumCircuitVisualization<>));
        return (QuantumCircuitSpec)hook.GetMethod("Describe")!.Invoke(vis, [ProblemFor(solver, instance)])!;
    }

    // ---- Answers are unchanged ----

    [Theory]
    [MemberData(nameof(RecordedAnswers))]
    public void Solve_ReturnsWhatItReturnedBeforeThePicture_AndItsStepsFitThePicture(string solver, string instance, string answer) {
        var s = Solver(solver);
        var on = s.Run(instance, withSteps: true);
        var off = s.Run(instance, withSteps: false);
        Assert.Equal(answer, on.Answer);
        Assert.Equal(answer, off.Answer);
        Assert.Equal(answer, s.solve(instance));
        Assert.Empty(off.Steps);
        AssertWellFormed(solver, instance, on);
        Assert.Equal(answer, Steps(on)[^1].Partial.Result);
        // A classical solver never runs the circuit; it calls the oracle (or, for trial division, nothing at all).
        Assert.All(Steps(on), st => Assert.Equal(0, st.Partial.Applied));
    }

    [Theory]
    [MemberData(nameof(RecordedServerAnswers))]
    public void ServerSolver_ReturnsWhatItReturnedBeforeThePicture_AndAsksTheServerOnce(string solver, string instance, int status, string reply, string path, string body, string answer) {
        foreach (bool withSteps in new[] { false, true }) {
            using var _ = FakeQuantumServer.Install(out var server);
            server.Canned = ((HttpStatusCode)status, reply);
            var run = Solver(solver).Run(instance, withSteps);
            Assert.Equal(answer, run.Answer);
            var request = Assert.Single(server.Requests);
            Assert.Equal((path, body), request);
            if (withSteps) AssertWellFormed(solver, instance, run);
        }
    }

    [Theory]
    [InlineData("deutschquantumsolver", "(0,1)", "balanced", 1L)]
    [InlineData("deutschquantumsolver", "(1,1)", "constant", 0L)]
    [InlineData("deutschjozsaquantumsolver", "(1, 1, 1, 1)", "constant", 0L)]
    [InlineData("deutschjozsaquantumsolver", "(0,1,1,0,1,0,0,1)", "balanced", 7L)]
    [InlineData("bernsteinvaziraniquantumsolver", "(0,1,0,1,1,0,1,0)", "(1,0,1)", 5L)]
    [InlineData("unstructuredgroversolver", "(0, 1, 0, 0)", "1", 1L)]
    [InlineData("unstructuredgroversolver", "(0,0,0,0,0,0,0,0,0,0,0,1,0,0,0,0)", "11", 11L)]
    public void ServerSolver_AppliesEveryColumn_ThenReadsTheServersOutcome(string solver, string instance, string answer, long outcome) {
        using var _ = FakeQuantumServer.Install(out var server);
        var run = Solver(solver).Run(instance, withSteps: true);
        Assert.Equal(answer, run.Answer);
        AssertWellFormed(solver, instance, run);
        var spec = SpecFor(solver, instance);
        var steps = Steps(run);
        Assert.Equal(spec.Columns.Count + 2, steps.Count);
        for (int c = 0; c < spec.Columns.Count; c++) {
            Assert.Equal((StepEvent.Try, c + 1), (steps[c].Event, steps[c].Partial.Applied));
            Assert.Equal(spec.ColumnFocus(c), steps[c].Focus);
        }
        var accept = steps[^2];
        Assert.Equal(StepEvent.Accept, accept.Event);
        Assert.Equal((spec.Columns.Count, outcome, answer), (accept.Partial.Applied, accept.Partial.Outcome, accept.Partial.Result));
        Assert.Equal([QuantumCircuits.ColumnId(spec.Columns.Count - 1)], accept.Focus);
        Assert.Equal(FakeQuantumServer.Shots, Assert.Single(accept.Partial.Counts).Value);
        Assert.True(steps[^1].Ok);
        // The picture: the bars of the last column put the outcome on top, and the measured qubits are the answer.
        var frames = Frames(solver, instance, run);
        Assert.Equal(outcome, frames[^1].Bars!.MaxBy(b => b.Probability)!.Outcome);
        Assert.All(spec.Measure, q => Assert.Equal(PictureState.Solution, frames[^1].Qubits[QuantumCircuits.QubitId(q)].State));
        Assert.Single(server.Requests);
    }

    [Theory]
    [InlineData("deutschquantumsolver", "(0,1)")]
    [InlineData("deutschjozsaquantumsolver", "(1, 1, 1, 1)")]
    [InlineData("bernsteinvaziraniquantumsolver", "(0,1,0,1,1,0,1,0)")]
    [InlineData("shorsquantumsolver", "15")]
    public void ServerSolver_ServerDown_StillDrawsTheCircuit_ThenRejects(string solver, string instance) {
        using var _ = FakeQuantumServer.Install(out var server);
        server.Canned = (HttpStatusCode.ServiceUnavailable, "{}");
        var run = Solver(solver).Run(instance, withSteps: true);
        AssertWellFormed(solver, instance, run);
        var steps = Steps(run);
        Assert.Equal(StepEvent.Reject, steps[^2].Event);
        Assert.StartsWith("The quantum server gave no", steps[^2].Caption);
        Assert.False(steps[^1].Ok);
        Assert.Equal("No answer: the quantum server did not give one.", steps[^1].Caption);
        Assert.Null(steps[^1].Partial.Outcome);
        var frames = Frames(solver, instance, run);
        Assert.Empty(frames[^1].Qubits);
    }

    // ---- What each solver says ----

    private static List<string> Captions(string solver, string instance) {
        using var _ = FakeQuantumServer.Install(out var server);
        return Steps(Solver(solver).Run(instance, withSteps: true)).Select(s => $"{s.Event}: {s.Caption}").ToList();
    }

    [Fact]
    public void DeutschClassical_CallsTheOracleTwice() {
        Assert.Equal([
            "Try: Call the oracle with x = 0: f(0) = 0.",
            "Try: Call the oracle with x = 1: f(1) = 1.",
            "Accept: f(0) = 0 and f(1) = 1 differ, so f is balanced. That took 2 oracle calls; Deutsch's algorithm needs 1.",
            "Done: f is balanced.",
        ], Captions("deutschclassicalsolver", "(0,1)"));
        var run = Solver("deutschclassicalsolver").Run("(0,1)", withSteps: true);
        Assert.All(Steps(run).Take(2), s => Assert.Equal(["g2_0"], s.Focus));
        Assert.Equal([new OracleQuery(0, 0), new OracleQuery(1, 1)], Steps(run)[^1].Partial.Queries);
        // The picture: the oracle is highlighted while it is called, and the circuit was never run.
        var frames = Frames("deutschclassicalsolver", "(0,1)", run);
        Assert.Equal(PictureState.ElementHighlight, frames[0].Gates["g2_0"].State);
        Assert.All(Enumerable.Range(0, 5), c => Assert.Equal(PictureState.Untraveled, frames[^1].Columns[$"t{c}"].State));
        Assert.All(frames, f => Assert.Null(f.Bars));
    }

    [Fact]
    public void DeutschQuantum_NarratesEachColumn_ThenTheServersReading() {
        Assert.Equal([
            "Try: Flip the helper qubit y to 1.",
            "Try: Apply H to every qubit: x0 now holds both inputs, 0 and 1, at once, each with amplitude 1/√2, and y becomes |−⟩.",
            "Try: One call to the oracle U_f. Because y is |−⟩, every input x with f(x) = 1 (1 of 2) gets its sign flipped: phase kickback.",
            "Try: Apply H to x0 again. The signs interfere: 1 now has probability 100%.",
            "Try: Measure x0: 1 with probability 100%.",
            "Accept: The quantum server measured x0 = 1 in 1,024 of 1,024 runs, so f is balanced. One oracle call.",
            "Done: f is balanced, found with one call to the oracle.",
        ], Captions("deutschquantumsolver", "(0,1)"));
    }

    [Fact]
    public void DeutschJozsaClassical_StopsOnceMoreThanHalfAgree_OrAtTheFirstDifference() {
        Assert.Equal([
            "Try: Call the oracle with x = 00: f(00) = 1.",
            "Try: Call the oracle with x = 01: f(01) = 1, the same as f(00).",
            "Try: Call the oracle with x = 10: f(10) = 1, the same as f(00).",
            "Accept: The first 3 of 4 inputs all give 1: more than half, so f cannot be balanced. It is constant. That took 3 oracle calls; the Deutsch-Jozsa algorithm needs 1.",
            "Done: f is constant.",
        ], Captions("deutschjozsaclassicalsolver", "(1, 1, 1, 1)"));
        Assert.Equal([
            "Try: Call the oracle with x = 000: f(000) = 0.",
            "Accept: Call the oracle with x = 001: f(001) = 1, unlike f(000) = 0. f is not constant, so it is balanced, after 2 calls.",
            "Done: f is balanced.",
        ], Captions("deutschjozsaclassicalsolver", "(0,1,1,0,1,0,0,1)"));
        Assert.Equal([
            "Reject: f gives 1 for 1 of its 4 inputs: not none, not all and not exactly half. It breaks the promise, so there is no answer to find.",
            "Done: No answer: f breaks the promise.",
        ], Captions("deutschjozsaclassicalsolver", "(1,0,0,0)"));
    }

    [Fact]
    public void BernsteinVaziraniClassical_ReadsOneBitPerCall() {
        Assert.Equal([
            "Try: Call the oracle with x = 100: f(100) = 1, so bit 1 of s is 1.",
            "Try: Call the oracle with x = 010: f(010) = 0, so bit 2 of s is 0.",
            "Try: Call the oracle with x = 001: f(001) = 1, so bit 3 of s is 1.",
            "Accept: s = 101, after 3 oracle calls, one per bit; the Bernstein-Vazirani algorithm needs 1.",
            "Done: s = 101: f(x) = s · x (mod 2) for every x.",
        ], Captions("bernsteinvaziraniclassicalsolver", "(0,1,0,1,1,0,1,0)"));
        Assert.Equal("Done: s = 011 does not fit every input: f(x) is not s · x for any s, so f breaks the promise.",
            Captions("bernsteinvaziraniclassicalsolver", "(0,1,1,1,0,0,0,0)")[^1]);
    }

    [Fact]
    public void Simon_CallsEveryInput_AndNotesEachRepeat() {
        Assert.Equal([
            "Try: Call the oracle with x = 000: f(000) = 5.",
            "Try: Call the oracle with x = 001: f(001) = 6.",
            "Try: Call the oracle with x = 010: f(010) = 5, the same as f(000), so s could be 010.",
            "Try: Call the oracle with x = 011: f(011) = 6, the same as f(001), so s could be 010.",
            "Try: Call the oracle with x = 100: f(100) = 3.",
            "Try: Call the oracle with x = 101: f(101) = 2.",
            "Try: Call the oracle with x = 110: f(110) = 3, the same as f(100), so s could be 010.",
            "Try: Call the oracle with x = 111: f(111) = 2, the same as f(101), so s could be 010.",
            "Accept: Every output comes from exactly two inputs, and each pair differs by 010: s = 010. That took 8 oracle calls.",
            "Done: s = 010: f(x) = f(x ⊕ s) for every x.",
        ], Captions("simonsolver", "(5, 6, 5, 6, 3, 2, 3, 2)"));
        Assert.Equal("Reject: The output 3 comes from 4 inputs, not 2: f is not 2-to-1, so no secret string fits.", Captions("simonsolver", "(3,3,3,3)")[^2]);
        Assert.Equal("Done: No answer: f breaks Simon's promise.", Captions("simonsolver", "(3,3,3,3)")[^1]);
        Assert.Equal("Done: s is empty: f has no input bits.", Captions("simonsolver", "(7)")[^1]);
    }

    [Fact]
    public void UnstructuredSearchBruteForce_RejectsEachUnmarkedItem_AsAWholeCandidate() {
        Assert.Equal([
            "Reject: Call the oracle with item 0 (00): f = 0, not marked.",
            "Accept: Call the oracle with item 1 (01): f = 1, so it is marked.",
            "Done: Item 1 is marked, found after 2 oracle calls.",
        ], Captions("unstructuredsearchsolver", "(0, 1, 0, 0)"));
        Assert.Equal("Done: No item of the 4 is marked: no solution.", Captions("unstructuredsearchsolver", "(0,0,0,0)")[^1]);
        // Not incremental: a rejected item is highlighted as the candidate, not marked Rejected.
        var run = Solver("unstructuredsearchsolver").Run("(0, 1, 0, 0)", withSteps: true);
        Assert.False(run.Incremental);
        Assert.Equal(PictureState.ElementHighlight, Frames("unstructuredsearchsolver", "(0, 1, 0, 0)", run)[0].Gates["g1_0"].State);
    }

    [Fact]
    public void Grover_NarratesTheRounds_ThenTheServersItem() {
        var captions = Captions("unstructuredgroversolver", "(0,0,0,0,0,1,0,0)");
        Assert.Equal([
            "Try: Apply H to every qubit: all 8 items are equally likely, 12.5% each.",
            "Try: Round 1: the oracle flips the sign of the marked item. The probabilities stay the same; only the sign changes.",
            "Try: Round 1: reflect every amplitude about the average. The marked item now has probability 78.1%.",
            "Try: Round 2: the oracle flips the sign of the marked item. The probabilities stay the same; only the sign changes.",
            "Try: Round 2: reflect every amplitude about the average. The marked item now has probability 94.5%.",
            "Try: Measure: item 5 (101) with probability 94.5%.",
            "Accept: The quantum server measured item 5 (101) in 1,024 of 1,024 runs.",
            "Done: Item 5 is marked, found with 2 rounds of Grover's algorithm.",
        ], captions);
        // One round is "1 round", not "1 rounds" (#902).
        Assert.Equal("Done: Item 1 is marked, found with 1 round of Grover's algorithm.", Captions("unstructuredgroversolver", "(0,1,0,0)")[^1]);
    }

    [Fact]
    public void TrialDivision_KeepsEachFactor_AndRejectsEachDivisorThatFails() {
        Assert.Equal([
            "Accept: 12 ÷ 2 = 6, so 2 is a prime factor.",
            "Accept: 6 ÷ 2 = 3, so 2 is a prime factor.",
            "Accept: No number from 2 to √3 divides 3, so 3 is prime: the last factor.",
            "Done: 12 = 2 × 2 × 3.",
        ], Captions("primefactorsolver", "12"));
        Assert.Equal([
            "Reject: 2 does not divide 35.",
            "Reject: 3 does not divide 35.",
            "Reject: 4 does not divide 35.",
            "Accept: 35 ÷ 5 = 7, so 5 is a prime factor.",
            "Accept: No number from 2 to √7 divides 7, so 7 is prime: the last factor.",
            "Done: 35 = 5 × 7.",
        ], Captions("primefactorsolver", "35"));
        var run = Solver("primefactorsolver").Run("35", withSteps: true);
        Assert.Equal(["()", "()", "()", "(5)", "(5,7)", "(5,7)"], Steps(run).Select(s => s.Partial.Result));
    }

    [Fact]
    public void TrialDivision_ALargePrime_KeepsTheFirst150Tries_AndTheAnswer() {
        var steps = Steps(Solver("primefactorsolver").Run("2147483647", withSteps: true));
        Assert.Equal(StepRecorder<QuantumProgress>.DefaultCap + 2, steps.Count);
        Assert.Equal(StepEvent.Accept, steps[^2].Event);
        Assert.Equal("No number from 2 to √2,147,483,647 divides 2,147,483,647, so 2,147,483,647 is prime.", steps[^2].Caption);
        Assert.Equal("2,147,483,647 is prime. (46189 not shown)", steps[^1].Caption);
    }

    [Fact]
    public void Shor_SendsTheNumber_AndReadsTheFactors_WithoutRunningTheDrawnCircuit() {
        Assert.Equal([
            "Try: Send 15 to the quantum server, which runs Shor's algorithm: a quantum circuit finds a period, and classical steps turn it into factors.",
            "Accept: The quantum server answered (3,5).",
            "Done: 15 = 3 × 5.",
        ], Captions("shorsquantumsolver", "15"));
        using var _ = FakeQuantumServer.Install(out var server);
        var run = Solver("shorsquantumsolver").Run("15", withSteps: true);
        var frames = Frames("shorsquantumsolver", "15", run);
        Assert.Equal(12, frames[^1].Columns.Count);
        Assert.All(frames[^1].Columns.Values, s => Assert.Equal(PictureState.Untraveled, s.State));
        Assert.Equal("(3,5)", frames[^1].Result);
    }

    [Fact]
    public void ARunThatIsNotRecorded_NeitherSimulatesNorAsksForMore() {
        // A plain solve passes the shared Off recorder: no circuit is built, and the server is asked once, as before.
        using var _ = FakeQuantumServer.Install(out var server);
        Assert.Equal("(1,0,1)", Solver("bernsteinvaziraniquantumsolver").solve("(0,1,0,1,1,0,1,0)"));
        Assert.Single(server.Requests);
        Assert.False(QuantumSteps.Recording(StepRecorder<QuantumProgress>.Off));
    }
}
