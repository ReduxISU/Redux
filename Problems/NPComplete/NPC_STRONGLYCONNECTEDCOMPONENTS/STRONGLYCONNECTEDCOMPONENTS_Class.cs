using API.Interfaces;
using API.Interfaces.Graphs;
using API.Problems.NPComplete.NPC_STRONGLYCONNECTEDCOMPONENTS.Solvers;
using API.Problems.NPComplete.NPC_STRONGLYCONNECTEDCOMPONENTS.Verifiers;
using API.DummyClasses;
using SPADE;

namespace API.Problems.NPComplete.NPC_STRONGLYCONNECTEDCOMPONENTS;

class STRONGLYCONNECTEDCOMPONENTS
    : IGraphProblem<KosarajuSolver, SCCVerifier, DummyVisualization, UtilCollectionGraph> {
    public string problemName { get; } = "Strongly Connected Components";

    public string problemLink { get; } =
        "https://en.wikipedia.org/wiki/Strongly_connected_component";

    public string formalDefinition { get; } =
        "Given a directed graph G = (V, E), find all maximal subsets C of V such that for every pair of vertices u and v in C, there is a directed path from u to v and from v to u.";

    public string problemDefinition { get; } =
        "A strongly connected component is a maximal group of vertices in a directed graph where every vertex can reach every other vertex in the same group. The goal is to return all such components.";

    public string source { get; } =
        "Swati Dhingra, Poorvi S. Dodwad, and Meghna Madan, \"Finding Strongly Connected Components in a Social Network Graph,\" International Journal of Computer Applications, Volume 136, No. 7, February 2016.";

    public string sourceLink { get; } =
        "https://ijcaonline.org/research/volume136/number7/dhingra-2016-ijca-908481.pdf";

    public string wikiName { get; } =
        "Strongly connected component";

    public static string _defaultInstance { get; } =
        "({1,2,3,4,5},{(1,2),(2,3),(3,1),(3,4),(4,5),(5,4)})";

    public string defaultInstance { get; } = _defaultInstance;

    public string instance { get; set; } = string.Empty;

    public string[] contributors { get; } =
    {
        "Surendra Thapa",
        "Rohan Shrestha"
    };

    // Declared, not derived from the Problems/NPComplete/ folder — Kosaraju's
    // algorithm solves this in polynomial time; it just lives under NPComplete/
    // for filing reasons.
    public ComplexityClass complexityClass { get; } = ComplexityClass.P;

    public KosarajuSolver defaultSolver { get; } = new KosarajuSolver();

    public SCCVerifier defaultVerifier { get; } = new SCCVerifier();

    public DummyVisualization defaultVisualization { get; } = new DummyVisualization();

    public UtilCollectionGraph graph { get; set; }

    public STRONGLYCONNECTEDCOMPONENTS() : this(_defaultInstance) { }

    public STRONGLYCONNECTEDCOMPONENTS(string instanceString) {
        instance = instanceString;

        StringParser parser = new("{(N,E) | N is set, E subset {(u,v) | u is N, v is N}}");
        parser.parse(instanceString);

        graph = new UtilCollectionGraph(parser["N"], parser["E"]);
    }
}