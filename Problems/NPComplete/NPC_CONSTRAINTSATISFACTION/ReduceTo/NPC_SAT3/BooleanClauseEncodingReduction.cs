using System.Text.RegularExpressions;
using API.Interfaces;
using API.Problems.NPComplete.NPC_SAT3;

namespace API.Problems.NPComplete.NPC_CONSTRAINTSATISFACTION.ReduceTo.NPC_SAT3;

// Stored under the target as requested by the course assignment. The interface,
// rather than the folder name, determines the SAT3 -> CSP navigation edge.
class BooleanClauseEncodingReduction : IReduction<SAT3, CONSTRAINTSATISFACTION> {
    public string reductionName => "Boolean Clause Encoding Reduction";
    public string reductionDefinition => "Reduces 3SAT to finite-domain CSP using Boolean domains and one literal-sum inequality per clause.";
    public string source => "Standard Boolean 0/1 clause encoding; construction and proof in ReductionReport.tex alongside this file.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors => Array.Empty<string>();
    public ReductionCost cost => ReductionCost.Linear;
    public ReductionType reductionType => ReductionType.Restriction;
    public ReductionComplexityBucket complexityBucket => ReductionComplexityBucket.Polynomial;
    public string complexity => "O(N^2) conservative worst-case time including parsing and hash collisions; O(N) output and space, N = input text length.";

    public SAT3 reductionFrom { get; }
    public CONSTRAINTSATISFACTION reductionTo { get; private set; }

    public BooleanClauseEncodingReduction() : this(new SAT3()) { }
    public BooleanClauseEncodingReduction(string instance) : this(new SAT3(instance)) { }
    public BooleanClauseEncodingReduction(SAT3 from) {
        ArgumentNullException.ThrowIfNull(from);
        reductionFrom = from;
        reductionTo = reduce();
    }

    public CONSTRAINTSATISFACTION reduce() {
        // Reparse the authoritative text: SAT3 has mutable instance/clauses/literals
        // properties, so its cached formula and legacy lists can become stale.
        var formula = new SAT3(reductionFrom.instance).formula;
        string variables = string.Join(",", formula.Variables);
        string domains = string.Join(",", formula.Variables.Select(_ => "{0,1}"));
        var constraints = formula.Clauses.Select(clause => {
            // Each term is exactly the truth value of its literal on {0,1}.
            // Parentheses around 1-x preserve addition/subtraction precedence.
            string sum = string.Join("+", clause.Literals.Select(literal =>
                literal.Negated ? $"(1-{literal.Variable})" : literal.Variable));
            return $"\"{sum}>=1\"";
        });
        // The target parser may deduplicate identical constraints. Conjunction
        // is idempotent, so this preserves the satisfying assignments.
        reductionTo = new CONSTRAINTSATISFACTION($"(({variables}),({domains}),{{{string.Join(",", constraints)}}})");
        return reductionTo;
    }

    // Redux's required forward map: a complete satisfying Boolean assignment
    // becomes a complete numeric CSP assignment, with identical variable names.
    public string mapSolutions(string problemFromSolution) {
        if (string.IsNullOrWhiteSpace(problemFromSolution)) throw Invalid("Certificate is empty.");
        string text = problemFromSolution.Trim();
        if (text.StartsWith('(') && text.EndsWith(')')) text = text[1..^1];
        var assignment = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (string entry in text.Split(',')) {
            var pair = Regex.Match(entry, @"\A\s*([A-Za-z_][A-Za-z0-9_]*)\s*[:=]\s*(True|False|T|F)\s*\z");
            if (!pair.Success) throw Invalid("Expected variable:True/False pairs.");
            if (!assignment.TryAdd(pair.Groups[1].Value, pair.Groups[2].Value is "True" or "T"))
                throw Invalid("A variable is assigned more than once.");
        }
        var formula = new SAT3(reductionFrom.instance).formula;
        if (assignment.Count != formula.Variables.Count || formula.Variables.Any(v => !assignment.ContainsKey(v)))
            throw Invalid("Assign every source variable exactly once and no unknown variables.");
        if (!formula.Evaluate(assignment)) throw Invalid("Assignment does not satisfy the source formula.");
        return "(" + string.Join(",", formula.Variables.Select(v => $"{v}:{(assignment[v] ? "1" : "0")}")) + ")";

        CertificateParseException Invalid(string detail) => new(reductionFrom, problemFromSolution, detail);
    }
}
