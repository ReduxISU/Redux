using System.Text;
using API.Interfaces;

namespace API.Tools;

/// <summary>
/// The one grammar SAT and 3SAT accept for a formula (#759):
/// <code>
/// formula  := and EOF
/// and      := or ( '&amp;' or )*
/// or       := atom ( '|' atom )*          every atom joined by '|' must be a single clause
/// atom     := '(' and ')' | literal
/// literal  := '!'? name                   no space between '!' and the name
/// name     := [A-Za-z][A-Za-z0-9_]*
/// </code>
/// Whitespace may appear between tokens but never inside a literal. Parentheses group clauses, so a
/// group that holds an '&amp;' cannot be OR-ed with anything ("x1 | (x2 &amp; x3)" is not CNF). Anything
/// else is a <see cref="ProblemParseException"/> whose message quotes the offending text.
/// </summary>
static class CnfFormula {

    enum Kind { Literal, And, Or, Open, Close, End }

    readonly record struct Token(Kind Kind, string Text, int Position);

    /// <summary>The formula with every whitespace character (tabs, newlines) written as a plain space,
    /// so code that re-reads the instance and strips only ' ' sees the same literals as the parser.</summary>
    public static string NormalizeWhitespace(string formula) {
        var sb = new StringBuilder(formula.Length);
        foreach (char c in formula)
            sb.Append(char.IsWhiteSpace(c) ? ' ' : c);
        return sb.ToString();
    }

    /// <summary>Parses <paramref name="formula"/> into its clauses (each a list of literals such as "x1" or "!x2").</summary>
    public static List<List<string>> Parse(string problemName, string formula) {
        if (string.IsNullOrWhiteSpace(formula))
            throw new ProblemParseException(problemName, formula, "instance is empty");
        var parser = new Parser(problemName, formula, Tokenize(problemName, formula));
        return parser.ParseFormula();
    }

    static bool IsNameStart(char c) => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
    static bool IsNameChar(char c) => IsNameStart(c) || c is >= '0' and <= '9' or '_';
    static bool IsDelimiter(char c) => char.IsWhiteSpace(c) || c is '(' or ')' or '&' or '|';

    static List<Token> Tokenize(string problemName, string formula) {
        var tokens = new List<Token>();
        int i = 0;
        while (i < formula.Length) {
            char c = formula[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            switch (c) {
                case '&': tokens.Add(new Token(Kind.And, "&", i)); i++; continue;
                case '|': tokens.Add(new Token(Kind.Or, "|", i)); i++; continue;
                case '(': tokens.Add(new Token(Kind.Open, "(", i)); i++; continue;
                case ')': tokens.Add(new Token(Kind.Close, ")", i)); i++; continue;
            }
            // Anything else runs up to the next delimiter and must be one literal.
            int start = i;
            while (i < formula.Length && !IsDelimiter(formula[i])) i++;
            string word = formula.Substring(start, i - start);
            CheckLiteral(problemName, formula, word, start);
            tokens.Add(new Token(Kind.Literal, word, start));
        }
        tokens.Add(new Token(Kind.End, "", formula.Length));
        return tokens;
    }

    static void CheckLiteral(string problemName, string formula, string word, int position) {
        string name = word.StartsWith('!') ? word.Substring(1) : word;
        string at = $" (at position {position + 1})";
        if (name.Length == 0)
            throw Fail(problemName, formula,
                $"'!' (NOT) must be written directly in front of a variable name, as in '!x1'{at}");
        if (name.Contains('!'))
            throw Fail(problemName, formula,
                $"'{word}' is not a valid literal: '!' (NOT) may appear only directly in front of a variable name, as in '!x1'{at}");
        foreach (char ch in name) {
            if (!IsNameChar(ch))
                throw Fail(problemName, formula,
                    $"'{word}' is not a valid variable name: '{ch}' is not allowed; names may contain only letters, digits and underscores{at}");
        }
        if (!IsNameStart(name[0]))
            throw Fail(problemName, formula, $"'{word}' is not a valid variable name: names must start with a letter{at}");
    }

    static ProblemParseException Fail(string problemName, string formula, string detail) =>
        new(problemName, formula, detail);

    sealed class Parser(string problemName, string formula, List<Token> tokens) {
        int _next;

        Token Peek => tokens[_next];
        Token Take() => tokens[_next++];

        ProblemParseException Error(string detail) => Fail(problemName, formula, detail);

        static string Describe(Token t) => t.Kind == Kind.End ? "the end of the formula" : $"'{t.Text}' at position {t.Position + 1}";

        public List<List<string>> ParseFormula() {
            List<List<string>> clauses = ParseAnd();
            Token t = Peek;
            if (t.Kind == Kind.End)
                return clauses;
            if (t.Kind == Kind.Close)
                throw Error($"unmatched ')' at position {t.Position + 1}");
            Token prev = tokens[_next - 1];
            throw Error($"missing '|' or '&' between '{prev.Text}' and {Describe(t)}; join literals with '|' and clauses with '&'");
        }

        List<List<string>> ParseAnd() {
            List<List<string>> clauses = ParseOr();
            while (Peek.Kind == Kind.And) {
                Take();
                clauses.AddRange(ParseOr());
            }
            return clauses;
        }

        List<List<string>> ParseOr() {
            List<List<string>> first = ParseAtom();
            if (Peek.Kind != Kind.Or)
                return first;
            var clause = new List<string>();
            AddToClause(clause, first);
            while (Peek.Kind == Kind.Or) {
                Take();
                AddToClause(clause, ParseAtom());
            }
            return new List<List<string>> { clause };
        }

        void AddToClause(List<string> clause, List<List<string>> atom) {
            if (atom.Count != 1)
                throw Error($"'|' cannot join a parenthesized group that contains '&' (near position {Peek.Position + 1}); "
                    + "write the formula as clauses of literals joined by '|', with the clauses joined by '&'");
            clause.AddRange(atom[0]);
        }

        List<List<string>> ParseAtom() {
            Token t = Take();
            switch (t.Kind) {
                case Kind.Literal:
                    return new List<List<string>> { new List<string> { t.Text } };
                case Kind.Open:
                    if (Peek.Kind == Kind.Close)
                        throw Error($"empty clause '()' at position {t.Position + 1}");
                    List<List<string>> inner = ParseAnd();
                    if (Peek.Kind != Kind.Close) {
                        if (Peek.Kind == Kind.End)
                            throw Error($"the '(' at position {t.Position + 1} is never closed");
                        throw Error($"missing '|' or '&' between '{tokens[_next - 1].Text}' and {Describe(Peek)}; join literals with '|' and clauses with '&'");
                    }
                    Take();
                    return inner;
                default:
                    Token before = _next >= 2 ? tokens[_next - 2] : default;
                    string after = _next >= 2 ? $" after '{before.Text}'" : " at the start";
                    throw Error($"expected a variable or '('{after} but found {Describe(t)}");
            }
        }
    }
}
