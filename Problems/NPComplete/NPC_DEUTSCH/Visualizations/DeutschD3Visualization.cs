using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_DEUTSCH;
using API.Tools;

class DeutschD3Visualization : IVisualization<DEUTSCH>
{
    public string visualizationName { get; } = "Deutsch Quantum Circuit (D3)";
    public string visualizationDefinition { get; } = "Builds a two-qubit Deutsch circuit, highlights the oracle block, and illustrates how interference distinguishes constant vs. balanced functions in one query using D3.js.";
    public string source { get; } = "https://d3js.org/";
    public string[] contributors { get; } = { "Andreas Kramer", "Courtney Bodily", "Rakesh Itani" };
    public string visualizationType { get; } = "Quantum Circuit D3";

    public DeutschD3Visualization() { }

    private sealed class D3GateOp
    {
        public string id { get; set; } = "";
        public string type { get; set; } = "";          // "h", "x", "cx", "m", "oracle", ...
        public string[] targets { get; set; } = Array.Empty<string>();
        public string[]? classical { get; set; }        // only for measurement
        public double[]? @params { get; set; }          // for rz/ry/rx/u3 etc. later
        public string? label { get; set; }              // used by oracle/block
        public double time { get; set; }                // assigned by scheduler (offset for measurements)
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
        public string type { get; set; } = "stage";     // e.g. "oracle", "stage"
        public string label { get; set; } = "";         
        public int timeStart { get; set; }
        public int timeEnd { get; set; }                
        public string[] targets { get; set; } = Array.Empty<string>(); // optional
    }


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
            var client = new QuantumServerAPI(QuantumServerAPI.ServerEnvironment.ISU_AWS);
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

    private sealed class Op
    {
        public string Type { get; init; } = "";
        public string Id { get; init; } = "";
        public string[] Targets { get; init; } = Array.Empty<string>();
        public string[]? Classical { get; init; }
        public double[]? Params { get; init; }
    }

    private string BuildD3FromQasm(string qasm, string? answer, DEUTSCH instance, string? solution)
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

            // Generic "gate args;" lines
            if (line.Contains(' '))
            {
                string noSemi = line.TrimEnd(';');
                string[] tokens = noSemi.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 2) continue;

                string gateToken = tokens[0].Trim();
                string argsPart = tokens[1].Trim();

                // Parse parameters if gateToken like "rz(0.5)" or "u3(a,b,c)"
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

                        // Best effort parse: numeric parameters (for your D3 label rendering)
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

        // Schedule for same-column gates
        List<D3GateOp> gates = ScheduleOpsAsap(ops);

        // Build payload
        var payload = new D3Payload
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

    private static D3Overlay? DetectDeutschOracleStage(D3Payload payload)
    {
        // Expect q0,q1 exist, but don’t hard-fail if naming differs
        var qubits = payload.qubits ?? Array.Empty<string>();
        if (qubits.Length < 2) return null;

        string q0 = qubits[0];
        string q1 = qubits[1];

        // Helper predicates
        static bool IsHOn(D3GateOp g, string q) =>
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

        return new D3Overlay
        {
            id = "uf",
            type = "oracle",
            label = "U_f",
            timeStart = (int)Math.Round(tStart),
            timeEnd = (int)Math.Round(tEnd),
            targets = new[] { q0, q1 }
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

            // Emit typed gate (no anonymous objects)
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

    private string BuildStaticD3Payload(DEUTSCH instance, string? solution)
    {
        bool[] f = instance.funcValues;
        bool isConstant = (f[0] == f[1]);

        var overlays = new[]
        {
            new D3Overlay
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
