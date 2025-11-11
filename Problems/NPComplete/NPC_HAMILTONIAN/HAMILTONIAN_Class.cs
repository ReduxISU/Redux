using API.Interfaces;
using API.Problems.NPComplete.NPC_HAMILTONIAN.Solvers;
using API.Problems.NPComplete.NPC_HAMILTONIAN.Verifiers;
using API.Problems.NPComplete.NPC_HAMILTONIAN.Visualizations;
using SPADE;

namespace API.Problems.NPComplete.NPC_HAMILTONIAN;

class HAMILTONIAN : IGraphProblem<HamiltonianBruteForce, HamiltonianVerifier, HamiltonianDefaultVisualization, UtilCollectionGraph>
{

    // --- Fields ---
    public string problemName { get; } = "Hamiltonian Path";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Hamiltonian_path";
    public string formalDefinition { get; } = "Hamiltonian Path = {<G> | G has a cycle which covers every node exactly once}";
    public string problemDefinition { get; } = "Hamiltonian Path is the problem of determining whether a Hamiltonian cycle (a path in an undirected or directed graph that visits each vertex exactly once).";
    public string source { get; } = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceLink { get; } = "https://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf";
    private static string _defaultInstance = "({1,2,3,4,5},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5}})";
    public string defaultInstance { get; } = _defaultInstance;
    public string instance { get; set; } = string.Empty;

    public string wikiName { get; } = "";
    private List<string> _nodes = new List<string>();
    private List<KeyValuePair<string, string>> _edges = new List<KeyValuePair<string, string>>();
    public HamiltonianBruteForce defaultSolver { get; } = new HamiltonianBruteForce();
    public HamiltonianVerifier defaultVerifier { get; } = new HamiltonianVerifier();
    public HamiltonianDefaultVisualization defaultVisualization { get; } = new HamiltonianDefaultVisualization();
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
    public HAMILTONIAN() : this(_defaultInstance)
    {

    }
    public HAMILTONIAN(string GInput)
    {
        instance = GInput;

        StringParser hamiltonian = new("{(N,E) | N is set, E subset N unorderedcross N}");
        hamiltonian.parse(GInput);
        nodes = hamiltonian["N"].ToList().Select(node => node.ToString()).ToList();
        edges = hamiltonian["E"].ToList().Select(edge =>
        {
            List<UtilCollection> cast = edge.ToList();
            return new KeyValuePair<string, string>(cast[0].ToString(), cast[1].ToString());
        }).ToList();

        graph = new UtilCollectionGraph(hamiltonian["N"], hamiltonian["E"]);
    }
}