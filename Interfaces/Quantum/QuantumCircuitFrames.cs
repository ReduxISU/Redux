using System.Text.Json.Serialization;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;

namespace API.Interfaces.Quantum;

// --- JSON shapes (explicit names so the contract does not depend on serializer naming options) ---

/// <summary>A qubit: its id (<c>q0</c>, by position), its name (<c>x0</c>, <c>y</c>) and its role (<c>query</c>, <c>helper</c>).</summary>
sealed record QuantumQubitDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("role")] string Role);

/// <summary>
/// A gate: its id (<c>g{column}_{first qubit}</c>), its type (<c>h</c>, <c>x</c>, <c>z</c>, <c>cx</c>, <c>measure</c>,
/// <c>box</c>), the qubits it acts on (by index; a box spans them), and, when it has them, its control qubit, its label and
/// whether it is the oracle.
/// </summary>
sealed record QuantumGateDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("qubits")] int[] Qubits) {
    [JsonPropertyName("control")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Control { get; init; }

    [JsonPropertyName("label")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Label { get; init; }

    [JsonPropertyName("oracle")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Oracle { get; init; }
}

/// <summary>A column of gates, in the order the columns run: its id (<c>t0</c>, by position) and its gates.</summary>
sealed record QuantumColumnDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("gates")] IReadOnlyList<QuantumGateDto> Gates);

/// <summary>A labelled run of columns, <c>from</c> to <c>to</c> inclusive.</summary>
sealed record QuantumStageDto(
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("from")] int From,
    [property: JsonPropertyName("to")] int To);

/// <summary>
/// The static part of a Quantum Circuit picture, sent once per response.
/// <list type="bullet">
/// <item><c>problem</c>: which problem (<c>deutsch</c>, <c>deutschJozsa</c>, <c>bernsteinVazirani</c>, <c>simon</c>,
/// <c>unstructuredSearch</c>, <c>primeFactorization</c>).</item>
/// <item><c>qubits</c>, <c>columns</c>, <c>stages</c>: the circuit; empty when the instance has none (see <c>note</c>).</item>
/// <item><c>measure</c>: the qubits the answer is read from, the most significant first.</item>
/// <item><c>readout</c>: <c>bits</c> (an outcome reads as a bit string) or <c>decimal</c>.</item>
/// <item><c>inputBits</c>: n, the number of input bits of the oracle f, to write a classical query's input as bits.</item>
/// <item><c>simulated</c>: whether frames carry outcome probabilities (<c>bars</c>).</item>
/// <item><c>note</c>: why there is no circuit, or what to know about it. <c>number</c>: the number to factor.</item>
/// </list>
/// </summary>
sealed record QuantumCircuitPayload(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("problem")] string Problem,
    [property: JsonPropertyName("qubits")] IReadOnlyList<QuantumQubitDto> Qubits,
    [property: JsonPropertyName("columns")] IReadOnlyList<QuantumColumnDto> Columns,
    [property: JsonPropertyName("stages")] IReadOnlyList<QuantumStageDto> Stages,
    [property: JsonPropertyName("measure")] int[] Measure,
    [property: JsonPropertyName("readout")] string Readout,
    [property: JsonPropertyName("inputBits")] int InputBits,
    [property: JsonPropertyName("simulated")] bool Simulated) {
    [JsonPropertyName("note")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Note { get; init; }

    [JsonPropertyName("number")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? Number { get; init; }
}

/// <summary>
/// One bar of the outcome chart: an outcome of the measured qubits (as a number, the first measured qubit the most
/// significant bit), its probability, and the sign of its amplitude (1 or -1; 0 when the amplitudes are complex).
/// </summary>
sealed record QuantumBarDto(
    [property: JsonPropertyName("outcome")] long Outcome,
    [property: JsonPropertyName("probability")] double Probability,
    [property: JsonPropertyName("sign")] int Sign);

/// <summary>
/// One picture of the run.
/// <list type="bullet">
/// <item><c>applied</c>: how many columns have run (0: none; a classical solver never runs any).</item>
/// <item><c>queries</c>: the classical oracle calls made so far, <c>{input, output}</c>.</item>
/// <item><c>columns</c>, <c>gates</c>, <c>qubits</c>: the state of each column, gate and qubit whose state is not
/// <see cref="PictureState.Background"/>.</item>
/// <item><c>bars</c>: the outcome probabilities once <c>applied</c> columns have run, when the circuit is simulated and at
/// least one column has run. With more than 5 measured qubits only outcomes above 0.4% are listed (at most 256).</item>
/// <item><c>outcome</c>: the value read from the measured qubits, once known. <c>counts</c>: how often each outcome came up,
/// when a quantum server said. <c>result</c>: the answer as the solver has it so far.</item>
/// </list>
/// </summary>
sealed record QuantumFrame(
    [property: JsonPropertyName("event")] StepEvent Event,
    [property: JsonPropertyName("caption")] string Caption,
    [property: JsonPropertyName("focus")] string[] Focus,
    [property: JsonPropertyName("applied")] int Applied,
    [property: JsonPropertyName("queries")] IReadOnlyList<OracleQuery> Queries,
    [property: JsonPropertyName("columns")] IReadOnlyDictionary<string, StateRef> Columns,
    [property: JsonPropertyName("gates")] IReadOnlyDictionary<string, StateRef> Gates,
    [property: JsonPropertyName("qubits")] IReadOnlyDictionary<string, StateRef> Qubits) {
    [JsonPropertyName("bars")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<QuantumBarDto>? Bars { get; init; }

    [JsonPropertyName("outcome")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? Outcome { get; init; }

    [JsonPropertyName("counts")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, long>? Counts { get; init; }

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Result { get; init; }

    [JsonPropertyName("ok")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Ok { get; init; }
}

/// <summary>
/// Derives the Quantum Circuit picture (Deutsch, Deutsch-Jozsa, Bernstein-Vazirani, Simon's Problem, Unstructured Search,
/// Prime Factorization) from (the instance's circuit, a step's <see cref="QuantumProgress"/>, step event, focus). Nothing
/// here knows about a particular solver, and nothing here calls a quantum server: the circuit comes from the instance and
/// the probabilities from simulating it (<see cref="QuantumSimulation"/>); what a server said comes only from the recorded
/// steps.
/// <para>
/// <b>Ids</b> are positions (see <see cref="QuantumCircuits"/>): qubits <c>q0</c>, columns <c>t0</c>, gates
/// <c>g{column}_{first qubit}</c>.
/// </para>
/// <para>
/// <b>States</b> (precedence <c>Rejected &gt; ElementHighlight &gt; Solution &gt; Covered &gt; Blocked &gt; Background</c>,
/// with <c>Untraveled</c> only where nothing else applies; see <see cref="GraphSubsetFrames.Raise"/>):
/// </para>
/// <list type="bullet">
/// <item>The focus (the column being applied and its gates, or the oracle during a classical query) is
/// <c>ElementHighlight</c>; in a Reject step of an incremental run it is <c>Rejected</c>
/// (<see cref="GraphSubsetFrames.FocusMark"/>).</item>
/// <item>A measured outcome is the answer in an Accept and in a correct Done: then the measured qubits and their measure
/// gates are <c>Solution</c>. In a Reject (an outcome that is not the answer) and in a Done with ok = false they are
/// <c>Rejected</c>.</item>
/// <item>In the Done frame of a run that applied no column (a classical solver), every column is <c>Untraveled</c>.</item>
/// <item>A run stopped by its time limit ends with a blank Done frame, ok = false (<see cref="StoppedFrame"/>).</item>
/// </list>
/// Columns already applied and columns still to come carry no state: <c>applied</c> says which are which. Covered and
/// Blocked are not used.
/// </summary>
static class QuantumCircuitFrames {
    public const string Kind = "quantumCircuit";

    /// <summary>Above this many measured qubits, <c>bars</c> lists only outcomes above <see cref="BarFloor"/>.</summary>
    public const int AllBarsUpTo = 5;
    public const double BarFloor = 0.004;
    public const int MaxBars = 256;

    // ---- The payload ----

    public static QuantumCircuitPayload Payload(QuantumCircuitSpec spec) =>
        new(Kind, spec.Problem,
            spec.Wires.Select((w, k) => new QuantumQubitDto(QuantumCircuits.QubitId(k), w.Name, w.Role)).ToList(),
            spec.Columns.Select((c, i) => new QuantumColumnDto(QuantumCircuits.ColumnId(i), c.Gates.Select(g =>
                new QuantumGateDto(QuantumCircuits.GateId(i, g), g.Type, g.Qubits) {
                    Control = g.Control,
                    Label = g.Label,
                    Oracle = g.Oracle ? true : null,
                }).ToList())).ToList(),
            spec.Stages.Select(s => new QuantumStageDto(s.Label, s.From, s.To)).ToList(),
            spec.Measure, spec.Readout, spec.InputBits, spec.Simulated) { Note = spec.Note, Number = spec.Number };

    // ---- Frames ----

    /// <summary>The outcome chart for a distribution: every outcome with any probability when at most
    /// <see cref="AllBarsUpTo"/> qubits are measured, else the ones above <see cref="BarFloor"/> (at most
    /// <see cref="MaxBars"/>, the most likely).</summary>
    public static List<QuantumBarDto> Bars(QuantumDistribution d, int measured) {
        var bars = new List<QuantumBarDto>();
        double floor = measured <= AllBarsUpTo ? 1e-9 : BarFloor;
        for (int i = 0; i < d.Probabilities.Length; i++)
            if (d.Probabilities[i] > floor)
                bars.Add(new QuantumBarDto(i, Math.Round(d.Probabilities[i], 6), d.Signs?[i] ?? 0));
        if (bars.Count > MaxBars)
            bars = bars.OrderByDescending(b => b.Probability).ThenBy(b => b.Outcome).Take(MaxBars).OrderBy(b => b.Outcome).ToList();
        return bars;
    }

    // The last column that measures the qubits the answer is read from.
    private static int ReadColumn(QuantumCircuitSpec spec) {
        for (int c = spec.Columns.Count - 1; c >= 0; c--)
            if (spec.Columns[c].Measures && spec.Columns[c].Gates.All(g => g.Qubits.All(q => spec.Measure.Contains(q)))) return c;
        return -1;
    }

    /// <summary>
    /// The frame for one step. <paramref name="incremental"/> says whether the step comes from an incremental run
    /// (<see cref="SolveRun.Incremental"/>); <paramref name="runApplies"/> whether any step of the run applies a column.
    /// </summary>
    public static QuantumFrame Frame(QuantumCircuitSpec spec, QuantumSimulation simulation, QuantumProgress partial, StepEvent ev,
        string[] focus, string caption, bool? ok = null, bool incremental = false, bool runApplies = true) {
        int applied = Math.Clamp(partial.Applied, 0, spec.Columns.Count);
        var columnIds = Enumerable.Range(0, spec.Columns.Count).Select(QuantumCircuits.ColumnId).ToHashSet(StringComparer.Ordinal);
        var gateIds = spec.Columns.SelectMany((c, i) => c.Gates.Select(g => QuantumCircuits.GateId(i, g))).ToHashSet(StringComparer.Ordinal);
        var qubitIds = Enumerable.Range(0, spec.Wires.Count).Select(QuantumCircuits.QubitId).ToHashSet(StringComparer.Ordinal);

        var columns = new Dictionary<string, PictureState>(StringComparer.Ordinal);
        var gates = new Dictionary<string, PictureState>(StringComparer.Ordinal);
        var qubits = new Dictionary<string, PictureState>(StringComparer.Ordinal);

        // The measured outcome: the answer in an Accept and a correct Done, wrong in a Reject and in a Done that is not.
        PictureState? read = ev == StepEvent.Done ? (ok == true ? PictureState.Solution : PictureState.Rejected)
            : ev == StepEvent.Accept ? PictureState.Solution : ev == StepEvent.Reject ? PictureState.Rejected : null;
        int readColumn = ReadColumn(spec);
        if (partial.Outcome != null && read is { } measured && readColumn >= 0) {
            foreach (int q in spec.Measure) GraphSubsetFrames.Raise(qubits, QuantumCircuits.QubitId(q), measured);
            foreach (var g in spec.Columns[readColumn].Gates) GraphSubsetFrames.Raise(gates, QuantumCircuits.GateId(readColumn, g), measured);
        }

        // The focus: the column being applied and its gates, or the oracle being queried.
        var mark = incremental ? GraphSubsetFrames.FocusMark(ev) : PictureState.ElementHighlight;
        foreach (var id in focus) {
            if (columnIds.Contains(id)) GraphSubsetFrames.Raise(columns, id, mark);
            else if (gateIds.Contains(id)) GraphSubsetFrames.Raise(gates, id, mark);
            else if (qubitIds.Contains(id)) GraphSubsetFrames.Raise(qubits, id, mark);
        }

        // A run that never ran the circuit (a classical solver): in the Done frame, every column is Untraveled.
        if (ev == StepEvent.Done && !runApplies)
            foreach (var id in columnIds) GraphSubsetFrames.Raise(columns, id, PictureState.Untraveled);

        var distribution = applied > 0 ? simulation.After(applied) : null;
        return new QuantumFrame(ev, caption, focus, applied, partial.Queries,
            GraphSubsetFrames.ToRefs(columns), GraphSubsetFrames.ToRefs(gates), GraphSubsetFrames.ToRefs(qubits)) {
            Bars = distribution == null ? null : Bars(distribution, spec.Measure.Length),
            Outcome = partial.Outcome,
            Counts = partial.Counts.Count > 0 ? partial.Counts : null,
            Result = partial.Result,
            Ok = ok,
        };
    }

    /// <summary>The blank Done frame, ok = false, that closes a run stopped by its time limit: nothing applied, nothing
    /// queried, nothing read.</summary>
    public static QuantumFrame StoppedFrame(string caption) =>
        new(StepEvent.Done, caption, [], 0, [], new SortedDictionary<string, StateRef>(StringComparer.Ordinal),
            new SortedDictionary<string, StateRef>(StringComparer.Ordinal), new SortedDictionary<string, StateRef>(StringComparer.Ordinal)) { Ok = false };

    // ---- The whole response ----

    /// <summary>
    /// The whole response: the payload, a frame per kept step, then a solved frame built from the answer unless the last step
    /// already is the Done step. A run stopped by its time limit ends with <see cref="StoppedFrame"/> instead, and its answer
    /// string (<see cref="SolveRun.TimedOutAnswer"/>) is never read.
    /// </summary>
    public static FramesResponse Build(QuantumCircuitSpec spec, SolveRun run, Func<string, bool> verifyAnswer) {
        var simulation = new QuantumSimulation(spec);
        var steps = run.StepsFor(typeof(QuantumProgress)).OfType<SolverStep<QuantumProgress>>().ToList();
        bool runApplies = steps.Any(s => s.Partial.Applied > 0);
        var frames = new List<object>();
        foreach (var step in steps)
            frames.Add(run.TimedOut && step.Event == StepEvent.Done
                ? StoppedFrame(step.Caption)
                : Frame(spec, simulation, step.Partial, step.Event, step.Focus, step.Caption, step.Ok, run.Incremental, runApplies));
        if (run.TimedOut) {
            if (steps.LastOrDefault() is not { Event: StepEvent.Done }) frames.Add(StoppedFrame(GraphSubsetFrames.StoppedCaption));
        } else if (steps.LastOrDefault() is not { Event: StepEvent.Done }) {
            // The steps cannot be drawn: the answer, as the solver gave it, judged by the problem's verifier.
            bool ok = verifyAnswer(run.Answer);
            frames.Add(Frame(spec, simulation, new QuantumProgress(result: run.Answer), StepEvent.Done, [],
                ok ? $"The answer is {run.Answer}." : $"The answer {run.Answer} is not correct.", ok, runApplies: true));
        }
        return new FramesResponse(nameof(VisualizationType.QuantumCircuit), Payload(spec), frames);
    }
}

/// <summary>
/// A visualization for a problem drawn as the Quantum Circuit picture. The visualization declares only the instance's
/// circuit (<see cref="Describe"/>); the frames hook then comes for free. Implement members explicitly so nothing new
/// appears in /info. The class must also declare <c>Type? IVisualization.StepShape =&gt; typeof(QuantumProgress)</c>.
/// </summary>
interface IQuantumCircuitVisualization<TProblem> : IFramesVisualization where TProblem : IProblem {
    QuantumCircuitSpec Describe(TProblem problem);

    FramesResponse IFramesVisualization.BuildFrames(string instance, SolveRun run) {
        TProblem problem = ParseGuard.CreateProblem<TProblem>(instance);
        return QuantumCircuitFrames.Build(Describe(problem), run, answer => {
            // The problem's own verifier decides; a certificate it cannot read is not correct.
            try { return problem.defaultVerifier.verify(instance, answer); } catch { return false; }
        });
    }
}
