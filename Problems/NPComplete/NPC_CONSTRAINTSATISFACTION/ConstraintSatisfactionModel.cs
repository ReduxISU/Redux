using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using API.Interfaces;

namespace API.Problems.NPComplete.NPC_CONSTRAINTSATISFACTION;

// Shared restricted expression language; see README.md in this folder.
internal sealed class ConstraintSatisfactionModel {
    internal sealed record Constraint(Dictionary<int, BigInteger> Coefficients, BigInteger Constant, string Operator) {
        internal bool Holds(int?[] values, Action? checkTime) {
            BigInteger difference = Constant;
            foreach (var (variable, coefficient) in Coefficients) {
                checkTime?.Invoke();
                if (values[variable] is not int value) return true; // Not yet decidable.
                difference += coefficient * value;
            }
            return Operator switch {
                "=" or "==" => difference == 0,
                "!=" => difference != 0,
                "<" => difference < 0,
                "<=" => difference <= 0,
                ">" => difference > 0,
                ">=" => difference >= 0,
                _ => throw new InvalidOperationException("Uncompiled comparison.")
            };
        }
    }

    private readonly List<Constraint> constraints = new();
    internal ConstraintSatisfactionModel(CONSTRAINTSATISFACTION problem, Action? checkTime = null) {
        var indices = problem.X.Select((name, index) => (name, index))
            .ToDictionary(pair => pair.name, pair => pair.index, StringComparer.Ordinal);
        foreach (string expression in problem.C) {
            checkTime?.Invoke();
            try {
                var comparisons = Regex.Matches(expression, @"[=!<>]+");
                if (comparisons.Count != 1 || comparisons[0].Value is not ("=" or "==" or "!=" or "<" or "<=" or ">" or ">="))
                    throw new FormatException("Expected exactly one comparison: =, ==, !=, <, <=, >, or >=.");
                Match comparison = comparisons[0];
                var coefficients = new Dictionary<int, BigInteger>();
                BigInteger constant = 0;
                ParseSum(expression[..comparison.Index], 1);
                ParseSum(expression[(comparison.Index + comparison.Length)..], -1);
                foreach (int index in coefficients.Where(p => p.Value.IsZero).Select(p => p.Key).ToArray())
                    coefficients.Remove(index);
                constraints.Add(new Constraint(coefficients, constant, comparison.Value));

                // Flatten sums to coefficients, using an explicit stack for parentheses.
                void ParseSum(string text, int side) {
                    var contexts = new Stack<int>();
                    int context = side, sign = 1, position = 0;
                    bool expectOperand = true;
                    while (position < text.Length) {
                        checkTime?.Invoke();
                        char token = text[position];
                        if (char.IsWhiteSpace(token)) { position++; continue; }
                        if (token is '+' or '-') {
                            if (!expectOperand) { expectOperand = true; sign = 1; }
                            if (token == '-') sign = -sign;
                            position++;
                        } else if (token == '(' && expectOperand) {
                            contexts.Push(context);
                            context *= sign;
                            sign = 1;
                            position++;
                        } else if (token == ')' && !expectOperand && contexts.Count > 0) {
                            context = contexts.Pop();
                            position++;
                        } else if (expectOperand && (char.IsAsciiDigit(token) || char.IsAsciiLetter(token) || token == '_')) {
                            int start = position++;
                            if (char.IsAsciiDigit(token)) {
                                while (position < text.Length && char.IsAsciiDigit(text[position])) position++;
                                constant += context * sign * BigInteger.Parse(text[start..position], CultureInfo.InvariantCulture);
                            } else {
                                while (position < text.Length && (char.IsAsciiLetterOrDigit(text[position]) || text[position] == '_')) position++;
                                string name = text[start..position];
                                if (!indices.TryGetValue(name, out int index)) throw new FormatException($"Unknown variable '{name}'.");
                                coefficients[index] = coefficients.GetValueOrDefault(index) + context * sign;
                            }
                            sign = 1;
                            expectOperand = false;
                        } else {
                            throw new FormatException($"Unsupported or misplaced token '{token}'. Only integer sums, differences, unary signs and parentheses are supported.");
                        }
                    }
                    if (expectOperand || contexts.Count != 0) throw new FormatException("Incomplete expression or unmatched parentheses.");
                }
            } catch (FormatException ex) {
                throw new ProblemParseException(problem.problemName, problem.instance, $"Invalid constraint '{expression}': {ex.Message}", ex);
            }
        }
    }

    internal bool Consistent(int?[] values, Action? checkTime = null) {
        foreach (Constraint constraint in constraints) {
            checkTime?.Invoke();
            if (!constraint.Holds(values, checkTime)) return false;
        }
        return true;
    }

    internal static string Certificate(CONSTRAINTSATISFACTION problem, int?[] values) =>
        "{" + string.Join(",", problem.X.Select((name, i) => $"({name},{values[i]!.Value.ToString(CultureInfo.InvariantCulture)})")) + "}";
}
