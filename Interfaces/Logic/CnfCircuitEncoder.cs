namespace API.Interfaces.Logic;

/// <summary>
/// Turns a CNF formula (clauses as lists of literal strings, <c>"x1"</c> / <c>"!x2"</c>,
/// the shape both SAT and SAT3 expose as their <c>clauses</c> property) into an equivalent
/// Boolean circuit. Each variable becomes an input wire; each negated variable gets one
/// shared NOT gate; each clause becomes a binary tree of OR gates; the clause outputs are
/// combined by a binary tree of AND gates. The circuit is satisfiable exactly when the
/// formula is — the backbone of the SAT → CircuitSAT reductions. The instance text it
/// produces is the canonical tuple form from <see cref="BooleanCircuit.ToString"/>.
/// </summary>
internal static class CnfCircuitEncoder {
    /// <summary>Canonical CircuitSAT instance text for the given CNF clauses.</summary>
    public static string BuildInstance(IReadOnlyList<IReadOnlyList<string>> clauses) {
        if (clauses.Count == 0)
            throw new ArgumentException("a formula needs at least one clause", nameof(clauses));

        // Inputs: every variable, in order of first appearance across the formula.
        List<string> variables = clauses
            .SelectMany(c => c)
            .Select(literal => literal.TrimStart('!'))
            .Distinct()
            .ToList();

        var used = new HashSet<string>(variables, StringComparer.Ordinal);
        var gates = new List<CircuitGate>();
        var notWire = new Dictionary<string, string>(StringComparer.Ordinal);

        string NegativeWire(string variable) {
            if (!notWire.TryGetValue(variable, out string? wire)) {
                wire = NextName("n", used);
                gates.Add(new CircuitGate(CircuitGateKind.Not, wire, new[] { variable }));
                notWire[variable] = wire;
            }
            return wire;
        }

        var clauseWires = new List<string>();
        foreach (IReadOnlyList<string> clause in clauses) {
            List<string> literalWires = clause
                .Select(literal => literal.StartsWith('!') ? NegativeWire(literal[1..]) : literal)
                .ToList();
            clauseWires.Add(Fold(CircuitGateKind.Or, literalWires, "o", used, gates));
        }

        string output = Fold(CircuitGateKind.And, clauseWires, "a", used, gates);
        return new BooleanCircuit(variables, gates, output).ToString();
    }

    // Combines wires with a binary tree of `kind` gates (fan-in 2). One wire in → that
    // wire itself, no gate; two wires → a single gate.
    private static string Fold(CircuitGateKind kind, List<string> wires, string nameBase,
        HashSet<string> used, List<CircuitGate> gates) {
        string left = wires[0];
        for (int i = 1; i < wires.Count; i++) {
            string output = NextName(nameBase, used);
            gates.Add(new CircuitGate(kind, output, new[] { left, wires[i] }));
            left = output;
        }
        return left;
    }

    // 'n'/'o'/'a' are only conventions, not reserved words — an instance whose variables
    // are literally named "n" or "o1" must not break the encoding, so keep trying
    // base, base1, base2, ... until the name is unused.
    private static string NextName(string baseName, HashSet<string> used) {
        string candidate = baseName;
        for (int i = 1; used.Contains(candidate); i++)
            candidate = baseName + i;
        used.Add(candidate);
        return candidate;
    }
}
