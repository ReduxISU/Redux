using API.Interfaces;
using API.Problems.NPComplete.NPC_DIRECTEDHAMILTONIAN.Solvers;
using API.Problems.NPComplete.NPC_DIRECTEDHAMILTONIAN.Verifiers;
using API.Problems.NPComplete.NPC_DIRECTEDHAMILTONIAN.Visualizations;
using SPADE;

namespace API.Problems.NPComplete.NPC_DIRECTEDHAMILTONIAN;

class DIRECTEDHAMILTONIAN : IGraphProblem<DirectedHamiltonianBruteForce, DirectedHamiltonianVerifier, DirectedHamiltonianDefaultVisualization, UtilCollectionGraph>
{

    // --- Fields ---
    public string problemName { get; } = "Directed Hamiltonian Path";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Hamiltonian_path";
    public string formalDefinition { get; } = "Directed Hamiltonian Path = {<G> | G has a cycle which covers every node exactly once}";
    public string problemDefinition { get; } = "Directed Hamiltonian Path is the problem of determining whether a Hamiltonian cycle (a path in an undirected or directed graph that visits each vertex exactly once).";
    public string source { get; } = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceLink { get; } = "https://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf";
    private static string _defaultInstance = "({1,2,3,4,5},{(2,1),(1,3),(2,3),(3,5),(4,2),(5,4)})";
    public string defaultInstance { get; } = _defaultInstance;
    public string instance { get; set; } = string.Empty;

    public string wikiName { get; } = "";
    private List<string> _nodes = new List<string>();
    private List<KeyValuePair<string, string>> _edges = new List<KeyValuePair<string, string>>();
    public DirectedHamiltonianBruteForce defaultSolver { get; } = new DirectedHamiltonianBruteForce();
    public DirectedHamiltonianVerifier defaultVerifier { get; } = new DirectedHamiltonianVerifier();
    public DirectedHamiltonianDefaultVisualization defaultVisualization { get; } = new DirectedHamiltonianDefaultVisualization();
    public UtilCollectionGraph graph { get; set; }
    public string[] contributors { get; } = { "Andrija Sevaljevic" };

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
    public DIRECTEDHAMILTONIAN() : this(_defaultInstance)
    {

    }
    public DIRECTEDHAMILTONIAN(string GInput)
    {
        instance = GInput;

        StringParser directedhamiltonianparser = new("{(N,E) | N is set, E subset N cross N}");
        directedhamiltonianparser.parse(GInput);
        nodes = directedhamiltonianparser["N"].ToList().Select(node => node.ToString()).ToList();
        edges = directedhamiltonianparser["E"].ToList().Select(edge =>
        {
            List<UtilCollection> cast = edge.ToList();
            return new KeyValuePair<string, string>(cast[0].ToString(), cast[1].ToString());
        }).ToList();

        graph = new UtilCollectionGraph(directedhamiltonianparser["N"], directedhamiltonianparser["E"]);
    }
}