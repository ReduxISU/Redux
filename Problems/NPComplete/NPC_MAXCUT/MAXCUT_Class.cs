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
    public string[] contributors {get;} = {"Max Gruenwoldt"};
    
    public string source {get;} = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceLink { get; } = "https://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf";
    private static string _defaultInstance = "({1,2,3,4,5},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5}})";
    public string defaultInstance { get; } = _defaultInstance;
    public string instance {get;set;} = string.Empty;
    
    private List<string> _nodes = new List<string>();
    private List<KeyValuePair<string, string>> _edges = new List<KeyValuePair<string, string>>();
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
    public List<KeyValuePair<string, string>> edges {
        get {
            return _edges;
        }
        set {
            _edges = value;
        }
    }

    // --- Methods Including Constructors ---
    public MAXCUT() : this(_defaultInstance) {

    }
    public MAXCUT(string GInput) {
        instance = GInput;

        StringParser maxcut = new("{(N,E) | N is set, E subset N unorderedcross N}");
        maxcut.parse(GInput);
        nodes = maxcut["N"].ToList().Select(node => node.ToString()).ToList();
        edges = maxcut["E"].ToList().Select(edge =>
        {
            List<UtilCollection> cast = edge.ToList();
            return new KeyValuePair<string, string>(cast[0].ToString(), cast[1].ToString());
        }).ToList();
        graph = new UtilCollectionGraph(maxcut["N"], maxcut["E"]);
    }
}