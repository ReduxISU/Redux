using API.Interfaces;
using API.Interfaces.Graphs;
using API.Problems.NPComplete.NPC_TSP.Verifiers;
using API.Problems.P.P_SPSP.Solvers;
using API.Problems.P.P_SPSP.Verifiers;
using API.Problems.P.P_SPSP.Visualizations;
using SPADE;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace API.Problems.P.P_SPSP;

class SPSP : IGraphProblem<SPSPSolver, SPSPVerifier, SPSPVisualization, UtilCollectionGraph>
{

    // --- Fields ---
    public string problemName { get; } = "Single Pair Shortest Path Problem";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Shortest_path_problem";
    public string formalDefinition { get; } = "For a weighted graph G= (V,E) with non-negative edge weights, a source vertex s \u2208 V, and a target vertex t \u2208 V, find the shortest path from s to t, where path length is defined as the sum of edge weights along the path.";
    public string problemDefinition { get; } = "Single Pair Shortest Path (SPSP) in a weighted graph is the problem of finding the shortest path from a given source vertex s and target vertex t in the graph, such that the sum of edge weights along the path is minimized.";
    public string source { get; } = "N/A";
    public string sourceLink { get; } = "N/A";
    private static string _defaultInstance =
    "({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)},1,5)";
    public string defaultInstance { get; } = _defaultInstance;
    public string instance { get; set; } = string.Empty;
    public string instanceFormat { get; } = "(N,E,s,t) where N is the set of node names, E is the set of edges (either unweighted (from,to) pairs or weighted ((from,to),weight) pairs, directed or undirected, non-negative weights only), s is the source node, and t is the target node. Example: ({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)},1,5)";
    public string certificateFormat { get; } = "Braces-wrapped, comma-separated sequence of node names giving the shortest path from source to target, or {} if the target is unreachable. Example: {1,3,5}";

    public string wikiName { get; } = "";
    public string sourceNode { get; private set; } = string.Empty;
    public string targetNode { get; private set; } = string.Empty;
    public bool isDirected { get; private set; }
    public bool isWeighted { get; private set; }
    private List<string> _nodes = new List<string>();
    private List<KeyValuePair<string, string>> _edges = new List<KeyValuePair<string, string>>();
    public SPSPSolver defaultSolver { get; } = new SPSPSolver();
    public SPSPVerifier defaultVerifier { get; } = new SPSPVerifier();
    public SPSPVisualization defaultVisualization { get; } = new SPSPVisualization();
    public UtilCollectionGraph graph { get; set; }
    public string[] contributors { get; } = { "Rajit Nilkar", "Scott Barfuss" };
    // Declared, not derived. Single-pair shortest path (non-negative weights) is
    // solvable in polynomial time (Dijkstra's algorithm).
    public ComplexityClass complexityClass { get; } = ComplexityClass.P;

    // --- Properties ---
    public List<string> nodes
    {
        get => _nodes;
        set => _nodes = value;
    }
    public List<KeyValuePair<string, string>> edges
    {
        get => _edges;
        set => _edges = value;
    }

    // --- Methods Including Constructors ---
    public SPSP() : this(_defaultInstance) { }

    public SPSP(string GInput)
    {
        instance = GInput;

        ParsedShortestPathInstance parsed = ParseInstance(GInput);
        nodes = parsed.Nodes;
        edges = parsed.Edges;
        sourceNode = parsed.SourceNode;
        targetNode = parsed.TargetNode;
        isDirected = parsed.IsDirected;
        isWeighted = parsed.IsWeighted;
        graph = new UtilCollectionGraph(parsed.NodeCollection, parsed.EdgeCollection);
    }

    private static ParsedShortestPathInstance ParseInstance(string rawInstance)
    {
        string graphInput = rawInstance;
        string? explicitSource = null;
        string? explicitTarget = null;

        List<string> outerTerms = SplitOuterTuple(rawInstance);
        if (outerTerms.Count == 4)
        {
            graphInput = $"({outerTerms[0]},{outerTerms[1]})";
            explicitSource = outerTerms[2];
            explicitTarget = outerTerms[3];
        }
        else if (outerTerms.Count == 3 && LooksLikeTuple(outerTerms[0]))
        {
            graphInput = outerTerms[0];
            explicitSource = outerTerms[1];
            explicitTarget = outerTerms[2];
        }

        GraphParseResult graphParse = ParseGraph(graphInput);
        UtilCollection nodeCollection = graphParse.Parser["N"] ?? throw new InvalidOperationException("Failed to parse N (nodes).");
        UtilCollection edgeCollection = graphParse.Parser["E"] ?? throw new InvalidOperationException("Failed to parse E (edges).");

        List<string> parsedNodes = nodeCollection.ToList().Select(node => node.ToString()).ToList();
        List<KeyValuePair<string, string>> parsedEdges = ToEdgePairs(edgeCollection);

        ValidateInstance(parsedNodes, edgeCollection, explicitSource, explicitTarget);

        string resolvedSource = explicitSource ?? (parsedNodes.Count > 0 ? parsedNodes[0] : string.Empty);
        string resolvedTarget = explicitTarget ?? (parsedNodes.Count > 0 ? parsedNodes[^1] : string.Empty);

        return new ParsedShortestPathInstance(
            nodeCollection,
            edgeCollection,
            parsedNodes,
            parsedEdges,
            resolvedSource,
            resolvedTarget,
            graphParse.IsDirected,
            graphParse.IsWeighted);
    }

    private static GraphParseResult ParseGraph(string graphInput)
    {
        (string Pattern, bool IsDirected, bool IsWeighted)[] parseAttempts =
        {
            ("{(N,E) | N is set, E subset {(e,w) | e is N cross N, w is int}}", true, true),
            ("{(N,E) | N is set, E subset {(e,w) | e is N unorderedcross N, w is int}}", false, true),
            ("{(N,E) | N is set, E subset N cross N}", true, false),
            ("{(N,E) | N is set, E subset N unorderedcross N}", false, false)
        };

        Exception? lastError = null;
        foreach (var attempt in parseAttempts)
        {
            try
            {
                StringParser parser = new(attempt.Pattern);
                parser.parse(graphInput);
                return new GraphParseResult(parser, attempt.IsDirected, attempt.IsWeighted);
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        throw new InvalidOperationException("Failed to parse SPSP instance.", lastError);
    }

    private static void ValidateInstance(
        List<string> parsedNodes,
        UtilCollection edgeCollection,
        string? explicitSource,
        string? explicitTarget)
    {
        HashSet<string> nodeSet = parsedNodes.ToHashSet();

        if (explicitSource != null && !nodeSet.Contains(explicitSource))
            throw new InvalidOperationException($"Source node '{explicitSource}' is not in N.");

        if (explicitTarget != null && !nodeSet.Contains(explicitTarget))
            throw new InvalidOperationException($"Target node '{explicitTarget}' is not in N.");

        foreach (UtilCollection rawEdge in edgeCollection.ToList())
        {
            ParsedEdge edge = ParseEdge(rawEdge);

            if (!nodeSet.Contains(edge.From))
                throw new InvalidOperationException($"Edge source '{edge.From}' is not in N.");

            if (!nodeSet.Contains(edge.To))
                throw new InvalidOperationException($"Edge target '{edge.To}' is not in N.");

            if (edge.Weight < 0)
                throw new InvalidOperationException("SPSP using Dijkstra's algorithm does not allow negative edge weights.");
        }
    }

    private static List<KeyValuePair<string, string>> ToEdgePairs(UtilCollection edgeCollection)
    {
        return edgeCollection.ToList().Select(rawEdge =>
        {
            ParsedEdge edge = ParseEdge(rawEdge);
            return new KeyValuePair<string, string>(edge.From, edge.To);
        }).ToList();
    }

    private static ParsedEdge ParseEdge(UtilCollection rawEdge)
    {
        bool firstLooksLikeCollection = LooksLikeCollection(rawEdge[0]);
        bool secondLooksLikeCollection = rawEdge.Count() > 1 && LooksLikeCollection(rawEdge[1]);
        bool isWeighted = rawEdge.Count() == 2 && firstLooksLikeCollection && !secondLooksLikeCollection;

        if (isWeighted)
        {
            UtilCollection endpoints = rawEdge[0];
            int weight = int.Parse(rawEdge[1].ToString());

            if (weight < 0)
                throw new InvalidOperationException($"SPSP using Dijkstra's algorithm does not allow negative edge weights. Found edge weight: {weight}");

            return new ParsedEdge(GetFrom(endpoints), GetTo(endpoints), weight);
        }

        return new ParsedEdge(GetFrom(rawEdge), GetTo(rawEdge), 1);
    }

    private static string GetFrom(UtilCollection endpoints)
    {
        if (endpoints.IsOrdered())
            return endpoints[0].ToString();

        List<UtilCollection> cast = endpoints.ToList();
        return cast[0].ToString();
    }

    private static string GetTo(UtilCollection endpoints)
    {
        if (endpoints.IsOrdered())
            return endpoints[1].ToString();

        List<UtilCollection> cast = endpoints.ToList();
        if (cast.Count == 1)
            return cast[0].ToString();

        return cast[1].ToString();
    }

    private static bool LooksLikeCollection(UtilCollection value)
    {
        string text = value.ToString().TrimStart();
        return text.StartsWith("{") || text.StartsWith("(");
    }

    private static bool LooksLikeTuple(string value)
    {
        return value.TrimStart().StartsWith("(");
    }

    private static List<string> SplitOuterTuple(string input)
    {
        string trimmed = input.Trim();
        if (trimmed.Length < 2 || trimmed[0] != '(' || trimmed[^1] != ')')
            return new List<string>();

        string inner = trimmed[1..^1];
        var parts = new List<string>();
        var current = new StringBuilder();
        int parenDepth = 0;
        int braceDepth = 0;
        int bracketDepth = 0;

        foreach (char ch in inner)
        {
            if (ch == ',' && parenDepth == 0 && braceDepth == 0 && bracketDepth == 0)
            {
                parts.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }

            current.Append(ch);
            switch (ch)
            {
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
        string TargetNode,
        bool IsDirected,
        bool IsWeighted);
    private sealed record ParsedEdge(string From, string To, int Weight);
}
