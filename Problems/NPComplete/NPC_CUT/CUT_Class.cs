using API.Interfaces;
using API.Problems.NPComplete.NPC_CUT.Solvers;
using API.Problems.NPComplete.NPC_CUT.Verifiers;

namespace API.Problems.NPComplete.NPC_CUT;

class CUT : IProblem<CutBruteForce, CutVerifier>{

    // --- Fields ---
    private string _problemName = "Cut";
    private string _formalDefinition = "Cut = {<G, k> | G is a graph with cut of size k}";
    private string _problemDefinition = "A cut in an undirected graph is a partition of the graph's vertices into two complementary sets S and T, and the size of the cut is the number of edges between S and T.";
    private string[] _contributers = {"Andrija Sevaljevic"};
    
    private string _source = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    private string _defaultInstance = "(({1,2,3,4,5},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5}}),5)";
    private string _instance = string.Empty;
    
    private List<string> _nodes = new List<string>();
    private List<KeyValuePair<string, string>> _edges = new List<KeyValuePair<string, string>>();
    private int _K;
    private CutBruteForce _defaultSolver = new CutBruteForce();
    private CutVerifier _defaultVerifier = new CutVerifier();
    private CutGraph _cutAsGraph;
    
    private string _wikiName = "";
  

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
    public CutBruteForce defaultSolver {
        get {
            return _defaultSolver;
        }
    }
    public CutVerifier defaultVerifier {
        get {
            return _defaultVerifier;
        }
    }

    public CutGraph cutAsGraph {
        get{
            return _cutAsGraph;
        }
    }

    // --- Methods Including Constructors ---
    public CUT() {
        _instance = defaultInstance;
        _cutAsGraph = new CutGraph(_instance,true);
        nodes = _cutAsGraph.nodesStringList;
        edges = _cutAsGraph.edgesKVP;
         _K = _cutAsGraph.K;


    }
    public CUT(string GInput) {
        _cutAsGraph = new CutGraph(GInput, true);
        nodes = _cutAsGraph.nodesStringList;
        edges = _cutAsGraph.edgesKVP;
        _K = _cutAsGraph.K;
        _instance = _cutAsGraph.ToString();


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