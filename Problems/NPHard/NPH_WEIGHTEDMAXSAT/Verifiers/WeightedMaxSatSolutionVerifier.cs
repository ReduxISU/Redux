using API.Interfaces;
using SPADE;

namespace API.Problems.NPHard.NPH_WEIGHTEDMAXSAT.Verifiers;

class WeightedMaxSatSolutionVerifier : IVerifier<WEIGHTEDMAXSAT> {
    internal const string CertificateGrammar = "{(A,V) | A is list, V is int}";

    // --- Fields ---
    public string verifierName { get; } = "Weighted Max-SAT Soluton Verifier";
    public string verifierDefinition { get; } = "Checks that a certificate assigns every problem variable exactly once, contains no unknown variables, and reports the resulting satisfied clause weight. It does not check optimality.";
    public string source { get; } = "Direct certificate evaluation.";
    public string sourceFile { get; } = SourceFile.Path();
    public string certificate { get; } = "((x1:False,x2:True,x3:True,x4:False),3)";
    public string[] contributors { get; } = { "Corbin Hay" };

    // --- Methods Including Constructors ---
    public WeightedMaxSatSolutionVerifier() { }

    public bool verify(WEIGHTEDMAXSAT problem, string certificateText) {
        var parsed = ParseCertificate(problem, certificateText);
        var expected = new HashSet<string>(problem.variables, StringComparer.Ordinal);
        if (!expected.SetEquals(parsed.Assignment.Keys)) return false;
        return problem.Value(parsed.Assignment) == parsed.Value;
    }

    internal static (IReadOnlyDictionary<string, bool> Assignment, long Value) ParseCertificate(
        WEIGHTEDMAXSAT problem, string text) {
        if (string.IsNullOrWhiteSpace(text))
            throw new CertificateParseException(problem, text, "certificate is empty");

        List<string> entries;
        string valueText;
        try {
            // SPADE 0.1.2 does not enforce tuple arity in StringParser grammars, so
            // validate the outer pair with SPADE's collection representation first.
            if (new UtilCollection(text).ToList().Count != 2)
                throw new FormatException("certificate must contain exactly an assignment list and an objective value");
            StringParser parser = new(CertificateGrammar);
            parser.parse(text);
            // A SPADE list preserves duplicate entries, allowing explicit rejection below.
            entries = parser["A"].ToList().Select(entry => entry.ToString()).ToList();
            valueText = parser["V"].ToString();
        } catch (Exception ex) {
            throw new CertificateParseException(problem, text, ex.Message);
        }

        if (!long.TryParse(valueText, out long value) || value < 0)
            throw new CertificateParseException(problem, text, $"objective '{valueText}' must be a nonnegative integer");

        var assignment = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (string entry in entries) {
            string[] pair = entry.Split(':');
            if (pair.Length != 2 || string.IsNullOrWhiteSpace(pair[0]))
                throw new CertificateParseException(problem, text, $"assignment '{entry}' must have variable:value form");
            string variable = pair[0].Trim();
            bool boolean = pair[1].Trim() switch {
                "True" or "T" => true,
                "False" or "F" => false,
                _ => throw new CertificateParseException(problem, text, $"assignment '{entry}' must use True/False or T/F"),
            };
            if (!assignment.TryAdd(variable, boolean))
                throw new CertificateParseException(problem, text, $"variable '{variable}' is assigned more than once");
        }

        return (assignment, value);
    }
}
