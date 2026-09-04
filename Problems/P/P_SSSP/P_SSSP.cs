using Antlr4.Runtime;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Problems.P.P_SSSP.Solvers;
using API.Problems.P.P_SSSP.Verifiers;
using API.Problems.P.P_SSSP.Visualizations;
using SPADE;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace API.Problems.P.P_SSSP;

class SSSP : IGraphProblem<SSSPSolver, SSSPVerifier, SSSPVisualization, UtilCollectionGraph> {
    // --- Fields ---
    public string problemName { get; } = "Single Source Shortest Path Problem";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Shortest_path_problem";
    public string formalDefinition { get; } = "For a weighted graph G = (V,E), with non-negative edge weights, a source vertex s \u2208 V, find the shortest path distance from s to every other vertex v \u2208 V, where path length is defined as the sum of edge weights along the path.";
    public string problemDefinition { get; } = "Single Source Shortest Path (SSSP) in a weighted graph is the problem of determining the shortest path from a source vertex to all other reachable vertices in the graph such that the sum of edge weights along each path is minimized.";
    public string source { get; } = "N/A";
    public string sourceLink { get; } = "N/A";
    private static string _defaultInstance = "({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)},1)";
    public string defaultInstance { get; } = _defaultInstance;
    public string instance { get; set; } = string.Empty;
    public string wikiName { get; } = "";
    public string sourceNode { get; private set; } = string.Empty;
    public bool isDirected { get; private set; }
    public bool isWeighted { get; private set; }
    private List<string> _nodes = new List<string>();
    private List<KeyValuePair<string, string>> _edges = new List<KeyValuePair<string, string>>();
    public SSSPSolver defaultSolver { get; } = new SSSPSolver();
    public SSSPVerifier defaultVerifier { get; } = new SSSPVerifier();
    public SSSPVisualization defaultVisualization { get; } = new SSSPVisualization();
    public UtilCollectionGraph graph { get; set; }
    public string[] contributors { get; } = { "Rajit Nilkar" };
    // Declared, not derived. Single-source shortest path (non-negative weights) is
    // solvable in polynomial time (Dijkstra's algorithm).
    public ComplexityClass complexityClass { get; } = ComplexityClass.P;

    // --- Properties ---
    public List<string> nodes {
        get => _nodes;
        set => _nodes = value;
    }
    public List<KeyValuePair<string, string>> edges {
        get => _edges;
        set => _edges = value;
    }

    // --- Methods Including Constructor ---
    public SSSP() : this(_defaultInstance) { }

    public SSSP(string GInput) {
        instance = GInput;

        ParsedShortestPathInstance parsed = ParseInstance(GInput);
        nodes = parsed.Nodes;
        edges = parsed.Edges;
        sourceNode = parsed.SourceNode;
        isDirected = parsed.IsDirected;
        isWeighted = parsed.IsWeighted;
        graph = new UtilCollectionGraph(parsed.NodeCollection, parsed.EdgeCollection);
    }

    private static ParsedShortestPathInstance ParseInstance(string rawInstance) {
        string graphInput = rawInstance;
        string? explicitSource = null;

        List<string> outerTerms = SplitOuterTuple(rawInstance);
        if (outerTerms.Count == 3) {
            graphInput = $"({outerTerms[0]},{outerTerms[1]})";
            explicitSource = outerTerms[2];
        } else if (outerTerms.Count == 2 && LooksLikeTuple(outerTerms[0])) {
            graphInput = outerTerms[0];
            explicitSource = outerTerms[1];
        }

        GraphParseResult graphParse = ParseGraph(graphInput);
        UtilCollection nodeCollection = graphParse.Parser["N"] ?? throw new InvalidOperationException("Failed to parse N (nodes)");
        UtilCollection edgeCollection = graphParse.Parser["E"] ?? throw new InvalidOperationException("Failed to parse E (edges)");

        List<string> parsedNodes = nodeCollection.ToList().Select(node => node.ToString()).ToList();
        List<KeyValuePair<string, string>> parsedEdges = ToEdgePairs(edgeCollection);

        ValidateInstance(parsedNodes, edgeCollection, explicitSource);

        string resolvedSource = explicitSource ?? (parsedNodes.Count > 0 ? parsedNodes[0] : string.Empty);

        return new ParsedShortestPathInstance(
            nodeCollection,
            edgeCollection,
            parsedNodes,
            parsedEdges,
            resolvedSource,
            graphParse.IsDirected,
            graphParse.IsWeighted);
    }

    private static GraphParseResult ParseGraph(string graphInput) {
        (string Pattern, bool IsDirected, bool IsWeighted)[] parseAttempts =
        {
            ("{(N,E) | N is set, E subset {(e,w) | e is N cross N, w is int}}", true, true),
            ("{(N,E) | N is set, E subset {(e,w) | e is unorderedcross N, w is int}}", false, true),
            ("{(N,E) | N is set, E subset N cross N}", true, false),
            ("{(N,E) | N is set, E subset N unorderedcross N}", false, false)
        };

        Exception? lastError = null;
        foreach (var attempt in parseAttempts) {
            try {
                StringParser parser = new(attempt.Pattern);
                parser.parse(graphInput);
                return new GraphParseResult(parser, attempt.IsDirected, attempt.IsWeighted);
            } catch (Exception e) {
                lastError = e;
            }
        }

        throw new InvalidOperationException("Failed to parse SSSP instance.", lastError);
    }

    private static void ValidateInstance(
        List<string> parsedNodes,
        UtilCollection edgeCollection,
        string? explicitSource) {
        HashSet<string> nodeSet = parsedNodes.ToHashSet();

        if (explicitSource != null && !nodeSet.Contains(explicitSource))
            throw new InvalidOperationException($"Source node '{explicitSource}' is not in N");

        foreach (UtilCollection rawEdge in edgeCollection) {
            ParsedEdge edge = ParseEdge(rawEdge);

            if (!nodeSet.Contains(edge.From))
                throw new InvalidOperationException($"Edge source '{edge.From}' is not in N.");

            if (!nodeSet.Contains(edge.To))
                throw new InvalidOperationException($"Edge target '{edge.To}' is not in N.");

            if (edge.Weight < 0)
                throw new InvalidOperationException("SSSP using Dijkstra's algorithm does not handle negative edge-weights.");
        }
    }

    private static List<KeyValuePair<string, string>> ToEdgePairs(UtilCollection edgeCollection) {
        return edgeCollection.ToList().Select(rawEdge => {
            ParsedEdge edge = ParseEdge(rawEdge);
            return new KeyValuePair<string, string>(edge.From, edge.To);
        }).ToList();
    }

    private static ParsedEdge ParseEdge(UtilCollection rawEdge) {
        // The unweighted/undirected grammar pattern binds a raw edge directly to an
        // unordered node-set (e.g. {1,2}), which cannot be indexed by position (only
        // ordered tuples/lists can be). Guard with IsOrdered() before probing rawEdge[0]/[1]
        // so such edges fall through to the GetFrom/GetTo path below, which already
        // handles unordered collections correctly.
        bool isWeighted = rawEdge.IsOrdered() && rawEdge.Count() == 2
            && LooksLikeCollection(rawEdge[0]) && !LooksLikeCollection(rawEdge[1]);

        if (isWeighted) {
            UtilCollection endpoints = rawEdge[0];
            int weight = int.Parse(rawEdge[1].ToString());

            if (weight < 0)
                throw new InvalidOperationException($"SSSP using Dijkstra's algorithm does not allow negative edge weights. Found edge weight: {weight}");

            return new ParsedEdge(GetFrom(endpoints), GetTo(endpoints), weight);
        }

        return new ParsedEdge(GetFrom(rawEdge), GetTo(rawEdge), 1);
    }

    private static bool LooksLikeCollection(UtilCollection value) {
        string text = value.ToString().TrimStart();
        return text.StartsWith("{") || text.StartsWith("(");
    }

    private static string GetFrom(UtilCollection endpoints) {
        if (endpoints.IsOrdered())
            return endpoints[0].ToString();

        List<UtilCollection> cast = endpoints.ToList();
        return cast[0].ToString();
    }

    private static string GetTo(UtilCollection endpoints) {
        if (endpoints.IsOrdered())
            return endpoints[1].ToString();

        List<UtilCollection> cast = endpoints.ToList();
        if (cast.Count == 1)
            return cast[0].ToString();

        return cast[1].ToString();
    }

    private static bool LooksLikeTuple(string value) {
        return value.TrimStart().StartsWith("(");
    }

    private static List<string> SplitOuterTuple(string input) {
        string trimmed = input.Trim();
        if (trimmed.Length < 2 || trimmed[0] != '(' || trimmed[^1] != ')')
            return new List<string>();

        string inner = trimmed[1..^1];
        var parts = new List<string>();
        var current = new StringBuilder();
        int parenDepth = 0;
        int braceDepth = 0;
        int bracketDepth = 0;

        foreach (char ch in inner) {
            if (ch == ',' && parenDepth == 0 && braceDepth == 0 && bracketDepth == 0) {
                parts.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }

            current.Append(ch);
            switch (ch) {
                case '(':
                    parenDepth++;
                    break;
                case ')':
                    parenDepth--;
                    break;
                case '{':
                    braceDepth++;
                    break;
                case '}':
                    braceDepth--;
                    break;
                case '[':
                    bracketDepth++;
                    break;
                case ']':
                    bracketDepth--;
                    break;
            }
        }

        if (current.Length > 0)
            parts.Add(current.ToString().Trim());

        return parts;
    }

    private sealed record GraphParseResult(StringParser Parser, bool IsDirected, bool IsWeighted);
    private sealed record ParsedShortestPathInstance(
        UtilCollection NodeCollection,
        UtilCollection EdgeCollection,
        List<string> Nodes,
        List<KeyValuePair<string, string>> Edges,
        string SourceNode,
        bool IsDirected,
        bool IsWeighted);
    private sealed record ParsedEdge(string From, string To, int Weight);
}
