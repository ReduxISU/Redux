using API.Interfaces;
using API.Interfaces.Graphs;
using API.Problems.P.P_STRONGLYCONNECTEDCOMPONENTS.Solvers;
using API.Problems.P.P_STRONGLYCONNECTEDCOMPONENTS.Verifiers;
using API.DummyClasses;
using SPADE;

namespace API.Problems.P.P_STRONGLYCONNECTEDCOMPONENTS;

class P_STRONGLYCONNECTEDCOMPONENTS 
    : IGraphProblem<KosarajuSolver, SCCVerifier, DummyVisualization, UtilCollectionGraph>
{
    public string problemName { get; } = "Strongly Connected Components";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Strongly_connected_component";
    public string formalDefinition { get; } = "Given a directed graph G = (V, E), find a partition of V into maximal sets where every vertex in each set can reach every other vertex in that same set.";
    public string problemDefinition { get; } = "Find all strongly connected components in a directed graph.";
    public string source { get; } = "N/A";
    public string sourceLink { get; } = "https://en.wikipedia.org/wiki/Strongly_connected_component";
    public string wikiName { get; } = "";

    public static string _defaultInstance { get; } = "({1,2,3,4,5},{(1,2),(2,3),(3,1),(3,4),(4,5),(5,4)})";
    public string defaultInstance { get; } = _defaultInstance;
    public string instance { get; set; } = string.Empty;

    public string[] contributors { get; } = { "Surendra Thapa", "Rohan Shrestha" };

    public KosarajuSolver defaultSolver { get; } = new KosarajuSolver();
    public SCCVerifier defaultVerifier { get; } = new SCCVerifier();
    public DummyVisualization defaultVisualization { get; } = new DummyVisualization();

    public UtilCollectionGraph graph { get; set; }

    public P_STRONGLYCONNECTEDCOMPONENTS() : this(_defaultInstance) { }

    public P_STRONGLYCONNECTEDCOMPONENTS(string instanceString)
    {
        instance = instanceString;

        StringParser parser = new("{(N,E) | N is set, E subset {(u,v) | u is N, v is N}}");
        parser.parse(instanceString);

        graph = new UtilCollectionGraph(parser["N"], parser["E"]);
    }
}