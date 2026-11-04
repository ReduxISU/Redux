using System.Globalization;
using System.Text.RegularExpressions;
using API.Interfaces;

namespace API.Problems.NPComplete.NPC_CONSTRAINTSATISFACTION.Verifiers;

class ConstraintSatisfactionVerifier : IVerifier<CONSTRAINTSATISFACTION> {
    public string verifierName => "General Finite-Domain Constraint Satisfaction Problem Solution Verifier";
    public string verifierDefinition => "Checks a complete domain-respecting assignment against every supported integer comparison in polynomial time.";
    public string source => "Direct certificate verification.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors => Array.Empty<string>();
    public string certificate { get; private set; } = "";

    public bool verify(CONSTRAINTSATISFACTION problem, string certificate) {
        this.certificate = certificate;
        var model = new ConstraintSatisfactionModel(problem);
        if (string.IsNullOrWhiteSpace(certificate))
            throw new CertificateParseException(problem, certificate, "Certificate is empty.");
        string text = certificate.Trim();
        if (text.Length < 2 || text[0] != '{' || text[^1] != '}')
            throw new CertificateParseException(problem, certificate, "Expected a set of (variable,value) pairs.");
        string body = text[1..^1];
        var pairs = new List<(string Name, int Value)>();
        int position = 0;
        while (position < body.Length && !string.IsNullOrWhiteSpace(body[position..])) {
            Match pair = Regex.Match(body[position..], @"\A\s*\(\s*([A-Za-z_][A-Za-z0-9_]*)\s*,\s*([+-]?[0-9]+)\s*\)\s*");
            if (!pair.Success || !int.TryParse(pair.Groups[2].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value))
                throw new CertificateParseException(problem, certificate, "Expected an identifier and a 32-bit integer in each pair.");
            pairs.Add((pair.Groups[1].Value, value));
            position += pair.Length;
            if (position == body.Length) break;
            if (body[position++] != ',' || string.IsNullOrWhiteSpace(body[position..]))
                throw new CertificateParseException(problem, certificate, "Expected another pair after a comma.");
        }
        var indices = problem.X.Select((name, index) => (name, index)).ToDictionary(p => p.name, p => p.index, StringComparer.Ordinal);
        var values = new int?[problem.X.Count];
        foreach (var (name, value) in pairs) {
            if (!indices.TryGetValue(name, out int index) || values[index].HasValue || !problem.D[index].Contains(value)) return false;
            values[index] = value;
        }
        return values.All(v => v.HasValue) && model.Consistent(values);
    }
}
