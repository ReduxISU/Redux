using API.Interfaces;
using API.Interfaces.Logic;
using API.Problems.NPHard.NPH_WEIGHTEDMAXSAT.Solvers;
using API.Problems.NPHard.NPH_WEIGHTEDMAXSAT.Verifiers;
using API.Problems.NPHard.NPH_WEIGHTEDMAXSAT.Visualizations;
using SPADE;

namespace API.Problems.NPHard.NPH_WEIGHTEDMAXSAT;

class WEIGHTEDMAXSAT : IProblem<WeightedMaxSatBruteForceSolver, WeightedMaxSatSolutionVerifier, WeightedMaxSatVisualization> {
    // --- Fields ---
    public string problemName { get; } = "Weighted Max-SAT";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Maximum_satisfiability_problem";
    public string formalDefinition { get; } = "Given weighted CNF clauses (C_i, w_i), find an assignment A maximizing sum of w_i over the clauses satisfied by A.";
    public string problemDefinition { get; } = "Weighted Max-SAT assigns a nonnegative integer weight to every nonempty CNF clause and asks for a truth assignment maximizing the total weight of satisfied clauses. Unit weights recover ordinary Max-SAT.";
    public string inputDescription { get; } = "A conjunction of nonempty CNF clauses, followed by a list of respective weights";
    public string outputDescription { get; } = "A complete truth assignment and the total weight of the clauses it satisfies";
    public string source { get; } = "Vijay V. Vazirani, Approximation Algorithms, Chapter 16: Maximum Satisfiability, Springer, 2001.";
    public string sourceFile { get; } = SourceFile.Path();
    public string wikiName { get; } = "Maximum_satisfiability_problem";
    public string[] contributors { get; } = { "Corbin Hay" };

    // Descriptive metadata notation; this is not passed directly to SPADE.
    public const string InstanceGrammar = "{(Φ,W) | Φ is a parenthesized CNF formula, W is list}";
    public static string _defaultInstance { get; } = "(((!x1 | x2) & (x3 | x4)),(1,2))";
    public string defaultInstance { get; } = _defaultInstance;
    public string instance { get; }
    public string instanceFormat { get; } = $"Format: {InstanceGrammar}. Φ uses SAT CNF syntax and W has one nonnegative integer per clause. Example: {_defaultInstance}";
    public string certificateFormat { get; } = "A tuple containing a complete comma-separated assignment and its satisfied weight. Booleans are True/False (T/F also accepted). Example: ((x1:False,x2:True,x3:True,x4:False),3)";
    public ComplexityClass complexityClass { get; } = ComplexityClass.NPHard;
    public ProblemType problemType { get; } = ProblemType.Logic;

    public WeightedMaxSatBruteForceSolver defaultSolver { get; } = new();
    public WeightedMaxSatSolutionVerifier defaultVerifier { get; } = new();
    public WeightedMaxSatVisualization defaultVisualization { get; } = new();

    internal IReadOnlyList<WeightedCnfClause> weightedClauses { get; }
    internal IReadOnlyList<string> variables { get; }
    internal CnfFormula formula { get; }

    // --- Methods Including Constructors ---
    public WEIGHTEDMAXSAT() : this(_defaultInstance) { }

    public WEIGHTEDMAXSAT(string input) {
        if (string.IsNullOrWhiteSpace(input))
            throw new ProblemParseException(problemName, input, "instance is empty");

        try {
            (string formulaText, string weightsText) = ParseOuterTuple(input);
            formula = CnfParser.Parse(RemoveFormulaWrapper(formulaText));
            IReadOnlyList<long> weights = ParseWeights(weightsText);
            if (weights.Count != formula.Clauses.Count)
                throw new FormatException(
                    $"weight count (currently: {weights.Count}) must equal clause count (currently: {formula.Clauses.Count})");
            _ = weights.Aggregate(0L, (sum, weight) => checked(sum + weight));
            weightedClauses = formula.Clauses.Zip(weights,
                (clause, weight) => new WeightedCnfClause(weight, clause)).ToList();
            variables = formula.Variables;
            instance = input;
        } catch (ProblemParseException) {
            throw;
        } catch (Exception e) {
            throw new ProblemParseException(problemName, input, e.Message, e);
        }
    }

    private static (string Formula, string Weights) ParseOuterTuple(string input) {
        string trimmed = input.Trim();
        if (trimmed.Length < 2 || trimmed[0] != '(' || trimmed[^1] != ')')
            throw new FormatException("instance must be an enclosing (Φ,W) tuple");

        int depth = 0;
        int separator = -1;
        for (int i = 0; i < trimmed.Length; i++) {
            if (trimmed[i] == '(') {
                depth++;
            } else if (trimmed[i] == ')') {
                depth--;
                if (depth < 0 || (depth == 0 && i != trimmed.Length - 1))
                    throw new FormatException("instance parentheses are not balanced");
            } else if (trimmed[i] == ',' && depth == 1) {
                if (separator >= 0)
                    throw new FormatException("instance must contain exactly two components: Φ and W");
                separator = i;
            }
        }

        if (depth != 0)
            throw new FormatException("instance parentheses are not balanced");
        if (separator < 0)
            throw new FormatException("instance must contain exactly two components: Φ and W");

        string formulaText = trimmed[1..separator].Trim();
        string weightsText = trimmed[(separator + 1)..^1].Trim();
        if (formulaText.Length == 0 || weightsText.Length == 0)
            throw new FormatException("Φ and W must both be nonempty");
        return (formulaText, weightsText);
    }

    private static string RemoveFormulaWrapper(string formulaText) {
        if (!HasSingleEnclosingPair(formulaText))
            throw new FormatException("Φ must have exactly one balanced enclosing wrapper");
        string unwrapped = formulaText[1..^1];
        if (string.IsNullOrWhiteSpace(unwrapped))
            throw new FormatException("Φ must be nonempty");
        return unwrapped;
    }

    private static IReadOnlyList<long> ParseWeights(string weightsText) {
        if (!HasSingleEnclosingPair(weightsText))
            throw new FormatException("W must be an ordered parenthesized list");

        StringParser parser = new("{W | W is list}");
        parser.parse(weightsText);
        List<string> entries = parser["W"].ToList().Select(entry => entry.ToString()).ToList();
        if (entries.Count == 0)
            throw new FormatException("W must contain at least one weight");

        var weights = new List<long>();
        foreach (string entry in entries) {
            if (!long.TryParse(entry, out long weight) || weight < 0)
                throw new FormatException($"weight '{entry}' must be a nonnegative integer");
            weights.Add(weight);
        }
        return weights;
    }

    private static bool HasSingleEnclosingPair(string text) {
        if (text.Length < 2 || text[0] != '(' || text[^1] != ')') return false;
        int depth = 0;
        for (int i = 0; i < text.Length; i++) {
            if (text[i] == '(') depth++;
            else if (text[i] == ')') {
                depth--;
                if (depth < 0 || (depth == 0 && i != text.Length - 1)) return false;
            }
        }
        return depth == 0;
    }

    internal long Value(IReadOnlyDictionary<string, bool> assignment) {
        long value = 0;
        foreach (WeightedCnfClause clause in weightedClauses) {
            if (clause.Clause.Literals.Any(l => assignment[l.Variable] != l.Negated))
                value = checked(value + clause.Weight);
        }
        return value;
    }
}

internal sealed record WeightedCnfClause(long Weight, CnfClause Clause);

internal static class WeightedMaxSatExactSearchLimit {
    public const int MaxVariables = 20;

    public static void ThrowIfExceeded(int variableCount) {
        if (variableCount > MaxVariables)
            throw new WeightedMaxSatExactSearchLimitExceededException(variableCount, MaxVariables);
    }
}

internal sealed class WeightedMaxSatExactSearchLimitExceededException : Exception {
    public int VariableCount { get; }
    public int MaxVariables { get; }

    public WeightedMaxSatExactSearchLimitExceededException(int variableCount, int maxVariables)
        : base($"Weighted Max-SAT exact search supports at most {maxVariables} variables; received {variableCount}.") {
        VariableCount = variableCount;
        MaxVariables = maxVariables;
    }
}
