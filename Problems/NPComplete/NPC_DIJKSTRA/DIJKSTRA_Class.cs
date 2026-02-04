using API.Interfaces;
using API.Problems.NPComplete.NPC_DIJKSTRA.Solvers;
using API.Problems.NPComplete.NPC_DIJKSTRA.Verifiers;
using API.Problems.NPComplete.NPC_DIJKSTRA.Visualizations;
using SPADE;

namespace API.Problems.NPComplete.NPC_Dijkstra;

class DIJKSTRA : IGraphProblem<DijkstraBruteForce, DijkstraVerifier, DijkstraDefaultVisualization, UtilCollectionGraph>
{

    // --- Fields ---
    public string problemName { get; } = "Dijkstra's algorithm";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Dijkstra%27s_algorithm";
    public string formalDefinition { get; } = "Dijkstra's algorithm = {[G, V, E, l] | \"G = (V, E) whose edge lengths l are positive integers\" }"; //TODO figure out what it's supposed to be
    public string problemDefinition { get; } = "Dijkstra’s Shortest Path Problem is the problem of determining the shortest path distances from a given source vertex to all other vertices in a directed graph with strictly positive edge weights.";
    public string source { get; } = "Dijkstra, E. W. A note on two problems in connexion with graphs. Numerische Mathematik 1, 1959, 269–271.";
    public string sourceLink { get; } = "https://ir.cwi.nl/pub/9256/9256D.pdf";
    private static string _defaultInstance = "({1,2,3,4,5},{{2,1,4},{1,3,2},{2,3,1},{3,5,7},{2,4,3},{4,5,9}})";
    public string defaultInstance { get; } = _defaultInstance;
    public string instance { get; set; } = string.Empty;

    public string wikiName { get; } = "";
    private List<string> _nodes = new List<string>();
    private List<KeyValuePair<string, string>> _edges = new List<KeyValuePair<string, string>>();
    public DijkstraBruteForce defaultSolver { get; } = new DijkstraBruteForce();
    public DijkstraVerifier defaultVerifier { get; } = new DijkstraVerifier();
    public DijkstraDefaultVisualization defaultVisualization { get; } = new DijkstraDefaultVisualization();
    public UtilCollectionGraph graph { get; set; }
    public string[] contributors { get; } = { "Tiger Sant", "Malaya Witt, Rajit Nilkar, Scott Barfuss" };

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
    public List<KeyValuePair<string, string>> edges
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
    public DIJKSTRA() : this(_defaultInstance)
    {

    }
    public DIJKSTRA(string GInput)
    {
        instance = GInput;

        StringParser dijkstra = new("{(N,E) | N is set, E subset N unorderedcross N}");
        dijkstra.parse(GInput);
        nodes = dijkstra["N"].ToList().Select(node => node.ToString()).ToList();
        edges = dijkstra["E"].ToList().Select(edge =>
        {
            List<UtilCollection> cast = edge.ToList();
            return new KeyValuePair<string, string>(cast[0].ToString(), cast[1].ToString());
        }).ToList();

        graph = new UtilCollectionGraph(dijkstra["N"], dijkstra["E"]);
    }
}