using API.Interfaces;
using API.Problems.NPComplete.NPC_WEIGHTEDCUT.Solvers;
using API.Problems.NPComplete.NPC_WEIGHTEDCUT.Verifiers;
using API.Problems.NPComplete.NPC_WEIGHTEDCUT.Visualizations;
using SPADE;

namespace API.Problems.NPComplete.NPC_WEIGHTEDCUT;

class WEIGHTEDCUT : IGraphProblem<WeightedCutBruteForce, WeightedCutVerifier, WeightedCutDefaultVisualization, UtilCollectionGraph>
{

    // --- Fields ---
    public string problemName { get; } = "Weighted Cut";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Cut_(graph_theory)";
    public string formalDefinition { get; } = "Weighted Cut = {<G, k> | G is a graph with cut of size k}";
    public string problemDefinition { get; } = "A weighted cut in an undirected graph is a partition of the graph's vertices into two complementary sets S and T, and the size of the cut is the sum of edge weights between S and T.";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };

    public string source { get; } = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceLink { get; } = "https://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf";
    public static string _defaultInstance { get; } = "(({1,2,3,4,5},{({2,1},5),({1,3},4),({2,3},2),({3,5},1),({2,4},4),({4,5},2)}),5)";
    public string defaultInstance { get; } = _defaultInstance;
    public string instance { get; set; } = string.Empty;

    private List<string> _nodes = new List<string>();
    private List<(string source, string destination, int weight)> _edges = new List<(string source, string destination, int weight)>();
    private int _K;
    public WeightedCutBruteForce defaultSolver { get; } = new WeightedCutBruteForce();
    public WeightedCutVerifier defaultVerifier { get; } = new WeightedCutVerifier();
    public WeightedCutDefaultVisualization defaultVisualization { get; } = new WeightedCutDefaultVisualization();
    public UtilCollectionGraph graph { get; set; }



    public string wikiName { get; } = "";
    //private List<List<string>> _clauses = new List<List<string>>();
    //private List<string> _literals = new List<string>();

    // --- Properties ---
    public List<string> nodes
    {
        get
        {
            return _nodes;
        }
        set
        {
            _nodes = value;
        }
    }
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

    public int K
    {
        get
        {
            return _K;
        }
        set
        {
            _K = value;
        }
    }

    // --- Methods Including Constructors ---
    public WEIGHTEDCUT() : this(_defaultInstance)
    {

    }
    public WEIGHTEDCUT(string GInput)
    {
        instance = GInput;

        StringParser weightedCut = new("{((N,E),K) | N is set, E subset {(e, w) | e is N unorderedcross N, w is int}, K is int}");
        weightedCut.parse(GInput);
        nodes = weightedCut["N"].ToList().Select(node => node.ToString()).ToList();
        edges = weightedCut["E"].ToList().Select(edge =>
        {
            List<UtilCollection> cast = edge[0].ToList();
            return (cast[0].ToString(), cast[1].ToString(), int.Parse(edge[1].ToString()));
        }).ToList();
        _K = int.Parse(weightedCut["K"].ToString());

        graph = new UtilCollectionGraph(weightedCut["N"], weightedCut["E"]);
    }
}
