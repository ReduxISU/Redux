using System.Text.RegularExpressions;
using API.Interfaces;

namespace API.Problems.NPComplete.NPC_CONSTRAINTSATISFACTION.Verifiers;

// Independently checks a complete assignment against the constraints.
// Returns false if a valid assignment violates a constraint, and true if all constraints are satisfied.
// Invalid input, domain violations, and evaluation errors throw exceptions.
class ConstraintSatisfactionVerifier : IVerifier<CONSTRAINTSATISFACTION> {
    public string verifierName => "General Finite-Domain Constraint Satisfaction Problem Solution Verifier";
    public string verifierDefinition => "Validates a complete domain-respecting decimal assignment, then evaluates every constraint. Invalid certificates throw; false means an unsatisfied constraint.";
    public string source => "Direct certificate verification.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors => Array.Empty<string>();
    public string certificate { get; private set; } = "";

    // Accepts a parenthesized identifier:number list in arbitrary variable order.
    // Every declared variable must occur exactly once and have a domain member value.
    // <exception cref="CertificateParseException">The certificate is invalid or incomplete.</exception>
    // <exception cref="ProblemParseException">A problem constraint cannot be prepared.</exception>
    public bool verify(CONSTRAINTSATISFACTION problem, string certificate) {
        ArgumentNullException.ThrowIfNull(problem);
        this.certificate = certificate;
        if (string.IsNullOrWhiteSpace(certificate)) throw Invalid("Certificate is empty.");
        // Validate the outer envelope before splitting entries. Solver exhaustion's
        // {} sentinel is intentionally rejected; () is valid only for an empty problem.
        string text = certificate.Trim();
        if (text.Length < 2 || text[0] != '(' || text[^1] != ')')
            throw Invalid("Expected a parenthesized assignment list, e.g. (x:1, y:2).");
        // Resolve names using the problem's exact case-sensitive ordering. Nullable
        // slots distinguish a missing assignment from a supplied numerical zero.
        var indices = problem.X.Select((name, index) => (name, index))
            .ToDictionary(p => p.name, p => p.index, StringComparer.Ordinal);
        var values = new decimal?[problem.X.Count];
        string body = text[1..^1];
        // Keep empty entries: a leading, trailing or repeated comma is an error.
        if (!string.IsNullOrWhiteSpace(body)) foreach (string entry in body.Split(',')) {
            // Anchor the entire entry: accepting a matching prefix would hide extra
            // colons, punctuation, whitespace within a number, or other unexpected data.
            Match pair = Regex.Match(entry, @"\A\s*([A-Za-z_][A-Za-z0-9_]*)\s*:\s*(" + CspNumber.Pattern + @")\s*\z");
            if (!pair.Success) throw Invalid("Expected identifier:number in every entry.");
            string name = pair.Groups[1].Value;
            if (!indices.TryGetValue(name, out int index)) throw Invalid($"Unknown variable '{name}'.");
            if (values[index].HasValue) throw Invalid($"Variable '{name}' is assigned more than once.");
            // Translate numeric format errors into certificate errors with the original
            // cause attached. Do not catch constraint evaluation failures below.
            decimal value;
            try { value = CspNumber.Parse(pair.Groups[2].Value); } catch (FormatException ex) { throw new CertificateParseException(problem, certificate, ex.Message, ex); }
            // Membership uses decimal equality, so alternate scales such as 1 and 1.0
            // denote the same domain value; the assignment is stored only after validation.
            if (!problem.D[index].Contains(value)) throw Invalid($"Value is outside the domain of '{name}'.");
            values[index] = value;
        }
        // Completeness is checked before model evaluation: a deferred constraint must
        // never let an incomplete certificate appear to satisfy the problem.
        if (values.Any(value => !value.HasValue)) throw Invalid("Every variable must be assigned exactly once.");
        // Independent parsing/evaluation: no trust in solver output or state.
        return new ConstraintSatisfactionModel(problem).Consistent(values);

        // Keep error construction consistent while retaining the supplied certificate.
        CertificateParseException Invalid(string detail) => new(problem, certificate, detail);
    }
}
