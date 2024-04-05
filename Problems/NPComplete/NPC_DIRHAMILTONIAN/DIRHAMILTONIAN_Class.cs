using API.Interfaces;
using API.Problems.NPComplete.NPC_DIRHAMILTONIAN.Solvers;
using API.Problems.NPComplete.NPC_DIRHAMILTONIAN.Verifiers;

namespace API.Problems.NPComplete.NPC_DIRHAMILTONIAN;

class DIRHAMILTONIAN : IProblem<DirectedHamiltonianBruteForce,DirectedHamiltonianVerifier> {

    // --- Fields ---
    private string _problemName = "Directed Hamiltonian";
    private string _formalDefinition = "Directed Hamiltonian = {<G> | G has a cycle which covers every node exactly once}";
    private string _problemDefinition = "Directed Hamiltonian is the problem of determining whether a Hamiltonian cycle (a path in an undirected or directed graph that visits each vertex exactly once).";
    private string _source = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    private string _defaultInstance = "({1,2,3,4,5},{(2,1),(1,3),(2,3),(3,5),(4,2),(5,4)})";
    private string _instance = string.Empty;

    private string _wikiName = "";
    private List<string> _nodes = new List<string>();
    private List<KeyValuePair<string, string>> _edges = new List<KeyValuePair<string, string>>();
    private DirectedHamiltonianBruteForce _defaultSolver = new DirectedHamiltonianBruteForce();
    private DirectedHamiltonianVerifier _defaultVerifier = new DirectedHamiltonianVerifier();
    private DirectedHamiltonianGraph _directedHamiltonianAsGraph;
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
    public DirectedHamiltonianBruteForce defaultSolver {
        get {
            return _defaultSolver;
        }
    }
    public DirectedHamiltonianVerifier defaultVerifier {
        get {
            return _defaultVerifier;
        }
    }

    public DirectedHamiltonianGraph directedHamiltonianAsGraph {
        get{
            return _directedHamiltonianAsGraph;
        }
        set{
            _directedHamiltonianAsGraph = value;
        }
    }

    // --- Methods Including Constructors ---
    public DIRHAMILTONIAN() {
        _instance = defaultInstance;
        _directedHamiltonianAsGraph = new DirectedHamiltonianGraph(_instance,true);
        nodes = _directedHamiltonianAsGraph.nodesStringList;
        edges = _directedHamiltonianAsGraph.edgesKVP;

    }
    public DIRHAMILTONIAN(string GInput) {
        _directedHamiltonianAsGraph = new DirectedHamiltonianGraph(GInput, true);
        nodes = _directedHamiltonianAsGraph.nodesStringList;
        edges = _directedHamiltonianAsGraph.edgesKVP;
        _instance = _directedHamiltonianAsGraph.ToString();

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