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
    public string formalDefinition { get; } = "TSP = {<G,K> | G is a weighted graph with a tour (a cycle that visits every vertex exactly once and returns to its start) whose edge weights add up to at most K}";
    public string problemDefinition { get; } = "A salesperson must visit every city exactly once and then return home. Given a graph of cities whose edge weights are the costs of traveling between them, and a budget K, the question is whether some tour (a route that visits every city exactly once and ends back at the city it started from) costs at most K in total.";
    public string inputDescription { get; } = "G, a weighted graph of cities, and K, the most a tour may cost";
    public string outputDescription { get; } = "A tour that visits every city exactly once, returns to its start, and costs at most K, if there is one";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };

    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    // The weighted graph, shared with Minimum Traveling Salesperson, whose instance is the same graph without K.
    public const string WeightedGraphGrammar = "N is set, E subset {(e, w) | e is N unorderedcross N, w is int}";
    public const string InstanceGrammar = "{((N,E),K) | " + WeightedGraphGrammar + ", K is int}";
    private static string _defaultInstance { get; } = "(({New York,Los Angeles,Chicago,Miami,Denver},{({New York,Chicago},790),({New York,Denver},1770),({New York,Los Angeles},2450),({New York,Miami},1280),({Chicago,Denver},1000),({Chicago,Los Angeles},2015),({Chicago,Miami},1370),({Denver,Los Angeles},1015),({Denver,Miami},2060),({Los Angeles,Miami},2745)}),7000)";
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
    // Variant (#616): K is a bound the verifier checks (tour weight <= K), so Decision; TSPBranchAndBound searches for the optimum, but the class has no optimization form.
    public ProblemVariant problemVariant { get; } = ProblemVariant.Decision;

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
        UtilCollectionGraph.RefuseLoops(tsp["E"], problemName, GInput);
        listedEdges = ReadWeightedEdges(tsp["E"]);
        // A tour takes the cheapest way between two cities, so parallel edges merge into the lightest (#769).
        edges = ParallelEdges.Lightest(listedEdges);
        _K = int.Parse(tsp["K"].ToString());

        graph = new UtilCollectionGraph(tsp["N"], tsp["E"]);
    }

    /// <summary>
    /// The weight of the edge between two cities, or null if there is none. <see cref="edges"/> holds one edge per pair, the
    /// lightest of any parallel edges, so this is the weight the verifier counts and the picture draws. It scans the edge list.
    /// </summary>
    public int? Weight(string a, string b) {
        foreach (var e in edges)
            if (e.source == a && e.target == b) return e.weight;
        foreach (var e in edges)
            if (e.source == b && e.target == a) return e.weight;
        return null;
    }

    /// <summary>Whether two cities are joined by an edge.</summary>
    public bool HasEdge(string a, string b) => Weight(a, b) != null;

    /// <summary>The first step of a closed tour (<paramref name="closed"/> ends where it starts) that is not an edge, or null.</summary>
    public (string From, string To)? FirstMissingHop(IReadOnlyList<string> closed) {
        for (int i = 0; i + 1 < closed.Count; i++)
            if (Weight(closed[i], closed[i + 1]) == null) return (closed[i], closed[i + 1]);
        return null;
    }

    /// <summary>The total weight of a path's steps, or null if one of them is not an edge.</summary>
    public int? Cost(IReadOnlyList<string> path) {
        int total = 0;
        for (int i = 0; i + 1 < path.Count; i++) {
            int? w = Weight(path[i], path[i + 1]);
            if (w == null) return null;
            total += w.Value;
        }
        return total;
    }

    /// <summary>
    /// The edges of a parsed <see cref="WeightedGraphGrammar"/> E, as (one end, other end, weight), in the order listed. An
    /// edge written twice, even the other way round, is listed once (<see cref="ParallelEdges.Distinct"/>).
    /// </summary>
    public static List<(string source, string target, int weight)> ReadWeightedEdges(UtilCollection E) =>
        ParallelEdges.Distinct(E.ToList().Select(edge => {
            List<UtilCollection> cast = edge[0].ToList();
            return (cast[0].ToString(), cast[1].ToString(), int.Parse(edge[1].ToString()));
        }));
}