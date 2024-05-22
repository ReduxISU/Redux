using API.Interfaces;
using API.Problems.NPComplete.NPC_GRAPHCOLORING.Solvers;
using API.Problems.NPComplete.NPC_GRAPHCOLORING.Verifiers;

namespace API.Problems.NPComplete.NPC_GRAPHCOLORING;

class GRAPHCOLORING : IProblem<GraphColoringBruteForce, GraphColoringVerifier>{


    #region Fields
    private readonly string _problemName = "Graph Coloring";
    private readonly string _formalDefinition = "GRAPHCOLORING = {<G,k> | G is a graph that has a k-coloring}";
    private readonly string _problemDefinition = "An assignment of labels (e.g., colors) to the vertices of a graph such that no two adjacent vertices are of the same label. This is called a vertex coloring.";

    private readonly string _source = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    private string[] _contributors = { "Daniel Igbokwe", "Alex Diviney" };

    private string _defaultInstance = "(({a,b,c,d,e,f,g,h,i},{{a,b},{b,c},{a,c},{d,a},{d,e},{a,e},{a,f},{f,g},{g,a},{a,h},{h,i},{i,a}}),3)";

    private string _instance  =  string.Empty;

    private List<string> _nodes =  new List<string>();

    private List<KeyValuePair<string, string>> _edges = new List<KeyValuePair<string, string>>();

    private Dictionary<string, string> _nodeColoring = new Dictionary<string, string>();

    private SortedSet<string> _colors = new SortedSet<string>(){"0", "1","2"};
  
    private int _K = 3;

    private string _wikiName = "";

    private GraphColoringBruteForce _defaultSolver = new GraphColoringBruteForce();
    private GraphColoringVerifier _defaultVerifier = new GraphColoringVerifier();

    private GraphColoringGraph _graphColoringAsGraph;

    #endregion


    #region Properties

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

    public String instance  {
        get{
            return _instance ;
        }

        set {
            _instance  = value;
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

    public Dictionary<string, string> nodeColoring {

        get{
            return _nodeColoring;
        }

        set {
            _nodeColoring = value;
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

   public GraphColoringGraph graphColoringAsGraph {
    get {
        return _graphColoringAsGraph;
    }
    set {
        _graphColoringAsGraph = value;
    }
   }

    
    public SortedSet<string> colors {
        get {
            return _colors;
        }
        set {
            _colors = value;
        }
    }
    
    public GraphColoringBruteForce defaultSolver {
        get {
            return _defaultSolver;
        }
    }
    public GraphColoringVerifier defaultVerifier {
        get {
            return _defaultVerifier;
        }
    }

    #endregion


    #region Constructors
      public GRAPHCOLORING() {
        _instance  = _defaultInstance;
        _graphColoringAsGraph = new GraphColoringGraph(_instance, true);
        nodes = _graphColoringAsGraph.nodesStringList;
        edges  = _graphColoringAsGraph.edgesKVP;
        K = _graphColoringAsGraph.K;
    }
    public GRAPHCOLORING(string GInput) {
        _instance  = GInput;
        _graphColoringAsGraph = new GraphColoringGraph(_instance, true);
        nodes = _graphColoringAsGraph.nodesStringList;
        edges  = _graphColoringAsGraph.edgesKVP;
        K = _graphColoringAsGraph.K; 
    }

    #endregion


    #region Methods
/// <summary>
/// This method sets the instance attribute of the graph and is called by a problem's constructor.
/// </summary>
/// <remarks>
/// Authored by Daniel Igbokwe.
/// Contributed to by Alex Diviney
/// </remarks>
    public void parseProblem() {

        string problem = "(({";

        // Parse nodes
        for(int i = 0; i < nodes.Count - 1; i++){
            problem += nodes[i] + ",";
        }
        problem += this._nodes[this._nodes.Count - 1] + "},{";

        // Parse edges
        for(int i= 0; i< this._edges.Count; i++){
            if(i % 2 == 0){
                 problem += "{"+ this._edges[i].Key + "," + this._edges[i].Value + "},";
            }
        }
        problem = problem.TrimEnd(',');
        // Parse k
        problem +="})," +this._K + ")";
        //this._defaultInstance = problem; //ALEX NOTE: We shouldn't ever update the defaultIntance. DEPRECATING
        this._instance  = problem;

    }

    #endregion
}