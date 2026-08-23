using System.Runtime.Serialization;
using API.Interfaces;
using API.Problems.NPComplete.NPC_STEINERTREE.Solvers;
using API.Problems.NPComplete.NPC_STEINERTREE.Verifiers;
using API.Problems.NPComplete.NPC_STEINERTREE.Visualizations;
using SPADE;

namespace API.Problems.NPComplete.NPC_STEINERTREE;

class STEINERTREE : IGraphProblem<SteinerTreeBruteForce, SteinerTreeVerifier, SteinerTreeDefaultVisualization, UtilCollectionGraph> {

    // --- Fields ---
    public string problemName {get;} = "Steiner Tree";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Steiner_tree_problem";
    public string formalDefinition {get;} = "Steiner = {<G,R,k> | G has a subtree of weight <= k containing the set of nodes in R}";
    public string problemDefinition {get;} = "Steiner tree problem in graphs requires a tree of minimum weight that contains all terminals";
    public string source {get;} = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceLink { get; } = "https://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf";
    public static string _defaultInstance = "(({1,2,3,4,5,6,7,8},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5},{6,7},{7,8},{6,8},{6,1}}),{5,2,8},6)";
    public string defaultInstance { get; } = _defaultInstance;
    public string instanceFormat { get; } = "((N,E),R,K) where N is the set of node names, E is the set of undirected edges as {node,node} pairs, R is the set of terminal nodes that must be connected, and K is the maximum allowed tree weight. Example: (({1,2,3,4,5,6,7,8},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5},{6,7},{7,8},{6,8},{6,1}}),{5,2,8},6)";
    public string certificateFormat { get; } = "Brace-wrapped, comma-separated list of {node,node} edges forming a connected subgraph that includes every terminal node in R. Example: {{8,6},{6,1},{1,2},{2,3},{3,5}}";
    public string instance {get;set;} = string.Empty;

    public string wikiName {get;} = "";
    private int _K;
    private List<string> _nodes = new List<string>();
    private List<string> _terminals = new List<string>();
    private List<KeyValuePair<string, string>> _edges = new List<KeyValuePair<string, string>>();
    public SteinerTreeBruteForce defaultSolver {get;} = new SteinerTreeBruteForce();
    public SteinerTreeVerifier defaultVerifier {get;} = new SteinerTreeVerifier();
    public SteinerTreeDefaultVisualization defaultVisualization { get; } = new SteinerTreeDefaultVisualization();
    public UtilCollectionGraph graph { get; set; }
    public string[] contributors {get;} = { "Andrija Sevaljevic" };
    // Declared, not derived. STEINERTREE is NP-complete (Karp, 1972).
    public ComplexityClass complexityClass { get; } = ComplexityClass.NPComplete;

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
    public List<string> terminals
    {
        get
        {
            return _terminals;
        }
        set
        {
            _terminals = value;
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
    public int K
    {
        get
        {
            return _K;
        }
        set
        {
            _K = value;
        }
    }

    // --- Methods Including Constructors ---
    public STEINERTREE() : this(_defaultInstance)
    {

    }
    public STEINERTREE(string GInput)
    {
        instance = GInput;

        StringParser steinerTreeGraph = new("{((N,E),R,K) | N is set, E subset N unorderedcross N, R is set, K is int}");
        steinerTreeGraph.parse(GInput);
        nodes = steinerTreeGraph["N"].ToList().Select(node => node.ToString()).ToList();
        edges = steinerTreeGraph["E"].ToList().Select(edge =>
        {
            List<UtilCollection> cast = edge.ToList();
            return new KeyValuePair<string, string>(cast[0].ToString(), cast[1].ToString());
        }).ToList();
        terminals = steinerTreeGraph["R"].ToList().Select(node => node.ToString()).ToList();
        _K = int.Parse(steinerTreeGraph["K"].ToString());

        graph = new UtilCollectionGraph(steinerTreeGraph["N"], steinerTreeGraph["E"]);
    }
}