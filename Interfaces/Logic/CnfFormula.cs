namespace API.Interfaces.Logic;

/// <summary>A CNF literal: a variable or its negation.</summary>
internal sealed record CnfLiteral(string Variable, bool Negated) {
    public override string ToString() => Negated ? "!" + Variable : Variable;
}

/// <summary>A disjunction of one or more literals.</summary>
internal sealed class CnfClause {
    public IReadOnlyList<CnfLiteral> Literals { get; }

    /// <summary>0-based offset of the clause in the source text, for error messages.</summary>
    public int Position { get; }

    public CnfClause(IReadOnlyList<CnfLiteral> literals, int position = 0) {
        if (literals.Count == 0)
            throw new ArgumentException("a clause needs at least one literal", nameof(literals));
        Literals = literals;
        Position = position;
    }

    public override string ToString() => "(" + string.Join(" | ", Literals) + ")";
}

/// <summary>A conjunction of one or more clauses.</summary>
internal sealed class CnfFormula {
    public IReadOnlyList<CnfClause> Clauses { get; }

    /// <summary>Distinct variable names, in order of first appearance.</summary>
    public IReadOnlyList<string> Variables { get; }

    public CnfFormula(IReadOnlyList<CnfClause> clauses) {
        if (clauses.Count == 0)
            throw new ArgumentException("a formula needs at least one clause", nameof(clauses));
        Clauses = clauses;
        Variables = clauses.SelectMany(c => c.Literals).Select(l => l.Variable).Distinct().ToList();
    }

    /// <summary>True when every clause has at least one literal made true by the assignment.</summary>
    public bool Evaluate(IReadOnlyDictionary<string, bool> assignment) =>
        Clauses.All(c => c.Literals.Any(l => assignment[l.Variable] != l.Negated));

    // String views for code that still works with "x" / "!x" literal strings. Each call returns
    // fresh lists, so callers may mutate them.

    /// <summary>Each clause as a list of literal strings, e.g. [["x1","!x2"],["x3"]].</summary>
    public List<List<string>> ClauseStrings() =>
        Clauses.Select(c => c.Literals.Select(l => l.ToString()).ToList()).ToList();

    /// <summary>Every literal occurrence in clause order, duplicates included.</summary>
    public List<string> LiteralStrings() =>
        Clauses.SelectMany(c => c.Literals).Select(l => l.ToString()).ToList();

    /// <summary>Canonical text form; parses back to the same formula.</summary>
    public override string ToString() => string.Join(" & ", Clauses);
}
