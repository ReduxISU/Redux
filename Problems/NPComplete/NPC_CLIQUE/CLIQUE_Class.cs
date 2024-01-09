using API.Interfaces;
using API.Problems.NPComplete.NPC_CLIQUE.Solvers;
using API.Problems.NPComplete.NPC_CLIQUE.Verifiers;

namespace API.Problems.NPComplete.NPC_CLIQUE;

class CLIQUE : IProblem<CliqueBruteForce,CliqueVerifier> {

    // --- Fields ---
    private string _problemName = "Clique";
    private string _formalDefinition = "Clique = {<G, k> | G is an graph that has a set of k mutually adjacent nodes}";
    private string _problemDefinition = "A clique is the problem of uncovering a subset of vertices in an undirected graph G = (V, E) such that every two distinct vertices are adjacent";
    private string _source = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    private string _defaultInstance = "(({1,2,3,4},{{4,1},{1,2},{4,3},{3,2},{2,4}}),3)";
    private string _instance = string.Empty;
    private string _wikiName = "";
    private List<string> _nodes = new List<string>();
    private List<KeyValuePair<string, string>> _edges = new List<KeyValuePair<string, string>>();
    private int _K ;
    private CliqueBruteForce _defaultSolver = new CliqueBruteForce();
    private CliqueVerifier _defaultVerifier = new CliqueVerifier();
    private CliqueGraph _cliqueAsGraph;
    private string[] _contributers = { "Kaden Marchetti", "Alex Diviney" };

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

    public string[] contributers{
        get{
            return _contributers;
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

    public int K {
        get {
            return _K;
        }
        set {
            _K = value;
        }
    }
    public CliqueBruteForce defaultSolver {
        get {
            return _defaultSolver;
        }
    }
    public CliqueVerifier defaultVerifier {
        get {
            return _defaultVerifier;
        }
    }

    public CliqueGraph cliqueAsGraph {
        get{
            return _cliqueAsGraph;
        }
        set{
            _cliqueAsGraph = value;
        }
    }

    // --- Methods Including Constructors ---
    public CLIQUE() {
        _instance = defaultInstance;
        _cliqueAsGraph = new CliqueGraph(_instance,true);
        nodes = _cliqueAsGraph.nodesStringList;
        edges = _cliqueAsGraph.edgesKVP;
         _K = _cliqueAsGraph.K;
    }
    public CLIQUE(string GInput) {
        _cliqueAsGraph = new CliqueGraph(GInput, true);
        nodes = _cliqueAsGraph.nodesStringList;
        edges = _cliqueAsGraph.edgesKVP;
        _K = _cliqueAsGraph.K;
        _instance = _cliqueAsGraph.ToString();
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
        
        // [0] is nodes,  [1] is edges,  [2] is k.
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

    public int getK(string Ginput) {

        string strippedInput = Ginput.Replace("{", "").Replace("}", "").Replace(" ", "").Replace("(", "").Replace(")","");
        
        // [0] is nodes,  [1] is edges,  [2] is k.
        string[] Gsections = strippedInput.Split(':');
        return Int32.Parse(Gsections[2]);
    }


}