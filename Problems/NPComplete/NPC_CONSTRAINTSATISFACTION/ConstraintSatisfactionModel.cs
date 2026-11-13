using API.Interfaces;

namespace API.Problems.NPComplete.NPC_CONSTRAINTSATISFACTION;

// This model is shared by the solver and verifier to validate and evaluate constraints.
// Expressions are prepared once and evaluated step by step. Evaluation can be stopped between arithmetic operations.
// Checks syntax and variable references once, then evaluates constraints against complete or partial assignments.
internal sealed class ConstraintSatisfactionModel {
    // A postfix instruction is either a literal, an indexed variable read, or an operator.
    // Indices refer to the problem's stable X ordering, avoiding name lookups during search.
    private sealed record Instruction(string Operator, decimal Number = 0, int Variable = -1);
    // Each arithmetic operand of a predicate has its own program and evaluation stack.
    private sealed record Arithmetic(Instruction[] Code) {
        // Executes a prepared operand. The enclosing constraint must first establish that
        // all referenced variables are assigned. Arithmetic errors deliberately propagate.
        internal decimal Evaluate(decimal?[] values, Action? checkTime) {
            // Postfix order removes the need for recursive tree evaluation: operands are
            // pushed before their operator. Cancellation is checked for each instruction.
            var stack = new Stack<decimal>();
            foreach (var instruction in Code) {
                checkTime?.Invoke();
                if (instruction.Operator == "number") stack.Push(instruction.Number);
                else if (instruction.Operator == "variable") stack.Push(values[instruction.Variable]!.Value);
                else if (instruction.Operator == "negate") stack.Push(-stack.Pop());
                // Unary plus leaves the top value unchanged. Binary operators pop the
                // right operand first; reversing this order would break subtraction/division.
                else if (instruction.Operator != "positive") {
                    decimal right = stack.Pop(), left = stack.Pop();
                    stack.Push(instruction.Operator switch {
                        "+" => left + right,
                        "-" => left - right,
                        "*" => left * right,
                        "/" => left / right,
                        _ => throw new InvalidOperationException("Uncompiled operator.")
                    });
                }
            }
            return stack.Pop();
        }
    }
    // Expressions contains the left operand followed by the right comparison operand,
    // set elements, or two range endpoints. Variables is the union of their dependencies.
    private sealed record Constraint(Arithmetic[] Expressions, int[] Variables, string Operator,
        bool Negated = false, bool LowerInclusive = false, bool UpperInclusive = false) {
        // Returns whether this predicate is satisfied or still deferred. Deferral means
        // no contradiction is known; it does not guarantee that a solution extension exists.
        internal bool Holds(decimal?[] values, Action? checkTime) {
            // Require the entire scope, even if algebraic simplification or a matching
            // set element could appear to make some references unnecessary. This avoids
            // incomplete reads and keeps readiness independent of arithmetic shortcuts.
            foreach (int variable in Variables) {
                checkTime?.Invoke();
                if (!values[variable].HasValue) return true; // Defer until all dependencies are ready.
            }
            // Evaluate every operand eagerly. A matching set element must not conceal
            // division by zero or overflow in a later element of that same predicate.
            var operands = new decimal[Expressions.Length];
            for (int i = 0; i < operands.Length; i++) operands[i] = Expressions[i].Evaluate(values, checkTime);
            decimal left = operands[0];
            bool result;
            if (Operator == "set") {
                result = false;
                for (int i = 1; i < operands.Length; i++) { checkTime?.Invoke(); result |= left == operands[i]; }
            } else if (Operator == "range") {
                // Reversed bounds are invalid arithmetic input, not an empty interval.
                // Equal bounds form a singleton only when both endpoints are included.
                if (operands[1] > operands[2]) throw new InvalidOperationException("Range lower bound exceeds upper bound.");
                result = (LowerInclusive ? left >= operands[1] : left > operands[1]) &&
                    (UpperInclusive ? left <= operands[2] : left < operands[2]);
            } else result = Operator switch {
                "=" or "==" => left == operands[1],
                "!=" => left != operands[1],
                "<" => left < operands[1],
                "<=" => left <= operands[1],
                ">" => left > operands[1],
                ">=" => left >= operands[1],
                _ => throw new InvalidOperationException("Uncompiled comparison.")
            };
            return Negated ? !result : result;
        }
    }
    // Prepared programs are private to a solve/verify call; assignments stay outside them.
    private readonly List<Constraint> constraints = new();
    // Builds the variable index and prepares every constraint before search begins.
    // Syntax errors retain Redux problem context; cancellation and evaluation errors
    // are not reclassified as parse failures.
    internal ConstraintSatisfactionModel(CONSTRAINTSATISFACTION problem, Action? checkTime = null) {
        ArgumentNullException.ThrowIfNull(problem);
        var indices = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < problem.X.Count; i++) { checkTime?.Invoke(); indices.Add(problem.X[i], i); }
        foreach (string expression in problem.C) {
            checkTime?.Invoke();
            try { constraints.Add(new Parser(expression, indices, checkTime).Parse()); } catch (FormatException ex) {
                throw new ProblemParseException(problem.problemName, problem.instance, $"Invalid constraint '{expression}': {ex.Message}", ex);
            }
        }
    }
    // Checks all ready predicates, including constant-only predicates at the root.
    // Deliberately avoids short-circuiting across constraints so an earlier false result
    // cannot hide a later evaluation error in a structurally valid assignment.
    internal bool Consistent(decimal?[] values, Action? checkTime = null) {
        bool result = true;
        foreach (var constraint in constraints) {
            checkTime?.Invoke();
            result &= constraint.Holds(values, checkTime); // Do not hide errors behind an earlier false predicate.
        }
        return result;
    }
    // Serializes a complete assignment in input-variable order using the same numeric
    // grammar accepted by the verifier. An empty complete assignment is represented by ().
    // Callers must establish completeness before invoking this method.
    internal static string Certificate(CONSTRAINTSATISFACTION problem, decimal?[] values, Action? checkTime = null) {
        var pairs = new List<string>();
        for (int i = 0; i < values.Length; i++) { checkTime?.Invoke(); pairs.Add($"{problem.X[i]}:{CspNumber.Format(values[i]!.Value)}"); }
        checkTime?.Invoke();
        string result = "(" + string.Join(", ", pairs) + ")";
        checkTime?.Invoke();
        return result;
    }
    // Tokenizes one constraint and separates its predicate from arithmetic operands.
    // Arithmetic uses an iterative shunting-yard pass; collection delimiters belong
    // to the predicate parser rather than the arithmetic operator stack.
    private sealed class Parser {
        private readonly List<string> tokens = new();
        private readonly IReadOnlyDictionary<string, int> indices;
        // Accumulates references across every operand, including set members/endpoints.
        private readonly HashSet<int> variables = new();
        private readonly Action? checkTime;
        private int position;
        private string Current => position < tokens.Count ? tokens[position] : "<end>";
        // Scan every character instead of matching fragments: unsupported characters
        // cannot be silently skipped. Signs are operators, not part of number tokens.
        internal Parser(string text, IReadOnlyDictionary<string, int> indices, Action? checkTime) {
            this.indices = indices; this.checkTime = checkTime;
            for (int i = 0; i < text.Length;) {
                checkTime?.Invoke();
                char c = text[i];
                if (char.IsWhiteSpace(c)) { i++; continue; }
                int start = i++;
                if (char.IsAsciiLetter(c) || c == '_') {
                    while (i < text.Length && (char.IsAsciiLetterOrDigit(text[i]) || text[i] == '_')) { checkTime?.Invoke(); i++; }
                } else if (char.IsAsciiDigit(c) || c == '.') {
                    while (i < text.Length && (char.IsAsciiDigit(text[i]) || text[i] == '.')) { checkTime?.Invoke(); i++; }
                } else if ("+-*/(){},[]=!<>".Contains(c)) {
                    if ("=!<>".Contains(c) && i < text.Length && text[i] == '=') i++;
                } else throw new FormatException($"Unexpected character '{c}'.");
                tokens.Add(text[start..i]);
            }
        }
        // The end marker makes incomplete predicates report a controlled format error.
        private string Take() { checkTime?.Invoke(); string token = Current; position++; return token; }
        private void Expect(string token) {
            if (Current != token) throw new FormatException($"Expected '{token}', found '{Current}'.");
            Take();
        }
        private bool Keyword(string word) => Current.Equals(word, StringComparison.OrdinalIgnoreCase);
        // Parses exactly one comparison or membership predicate and rejects trailing data.
        // Membership supports sets and independently open/closed interval endpoints.
        internal Constraint Parse() {
            // Only the left operand may contain a membership keyword attached by the
            // webpage's space removal. Right operands retain the original arithmetic grammar.
            var expressions = new List<Arithmetic> { ParseArithmetic(allowCompactMembership: true) };
            string op = Take();
            // Parentheses or a numeric left operand already separate tokens, e.g.
            // (x+y)notin[2,5] or 2notin{3}. Normalize the joined keyword before using
            // the existing membership parser; spaced "not in" still follows its usual path.
            if (op.Equals("notin", StringComparison.OrdinalIgnoreCase)) {
                tokens.Insert(position, "in");
                op = "not";
            }
            // Only membership accepts "not". Ordinary arithmetic comparisons retain
            // their own operators, including !=; Boolean combinations are unsupported.
            bool negated = op.Equals("not", StringComparison.OrdinalIgnoreCase);
            if (negated) { if (!Keyword("in")) throw new FormatException("Expected 'in' after 'not'."); op = Take(); }
            bool lower = false, upper = false;
            if (op.Equals("in", StringComparison.OrdinalIgnoreCase)) {
                // Braces identify explicit set elements; brackets/parentheses identify
                // a two-endpoint range. Empty sets are legal and need no right operands.
                if (Current == "{") {
                    Take();
                    if (Current != "}") {
                        expressions.Add(ParseArithmetic());
                        while (Current == ",") { Take(); expressions.Add(ParseArithmetic()); }
                    }
                    Expect("}"); op = "set";
                } else {
                    if (Current is not ("[" or "(")) throw new FormatException("Expected a set or range.");
                    lower = Take() == "[";
                    expressions.Add(ParseArithmetic()); Expect(","); expressions.Add(ParseArithmetic());
                    if (Current is not ("]" or ")")) throw new FormatException("Expected range closing delimiter.");
                    upper = Take() == "]"; op = "range";
                }
            } else {
                if (op is not ("=" or "==" or "!=" or "<" or "<=" or ">" or ">=")) throw new FormatException("Expected one comparison or membership operator.");
                expressions.Add(ParseArithmetic());
            }
            // Full consumption rejects chained comparisons and otherwise valid prefixes
            // followed by unexpected input. No accepted suffix is ignored.
            if (position != tokens.Count) throw new FormatException($"Unexpected token '{Current}'.");
            return new Constraint(expressions.ToArray(), variables.ToArray(), op, negated, lower, upper);
        }
        // Recover the single variable/operator boundary lost in inputs such as
        // xnotin(2,5) and x+yin{2,5}. This is deliberately not a global replacement:
        // identifiers may themselves contain "in" or "notin", and right-hand set
        // elements/range bounds must never acquire nested membership predicates.
        private void ExpandCompactMembership() {
            // A compact operator must be immediately followed by a set/range opener.
            // Names in comparisons (e.g. xnotin=2) are therefore left untouched.
            if (position + 1 >= tokens.Count || tokens[position + 1] is not ("{" or "[" or "(")) return;
            string token = Current;
            string? variable = null, keyword = null;
            foreach (string suffix in new[] { "notin", "in" }) {
                checkTime?.Invoke();
                // Keywords are case insensitive, while the remaining variable prefix
                // must exactly match a declared name. Unknown names are never guessed.
                if (token.Length <= suffix.Length || !token.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) continue;
                string prefix = token[..^suffix.Length];
                if (!indices.ContainsKey(prefix)) continue;
                // If x and xnot both exist, xnotin could mean either x not in or
                // xnot in. Parenthesizing the left operand preserves that distinction
                // even after the webpage removes spaces; do not silently choose one.
                if (variable != null) throw new FormatException("Ambiguous compact membership; parenthesize the left operand, e.g. (x) not in [2,5].");
                variable = prefix;
                keyword = suffix;
            }
            if (variable == null) return;
            // Split only this token. All later parsing, dependency tracking, evaluation,
            // and certificate validation continue through the existing shared code.
            tokens[position] = variable;
            tokens.Insert(position + 1, keyword!);
        }
        // Shunting yard preserves precedence and left associativity with no recursive calls.

        // Produces postfix instructions without recursion. Stops at the first predicate
        // or collection delimiter outside arithmetic parentheses, leaving it for Parse.
        // The operand flag distinguishes unary signs from binary addition/subtraction.
        private Arithmetic ParseArithmetic(bool allowCompactMembership = false) {
            var output = new List<Instruction>();
            var operators = new Stack<string>();
            bool operand = true;
            // Depth counts arithmetic grouping only. A closing range parenthesis at
            // depth zero must remain unconsumed for the membership parser.
            int depth = 0;
            while (position < tokens.Count) {
                // Membership terminates the outer arithmetic operand. Expanding only
                // when an operand is expected also leaves a standalone "notin" token
                // after a completed expression for Parse() to handle as the operator.
                if (allowCompactMembership && operand && depth == 0) ExpandCompactMembership();
                checkTime?.Invoke(); string token = Current;
                if (operand) {
                    // Stack unary signs in encounter order; popping applies them from
                    // the inside out, so repeated signs and -(...) behave consistently.
                    if (token is "+" or "-") { operators.Push(Take() == "+" ? "positive" : "negate"); continue; }
                    if (token == "(") { operators.Push(Take()); depth++; continue; }
                    if (char.IsAsciiDigit(token[0]) || token[0] == '.') output.Add(new Instruction("number", CspNumber.Parse(Take())));
                    else if (indices.TryGetValue(token, out int index)) { Take(); variables.Add(index); output.Add(new Instruction("variable", Variable: index)); } else throw new FormatException($"Expected a number or known variable, found '{token}'.");
                    operand = false;
                } else if (token == ")" && depth > 0) {
                    // Emit the entire grouped operand before discarding its opening mark.
                    // Unary operators outside the group remain pending until appropriate.
                    while (operators.Peek() != "(") { checkTime?.Invoke(); output.Add(new Instruction(operators.Pop())); }
                    operators.Pop(); depth--; Take();
                } else if (token is "+" or "-" or "*" or "/") {
                    // Emit higher-precedence operators first. Emitting equal precedence
                    // here enforces left associativity, e.g. a/b/c means (a/b)/c.
                    while (operators.Count > 0 && operators.Peek() != "(" && Precedence(operators.Peek()) >= Precedence(token)) {
                        checkTime?.Invoke(); output.Add(new Instruction(operators.Pop()));
                    }
                    operators.Push(Take()); operand = true;
                } else break;
            }
            // Missing operands and unmatched groups are syntax errors, rather than
            // stack underflows or partial programs that fail only during evaluation.
            if (operand || depth != 0) throw new FormatException("Incomplete arithmetic or unmatched parentheses.");
            while (operators.Count > 0) { checkTime?.Invoke(); output.Add(new Instruction(operators.Pop())); }
            return new Arithmetic(output.ToArray());
        }
        // Unary signs bind before products/quotients, which bind before sums/differences.
        private static int Precedence(string op) => op is "positive" or "negate" ? 3 : op is "*" or "/" ? 2 : 1;
    }
}
