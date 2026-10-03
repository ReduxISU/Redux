using System.Text;
using NCalc;

namespace API.Interfaces;

/// <summary>
/// A named mathematical expression a contributor can actually evaluate, not just
/// read — e.g. a solver's <c>complexity</c>/<c>spaceComplexity</c> (a Big-O growth-rate
/// function of instance-size variables like <c>n</c>, <c>m</c>), or any other place in
/// the knowledge base that wants a real function of named variables instead of a
/// free-text description of one.
/// <para>
/// <see cref="Function"/> is written in ordinary math notation, not NCalc's own
/// syntax (NCalc is the expression-evaluation library this class evaluates through,
/// entirely internally): <c>^</c> is exponentiation (NCalc's own <c>^</c> is bitwise
/// XOR), <c>n!</c> is a postfix factorial, <c>|S|</c> is cardinality/absolute-value,
/// adjacent tokens like <c>2n</c> or a bare <c>n log n</c> multiply implicitly, and
/// <c>C(m, n)</c> is the binomial coefficient. <see cref="Evaluate(IReadOnlyDictionary{string, double})"/>
/// rewrites all of that into NCalc's grammar internally; nothing about that rewrite is
/// visible outside this class.
/// </para>
/// <para>
/// Migrating an old free-text Big-O string (e.g. the pre-existing
/// <c>ISolver.complexity</c> string) is a mechanical split at the closing paren, not a
/// rewrite — the "O(...)" framing is not part of this type, it belongs at whatever
/// call site displays the value as a Big-O bound:
/// <code>
/// // was: public string complexity { get; } = "O(2^n * n^2 * m), n = |nodes|, m = |edges|";
/// public MathematicalFunction complexity { get; } = new("2^n * n^2 * m", ", n = |nodes|, m = |edges|");
/// // displayed elsewhere as: $"O({complexity.Function}){complexity.Description}"
/// </code>
/// </para>
/// </summary>
public sealed class MathematicalFunction {
    /// <summary>The expression itself, e.g. "2^n * n^2 * m".</summary>
    public string Function { get; }

    /// <summary>
    /// Free-text commentary that travels alongside <see cref="Function"/> but is never
    /// itself evaluated — typically a variable-meaning legend, e.g.
    /// ", n = |nodes|, m = |edges|". Stored verbatim, including whatever leading
    /// separator (", ", " ", or none) it originally had.
    /// </summary>
    public string Description { get; }

    private readonly Lazy<MathExpressionRewriter.Parsed> _parsed;

    /// <summary>Constructs a function from its expression and, optionally, trailing free-text commentary.</summary>
    /// <param name="function">The expression itself, e.g. "2^n * n^2 * m".</param>
    /// <param name="description">Free-text commentary that travels with the expression. See <see cref="Description"/>.</param>
    public MathematicalFunction(string function, string description = "") {
        Function = function;
        Description = description;
        _parsed = new Lazy<MathExpressionRewriter.Parsed>(() => MathExpressionRewriter.Parse(Function));
    }

    /// <summary>
    /// The set of variable names <see cref="Function"/> actually reads (function names
    /// like the "C" in "C(m, n)" are excluded), in the order first encountered.
    /// </summary>
    public IReadOnlyList<string> VariableNames => _parsed.Value.VariableNames;

    /// <summary>
    /// Evaluates the function for a given set of variable values, e.g.
    /// <c>Evaluate(new Dictionary&lt;string, double&gt; { ["n"] = nodes.Count, ["m"] = edges.Count })</c>
    /// for a function declared as "2^n * n^2 * m". Every name in
    /// <see cref="VariableNames"/> must have an entry, or evaluation throws.
    /// </summary>
    public double Evaluate(IReadOnlyDictionary<string, double> variableValues) {
        var expression = new Expression(_parsed.Value.NCalcSyntax);
        MathExpressionRewriter.RegisterFunctions(expression);
        foreach (var (name, value) in variableValues) {
            expression.Parameters[name] = value;
        }
        try {
            return Convert.ToDouble(expression.Evaluate());
        } catch (Exception ex) {
            throw new InvalidOperationException(
                $"Failed to evaluate function \"{Function}\": {ex.Message}", ex);
        }
    }

    /// <summary>Convenience overload of <see cref="Evaluate(IReadOnlyDictionary{string, double})"/>.</summary>
    public double Evaluate(params (string Name, double Value)[] variableValues) =>
        Evaluate(variableValues.ToDictionary(v => v.Name, v => v.Value));

    /// <summary>The expression itself — same text as <see cref="Function"/>.</summary>
    public override string ToString() => Function;

    /// <summary>
    /// Rewrites ordinary math notation (see the class docs) into NCalc's own
    /// expression grammar. Internal to <see cref="MathematicalFunction"/> — nothing
    /// outside this file needs to know NCalc is what evaluates the result.
    /// </summary>
    private static class MathExpressionRewriter {
        public readonly record struct Parsed(string NCalcSyntax, IReadOnlyList<string> VariableNames);

        public static void RegisterFunctions(Expression expression) {
            expression.Functions["Factorial"] = args => {
                double n = Convert.ToDouble(args.Evaluate(0));
                double result = 1;
                for (int i = 2; i <= (int)Math.Round(n); i++) result *= i;
                return result;
            };
            expression.Functions["Abs"] = args => Math.Abs(Convert.ToDouble(args.Evaluate(0)));
            // Single-argument natural log — the base is irrelevant to a Big-O claim,
            // so contributors write bare "log n" rather than picking one.
            expression.Functions["Log"] = args => Math.Log(Convert.ToDouble(args.Evaluate(0)));
            expression.Functions["Sqrt"] = args => Math.Sqrt(Convert.ToDouble(args.Evaluate(0)));
            // Binomial coefficient — "choose", used as e.g. "C(m, n)".
            expression.Functions["C"] = args => {
                double n = Convert.ToDouble(args.Evaluate(0));
                double k = Convert.ToDouble(args.Evaluate(1));
                double result = 1;
                for (int i = 0; i < (int)Math.Round(k); i++) result = result * (n - i) / (i + 1);
                return result;
            };
        }

        public static Parsed Parse(string bigOExpression) {
            var node = ParseToAst(bigOExpression);

            var syntax = new StringBuilder();
            Render(node, syntax);

            var names = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            Collect(node, names, seen);

            return new Parsed(syntax.ToString(), names);
        }

        private static void Collect(Node node, List<string> names, HashSet<string> seen) {
            switch (node) {
                case IdentNode id:
                    if (seen.Add(id.Name)) names.Add(id.Name);
                    break;
                case CallNode call:
                    foreach (var a in call.Args) Collect(a, names, seen);
                    break;
                case PowNode p:
                    Collect(p.Base, names, seen);
                    Collect(p.Exponent, names, seen);
                    break;
                case UnaryNode u:
                    Collect(u.Inner, names, seen);
                    break;
                case BinOpNode b:
                    Collect(b.Left, names, seen);
                    Collect(b.Right, names, seen);
                    break;
                case NumberNode:
                    break;
                default:
                    throw new InvalidOperationException($"Unhandled Big-O expression node type {node.GetType()}.");
            }
        }

        private static void Render(Node node, StringBuilder sb) {
            switch (node) {
                case NumberNode n:
                    sb.Append(n.Text);
                    break;
                case IdentNode id:
                    sb.Append(id.Name);
                    break;
                case CallNode call:
                    sb.Append(CanonicalFunctionName(call.Name));
                    sb.Append('(');
                    for (int i = 0; i < call.Args.Count; i++) {
                        if (i > 0) sb.Append(',');
                        Render(call.Args[i], sb);
                    }
                    sb.Append(')');
                    break;
                case FactorialNode f:
                    sb.Append("Factorial(");
                    Render(f.Inner, sb);
                    sb.Append(')');
                    break;
                case AbsNode a:
                    sb.Append("Abs(");
                    Render(a.Inner, sb);
                    sb.Append(')');
                    break;
                case UnaryMinusNode m:
                    sb.Append("(-");
                    Render(m.Inner, sb);
                    sb.Append(')');
                    break;
                case PowNode p:
                    sb.Append("Pow(");
                    Render(p.Base, sb);
                    sb.Append(',');
                    Render(p.Exponent, sb);
                    sb.Append(')');
                    break;
                case BinOpNode b:
                    sb.Append('(');
                    Render(b.Left, sb);
                    sb.Append(b.Op);
                    Render(b.Right, sb);
                    sb.Append(')');
                    break;
                default:
                    throw new InvalidOperationException($"Unhandled Big-O expression node type {node.GetType()}.");
            }
        }

        private static string CanonicalFunctionName(string name) => name.ToLowerInvariant() switch {
            "sqrt" => "Sqrt",
            "log" or "ln" => "Log",
            _ => name,
        };

        // ---- Recursive-descent parser --------------------------------------------
        // expr   := term (('+' | '-') term)*
        // term   := power ( (('*' | '/' | '%') power) | power )*   // trailing `| power`
        //           is implicit multiplication, e.g. "2n" or a bare "n log n"
        // power  := unary ('^' power)?                              // right-associative
        // unary  := '-' unary | postfix
        // postfix:= primary '!'*
        // primary:= NUMBER
        //         | 'log'/'ln' postfix                              // bare, no parens
        //         | IDENT ('(' expr (',' expr)* ')')?
        //         | '(' expr ')'
        //         | '|' expr '|'

        private abstract class Node { }
        private sealed class NumberNode : Node { public required string Text; }
        private sealed class IdentNode : Node { public required string Name; }
        private sealed class CallNode : Node { public required string Name; public required List<Node> Args; }
        private abstract class UnaryNode : Node { public required Node Inner; }
        private sealed class FactorialNode : UnaryNode { }
        private sealed class AbsNode : UnaryNode { }
        private sealed class UnaryMinusNode : UnaryNode { }
        private sealed class PowNode : Node { public required Node Base; public required Node Exponent; }
        private sealed class BinOpNode : Node { public required Node Left; public required Node Right; public required string Op; }

        private static readonly HashSet<string> BareFunctionNames = new(StringComparer.OrdinalIgnoreCase) { "log", "ln" };

        private sealed class Tokenizer {
            public readonly List<(string Kind, string Text)> Tokens = new();
            private int _pos;

            public Tokenizer(string s) {
                int i = 0;
                while (i < s.Length) {
                    char c = s[i];
                    if (char.IsWhiteSpace(c)) { i++; continue; }
                    if (char.IsDigit(c) || (c == '.' && i + 1 < s.Length && char.IsDigit(s[i + 1]))) {
                        int start = i;
                        while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
                        Tokens.Add(("num", s[start..i]));
                        continue;
                    }
                    if (char.IsLetter(c) || c == '_') {
                        int start = i;
                        while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i++;
                        Tokens.Add(("ident", s[start..i]));
                        continue;
                    }
                    if ("+-*/%^(),!|".IndexOf(c) >= 0) {
                        Tokens.Add(("op", c.ToString()));
                        i++;
                        continue;
                    }
                    throw new FormatException($"Unexpected character '{c}' at position {i} in Big-O expression \"{s}\".");
                }
            }

            public (string Kind, string Text)? Peek => _pos < Tokens.Count ? Tokens[_pos] : null;
            public (string Kind, string Text) Next() => Tokens[_pos++];
            public bool AtEnd => _pos >= Tokens.Count;

            public bool PeekIsOp(string op) => Peek is { } t && t.Kind == "op" && t.Text == op;

            public void Expect(string op) {
                if (!PeekIsOp(op))
                    throw new FormatException($"Expected '{op}' but found {(Peek is { } t ? $"'{t.Text}'" : "end of expression")}.");
                Next();
            }

            /// True if the next token could start a new primary — used to detect
            /// implicit multiplication ("2n") between two adjacent expressions. Excludes
            /// '|' deliberately: it is both the open and close delimiter of an
            /// absolute-value/cardinality group, and treating a bare '|' as "starts a
            /// new primary" would make a closing '|' look like the start of a nested
            /// one. Implicit multiplication directly against a "|...|" group (e.g.
            /// "2|S|") isn't needed by any current expression, so this is the simple
            /// fix rather than tracking open/close pipe state through the tokenizer.
            public bool PeekStartsPrimary() => Peek is { } t &&
                (t.Kind == "num" || t.Kind == "ident" || (t.Kind == "op" && t.Text == "("));
        }

        private static Node ParseToAst(string expr) {
            var tk = new Tokenizer(expr);
            var node = ParseExpr(tk);
            if (!tk.AtEnd)
                throw new FormatException($"Unexpected trailing input in Big-O expression \"{expr}\" near '{tk.Peek!.Value.Text}'.");
            return node;
        }

        private static Node ParseExpr(Tokenizer tk) {
            var left = ParseTerm(tk);
            while (tk.PeekIsOp("+") || tk.PeekIsOp("-")) {
                string op = tk.Next().Text;
                var right = ParseTerm(tk);
                left = new BinOpNode { Left = left, Right = right, Op = op };
            }
            return left;
        }

        private static Node ParseTerm(Tokenizer tk) {
            var left = ParsePower(tk);
            while (true) {
                if (tk.PeekIsOp("*") || tk.PeekIsOp("/") || tk.PeekIsOp("%")) {
                    string op = tk.Next().Text;
                    var right = ParsePower(tk);
                    left = new BinOpNode { Left = left, Right = right, Op = op };
                } else if (tk.PeekStartsPrimary()) {
                    // Implicit multiplication: "2n", "n log n", etc.
                    var right = ParsePower(tk);
                    left = new BinOpNode { Left = left, Right = right, Op = "*" };
                } else {
                    break;
                }
            }
            return left;
        }

        private static Node ParsePower(Tokenizer tk) {
            var baseNode = ParseUnary(tk);
            if (tk.PeekIsOp("^")) {
                tk.Next();
                var exponent = ParsePower(tk); // right-associative
                return new PowNode { Base = baseNode, Exponent = exponent };
            }
            return baseNode;
        }

        private static Node ParseUnary(Tokenizer tk) {
            if (tk.PeekIsOp("-")) {
                tk.Next();
                return new UnaryMinusNode { Inner = ParseUnary(tk) };
            }
            return ParsePostfix(tk);
        }

        private static Node ParsePostfix(Tokenizer tk) {
            var node = ParsePrimary(tk);
            while (tk.PeekIsOp("!")) {
                tk.Next();
                node = new FactorialNode { Inner = node };
            }
            return node;
        }

        private static Node ParsePrimary(Tokenizer tk) {
            var token = tk.Peek ?? throw new FormatException("Unexpected end of Big-O expression.");

            if (token.Kind == "num") {
                tk.Next();
                return new NumberNode { Text = token.Text };
            }

            if (token.Kind == "ident") {
                tk.Next();
                if (tk.PeekIsOp("(")) {
                    tk.Next();
                    var args = new List<Node>();
                    if (!tk.PeekIsOp(")")) {
                        args.Add(ParseExpr(tk));
                        while (tk.PeekIsOp(",")) {
                            tk.Next();
                            args.Add(ParseExpr(tk));
                        }
                    }
                    tk.Expect(")");
                    return new CallNode { Name = token.Text, Args = args };
                }
                if (BareFunctionNames.Contains(token.Text) && tk.PeekStartsPrimary()) {
                    // Bare "log n" / "ln n" — no parens, single implicit argument.
                    var arg = ParsePostfix(tk);
                    return new CallNode { Name = token.Text, Args = new List<Node> { arg } };
                }
                return new IdentNode { Name = token.Text };
            }

            if (token.Kind == "op" && token.Text == "(") {
                tk.Next();
                var inner = ParseExpr(tk);
                tk.Expect(")");
                return inner;
            }

            if (token.Kind == "op" && token.Text == "|") {
                tk.Next();
                var inner = ParseExpr(tk);
                tk.Expect("|");
                return new AbsNode { Inner = inner };
            }

            throw new FormatException($"Unexpected token '{token.Text}' in Big-O expression.");
        }
    }
}
