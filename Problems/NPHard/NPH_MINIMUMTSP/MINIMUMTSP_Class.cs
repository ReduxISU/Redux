using API.Interfaces;
using API.Interfaces.Graphs;
using API.Problems.NPComplete.NPC_TSP;
using API.Problems.NPHard.NPH_MINIMUMTSP.Solvers;
using API.Problems.NPHard.NPH_MINIMUMTSP.Verifiers;
using API.Problems.NPHard.NPH_MINIMUMTSP.Visualizations;
using SPADE;

namespace API.Problems.NPHard.NPH_MINIMUMTSP;

/// <summary>
/// Minimum Traveling Salesperson: the optimization version of <see cref="TSP"/>. The instance is TSP's weighted graph
/// without K, and the answer is a cheapest tour. Split from TSP the same way Minimum Vertex Cover is split from
/// Vertex Cover (#765).
/// </summary>
class MINIMUMTSP : IGraphProblem<BruteForceMinimumTSP, MinimumTSPVerifier, MinimumTSPDefaultVisualization, UtilCollectionGraph> {

    // --- Fields ---
    public string problemName { get; } = "Minimum Traveling Salesperson";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Travelling_salesman_problem";
    public string formalDefinition { get; } = "MINIMUMTSP = {<G, T> | G is a weighted graph and T is a tour of G (a cycle that visits every vertex exactly once and returns to its start) whose total edge weight is minimized}";
    public string inputDescription { get; } = "G, a weighted graph of cities";
    public string outputDescription { get; } = "A cheapest tour: one that visits every city exactly once, returns to its start, and costs no more than any other tour";
    public string problemDefinition { get; } = "A salesperson must visit every city exactly once and then return home. Given a graph of cities whose edge weights are the costs of traveling between them, the problem is to find a tour (a route that visits every city exactly once and ends back at the city it started from) with the smallest total cost. Unlike Traveling Salesperson, there is no budget K: the answer is the cheapest tour itself.";
    public string source { get; } = "Dantzig, G.; Fulkerson, R.; Johnson, S. (1954). Solution of a Large-Scale Traveling-Salesman Problem. Journal of the Operations Research Society of America, 2(4), 393-410.";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://doi.org/10.1287/opre.2.4.393";
    public const string InstanceGrammar = "{(N,E) | " + TSP.WeightedGraphGrammar + "}";
    private static string _defaultInstance = "({a,b,c,d,e},{({a,b},3),({a,c},4),({a,d},2),({a,e},7),({b,c},4),({b,d},6),({b,e},3),({c,d},5),({c,e},8),({d,e},6)})";
    public string defaultInstance { get; } = _defaultInstance;
    public string instanceFormat { get; } = $"Weighted graph, shaped as (nodes, edges): {InstanceGrammar}. Each edge is ({{n1,n2}},w), an undirected pair of nodes and its integer weight. Parallel edges: when two edges join the same cities, the lightest one is used. Example: {_defaultInstance}";
    public string certificateFormat { get; } =
        $"Format: {MinimumTSPVerifier.CertificateGrammar} Example: {MinimumTSPVerifier.CertificateExample}";
    public string instance { get; set; } = string.Empty;
    private List<string> _nodes = new List<string>();
    private List<(string source, string target, int weight)> _edges = new List<(string source, string target, int weight)>();
    // The weight of the edge between two cities, keyed by the pair in ordinal order.
    private Dictionary<(string, string), int> _weights = new Dictionary<(string, string), int>();
    /// <summary>
    /// The edges as listed in the instance, before parallel edges are merged into <see cref="edges"/>. Kept for
    /// pictures that show every listed weight; internal, so it is not part of the problem's JSON.
    /// </summary>
    internal List<(string source, string target, int weight)> listedEdges { get; private set; } = new();
    public string wikiName { get; } = "";
    public BruteForceMinimumTSP defaultSolver { get; } = new BruteForceMinimumTSP();
    public MinimumTSPVerifier defaultVerifier { get; } = new MinimumTSPVerifier();
    public MinimumTSPDefaultVisualization defaultVisualization { get; } = new MinimumTSPDefaultVisualization();

    public UtilCollectionGraph graph { get; set; }

    public string[] contributors { get; } = { "Michael Trosper" };
    // Declared, not derived. Finding a cheapest tour is NP-hard: its cost answers TSP's question (is there a tour of cost
    // at most K?) for every K. No efficient check is known that a given tour is the cheapest, so it is not placed in NP.
    public ComplexityClass complexityClass { get; } = ComplexityClass.NPHard;
    public ProblemType problemType { get; } = ProblemType.NetworkDesign;
    // Variant (#616): asks for the cheapest tour itself, not whether one of cost at most K exists.
    public ProblemVariant problemVariant { get; } = ProblemVariant.Optimization;


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

    // --- Methods Including Constructors ---
    public MINIMUMTSP() : this(_defaultInstance) {

    }
    public MINIMUMTSP(string instanceInput) {
        if (string.IsNullOrWhiteSpace(instanceInput)) {
            throw new ProblemParseException("MINIMUMTSP", instanceInput, "instance is empty");
        }

        instance = instanceInput;

        StringParser minimumTsp = new(InstanceGrammar);
        try {
            minimumTsp.parse(instanceInput);
            nodes = minimumTsp["N"].ToList().Select(node => node.ToString()).ToList();
            RefuseLoops(minimumTsp["E"], instanceInput);
            listedEdges = TSP.ReadWeightedEdges(minimumTsp["E"]);
            // A tour takes the cheapest way between two cities, so parallel edges merge into the lightest (#769).
            edges = ParallelEdges.Lightest(listedEdges);
            graph = new UtilCollectionGraph(minimumTsp["N"], minimumTsp["E"]);
        } catch (Exception ex) when (ex is not ProblemParseException) {
            throw new ProblemParseException("MINIMUMTSP", instanceInput, ex.Message);
        }

        // Every edge joins two of the nodes (the grammar alone lets an edge name another city). Parallel edges are already
        // merged, so each pair has one weight. Loops were refused above.
        foreach (var (source, target, weight) in edges) {
            foreach (string end in new[] { source, target }) {
                if (!nodes.Contains(end)) {
                    throw new ProblemParseException("MINIMUMTSP", instanceInput, $"an edge names {end}, which is not one of the nodes");
                }
            }
            _weights[Key(source, target)] = weight;
        }
    }

    // SPADE reads the loop ({a,a},w) as ({a},w), which TSP's edge reader would fail on with an index error. Refused with
    // the same detail as TSP's (#882); this can use UtilCollectionGraph.RefuseLoops once both branches land.
    private void RefuseLoops(UtilCollection edges, string instanceInput) {
        foreach (UtilCollection edge in edges.ToList()) {
            List<UtilCollection> ends = edge[0].ToList();
            if (ends.Count == 1) {
                throw new ProblemParseException("MINIMUMTSP", instanceInput, $"edge {{{ends[0]},{ends[0]}}} joins a node to itself; {problemName} has no loops");
            }
        }
    }

    private static (string, string) Key(string a, string b) => string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a);

    /// <summary>The weight of the edge between two different cities, or null if no edge joins them.</summary>
    public int? Weight(string a, string b) =>
        a != b && _weights.TryGetValue(Key(a, b), out int weight) ? weight : null;
}
