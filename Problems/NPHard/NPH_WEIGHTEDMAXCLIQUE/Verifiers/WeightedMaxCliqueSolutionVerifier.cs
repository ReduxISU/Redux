using System.Globalization;
using API.Interfaces;
using SPADE;

namespace API.Problems.NPHard.NPH_WEIGHTEDMAXCLIQUE.Verifiers;

[DefaultFor(typeof(WEIGHTEDMAXCLIQUE))]
internal sealed class WeightedMaxCliqueSolutionVerifier : IVerifier<WEIGHTEDMAXCLIQUE> {
    public const string CertificateExample = "({a,b,c},12)";
    public string verifierName { get; } = "Weighted Maximum Clique Solution Verifier";
    public string verifierDefinition { get; } = "Checks vertex membership, uniqueness, pairwise adjacency, and the reported total vertex weight. Accepts feasible suboptimal cliques and does not verify optimality.";
    public string source { get; } = "Direct clique feasibility and objective-value evaluation.";
    public string sourceFile { get; } = SourceFile.Path();
    public string certificate { get; } = CertificateExample;
    public string[] contributors { get; } = { "Corbin Hay" };

    public bool verify(WEIGHTEDMAXCLIQUE problem, string certificateText) {
        var parsed = ParseCertificate(problem, certificateText);
        int[] selected = parsed.Vertices.Select(problem.VertexIndex).ToArray();
        if (selected.Any(vertex => vertex < 0)) return false;
        return problem.IsClique(selected) && problem.Value(selected) == parsed.Value;
    }

    internal static (IReadOnlyList<string> Vertices, long Value) ParseCertificate(
        WEIGHTEDMAXCLIQUE problem, string text) {
        try {
            // SPADE ignores some malformed separators and trailing input. Reuse the
            // instance's delimiter/entry guard before parsing any collections.
            WeightedCliqueInputSyntax.Validate(text);
            string compact = string.Concat(text.Where(c => !char.IsWhiteSpace(c)));
            if (!compact.StartsWith("({", StringComparison.Ordinal))
                throw new FormatException("certificate must be a ({vertices},totalWeight) tuple with a brace-delimited vertex set");

            // Validate the set wrapper above, then parse it as an ordered list so
            // SPADE cannot erase duplicate selected vertices before we reject them.
            StringParser parser = new("{C | C is list}");
            parser.parse(compact.Replace('{', '(').Replace('}', ')'));
            UtilCollection root = parser["C"];
            if (!root.IsOrdered() || root.Count() != 2)
                throw new FormatException("certificate must contain exactly a vertex set and an objective value");
            if (!root[0].IsOrdered())
                throw new FormatException("certificate must contain a vertex set");
            string valueText = root[1].ToString();
            // TryParse accepts trailing NULs even with NumberStyles.None.
            if (!root[1].IsValue() || valueText.Length == 0 || valueText.Any(c => c is < '0' or > '9')
                || !long.TryParse(valueText, NumberStyles.None,
                    CultureInfo.InvariantCulture, out long value))
                throw new FormatException($"objective value must be a nonnegative 64-bit integer no greater than {long.MaxValue}");

            var vertices = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (UtilCollection entry in root[0].ToList()) {
                if (!entry.IsValue() || string.IsNullOrWhiteSpace(entry.ToString()))
                    throw new FormatException("selected vertices must be nonempty scalar identifiers");
                string vertex = entry.ToString();
                if (!seen.Add(vertex))
                    throw new FormatException($"vertex '{vertex}' is selected more than once");
                vertices.Add(vertex);
            }
            return (vertices.AsReadOnly(), value);
        } catch (Exception ex) when (ex is not OutOfMemoryException && ex is not CertificateParseException) {
            throw new CertificateParseException(problem, text, ex.Message, ex);
        }
    }
}
