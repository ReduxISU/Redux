using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_BERNSTEINVAZIRANI;
using API.Tools;

class BernsteinVaziraniD3Visualization : IVisualization<BERNSTEINVAZIRANI>
{
    public string visualizationName { get; } = "Bernstein-Vazirani Quantum Circuit (D3)";
    public string visualizationDefinition { get; } =
        "Constructs a quantum circuit to recover the secret bit string with a single oracle query.";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Andreas Kramer, Courtney Bodily, Rakesh Itani" };
    public string visualizationType { get; } = "Quantum Circuit D3";

    public BernsteinVaziraniD3Visualization() { }

    private sealed class D3GateOp
    {
        public string id { get; set; } = "";
        public string type { get; set; } = "";
        public string[] targets { get; set; } = Array.Empty<string>();
        public string[]? classical { get; set; }
        public double[]? @params { get; set; }
        public string? label { get; set; }
        public double time { get; set; }
    }

    private sealed class D3Payload
    {
        public string[] qubits { get; set; } = Array.Empty<string>();
        public string[] classical { get; set; } = Array.Empty<string>();
        public List<D3GateOp> gates { get; set; } = new();
        public List<D3Overlay> overlays { get; set; } = new();
        public Dictionary<string, object?> metadata { get; set; } = new();
    }

    private sealed class D3Overlay
    {
        public string id { get; set; } = "";
        public string type { get; set; } = "stage";
        public string label { get; set; } = "";
        public int timeStart { get; set; }
        public int timeEnd { get; set; }
        public string[] targets { get; set; } = Array.Empty<string>();
    }

    public API_JSON visualize(BERNSTEINVAZIRANI instance)
    {
        return BuildVisualization(instance, solution: null);
    }

    public API_JSON SolvedVisualization(BERNSTEINVAZIRANI instance, string solution)
    {
        return BuildVisualization(instance, solution);
    }

    private API_JSON BuildVisualization(BERNSTEINVAZIRANI instance, string? solution)
    {
        string circuitJson = BuildStaticD3Payload(instance, solution);
        string? answerFromApi = null;

        try
        {
            bool[] requestBody = instance.funcValues.ToArray();
            var client = new QuantumServerAPI(QuantumServerAPI.ServerEnvironment.ISU_AWS);
            string response = client.PostAsync("/bernstein-vazirani-quantum", requestBody).Result;

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
            // fall back to static payload
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
                ["secretString"] = finalSolution,
                ["oracleType"] = "linear"
            }
        };
    }

    private sealed class Op
    {
        public string Type { get; init; } = "";
        public string Id { get; init; } = "";
        public string[] Targets { get; init; } = Array.Empty<string>();
        public string[]? Classical { get; init; }
        public double[]? Params { get; init; }
    }

    private string BuildD3FromQasm(string qasm, string? answer, BERNSTEINVAZIRANI instance, string? solution)
    {
        var qubits = new List<string>();
        var classical = new List<string>();
        var ops = new List<Op>();

        foreach (string rawLine in qasm.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal))
                continue;
            if (line.StartsWith("OPENQASM", StringComparison.OrdinalIgnoreCase))
                continue;
            if (line.StartsWith("include", StringComparison.OrdinalIgnoreCase))
                continue;

            if (line.StartsWith("qreg ", StringComparison.Ordinal))
            {
                string nameAndSize = line.Replace("qreg", "", StringComparison.Ordinal).Replace(";", "").Trim();
                string[] parts = nameAndSize.Split('[', ']');
                if (parts.Length >= 2 && int.TryParse(parts[1], out int size))
                {
                    for (int i = 0; i < size; i++)
                        qubits.Add($"{parts[0]}{i}");
                }
                continue;
            }

            if (line.StartsWith("creg ", StringComparison.Ordinal))
            {
                string nameAndSize = line.Replace("creg", "", StringComparison.Ordinal).Replace(";", "").Trim();
                string[] parts = nameAndSize.Split('[', ']');
                if (parts.Length >= 2 && int.TryParse(parts[1], out int size))
                {
                    for (int i = 0; i < size; i++)
                        classical.Add($"{parts[0]}{i}");
                }
                continue;
            }

            if (line.StartsWith("measure", StringComparison.Ordinal))
            {
                string noSemi = line.TrimEnd(';');
                string[] arrowSplit = noSemi.Split("->", StringSplitOptions.RemoveEmptyEntries);
                if (arrowSplit.Length == 2)
                {
                    string q = NormalizeQubit(arrowSplit[0].Replace("measure", "", StringComparison.Ordinal).Trim());
                    string c = NormalizeQubit(arrowSplit[1].Trim());

                    ops.Add(new Op
                    {
                        Id = $"m{ops.Count}",
                        Type = "m",
                        Targets = new[] { q },
                        Classical = new[] { c }
                    });
                }
                continue;
            }

            if (line.Contains(' '))
            {
                string noSemi = line.TrimEnd(';');
                string[] tokens = noSemi.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 2) continue;

                string gateToken = tokens[0].Trim();
                string argsPart = tokens[1].Trim();

                string gateType = gateToken;
                double[]? gateParams = null;

                int parenStart = gateToken.IndexOf('(');
                if (parenStart >= 0)
                {
                    int parenEnd = gateToken.LastIndexOf(')');
                    if (parenEnd > parenStart)
                    {
                        gateType = gateToken.Substring(0, parenStart).Trim();

                        string inside = gateToken.Substring(parenStart + 1, parenEnd - parenStart - 1);
                        var parts = inside.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                          .Select(s => s.Trim())
                                          .ToArray();

                        var parsed = new List<double>();
                        foreach (var p in parts)
                        {
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

                if (targets.Length > 0)
                {
                    ops.Add(new Op
                    {
                        Id = $"{gateType.ToLower()}{ops.Count}",
                        Type = gateType.ToLower(),
                        Targets = targets,
                        Params = gateParams
                    });
                }
            }
        }

        List<D3GateOp> gates = ScheduleOpsAsap(ops);

        var payload = new D3Payload
        {
            qubits = qubits.Count > 0 ? qubits.ToArray() : BuildDefaultQubits(instance),
            classical = classical.Count > 0 ? classical.ToArray() : BuildDefaultClassical(instance),
            gates = gates,
            metadata = new Dictionary<string, object?>
            {
                ["solution"] = solution ?? answer,
                ["oracleType"] = "linear"
            }
        };

        var uf = DetectOracleStage(payload);
        if (uf != null)
            payload.overlays.Add(uf);

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    private static D3Overlay? DetectOracleStage(D3Payload payload)
    {
        // Heuristic: first full layer of H on data+ancilla, then next full layer of H on data only
        var qubits = payload.qubits ?? Array.Empty<string>();
        if (qubits.Length < 2) return null;

        string ancilla = qubits.Last();
        var dataQubits = qubits.Take(qubits.Length - 1).ToArray();

        bool IsHOn(D3GateOp g, string q) =>
            string.Equals(g.type, "h", StringComparison.OrdinalIgnoreCase) &&
            g.targets != null &&
            g.targets.Length == 1 &&
            string.Equals(g.targets[0], q, StringComparison.Ordinal);

        var timesWithAllH = payload.gates
            .Where(g => IsHOn(g, g.targets.FirstOrDefault() ?? string.Empty))
            .GroupBy(g => g.time)
            .OrderBy(g => g.Key)
            .Select(grp => new
            {
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

        return new D3Overlay
        {
            id = "uf",
            type = "oracle",
            label = "U_f",
            timeStart = (int)Math.Round(tStart),
            timeEnd = (int)Math.Round(tEnd),
            targets = qubits.ToArray()
        };
    }

    private static List<D3GateOp> ScheduleOpsAsap(List<Op> ops)
    {
        var gates = new List<D3GateOp>();

        double currentTime = 0;
        var layerUsed = new HashSet<string>(StringComparer.Ordinal);
        string? layerType = null;

        void NextLayer()
        {
            currentTime++;
            layerUsed.Clear();
            layerType = null;
        }

        foreach (var op in ops)
        {
            var resources = new HashSet<string>(op.Targets, StringComparer.Ordinal);
            if (op.Type == "m" && op.Classical != null)
            {
                foreach (var c in op.Classical) resources.Add(c);
            }

            bool conflicts = resources.Overlaps(layerUsed);
            bool typeMismatch = layerType != null &&
                                !string.Equals(layerType, op.Type, StringComparison.OrdinalIgnoreCase);

            if (conflicts || typeMismatch)
                NextLayer();

            layerType ??= op.Type;

            foreach (var r in resources) layerUsed.Add(r);

            gates.Add(new D3GateOp
            {
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

    private static void OffsetMeasurementTimes(List<D3GateOp> gates)
    {
        const double eps = 0.01;
        var groups = gates.GroupBy(g => g.time);
        foreach (var grp in groups)
        {
            double slot = 0;
            foreach (var g in grp.Where(x => string.Equals(x.type, "m", StringComparison.OrdinalIgnoreCase)))
            {
                g.time = grp.Key + slot * eps;
                slot += 1;
            }
        }
    }

    private static string NormalizeQubit(string qasmRef)
    {
        string trimmed = qasmRef.Trim();
        int bracket = trimmed.IndexOf('[');
        if (bracket >= 0)
        {
            int end = trimmed.IndexOf(']', bracket + 1);
            if (end > bracket)
            {
                string name = trimmed.Substring(0, bracket);
                string idx = trimmed.Substring(bracket + 1, end - bracket - 1);
                return $"{name}{idx}";
            }
        }
        return trimmed.TrimEnd(';');
    }

    private string BuildStaticD3Payload(BERNSTEINVAZIRANI instance, string? solution)
    {
        string[] qubits = BuildDefaultQubits(instance);
        string[] classical = BuildDefaultClassical(instance);

        var gates = new List<object>();

        string ancilla = qubits.Last();

        // Prepare ancilla in |1>
        gates.Add(new { id = "x0", type = "x", targets = new[] { ancilla }, time = 0 });

        // Hadamard on all qubits
        for (int i = 0; i < qubits.Length; i++)
        {
            gates.Add(new { id = $"h{i}", type = "h", targets = new[] { qubits[i] }, time = 1 });
        }

        // Oracle column placeholder
        int oracleTime = 2;
        gates.Add(new { id = "oracle", type = "oracle", targets = qubits.ToArray(), label = "U_f", time = oracleTime });

        // Hadamard on data qubits
        int postHTime = 3;
        for (int i = 0; i < qubits.Length - 1; i++)
        {
            gates.Add(new { id = $"h_post_{i}", type = "h", targets = new[] { qubits[i] }, time = postHTime });
        }

        // Measurements on data qubits
        int measureTime = 4;
        for (int i = 0; i < qubits.Length - 1; i++)
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
            new D3Overlay
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
                oracleType = "linear"
            }
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string[] BuildDefaultQubits(BERNSTEINVAZIRANI instance)
    {
        int dataCount = Math.Max(1, instance.NBits);
        var qubits = new List<string>();
        for (int i = 0; i < dataCount; i++)
            qubits.Add($"q{i}");
        qubits.Add($"q{dataCount}"); // ancilla
        return qubits.ToArray();
    }

    private static string[] BuildDefaultClassical(BERNSTEINVAZIRANI instance)
    {
        int dataCount = Math.Max(1, instance.NBits);
        var classical = new List<string>();
        for (int i = 0; i < dataCount; i++)
            classical.Add($"c{i}");
        return classical.ToArray();
    }
}
