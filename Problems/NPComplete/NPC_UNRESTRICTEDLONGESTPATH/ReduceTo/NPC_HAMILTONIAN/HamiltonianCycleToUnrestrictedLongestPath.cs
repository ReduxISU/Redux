using System.Globalization;
using API.Interfaces;
using API.Problems.NPComplete.NPC_HAMILTONIAN;

namespace API.Problems.NPComplete.NPC_UNRESTRICTEDLONGESTPATH.ReduceTo.NPC_HAMILTONIAN;

class HamiltonianCycleToUnrestrictedLongestPath : IReduction<HAMILTONIAN, UNRESTRICTEDLONGESTPATH> {
    public string reductionName { get; } = "Hamiltonian Cycle Vertex Split";
    public string reductionDefinition { get; } =
        "Splits one vertex into two copies, attaches a leaf to each copy, and asks for a path through every output vertex.";
    public string source { get; } = "Vertex-splitting construction with a proof in both directions.";
    public string sourceFile { get; } =
        "Problems/NPComplete/NPC_UNRESTRICTEDLONGESTPATH/ReduceTo/NPC_HAMILTONIAN/HamiltonianCycleToUnrestrictedLongestPath.cs";
    public string[] contributors { get; } = Array.Empty<string>();
    public ReductionCost cost { get; } = ReductionCost.Linear;
    public ReductionType reductionType { get; } = ReductionType.LocalReplacement;
    public ReductionComplexityBucket complexityBucket { get; } = ReductionComplexityBucket.Polynomial;
    public string complexity { get; } =
        "O((n + m) log(n + m + 1)) ordered collection operations plus serialization and polynomial target parsing.";

    public HAMILTONIAN reductionFrom { get; }
    public UNRESTRICTEDLONGESTPATH reductionTo { get; private set; }

    public HamiltonianCycleToUnrestrictedLongestPath() : this(new HAMILTONIAN()) { }

    public HamiltonianCycleToUnrestrictedLongestPath(string instance) : this(new HAMILTONIAN(instance)) { }

    public HamiltonianCycleToUnrestrictedLongestPath(HAMILTONIAN from) {
        ArgumentNullException.ThrowIfNull(from);
        reductionFrom = from;
        reductionTo = reduce();
    }

    public UNRESTRICTEDLONGESTPATH reduce() {
        int n = reductionFrom.nodes.Count;
        if (n < 2) {
            // Redux accepts a singleton cycle exactly when it has a closing self-loop.
            bool hasLoop = n == 1 && reductionFrom.edges.Any(edge =>
                edge.Key == reductionFrom.nodes[0] && edge.Value == reductionFrom.nodes[0]);
            reductionTo = new UNRESTRICTEDLONGESTPATH(hasLoop ? "(({0},{}),0)" : "(({0},{}),1)");
            return reductionTo;
        }

        var vertexIds = IndexSourceVertices();
        // Source vertex 0 is replaced by copies 0 and n. The leaves are n+1 and n+2.
        int leftLeaf = checked(n + 1);
        int rightLeaf = checked(n + 2);
        var edges = new SortedSet<(int First, int Second)> { (0, leftLeaf), (n, rightLeaf) };

        foreach (var edge in reductionFrom.edges) {
            if (!vertexIds.TryGetValue(edge.Key, out int u) ||
                !vertexIds.TryGetValue(edge.Value, out int v) || u == v) {
                continue;
            }

            int first = Math.Min(u, v);
            int second = Math.Max(u, v);
            edges.Add((first, second));
            if (first == 0) {
                edges.Add((second, n));
            }
        }

        string vertices = string.Join(",", Enumerable.Range(0, checked(n + 3)).Select(FormatNumber));
        string pairs = string.Join(",", edges.Select(edge =>
            "{" + FormatNumber(edge.First) + "," + FormatNumber(edge.Second) + "}"));
        // A graph with n+3 vertices requires n+2 edges to visit every vertex.
        string instance = "(({" + vertices + "},{" + pairs + "})," + FormatNumber(rightLeaf) + ")";
        reductionTo = new UNRESTRICTEDLONGESTPATH(instance);
        return reductionTo;
    }

    public string mapSolutions(string problemFromSolution) {
        string? certificate = problemFromSolution?.Trim();
        if (certificate is null || certificate.Length < 2 || certificate[0] != '{' || certificate[^1] != '}') {
            throw new FormatException("Expected a brace-delimited Hamiltonian cycle.");
        }

        string[] order = certificate[1..^1].Split(',', StringSplitOptions.TrimEntries);
        if (order.Any(vertex => vertex.Length == 0 || vertex.Contains('{') || vertex.Contains('}')) ||
            !reductionFrom.defaultVerifier.verify(reductionFrom, "{" + string.Join(",", order) + "}")) {
            throw new FormatException("The certificate is not a Hamiltonian cycle of the source graph.");
        }

        int n = reductionFrom.nodes.Count;
        if (n == 1) {
            return "{0}";
        }

        // The verifier accepts an optional repeated start at the end. Use only the n cycle vertices.
        var vertexIds = IndexSourceVertices();
        int index = Array.IndexOf(order, vertexIds.First().Key, 0, n);
        var path = new List<string> { FormatNumber(n + 1), "0" };
        for (int count = 1; count < n; count++) {
            index = (index + 1) % n;
            path.Add(FormatNumber(vertexIds[order[index]]));
        }
        path.Add(FormatNumber(n));
        path.Add(FormatNumber(n + 2));
        return "{" + string.Join(",", path) + "}";
    }

    private SortedDictionary<string, int> IndexSourceVertices() {
        // The Hamiltonian solver permutes the source node list in place.
        var ids = new SortedDictionary<string, int>(StringComparer.Ordinal);
        int index = 0;
        foreach (string vertex in reductionFrom.nodes.OrderBy(vertex => vertex, StringComparer.Ordinal)) {
            ids.Add(vertex, index++);
        }
        return ids;
    }

    private static string FormatNumber(int value) => value.ToString(CultureInfo.InvariantCulture);
}
