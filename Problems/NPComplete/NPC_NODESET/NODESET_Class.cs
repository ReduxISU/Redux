using API.Interfaces;
using API.Problems.NPComplete.NPC_NODESET.Solvers;
using API.Problems.NPComplete.NPC_NODESET.Verifiers;

namespace API.Problems.NPComplete.NPC_NODESET;

class NODESET : IProblem<NodeSetBruteForce,NodeSetVerifier> {

    // --- Fields ---
    private string _problemName = "Feedback Node Set";
    private string _formalDefinition = "Feedback Node Set = ";
    private string _problemDefinition = "Node Set is the problem";
    private string _source = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    private string _defaultInstance = "(({1,2,3,4},{(1,2),(2,4),(3,2),(4,1),(4,3)}),1)";
    private string _instance = string.Empty;
    private int _K;

    private string _wikiName = "";
    private List<string> _nodes = new List<string>();
    private List<KeyValuePair<string, string>> _edges = new List<KeyValuePair<string, string>>();
    private NodeSetBruteForce _defaultSolver = new NodeSetBruteForce();
    private NodeSetVerifier _defaultVerifier = new NodeSetVerifier();
    private NodeSetGraph _nodeSetAsGraph;
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
    public int K {
        get {
            return _K;
        }
        set {
            _K = value;
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
    public NodeSetBruteForce defaultSolver {
        get {
            return _defaultSolver;
        }
    }
    public NodeSetVerifier defaultVerifier {
        get {
            return _defaultVerifier;
        }
    }

    public NodeSetGraph nodeSetAsGraph {
        get{
            return _nodeSetAsGraph;
        }
        set{
            _nodeSetAsGraph = value;
        }
    }

    // --- Methods Including Constructors ---
    public NODESET() {
        _instance = defaultInstance;
        _nodeSetAsGraph = new NodeSetGraph(_instance,true);
        nodes = _nodeSetAsGraph.nodesStringList;
        edges = _nodeSetAsGraph.edgesKVP;
        K = _nodeSetAsGraph.K;

    }
    public NODESET(string GInput) {
        _nodeSetAsGraph = new NodeSetGraph(GInput, true);
        nodes = _nodeSetAsGraph.nodesStringList;
        edges = _nodeSetAsGraph.edgesKVP;
        K = _nodeSetAsGraph.K;
        _instance = _nodeSetAsGraph.ToString();

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