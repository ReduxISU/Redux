using API.Interfaces;
using API.Problems.NPHard.NPH_MINIMUMVERTEXCOVER.Solvers;
using API.Problems.NPHard.NPH_MINIMUMVERTEXCOVER.Verifiers;
using API.Interfaces.Graphs;
using SPADE;
using API.Problems.NPHard.NPH_MINIMUMVERTEXCOVER.Visualizations;

namespace API.Problems.NPHard.NPH_MINIMUMVERTEXCOVER;

class MINIMUMVERTEXCOVER : IGraphProblem<BruteForceMinimumVertexCover, MinimumVertexCoverVerifier, MinimumVertexCoverDefaultVisualization, UtilCollectionGraph> {

    // --- Fields ---
    public string problemName { get; } = "Minimum Vertex Cover";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Vertex_cover";
    public string formalDefinition { get; } = "MINIMUMVERTEXCOVER = {<G, C> | G is an undirected graph and C is a vertex cover of G such that |C| is minimized}";
    public string inputDescription { get; } = "G, a graph";
    public string outputDescription { get; } = " Minimum subset of nodes C that forms a vertex cover of G";
    public string problemDefinition { get; } = "A vertex cover is a subset of nodes C, such that every edge in the graph, G, touches a node in C. A minimal vertex cover is the smallest possible subset C.";
    public string source { get; } = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceLink { get; } = "https://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf";
    private static string _defaultInstance = "({a,b,c,d,e},{{a,b},{a,c},{a,e},{b,e},{c,d}})";
    public string defaultInstance { get; } = _defaultInstance;
    public string instanceFormat { get; } = "Graph, shaped as (nodes, edges). Nodes are a brace-delimited comma-separated list {n1,n2,...}; edges are a brace-delimited list of undirected pairs {{n1,n2},{n2,n3},...} drawn from the node set. Example: ({a,b,c,d},{{a,b},{a,c},{a,d}})";
    public string certificateFormat { get; } = "Comma-separated node names, optionally wrapped in braces. Must name nodes from the instance's node set such that every edge has at least one endpoint in the set (a vertex cover). Example: {a}";
    public string instance { get; set; } = string.Empty;
    private List<string> _nodes = new List<string>();
    private List<KeyValuePair<string, string>> _edges = new List<KeyValuePair<string, string>>();
    public string wikiName { get; } = "";
    public BruteForceMinimumVertexCover defaultSolver { get; } = new BruteForceMinimumVertexCover();
    public MinimumVertexCoverVerifier defaultVerifier { get; } = new MinimumVertexCoverVerifier();
    public MinimumVertexCoverDefaultVisualization defaultVisualization { get; } = new MinimumVertexCoverDefaultVisualization();

    public UtilCollectionGraph graph { get; set; }

    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public ComplexityClass complexityClass { get; } = ComplexityClass.NPHard;
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

    // --- Methods Including Constructors ---
    public MINIMUMVERTEXCOVER() : this(_defaultInstance) {

    }
    public MINIMUMVERTEXCOVER(string instanceInput) {
        if (string.IsNullOrWhiteSpace(instanceInput)) {
            throw new ProblemParseException("MINIMUMVERTEXCOVER", instanceInput, "instance is empty");
        }

        instance = instanceInput;

        StringParser vertexCover = new("{(N,E) | N is set, E subset N unorderedcross N}");
        try {
            vertexCover.parse(instanceInput);
            nodes = vertexCover["N"].ToList().Select(node => node.ToString()).ToList();
            edges = vertexCover["E"].ToList().Select(edge => {
                List<UtilCollection> cast = edge.ToList();
                return new KeyValuePair<string, string>(cast[0].ToString(), cast[1].ToString());
            }).ToList();

            graph = new UtilCollectionGraph(vertexCover["N"], vertexCover["E"]);
        } catch (Exception ex) when (ex is not ProblemParseException) {
            throw new ProblemParseException("MINIMUMVERTEXCOVER", instanceInput, ex.Message);
        }
    }
}

