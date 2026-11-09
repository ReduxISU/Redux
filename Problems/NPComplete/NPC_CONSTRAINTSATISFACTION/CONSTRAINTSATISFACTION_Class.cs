using System.Text.RegularExpressions;
using API.Interfaces;
using API.Problems.NPComplete.NPC_CONSTRAINTSATISFACTION.Solvers;
using API.Problems.NPComplete.NPC_CONSTRAINTSATISFACTION.Verifiers;
using API.Problems.NPComplete.NPC_CONSTRAINTSATISFACTION.Visualizations;

namespace API.Problems.NPComplete.NPC_CONSTRAINTSATISFACTION;

// Problem class for the General Finite-Domain Constraint Satisfaction Problem.
// Parses and validates X/D/C collections while leaving constraint text to the solver/verifier.
// Variable and domain positions remain aligned throughout solving and verification.
class CONSTRAINTSATISFACTION : IProblem<ConstraintSatisfactionIntelligent, ConstraintSatisfactionVerifier, ConstraintSatisfactionVisualization> {
    public string problemName { get; } = "General Finite-Domain Constraint Satisfaction Problem";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Constraint_satisfaction_problem";
    public string formalDefinition { get; } = "CONSTRAINTSATISFACTION = {<X,D,C> | there exists an assignment a with a(X[i]) in D[i] for every i, satisfying every constraint in C}";
    public string problemDefinition { get; } = "Given distinct variables, an explicitly listed finite nonempty numeric domain for each variable, and constraints, determine whether values can be assigned to all variables so that every constraint is satisfied.";
    public string inputDescription { get; } = "X, an ordered list of distinct variables; D, their corresponding finite numeric domains; and C, a set of quoted constraint expressions";
    public string outputDescription { get; } = "True or False, whether a complete assignment from the specified domains satisfies all constraints";
    public string source { get; } = "U. Montanari, “Networks of Constraints: Fundamental Properties and Applications to Picture Processing,” Information Sciences, vol. 7, pp. 95–132, Jan. 1974.";
    public string sourceLink { get; } = "https://ics.uci.edu/~dechter/courses/ics-276/spring-19/reading/montanari.pdf";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Reece Foulkrod", "Gabriel Ory", "Ziming Wang" };

    private const string DefaultInstance = "((x,y),({1,2},{1,2}),{\"x!=y\"})";
    public string defaultInstance { get; } = DefaultInstance;
    public string instanceFormat { get; } = "Format: (X,D,C). X is a parenthesized list of distinct identifiers; D is a parenthesized list of nonempty sets of exactly representable System.Decimal values, one per variable in the same order; C is a set of double-quoted constraint expressions. Identifiers use letters, digits and underscores, starting with a letter or underscore. Example: " + DefaultInstance;
    public string certificateFormat { get; } = "A parenthesized list assigning every variable exactly once from its domain. Example: (x:1, y:2)";
    public string instance { get; set; } = string.Empty;
    public string wikiName { get; } = "Constraint_satisfaction_problem";
    public ConstraintSatisfactionIntelligent defaultSolver { get; } = new ConstraintSatisfactionIntelligent();
    public ConstraintSatisfactionVerifier defaultVerifier { get; } = new ConstraintSatisfactionVerifier();
    public ConstraintSatisfactionVisualization defaultVisualization { get; } = new ConstraintSatisfactionVisualization();
    public ComplexityClass complexityClass { get; } = ComplexityClass.NPComplete;
    public ProblemVariant problemVariant { get; } = ProblemVariant.Decision;
    public ProblemType problemType { get; } = ProblemType.MathematicalProgramming;

    // Distinct case-sensitive identifiers, retained in input order.
    public IReadOnlyList<string> X { get; }
    // Nonempty finite domains; D[i] belongs to X[i]. Values retain input order.
    public IReadOnlyList<IReadOnlyList<decimal>> D { get; }
    // Unquoted constraint text, deduplicated in order of first occurrence.
    public IReadOnlyList<string> C { get; }

    public CONSTRAINTSATISFACTION() : this(DefaultInstance) { }

    // Validates collection structure, identifiers, domain values, and quoted constraints. 
    // Prepare the expression during each solve/verify call to support cancellation and preserve the original text.
    public CONSTRAINTSATISFACTION(string stringInstance) {
        if (string.IsNullOrWhiteSpace(stringInstance))
            throw new ProblemParseException(problemName, stringInstance, "instance is empty");

        try {
            // Split only at the current collection level; nested domains and quoted
            // commas in constraints must not become extra X/D/C sections.
            var sections = SplitCollection(stringInstance, '(', ')');
            if (sections.Count != 3)
                throw new FormatException("Instance must contain exactly three sections: (X,D,C).");

            // Validate names before building indices elsewhere. Ordinal distinctness
            // keeps x and X separate while rejecting repeated spellings.
            var variables = SplitCollection(sections[0], '(', ')');
            if (variables.Any(v => !Regex.IsMatch(v, @"\A[A-Za-z_][A-Za-z0-9_]*\z")))
                throw new FormatException("Each variable must be an identifier.");
            if (variables.Distinct(StringComparer.Ordinal).Count() != variables.Count)
                throw new FormatException("Variables must be distinct.");

            // Enforce one nonempty domain per variable. A set detects numeric duplicates
            // (including 1 and 1.0); a list independently preserves enumeration order.
            var domains = SplitCollection(sections[1], '(', ')');
            if (domains.Count != variables.Count)
                throw new FormatException("There must be exactly one domain per variable.");
            var parsedDomains = new List<IReadOnlyList<decimal>>();
            foreach (string domain in domains) {
                var values = SplitCollection(domain, '{', '}');
                if (values.Count == 0)
                    throw new FormatException("Variable domains must be nonempty.");
                var seenNumbers = new HashSet<decimal>();
                var numbers = new List<decimal>();
                foreach (string value in values) {
                    decimal number = CspNumber.Parse(value);
                    if (!seenNumbers.Add(number))
                        throw new FormatException("Domain values must be distinct.");
                    numbers.Add(number);
                }
                parsedDomains.Add(numbers.AsReadOnly());
            }

            // Only the quoting envelope is handled here; arithmetic and membership
            // syntax are validated later by the model. Embedded/escaped quotes are unsupported.
            var constraints = new List<string>();
            foreach (string constraint in SplitCollection(sections[2], '{', '}')) {
                if (constraint.Length < 3 || constraint[0] != '"' || constraint[^1] != '"' ||
                    constraint[1..^1].Contains('"') || string.IsNullOrWhiteSpace(constraint[1..^1]))
                    throw new FormatException("Each constraint must be a nonempty double-quoted expression without embedded quotes.");
                constraints.Add(constraint[1..^1]);
            }

            // Preserve domain positions and constraint text for consumers and serialization.
            // Expression evaluation belongs to the solver/verifier.
            X = variables.AsReadOnly();
            D = parsedDomains.AsReadOnly();
            C = constraints.Distinct(StringComparer.Ordinal).ToList().AsReadOnly();
            instance = stringInstance;
        } catch (FormatException ex) {
            // Preserve the failed instance and cause for Redux callers. Do not broadly
            // catch unrelated runtime failures and mislabel them as invalid input.
            throw new ProblemParseException(problemName, stringInstance, ex.Message, ex);
        }
    }

    // Split top-level commas while preserving nested collections and quoted expressions.
    // Extracts comma-separated elements from a collection.
    // Nested collections are handled using a delimiter stack, while quoted expressions are left for the expression parser to validate.
    private static List<string> SplitCollection(string text, char open, char close) {
        text = text.Trim();
        if (text.Length < 2 || text[0] != open || text[^1] != close)
            throw new FormatException($"Expected a collection enclosed in '{open}' and '{close}'.");

        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text[1..^1]))
            return result;

        // Store expected closing characters rather than merely counting depth, so
        // crossing or mismatched delimiters cannot pass as balanced input.
        var brackets = new Stack<char>();
        bool quoted = false;
        int start = 1;
        for (int i = 1; i < text.Length - 1; i++) {
            char current = text[i];
            if (current == '"') {
                quoted = !quoted;
            } else if (!quoted) {
                if (current is '(' or '{') {
                    brackets.Push(current == '(' ? ')' : '}');
                } else if (current is ')' or '}') {
                    if (brackets.Count == 0 || brackets.Pop() != current)
                        throw new FormatException("Mismatched collection delimiters.");
                } else if (current == ',' && brackets.Count == 0) {
                    // Keep empty elements until the final check; dropping them would
                    // silently accept leading, trailing, or doubled commas.
                    result.Add(text[start..i].Trim());
                    start = i + 1;
                }
            }
        }
        if (quoted || brackets.Count != 0)
            throw new FormatException("Unclosed quote or collection delimiter.");
        result.Add(text[start..^1].Trim());
        if (result.Any(string.IsNullOrEmpty))
            throw new FormatException("Collection elements cannot be empty.");
        return result;
    }
}
