using System.Text.Json;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Quantum;
using API.Interfaces.Steps;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// The Quantum Circuit picture's base on its own: the circuits built from instances, the simulation behind the outcome
// bars, ids, the payload, the states of each frame, the answer-only Done frame and a stopped run. The solvers' steps and
// the endpoint are tested in QuantumSolvers_Tests and QuantumCircuitFrames_Endpoint_Tests.
public class QuantumCircuitFrames_Tests {
    private static string Json(object value) => JsonSerializer.Serialize(value, FramesResponse.JsonOptions);
    private static string Compact(string json) => JsonSerializer.Serialize(JsonDocument.Parse(json).RootElement);
    private static string States(IReadOnlyDictionary<string, StateRef> map) => string.Join(" ", map.Select(p => $"{p.Key}:{p.Value.State}"));

    private static int[] Table(string s) => s.Trim('(', ')').Split(',').Select(int.Parse).ToArray();

    // Bernstein-Vazirani's default: f(x) = 101 · x.
    private static readonly QuantumCircuitSpec Bv = QuantumCircuits.BernsteinVazirani(Table("(0,1,0,1,1,0,1,0)").Select(v => v == 1).ToArray());

    private static QuantumDistribution Final(QuantumCircuitSpec spec) => new QuantumSimulation(spec).After(spec.Columns.Count)!;

    // ---- Ids and payload ----

    [Fact]
    public void Ids_ArePositions() {
        Assert.Equal(("q0", "t3", "g2_0", "g4_2"), (QuantumCircuits.QubitId(0), QuantumCircuits.ColumnId(3),
            QuantumCircuits.GateId(2, new QuantumGate("box", [0, 1, 2, 3])), QuantumCircuits.GateId(4, new QuantumGate("measure", [2]))));
    }

    [Fact]
    public void Payload_Json_BernsteinVazirani_IsTheMockupCircuit() {
        Assert.Equal(
            "{\"kind\":\"quantumCircuit\",\"problem\":\"bernsteinVazirani\","
            + "\"qubits\":[{\"id\":\"q0\",\"name\":\"x0\",\"role\":\"query\"},{\"id\":\"q1\",\"name\":\"x1\",\"role\":\"query\"},{\"id\":\"q2\",\"name\":\"x2\",\"role\":\"query\"},{\"id\":\"q3\",\"name\":\"y\",\"role\":\"helper\"}],"
            + "\"columns\":[{\"id\":\"t0\",\"gates\":[{\"id\":\"g0_3\",\"type\":\"x\",\"qubits\":[3]}]},"
            + "{\"id\":\"t1\",\"gates\":[{\"id\":\"g1_0\",\"type\":\"h\",\"qubits\":[0]},{\"id\":\"g1_1\",\"type\":\"h\",\"qubits\":[1]},{\"id\":\"g1_2\",\"type\":\"h\",\"qubits\":[2]},{\"id\":\"g1_3\",\"type\":\"h\",\"qubits\":[3]}]},"
            + "{\"id\":\"t2\",\"gates\":[{\"id\":\"g2_0\",\"type\":\"box\",\"qubits\":[0,1,2,3],\"label\":\"U_f\",\"oracle\":true}]},"
            + "{\"id\":\"t3\",\"gates\":[{\"id\":\"g3_0\",\"type\":\"h\",\"qubits\":[0]},{\"id\":\"g3_1\",\"type\":\"h\",\"qubits\":[1]},{\"id\":\"g3_2\",\"type\":\"h\",\"qubits\":[2]}]},"
            + "{\"id\":\"t4\",\"gates\":[{\"id\":\"g4_0\",\"type\":\"measure\",\"qubits\":[0]},{\"id\":\"g4_1\",\"type\":\"measure\",\"qubits\":[1]},{\"id\":\"g4_2\",\"type\":\"measure\",\"qubits\":[2]}]}],"
            + "\"stages\":[{\"label\":\"prepare\",\"from\":0,\"to\":1},{\"label\":\"query once\",\"from\":2,\"to\":2},{\"label\":\"interfere\",\"from\":3,\"to\":3},{\"label\":\"read\",\"from\":4,\"to\":4}],"
            + "\"measure\":[0,1,2],\"readout\":\"bits\",\"inputBits\":3,\"simulated\":true}",
            Compact(Json(QuantumCircuitFrames.Payload(Bv))));
    }

    [Fact]
    public void Payload_WithoutACircuit_SaysWhy() {
        var payload = QuantumCircuitFrames.Payload(QuantumCircuits.PrimeFactorization("12"));
        Assert.Equal("{\"kind\":\"quantumCircuit\",\"problem\":\"primeFactorization\",\"qubits\":[],\"columns\":[],\"stages\":[],\"measure\":[],"
            + "\"readout\":\"bits\",\"inputBits\":0,\"simulated\":false,\"note\":\"No quantum circuit is needed for 12: the classical checks factor it completely.\",\"number\":12}",
            Compact(Json(payload)));
    }

    // ---- Circuits ----

    [Fact]
    public void Deutsch_HasTwoQubits_FiveColumns_AndTheOracleInTheMiddle() {
        var spec = QuantumCircuits.Deutsch([false, true]);
        Assert.Equal(["x0 query", "y helper"], spec.Wires.Select(w => $"{w.Name} {w.Role}"));
        Assert.Equal(["x", "h h", "box", "h", "measure"], spec.Columns.Select(c => string.Join(" ", c.Gates.Select(g => g.Type))));
        Assert.Equal("g2_0", spec.OracleGateId);
        Assert.Equal([0], spec.Measure);
        Assert.True(spec.Simulated);
    }

    [Theory]
    [InlineData("deutschJozsa", "(1,1,1)", "The table has 3 entries; a circuit needs a power of two (2, 4, 8, ...).")]
    [InlineData("deutschJozsa", "(1)", "A one-entry table has no input bits, so there is nothing for a circuit to query.")]
    [InlineData("deutschJozsa", "(0,0,2,2)", "f must output bits (0 or 1), so this table has no circuit.")]
    [InlineData("simon", "(1,2,3)", "The table has 3 entries; a circuit needs a power of two (2, 4, 8, ...).")]
    [InlineData("simon", "(0,-1)", "f's outputs must be whole numbers, 0 or more, to be written into output qubits.")]
    [InlineData("simon", "(0,2147483647)", "The circuit needs 32 qubits (1 query, 31 output), too many to draw here.")]
    [InlineData("unstructuredSearch", "(1)", "A one-entry table has no input bits, so there is nothing for a circuit to query.")]
    public void ATableWithNoCircuit_HasANote(string problem, string table, string note) {
        var values = Table(table);
        var spec = problem switch {
            "deutschJozsa" => QuantumCircuits.DeutschJozsa(values),
            "simon" => QuantumCircuits.Simon(values),
            _ => QuantumCircuits.UnstructuredSearch(values),
        };
        Assert.Empty(spec.Wires);
        Assert.Empty(spec.Columns);
        Assert.False(spec.Simulated);
        Assert.Null(spec.OracleGateId);
        Assert.Equal(note, spec.Note);
    }

    [Theory]
    [InlineData("12", null, "No quantum circuit is needed for 12: the classical checks factor it completely.")]
    [InlineData("13", null, "No quantum circuit is needed for 13: the classical checks factor it completely.")]
    [InlineData("9", null, "No quantum circuit is needed for 9: the classical checks factor it completely.")]
    [InlineData("1", null, "Only whole numbers of at least 2 have prime factors to find.")]
    [InlineData("-15", null, "Only whole numbers of at least 2 have prime factors to find.")]
    [InlineData("15", 12, "Shor's period finding for 15 with a = 2, whose period is 4. A real run picks a at random.")]
    [InlineData("42", 15, "Shor's period finding for 21 with a = 2, whose period is 6. A real run picks a at random.")]
    [InlineData("1001", null, "Shor's algorithm finds a period for 1,001 on 30 qubits, too many to draw here.")]
    public void PrimeFactorization_DrawsShorsCircuitForThePartThatNeedsIt(string number, int? qubits, string note) {
        var spec = QuantumCircuits.PrimeFactorization(number);
        Assert.Equal(qubits ?? 0, spec.Wires.Count);
        Assert.Equal(note, spec.Note);
        Assert.False(spec.Simulated);
        if (qubits is int q) {
            int t = q / 3 * 2;
            Assert.Equal("decimal", spec.Readout);
            Assert.Equal(Enumerable.Range(0, t), spec.Measure);
            Assert.Equal(t + 4, spec.Columns.Count);   // X, H, t controlled multiplications, QFT†, measure
            Assert.Equal(Enumerable.Range(0, t).Reverse().Select(c => (int?)c), spec.Columns.Skip(2).Take(t).Select(c => c.Gates[0].Control));
        }
    }

    // ---- The simulation behind the bars ----

    [Fact]
    public void BernsteinVazirani_ReadsTheHiddenString_AndTheOracleFlipsTheSignsOfSDotX() {
        var sim = new QuantumSimulation(Bv);
        var afterOracle = sim.After(3)!;
        for (int x = 0; x < 8; x++) {
            Assert.Equal(0.125, afterOracle.Probabilities[x], 9);
            Assert.Equal(System.Numerics.BitOperations.PopCount((uint)(x & 5)) % 2 == 1 ? -1 : 1, afterOracle.Signs![x]);
        }
        var final = Final(Bv);
        Assert.Equal(5, final.MostLikely);
        Assert.Equal(1, final.Probabilities[5], 9);
    }

    [Theory]
    [InlineData("(1,1,1,1)", 1.0)]
    [InlineData("(0,1,1,0,1,0,0,1)", 0.0)]
    [InlineData("(1,0,0,0)", 0.25)]
    public void DeutschJozsa_AllZerosHasProbabilityOneIffConstant(string table, double allZeros) {
        Assert.Equal(allZeros, Final(QuantumCircuits.DeutschJozsa(Table(table))).Probabilities[0], 9);
    }

    [Theory]
    [InlineData(false, false, 0)]
    [InlineData(true, true, 0)]
    [InlineData(false, true, 1)]
    [InlineData(true, false, 1)]
    public void Deutsch_ReadsOneIffBalanced(bool f0, bool f1, int read) {
        var d = Final(QuantumCircuits.Deutsch([f0, f1]));
        Assert.Equal(read, d.MostLikely);
        Assert.Equal(1, d.Probabilities[read], 9);
    }

    [Theory]
    [InlineData("(5,6,5,6,3,2,3,2)", 2)]
    [InlineData("(0,1,2,3)", 0)]
    [InlineData("(11,3,4,9,7,5,2,0,2,0,7,5,4,9,11,3)", 14)]
    public void Simon_EveryOutcomeIsOrthogonalToS(string table, int s) {
        var d = Final(QuantumCircuits.Simon(Table(table)));
        for (int y = 0; y < d.Probabilities.Length; y++)
            if (d.Probabilities[y] > 1e-9) Assert.Equal(0, System.Numerics.BitOperations.PopCount((uint)(y & s)) % 2);
    }

    [Theory]
    [InlineData("(0,1,0,0)", 1, 1, 1.0)]
    [InlineData("(0,0,0,0,0,1,0,0)", 5, 2, 0.94)]
    [InlineData("(0,0,0,0,0,0,0,0,0,0,0,1,0,0,0,0)", 11, 3, 0.96)]
    public void Grover_PeaksOnTheMarkedItem(string table, int marked, int rounds, double atLeast) {
        var spec = QuantumCircuits.UnstructuredSearch(Table(table));
        Assert.Equal(1 + 2 * rounds + 1, spec.Columns.Count);
        var d = Final(spec);
        Assert.Equal(marked, d.MostLikely);
        Assert.True(d.Probabilities[marked] >= atLeast, $"{d.Probabilities[marked]}");
    }

    // #848: Unstructured Search numbers an item's bits with x0 the least significant, so the answer is read high bit first
    // (x_{n-1} ... x0) and an outcome is the item number; qubit k holds x_k.
    [Theory]
    [InlineData("(0,1,0,0)", 1, new[] { 1, 0 })]
    [InlineData("(0,0,1,0)", 2, new[] { 1, 0 })]
    [InlineData("(0,0,0,0,0,0,1,0)", 6, new[] { 2, 1, 0 })]
    [InlineData("(0,0,0,0,0,0,0,0,0,0,0,1,0,0,0,0)", 11, new[] { 3, 2, 1, 0 })]
    public void Grover_ReadsHighBitFirst_SoAnOutcomeIsTheItem_AndQubitKHoldsBitK(string table, int marked, int[] measure) {
        var spec = QuantumCircuits.UnstructuredSearch(Table(table));
        Assert.Equal(measure, spec.Measure);
        var bars = QuantumCircuitFrames.Bars(Final(spec), spec.Measure.Length);
        Assert.Equal(marked, bars.OrderByDescending(b => b.Probability).First().Outcome);
        var state = new QuantumState(spec.Wires.Count);
        foreach (var column in spec.Columns) column.Apply?.Invoke(state);
        for (int k = 0; k < spec.Wires.Count; k++) {
            var x = state.Distribution([k]);
            Assert.Equal((marked >> k) & 1, (int)x.MostLikely);
        }
    }

    [Fact]
    public void Grover_NothingMarked_RunsNoRounds_AndStaysEven() {
        var spec = QuantumCircuits.UnstructuredSearch(Table("(0,0,0,0)"));
        Assert.Equal(2, spec.Columns.Count);
        Assert.All(Final(spec).Probabilities, p => Assert.Equal(0.25, p, 9));
    }

    [Fact]
    public void EverySimulatedColumn_KeepsTheProbabilitiesSummingToOne() {
        foreach (var spec in new[] { Bv, QuantumCircuits.Simon(Table("(5,6,5,6,3,2,3,2)")), QuantumCircuits.UnstructuredSearch(Table("(0,0,0,0,0,1,0,0)")) }) {
            var sim = new QuantumSimulation(spec);
            for (int k = 0; k <= spec.Columns.Count; k++) Assert.Equal(1, sim.After(k)!.Probabilities.Sum(), 9);
        }
    }

    [Fact]
    public void Captions_ReadTheSimulation() {
        var sim = new QuantumSimulation(Bv);
        Assert.Equal([
            "Flip the helper qubit y to 1.",
            "Apply H to every qubit: the query qubits now hold all 8 inputs at once, each with amplitude 1/√8, and y becomes |−⟩.",
            "One call to the oracle U_f. Because y is |−⟩, every input x with f(x) = 1 (4 of 8) gets its sign flipped: phase kickback.",
            "Apply H to the query qubits again. The signs interfere: 101 now has probability 100%.",
            "Measure the query qubits: 101 with probability 100%.",
        ], Enumerable.Range(0, 5).Select(sim.Caption));
        var grover = QuantumCircuits.UnstructuredSearch(Table("(0,0,0,0,0,1,0,0)"));
        Assert.Equal("Round 2: reflect every amplitude about the average. The marked item now has probability 94.5%.", new QuantumSimulation(grover).Caption(4));
    }

    // The rounds drawn and simulated here are the rounds the quantum server runs: quantumsolver's grover_rounds (#24 there)
    // tests the same table.
    [Theory]
    [InlineData(4, 1, 1)]
    [InlineData(8, 1, 2)]
    [InlineData(16, 1, 3)]
    [InlineData(8, 2, 1)]
    [InlineData(4, 2, 1)]
    [InlineData(4, 3, 1)]
    [InlineData(64, 4, 3)]
    [InlineData(4, 0, 0)]
    [InlineData(4, 4, 0)]
    [InlineData(2, 2, 0)]
    public void GroverRounds_MatchTheQuantumServer(int items, int marked, int rounds) =>
        Assert.Equal(rounds, QuantumCircuits.GroverRounds(items, marked));

    // #907: when outcomes share the top probability, the caption says so and never names one picked from the tie.
    [Theory]
    [InlineData("(0,1,0,0)", "Measure: item 1 (01) with probability 100%.")]
    [InlineData("(0,1,1,0)", "Measure: every item is equally likely (25% each).")]
    [InlineData("(1,1,1,1)", "Measure: every item is equally likely (25% each).")]
    [InlineData("(0,1,0,0,0,0,1,0)", "Measure: item 1 (001) and item 6 (110) are equally likely (50% each).")]
    [InlineData("(1,1,1,1,1,0,0,0,0,0,0,0,0,0,0,0)", "Measure: 5 items are equally likely (19.1% each).")]
    public void Grover_MeasureCaption_SaysATieAsATie(string table, string caption) {
        var spec = QuantumCircuits.UnstructuredSearch(Table(table));
        Assert.Equal(caption, new QuantumSimulation(spec).Caption(spec.Columns.Count - 1));
    }

    [Fact]
    public void PhaseKickback_Captions_SayATieAsATie() {
        // Majority of 3 bits is balanced, and its four likeliest outcomes tie at 25% (#907).
        var spec = QuantumCircuits.DeutschJozsa(Table("(0,0,0,1,0,1,1,1)"));
        var sim = new QuantumSimulation(spec);
        Assert.Equal("Measure the query qubits: 001, 010, 100 and 111 are equally likely (25% each).", sim.Caption(4));
        var bv = QuantumCircuits.BernsteinVazirani(Table("(0,0,0,1,0,1,1,1)").Select(v => v == 1).ToArray());
        Assert.Equal("Apply H to the query qubits again. The signs interfere: 001, 010, 100 and 111 now have probability 25% each.", new QuantumSimulation(bv).Caption(3));
        Assert.Equal([0L, 2L], new QuantumDistribution([0.5, 0, 0.5, 0], null).Likeliest);
    }

    [Fact]
    public void ACircuitTooBigToSimulate_HasNoBars_AndCaptionsWithoutNumbers() {
        var spec = QuantumCircuits.BernsteinVazirani(new bool[1 << 16]);
        Assert.Equal(17, spec.Wires.Count);
        Assert.False(spec.Simulated);
        var sim = new QuantumSimulation(spec);
        Assert.Null(sim.After(3));
        Assert.Equal("Apply H to the query qubits again. The signs interfere.", sim.Caption(3));
    }

    // ---- Frames ----

    private static QuantumFrame Frame(QuantumCircuitSpec spec, QuantumProgress partial, StepEvent ev, string[] focus, bool? ok = null, bool incremental = true, bool runApplies = true) =>
        QuantumCircuitFrames.Frame(spec, new QuantumSimulation(spec), partial, ev, focus, "c", ok, incremental, runApplies);

    [Fact]
    public void Frame_Json_HasTheContractFieldsInOrder_AndOptionalOnesOnlyWhenSet() {
        using var doc = JsonDocument.Parse(Json(Frame(Bv, new QuantumProgress(), StepEvent.Try, [])));
        Assert.Equal(["event", "caption", "focus", "applied", "queries", "columns", "gates", "qubits", "step"], doc.RootElement.EnumerateObject().Select(p => p.Name));
        var done = Frame(Bv, new QuantumProgress(5, [new OracleQuery(4, 1)], 5, [new("101", 1024)], "(1,0,1)"), StepEvent.Done, [], ok: true);
        using var doneDoc = JsonDocument.Parse(Json(done));
        Assert.Equal(["event", "caption", "focus", "applied", "queries", "columns", "gates", "qubits", "step", "bars", "outcome", "counts", "result", "ok"],
            doneDoc.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal("[{\"input\":4,\"output\":1}]", JsonSerializer.Serialize(doneDoc.RootElement.GetProperty("queries")));
        Assert.Equal("[{\"outcome\":5,\"probability\":1,\"sign\":1}]", JsonSerializer.Serialize(doneDoc.RootElement.GetProperty("bars")));
        Assert.Equal("{\"101\":1024}", JsonSerializer.Serialize(doneDoc.RootElement.GetProperty("counts")));
    }

    [Fact]
    public void Frame_TheColumnBeingApplied_IsHighlighted_AndBarsShowTheStateAfterIt() {
        var f = Frame(Bv, new QuantumProgress(2), StepEvent.Try, Bv.ColumnFocus(1));
        Assert.Equal("t1:ElementHighlight", States(f.Columns));
        Assert.Equal("g1_0:ElementHighlight g1_1:ElementHighlight g1_2:ElementHighlight g1_3:ElementHighlight", States(f.Gates));
        Assert.Empty(f.Qubits);
        Assert.Equal(8, f.Bars!.Count);
        Assert.All(f.Bars, b => Assert.Equal((0.125, 1), (b.Probability, b.Sign)));
        // The oracle flips the signs of the inputs with s · x = 1.
        var oracle = Frame(Bv, new QuantumProgress(3), StepEvent.Try, Bv.ColumnFocus(2));
        Assert.Equal([1, -1, 1, -1, -1, 1, -1, 1], oracle.Bars!.Select(b => b.Sign));
    }

    [Fact]
    public void Frame_NoColumnApplied_HasNoBars() {
        Assert.Null(Frame(Bv, new QuantumProgress(0, [new OracleQuery(4, 1)]), StepEvent.Try, ["g2_0"]).Bars);
    }

    [Fact]
    public void Frame_AFailedStepOfAnIncrementalRun_MarksItsFocusRejected_AWholeCandidateOnlyHighlighted() {
        Assert.Equal("g2_0:Rejected", States(Frame(Bv, new QuantumProgress(0, [new OracleQuery(0, 0)]), StepEvent.Reject, ["g2_0"]).Gates));
        Assert.Equal("g2_0:ElementHighlight", States(Frame(Bv, new QuantumProgress(0, [new OracleQuery(0, 0)]), StepEvent.Reject, ["g2_0"], incremental: false).Gates));
    }

    [Fact]
    public void Frame_TheMeasuredAnswer_IsSolution_InAnAcceptAndACorrectDone_AndRejectedInAWrongOne() {
        var accept = Frame(Bv, new QuantumProgress(5, outcome: 5, result: "(1,0,1)"), StepEvent.Accept, ["t4"]);
        Assert.Equal("t4:ElementHighlight", States(accept.Columns));
        Assert.Equal("q0:Solution q1:Solution q2:Solution", States(accept.Qubits));
        Assert.Equal("g4_0:Solution g4_1:Solution g4_2:Solution", States(accept.Gates));
        var done = Frame(Bv, new QuantumProgress(5, outcome: 5, result: "(1,0,1)"), StepEvent.Done, [], ok: true);
        Assert.Equal("q0:Solution q1:Solution q2:Solution", States(done.Qubits));
        Assert.Equal("g4_0:Solution g4_1:Solution g4_2:Solution", States(done.Gates));
        Assert.Empty(done.Columns);
        var wrong = Frame(Bv, new QuantumProgress(5, outcome: 3, result: "(0,1,1)"), StepEvent.Done, [], ok: false);
        Assert.Equal("q0:Rejected q1:Rejected q2:Rejected", States(wrong.Qubits));
        // A Try that has an outcome (a server's counts) does not claim it as the answer yet.
        Assert.Empty(Frame(Bv, new QuantumProgress(5, outcome: 5), StepEvent.Try, []).Qubits);
    }

    [Fact]
    public void Frame_TheDoneOfARunThatNeverRanTheCircuit_LeavesEveryColumnUntraveled() {
        var done = Frame(Bv, new QuantumProgress(0, [new OracleQuery(4, 1), new OracleQuery(2, 0), new OracleQuery(1, 1)], result: "(1,0,1)"), StepEvent.Done, [], ok: true, runApplies: false);
        Assert.Equal("t0:Untraveled t1:Untraveled t2:Untraveled t3:Untraveled t4:Untraveled", States(done.Columns));
        Assert.Empty(done.Qubits);
        Assert.Null(done.Bars);
        Assert.Equal("(1,0,1)", done.Result);
    }

    [Fact]
    public void Frame_FocusOutsideTheCircuit_IsIgnoredByTheStateMaps() {
        var f = Frame(QuantumCircuits.PrimeFactorization("12"), new QuantumProgress(result: "(2)"), StepEvent.Accept, ["g2_0", "t9"]);
        Assert.Empty(f.Columns);
        Assert.Empty(f.Gates);
        Assert.Equal(["g2_0", "t9"], f.Focus);
    }

    [Fact]
    public void Bars_ManyMeasuredQubits_ListOnlyVisibleOutcomes() {
        // 1,024 items: spread evenly each is 0.1%, too thin to draw; after Grover's rounds only the marked one shows.
        var spec = QuantumCircuits.UnstructuredSearch(Enumerable.Range(0, 1024).Select(i => i == 9 ? 1 : 0).ToArray());
        var sim = new QuantumSimulation(spec);
        Assert.Empty(QuantumCircuitFrames.Bars(sim.After(1)!, 10));
        var bar = Assert.Single(QuantumCircuitFrames.Bars(sim.After(spec.Columns.Count)!, 10));
        Assert.Equal(9, bar.Outcome);
        Assert.True(bar.Probability > 0.99);
        // Up to 5 measured qubits every outcome with any probability is listed.
        Assert.Equal(8, QuantumCircuitFrames.Bars(new QuantumSimulation(Bv).After(2)!, 3).Count);
    }

    // ---- The whole response ----

    private static SolverStep<QuantumProgress> Step(QuantumProgress p, StepEvent ev, string[] focus, string caption, bool? ok = null) =>
        new(p, ev, focus, caption) { Ok = ok };

    [Fact]
    public void Build_DrawsEachStep_AndEndsWithTheDoneStep() {
        object[] steps = [
            Step(new QuantumProgress(1), StepEvent.Try, Bv.ColumnFocus(0), "a"),
            Step(new QuantumProgress(5, outcome: 5, result: "(1,0,1)"), StepEvent.Accept, ["t4"], "b"),
            Step(new QuantumProgress(5, outcome: 5, result: "(1,0,1)"), StepEvent.Done, [], "c", true),
        ];
        var response = QuantumCircuitFrames.Build(Bv, new SolveRun("(1,0,1)", steps, typeof(QuantumProgress)) { Incremental = true }, _ => throw new InvalidOperationException("not asked"));
        Assert.Equal("QuantumCircuit", response.Type);
        Assert.Equal(["a", "b", "c"], response.Frames.Cast<QuantumFrame>().Select(f => f.Caption));
        Assert.True(response.Frames.Cast<QuantumFrame>().Last().Ok);
    }

    [Fact]
    public void Build_StepsThatCannotBeDrawn_GiveOneDoneFrameFromTheAnswer() {
        var run = new SolveRun("(1,0,1)", ["legacy"], null);
        var frame = Assert.IsType<QuantumFrame>(Assert.Single(QuantumCircuitFrames.Build(Bv, run, a => a == "(1,0,1)").Frames));
        Assert.Equal((StepEvent.Done, "The answer is (1,0,1).", true, "(1,0,1)"), (frame.Event, frame.Caption, frame.Ok, frame.Result));
        var wrong = Assert.IsType<QuantumFrame>(Assert.Single(QuantumCircuitFrames.Build(Bv, new SolveRun("(1,1,1)", [], null), _ => false).Frames));
        Assert.Equal(("The answer (1,1,1) is not correct.", false), (wrong.Caption, wrong.Ok));
    }

    [Fact]
    public void Build_AStoppedRun_EndsBlank_AndNeverReadsItsAnswer() {
        object[] steps = [
            Step(new QuantumProgress(2), StepEvent.Try, Bv.ColumnFocus(1), "a"),
            Step(new QuantumProgress(), StepEvent.Done, [], "Stopped after 0.05 s and 2 steps; the solver had not finished.", false),
        ];
        var run = new SolveRun(SolveRun.TimedOutAnswer, steps, typeof(QuantumProgress)) { TimedOut = true, Incremental = true };
        var frames = QuantumCircuitFrames.Build(Bv, run, _ => throw new InvalidOperationException("a stopped run's answer is never verified")).Frames.Cast<QuantumFrame>().ToList();
        Assert.Equal(2, frames.Count);
        var done = frames[^1];
        Assert.Equal((StepEvent.Done, false, 0), (done.Event, done.Ok, done.Applied));
        Assert.Empty(done.Queries);
        Assert.Empty(done.Columns);
        Assert.Empty(done.Gates);
        Assert.Empty(done.Qubits);
        Assert.Null(done.Bars);
        Assert.Null(done.Result);
        // Stopped before any step: the blank Done is added.
        var bare = QuantumCircuitFrames.Build(Bv, new SolveRun(SolveRun.TimedOutAnswer, [], typeof(QuantumProgress)) { TimedOut = true }, _ => true);
        Assert.Equal(GraphSubsetFrames.StoppedCaption, Assert.IsType<QuantumFrame>(Assert.Single(bare.Frames)).Caption);
    }

    [Fact]
    public void EmptyAnswer_OfTheQuantumShape_IsNothingDone() {
        Assert.Equal(new QuantumProgress(), Shapes.EmptyAnswer(typeof(QuantumProgress)));
    }
}
