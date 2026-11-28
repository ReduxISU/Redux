using SPADE;

namespace API.Interfaces.Logic;

// Grammar (SPADE definition string, see CircuitParser.InstanceGrammar):
//
//   circuit : '(' inputs ',' gates ',' output ')'
//   inputs  : '{' ID (',' ID)* '}'
//   gates   : '(' gate (',' gate)* ')'
//   gate    : '(' ID ',' operator ',' ID (',' ID)? ')'
//   operator: 'and' | 'or' | 'not'
//   ID      : [A-Za-z_][A-Za-z0-9_]*
//
// A circuit is the tuple (Inputs, Gates, Output) from the formal definition of
// CIRCUIT-SAT (CLRS 3rd ed., Section 34.3), so the instance reads
// ({x1,x2,x3},((g1,and,x1,x2),(g2,not,x3),(g3,or,g1,g2)),g3).
//
// SPADE enforces the SHAPE — three components, inputs a set, gates a list, output a name
// — which is all a definition string can say. Everything that depends on the MEANING of
// the names is checked here, with a message that names the offending wire: the wire-name
// alphabet, the operator word, the operator's fan-in, that a gate's inputs are already
// declared, and that the output exists. As elsewhere in Redux, a set silently collapses
// duplicates, so `({x1,x1},(),x1)` is simply a circuit with one input.
//
// A gate may only reference declared inputs or the outputs of EARLIER gates — that
// ordering rule is also what makes the circuit acyclic, which is what lets the solver and
// the verifier evaluate it in one pass. The output wire may be any declared wire,
// including a bare input, and a circuit may have no gates at all (`({x1},(),x1)`), which
// is what a single-literal CNF formula becomes.

/// <summary>Thrown when text is not a well-formed Boolean circuit.</summary>
internal sealed class CircuitParseException : FormatException {
    public CircuitParseException(string detail) : base(detail) { }
}

/// <summary>
/// Reads a CircuitSAT instance such as <c>({x1,x2},((g1,and,x1,x2)),g1)</c> into a
/// <see cref="BooleanCircuit"/>, using SPADE for the tuple structure.
/// </summary>
internal sealed class CircuitParser {
    /// <summary>SPADE definition string: the input-wire set, the gate list, the output wire.</summary>
    public const string InstanceGrammar = "{(I,G,o) | I is set, G is list, o is string}";

    private CircuitParser() { }

    /// <exception cref="CircuitParseException">The input is not a well-formed circuit.</exception>
    public static BooleanCircuit Parse(string input) {
        ArgumentNullException.ThrowIfNull(input);

        // SPADE's tokenizer does not tolerate insignificant whitespace between elements
        // (it reads " x2" as a different name), so strip it before handing the structure over.
        string text = string.Concat(input.Where(c => !char.IsWhiteSpace(c)));
        if (text.Length == 0)
            throw new CircuitParseException("circuit is empty");

        // SPADE accepts trailing junk after a well-formed prefix, so the instance must be
        // one balanced parenthesized tuple covering the whole string.
        EnsureSingleBalancedTuple(text);

        List<string> wires;
        List<List<string>> gateTuples;
        string output;
        try {
            // Read everything inside the try: SPADE can accept the text and fail only when
            // its parts are materialized or when a component is missing.
            StringParser parser = new(InstanceGrammar);
            parser.parse(text);
            wires = ((UtilCollection)parser["I"]).ToList().Select(e => e.ToString()).ToList();
            gateTuples = ((UtilCollection)parser["G"]).ToList().Select(GateTuple).ToList();
            output = ((UtilCollection)parser["o"]).ToString();
        } catch (CircuitParseException) {
            throw;
        } catch (Exception e) {
            throw new CircuitParseException(Explain(e));
        }

        return Build(wires, gateTuples, output);
    }

    // One element of the gate list. A gate must itself be an ordered tuple: SPADE hands
    // back a scalar (count 0, not ordered) for `(g1)`, and a set for `{g1,and,x1}`, so
    // both are rejected here rather than crashing the caller.
    private static List<string> GateTuple(UtilCollection element) {
        try {
            element.assertOrdered();
        } catch (Exception) {
            throw new CircuitParseException(
                $"each gate is a tuple (output,operator,inputs), e.g. (g1,and,x1,x2) or (g2,not,x3), found '{element}'");
        }
        return element.ToList().Select(e => e.ToString()).ToList();
    }

    private static BooleanCircuit Build(List<string> inputs, List<List<string>> gateTuples, string output) {
        if (inputs.Count == 0)
            throw new CircuitParseException("circuit has no input wires");

        foreach (string wire in inputs)
            ValidateName(wire);

        var defined = new HashSet<string>(inputs, StringComparer.Ordinal);
        var gates = new List<CircuitGate>();

        foreach (List<string> tuple in gateTuples) {
            if (tuple.Count < 3)
                throw new CircuitParseException(
                    $"gate '{string.Join(",", tuple)}' is missing a part: a gate is (output,operator,inputs)");

            string gateOutput = tuple[0];
            ValidateName(gateOutput);
            if (!defined.Add(gateOutput))
                throw new CircuitParseException($"wire '{gateOutput}' is defined twice");

            CircuitGateKind kind = ParseOperator(tuple[1], gateOutput);
            List<string> operands = tuple.GetRange(2, tuple.Count - 2);
            ValidateFanIn(kind, gateOutput, operands);
            foreach (string wire in operands) {
                ValidateName(wire);
                // The gate's own output is in `defined` but is never a legal operand, so a
                // self-reference like (g1,and,g1,x1) is reported the same way as a
                // forward reference: neither is a name that is already available.
                if (wire == gateOutput || !defined.Contains(wire))
                    throw new CircuitParseException(
                        $"'{wire}' is not a declared input or earlier gate output");
            }

            gates.Add(new CircuitGate(kind, gateOutput, operands));
        }

        ValidateName(output);
        if (!defined.Contains(output))
            throw new CircuitParseException($"output wire '{output}' is not declared");

        return new BooleanCircuit(inputs, gates, output);
    }

    private static CircuitGateKind ParseOperator(string text, string gateOutput) {
        switch (text.ToLowerInvariant()) {
            case "and": return CircuitGateKind.And;
            case "or": return CircuitGateKind.Or;
            case "not": return CircuitGateKind.Not;
            default:
                throw new CircuitParseException(
                    $"gate '{gateOutput}' has operator '{text}'; expected 'and', 'or' or 'not'");
        }
    }

    private static void ValidateFanIn(CircuitGateKind kind, string gateOutput, List<string> operands) {
        (int expected, string shape) = kind switch {
            CircuitGateKind.Not => (1, $"({gateOutput},not,a)"),
            CircuitGateKind.And => (2, $"({gateOutput},and,a,b)"),
            _ => (2, $"({gateOutput},or,a,b)"),
        };
        if (operands.Count != expected)
            throw new CircuitParseException(
                $"gate '{gateOutput}' uses '{CircuitGate.Text(kind)}', which takes {expected} input " +
                $"wire{(expected == 1 ? "" : "s")}; write it as {shape}");
    }

    private static void ValidateName(string wire) {
        if (wire.Length == 0)
            throw new CircuitParseException("a wire needs a name");
        if (!(char.IsAsciiLetter(wire[0]) || wire[0] == '_'))
            throw new CircuitParseException(
                $"wire names must start with a letter or '_': '{wire}'");
        foreach (char c in wire) {
            if (!(char.IsAsciiLetterOrDigit(c) || c == '_'))
                throw new CircuitParseException($"'{wire}' is not a valid wire name: '{c}' is not allowed");
        }
    }

    // SPADE reports structural problems in its own vocabulary; translate the ones a caller
    // can actually act on and keep the original text otherwise.
    private static string Explain(Exception e) => e.Message switch {
        string m when m.Contains("not a set") =>
            "the input wires are a set in braces: ({x1,x2},...)",
        string m when m.Contains("not a list") =>
            "the gates are a parenthesized list of gate tuples: ((g1,and,x1,x2),(g2,not,x3))",
        "Braces not matched" =>
            "')' is missing: an instance is ({inputs},(gates),output)",
        string m when m.Contains("Index") =>
            "an instance has exactly three components: ({inputs},(gates),output)",
        string m when m.Contains("Cannot index into a set") =>
            "an instance is a tuple, not a single name: ({inputs},(gates),output)",
        string m => m,
    };

    private static void EnsureSingleBalancedTuple(string text) {
        if (text[0] != '(')
            throw new CircuitParseException(
                "an instance is a tuple of three components: ({inputs},(gates),output)");

        int depth = 0;
        for (int i = 0; i < text.Length; i++) {
            switch (text[i]) {
                case '(' or '{':
                    depth++;
                    break;
                case ')' or '}':
                    depth--;
                    if (depth < 0)
                        throw new CircuitParseException($"'{text[i]}' at position {i + 1} has no matching opener");
                    if (depth == 0 && i != text.Length - 1)
                        throw new CircuitParseException(
                            $"unexpected text after the instance tuple, starting at position {i + 2}");
                    break;
            }
        }
        if (depth != 0)
            throw new CircuitParseException("')' is missing: an instance is ({inputs},(gates),output)");
    }
}
