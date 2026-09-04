using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;

namespace API.Problems.NPComplete.NPC_TSP.Solvers;

class TSPGreedy : ISolver<TSP> {

    // --- Fields ---
    public string solverName { get; } = "Traveling Salesperson Greedy";
    public string solverDefinition { get; } = "This is a greedy solver for the NP-Complete Traveling Sales Person problem";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Derek Winmill", "Beau Williams", "Corbin Hay" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Irrevocable locally-optimal choice each step (nearest-neighbor).
    public SolverType solverType { get; } = SolverType.Greedy;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
    // Declared, not derived. Nearest-neighbor is restarted from each of the n start nodes; each
    // construction does up to n steps, each scanning all n candidate nodes, each calling
    // getEdgeWeight() which does an O(m) linear scan of the edge list (no adjacency lookup) --
    // O(n^2 * m) per start, times n starts. No approximation-ratio guarantee is proven or
    // enforced here (TSP_Class.cs places no triangle-inequality/metric constraint on edge
    // weights, and a failed nearest-neighbor pass can also skip a start entirely), so this
    // stays Greedy rather than Approximation.
    public string complexity { get; } = "O(n^3 * m), n = |nodes|, m = |edges|";

    // --- Methods Including Constructors ---
    public TSPGreedy() {

    }

    private bool getEdgeWeight(TSP tsp, string from, string to, out int weight) {
        var directEdge = tsp.edges.FirstOrDefault(edge => edge.source == from && edge.target == to);
        if (directEdge != default) {
            weight = directEdge.weight;
            return true;
        }

        var reverseEdge = tsp.edges.FirstOrDefault(edge => edge.source == to && edge.target == from);
        if (reverseEdge != default) {
            weight = reverseEdge.weight;
            return true;
        }

        weight = 0;
        return false;
    }

    private string pathToCertificate(List<string> path) {
        return "{" + string.Join(',', path) + "}";
    }

    public string solve(TSP tsp) {
        if (tsp.nodes.Count == 0) {
            return "{}";
        }

        string bestCertificate = "{}";
        int bestTotalWeight = int.MaxValue;

        for (int startIndex = 0; startIndex < tsp.nodes.Count; startIndex++) {
            string startNode = tsp.nodes[startIndex];
            List<string> route = new List<string>();
            HashSet<string> visited = new HashSet<string>();

            string currentNode = startNode;
            route.Add(currentNode);
            visited.Add(currentNode);

            bool pathFailed = false;
            int totalWeight = 0;

            while (visited.Count < tsp.nodes.Count) {
                string nextNode = "";
                int bestWeight = int.MaxValue;

                foreach (string node in tsp.nodes) {
                    if (visited.Contains(node)) {
                        continue;
                    }

                    // This is the greedy choice: we pick the next node with the smallest edge weight from the current node
                    int edgeWeight;
                    bool edgeExists = getEdgeWeight(tsp, currentNode, node, out edgeWeight);
                    if (edgeExists && edgeWeight < bestWeight) {
                        bestWeight = edgeWeight;
                        nextNode = node;
                    }
                }

                if (nextNode == "") {
                    pathFailed = true;
                    break;
                }

                totalWeight += bestWeight;
                route.Add(nextNode);
                visited.Add(nextNode);
                currentNode = nextNode;
            }

            if (pathFailed) {
                continue;
            }

            int returnEdgeWeight;
            if (!getEdgeWeight(tsp, currentNode, startNode, out returnEdgeWeight)) {
                continue;
            }

            totalWeight += returnEdgeWeight;
            route.Add(startNode);
            string certificate = pathToCertificate(route);

            if (tsp.defaultVerifier.verify(tsp, certificate) && totalWeight < bestTotalWeight) {
                bestTotalWeight = totalWeight;
                bestCertificate = certificate;
            }
        }

        return bestCertificate;
    }
}
