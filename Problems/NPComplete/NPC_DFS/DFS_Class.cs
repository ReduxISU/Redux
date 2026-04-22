using API.Interfaces;
using API.Interfaces.Graphs;
using API.Problems.NPComplete.NPC_DFS.Solvers;
using API.Problems.NPComplete.NPC_DFS.Verifiers;
using API.Problems.NPComplete.NPC_DFS.Visualizations;
using SPADE;

namespace API.Problems.NPComplete.NPC_DFS;

class DFS : IGraphProblem<DFSSolver, DFSVerifier, DFSVisualization, UtilCollectionGraph>
{
    public string problemName { get; } = "Depth-First Search";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Depth-first_search";
    public string formalDefinition { get; } = "Depth-First Search = {<G,s,t> | G is a graph with an ordered edge list, s and t are nodes of G, and the output is the first s-to-t path found by depth-first search}";
    public string problemDefinition { get; } = "Depth-First Search asks for the path returned by DFS from a source node to a target node. The order of the edge list determines the neighbor visitation order.";
    public string source { get; } = "N/A";
    public string sourceLink { get; } = "N/A";
    private static readonly string _defaultInstance = "(({1,2,3,4,5,6},((1,2),(2,4),(1,3),(3,5),(5,6))),1,6)";
    public string defaultInstance { get; } = _defaultInstance;
    public string instance { get; set; } = string.Empty;
    public string wikiName { get; } = "";
    public string sourceNode { get; private set; } = string.Empty;
    public string targetNode { get; private set; } = string.Empty;
    public bool isDirected { get; private set; }
    private List<string> _nodes = new();
    private List<KeyValuePair<string, string>> _edges = new();
    public DFSSolver defaultSolver { get; } = new DFSSolver();
    public DFSVerifier defaultVerifier { get; } = new DFSVerifier();
    public DFSVisualization defaultVisualization { get; } = new DFSVisualization();
    public UtilCollectionGraph graph { get; set; }
    public string[] contributors { get; } = { "Scott Barfuss" };

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

    public DFS() : this(_defaultInstance) { }

    public DFS(string input)
    {
        instance = input;

        StringParser dfsParser = new("{((N,E),S,T) | N is set, E is list, S is string, T is string}");
        dfsParser.parse(input);

        UtilCollection nodeCollection = dfsParser["N"] ?? throw new InvalidOperationException("Failed to parse N (nodes).");
        UtilCollection edgeCollection = dfsParser["E"] ?? throw new InvalidOperationException("Failed to parse E (edges).");
        UtilCollection sourceCollection = dfsParser["S"] ?? throw new InvalidOperationException("Failed to parse S (source node).");
        UtilCollection targetCollection = dfsParser["T"] ?? throw new InvalidOperationException("Failed to parse T (target node).");

        nodes = nodeCollection.ToList().Select(node => node.ToString()).ToList();
        sourceNode = sourceCollection.ToString();
        targetNode = targetCollection.ToString();

        ValidateEndpoints();
        edges = ParseEdges(edgeCollection);
        graph = new UtilCollectionGraph(nodeCollection, edgeCollection);
    }

    private void ValidateEndpoints()
    {
        HashSet<string> nodeSet = nodes.ToHashSet();

        if (!nodeSet.Contains(sourceNode))
            throw new InvalidOperationException($"Source node '{sourceNode}' is not in N.");

        if (!nodeSet.Contains(targetNode))
            throw new InvalidOperationException($"Target node '{targetNode}' is not in N.");
    }

    private List<KeyValuePair<string, string>> ParseEdges(UtilCollection edgeCollection)
    {
        HashSet<string> nodeSet = nodes.ToHashSet();
        bool? directed = null;
        var parsedEdges = new List<KeyValuePair<string, string>>();

        foreach (UtilCollection rawEdge in edgeCollection.ToList())
        {
            List<UtilCollection> cast = rawEdge.ToList();
            if (cast.Count == 0 || cast.Count > 2)
                throw new InvalidOperationException("Each DFS edge must be a pair.");

            bool edgeIsDirected = rawEdge.IsOrdered();
            if (directed == null)
                directed = edgeIsDirected;
            else if (directed.Value != edgeIsDirected)
                throw new InvalidOperationException("DFS instances cannot mix directed and undirected edges.");

            string from = cast[0].ToString();
            string to = cast.Count == 1 ? cast[0].ToString() : cast[1].ToString();

            if (!nodeSet.Contains(from))
                throw new InvalidOperationException($"Edge source '{from}' is not in N.");

            if (!nodeSet.Contains(to))
                throw new InvalidOperationException($"Edge target '{to}' is not in N.");

            parsedEdges.Add(new KeyValuePair<string, string>(from, to));
        }

        isDirected = directed ?? false;
        return parsedEdges;
    }
}
