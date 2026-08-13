using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_DEUTSCHJOZSA;
using API.Problems.NPComplete.NPC_DEUTSCHJOZSA.Solvers;
using API.Tools;

class DeutschJozsaD3Visualization : IVisualization<DEUTSCHJOZSA> {
    public string visualizationName { get; } = "Deutsch-Jozsa Quantum Circuit (D3)";
    public string visualizationDefinition { get; } =
        "Builds an n-qubit Deutsch-Jozsa circuit with Hadamard prep, highlights the oracle, and shows how one query distinguishes constant vs. balanced functions via interference using D3.js.";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Andreas Kramer", "Courtney Bodily", "Rakesh Itani" };
    public VisualizationType visualizationType { get; } = VisualizationType.QuantumCircuitD3;
    public ISolver solver { get; } = new DeutschJozsaClassicalSolver();
    public DeutschJozsaD3Visualization() { }

    private sealed class D3GateOp {
        public string id { get; set; } = "";
        public string type { get; set; } = "";
        public string[] targets { get; set; } = Array.Empty<string>();
        public string[]? classical { get; set; }
        public double[]? @params { get; set; }
        public string? label { get; set; }
        public double time { get; set; }
    }

    private sealed class D3Payload {
        public string[] qubits { get; set; } = Array.Empty<string>();
        public string[] classical { get; set; } = Array.Empty<string>();
        public List<D3GateOp> gates { get; set; } = new();
        public List<D3Overlay> overlays { get; set; } = new();
        public Dictionary<string, object?> metadata { get; set; } = new();
    }

    private sealed class D3Overlay {
        public string id { get; set; } = "";
        public string type { get; set; } = "stage";
        public string label { get; set; } = "";
        public int timeStart { get; set; }
        public int timeEnd { get; set; }
        public string[] targets { get; set; } = Array.Empty<string>();
    }

    public API_JSON visualize(DEUTSCHJOZSA instance) {
        return BuildVisualization(instance, solution: null);
    }

    public API_JSON SolvedVisualization(DEUTSCHJOZSA instance, string solution) {
        return BuildVisualization(instance, solution);
    }

    private API_JSON BuildVisualization(DEUTSCHJOZSA instance, string? solution) {
        string circuitJson = BuildStaticD3Payload(instance, solution);
        string? answerFromApi = null;

        try {
            bool[] requestBody = instance.w.Select(v => v != 0).ToArray();
            var client = new QuantumServerAPI();
            string response = client.PostAsync("/deutsch-jozsa-quantum", requestBody).Result;

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
            // fall back to the static payload
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
                ["oracleType"] = answerFromApi ?? (IsConstant(instance) ? "constant" : "balanced")
            }
        };
    }

    private sealed class Op {
        public string Type { get; init; } = "";
        public string Id { get; init; } = "";
        public string[] Targets { get; init; } = Array.Empty<string>();
        public string[]? Classical { get; init; }
        public double[]? Params { get; init; }
    }

    private string BuildD3FromQasm(string qasm, string? answer, DEUTSCHJOZSA instance, string? solution) {
        var qubits = new List<string>();
        var classical = new List<string>();
        var ops = new List<Op>();

        foreach (string rawLine in qasm.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal))
                continue;
            if (line.StartsWith("OPENQASM", StringComparison.OrdinalIgnoreCase))
                continue;
            if (line.StartsWith("include", StringComparison.OrdinalIgnoreCase))
                continue;

            if (line.StartsWith("qreg ", StringComparison.Ordinal)) {
                string nameAndSize = line.Replace("qreg", "", StringComparison.Ordinal).Replace(";", "").Trim();
                string[] parts = nameAndSize.Split('[', ']');
                if (parts.Length >= 2 && int.TryParse(parts[1], out int size)) {
                    for (int i = 0; i < size; i++)
                        qubits.Add($"{parts[0]}{i}");
                }
                continue;
            }

            if (line.StartsWith("creg ", StringComparison.Ordinal)) {
                string nameAndSize = line.Replace("creg", "", StringComparison.Ordinal).Replace(";", "").Trim();
                string[] parts = nameAndSize.Split('[', ']');
                if (parts.Length >= 2 && int.TryParse(parts[1], out int size)) {
                    for (int i = 0; i < size; i++)
                        classical.Add($"{parts[0]}{i}");
                }
                continue;
            }

            if (line.StartsWith("measure", StringComparison.Ordinal)) {
                string noSemi = line.TrimEnd(';');
                string[] arrowSplit = noSemi.Split("->", StringSplitOptions.RemoveEmptyEntries);
                if (arrowSplit.Length == 2) {
                    string q = NormalizeQubit(arrowSplit[0].Replace("measure", "", StringComparison.Ordinal).Trim());
                    string c = NormalizeQubit(arrowSplit[1].Trim());

                    ops.Add(new Op {
                        Id = $"m{ops.Count}",
                        Type = "m",
                        Targets = new[] { q },
                        Classical = new[] { c }
                    });
                }
                continue;
            }

            if (line.Contains(' ')) {
                string noSemi = line.TrimEnd(';');
                string[] tokens = noSemi.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 2) continue;

                string gateToken = tokens[0].Trim();
                string argsPart = tokens[1].Trim();

                string gateType = gateToken;
                double[]? gateParams = null;

                int parenStart = gateToken.IndexOf('(');
                if (parenStart >= 0) {
                    int parenEnd = gateToken.LastIndexOf(')');
                    if (parenEnd > parenStart) {
                        gateType = gateToken.Substring(0, parenStart).Trim();

                        string inside = gateToken.Substring(parenStart + 1, parenEnd - parenStart - 1);
                        var parts = inside.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                          .Select(s => s.Trim())
                                          .ToArray();

                        var parsed = new List<double>();
                        foreach (var p in parts) {
                            if (double.TryParse(p, out double val))
                                parsed.Add(val);
                        }
                        if (parsed.Count > 0)
                            gateParams = parsed.ToArray();
                    }
                }

                string[] targets = argsPart.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                           .Select(a => NormalizeQubit(a.Trim()))
                                           .ToArray();

                if (targets.Length > 0) {
                    ops.Add(new Op {
                        Id = $"{gateType.ToLower()}{ops.Count}",
                        Type = gateType.ToLower(),
                        Targets = targets,
                        Params = gateParams
                    });
                }
            }
        }

        List<D3GateOp> gates = ScheduleOpsAsap(ops);

        var payload = new D3Payload {
            qubits = qubits.Count > 0 ? qubits.ToArray() : BuildDefaultQubits(instance),
            classical = classical.Count > 0 ? classical.ToArray() : BuildDefaultClassical(instance),
            gates = gates,
            metadata = new Dictionary<string, object?> {
                ["solution"] = solution,
                ["oracleType"] = answer ?? (IsConstant(instance) ? "constant" : "balanced")
            }
        };

        var uf = DetectDeutschJozsaOracleStage(payload);
        if (uf != null)
            payload.overlays.Add(uf);

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    private static D3Overlay? DetectDeutschJozsaOracleStage(D3Payload payload) {
        var qubits = payload.qubits ?? Array.Empty<string>();
        if (qubits.Length < 2) return null;

        // Assume last qubit is ancilla; others are data qubits
        var dataQubits = qubits.Take(qubits.Length - 1).ToArray();
        if (dataQubits.Length == 0) return null;

        bool IsHOn(D3GateOp g, string q) =>
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

        if (tEnd < tStart) {
            double candidate = tPrep.Value + 1;
            bool exists = payload.gates.Any(g => Math.Abs(g.time - candidate) < 1e-9);
            if (!exists) return null;
            tStart = candidate;
            tEnd = candidate;
        }

        bool hasOracleOps = payload.gates.Any(g => g.time >= tStart && g.time <= tEnd);
        if (!hasOracleOps) return null;

        return new D3Overlay {
            id = "uf",
            type = "oracle",
            label = "U_f",
            timeStart = (int)Math.Round(tStart),
            timeEnd = (int)Math.Round(tEnd),
            targets = qubits.ToArray()
        };
    }

    private static List<D3GateOp> ScheduleOpsAsap(List<Op> ops) {
        var gates = new List<D3GateOp>();

        double currentTime = 0;
        var layerUsed = new HashSet<string>(StringComparer.Ordinal);
        string? layerType = null;

        void NextLayer() {
            currentTime++;
            layerUsed.Clear();
            layerType = null;
        }

        foreach (var op in ops) {
            var resources = new HashSet<string>(op.Targets, StringComparer.Ordinal);
            if (op.Type == "m" && op.Classical != null) {
                foreach (var c in op.Classical) resources.Add(c);
            }

            bool conflicts = resources.Overlaps(layerUsed);
            bool typeMismatch = layerType != null &&
                                !string.Equals(layerType, op.Type, StringComparison.OrdinalIgnoreCase);

            if (conflicts || typeMismatch)
                NextLayer();

            layerType ??= op.Type;

            foreach (var r in resources) layerUsed.Add(r);

            gates.Add(new D3GateOp {
                id = op.Id,
                type = op.Type,
                targets = op.Targets,
                classical = (op.Type == "m") ? (op.Classical ?? Array.Empty<string>()) : null,
                @params = (op.Params != null && op.Params.Length > 0) ? op.Params : null,
                time = currentTime
            });
        }

        OffsetMeasurementTimes(gates);

        return gates;
    }

    private static void OffsetMeasurementTimes(List<D3GateOp> gates) {
        const double eps = 0.01;
        var groups = gates.GroupBy(g => g.time);
        foreach (var grp in groups) {
            double slot = 0;
            foreach (var g in grp.Where(x => string.Equals(x.type, "m", StringComparison.OrdinalIgnoreCase))) {
                g.time = grp.Key + slot * eps;
                slot += 1;
            }
        }
    }

    private static string NormalizeQubit(string qasmRef) {
        string trimmed = qasmRef.Trim();
        int bracket = trimmed.IndexOf('[');
        if (bracket >= 0) {
            int end = trimmed.IndexOf(']', bracket + 1);
            if (end > bracket) {
                string name = trimmed.Substring(0, bracket);
                string idx = trimmed.Substring(bracket + 1, end - bracket - 1);
                return $"{name}{idx}";
            }
        }
        return trimmed.TrimEnd(';');
    }

    private string BuildStaticD3Payload(DEUTSCHJOZSA instance, string? solution) {
        int dataCount = Math.Max(1, instance.n);
        string[] qubits = BuildDefaultQubits(instance);
        string[] classical = BuildDefaultClassical(instance);

        var gates = new List<object>();
        // Ancilla assumed to be last qubit
        string ancilla = qubits.Last();

        // X on ancilla
        gates.Add(new { id = "x0", type = "x", targets = new[] { ancilla }, time = 0 });

        // H on all qubits (data + ancilla)
        for (int i = 0; i < qubits.Length; i++) {
            gates.Add(new { id = $"h{i}", type = "h", targets = new[] { qubits[i] }, time = 1 });
        }

        // Placeholder oracle column
        int oracleTime = 2;
        if (IsConstant(instance)) {
            gates.Add(new { id = "oracle_const", type = "i", targets = new[] { ancilla }, label = "U_f", time = oracleTime });
        } else {
            gates.Add(new { id = "oracle_bal", type = "cx", targets = new[] { qubits[0], ancilla }, label = "U_f", time = oracleTime });
        }

        // H on data qubits
        int postHTime = 3;
        for (int i = 0; i < dataCount; i++) {
            gates.Add(new { id = $"h_post_{i}", type = "h", targets = new[] { qubits[i] }, time = postHTime });
        }

        // Measure data qubits
        int measureTime = 4;
        for (int i = 0; i < dataCount; i++) {
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
            new D3Overlay
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
                oracleType = IsConstant(instance) ? "constant" : "balanced"
            }
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    private static bool IsConstant(DEUTSCHJOZSA instance) {
        if (instance.w.Count == 0) return true;
        int first = instance.w[0];
        return instance.w.All(v => v == first);
    }

    private static string[] BuildDefaultQubits(DEUTSCHJOZSA instance) {
        int dataCount = Math.Max(1, instance.n);
        var qubits = new List<string>();
        for (int i = 0; i < dataCount; i++)
            qubits.Add($"q{i}");
        qubits.Add($"q{dataCount}"); // ancilla
        return qubits.ToArray();
    }

    private static string[] BuildDefaultClassical(DEUTSCHJOZSA instance) {
        int dataCount = Math.Max(1, instance.n);
        var classical = new List<string>();
        for (int i = 0; i < dataCount; i++)
            classical.Add($"c{i}");
        return classical.ToArray();
    }
}
