using System.Text.Json.Serialization;
using API.Interfaces;

namespace API.Problems.NPComplete.NPC_UNRESTRICTEDLONGESTPATH.Verifiers;

class UnrestrictedLongestPathVerifier : IVerifier<UNRESTRICTEDLONGESTPATH> {
    public const string CertificateGrammar =
        "{v1,v2,...,vt} | all vertices are distinct, consecutive vertices share an edge, and t - 1 >= K";
    public const string CertificateExample = "{1,2,3}";

    // --- Fields ---
    public string verifierName { get; } = "Default Unrestricted Longest Path Verifier";
    public string verifierDefinition { get; } =
        "Checks that the certificate lists a simple path in the input graph with at least K edges.";
    public string source { get; } =
        "Monien, B. (1985). How to find long paths efficiently. North-Holland Mathematics Studies, 239–254.";
    public string sourceLink { get; } = "https://doi.org/10.1016/s0304-0208(08)73110-4";
    public string[] contributors { get; } = { "Alice Giola", "Sadman Hasan", "Javier Rodriguez Lopez" };
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? complexity { get; set; } = "O(n + m + p), n = |nodes|, m = |edges|, p = certificate path length";
    private string _certificate = "";

    public string certificate {
        get {
            return _certificate;
        }
    }

    public string sourceFile { get; } = SourceFile.Path();

    // --- Methods Including Constructors ---
    public UnrestrictedLongestPathVerifier() { }

    public bool verify(UNRESTRICTEDLONGESTPATH problem, string certificate) {
        _certificate = certificate;
        if (string.IsNullOrWhiteSpace(certificate))
            throw new CertificateParseException(problem, certificate, "certificate is empty");

        string trimmed = certificate.Trim();
        if (trimmed.Length < 2 || trimmed[0] != '{' || trimmed[^1] != '}')
            throw new CertificateParseException(problem, certificate, "expected a brace-delimited vertex sequence");

        string contents = trimmed[1..^1].Trim();
        var path = new List<string>();
        if (contents.Length > 0) {
            string[] vertices = contents.Split(',');
            if (vertices.Any(vertex =>
                string.IsNullOrWhiteSpace(vertex)
                || vertex.Contains('{')
                || vertex.Contains('}')
            )) {
                throw new CertificateParseException(problem, certificate, "certificate contains an invalid vertex name");
            }
            path.AddRange(vertices.Select(vertex => vertex.Trim()));
        }

        // A path is an ordered sequence; unlike a set certificate, repeated vertices
        // invalidate it, and its edge count is one less than its vertex count.
        if (path.Count == 0 || path.Count - 1 < problem.K)
            return false;

        var graphVertices = new HashSet<string>(problem.nodes);
        // A simple path cannot contain more vertices than the graph; reject such
        // certificates before doing the full duplicate and edge checks.
        if (path.Count > graphVertices.Count)
            return false;
        if (path.Distinct().Count() != path.Count)
            return false;
        if (path.Any(vertex => !graphVertices.Contains(vertex)))
            return false;

        var graphEdges = new HashSet<(string first, string second)>();
        foreach (var edge in problem.edges) {
            graphEdges.Add((edge.Key, edge.Value));
            graphEdges.Add((edge.Value, edge.Key));
        }

        for (int i = 1; i < path.Count; i++) {
            if (!graphEdges.Contains((path[i - 1], path[i])))
                return false;
        }

        return true;
    }
}
