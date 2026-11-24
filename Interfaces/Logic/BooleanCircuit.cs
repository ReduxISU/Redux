namespace API.Interfaces.Logic;

/// <summary>The Boolean operator a single circuit gate computes, written in lower case in an instance.</summary>
internal enum CircuitGateKind { Not, And, Or }

/// <summary>
/// One gate: <c>Output = op(Inputs)</c>, written as the tuple <c>(output,op,inputs...)</c>
/// — <c>(g1,and,x1,x2)</c> or <c>(g2,not,x3)</c>. NOT takes one input wire; AND and OR take
/// two. A gate's output wire may be referenced by any number of later gates (that fan-out
/// is what makes this a circuit and not merely a formula).
/// </summary>
internal sealed record CircuitGate(CircuitGateKind Kind, string Output, IReadOnlyList<string> Inputs) {
    /// <summary>The operator's instance text: <c>and</c>, <c>or</c> or <c>not</c>.</summary>
    public static string Text(CircuitGateKind kind) => kind switch {
        CircuitGateKind.Not => "not",
        CircuitGateKind.And => "and",
        CircuitGateKind.Or => "or",
        _ => throw new InvalidOperationException($"unknown gate kind '{kind}'"),
    };

    /// <summary>The operator's instance text: <c>and</c>, <c>or</c> or <c>not</c>.</summary>
    public string OperatorText => Text(Kind);

    /// <summary>The gate as it appears inside an instance, e.g. <c>(g1,and,x1,x2)</c>.</summary>
    public string ToDefinitionString() => "(" + string.Join(",", new[] { Output, OperatorText }.Concat(Inputs)) + ")";

    public override string ToString() => ToDefinitionString();
}

/// <summary>
/// A Boolean circuit as the tuple <c>(Inputs, Gates, Output)</c> from the formal
/// definition (CLRS 3rd ed., Section 34.3): a set of input wires, a list of NOT/AND/OR gates and
/// one output wire, written <c>({x1,x2},((g1,and,x1,x2)),g1)</c>. Gates are stored in
/// dependency order — every gate's input wires are either declared inputs or outputs of
/// strictly earlier gates — which makes the circuit acyclic by construction (see
/// CircuitParser).
/// </summary>
internal sealed class BooleanCircuit {
    /// <summary>Declared input wires, in order of appearance. Duplicates collapse, since
    /// the inputs are a set in the instance text.</summary>
    public IReadOnlyList<string> Inputs { get; }

    /// <summary>Gates in dependency order.</summary>
    public IReadOnlyList<CircuitGate> Gates { get; }

    /// <summary>The wire whose value is the circuit's answer.</summary>
    public string Output { get; }

    /// <summary>Every wire name — inputs first, then gate outputs in gate order.</summary>
    public IReadOnlyList<string> Wires { get; }

    public BooleanCircuit(IReadOnlyList<string> inputs, IReadOnlyList<CircuitGate> gates, string output) {
        if (inputs.Count == 0)
            throw new ArgumentException("a circuit needs at least one input wire", nameof(inputs));
        Inputs = inputs;
        Gates = gates;
        Output = output;
        Wires = inputs.Concat(gates.Select(g => g.Output)).ToList();
    }

    /// <summary>True when some assignment of the inputs makes the output wire True.</summary>
    public bool Evaluate(IReadOnlyDictionary<string, bool> inputs) =>
        WireValues(inputs)[Output];

    /// <summary>
    /// The value of every wire under the given input assignment: inputs are copied from
    /// <paramref name="inputs"/> (every one must be present), gates are computed in order.
    /// </summary>
    public Dictionary<string, bool> WireValues(IReadOnlyDictionary<string, bool> inputs) {
        var values = new Dictionary<string, bool>();
        foreach (string wire in Inputs) {
            values[wire] = inputs[wire];
        }
        foreach (CircuitGate gate in Gates) {
            values[gate.Output] = gate.Kind switch {
                CircuitGateKind.Not => !values[gate.Inputs[0]],
                CircuitGateKind.And => values[gate.Inputs[0]] && values[gate.Inputs[1]],
                CircuitGateKind.Or => values[gate.Inputs[0]] || values[gate.Inputs[1]],
                _ => throw new InvalidOperationException($"unknown gate kind '{gate.Kind}'"),
            };
        }
        return values;
    }

    /// <summary>Canonical tuple form; parses back to the same circuit.</summary>
    public override string ToString() =>
        "({" + string.Join(",", Inputs) + "},(" + string.Join(",", Gates) + ")," + Output + ")";
}

/// <summary>
/// Shared reader/writer for the SAT-family certificate format used across the Logic
/// problems: comma-separated <c>variable:True</c> pairs, optionally wrapped in
/// parentheses, where <c>:</c> or <c>=</c> may be used as the separator and
/// True/False/T/F as the values (same accepted spellings the SAT and 3SAT verifiers
/// have always taken). Callers wrap the <see cref="FormatException"/> in whatever
/// typed parse exception their context needs (CertificateParseException for a
/// verifier, ReductionInputException for a reduction's mapSolutions).
/// </summary>
internal static class TruthAssignment {
    /// <summary>
    /// Parses a certificate into variable → value. Duplicate names: last one wins.
    /// Names the caller does not recognize are the caller's problem to ignore or reject.
    /// </summary>
    /// <exception cref="FormatException">The text is not in the certificate format.</exception>
    public static Dictionary<string, bool> Parse(string certificate) {
        if (string.IsNullOrWhiteSpace(certificate)) {
            throw new FormatException("certificate is empty");
        }

        string strippedInput = certificate.Replace(" ", "").Replace("(", "").Replace(")", "");

        var assignment = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (string assignmentText in strippedInput.Split(',')) {
            string[] parts = assignmentText.Split(':');
            if (parts.Length <= 1)
                parts = assignmentText.Split('=');
            if (parts.Length < 2) {
                throw new FormatException(
                    $"assignment '{assignmentText}' is missing a ':' or '=' separator");
            }

            string name = parts[0];
            string TF = parts[1];
            if (TF == "True" || TF == "T") {
                assignment[name] = true;
            } else if (TF == "False" || TF == "F") {
                assignment[name] = false;
            } else {
                throw new FormatException(
                    $"assignment '{assignmentText}' has value '{TF}'; expected True/False (capitalized) or T/F");
            }
        }
        return assignment;
    }

    /// <summary>
    /// Serializes the values of <paramref name="names"/> (in that order) into the
    /// canonical <c>(x1:True,x2:False)</c> form. Names missing from
    /// <paramref name="values"/> are written as False.
    /// </summary>
    public static string ToCertificate(IEnumerable<string> names, IReadOnlyDictionary<string, bool> values) =>
        "(" + string.Join(",", names.Select(n => $"{n}:{values.TryGetValue(n, out bool v) && v}")) + ")";
}
