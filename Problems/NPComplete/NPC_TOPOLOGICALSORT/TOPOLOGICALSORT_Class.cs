using API.Interfaces;
using API.Problems.NPComplete.NPC_TOPOLOGICALSORT.Solvers;
using API.Problems.NPComplete.NPC_TOPOLOGICALSORT.Verifiers;
using API.Problems.NPComplete.NPC_TOPOLOGICALSORT.Visualizations;
using SPADE;

namespace API.Problems.NPComplete.NPC_TOPOLOGICALSORT;

class TOPOLOGICALSORT : IGraphProblem<KahnsAlgorithm, TopologicalSortVerifier, TopologicalSortDefaultVisualization, UtilCollectionGraph>
{
    // --- Fields ---
    public string problemName { get; } = "Topological Sort";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Topological_sorting";
    public string formalDefinition { get; } = "TOPOLOGICALSORT = {<G> | G is a DAG with a valid linear ordering of vertices such that for every directed edge (u,v), u appears before v}";
    public string problemDefinition { get; } = "Topological Sort is the problem of arranging the vertices of a directed acyclic graph (DAG) into a linear sequence such that all directed edges point forward in the sequence. If the graph contains a cycle, no valid topological ordering exists.";
    public string source { get; } = "Kahn, A. B. (1962). Topological sorting of large networks. Communications of the ACM, 5(11), 558-562.";
    public string sourceLink { get; } = "https://dl.acm.org/doi/10.1145/368996.369025";
    private static string _defaultInstance = "({1,2,3,4,5,6},{(1,2),(1,3),(2,4),(3,4),(4,5),(3,6)})";
    public string defaultInstance { get; } = _defaultInstance;
    public string instanceFormat { get; } = "(N,E) where N is the set of node names and E is the set of directed edges as (from,to) pairs. Example: ({1,2,3,4,5,6},{(1,2),(1,3),(2,4),(3,4),(4,5),(3,6)})";
    public string certificateFormat { get; } = "Brace-wrapped, comma-separated linear ordering of all node names such that for every edge (u,v), u appears before v. Example: {1,2,3,4,5,6}";
    public string instance { get; set; } = string.Empty;
    public string wikiName { get; } = "";
    private List<string> _nodes = new List<string>();
    private List<KeyValuePair<string, string>> _edges = new List<KeyValuePair<string, string>>();
    public KahnsAlgorithm defaultSolver { get; } = new KahnsAlgorithm();
    public TopologicalSortVerifier defaultVerifier { get; } = new TopologicalSortVerifier();
    public TopologicalSortDefaultVisualization defaultVisualization { get; } = new TopologicalSortDefaultVisualization();
    public UtilCollectionGraph graph { get; set; }
    public string[] contributors { get; } = { "Pravesh Aryal", "Koras Koirala", "Dinesh Khanal" };
    // Declared, not derived from the Problems/NPComplete/ folder — Kahn's algorithm
    // solves this in polynomial time; it just lives under NPComplete/ for filing
    // reasons.
    public ComplexityClass complexityClass { get; } = ComplexityClass.P;

    // --- Properties ---
    public List<string> nodes
    {
        get { return _nodes; }
        set { _nodes = value; }
    }

    public List<KeyValuePair<string, string>> edges
    {
        get { return _edges; }
        set { _edges = value; }
    }

    // --- Constructors ---
    public TOPOLOGICALSORT() : this(_defaultInstance) { }

    public TOPOLOGICALSORT(string GInput)
    {
        instance = GInput;
        StringParser parser = new("{(N,E) | N is set, E subset N cross N}");
        parser.parse(GInput);
        nodes = parser["N"].ToList().Select(node => node.ToString()).ToList();
        edges = parser["E"].ToList().Select(edge =>
        {
            List<UtilCollection> cast = edge.ToList();
            return new KeyValuePair<string, string>(cast[0].ToString(), cast[1].ToString());
        }).ToList();
        graph = new UtilCollectionGraph(parser["N"], parser["E"]);
    }
}
