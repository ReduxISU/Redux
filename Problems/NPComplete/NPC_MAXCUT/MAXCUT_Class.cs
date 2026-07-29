using API.Interfaces;
using API.Problems.NPComplete.NPC_MAXCUT.Solvers;
using API.Problems.NPComplete.NPC_MAXCUT.Verifiers;
using API.Problems.NPComplete.NPC_MAXCUT.Visualizations;
using SPADE;

namespace API.Problems.NPComplete.NPC_MAXCUT;

class MAXCUT : IGraphProblem<MaxCutSolver, MaxCutVerifier, MaxCutVisualization, UtilCollectionGraph>
{
    public string problemName { get; } = "Max Cut";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Maximum_cut";
    public string formalDefinition { get; } = "MaxCut = {<G> | G is a weighted undirected graph} — find the partition of V into non-empty S and T maximizing the total weight of edges between S and T.";
    public string problemDefinition { get; } = "Given a weighted undirected graph, find a partition of the vertices into two non-empty sets S and T such that the total weight of edges crossing the partition is maximized.";
    public string[] contributors { get; } = { "Max Gruenwoldt", "Eric Hill", "Michael Trosper" };
    public string source { get; } = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceLink { get; } = "https://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf";
    public static string _defaultInstance { get; } = "({1,2,3,4,5},{({2,1},5),({1,3},4),({2,3},2),({3,5},1),({2,4},4),({4,5},2)})";
    public string defaultInstance { get; } = _defaultInstance;
    public string instance { get; set; } = string.Empty;
    public string wikiName { get; } = "";
    public string instanceFormat { get; } = "Weighted undirected graph: ({nodes},{({u,v},w),...}) — e.g. ({1,2,3},{({1,2},4),({2,3},2),({1,3},3)})";
    public string certificateFormat { get; } = "Set of vertices on the S side of the maximum cut: {node,...} — e.g. {1,3}";

    private List<string> _nodes = new();
    private List<(string source, string destination, int weight)> _edges = new();

    public MaxCutSolver defaultSolver { get; } = new MaxCutSolver();
    public MaxCutVerifier defaultVerifier { get; } = new MaxCutVerifier();
    public MaxCutVisualization defaultVisualization { get; } = new MaxCutVisualization();
    public UtilCollectionGraph graph { get; set; }
    // Declared, not derived. MAXCUT is NP-complete (Karp, 1972) — distinct from
    // min-cut (P_MINCUT), which is in P.
    public ComplexityClass complexityClass { get; } = ComplexityClass.NPComplete;

    public List<string> nodes { get => _nodes; set => _nodes = value; }
    public List<(string source, string destination, int weight)> edges { get => _edges; set => _edges = value; }

    public MAXCUT() : this(_defaultInstance) { }

    public MAXCUT(string instanceString)
    {
        instance = instanceString;
        StringParser maxCut = new("{(N,E) | N is set, E subset {(e, w) | e is N unorderedcross N, w is int}}");
        maxCut.parse(instance);
        nodes = maxCut["N"].ToList().Select(node => node.ToString()).ToList();
        edges = maxCut["E"].ToList().Select(edge =>
        {
            List<UtilCollection> cast = edge[0].ToList();
            return (cast[0].ToString(), cast[1].ToString(), int.Parse(edge[1].ToString()));
        }).ToList();
        graph = new UtilCollectionGraph(maxCut["N"], maxCut["E"]);
    }
}
