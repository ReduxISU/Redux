using API.Interfaces;
using API.Problems.NPComplete.NPC_PDTSP.Solvers;
using API.Problems.NPComplete.NPC_PDTSP.Verifiers;
using SPADE;

namespace API.Problems.NPComplete.NPC_PDTSP;

class PDTSP : IGraphProblem
{
    // --- Fields ---
    public string problemName { get; } = "Pickup and Delivery Traveling Salesperson";
    public string problemLink { get; } =
        "https://backend.orbit.dtu.dk/ws/portalfiles/portal/4219697/The%20Traveling%20Salesman%20Problem%20with%20Pickup%20and%20Delivery%20-%20Polyhedral%20Results%20and%20a%20Branch-and-Cut%20Algorithm.pdf";

    public string formalDefinition { get; } =
        "PDTSP = {<G,D,R,K> | G has a Hamiltonian tour starting and "
        + "ending at depot D, with total weight <= K, such that for "
        + "every request (p,d) in R, p is visited before d}";

    public string problemDefinition { get; } =
        "Visit every non-depot vertex exactly once, visiting each pickup "
        + "before its corresponding delivery, then return to the depot.";

    public string inputDescription { get; } =
        "A weighted undirected graph G, depot D, ordered pickup/delivery "
        + "pairs R, and maximum tour weight K.";
    public string outputDescription { get; } =
        "A tour satisfying the pickup/delivery constraints with weight "
        + "at most K, or {} if no such tour exists.";
    public string[] contributors { get; } = { "Quinton Smith" };

    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();

    // D = depot.
    // R contains ordered tuples: (pickup, delivery).
    // Graph edges remain unordered because this graph is undirected.
    public const string InstanceGrammar =
        "{((N,E),D,R,K) | N is set, "
        + "E subset {(e,w) | e is N unorderedcross N, w is int}, "
        + "D is N, R subset N cross N, K is int}";

    private static string _defaultInstance { get; } =
        "(({Depot,P1,D1}," + "{({Depot,P1},2),({P1,D1},3),({D1,Depot},4)})," + "Depot,{(P1,D1)},9)";

    public string defaultInstance { get; } = _defaultInstance;
    public string instanceFormat { get; } =
        $"Format: {InstanceGrammar} Example: {_defaultInstance}";
    public string certificateFormat { get; } =
        "Format: {Depot,...,Depot}; visit every non-depot vertex exactly "
        + "once, each pickup before its delivery, with total weight <= K. "
        + "Example: {Depot,P1,D1,Depot}";

    public string wikiName { get; } = "";

    public string instance { get; set; } = string.Empty;

    public PDTSPBruteForce defaultSolver { get; } = new();
    public PDTSPVerifier defaultVerifier { get; } = new();

    // Connect the typed defaults to the backend's IProblem interface.
    ISolver IProblem.defaultSolver => defaultSolver;
    IVerifier IProblem.defaultVerifier => defaultVerifier;

    public UtilCollectionGraph graph { get; set; }
    Graph IGraphProblem.graph => graph;

    public ComplexityClass complexityClass { get; } = ComplexityClass.NPComplete;

    public ProblemType problemType { get; } = ProblemType.NetworkDesign;

    public ProblemVariant problemVariant { get; } = ProblemVariant.Decision;

    // --- Properties ---
    public List<string> nodes { get; set; } = new();

    public List<(string source, string target, int weight)> edges { get; set; } = new();

    public string depot { get; set; } = string.Empty;

    public List<(string pickup, string delivery)> requests { get; set; } = new();

    public int K { get; set; }

    // --- Constructors ---
    public PDTSP()
        : this(_defaultInstance) { }

    public PDTSP(string GInput)
    {
        instance = GInput;

        StringParser pdtsp = new(InstanceGrammar);
        pdtsp.parse(GInput);

        nodes = pdtsp["N"].ToList().Select(node => node.ToString()).ToList();

        edges = pdtsp["E"]
            .ToList()
            .Select(edge =>
            {
                List<UtilCollection> endpoints = edge[0].ToList();

                return (
                    endpoints[0].ToString(),
                    endpoints[1].ToString(),
                    int.Parse(edge[1].ToString())
                );
            })
            .ToList();

        depot = pdtsp["D"].ToString();

        requests = pdtsp["R"]
            .ToList()
            .Select(request =>
            {
                List<UtilCollection> pair = request.ToList();

                return (pair[0].ToString(), pair[1].ToString());
            })
            .ToList();

        K = int.Parse(pdtsp["K"].ToString());

        graph = new UtilCollectionGraph(pdtsp["N"], pdtsp["E"]);
    }
}
