using API.Interfaces;
using API.Problems.NPComplete.NPC_TSP.Solvers;
using API.Problems.NPComplete.NPC_TSP.Verifiers;
using API.Problems.NPComplete.NPC_TSP.Visualizations;
using SPADE;


namespace API.Problems.NPComplete.NPC_TSP;

class TSP : IGraphProblem<TSPBruteForce, TSPVerifier, TSPDefaultVisualization, UtilCollectionGraph> {

    // --- Fields ---
    public string problemName {get;} = "Traveling Salesperson";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Travelling_salesman_problem";
    public string formalDefinition {get;} = "TSP = {<G,k> | G is a weighted graph where there is a path through every vertex whose weights add up to less than k}";
    public string problemDefinition {get;} = "";
    public string[] contributors {get;} = {"Andrija Sevaljevic"};
    
    public string source {get;} = "";
    private static string _defaultInstance { get; } = "(({New York,Chicago,Denver,Los Angeles,Miami},{({New York,Chicago},790),({New York,Denver},1770),({New York,Los Angeles},2450),({New York,Miami},1280),({Chicago,Denver},1000),({Chicago,Los Angeles},2015),({Chicago,Miami},1370),({Denver,Los Angeles},1015),({Denver,Miami},2060),({Los Angeles,Miami},2745)}),8000)";
    public string defaultInstance { get; } = _defaultInstance;
                                      
    public string instance {get;set;} = string.Empty;
    
    private List<string> _nodes = new List<string>();
    private List<(string source, string target, int weight)> _edges = new List<(string source, string destination, int weight)>();
    private int _K;
    public TSPBruteForce defaultSolver {get;} = new TSPBruteForce();
    public TSPVerifier defaultVerifier { get; } = new TSPVerifier();
    public TSPDefaultVisualization defaultVisualization { get; } = new TSPDefaultVisualization();
    public UtilCollectionGraph graph { get; set; }
    
    public string wikiName {get;} = "";
  

    // --- Properties ---
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

    // --- Methods Including Constructors ---
     public TSP() : this(_defaultInstance) {
        
    }

    public TSP(string GInput)
    {
        instance = GInput;

        StringParser tsp = new("{((N,E),K) | N is set, E subset {(e, w) | e is N unorderedcross N, w is int}, K is int}");
        tsp.parse(GInput);
        nodes = tsp["N"].ToList().Select(node => node.ToString()).ToList();
        edges = tsp["E"].ToList().Select(edge =>
        {
            List<UtilCollection> cast = edge[0].ToList();
            return (cast[0].ToString(), cast[1].ToString(), int.Parse(edge[1].ToString()));
        }).ToList();
        _K = int.Parse(tsp["K"].ToString());

        graph = new UtilCollectionGraph(tsp["N"], tsp["E"]);
    }
}