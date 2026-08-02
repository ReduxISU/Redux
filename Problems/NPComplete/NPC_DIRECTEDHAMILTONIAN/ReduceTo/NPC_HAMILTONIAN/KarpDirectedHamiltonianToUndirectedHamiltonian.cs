using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_HAMILTONIAN;

namespace API.Problems.NPComplete.NPC_DIRECTEDHAMILTONIAN.ReduceTo.NPC_HAMILTONIAN;

class KarpDirectedHamiltonianToUndirectedHamiltonian : IReduction<DIRECTEDHAMILTONIAN, HAMILTONIAN>
{

    // --- Fields ---
    public string reductionName { get; } = "Karp Directed Hamiltonian To Undirected Hamiltonian";
    public string reductionDefinition { get; } = "TODO";
    public string source { get; } = "TODO";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    // reduce() emits exactly 3 gadget nodes + 2 gadget edges per input node, and 1
    // converted edge per input edge — O(n+m), no cross-product terms.
    public ReductionCost cost { get; } = ReductionCost.Linear;
    public List<Gadget> gadgets { get; }
    private DIRECTEDHAMILTONIAN _reductionFrom;
    private HAMILTONIAN _reductionTo;


    // --- Properties ---
    public DIRECTEDHAMILTONIAN reductionFrom
    {
        get
        {
            return _reductionFrom;
        }
        set
        {
            _reductionFrom = value;
        }
    }
    public HAMILTONIAN reductionTo
    {
        get
        {
            return _reductionTo;
        }
        set
        {
            _reductionTo = value;
        }
    }

    // --- Methods Including Constructors ---

    public KarpDirectedHamiltonianToUndirectedHamiltonian(DIRECTEDHAMILTONIAN from)
    {
        gadgets = new();
        _reductionFrom = from;
        _reductionTo = reduce();
    }
    public KarpDirectedHamiltonianToUndirectedHamiltonian(string instance) : this(new DIRECTEDHAMILTONIAN(instance)) { }
    public KarpDirectedHamiltonianToUndirectedHamiltonian() : this(new DIRECTEDHAMILTONIAN()) { }

    public HAMILTONIAN reduce()
    {
        // Part 1: Create necessary data structures
        DIRECTEDHAMILTONIAN DH = _reductionFrom;
        HAMILTONIAN UH = new HAMILTONIAN();

        List<string> newNodes = new List<string>();
        List<KeyValuePair<string, string>> newEdges = new List<KeyValuePair<string, string>>();

        // Part 2: Convert nodes from directed graph to undirected graph
        foreach (string v in DH.nodes)
        {
            string v1 = v + "1";
            string v2 = v + "2";
            string v3 = v + "3";

            newNodes.Add(v1);
            newNodes.Add(v2);
            newNodes.Add(v3);

            // Force traversal order inside the gadget
            newEdges.Add(new KeyValuePair<string, string>(v1, v2));
            newEdges.Add(new KeyValuePair<string, string>(v2, v3));
        }

        // For every directed edge (u → v), add undirected edge (u3, v1)
        foreach (var e in DH.edges)
        {
            string u = e.Key;
            string v = e.Value;

            string u3 = u + "3";
            string v1 = v + "1";

            newEdges.Add(new KeyValuePair<string, string>(u3, v1));
        }

        // Part 3: Construct the new HAMILTONIAN instance
        string nodesString = string.Join(",", newNodes);

        string edgesString = "";
        foreach (var edge in newEdges)
        {
            edgesString += "{" + edge.Key + "," + edge.Value + "},";
        }
        edgesString = edgesString.Trim(',');

        string G = "({" + nodesString + "},{" + edgesString + "})";
        
        UH = new HAMILTONIAN(G);
        
        reductionTo = UH;
        return UH;
    }

    public string mapSolutions(string problemFromSolution)
    {
        return "";
    }
}
