using API.Interfaces;
using API.Problems.NPComplete.NPC_GRAPHCOLORING.Solvers;
using API.Problems.NPComplete.NPC_GRAPHCOLORING.Verifiers;
using API.Problems.NPComplete.NPC_GRAPHCOLORING.Visualizations;
using SPADE;

namespace API.Problems.NPComplete.NPC_GRAPHCOLORING;

class GRAPHCOLORING : IGraphProblem<GraphColoringBruteForce, GraphColoringVerifier, GraphColoringDefaultVisualization, UtilCollectionGraph> {


    #region Fields
    public string problemName {get;} = "Graph Coloring";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Graph_coloring";
    public string formalDefinition {get;} = "GRAPHCOLORING = {<G,k> | G is a graph that has a k-coloring}";
    public string problemDefinition {get;} = "An assignment of labels (e.g., colors) to the vertices of a graph such that no two adjacent vertices are of the same label. This is called a vertex coloring.";

    public string source {get;} = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceLink { get; } = "https://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf";
    public string[] contributors {get;} = { "Daniel Igbokwe", "Alex Diviney" };

    private static string _defaultInstance = "(({a,b,c,d,e,f,g,h,i},{{a,b},{b,c},{a,c},{d,a},{d,e},{a,e},{a,f},{f,g},{g,a},{a,h},{h,i},{i,a}}),3)";
    public string defaultInstance { get; } = _defaultInstance;

    public string instance {get;set;} = string.Empty;

    private List<string> _nodes =  new List<string>();

    private List<KeyValuePair<string, string>> _edges = new List<KeyValuePair<string, string>>();

    private Dictionary<string, string> _nodeColoring = new Dictionary<string, string>();

    private SortedSet<string> _colors = new SortedSet<string>(){"0", "1","2"};
  
    private int _K = 3;

    public string wikiName {get;} = "";

    public GraphColoringBruteForce defaultSolver {get;} = new GraphColoringBruteForce();
    public GraphColoringVerifier defaultVerifier {get;} = new GraphColoringVerifier();

    public GraphColoringDefaultVisualization defaultVisualization { get; } = new GraphColoringDefaultVisualization();
    public UtilCollectionGraph graph { get; }

    #endregion


    #region Properties


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

    public SortedSet<string> colors {
        get {
            return _colors;
        }
        set {
            _colors = value;
        }
    }

    #endregion


    #region Constructors
    public GRAPHCOLORING() : this(_defaultInstance) {

    }
    public GRAPHCOLORING(string GInput) {
        instance = GInput;

        StringParser graphcoloring = new("{((N,E),K) | N is set, E subset N unorderedcross N, K is int}");
        graphcoloring.parse(GInput);
        nodes = graphcoloring["N"].ToList().Select(node => node.ToString()).ToList();
        edges = graphcoloring["E"].ToList().Select(edge =>
        {
            List<UtilCollection> cast = edge.ToList();
            return new KeyValuePair<string, string>(cast[0].ToString(), cast[1].ToString());
        }).ToList();
        _K = int.Parse(graphcoloring["K"].ToString());

        graph = new UtilCollectionGraph(graphcoloring["N"], graphcoloring["E"]);
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
        instance  = problem;

    }

    #endregion
}