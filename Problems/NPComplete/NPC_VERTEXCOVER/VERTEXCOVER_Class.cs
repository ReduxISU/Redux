using API.Interfaces;
using API.Problems.NPComplete.NPC_VERTEXCOVER.Solvers;
using API.Problems.NPComplete.NPC_VERTEXCOVER.Verifiers;
using API.Interfaces.Graphs;
using SPADE;
using API.Problems.NPComplete.NPC_VERTEXCOVER.Visualizations;

namespace API.Problems.NPComplete.NPC_VERTEXCOVER;

class VERTEXCOVER : IGraphProblem<VertexCoverBruteForce,VCVerifier,VertexCoverDefaultVisualization,UtilCollectionGraph> {

    // --- Fields ---
    public string problemName {get;} = "Vertex Cover";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Vertex_cover";
    public string formalDefinition {get;} = "VERTEXCOVER = {<G, k> | G in an undirected graph that has a k-node vertex cover}";
    public string problemDefinition {get;} = "A vertex cover is a subset of nodes S, such that every edge in the graph, G, touches a node in S.";
    public string source {get;} = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceLink { get; } = "https://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf";
    private static string _defaultInstance = "(({a,b,c,d,e},{{a,b},{a,c},{a,e},{b,e},{c,d}}),3)";
    public string defaultInstance { get; } = _defaultInstance;
    public string instance {get;set;} = string.Empty;
    private List<string> _nodes = new List<string>();
    private List<KeyValuePair<string, string>> _edges = new List<KeyValuePair<string, string>>();
    private int _K = 3;
    public string wikiName {get;} = "";
    public VertexCoverBruteForce defaultSolver {get;} = new VertexCoverBruteForce();
    public VCVerifier defaultVerifier { get; } = new VCVerifier();
    public VertexCoverDefaultVisualization defaultVisualization { get; } = new VertexCoverDefaultVisualization();

    public UtilCollectionGraph graph { get; set; }
    private string _vertexCover = string.Empty;

    public string[] contributors {get;} = { "Janita Aamir", "Alex Diviney" };


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
    public VERTEXCOVER() : this(_defaultInstance) {

    }
    public VERTEXCOVER(string instanceInput) {
        instance = instanceInput;

        StringParser vertexCover = new("{((N,E),K) | N is set, E subset N unorderedcross N, K is int}");
        vertexCover.parse(instanceInput);
        nodes = vertexCover["N"].ToList().Select(node => node.ToString()).ToList();
        edges = vertexCover["E"].ToList().Select(edge =>
        {
            List<UtilCollection> cast = edge.ToList();
            return new KeyValuePair<string, string>(cast[0].ToString(), cast[1].ToString());
        }).ToList();
        _K = int.Parse(vertexCover["K"].ToString());

        graph = new UtilCollectionGraph(vertexCover["N"], vertexCover["E"]);
    }
}

