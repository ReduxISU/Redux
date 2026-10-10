using API.Interfaces;
using API.Interfaces.Graphs;
using API.Problems.NPComplete.NPC_TSP.Solvers;
using API.Problems.NPComplete.NPC_TSP.Verifiers;
using API.Problems.NPComplete.NPC_TSP.Visualizations;
using SPADE;


namespace API.Problems.NPComplete.NPC_TSP;

class TSP : IGraphProblem<TSPBruteForce, TSPVerifier, TSPDefaultVisualization, UtilCollectionGraph> {

    // --- Fields ---
    public string problemName { get; } = "Traveling Salesperson";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Travelling_salesman_problem";
    public string formalDefinition { get; } = "TSP = {<G,k> | G is a weighted graph where there is a path through every vertex whose weights add up to less than k}";
    public string problemDefinition { get; } = "";
    public string inputDescription { get; } = "G, a weighted graph of cities, and k, a maximum tour weight";
    public string outputDescription { get; } = "The shortest tour visiting every city exactly once";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };

    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    // The weighted graph, shared with Minimum Traveling Salesperson, whose instance is the same graph without K.
    public const string WeightedGraphGrammar = "N is set, E subset {(e, w) | e is N unorderedcross N, w is int}";
    public const string InstanceGrammar = "{((N,E),K) | " + WeightedGraphGrammar + ", K is int}";
    private static string _defaultInstance { get; } = "(({New York,Chicago,Denver,Los Angeles,Miami},{({New York,Chicago},790),({New York,Denver},1770),({New York,Los Angeles},2450),({New York,Miami},1280),({Chicago,Denver},1000),({Chicago,Los Angeles},2015),({Chicago,Miami},1370),({Denver,Los Angeles},1015),({Denver,Miami},2060),({Los Angeles,Miami},2745)}),8000)";
    public string defaultInstance { get; } = _defaultInstance;
    public string instanceFormat { get; } = $"Format: {InstanceGrammar} Example: {_defaultInstance} Parallel edges: when two edges join the same cities, the lightest one is used.";
    public string certificateFormat { get; } =
        $"Format: {TSPVerifier.CertificateGrammar} Example: {TSPVerifier.CertificateExample}";

    public string instance { get; set; } = string.Empty;

    private List<string> _nodes = new List<string>();
    private List<(string source, string target, int weight)> _edges = new List<(string source, string destination, int weight)>();
    private int _K;
    public TSPBruteForce defaultSolver { get; } = new TSPBruteForce();
    public TSPVerifier defaultVerifier { get; } = new TSPVerifier();
    public TSPDefaultVisualization defaultVisualization { get; } = new TSPDefaultVisualization();
    public UtilCollectionGraph graph { get; set; }
    // Declared, not derived. TSP (decision version) is NP-complete (Karp, 1972).
    public ComplexityClass complexityClass { get; } = ComplexityClass.NPComplete;
    public ProblemType problemType { get; } = ProblemType.NetworkDesign;

    public string wikiName { get; } = "";


    // --- Properties ---
    public List<string> nodes {
        get {
            return _nodes;
        }
        set {
            _nodes = value;
        }
    }
    public List<(string source, string target, int weight)> edges {
        get {
            return _edges;
        }
        set {
            _edges = value;
        }
    }

    /// <summary>
    /// The edges as listed in the instance, before parallel edges are merged into <see cref="edges"/>. Kept for
    /// pictures that show every listed weight; internal, so it is not part of the problem's JSON.
    /// </summary>
    internal List<(string source, string target, int weight)> listedEdges { get; private set; } = new();

    public int K {
        get {
            return _K;
        }
        set {
            _K = value;
        }
    }

    // --- Methods Including Constructors ---
    public TSP() : this(_defaultInstance) {

    }

    public TSP(string GInput) {
        instance = GInput;

        StringParser tsp = new(InstanceGrammar);
        tsp.parse(GInput);
        nodes = tsp["N"].ToList().Select(node => node.ToString()).ToList();
        listedEdges = ReadWeightedEdges(tsp["E"]);
        // A tour takes the cheapest way between two cities, so parallel edges merge into the lightest (#769).
        edges = ParallelEdges.Lightest(listedEdges);
        _K = int.Parse(tsp["K"].ToString());

        graph = new UtilCollectionGraph(tsp["N"], tsp["E"]);
    }

    /// <summary>The edges of a parsed <see cref="WeightedGraphGrammar"/> E, as (one end, other end, weight), in the order listed.</summary>
    public static List<(string source, string target, int weight)> ReadWeightedEdges(UtilCollection E) =>
        E.ToList().Select(edge => {
            List<UtilCollection> cast = edge[0].ToList();
            return (cast[0].ToString(), cast[1].ToString(), int.Parse(edge[1].ToString()));
        }).ToList();
}