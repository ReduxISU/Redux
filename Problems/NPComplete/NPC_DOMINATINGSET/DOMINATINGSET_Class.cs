using API.Interfaces;
using API.Problems.NPComplete.NPC_DOMINATINGSET.Solvers;
using API.Problems.NPComplete.NPC_DOMINATINGSET.Verifiers;
using API.Problems.NPComplete.NPC_DOMINATINGSET.Visualizers;
using SPADE;

namespace API.Problems.NPComplete.NPC_DOMINATINGSET;

class DOMINATINGSET
    : IGraphProblem<
        DominatingSetSolver,
        DominatingSetVerifier,
        DominatingSetDefaultVisualization,
        UtilCollectionGraph
    >
{
    // --- Properties ---
    public string problemName { get; } = "Dominating Set";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Dominating_set";
    public string formalDefinition { get; } =
        "Dominating Set = {<G, k> | G is a graph with a dominating set less than or equal to k}";
    public string problemDefinition { get; } =
        "A dominating set of a graph G is a subset D of the vertices of G such that every vertex v of G is either in the set D or v has at least one neighbour that is in D.";
    public string[] contributors { get; } = { "Quinton Smith" };

    public string source { get; } =
        "https://webhome.cs.uvic.ca/~wendym/courses/425/14/notes/425_03_dom_alg.pdf";
    public string sourceLink { get; } =
        "https://webhome.cs.uvic.ca/~wendym/courses/425/14/notes/425_03_dom_alg.pdf";
    private static string _defaultInstance =
        "(({0,1,2,3,4},{{1,0},{0,3},{1,2},{2,4},{1,3},{3,4},{4,1}}),2)";
    public string defaultInstance { get; } = _defaultInstance;
    public string instance { get; set; } = string.Empty;

    private List<string> _nodes = new List<string>();
    private List<KeyValuePair<string, string>> _edges = new List<KeyValuePair<string, string>>();

    public List<KeyValuePair<string, string>> edges
    {
        get { return _edges; }
        set { _edges = value; }
    }

    private int _K;
    public DominatingSetSolver defaultSolver { get; } = new DominatingSetSolver();
    public DominatingSetVerifier defaultVerifier { get; } = new DominatingSetVerifier();
    public DominatingSetDefaultVisualization defaultVisualization { get; } =
        new DominatingSetDefaultVisualization();

    public UtilCollectionGraph graph { get; set; }

    public string wikiName { get; } = string.Empty;

    public List<string> nodes
    {
        get { return _nodes; }
        set { _nodes = value; }
    }

    public int K
    {
        get { return _K; }
        set { _K = value; }
    }

    // --- Constructors ---
    public DOMINATINGSET()
        : this(_defaultInstance) { }

    public DOMINATINGSET(string GInput)
    {
        instance = GInput;

        StringParser dominatingSetGraph = new(
            "{((N,E),K) | N is set, E subset N unorderedcross N, K is int}"
        );
        dominatingSetGraph.parse(GInput);
        nodes = dominatingSetGraph["N"].ToList().Select(node => node.ToString()).ToList();
        edges = dominatingSetGraph["E"]
            .ToList()
            .Select(edge =>
            {
                List<UtilCollection> cast = edge.ToList();
                return new KeyValuePair<string, string>(cast[0].ToString(), cast[1].ToString());
            })
            .ToList();
        _K = int.Parse(dominatingSetGraph["K"].ToString());

        graph = new UtilCollectionGraph(dominatingSetGraph["N"], dominatingSetGraph["E"]);
    }
}
