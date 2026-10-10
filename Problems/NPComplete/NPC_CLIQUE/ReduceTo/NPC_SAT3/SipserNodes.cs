namespace API.Problems.NPComplete.NPC_CLIQUE.ReduceTo.NPC_SAT3;

// Sipser's 3SAT -> Clique names each node '<literal>_<clauseIdx>' (e.g. "!x2_0"). Both directions of
// that construction read those names: SipserReduceToSAT3 (clique -> formula) and the backward map of
// SipserReduceToCliqueStandard (clique answer -> assignment) share this parser.
static class SipserNodes {
    /// <summary>Splits a node name into its literal and the clause it came from. Throws <see cref="ArgumentException"/> when the name is not Sipser-formatted.</summary>
    public static (string literal, int clauseIdx) Parse(string node) {
        int u = node.LastIndexOf('_');
        if (u < 0)
            throw new ArgumentException(
                $"Node '{node}' is not Sipser-formatted: expected '<literal>_<clauseIdx>'");
        if (!int.TryParse(node.Substring(u + 1), out int idx))
            throw new ArgumentException(
                $"Node '{node}' is not Sipser-formatted: clause index '{node.Substring(u + 1)}' is not an integer");
        return (node.Substring(0, u), idx);
    }

    /// <summary>The variable a node's literal mentions and whether the node makes it True (a plain literal) or False ("!x").</summary>
    public static (string variable, bool value) Assignment(string node) {
        (string literal, int _) = Parse(node);
        bool positive = !literal.StartsWith("!");
        return (positive ? literal : literal.Substring(1), positive);
    }
}
