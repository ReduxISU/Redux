using API.Interfaces;
using API.Problems.NPComplete.NPC_MAXCUT.Solvers;
using API.Problems.NPComplete.NPC_MAXCUT.Verifiers;
using API.Problems.NPComplete.NPC_MAXCUT.Visualizations;
using SPADE;

namespace API.Problems.NPComplete.NPC_MAXCUT;

class MAXCUT : IGraphProblem<MaxCutSolver, MaxCutVerifier, MaxCutVisualization, UtilCollectionGraph> {

    // --- Fields ---
    public string problemName {get;} = "Max Cut";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Maximum_cut";
    public string formalDefinition {get;} = "Max Cut = {<G> | G is a graph}";
    public string problemDefinition {get;} = "A maximum cut in an undirected (possibly weighted) graph is a partition of the graphs vertices into two complementary sets S and T such that the total number (or total weight) of edges between S and T is maximized. The goal of the Max Cut problem is to find such a partition.";
    public string[] contributors {get;} = {"Max Gruenwoldt", "Eric Hill"};
    
    public string source {get;} = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceLink { get; } = "https://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf";
    // private static string _defaultInstance = "({1,2,3,4,5},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5}})";
    public static string _defaultInstance { get; } = "({1,2,3,4,5},{({2,1},5),({1,3},4),({2,3},2),({3,5},1),({2,4},4),({4,5},2)})";
    public string defaultInstance { get; } = _defaultInstance;
    public string instance {get;set;} = string.Empty;
    
    private List<string> _nodes = new List<string>();
    // private List<KeyValuePair<string, string>> _edges = new List<KeyValuePair<string, string>>();
    private List<(string source, string destination, int weight)> _edges = new List<(string source, string destination, int weight)>();
    private int _K;
    public MaxCutSolver defaultSolver {get;} = new MaxCutSolver();
    public MaxCutVerifier defaultVerifier {get;} = new MaxCutVerifier();
    public MaxCutVisualization defaultVisualization { get; } = new MaxCutVisualization();
    public UtilCollectionGraph graph { get; set; }
    
    public string wikiName {get;} = "";
  

    // --- Properties ---
    public List<string> nodes {
        get {
            return _nodes;
        }
        set {
            _nodes = value;
        }
    }
    // public List<KeyValuePair<string, string>> edges {
    //     get {
    //         return _edges;
    //     }
    //     set {
    //         _edges = value;
    //     }
    // }

    public List<(string source, string destination, int weight)> edges
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

    // --- Methods Including Constructors ---
    public MAXCUT() : this(_defaultInstance) {

    }
    public MAXCUT(string GInput) {
        instance = GInput;

        // StringParser maxcut = new("{(N,E) | N is set, E subset N unorderedcross N}");
        // maxcut.parse(GInput);
        // nodes = maxcut["N"].ToList().Select(node => node.ToString()).ToList();
        // edges = maxcut["E"].ToList().Select(edge =>
        // {
        //     List<UtilCollection> cast = edge.ToList();
        //     return new KeyValuePair<string, string>(cast[0].ToString(), cast[1].ToString());
        // }).ToList();
        // graph = new UtilCollectionGraph(maxcut["N"], maxcut["E"]);

        StringParser weightedCut = new("{(N,E) | N is set, E subset {(e, w) | e is N unorderedcross N, w is int}}");
        // StringParser weightedCut = new("{(N,E) | N is set, E subset {(e, w) | e subset {(j,k) | j is N unorderedcross N, k is N unorderedcross N}, w is int}}");
        Console.WriteLine($"Parsing instance: {instance}");
        weightedCut.parse(GInput);
        nodes = weightedCut["N"].ToList().Select(node => node.ToString()).ToList();
        Console.WriteLine($"Parsed Nodes:");
        foreach (var node in nodes)
        {
            Console.WriteLine($"{node}");
        }
        edges = weightedCut["E"].ToList().Select(edge =>
        {
            List<UtilCollection> cast = edge[0].ToList();
            return (cast[0].ToString(), cast[1].ToString(), int.Parse(edge[1].ToString()));
        }).ToList();
        Console.WriteLine($"Parsed Edges:");
        foreach (var edge in edges)
        {
            Console.WriteLine($"{edge}");
        }
        // _K = int.Parse(weightedCut["K"].ToString());

        graph = new UtilCollectionGraph(weightedCut["N"], weightedCut["E"]);
    }
}