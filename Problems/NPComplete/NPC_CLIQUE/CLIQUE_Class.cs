using API.Interfaces;
using API.Problems.NPComplete.NPC_CLIQUE.Solvers;
using API.Problems.NPComplete.NPC_CLIQUE.Verifiers;
using API.Problems.NPComplete.NPC_CLIQUE.Visualizers;
using SPADE;

namespace API.Problems.NPComplete.NPC_CLIQUE;

class CLIQUE : IGraphProblem<CliqueBruteForce,CliqueVerifier,cliqueDefaultVisualization,UtilCollectionGraph> {

    // --- Fields ---
    public string problemName {get;} = "Clique";
    public string problemLink {get;} = "https://en.wikipedia.org/wiki/Clique";
    public string formalDefinition {get;} = "Clique = {<G, k> | G is an graph that has a set of k mutually adjacent nodes}";
    public string problemDefinition {get;} = "A clique is the problem of uncovering a subset of vertices in an undirected graph G = (V, E) such that every two distinct vertices are adjacent";
    public string source { get; } = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceLink { get; } = "https://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf";
    private static string _defaultInstance = "(({1,2,3,4},{{4,1},{1,2},{4,3},{3,2},{2,4}}),3)"; 
    public string defaultInstance {get;} = _defaultInstance;
    public string instance {get;set;} = string.Empty;
    public string wikiName {get;} = "";
    private List<string> _nodes = new List<string>();
    private List<KeyValuePair<string, string>> _edges = new List<KeyValuePair<string, string>>();
    private int _K ;
    public CliqueBruteForce defaultSolver {get;} = new CliqueBruteForce();
    public CliqueVerifier defaultVerifier { get; } = new CliqueVerifier();
    public cliqueDefaultVisualization defaultVisualization { get; } = new cliqueDefaultVisualization();
    public UtilCollectionGraph graph { get; set; }
    public string[] contributors {get;} = { "Kaden Marchetti", "Alex Diviney" };

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
    public CLIQUE() : this(_defaultInstance) {

    }
    public CLIQUE(string GInput)
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