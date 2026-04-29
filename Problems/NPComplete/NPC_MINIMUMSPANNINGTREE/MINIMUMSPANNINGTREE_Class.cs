using API.Interfaces;
using API.Interfaces.Graphs;
using API.Problems.NPComplete.NPC_MINIMUMSPANNINGTREE.Solvers;
using API.Problems.NPComplete.NPC_MINIMUMSPANNINGTREE.Verifiers;
using API.Problems.NPComplete.NPC_MINIMUMSPANNINGTREE.Visualizations;
using SPADE;
using System.Collections.Generic;
using System.Linq;

namespace API.Problems.NPComplete.NPC_MINIMUMSPANNINGTREE;

class MINIMUMSPANNINGTREE : IGraphProblem<KruskalSolver, MinimumSpanningTreeVerifier, MinimumSpanningTreeVisualization, UtilCollectionGraph>
{
    public string problemName { get; } = "Minimum Spanning Tree";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Minimum_spanning_tree";
    public string formalDefinition { get; } = "Given a weighted undirected graph G = (V, E, w), find a subset T \u2286 E such that T is a spanning tree of G and the sum of weights in T is minimum.";
    public string problemDefinition { get; } = "Find a minimum-weight spanning tree in a weighted, undirected graph.";
    public string source { get; } = "N/A";
    public string sourceLink { get; } = "https://en.wikipedia.org/wiki/Minimum_spanning_tree";
    public string wikiName { get; } = "";
    public static string _defaultInstance { get; } = "({1,2,3,4},{({1,2},1),({2,3},2),({3,4},1),({1,4},2),({1,3},3)})";
    public string defaultInstance { get; } = _defaultInstance;
    public string instance { get; set; } = string.Empty;
    public string[] contributors { get; } = { "Andreas Kramer", "Val Kimbrough" };

    private List<string> _nodes = new List<string>();
    private List<(string source, string destination, int weight)> _edges = new List<(string source, string destination, int weight)>();

    public KruskalSolver defaultSolver { get; } = new KruskalSolver();
    public MinimumSpanningTreeVerifier defaultVerifier { get; } = new MinimumSpanningTreeVerifier();
    public MinimumSpanningTreeVisualization defaultVisualization { get; } = new MinimumSpanningTreeVisualization();
    public UtilCollectionGraph graph { get; set; }

    public List<string> nodes
    {
        get => _nodes;
        set => _nodes = value;
    }

    public List<(string source, string destination, int weight)> edges
    {
        get => _edges;
        set => _edges = value;
    }

    public MINIMUMSPANNINGTREE() : this(_defaultInstance) { }

    public MINIMUMSPANNINGTREE(string instanceString)
    {
        instance = instanceString;

        StringParser mstParser = new("{(N,E) | N is set, E subset {(e, w) | e is N unorderedcross N, w is int}}");
        mstParser.parse(instanceString);

        nodes = mstParser["N"].ToList().Select(node => node.ToString()).ToList();
        edges = mstParser["E"].ToList().Select(edge =>
        {
            List<UtilCollection> endpoints = edge[0].ToList();
            return (endpoints[0].ToString(), endpoints[1].ToString(), int.Parse(edge[1].ToString()));
        }).ToList();

        ValidateInstance(nodes, mstParser["E"]);
        graph = new UtilCollectionGraph(mstParser["N"], mstParser["E"]);
    }

    private static void ValidateInstance(List<string> nodes, UtilCollection edgeCollection)
    {
        HashSet<string> nodeSet = nodes.ToHashSet();
        foreach (UtilCollection rawEdge in edgeCollection.ToList())
        {
            List<UtilCollection> endpoints = rawEdge[0].ToList();
            string u = endpoints[0].ToString();
            string v = endpoints[1].ToString();
            if (!nodeSet.Contains(u) || !nodeSet.Contains(v))
                throw new InvalidOperationException("MST instance contains an edge with a vertex that is not in the node set.");
        }
    }
}
