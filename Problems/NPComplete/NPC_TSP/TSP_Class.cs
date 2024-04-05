using API.Interfaces;
using API.Problems.NPComplete.NPC_TSP.Solvers;
using API.Problems.NPComplete.NPC_TSP.Verifiers;

namespace API.Problems.NPComplete.NPC_TSP;

class TSP : IProblem<TSPBruteForce, TSPVerifier>{

    // --- Fields ---
    private string _problemName = "Traveling Sales Person";
    private string _formalDefinition = "TSP = {<G,k> | G is a weighted graph where there is a path through every vertex whose weights add up to less than k}";
    private string _problemDefinition = "";
    private string[] _contributors = {"Andrija Sevaljevic"};
    
    private string _source = "";
    private string _defaultInstance = "(({Narnia,Atlantis,Wakanda,Pocatello,Neverland},{{Narnia,Atlantis,30},{Narnia,Wakanda,55},{Narnia,Pocatello,80},{Narnia,Neverland,45},{Atlantis,Wakanda,65},{Atlantis,Pocatello,15},{Atlantis,Neverland,30},{Wakanda,Pocatello,40},{Wakanda,Neverland,90},{Pocatello,Neverland,25}}),200)";
                                      
    private string _instance = string.Empty;
    
    private List<string> _nodes = new List<string>();
    private List<(string source, string target, int weight)> _edges = new List<(string source, string destination, int weight)>();
    private int _K;
    private TSPBruteForce _defaultSolver = new TSPBruteForce();
    private TSPVerifier _defaultVerifier = new TSPVerifier();
    private TSPGraph _tspAsGraph;
    
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
    public List<(string source, string target, int weight)> edges {
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
    public TSPBruteForce defaultSolver {
        get {
            return _defaultSolver;
        }
    }
    public TSPVerifier defaultVerifier {
        get {
            return _defaultVerifier;
        }
    }

    public TSPGraph tspAsGraph {
        get{
            return _tspAsGraph;
        }
    }

    // --- Methods Including Constructors ---
    public TSP() {
        _instance = defaultInstance;
        _tspAsGraph = new TSPGraph(_instance,true);
        nodes = _tspAsGraph.nodesStringList;
        edges = _tspAsGraph.edgesTuple;
         _K = _tspAsGraph.K;


    }
    public TSP(string GInput) {
        _tspAsGraph = new TSPGraph(GInput, true);
        nodes = _tspAsGraph.nodesStringList;
        edges = _tspAsGraph.edgesTuple;
        _K = _tspAsGraph.K;
        _instance = _tspAsGraph.ToString();


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