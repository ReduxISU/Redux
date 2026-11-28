using System.Globalization;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_MAXCUT.Solvers;
using API.Tools;

namespace API.Problems.NPComplete.NPC_MAXCUT.Visualizations;

/// <summary>
/// Draws the QAOA circuit the quantum service optimized for a Max Cut instance: one qubit per node, a Hadamard
/// layer, then for each of the service's layers the cost unitary U_C(γ) (a cx·rz·cx per edge, the rz angle scaled
/// by the edge's weight) and the mixer U_B(β) (an rx on every qubit), then measurement. The solved picture adds the
/// measured cut.
/// </summary>
class MaxCutQAOAVisualization : IVisualization<MAXCUT, API_QUANTUMCIRCUIT> {
    public string visualizationName { get; } = "Max Cut QAOA Quantum Circuit (D3)";
    public string visualizationDefinition { get; } =
        "Shows the QAOA circuit the quantum service optimized for the graph, one qubit per node: a Hadamard layer, " +
        "then for each layer the cost unitary U_C(γ), a CNOT-RZ-CNOT on every edge with the rotation scaled by the " +
        "edge's weight, and the mixer U_B(β), an RX on every qubit, then measurement. The solved view adds the " +
        "measured cut.";
    public string source { get; } = "https://arxiv.org/abs/1411.4028";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Jason L. Wright" };
    public VisualizationType visualizationType { get; } = VisualizationType.QuantumCircuitD3;
    public ISolver solver { get; } = new MaxCutQAOASolver();

    public MaxCutQAOAVisualization() { }

    // The circuit's angles are what the service optimizes, so even the plain picture asks the service for them.
    // With the service's fixed default seed, this call returns the same result the solver got.
    public API_QUANTUMCIRCUIT visualize(MAXCUT problem) => Build(problem, solution: null);

    public API_JSON SolvedVisualization(MAXCUT problem, string solution) => Build(problem, solution);

    private API_QUANTUMCIRCUIT Build(MAXCUT problem, string? solution) {
        // Any failure of the quantum service, or a reply with neither an answer nor a circuit, throws
        // SolverUnavailableException (HTTP 503 solver_unavailable) rather than drawing an empty circuit (#625).
        string response = QuantumServerAPI.Ask(solver, problem.problemName, client => {
            string reply = client.PostAsync("/maxcut-quantum", new MaxCutQAOASolver.JSON_MaxCut_Problem(problem)).Result;
            using JsonDocument doc = JsonDocument.Parse(reply);
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("answer", out _))
                throw QuantumServerAPI.MissingAnswer("answer", reply);
            return reply;
        });

        using JsonDocument reply = JsonDocument.Parse(response);
        JsonElement root = reply.RootElement;

        var payload = root.TryGetProperty("qasm", out JsonElement qasm) && qasm.ValueKind == JsonValueKind.String
            ? FromQasm(qasm.GetString()!, problem.nodes, Angles(root, "gammas"), Angles(root, "betas"))
            // With no weighted edges every cut weighs 0, so the service runs no circuit.
            : new QasmD3Payload {
                qubits = problem.nodes.ToArray(),
                metadata = { ["Note"] = "The graph has no weighted edges, so every cut weighs 0 and no circuit is run." },
            };

        if (solution != null) {
            payload.metadata["solution"] = solution;
            if (root.TryGetProperty("answer_bitstring", out JsonElement bits))
                payload.metadata["solutionBits"] = bits.GetString();
        }
        if (root.TryGetProperty("cut_weight", out JsonElement weight))
            payload.metadata["Cut weight"] = weight.GetRawText();
        if (root.TryGetProperty("seed", out JsonElement seed))
            payload.metadata["Seed"] = seed.GetRawText();

        using JsonDocument d3 = JsonDocument.Parse(JsonSerializer.Serialize(payload));
        return new API_QUANTUMCIRCUIT {
            solution = solution,
            format = QuantumCircuitFormat.D3,
            d3 = d3.RootElement.Clone(),
        };
    }

    private static double[] Angles(JsonElement root, string field) =>
        root.TryGetProperty(field, out JsonElement angles) && angles.ValueKind == JsonValueKind.Array
            ? angles.EnumerateArray().Select(a => a.GetDouble()).ToArray()
            : Array.Empty<double>();

    // Builds the D3 payload from the service's QASM: wires named after the graph's nodes (qubit i is node i, as the
    // solver sends them), rotation angles rounded for display, and an overlay over each cost and mixer layer.
    internal static QasmD3Payload FromQasm(string qasm, List<string> nodes, double[] gammas, double[] betas) {
        var (qubits, classical, ops) = QasmD3Scheduler.ParseQasm(qasm);
        // measure_all puts a barrier before the measurements; the renderer would draw it as a gate.
        ops = ops.Where(op => op.Type != "barrier").ToList();
        List<QasmD3GateOp> gates = QasmD3Scheduler.ScheduleAsap(ops);

        var wireName = qubits.Select((q, i) => (q, name: i < nodes.Count ? nodes[i] : q)).ToDictionary(p => p.q, p => p.name);
        foreach (QasmD3GateOp gate in gates) {
            gate.targets = gate.targets.Select(t => wireName.GetValueOrDefault(t, t)).ToArray();
            if (gate.@params != null)
                gate.@params = gate.@params.Select(p => Math.Round(p, 2)).ToArray();
        }
        string[] wires = qubits.Select(q => wireName[q]).ToArray();

        var payload = new QasmD3Payload { qubits = wires, classical = classical.ToArray(), gates = gates };
        payload.overlays.AddRange(LayerOverlays(gates, wires));
        if (gammas.Length > 0)
            payload.metadata["γ (cost angles)"] = string.Join(", ", gammas.Select(Format));
        if (betas.Length > 0)
            payload.metadata["β (mixer angles)"] = string.Join(", ", betas.Select(Format));
        payload.metadata["Layers (p)"] = gammas.Length;
        return payload;
    }

    // The circuit is h on every qubit, then per layer a run of cx/rz gates (the cost unitary) followed by a run of
    // rx gates (the mixer), then measurements. Each run becomes one labelled overlay spanning its columns.
    private static IEnumerable<QasmD3Overlay> LayerOverlays(List<QasmD3GateOp> gates, string[] wires) {
        static string Stage(string type) => type switch {
            "cx" or "rz" => "cost",
            "rx" => "mixer",
            _ => "other",
        };

        int costLayer = 0, mixerLayer = 0;
        int i = 0;
        while (i < gates.Count) {
            string stage = Stage(gates[i].type);
            int start = i;
            while (i < gates.Count && Stage(gates[i].type) == stage)
                i++;
            if (stage == "other")
                continue;

            int k = stage == "cost" ? ++costLayer : ++mixerLayer;
            yield return new QasmD3Overlay {
                id = $"{stage}{k}",
                type = "stage",
                label = stage == "cost" ? $"U_C(γ{k})" : $"U_B(β{k})",
                timeStart = (int)gates[start].time,
                timeEnd = (int)gates[i - 1].time,
                targets = wires,
            };
        }
    }

    private static string Format(double angle) => angle.ToString("0.###", CultureInfo.InvariantCulture);
}
