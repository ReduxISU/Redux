using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_DEUTSCHJOZSA;
using API.Problems.NPComplete.NPC_DEUTSCHJOZSA.Solvers;
using API.Tools;

class DeutschJozsaD3Visualization : IVisualization<DEUTSCHJOZSA>
{
    public string visualizationName { get; } = "Deutsch-Jozsa Quantum Circuit (D3)";
    public string visualizationDefinition { get; } =
        "Builds an n-qubit Deutsch-Jozsa circuit with Hadamard prep, highlights the oracle, and shows how one query distinguishes constant vs. balanced functions via interference using D3.js.";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Andreas Kramer", "Courtney Bodily", "Rakesh Itani" };
    public VisualizationType visualizationType { get; } = VisualizationType.QuantumCircuitD3;
    public ISolver solver { get; } = new DeutschJozsaClassicalSolver();
    public DeutschJozsaD3Visualization() { }

    public API_JSON visualize(DEUTSCHJOZSA instance)
    {
        return BuildVisualization(instance, solution: null);
    }

    public API_JSON SolvedVisualization(DEUTSCHJOZSA instance, string solution)
    {
        return BuildVisualization(instance, solution);
    }

    private API_JSON BuildVisualization(DEUTSCHJOZSA instance, string? solution)
    {
        string circuitJson = BuildStaticD3Payload(instance, solution);
        string? answerFromApi = null;

        try
        {
            bool[] requestBody = instance.w.Select(v => v != 0).ToArray();
            var client = new QuantumServerAPI();
            string response = client.PostAsync("/deutsch-jozsa-quantum", requestBody).Result;

            using JsonDocument doc = JsonDocument.Parse(response);
            JsonElement root = doc.RootElement;

            if (root.TryGetProperty("answer", out JsonElement answerElement))
                answerFromApi = answerElement.GetString();

            if (root.TryGetProperty("qasm", out JsonElement qasmElement))
            {
                string? qasm = qasmElement.GetString();
                if (!string.IsNullOrWhiteSpace(qasm))
                    circuitJson = BuildD3FromQasm(qasm, answerFromApi, instance, solution);
            }
        }
        catch
        {
            // fall back to the static payload
        }

        JsonElement d3Element;
        using (var payloadDoc = JsonDocument.Parse(circuitJson))
            d3Element = payloadDoc.RootElement.Clone();

        string? finalSolution = solution ?? answerFromApi;

        return new API_QUANTUMCIRCUIT
        {
            solution = finalSolution,
            format = QuantumCircuitFormat.D3,
            d3 = d3Element,
            metadata = new Dictionary<string, object?>
            {
                ["oracleType"] = answerFromApi ?? (IsConstant(instance) ? "constant" : "balanced")
            }
        };
    }

    private string BuildD3FromQasm(string qasm, string? answer, DEUTSCHJOZSA instance, string? solution)
    {
        var (qubits, classical, ops) = QasmD3Scheduler.ParseQasm(qasm);
        List<QasmD3GateOp> gates = QasmD3Scheduler.ScheduleAsap(ops);

        var payload = new QasmD3Payload
        {
            qubits = qubits.Count > 0 ? qubits.ToArray() : BuildDefaultQubits(instance),
            classical = classical.Count > 0 ? classical.ToArray() : BuildDefaultClassical(instance),
            gates = gates,
            metadata = new Dictionary<string, object?>
            {
                ["solution"] = solution,
                ["oracleType"] = answer ?? (IsConstant(instance) ? "constant" : "balanced")
            }
        };

        var uf = DetectDeutschJozsaOracleStage(payload);
        if (uf != null)
            payload.overlays.Add(uf);

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    private static QasmD3Overlay? DetectDeutschJozsaOracleStage(QasmD3Payload payload)
    {
        var qubits = payload.qubits ?? Array.Empty<string>();
        if (qubits.Length < 2) return null;

        // Assume last qubit is ancilla; others are data qubits
        var dataQubits = qubits.Take(qubits.Length - 1).ToArray();
        if (dataQubits.Length == 0) return null;

        bool IsHOn(QasmD3GateOp g, string q) =>
            string.Equals(g.type, "h", StringComparison.OrdinalIgnoreCase) &&
            g.targets != null &&
            g.targets.Length == 1 &&
            string.Equals(g.targets[0], q, StringComparison.Ordinal);

        // Find earliest layer where all data qubits get H (ignore ancilla H)
        var hByTime = payload.gates
            .Where(g => g.targets != null &&
                        g.targets.Length == 1 &&
                        dataQubits.Contains(g.targets[0]) &&
                        IsHOn(g, g.targets[0]))
            .GroupBy(g => g.time)
            .OrderBy(g => g.Key);

        double? tPrep = hByTime
            .FirstOrDefault(grp => grp.Select(g => g.targets[0]).Distinct().Count() == dataQubits.Length)?
            .Key;

        if (tPrep == null) return null;

        // Next layer after tPrep where all data qubits get H again
        double? tPost = hByTime
            .FirstOrDefault(grp => grp.Key > tPrep &&
                                   grp.Select(g => g.targets[0]).Distinct().Count() == dataQubits.Length)?
            .Key;

        if (tPost == null) return null;

        double tStart = tPrep.Value + 1;
        double tEnd = tPost.Value - 1;

        if (tEnd < tStart)
        {
            double candidate = tPrep.Value + 1;
            bool exists = payload.gates.Any(g => Math.Abs(g.time - candidate) < 1e-9);
            if (!exists) return null;
            tStart = candidate;
            tEnd = candidate;
        }

        bool hasOracleOps = payload.gates.Any(g => g.time >= tStart && g.time <= tEnd);
        if (!hasOracleOps) return null;

        return new QasmD3Overlay
        {
            id = "uf",
            type = "oracle",
            label = "U_f",
            timeStart = (int)Math.Round(tStart),
            timeEnd = (int)Math.Round(tEnd),
            targets = qubits.ToArray()
        };
    }

    private string BuildStaticD3Payload(DEUTSCHJOZSA instance, string? solution)
    {
        int dataCount = Math.Max(1, instance.n);
        string[] qubits = BuildDefaultQubits(instance);
        string[] classical = BuildDefaultClassical(instance);

        var gates = new List<object>();
        // Ancilla assumed to be last qubit
        string ancilla = qubits.Last();

        // X on ancilla
        gates.Add(new { id = "x0", type = "x", targets = new[] { ancilla }, time = 0 });

        // H on all qubits (data + ancilla)
        for (int i = 0; i < qubits.Length; i++)
        {
            gates.Add(new { id = $"h{i}", type = "h", targets = new[] { qubits[i] }, time = 1 });
        }

        // Placeholder oracle column
        int oracleTime = 2;
        if (IsConstant(instance))
        {
            gates.Add(new { id = "oracle_const", type = "i", targets = new[] { ancilla }, label = "U_f", time = oracleTime });
        }
        else
        {
            gates.Add(new { id = "oracle_bal", type = "cx", targets = new[] { qubits[0], ancilla }, label = "U_f", time = oracleTime });
        }

        // H on data qubits
        int postHTime = 3;
        for (int i = 0; i < dataCount; i++)
        {
            gates.Add(new { id = $"h_post_{i}", type = "h", targets = new[] { qubits[i] }, time = postHTime });
        }

        // Measure data qubits
        int measureTime = 4;
        for (int i = 0; i < dataCount; i++)
        {
            gates.Add(new
            {
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

        var payload = new
        {
            qubits,
            classical,
            gates,
            overlays,
            metadata = new
            {
                solution,
                oracleType = IsConstant(instance) ? "constant" : "balanced"
            }
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    private static bool IsConstant(DEUTSCHJOZSA instance)
    {
        if (instance.w.Count == 0) return true;
        int first = instance.w[0];
        return instance.w.All(v => v == first);
    }

    private static string[] BuildDefaultQubits(DEUTSCHJOZSA instance)
    {
        int dataCount = Math.Max(1, instance.n);
        var qubits = new List<string>();
        for (int i = 0; i < dataCount; i++)
            qubits.Add($"q{i}");
        qubits.Add($"q{dataCount}"); // ancilla
        return qubits.ToArray();
    }

    private static string[] BuildDefaultClassical(DEUTSCHJOZSA instance)
    {
        int dataCount = Math.Max(1, instance.n);
        var classical = new List<string>();
        for (int i = 0; i < dataCount; i++)
            classical.Add($"c{i}");
        return classical.ToArray();
    }
}
