using API.Interfaces;
using API.Problems.NPComplete.NPC_SHORTESTPATH.Solvers;
using API.Problems.NPComplete.NPC_SHORTESTPATH.Verifiers;
using API.Problems.NPComplete.NPC_SHORTESTPATH.Visualizations;
using SPADE;
using System.Linq;
using System.Collections.Generic;
using API.Interfaces.Graphs;

namespace API.Problems.NPComplete.NPC_SHORTESTPATH;

class SHORTESTPATH: IGraphProblem<DijkstraSolver, ShortestPathVerifier, ShortestPathVisualization, UtilCollectionGraph>
{

    // --- Fields ---
    public string problemName { get; } = "Shortest Path Problem";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Shortest_path_problem";
    public string formalDefinition { get; } = "For a weighted graph G= (V,E) with weight function w:E->\\(\\mathbb{R}\\), find the shortest path source s to target t, where path length is the sum of edge weights"; //TODO figure out what it's supposed to be
    public string problemDefinition { get; } = "Shortest Path Problem is the problem of determining the shortest path from a given source vertex to all other vertices such that the sum of the weights of its inherent edges is minimized.";
    public string source { get; } = "N/A";
    public string sourceLink { get; } = "N/A";
    private static string _defaultInstance =
    "({1,2,3,4,5},{({1,2},4),({1,3},2),({2,3},1),({3,5},7),({2,4},3),({4,5},9)})";
    public string defaultInstance { get; } = _defaultInstance;
    public string instance { get; set; } = string.Empty;

    public string wikiName { get; } = "";
    private List<string> _nodes = new List<string>();
    private List<KeyValuePair<string, string>> _edges = new List<KeyValuePair<string, string>>();
    public DijkstraSolver defaultSolver { get; } = new DijkstraSolver();
    public ShortestPathVerifier defaultVerifier { get; } = new ShortestPathVerifier();
    public ShortestPathVisualization defaultVisualization { get; } = new ShortestPathVisualization();
    public UtilCollectionGraph graph { get; set; }
    public string[] contributors { get; } = { "Tiger Sant", "Malaya Witt", "Rajit Nilkar", "Scott Barfuss" };
    
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
    public SHORTESTPATH() : this(_defaultInstance) { }

    public SHORTESTPATH(string GInput)
    {
        instance = GInput;

        StringParser parser;
        try // First try parsing with weights
        {
            parser = new("{(N,E) | N is set, E subset {(e,w) | e is N unorderedcross N, w is int}}");
            parser.parse(GInput);

            nodes = parser["N"].ToList().Select(n => n.ToString()).ToList();
            edges = parser["E"].ToList().Select(edge =>
            {
                // edge is ({u,v}, w)
                var endpoints = edge[0].ToList();
                if (endpoints.Count == 1)
                {
                    string v = endpoints[0].ToString();
                    return new KeyValuePair<string, string>(v, v);
                    // Self-loop, treat as edge from v to itself
                }
                return new KeyValuePair<string, string>(endpoints[0].ToString(), endpoints[1].ToString());
            }).ToList();
        }
        catch // If parsing with weights fails, try parsing without weights
        {
            parser = new("{(N,E) | N is set, E subset N unorderedcross N}");
            parser.parse(GInput); // Try parsing without weights

            nodes = parser["N"].ToList().Select(n => n.ToString()).ToList();
            edges = parser["E"].ToList().Select(edge =>
            {
                var endpoints = edge.ToList();
                if (endpoints.Count == 1)
                {
                    // Self-loop, treat as edge from v to itself
                    string v = endpoints[0].ToString();
                    return new KeyValuePair<string, string>(v, v);
                }
                return new KeyValuePair<string, string>(endpoints[0].ToString(), endpoints[1].ToString());
            }).ToList(); // Assume unweighted if weights are not provided
        }

        // Build the graph from the parsed nodes and edges
        graph = new UtilCollectionGraph(parser["N"], parser["E"]);
    }
}