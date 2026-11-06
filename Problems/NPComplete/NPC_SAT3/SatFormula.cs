namespace API.Problems.NPComplete.NPC_SAT3;

/// <summary>
/// A 3SAT formula as a randomized solver works on it: its variables in order of first appearance (never a HashSet, so a
/// seed gives the same run in every process), and each clause as (variable index, negated) pairs, in the order written.
/// </summary>
sealed class SatFormula {
    public List<string> Variables { get; }
    /// <summary>The clauses as the instance writes them, for captions.</summary>
    public List<List<string>> Written { get; }
    public List<(int Variable, bool Negated)[]> Clauses { get; }

    private SatFormula(List<string> variables, List<List<string>> written, List<(int Variable, bool Negated)[]> clauses) {
        Variables = variables;
        Written = written;
        Clauses = clauses;
    }

    public static SatFormula Of(SAT3 sat3) {
        List<string> variables = sat3.literals.Where(l => l.Length > 0).Select(l => l.TrimStart('!')).Distinct().ToList();
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int v = 0; v < variables.Count; v++) index[variables[v]] = v;
        var clauses = sat3.clauses
            .Select(c => c.Where(l => l.Length > 0).Select(l => (Variable: index[l.TrimStart('!')], Negated: l.StartsWith('!'))).ToArray())
            .ToList();
        return new SatFormula(variables, sat3.clauses, clauses);
    }

    /// <summary>Whether the clause has a true literal under a whole assignment.</summary>
    public static bool Holds((int Variable, bool Negated)[] clause, bool[] values) {
        foreach (var (variable, negated) in clause)
            if (values[variable] != negated) return true;
        return false;
    }

    /// <summary>Whether the clause has a true literal under a partial assignment (null: not set yet).</summary>
    public static bool Holds((int Variable, bool Negated)[] clause, bool?[] values) {
        foreach (var (variable, negated) in clause)
            if (values[variable] is bool value && value != negated) return true;
        return false;
    }

    /// <summary>The 0-based indexes of the clauses with no true literal under a whole assignment (an empty clause is left out).</summary>
    public List<int> FalseClauses(bool[] values) {
        var list = new List<int>();
        for (int c = 0; c < Clauses.Count; c++)
            if (Clauses[c].Length > 0 && !Holds(Clauses[c], values)) list.Add(c);
        return list;
    }

    /// <summary>The 0-based indexes of the clauses with no true literal under a partial assignment (null: not set yet).</summary>
    public List<int> FalseClauses(bool?[] values) {
        var list = new List<int>();
        for (int c = 0; c < Clauses.Count; c++)
            if (Clauses[c].Length > 0 && !Holds(Clauses[c], values)) list.Add(c);
        return list;
    }

    /// <summary>The variables of a whole assignment with their values, in variable order.</summary>
    public List<KeyValuePair<string, bool>> Values(bool[] values) {
        var list = new List<KeyValuePair<string, bool>>();
        for (int v = 0; v < Variables.Count; v++)
            list.Add(new KeyValuePair<string, bool>(Variables[v], values[v]));
        return list;
    }

    /// <summary>The variables that are set in a partial assignment, with their values, in variable order (an unset one is left out).</summary>
    public List<KeyValuePair<string, bool>> Values(bool?[] values) {
        var list = new List<KeyValuePair<string, bool>>();
        for (int v = 0; v < Variables.Count; v++)
            if (values[v] is bool value) list.Add(new KeyValuePair<string, bool>(Variables[v], value));
        return list;
    }

    /// <summary>The certificate of a whole assignment: "(x1:True,x2:False)".</summary>
    public string Certificate(bool[] values) =>
        "(" + string.Join(",", Variables.Select((name, v) => name + ":" + values[v])) + ")";
}
