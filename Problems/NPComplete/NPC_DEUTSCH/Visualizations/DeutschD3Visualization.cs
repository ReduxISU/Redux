using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_DEUTSCH;
using API.Problems.NPComplete.NPC_DEUTSCH.Solvers;
using API.Tools;

class DeutschD3Visualization : IVisualization<DEUTSCH>
{
    public string visualizationName { get; } = "Deutsch Quantum Circuit (D3)";
    public string visualizationDefinition { get; } = "Builds a two-qubit Deutsch circuit, highlights the oracle block, and illustrates how interference distinguishes constant vs. balanced functions in one query using D3.js.";
    public string source { get; } = "https://d3js.org/";
    public string[] contributors { get; } = { "Andreas Kramer", "Courtney Bodily", "Rakesh Itani" };
    public VisualizationType visualizationType { get; } = VisualizationType.QuantumCircuitD3;
    public ISolver solver { get; } = new DeutschClassicalSolver();

    public DeutschD3Visualization() { }

    public API_JSON visualize(DEUTSCH instance)
    {
        return BuildVisualization(instance, solution: null);
    }

    public API_JSON SolvedVisualization(DEUTSCH instance, string solution)
    {
        return BuildVisualization(instance, solution);
    }

    private API_JSON BuildVisualization(DEUTSCH instance, string? solution)
    {
        string circuitJson = BuildStaticD3Payload(instance, solution);
        string? answerFromApi = null;

        try
        {
            bool[] requestBody = instance.funcValues;
            var client = new QuantumServerAPI();
            string response = client.PostAsync("/deutsch-quantum", requestBody).Result;

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
            // fallback to static payload in circuitJson
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

            // Optional: carry metadata at top level too
            metadata = new Dictionary<string, object?>
            {
                ["oracleType"] = answerFromApi
                    ?? ((instance.funcValues[0] == instance.funcValues[1]) ? "constant" : "balanced")
            },
        };
    }

    // QASM -> ops -> ASAP schedule

    private string BuildD3FromQasm(string qasm, string? answer, DEUTSCH instance, string? solution)
    {
        var (qubits, classical, ops) = QasmD3Scheduler.ParseQasm(qasm);
        List<QasmD3GateOp> gates = QasmD3Scheduler.ScheduleAsap(ops);

        // Build payload
        var payload = new QasmD3Payload
        {
            qubits = qubits.Count > 0 ? qubits.ToArray() : new[] { "q0", "q1" },
            classical = classical.Count > 0 ? classical.ToArray() : new[] { "c0" },
            gates = gates,
            metadata = new Dictionary<string, object?>
            {
                ["solution"] = solution,
                ["oracleType"] = answer ?? ((instance.funcValues[0] == instance.funcValues[1]) ? "constant" : "balanced")
            }
        };

        var uf = DetectDeutschOracleStage(payload);
        if (uf != null)
            payload.overlays.Add(uf);

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    private static QasmD3Overlay? DetectDeutschOracleStage(QasmD3Payload payload)
    {
        // Expect q0,q1 exist, but don’t hard-fail if naming differs
        var qubits = payload.qubits ?? Array.Empty<string>();
        if (qubits.Length < 2) return null;

        string q0 = qubits[0];
        string q1 = qubits[1];

        // Helper predicates
        static bool IsHOn(QasmD3GateOp g, string q) =>
            string.Equals(g.type, "h", StringComparison.OrdinalIgnoreCase) &&
            g.targets != null && g.targets.Length == 1 &&
            string.Equals(g.targets[0], q, StringComparison.Ordinal);

        // 1) Find all times that have H on q0 and H on q1
        var timesWithHq0 = payload.gates.Where(g => IsHOn(g, q0)).Select(g => g.time).ToHashSet();
        var timesWithHq1 = payload.gates.Where(g => IsHOn(g, q1)).Select(g => g.time).ToHashSet();

        var prepTimes = timesWithHq0.Intersect(timesWithHq1).OrderBy(t => t).ToList();
        if (prepTimes.Count == 0) return null;

        // Choose the last "prep H layer" that happens before the final measurement
        // In Deutsch, it’s typically the only shared-H layer before the oracle.
        double tPrep = prepTimes.Last();

        // 2) Find the next H on q0 AFTER tPrep (post-oracle H)
        double? tPost = payload.gates
            .Where(g => IsHOn(g, q0) && g.time > tPrep)
            .Select(g => (double?)g.time)
            .OrderBy(t => t)
            .FirstOrDefault();

        if (tPost == null) return null;

        // 3) Oracle region is strictly between (tPrep, tPost)
        double tStart = tPrep + 1;
        double tEnd = tPost.Value - 1;

        if (tEnd < tStart)
        {
            // Sometimes the oracle collapses to exactly one column and your schedule might put post-H immediately next.
            // In that case, treat oracle as the single column tPrep+1 if it exists.
            double candidate = tPrep + 1;
            bool exists = payload.gates.Any(g => Math.Abs(g.time - candidate) < 1e-9);
            if (!exists) return null;

            tStart = candidate;
            tEnd = candidate;
        }

        // Only create overlay if there is at least one gate in the region
        bool hasOracleOps = payload.gates.Any(g => g.time >= tStart && g.time <= tEnd);
        if (!hasOracleOps) return null;

        return new QasmD3Overlay
        {
            id = "uf",
            type = "oracle",
            label = "U_f",
            timeStart = (int)Math.Round(tStart),
            timeEnd = (int)Math.Round(tEnd),
            targets = new[] { q0, q1 }
        };
    }

    private string BuildStaticD3Payload(DEUTSCH instance, string? solution)
    {
        bool[] f = instance.funcValues;
        bool isConstant = (f[0] == f[1]);

        var overlays = new[]
        {
            new QasmD3Overlay
            {
                type = "oracle",
                label = "U_f",
                timeStart = 2,
                timeEnd = 2,
                targets = new[] { "q0", "q1" }
            }
        };

        var payload = new
        {
            qubits = new[] { "q0", "q1" },
            classical = new[] { "c0" },
            gates = new object[]
            {
                new { id = "x0",  type = "x",  targets = new[] { "q1" },            time = 0 },

                // same-column H gates
                new { id = "h0",  type = "h",  targets = new[] { "q0" },            time = 1 },
                new { id = "h1",  type = "h",  targets = new[] { "q1" },            time = 1 },

                isConstant
                    ? new { id = "x1",  type = "x",  targets = new[] { "q1" },       time = 2 }
                    : new { id = "cx1", type = "cx", targets = new[] { "q0", "q1" }, time = 2 },

                new { id = "h2",  type = "h",  targets = new[] { "q0" },            time = 3 },

                new
                {
                    id = "m0",
                    type = "m",
                    targets = new[] { "q0" },
                    classical = new[] { "c0" },
                    time = 4
                }
            },
            overlays,
            metadata = new
            {
                solution,
                oracleType = isConstant ? "constant" : "balanced"
            }
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }
}
