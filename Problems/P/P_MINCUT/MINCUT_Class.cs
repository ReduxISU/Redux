using API.Interfaces;
using API.Interfaces.Graphs;
using API.Problems.P.P_MINCUT.Solvers;
using API.Problems.P.P_MINCUT.Verifiers;
using API.Problems.P.P_MINCUT.Visualizations;
using SPADE;

namespace API.Problems.P.P_MINCUT;

class MINCUT : IGraphProblem<MinCutStoerWagner, MinCutVerifier, MinCutVisualization, UtilCollectionGraph> {
    public string problemName { get; } = "Minimum Cut";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Minimum_cut";
    public string formalDefinition { get; } = "MinCut = {<G> | G is a weighted undirected graph} — find the partition of V into non-empty S and T minimizing the total weight of edges between S and T.";
    public string problemDefinition { get; } = "Given a weighted undirected graph, find a partition of the vertices into two non-empty sets S and T such that the total weight of edges crossing the partition is minimized.";
    public string inputDescription { get; } = "A weighted undirected graph";
    public string outputDescription { get; } = "The set of edges crossing a minimum-weight cut of the graph";
    public string[] contributors { get; } = { "Michael Trosper" };
    // Declared, not derived. Global minimum cut is solvable in polynomial time
    // (Stoer-Wagner).
    public ComplexityClass complexityClass { get; } = ComplexityClass.P;
    public ProblemType problemType { get; } = ProblemType.NetworkDesign;
    public string source { get; } = "Stoer, Mechthild, and Frank Wagner. \"A simple min-cut algorithm.\" Journal of the ACM 44, no. 4 (1997): 585-591.";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://dl.acm.org/doi/10.1145/263867.263872";
    public static string _defaultInstance { get; } = "({1,2,3,4,5},{({2,1},5),({1,3},4),({2,3},2),({3,5},1),({2,4},4),({4,5},2)})";
    public string defaultInstance { get; } = _defaultInstance;
    public string instance { get; set; } = string.Empty;
    public string wikiName { get; } = "";
    public string instanceFormat { get; } = "Weighted undirected graph: ({nodes},{({u,v},w),...}) — e.g. ({1,2,3},{({1,2},4),({2,3},2),({1,3},3)}). Parallel edges: when two edges join the same nodes, their weights are added, and a certificate lists the pair once with the total. An edge written twice with the same weight, either way round, counts once.";
    public string certificateFormat { get; } = "Set of cut edges with weights: {({u,v},w),...} — e.g. {({1,2},4),({1,3},3)}";

    private List<string> _nodes = new();
    private List<(string source, string destination, int weight)> _edges = new();

    public MinCutStoerWagner defaultSolver { get; } = new MinCutStoerWagner();
    public MinCutVerifier defaultVerifier { get; } = new MinCutVerifier();
    public MinCutVisualization defaultVisualization { get; } = new MinCutVisualization();
    public UtilCollectionGraph graph { get; set; }

    public List<string> nodes { get => _nodes; set => _nodes = value; }
    public List<(string source, string destination, int weight)> edges { get => _edges; set => _edges = value; }

    /// <summary>
    /// The edges as listed in the instance, before parallel edges are merged into <see cref="edges"/>. Kept for
    /// pictures that show every listed weight; internal, so it is not part of the problem's JSON.
    /// </summary>
    internal List<(string source, string destination, int weight)> listedEdges { get; private set; } = new();

    public MINCUT() : this(_defaultInstance) { }

    public MINCUT(string instanceString) {
        instance = instanceString;
        StringParser parser = new("{(N,E) | N is set, E subset {(e, w) | e is N unorderedcross N, w is int}}");
        parser.parse(instanceString);
        nodes = parser["N"].ToList().Select(n => n.ToString()).ToList();
        UtilCollectionGraph.RefuseLoops(parser["E"], problemName, instanceString);
        listedEdges = ParallelEdges.Distinct(parser["E"].ToList().Select(edge => {
            List<UtilCollection> endpoints = edge[0].ToList();
            return (endpoints[0].ToString(), endpoints[1].ToString(), int.Parse(edge[1].ToString()));
        }));
        // Every parallel edge crosses a cut that separates its ends, so their weights add (#769).
        edges = ParallelEdges.Summed(listedEdges);
        graph = new UtilCollectionGraph(parser["N"], parser["E"]);
    }
}
