using System.Globalization;
using API.Interfaces;
using API.Problems.NPComplete.NPC_PDTSP.Verifiers;
using SPADE;

namespace API.Problems.NPComplete.NPC_PDTSP;

class PDTSP : IGraphProblem {
    public string problemName { get; } = "Pickup and Delivery Traveling Salesperson";
    public string problemLink { get; } = ReferenceLink;
    public string formalDefinition { get; } = "PDTSP = {<G,R,S,K> | G is a nonnegative weighted undirected graph, R pairs all nondepot vertices, and a Hamiltonian tour from S to S visits each pickup before its paired delivery and has cost <= K}";
    public string problemDefinition { get; } = "One vehicle starts at a depot, visits every pickup and delivery exactly once, and returns to the depot within a cost bound. Each request's pickup must occur before its delivery. Vehicle capacity is unlimited; there are no time windows or loading-order constraints.";
    public string inputDescription { get; } = "G, a weighted undirected graph; R, disjoint ordered (pickup,delivery) pairs covering every nondepot vertex; S, the depot; K, a nonnegative tour-cost bound";
    public string outputDescription { get; } = "A feasible depot-to-depot tour of cost at most K, or {} if none exists";
    internal const string Reference = "Renaud, J., Boctor, F. F., and Ouenniche, J. A heuristic for the pickup and delivery traveling salesman problem. Computers & Operations Research 27(9), 905-916, 2000.";
    internal const string ReferenceLink = "https://doi.org/10.1016/S0305-0548(99)00066-0";
    public string source { get; } = Reference;
    public string sourceLink { get; } = ReferenceLink;
    public string sourceFile { get; } = SourceFile.Path();
    public string wikiName { get; } = "";
    public string[] contributors { get; } = Array.Empty<string>();
    public ComplexityClass complexityClass { get; } = ComplexityClass.NPComplete;
    public ProblemType problemType { get; } = ProblemType.NetworkDesign | ProblemType.SequencingAndScheduling;
    public ProblemVariant problemVariant { get; } = ProblemVariant.Decision;

    public const string InstanceGrammar = "((N,E),R,S,K), where N is a nonempty set of labels, E is a set of ({u,v},w) edges with nonnegative 32-bit integer weights, R is a set of ordered (pickup,delivery) pairs, S is a depot in N, and K is a nonnegative 64-bit integer";
    private const string DefaultInstance = "(({S,P1,D1,P2,D2},{({S,P1},1),({P1,D1},1),({D1,P2},1),({P2,D2},1),({D2,S},1)}),{(P1,D1),(P2,D2)},S,5)";
    public string defaultInstance { get; } = DefaultInstance;
    public string instanceFormat { get; } = $"Format: {InstanceGrammar}. Each nondepot vertex occurs in exactly one request. Missing edges cannot be traversed; parallel edges and self-loops are forbidden. A depot-only graph with no requests has a zero-cost tour. Example: {DefaultInstance}";
    public string certificateFormat { get; } = $"Format: {PDTSPVerifier.CertificateGrammar}. Example: {PDTSPVerifier.CertificateExample}. {{}} denotes no solution and is not a valid tour certificate.";
    public string instance { get; }
    public IReadOnlyList<string> nodes { get; }
    public IReadOnlyList<(string source, string target, int weight)> edges { get; }
    public IReadOnlyList<(string pickup, string delivery)> requests { get; }
    public string depot { get; }
    public long K { get; }
    public Graph graph { get; }

    internal const long NoEdge = long.MaxValue;
    private readonly long[,] costs;
    internal int DepotIndex { get; }
    internal IReadOnlyDictionary<string, int> NodeIndices { get; }
    // -1 means this vertex is a pickup (or the depot).
    internal int[] PickupForDelivery { get; }
    internal long Distance(int from, int to) => costs[from, to];
    internal string TourCertificate(IEnumerable<int> tour) => "{" + string.Join(",", tour.Select(i => nodes[i])) + "}";

    public PDTSP() : this(DefaultInstance) { }

    public PDTSP(string stringInstance) {
        instance = stringInstance;
        try {
            if (string.IsNullOrWhiteSpace(stringInstance))
                throw new FormatException("Instance is empty.");

            string text = stringInstance.Trim();
            ValidateDelimiters(text);
            // Use SPADE's collection parser, then validate both structure and domain constraints.
            UtilCollection input = new(text);
            Require(input.IsOrdered() && input.Count() == 4, "Expected ((N,E),R,S,K).");
            Require(input[0].IsOrdered() && input[0].Count() == 2, "Expected graph (N,E).");
            UtilCollection nodeSet = input[0][0], edgeSet = input[0][1], requestSet = input[1];
            Require(!nodeSet.IsValue() && !nodeSet.IsOrdered(), "N must be a set.");
            Require(!edgeSet.IsValue() && !edgeSet.IsOrdered(), "E must be a set.");
            Require(!requestSet.IsValue() && !requestSet.IsOrdered(), "R must be a set.");

            var labels = nodeSet.ToList().Select(Label).ToArray();
            Require(labels.Length > 0, "N must contain a depot.");
            var indices = labels.Select((label, i) => (label, i)).ToDictionary(x => x.label, x => x.i, StringComparer.Ordinal);
            nodes = Array.AsReadOnly(labels);
            NodeIndices = indices;
            depot = Label(input[2]);
            Require(indices.ContainsKey(depot), "The depot must belong to N.");
            DepotIndex = indices[depot];
            Require(input[3].IsValue() && long.TryParse(input[3].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _), "K must be a nonnegative 64-bit integer.");
            K = long.Parse(input[3].ToString(), CultureInfo.InvariantCulture);
            Require(K >= 0, "K must be nonnegative.");

            costs = new long[labels.Length, labels.Length];
            for (int i = 0; i < labels.Length; i++)
                for (int j = 0; j < labels.Length; j++)
                    costs[i, j] = i == j ? 0 : NoEdge;

            var parsedEdges = new List<(string source, string target, int weight)>();
            foreach (var edge in edgeSet.ToList()) {
                Require(edge.IsOrdered() && edge.Count() == 2, "Expected edge ({u,v},w).");
                Require(!edge[0].IsValue() && !edge[0].IsOrdered() && edge[0].Count() == 2, "An undirected edge must contain two distinct endpoints.");
                var ends = edge[0].ToList().Select(Label).ToArray();
                Require(indices.ContainsKey(ends[0]) && indices.ContainsKey(ends[1]), "Edge endpoints must belong to N.");
                Require(edge[1].IsValue() && int.TryParse(edge[1].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _), "Edge weights must be nonnegative 32-bit integers.");
                int weight = int.Parse(edge[1].ToString(), CultureInfo.InvariantCulture);
                Require(weight >= 0, "Edge weights must be nonnegative.");
                int u = indices[ends[0]], v = indices[ends[1]];
                Require(u != v && costs[u, v] == NoEdge, "Self-loops and parallel edges are forbidden.");
                costs[u, v] = costs[v, u] = weight;
                parsedEdges.Add((ends[0], ends[1], weight));
            }
            edges = parsedEdges.AsReadOnly();

            PickupForDelivery = Enumerable.Repeat(-1, labels.Length).ToArray();
            var paired = new HashSet<int> { DepotIndex };
            var parsedRequests = new List<(string pickup, string delivery)>();
            foreach (var request in requestSet.ToList()) {
                Require(request.IsOrdered() && request.Count() == 2, "A request must be an ordered (pickup,delivery) pair.");
                string pickup = Label(request[0]), delivery = Label(request[1]);
                Require(indices.ContainsKey(pickup) && indices.ContainsKey(delivery), "Request endpoints must belong to N.");
                int p = indices[pickup], d = indices[delivery];
                Require(paired.Add(p) && paired.Add(d), "Requests must be disjoint, with distinct endpoints, and cannot include the depot.");
                PickupForDelivery[d] = p;
                parsedRequests.Add((pickup, delivery));
            }
            Require(paired.Count == labels.Length, "Every nondepot vertex must occur in exactly one request.");
            requests = parsedRequests.AsReadOnly();
            graph = new UtilCollectionGraph(nodeSet, edgeSet);
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            throw new ProblemParseException(problemName, stringInstance, ex.Message, ex);
        }
    }

    private static string Label(UtilCollection value) {
        Require(value.IsValue(), "Node labels must be scalar values.");
        string label = value.ToString().Trim();
        Require(label.Length > 0 && label.IndexOfAny(new[] { '{', '}', '(', ')', ',' }) < 0, "Node labels must be nonempty and contain no collection delimiters.");
        return label;
    }

    private static void ValidateDelimiters(string text) {
        Require(text[0] == '(' && text[^1] == ')', "The instance must be enclosed in parentheses.");
        var expected = new Stack<char>();
        for (int i = 0; i < text.Length; i++) {
            char c = text[i];
            if (c == '(' || c == '{') {
                expected.Push(c == '(' ? ')' : '}');
            } else if (c == ')' || c == '}') {
                Require(expected.Count > 0 && expected.Pop() == c, "Collection delimiters must match.");
                Require(expected.Count > 0 || i == text.Length - 1, "Unexpected text after the instance.");
            }
        }
        Require(expected.Count == 0, "Collection delimiters must match.");
    }

    private static void Require(bool condition, string message) {
        if (!condition)
            throw new FormatException(message);
    }
}
