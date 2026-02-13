using API.Interfaces;
using API.Problems.NPComplete.NPC_SHORTESTPATH.Solvers;
using API.Problems.NPComplete.NPC_SHORTESTPATH.Verifiers;
using API.Problems.NPComplete.NPC_SHORTESTPATH.Visualizations;
using SPADE;
using Xunit;
using System.Linq;
using System.Collections.Generic;
using API.Interfaces.Graphs;

namespace API.Problems.NPComplete.NPC_SHORTESTPATH;

public class SHORTESTPATH: IGraphProblem<DijkstraSolver, ShortestPathVerifier, ShortestPathVisualization, UtilCollectionGraph>
{

    // --- Fields ---
    public string problemName { get; } = "Shortest Path Problem";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Shortest_path_problem";
    public string formalDefinition { get; } = "For a weighted graph G= (V,E) with weight function w:E->\\(\\mathbb{R}\\), find the shortest path source s to target t, where path length is the sum of edge weights"; //TODO figure out what it's supposed to be
    public string problemDefinition { get; } = "Shortest Path Problem is the problem of determining the shortest path from a given source vertex to all other vertices such that the sum of the weights of its inherent edges is minimized.";
    public string source { get; } = "N/A";
    public string sourceLink { get; } = "N/A";
    private static string _defaultInstance = "({1,2,3,4,5},{{2,1,4},{1,3,2},{2,3,1},{3,5,7},{2,4,3},{4,5,9}})";
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
        get
        {
            return _nodes;
        }
        set
        {
            _nodes = value;
        }
    }
    public List<KeyValuePair<string, string>> edges
    {
        get
        {
            return _edges;
        }
        set
        {
            _edges = value;
        }
    }

    // --- Methods Including Constructors ---
    public SHORTESTPATH() : this(_defaultInstance)
    {

    }
    public SHORTESTPATH(string GInput)
    {
        instance = GInput;

        StringParser parser = new("{(N,E) | N is set, E subset N unorderedcross N}");
        parser.parse(GInput);
        nodes = parser["N"].ToList().Select(node => node.ToString()).ToList();
        edges = parser["E"].ToList().Select(edge =>
        {
            List<UtilCollection> cast = edge.ToList();
            return new KeyValuePair<string, string>(cast[0].ToString(), cast[1].ToString());
        }).ToList();

        graph = new UtilCollectionGraph(parser["N"], parser["E"]);
    }
}