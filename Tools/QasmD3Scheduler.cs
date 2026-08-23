using System;
using System.Collections.Generic;
using System.Linq;

namespace API.Tools;

// Shared DTOs for the QASM -> D3 gate-scheduling pipeline used by the D3 quantum-circuit
// visualizations (Deutsch, Deutsch-Jozsa, Bernstein-Vazirani). These were previously
// triplicated as identical private nested classes in each visualization file.

internal sealed class QasmOp {
    public string Type { get; init; } = "";
    public string Id { get; init; } = "";
    public string[] Targets { get; init; } = Array.Empty<string>();
    public string[]? Classical { get; init; }
    public double[]? Params { get; init; }
}

internal sealed class QasmD3GateOp {
    public string id { get; set; } = "";
    public string type { get; set; } = "";          // "h", "x", "cx", "m", "oracle", ...
    public string[] targets { get; set; } = Array.Empty<string>();
    public string[]? classical { get; set; }        // only for measurement
    public double[]? @params { get; set; }          // for rz/ry/rx/u3 etc. later
    public string? label { get; set; }              // used by oracle/block
    public double time { get; set; }                // assigned by scheduler (offset for measurements)
}

internal sealed class QasmD3Payload {
    public string[] qubits { get; set; } = Array.Empty<string>();
    public string[] classical { get; set; } = Array.Empty<string>();
    public List<QasmD3GateOp> gates { get; set; } = new();
    public List<QasmD3Overlay> overlays { get; set; } = new();
    public Dictionary<string, object?> metadata { get; set; } = new();
}

internal sealed class QasmD3Overlay {
    public string id { get; set; } = "";
    public string type { get; set; } = "stage";     // e.g. "oracle", "stage"
    public string label { get; set; } = "";
    public int timeStart { get; set; }
    public int timeEnd { get; set; }
    public string[] targets { get; set; } = Array.Empty<string>(); // optional
}

// Shared QASM-text parsing + ASAP gate scheduling for the D3 quantum-circuit visualizations.
// Extracted from what was byte-for-byte-identical private logic in DeutschD3Visualization,
// DeutschJozsaD3Visualization, and BernsteinVaziraniD3Visualization.
internal static class QasmD3Scheduler {
    internal static (List<string> Qubits, List<string> Classical, List<QasmOp> Ops) ParseQasm(string qasm) {
        var qubits = new List<string>();
        var classical = new List<string>();
        var ops = new List<QasmOp>();

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

                    ops.Add(new QasmOp {
                        Id = $"m{ops.Count}",
                        Type = "m",
                        Targets = new[] { q },
                        Classical = new[] { c }
                    });
                }
                continue;
            }

            // Generic "gate args;" lines
            if (line.Contains(' ')) {
                string noSemi = line.TrimEnd(';');
                string[] tokens = noSemi.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 2) continue;

                string gateToken = tokens[0].Trim();
                string argsPart = tokens[1].Trim();

                // Parse parameters if gateToken like "rz(0.5)" or "u3(a,b,c)"
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

                        // Best effort parse: numeric parameters (for your D3 label rendering)
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
                    ops.Add(new QasmOp {
                        Id = $"{gateType.ToLower()}{ops.Count}",
                        Type = gateType.ToLower(),
                        Targets = targets,
                        Params = gateParams
                    });
                }
            }
        }

        return (qubits, classical, ops);
    }

    internal static List<QasmD3GateOp> ScheduleAsap(List<QasmOp> ops) {
        var gates = new List<QasmD3GateOp>();

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

            // Emit typed gate (no anonymous objects)
            gates.Add(new QasmD3GateOp {
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

    private static void OffsetMeasurementTimes(List<QasmD3GateOp> gates) {
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

    internal static string NormalizeQubit(string qasmRef) {
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
}
