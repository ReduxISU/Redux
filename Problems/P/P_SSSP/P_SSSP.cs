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

class SSSP : IGraphProblem<SSSPSolver, SSSPVerifier, SSSPVisualization, UtilCollectionGraph>
{
    // --- Fields ---
    public string problemName { get; } = "Single Source Shortest Path Problem";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Shortest_path_problem";
    public string formalDefinition { get; } = "For a weighted graph G = (V,E), with non-negative edge weights, a source vertex s \u2208 V, find the shortest path distance from s to every other vertex v \u2208 V, where path length is defined as the sum of edge weights along the path.";
    public string problemDefinition { get; } = "Single Source Shortest Path (SSSP) in a weighted graph is the problem of determining the shortest path from a source vertex to all other reachable vertices in the graph such that the sum of edge weights along each path is minimized.";
    public string source { get; } = "N/A";
    public string sourceLink { get; } = "N/A";
    private static string _defaultInstance =
    "({1,2,3,4,5},{((1,2),4),((1,3),2),((2,3),1),((3,5),7),((2,4),3),((4,5),9)},1)";
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

    // --- Methods Including Constructor ---
    public SSSP() : this(_defaultInstance) { }

    public SSSP(string GInput)
    {
        throw new NotImplementedException("SSSP constructor to be implemented");
    }
}
