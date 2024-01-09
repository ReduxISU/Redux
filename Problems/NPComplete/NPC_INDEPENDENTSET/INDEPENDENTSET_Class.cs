using API.Interfaces;
using API.Problems.NPComplete.NPC_INDEPENDENTSET.Solvers;
using API.Problems.NPComplete.NPC_INDEPENDENTSET.Verifiers;

namespace API.Problems.NPComplete.NPC_INDEPENDENTSET;
class INDEPENDENTSET : IProblem<IndependentSetBruteForce,IndependentSetVerifier> {

    // --- Fields ---
    private string _problemName = "Independent Set";
    private string _formalDefinition = "In a graph G = (V, E), an independent set is a subset X of vertices no two of which are adjacent";
    private string _problemDefinition = "An Independent Set is a set of nodes in a graph G, where no node is connected to another node in the set";
    private string _source = "Golumbic, M. C. (2004). Algorithmic graph theory and perfect graphs. Elsevier.";
    private string _defaultInstance = "(({a,b,c,d,e,f,g,h,i},{{a,b},{b,a},{b,c},{c,a},{a,c},{c,b},{a,d},{d,a},{d,e},{e,a},{a,e},{e,d},{a,f},{f,a},{f,g},{g,a},{a,g},{g,f},{a,h},{h,a},{h,i},{i,a},{a,i},{i,h}}),4)";
    private string _instance = string.Empty;
    private string _wikiName = "";
    private List<string> _nodes = new List<string>();
    private List<KeyValuePair<string, string>> _edges = new List<KeyValuePair<string, string>>();
    private int _K ;
    private IndependentSetBruteForce _defaultSolver = new IndependentSetBruteForce();
    private IndependentSetVerifier _defaultVerifier = new IndependentSetVerifier();
    private IndependentSetGraph _independentSetAsGraph;
    private string[] _contributers = { "Russell Phillips" };

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
    public IndependentSetBruteForce defaultSolver {
        get {
            return _defaultSolver;
        }
    }
    public IndependentSetVerifier defaultVerifier {
        get {
            return _defaultVerifier;
        }
    }

    public IndependentSetGraph independentSetAsGraph {
        get{
            return _independentSetAsGraph;
        }
        set{
            _independentSetAsGraph = value;
        }
    }

    // --- Methods Including Constructors ---
    public INDEPENDENTSET() {
        _instance = defaultInstance;
        _independentSetAsGraph = new IndependentSetGraph(_instance,true);
        nodes = _independentSetAsGraph.nodesStringList;
        edges = _independentSetAsGraph.edgesKVP;
         _K = _independentSetAsGraph.K;
    }
    public INDEPENDENTSET(string GInput) {
        _independentSetAsGraph = new IndependentSetGraph(GInput, true);
        nodes = _independentSetAsGraph.nodesStringList;
        edges = _independentSetAsGraph.edgesKVP;
        _K = _independentSetAsGraph.K;
        _instance = _independentSetAsGraph.ToString();
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