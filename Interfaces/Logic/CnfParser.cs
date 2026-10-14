namespace API.Interfaces.Logic;

// Grammar (whitespace between tokens is ignored):
//
//   formula : clause ('&' clause)* EOF
//   clause  : '(' literal ('|' literal)* ')'
//           | literal ('|' literal)*
//   literal : '!'? IDENT
//   IDENT   : [A-Za-z_][A-Za-z0-9_]*
//
// Plus one rule the grammar can't express: when a formula has more than one clause, every
// multi-literal clause must be parenthesized. Otherwise "a | b & c" would be read as
// (a | b) & c, which is not what the usual precedence (& binds tighter than |) means.
// A bare single clause ("a | b") and bare single-literal clauses ("a & (b | c)") are fine.

/// <summary>Thrown when text is not a well-formed CNF formula.</summary>
internal sealed class CnfParseException : FormatException {
    /// <summary>0-based offset in the input where the problem was found.</summary>
    public int Position { get; }

    public CnfParseException(string detail, int position)
        : base($"{detail} (at column {position + 1})") {
        Position = position;
    }
}

internal enum CnfTokenKind { Identifier, Not, And, Or, LParen, RParen, End }

internal readonly record struct CnfToken(CnfTokenKind Kind, string Text, int Position) {
    public string Describe() => Kind == CnfTokenKind.End ? "end of input" : $"'{Text}'";
}

internal static class CnfLexer {
    public static List<CnfToken> Tokenize(string input) {
        var tokens = new List<CnfToken>();
        int i = 0;
        while (i < input.Length) {
            char c = input[i];
            if (char.IsWhiteSpace(c)) {
                i++;
                continue;
            }
            CnfTokenKind? symbol = c switch {
                '!' => CnfTokenKind.Not,
                '&' => CnfTokenKind.And,
                '|' => CnfTokenKind.Or,
                '(' => CnfTokenKind.LParen,
                ')' => CnfTokenKind.RParen,
                _ => null,
            };
            if (symbol is { } kind) {
                tokens.Add(new CnfToken(kind, c.ToString(), i));
                i++;
                continue;
            }
            if (IsIdentifierStart(c)) {
                int start = i;
                while (i < input.Length && IsIdentifierPart(input[i]))
                    i++;
                tokens.Add(new CnfToken(CnfTokenKind.Identifier, input[start..i], start));
                continue;
            }
            if (char.IsAsciiDigit(c))
                throw new CnfParseException($"variable names must start with a letter or '_', found '{c}'", i);
            throw new CnfParseException($"unexpected character '{c}'", i);
        }
        tokens.Add(new CnfToken(CnfTokenKind.End, "", input.Length));
        return tokens;
    }

    private static bool IsIdentifierStart(char c) => char.IsAsciiLetter(c) || c == '_';
    private static bool IsIdentifierPart(char c) => char.IsAsciiLetterOrDigit(c) || c == '_';
}

/// <summary>Recursive-descent parser for CNF formulas such as <c>(x1 | !x2) &amp; x3</c>.</summary>
internal sealed class CnfParser {
    private readonly List<CnfToken> _tokens;
    private int _index;

    private CnfParser(List<CnfToken> tokens) {
        _tokens = tokens;
    }

    /// <exception cref="CnfParseException">The input is not a well-formed CNF formula.</exception>
    public static CnfFormula Parse(string input) {
        ArgumentNullException.ThrowIfNull(input);
        return new CnfParser(CnfLexer.Tokenize(input)).ParseFormula();
    }

    private CnfToken Current => _tokens[_index];

    private bool TryConsume(CnfTokenKind kind) {
        if (Current.Kind != kind)
            return false;
        _index++;
        return true;
    }

    private CnfFormula ParseFormula() {
        if (Current.Kind == CnfTokenKind.End)
            throw new CnfParseException("formula is empty", Current.Position);

        var clauses = new List<CnfClause>();
        CnfClause? firstBareMultiLiteral = null;
        do {
            bool parenthesized = Current.Kind == CnfTokenKind.LParen;
            CnfClause clause = ParseClause();
            if (!parenthesized && clause.Literals.Count > 1)
                firstBareMultiLiteral ??= clause;
            clauses.Add(clause);
        } while (TryConsume(CnfTokenKind.And));

        if (Current.Kind == CnfTokenKind.Or)
            throw new CnfParseException("'|' cannot join parenthesized clauses; use '&' between clauses", Current.Position);
        if (Current.Kind != CnfTokenKind.End)
            throw new CnfParseException($"expected '&' or end of input, found {Current.Describe()}", Current.Position);
        if (clauses.Count > 1 && firstBareMultiLiteral is not null)
            throw new CnfParseException(
                "a clause with more than one literal must be parenthesized when the formula has several clauses, e.g. (a | b) & c",
                firstBareMultiLiteral.Position);

        return new CnfFormula(clauses);
    }

    private CnfClause ParseClause() {
        CnfToken start = Current;
        if (!TryConsume(CnfTokenKind.LParen))
            return new CnfClause(ParseDisjunction(), start.Position);

        if (Current.Kind == CnfTokenKind.RParen)
            throw new CnfParseException("empty clause '()'", start.Position);
        List<CnfLiteral> literals = ParseDisjunction();
        if (Current.Kind == CnfTokenKind.And)
            throw new CnfParseException("'&' inside a clause; CNF clauses join literals with '|' only", Current.Position);
        if (!TryConsume(CnfTokenKind.RParen))
            throw new CnfParseException($"expected '|' or ')', found {Current.Describe()}", Current.Position);
        return new CnfClause(literals, start.Position);
    }

    private List<CnfLiteral> ParseDisjunction() {
        var literals = new List<CnfLiteral> { ParseLiteral() };
        while (TryConsume(CnfTokenKind.Or))
            literals.Add(ParseLiteral());
        return literals;
    }

    private CnfLiteral ParseLiteral() {
        bool negated = TryConsume(CnfTokenKind.Not);
        CnfToken token = Current;
        switch (token.Kind) {
            case CnfTokenKind.Identifier:
                _index++;
                return new CnfLiteral(token.Text, negated);
            case CnfTokenKind.Not:
                throw new CnfParseException("double negation is not a CNF literal", token.Position);
            case CnfTokenKind.LParen:
                throw new CnfParseException(
                    negated ? "negating a parenthesized expression is not CNF" : "nested parentheses are not allowed in CNF",
                    token.Position);
            default:
                throw new CnfParseException(
                    $"expected {(negated ? "a variable after '!'" : "a literal")}, found {token.Describe()}",
                    token.Position);
        }
    }
}
