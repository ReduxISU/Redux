using API.Interfaces;
using API.Problems.NPComplete.NPC_HAMILTONIAN.Solvers;
using API.Problems.NPComplete.NPC_HAMILTONIAN.Verifiers;

namespace API.Problems.NPComplete.NPC_HAMILTONIAN;

class HAMILTONIAN : IProblem<HamiltonianBruteForce,HamiltonianVerifier> {

    // --- Fields ---
    private string _problemName = "Hamiltonian";
    private string _formalDefinition = "Hamiltonian = {<G> | G has a cycle which covers every node exactly once}";
    private string _problemDefinition = "Hamiltonian is the problem of determining whether a Hamiltonian cycle (a path in an undirected or directed graph that visits each vertex exactly once).";
    private string _source = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    private string _defaultInstance = "({1,2,3,4,5},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5}})";
    private string _instance = string.Empty;

    private string _wikiName = "";
    private List<string> _nodes = new List<string>();
    private List<KeyValuePair<string, string>> _edges = new List<KeyValuePair<string, string>>();
    private HamiltonianBruteForce _defaultSolver = new HamiltonianBruteForce();
    private HamiltonianVerifier _defaultVerifier = new HamiltonianVerifier();
    private HamiltonianGraph _hamiltonianAsGraph;
    private string[] _contributors = { "Andrija Sevaljevic" };

    // --- Properties ---
    public string problemName {
        get {
            return _problemName;
        }
    }
    public string formalDefinition {
        get {
            return _formalDefinition;
        }
    }
    public string problemDefinition {
        get {
            return _problemDefinition;
        }
    }

    public string source {
        get {
            return _source;
        }
    }

    public string[] contributors{
        get{
            return _contributors;
        }
    }
    public string defaultInstance {
        get {
            return _defaultInstance;
        }
    }
    public string instance {
        get {
            return _instance;
        }
        set {
            _instance = value;
        }
    }
    public string wikiName {
        get {
            return _wikiName;
        }
    }
    public List<string> nodes {
        get {
            return _nodes;
        }
        set {
            _nodes = value;
        }
    }
    public List<KeyValuePair<string, string>> edges {
        get {
            return _edges;
        }
        set {
            _edges = value;
        }
    }
    public HamiltonianBruteForce defaultSolver {
        get {
            return _defaultSolver;
        }
    }
    public HamiltonianVerifier defaultVerifier {
        get {
            return _defaultVerifier;
        }
    }

    public HamiltonianGraph hamiltonianAsGraph {
        get{
            return _hamiltonianAsGraph;
        }
        set{
            _hamiltonianAsGraph = value;
        }
    }

    // --- Methods Including Constructors ---
    public HAMILTONIAN() {
        _instance = defaultInstance;
        _hamiltonianAsGraph = new HamiltonianGraph(_instance,true);
        nodes = _hamiltonianAsGraph.nodesStringList;
        edges = _hamiltonianAsGraph.edgesKVP;

    }
    public HAMILTONIAN(string GInput) {
        _hamiltonianAsGraph = new HamiltonianGraph(GInput, true);
        nodes = _hamiltonianAsGraph.nodesStringList;
        edges = _hamiltonianAsGraph.edgesKVP;
        _instance = _hamiltonianAsGraph.ToString();

    }


    public List<string> getNodes(string Ginput) {

        List<string> allGNodes = new List<string>();
        string strippedInput = Ginput.Replace("{", "").Replace("}", "").Replace(" ", "").Replace("(", "").Replace(")","");
        
        // [0] is nodes,  [1] is edges,  [2] is k.
        string[] Gsections = strippedInput.Split(':');
        string[] Gnodes = Gsections[0].Split(',');
        
        foreach(string node in Gnodes) {
            allGNodes.Add(node);
        }

        return allGNodes;
    }
    public List<KeyValuePair<string, string>> getEdges(string Ginput) {

        List<KeyValuePair<string, string>> allGEdges = new List<KeyValuePair<string, string>>();

        string strippedInput = Ginput.Replace("{", "").Replace("}", "").Replace(" ", "").Replace("(", "").Replace(")","");
        
        string[] Gsections = strippedInput.Split(':');
        string[] Gedges = Gsections[1].Split('&');
        
        foreach (string edge in Gedges) {
            string[] fromTo = edge.Split(',');
            string nodeFrom = fromTo[0];
            string nodeTo = fromTo[1];
            
            KeyValuePair<string,string> fullEdge = new KeyValuePair<string,string>(nodeFrom, nodeTo);
            allGEdges.Add(fullEdge);
        }

        return allGEdges;
    }




}