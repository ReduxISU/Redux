using API.Interfaces;
using API.Problems.NPComplete.NPC_CLIQUECOVER.Solvers;
using API.Problems.NPComplete.NPC_CLIQUECOVER.Verifiers;

namespace API.Problems.NPComplete.NPC_CLIQUECOVER;

class CLIQUECOVER : IProblem<CliqueCoverBruteForce,CliqueCoverVerifier> {

    // --- Fields ---
    private string _problemName = "Clique Cover";
    private string _formalDefinition = "Clique Cover = {<G, k> | G is a graph represnted by k or fewer cliques}";
    private string _problemDefinition = "A clique cover is a partition of the vertices into cliques, subsets of vertices within which every two vertices are adjacent";
    private string _source = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    private string _defaultInstance = "(({1,2,3,4,5,6,7,8},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5},{6,7},{7,8},{6,8}}),3)";
    private string _instance = string.Empty;

    private string _wikiName = "";
    private List<string> _nodes = new List<string>();
    private List<KeyValuePair<string, string>> _edges = new List<KeyValuePair<string, string>>();
    private int _K = 3;
    private CliqueCoverBruteForce _defaultSolver = new CliqueCoverBruteForce();
    private CliqueCoverVerifier _defaultVerifier = new CliqueCoverVerifier();
    private CliqueCoverGraph _cliqueCoverAsGraph;
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

    public int K {
        get {
            return _K;
        }
        set {
            _K = value;
        }
    }
    public CliqueCoverBruteForce defaultSolver {
        get {
            return _defaultSolver;
        }
    }
    public CliqueCoverVerifier defaultVerifier {
        get {
            return _defaultVerifier;
        }
    }

    public CliqueCoverGraph cliqueCoverAsGraph {
        get{
            return _cliqueCoverAsGraph;
        }
        set{
            _cliqueCoverAsGraph = value;
        }
    }

    // --- Methods Including Constructors ---
    public CLIQUECOVER() {
        _instance = defaultInstance;
        _cliqueCoverAsGraph = new CliqueCoverGraph(_instance,true);
        nodes = _cliqueCoverAsGraph.nodesStringList;
        edges = _cliqueCoverAsGraph.edgesKVP;
         _K = _cliqueCoverAsGraph.K;


    }
    public CLIQUECOVER(string GInput) {
        _cliqueCoverAsGraph = new CliqueCoverGraph(GInput, true);
        nodes = _cliqueCoverAsGraph.nodesStringList;
        edges = _cliqueCoverAsGraph.edgesKVP;
        _K = _cliqueCoverAsGraph.K;
        _instance = _cliqueCoverAsGraph.ToString();


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