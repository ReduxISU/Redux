using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_BERNSTEINVAZIRANI;
using API.Problems.NPComplete.NPC_BERNSTEINVAZIRANI.Solvers;
using API.Tools;

class BernsteinVaziraniD3Visualization : IVisualization<BERNSTEINVAZIRANI> {
    public string visualizationName { get; } = "Bernstein-Vazirani Quantum Circuit (D3)";
    public string visualizationDefinition { get; } =
        "Builds the Bernstein-Vazirani circuit with ancilla, highlights the oracle that encodes the secret string, and shows how a single query plus phase kickback reveals the hidden bits in D3.js.";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Andreas Kramer", "Courtney Bodily", "Rakesh Itani" };
    public VisualizationType visualizationType { get; } = VisualizationType.QuantumCircuitD3;
    public ISolver solver { get; } = new BernsteinVaziraniClassicalSolver();

    public BernsteinVaziraniD3Visualization() { }

    public API_JSON visualize(BERNSTEINVAZIRANI instance) {
        return BuildVisualization(instance, solution: null);
    }

    public API_JSON SolvedVisualization(BERNSTEINVAZIRANI instance, string solution) {
        return BuildVisualization(instance, solution);
    }

    private API_JSON BuildVisualization(BERNSTEINVAZIRANI instance, string? solution) {
        string circuitJson = BuildStaticD3Payload(instance, solution);
        string? answerFromApi = null;

        try {
            bool[] requestBody = instance.funcValues.ToArray();
            var client = new QuantumServerAPI();
            string response = client.PostAsync("/bernstein-vazirani-quantum", requestBody).Result;

            using JsonDocument doc = JsonDocument.Parse(response);
            JsonElement root = doc.RootElement;

            if (root.TryGetProperty("answer", out JsonElement answerElement))
                answerFromApi = answerElement.GetString();

            if (root.TryGetProperty("qasm", out JsonElement qasmElement)) {
                string? qasm = qasmElement.GetString();
                if (!string.IsNullOrWhiteSpace(qasm))
                    circuitJson = BuildD3FromQasm(qasm, answerFromApi, instance, solution);
            }
        } catch {
            // fall back to static payload
        }

        JsonElement d3Element;
        using (var payloadDoc = JsonDocument.Parse(circuitJson))
            d3Element = payloadDoc.RootElement.Clone();

        string? finalSolution = solution ?? answerFromApi;

        return new API_QUANTUMCIRCUIT {
            solution = finalSolution,
            format = QuantumCircuitFormat.D3,
            d3 = d3Element,
            metadata = new Dictionary<string, object?> {
                ["secretString"] = finalSolution,
                ["oracleType"] = "linear"
            }
        };
    }

    private string BuildD3FromQasm(string qasm, string? answer, BERNSTEINVAZIRANI instance, string? solution) {
        var (qubits, classical, ops) = QasmD3Scheduler.ParseQasm(qasm);
        List<QasmD3GateOp> gates = QasmD3Scheduler.ScheduleAsap(ops);

        var payload = new QasmD3Payload {
            qubits = qubits.Count > 0 ? qubits.ToArray() : BuildDefaultQubits(instance),
            classical = classical.Count > 0 ? classical.ToArray() : BuildDefaultClassical(instance),
            gates = gates,
            metadata = new Dictionary<string, object?> {
                ["solution"] = solution ?? answer,
                ["oracleType"] = "linear"
            }
        };

        var uf = DetectOracleStage(payload);
        if (uf != null)
            payload.overlays.Add(uf);

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    private static QasmD3Overlay? DetectOracleStage(QasmD3Payload payload) {
        // Heuristic: first full layer of H on data+ancilla, then next full layer of H on data only
        var qubits = payload.qubits ?? Array.Empty<string>();
        if (qubits.Length < 2) return null;

        string ancilla = qubits.Last();
        var dataQubits = qubits.Take(qubits.Length - 1).ToArray();

        bool IsHOn(QasmD3GateOp g, string q) =>
            string.Equals(g.type, "h", StringComparison.OrdinalIgnoreCase) &&
            g.targets != null &&
            g.targets.Length == 1 &&
            string.Equals(g.targets[0], q, StringComparison.Ordinal);

        var timesWithAllH = payload.gates
            .Where(g => IsHOn(g, g.targets.FirstOrDefault() ?? string.Empty))
            .GroupBy(g => g.time)
            .OrderBy(g => g.Key)
            .Select(grp => new {
                Time = grp.Key,
                Targets = grp.Select(g => g.targets[0]).ToHashSet()
            })
            .ToList();

        double? tPrep = timesWithAllH.FirstOrDefault(t => dataQubits.All(q => t.Targets.Contains(q)) && t.Targets.Contains(ancilla))?.Time;
        if (tPrep == null) return null;

        double? tPost = timesWithAllH.FirstOrDefault(t => t.Time > tPrep && dataQubits.All(q => t.Targets.Contains(q)) && !t.Targets.Contains(ancilla))?.Time;
        if (tPost == null) return null;

        double tStart = tPrep.Value + 1;
        double tEnd = tPost.Value - 1;

        if (tEnd < tStart) {
            double candidate = tPrep.Value + 1;
            bool exists = payload.gates.Any(g => Math.Abs(g.time - candidate) < 1e-9);
            if (!exists) return null;
            tStart = candidate;
            tEnd = candidate;
        }

        bool hasOracleOps = payload.gates.Any(g => g.time >= tStart && g.time <= tEnd);
        if (!hasOracleOps) return null;

        return new QasmD3Overlay {
            id = "uf",
            type = "oracle",
            label = "U_f",
            timeStart = (int)Math.Round(tStart),
            timeEnd = (int)Math.Round(tEnd),
            targets = qubits.ToArray()
        };
    }

    private string BuildStaticD3Payload(BERNSTEINVAZIRANI instance, string? solution) {
        string[] qubits = BuildDefaultQubits(instance);
        string[] classical = BuildDefaultClassical(instance);

        var gates = new List<object>();

        string ancilla = qubits.Last();

        // Prepare ancilla in |1>
        gates.Add(new { id = "x0", type = "x", targets = new[] { ancilla }, time = 0 });

        // Hadamard on all qubits
        for (int i = 0; i < qubits.Length; i++) {
            gates.Add(new { id = $"h{i}", type = "h", targets = new[] { qubits[i] }, time = 1 });
        }

        // Oracle column placeholder
        int oracleTime = 2;
        gates.Add(new { id = "oracle", type = "oracle", targets = qubits.ToArray(), label = "U_f", time = oracleTime });

        // Hadamard on data qubits
        int postHTime = 3;
        for (int i = 0; i < qubits.Length - 1; i++) {
            gates.Add(new { id = $"h_post_{i}", type = "h", targets = new[] { qubits[i] }, time = postHTime });
        }

        // Measurements on data qubits
        int measureTime = 4;
        for (int i = 0; i < qubits.Length - 1; i++) {
            gates.Add(new {
                id = $"m{i}",
                type = "m",
                targets = new[] { qubits[i] },
                classical = new[] { classical[i] },
                time = measureTime + i * 0.01
            });
        }

        var overlays = new[]
        {
            new QasmD3Overlay
            {
                type = "oracle",
                label = "U_f",
                timeStart = oracleTime,
                timeEnd = oracleTime,
                targets = qubits
            }
        };

        var payload = new {
            qubits,
            classical,
            gates,
            overlays,
            metadata = new {
                solution,
                oracleType = "linear"
            }
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string[] BuildDefaultQubits(BERNSTEINVAZIRANI instance) {
        int dataCount = Math.Max(1, instance.NBits);
        var qubits = new List<string>();
        for (int i = 0; i < dataCount; i++)
            qubits.Add($"q{i}");
        qubits.Add($"q{dataCount}"); // ancilla
        return qubits.ToArray();
    }

    private static string[] BuildDefaultClassical(BERNSTEINVAZIRANI instance) {
        int dataCount = Math.Max(1, instance.NBits);
        var classical = new List<string>();
        for (int i = 0; i < dataCount; i++)
            classical.Add($"c{i}");
        return classical.ToArray();
    }
}
