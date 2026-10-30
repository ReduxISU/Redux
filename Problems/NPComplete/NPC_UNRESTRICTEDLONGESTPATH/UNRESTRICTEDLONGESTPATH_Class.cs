using System.Globalization;
using System.Text.RegularExpressions;
using API.Interfaces;
using API.Problems.NPComplete.NPC_UNRESTRICTEDLONGESTPATH.Solvers;
using API.Problems.NPComplete.NPC_UNRESTRICTEDLONGESTPATH.Verifiers;
using API.Problems.NPComplete.NPC_UNRESTRICTEDLONGESTPATH.Visualizations;
using SPADE;

namespace API.Problems.NPComplete.NPC_UNRESTRICTEDLONGESTPATH;

class UNRESTRICTEDLONGESTPATH : IGraphProblem<UnrestrictedLongestPathSolver, UnrestrictedLongestPathVerifier, UnrestrictedLongestPathVisualization, UtilCollectionGraph> {

    // --- Fields ---
    public string problemName { get; } = "Unrestricted Longest Path";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Longest_path_problem";
    public string formalDefinition { get; } = "Unrestricted Longest Path Problem = {<G, k> | Exists a simple path in G with at least k edges}.";
    public string problemDefinition { get; } = "Unrestricted Longest Path Problem decision problem asks whether given a graph G = (V, E) and an integer k, whether G contains a simple path (no repeated vertices) with at least k edges.";
    public string inputDescription { get; } = "G, a graph, and k, the minimum number of edges in the path.";
    public string outputDescription { get; } = "True or False, whether G has a simple path with at least k edges.";
    public string source { get; } = "Monien, B. (1985). How to find long paths efficiently. North-Holland Mathematics Studies, 239–254.";
    public string sourceLink { get; } = "https://doi.org/10.1016/s0304-0208(08)73110-4";

    private static readonly string _defaultInstance = "(({1,2,3,4,5,6},{{1,2},{1,3},{2,3},{2,4},{3,5},{4,5},{4,6},{5,6}}),5)";
    public const string InstanceGrammar =
        "{((N,E),K) | N is set, E subset N unorderedcross N, K is int}";
    private const string VertexPattern = @"[^\s,{}()]+";
    private const string Vertex = "(?<vertex>" + VertexPattern + ")";
    private const string Edge = @"(?<edge>\{\s*" + VertexPattern + @"\s*,\s*" + VertexPattern + @"\s*\})";
    private static readonly Regex InstanceSyntax = new(
        @"\A\s*\(\s*\(\s*\{\s*" + Vertex + @"(?:\s*,\s*" + Vertex + @")*\s*\}\s*,\s*\{\s*(?:" +
        Edge + @"(?:\s*,\s*" + Edge + @")*)?\s*\}\s*\)\s*,\s*[0-9]+\s*\)\s*\z",
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    public string defaultInstance { get; } = _defaultInstance;
    public string instanceFormat { get; } =
        $"Format: {InstanceGrammar} Example: {_defaultInstance}. " +
        "N must be nonempty. Vertex labels cannot contain whitespace, commas, braces, or parentheses. " +
        "Edges are unordered pairs of distinct declared vertices; duplicate vertices and edges are rejected. " +
        "K is a nonnegative decimal integer from 0 through 2147483647. Whitespace between tokens is allowed.";

    public string certificateFormat { get; } =
        $"Format: {UnrestrictedLongestPathVerifier.CertificateGrammar}. Example: {UnrestrictedLongestPathVerifier.CertificateExample}";
    public string instance { get; }
    public string wikiName { get; } = "Longest path";

    public UtilCollectionGraph graph { get; }

    public UnrestrictedLongestPathSolver defaultSolver { get; } = new UnrestrictedLongestPathSolver();
    public UnrestrictedLongestPathVerifier defaultVerifier { get; } = new UnrestrictedLongestPathVerifier();
    public UnrestrictedLongestPathVisualization defaultVisualization { get; } = new UnrestrictedLongestPathVisualization();
    public string[] contributors { get; } = { "Alice Giola", "Sadman Hasan", "Javier Rodriguez Lopez" };
    public ComplexityClass complexityClass { get; } = ComplexityClass.NPComplete;

    public ProblemType problemType { get; } = ProblemType.GraphTheory;

    // --- Properties ---
    public IReadOnlyList<string> nodes { get; }
    public IReadOnlyList<KeyValuePair<string, string>> edges { get; }
    public int K { get; }

    public string sourceFile { get; } = SourceFile.Path();

    // --- Methods and Constructors ---
    public UNRESTRICTEDLONGESTPATH() : this(_defaultInstance) { }

    public UNRESTRICTEDLONGESTPATH(string stringInstance) {
        try {
            if (string.IsNullOrWhiteSpace(stringInstance)) {
                throw new FormatException("instance is empty");
            }
            Match syntax = InstanceSyntax.Match(stringInstance);
            if (!syntax.Success) {
                throw new FormatException(
                    "expected (({vertices},{{u,v},...}),K), with a nonempty vertex set, " +
                    "two distinct endpoints per edge, and a nonnegative decimal K");
            }

            StringParser longestPathGraph = new(InstanceGrammar);
            longestPathGraph.parse(string.Concat(stringInstance.Where(c => !char.IsWhiteSpace(c))));
            List<string> parsedNodes = longestPathGraph["N"].ToList().Select(node => node.ToString()).ToList();
            List<UtilCollection> parsedEdges = longestPathGraph["E"].ToList();
            if (!int.TryParse(longestPathGraph["K"].ToString(), NumberStyles.None,
                CultureInfo.InvariantCulture, out int parsedK) || parsedK < 0) {
                throw new FormatException("K must be an integer from 0 through 2147483647");
            }
            if (parsedNodes.Count == 0 || parsedNodes.Count != syntax.Groups["vertex"].Captures.Count ||
                parsedNodes.Distinct(StringComparer.Ordinal).Count() != parsedNodes.Count) {
                throw new FormatException("the vertex set must be nonempty and contain distinct labels");
            }
            if (parsedEdges.Count != syntax.Groups["edge"].Captures.Count) {
                throw new FormatException("duplicate undirected edges are not allowed");
            }

            HashSet<string> vertexSet = new(parsedNodes, StringComparer.Ordinal);
            HashSet<(string, string)> edgeSet = new();
            List<KeyValuePair<string, string>> edgeList = new();
            foreach (UtilCollection edge in parsedEdges) {
                if (edge.IsValue() || edge.IsOrdered()) {
                    throw new FormatException("each edge must be an unordered pair of vertex labels");
                }
                List<UtilCollection> endpoints = edge.ToList();
                if (endpoints.Count != 2 || endpoints.Any(node => !node.IsValue())) {
                    throw new FormatException("each edge must contain exactly two distinct vertex labels; loops are not allowed");
                }
                string u = endpoints[0].ToString();
                string v = endpoints[1].ToString();
                if (u == v || !vertexSet.Contains(u) || !vertexSet.Contains(v)) {
                    throw new FormatException("edge endpoints must be distinct declared vertices");
                }
                var canonical = string.CompareOrdinal(u, v) < 0 ? (u, v) : (v, u);
                if (!edgeSet.Add(canonical)) {
                    throw new FormatException("duplicate undirected edges are not allowed");
                }
                edgeList.Add(new(u, v));
            }

            graph = new UtilCollectionGraph(longestPathGraph["N"], longestPathGraph["E"]);
            nodes = parsedNodes.AsReadOnly();
            edges = edgeList.AsReadOnly();
            K = parsedK;
            instance = stringInstance;
        } catch (Exception ex) when (ex is not ProblemParseException && ex is not OutOfMemoryException) {
            throw new ProblemParseException("UNRESTRICTEDLONGESTPATH", stringInstance, ex.Message, ex);
        }
    }
}
