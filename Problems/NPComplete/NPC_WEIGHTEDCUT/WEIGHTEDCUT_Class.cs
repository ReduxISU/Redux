using API.Interfaces;
using API.Problems.NPComplete.NPC_WEIGHTEDCUT.Solvers;
using API.Problems.NPComplete.NPC_WEIGHTEDCUT.Verifiers;

namespace API.Problems.NPComplete.NPC_WEIGHTEDCUT;

class WEIGHTEDCUT : IProblem<WeightedCutBruteForce, WeightedCutVerifier>{

    // --- Fields ---
    private string _problemName = "Weighted Cut";
    private string _formalDefinition = "Cut = {<G, k> | G is a graph with cut of size k}";
    private string _problemDefinition = "A cut in an undirected graph is a partition of the graph's vertices into two complementary sets S and T, and the size of the cut is the number of edges between S and T.";
    private string[] _contributors = {"Andrija Sevaljevic"};
    
    private string _source = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    private string _defaultInstance = "(({1,2,3,4,5},{{2,1,5},{1,3,4},{2,3,2},{3,5,1},{2,4,4},{4,5,2}}),5)";
    private string _instance = string.Empty;
    
    private List<string> _nodes = new List<string>();
    private List<(string source, string destination, int weight)> _edges = new List<(string source, string destination, int weight)>();
    private int _K;
    private WeightedCutBruteForce _defaultSolver = new WeightedCutBruteForce();
    private WeightedCutVerifier _defaultVerifier = new WeightedCutVerifier();
    private WeightedCutGraph _weightedCutAsGraph;
    


    private string _wikiName = "";
    //private List<List<string>> _clauses = new List<List<string>>();
    //private List<string> _literals = new List<string>();

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
    public List<(string source,string destination,int weight)> edges {
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
    public WeightedCutBruteForce defaultSolver {
        get {
            return _defaultSolver;
        }
    }
    public WeightedCutVerifier defaultVerifier {
        get {
            return _defaultVerifier;
        }
    }

    public WeightedCutGraph weightedCutAsGraph {
        get{
            return _weightedCutAsGraph;
        }
    }

    // --- Methods Including Constructors ---
    public WEIGHTEDCUT() {
        _instance = defaultInstance;
        _weightedCutAsGraph = new WeightedCutGraph(_instance,true);
        nodes = _weightedCutAsGraph.nodesStringList;
        edges = _weightedCutAsGraph.edgesTuple;
         _K = _weightedCutAsGraph.K;


    }
    public WEIGHTEDCUT(string GInput) {
        _weightedCutAsGraph = new WeightedCutGraph(GInput, true);
        nodes = _weightedCutAsGraph.nodesStringList;
        edges = _weightedCutAsGraph.edgesTuple;
        _K = _weightedCutAsGraph.K;
        _instance = _weightedCutAsGraph.ToString();


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