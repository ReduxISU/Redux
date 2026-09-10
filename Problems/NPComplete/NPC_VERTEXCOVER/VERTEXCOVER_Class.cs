using API.Interfaces;
using API.Problems.NPComplete.NPC_VERTEXCOVER.Solvers;
using API.Problems.NPComplete.NPC_VERTEXCOVER.Verifiers;
using API.Interfaces.Graphs;
using SPADE;
using API.Problems.NPComplete.NPC_VERTEXCOVER.Visualizations;

namespace API.Problems.NPComplete.NPC_VERTEXCOVER;

class VERTEXCOVER : IGraphProblem<VertexCoverBruteForce, VCVerifier, VertexCoverDefaultVisualization, UtilCollectionGraph> {

    // --- Fields ---
    public string problemName { get; } = "Vertex Cover";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Vertex_cover";
    public string formalDefinition { get; } = "VERTEXCOVER = {<G, k> | G in an undirected graph that has a k-node vertex cover}";
    public string problemDefinition { get; } = "A vertex cover is a subset of nodes S, such that every edge in the graph, G, touches a node in S.";
    public string source { get; } = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceLink { get; } = "https://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf";
    private static string _defaultInstance = "(({a,b,c,d,e},{{a,b},{a,c},{a,e},{b,e},{c,d}}),3)";
    public string defaultInstance { get; } = _defaultInstance;
    public string instanceFormat { get; } = "Graph and target cover size, shaped as ((nodes, edges), k). Nodes are a brace-delimited comma-separated list {n1,n2,...}; edges are a brace-delimited list of undirected pairs {{n1,n2},{n2,n3},...} drawn from the node set; k is the required vertex-cover size. Example: (({a,b,c,d},{{a,b},{a,c},{a,d}}),1)";
    public string certificateFormat { get; } = "Comma-separated node names, optionally wrapped in braces. Must name nodes from the instance's node set such that every edge has at least one endpoint in the set (a vertex cover). Example: {a}";
    public string instance { get; set; } = string.Empty;
    private List<string> _nodes = new List<string>();
    private List<KeyValuePair<string, string>> _edges = new List<KeyValuePair<string, string>>();
    private int _K = 3;
    public string wikiName { get; } = "";
    public VertexCoverBruteForce defaultSolver { get; } = new VertexCoverBruteForce();
    public VCVerifier defaultVerifier { get; } = new VCVerifier();
    public VertexCoverDefaultVisualization defaultVisualization { get; } = new VertexCoverDefaultVisualization();

    public UtilCollectionGraph graph { get; set; }
    private string _vertexCover = string.Empty;

    public string[] contributors { get; } = { "Janita Aamir", "Alex Diviney" };
    // Declared, not derived. VERTEXCOVER is NP-complete (Karp, 1972).
    public ComplexityClass complexityClass { get; } = ComplexityClass.NPComplete;
    public ProblemType problemType { get; } = ProblemType.GraphTheory;


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
        if (string.IsNullOrWhiteSpace(instanceInput)) {
            throw new ProblemParseException("VERTEXCOVER", instanceInput, "instance is empty");
        }

        instance = instanceInput;

        StringParser vertexCover = new("{((N,E),K) | N is set, E subset N unorderedcross N, K is int}");
        try {
            vertexCover.parse(instanceInput);
            nodes = vertexCover["N"].ToList().Select(node => node.ToString()).ToList();
            edges = vertexCover["E"].ToList().Select(edge => {
                List<UtilCollection> cast = edge.ToList();
                return new KeyValuePair<string, string>(cast[0].ToString(), cast[1].ToString());
            }).ToList();
            _K = int.Parse(vertexCover["K"].ToString());

            graph = new UtilCollectionGraph(vertexCover["N"], vertexCover["E"]);
        } catch (Exception ex) when (ex is not ProblemParseException) {
            throw new ProblemParseException("VERTEXCOVER", instanceInput, ex.Message);
        }
    }
}

