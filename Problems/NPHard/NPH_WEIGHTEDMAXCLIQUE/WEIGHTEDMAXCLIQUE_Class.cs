using System.Collections.ObjectModel;
using System.Globalization;
using API.Interfaces;
using SPADE;

namespace API.Problems.NPHard.NPH_WEIGHTEDMAXCLIQUE;

internal sealed class WEIGHTEDMAXCLIQUE : IGraphProblem {
    public string problemName { get; } = "Weighted Maximum Clique";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Clique_problem";
    public string formalDefinition { get; } = "Given an undirected graph G=(V,E) and nonnegative integer vertex weights w, find a clique C subset V maximizing the sum of w(v) for v in C.";
    public string inputDescription { get; } = "An undirected graph and one nonnegative integer weight for every vertex";
    public string outputDescription { get; } = "One maximum-weight clique and its total vertex weight";
    public string source { get; } = "Carraghan, R. and Pardalos, P. M. (1990). An exact algorithm for the maximum clique problem. Operations Research Letters 9(6), 375–382. Vertex-weighted optimization generalization.";
    public string sourceLink { get; } = "https://doi.org/10.1016/0167-6377(90)90057-C";
    public string sourceFile { get; } = SourceFile.Path();
    public string wikiName { get; } = "Clique_problem";
    public string[] contributors { get; } = { "Corbin Hay" };
    public string problemDefinition { get; } = "Find a clique of maximum total nonnegative integer vertex weight in an undirected graph. The empty clique is allowed. Unit weights recover Maximum Clique.";
    public ComplexityClass complexityClass { get; } = ComplexityClass.NPHard;
    public ProblemVariant problemVariant { get; } = ProblemVariant.Optimization;
    public ProblemType problemType { get; } = ProblemType.GraphTheory;

    public const string InstanceGrammar = "{((N,E),W) | N is set, E subset N unorderedcross N, W is list}";
    public const string DefaultInstance = "(({a,b,c,d},{{a,b},{a,c},{b,c},{b,d}}),((a,4),(b,3),(c,5),(d,7)))";
    public string defaultInstance { get; } = DefaultInstance;
    public string instanceFormat { get; } = $"Format: {InstanceGrammar}. E contains two distinct endpoints per undirected edge. W is an ordered list of (vertex,weight) pairs in any order, with exactly one weight per vertex. Weights and their sum must fit a nonnegative signed 64-bit integer. Example: {DefaultInstance}";
    public string certificateFormat { get; } = "({v1,v2,...},totalWeight): distinct selected vertex identifiers and their nonnegative signed 64-bit total weight. Vertices must form a clique; feasibility and optimality are separate checks. The empty clique is ({},0). Example: ({a,b,c},12)";

    public string instance { get; }
    public IReadOnlyList<string> nodes { get; }
    public IReadOnlyList<KeyValuePair<string, string>> edges { get; }
    public IReadOnlyDictionary<string, long> weights { get; }
    // Also bounds every possible clique value and the planned candidate-weight upper bound.
    public long totalVertexWeight { get; }
    public UtilCollectionGraph graph { get; }
    Graph IGraphProblem.graph => graph;

    // Stable indices shared by all solvers, independent of input collection order.
    // Keep adjacency private so algorithms cannot mutate the problem's graph.
    private readonly string[] _orderedNodes;
    private readonly HashSet<int>[] _adjacency;
    internal int VertexCount => _orderedNodes.Length;
    internal long VertexWeight(int vertex) => weights[_orderedNodes[vertex]];
    internal int Degree(int vertex) => _adjacency[vertex].Count;
    internal bool AreAdjacent(int a, int b) => _adjacency[a].Contains(b);

    internal bool CanAdd(int vertex, IReadOnlyList<int> clique) {
        foreach (int member in clique) {
            if (!AreAdjacent(vertex, member)) return false;
        }
        return true;
    }

    internal bool IsClique(IReadOnlyList<int> vertices) {
        for (int i = 0; i < vertices.Count; i++) {
            for (int j = 0; j < i; j++) {
                if (!AreAdjacent(vertices[i], vertices[j])) return false;
            }
        }
        return true;
    }

    internal long Value(IReadOnlyList<int> vertices) {
        long value = 0;
        foreach (int vertex in vertices) value = checked(value + VertexWeight(vertex));
        return value;
    }

    internal string FormatCertificate(IReadOnlyList<int> clique, long weight) =>
        "({" + string.Join(",", clique.Order().Select(v => _orderedNodes[v])) + "},"
        + weight.ToString(CultureInfo.InvariantCulture) + ")";

    public WEIGHTEDMAXCLIQUE() : this(DefaultInstance) { }

    public WEIGHTEDMAXCLIQUE(string input) {
        try {
            WeightedCliqueInputSyntax.Validate(input);
            // Parse the complete tree so SPADE cannot silently ignore extra tuple components.
            StringParser parser = new("{I | I is list}");
            // SPADE does not consistently ignore whitespace around collections.
            // The syntax guard has already rejected whitespace within scalar tokens.
            parser.parse(string.Concat(input.Where(c => !char.IsWhiteSpace(c))));
            UtilCollection root = parser["I"];
            RequirePair(root, "instance ((N,E),W)");
            RequirePair(root[0], "graph (N,E)");
            UtilCollection n = root[0][0], e = root[0][1], w = root[1];
            if (!n.IsUnordered()) throw new FormatException("N must be a brace-delimited vertex set");
            if (!e.IsUnordered()) throw new FormatException("E must be a brace-delimited undirected edge set");
            if (!w.IsOrdered()) throw new FormatException("W must be a parenthesized list of (vertex,weight) pairs");

            var nodeList = n.ToList().Select(Vertex).ToList();
            var nodeSet = nodeList.ToHashSet(StringComparer.Ordinal);
            var edgeList = new List<KeyValuePair<string, string>>();
            var seenEdges = new HashSet<(string, string)>();
            foreach (UtilCollection edge in e.ToList()) {
                if (!edge.IsUnordered() || edge.Count() != 2)
                    throw new FormatException("each undirected edge must be a set of two distinct vertices, e.g. {a,b}; self-loops are not allowed");
                List<UtilCollection> endpoints = edge.ToList();
                string a = Vertex(endpoints[0]), b = Vertex(endpoints[1]);
                if (!nodeSet.Contains(a) || !nodeSet.Contains(b))
                    throw new FormatException($"edge {{{a},{b}}} contains a vertex outside N");
                // SPADE can retain both {a,b} and {b,a} in a set (collection hashing
                // depends on enumeration order). Canonicalize the undirected pairs.
                if (StringComparer.Ordinal.Compare(a, b) > 0) (a, b) = (b, a);
                if (seenEdges.Add((a, b))) edgeList.Add(new(a, b));
            }

            var weightMap = new Dictionary<string, long>(StringComparer.Ordinal);
            long total = 0;
            foreach (UtilCollection entry in w.ToList()) {
                RequirePair(entry, "weight assignment (vertex,weight)");
                string vertex = Vertex(entry[0]);
                if (!nodeSet.Contains(vertex))
                    throw new FormatException($"weight assignment names unknown vertex '{vertex}'");
                if (!entry[1].IsValue() || !long.TryParse(entry[1].ToString(), NumberStyles.None,
                        CultureInfo.InvariantCulture, out long weight))
                    throw new FormatException($"weight for '{vertex}' must be a nonnegative integer no greater than {long.MaxValue}");
                if (!weightMap.TryAdd(vertex, weight))
                    throw new FormatException($"duplicate weight assignment for vertex '{vertex}'");
                total = checked(total + weight);
            }
            foreach (string vertex in nodeList) {
                if (!weightMap.ContainsKey(vertex))
                    throw new FormatException($"missing weight assignment for vertex '{vertex}'");
            }

            nodes = nodeList.AsReadOnly();
            edges = edgeList.AsReadOnly();
            weights = new ReadOnlyDictionary<string, long>(weightMap);
            _orderedNodes = nodeList.Order(StringComparer.Ordinal).ToArray();
            var indices = _orderedNodes.Select((name, index) => (name, index))
                .ToDictionary(entry => entry.name, entry => entry.index, StringComparer.Ordinal);
            _adjacency = _orderedNodes.Select(_ => new HashSet<int>()).ToArray();
            foreach (var edge in edgeList) {
                int a = indices[edge.Key], b = indices[edge.Value];
                _adjacency[a].Add(b);
                _adjacency[b].Add(a);
            }
            totalVertexWeight = total;
            var canonicalEdges = edgeList.Select(edge =>
                new UtilCollection(new HashSet<string> { edge.Key, edge.Value })).ToHashSet();
            graph = new UtilCollectionGraph(n, new UtilCollection(canonicalEdges));
            instance = input;
        } catch (OverflowException ex) {
            throw new ProblemParseException(problemName, input, $"sum of vertex weights must not exceed {long.MaxValue}", ex);
        } catch (Exception ex) when (ex is not OutOfMemoryException && ex is not ProblemParseException) {
            throw new ProblemParseException(problemName, input, ex.Message, ex);
        }
    }

    private static void RequirePair(UtilCollection value, string description) {
        if (!value.IsOrdered() || value.Count() != 2)
            throw new FormatException($"{description} must be a parenthesized tuple with exactly two components");
    }

    private static string Vertex(UtilCollection value) {
        if (!value.IsValue() || string.IsNullOrWhiteSpace(value.ToString()))
            throw new FormatException("vertex identifiers must be nonempty scalar values");
        return value.ToString();
    }
}

// SPADE 0.1.2 ignores trailing input and empty comma-separated entries. This
// delimiter/entry guard rejects those cases before SPADE parses the collections.
// It does not interpret graph data or replace SPADE's collection parser.
internal static class WeightedCliqueInputSyntax {
    internal static void Validate(string input) {
        if (string.IsNullOrWhiteSpace(input)) throw new FormatException("instance is empty");
        string text = input.Trim();
        if (text[0] != '(' || text[^1] != ')')
            throw new FormatException("instance must be an enclosing ((N,E),W) tuple");

        var delimiters = new Stack<char>();
        bool needsEntry = true;
        bool justOpened = false;
        for (int i = 0; i < text.Length; i++) {
            char c = text[i];
            if (char.IsWhiteSpace(c)) continue;
            if (c is '(' or '{') {
                if (!needsEntry) throw new FormatException("missing comma before collection");
                delimiters.Push(c);
                needsEntry = justOpened = true;
            } else if (c is ')' or '}') {
                if (delimiters.Count == 0 || delimiters.Pop() != (c == ')' ? '(' : '{'))
                    throw new FormatException("collection delimiters are not balanced");
                if (needsEntry && !justOpened) throw new FormatException("empty entry after comma");
                needsEntry = justOpened = false;
                if (delimiters.Count == 0 && i != text.Length - 1)
                    throw new FormatException("unexpected text after instance tuple");
            } else if (c == ',') {
                if (needsEntry) throw new FormatException("empty entry before comma");
                needsEntry = true;
                justOpened = false;
            } else {
                if (!needsEntry) throw new FormatException("missing comma between entries");
                while (i + 1 < text.Length && !char.IsWhiteSpace(text[i + 1])
                       && text[i + 1] is not ('(' or ')' or '{' or '}' or ',')) i++;
                needsEntry = justOpened = false;
            }
        }
        if (delimiters.Count != 0) throw new FormatException("collection delimiters are not balanced");
    }
}
