using API.Interfaces;
using API.Problems.NPComplete.NPC_CLIQUE.Visualizers;
using API.Problems.NPComplete.NPC_CLIQUECOVER.Solvers;
using API.Problems.NPComplete.NPC_CLIQUECOVER.Verifiers;
using API.Problems.NPComplete.NPC_CLIQUECOVER.Visualizations;
using Microsoft.AspNetCore.SignalR;
using SPADE;

namespace API.Problems.NPComplete.NPC_CLIQUECOVER;

class CLIQUECOVER : IGraphProblem<CliqueCoverBruteForce,CliqueCoverVerifier,CliqueCoverDefaultVisualization,UtilCollectionGraph> {

    // --- Fields ---
    public string problemName {get;} = "Clique Cover";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Clique_cover";
    public string formalDefinition {get;} = "Clique Cover = {<G, k> | G is a graph represnted by k or fewer cliques}";
    public string problemDefinition {get;} = "A clique cover is a partition of the vertices into cliques, subsets of vertices within which every two vertices are adjacent";
    public string source { get; } = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceLink { get; } = "https://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf";
    public static string _defaultInstance = "(({1,2,3,4,5,6,7,8},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5},{6,7},{7,8},{6,8}}),3)";
    public string defaultInstance { get; } = _defaultInstance;
    public string instance {get;set;} = string.Empty;

    public string wikiName {get;} = "";
    private List<string> _nodes = new List<string>();
    private List<KeyValuePair<string, string>> _edges = new List<KeyValuePair<string, string>>();
    private int _K = 3;
    public CliqueCoverBruteForce defaultSolver {get;} = new CliqueCoverBruteForce();
    public CliqueCoverVerifier defaultVerifier { get; } = new CliqueCoverVerifier();
    public CliqueCoverDefaultVisualization defaultVisualization { get; } = new CliqueCoverDefaultVisualization();
    public UtilCollectionGraph graph { get; }
    public string[] contributors {get;} = { "Andrija Sevaljevic" };

    // --- Properties ---
    public List<string> nodes {
        get {
            return _nodes;
        }
        set {
            _nodes = value;
        }
    }
    public List<KeyValuePair<string, string>> edges {
        get {
            return _edges;
        }
        set {
            _edges = value;
        }
    }

    public int K {
        get {
            return _K;
        }
        set {
            _K = value;
        }
    }

    // --- Methods Including Constructors ---
    public CLIQUECOVER() : this(_defaultInstance) {

    }
    public CLIQUECOVER(string GInput)
    {
        instance = GInput;

        StringParser cliqueGraph = new("{((N,E),K) | N is set, E subset N unorderedcross N, K is int}");
        cliqueGraph.parse(GInput);
        nodes = cliqueGraph["N"].ToList().Select(node => node.ToString()).ToList();
        edges = cliqueGraph["E"].ToList().Select(edge =>
        {
            List<UtilCollection> cast = edge.ToList();
            return new KeyValuePair<string, string>(cast[0].ToString(), cast[1].ToString());
        }).ToList();
        _K = int.Parse(cliqueGraph["K"].ToString());

        graph = new UtilCollectionGraph(cliqueGraph["N"], cliqueGraph["E"]);

    }
}