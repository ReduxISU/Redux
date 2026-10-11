using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_TSP.Solvers;

class TSPGreedy : ISolver<TSP, Tour>, IIncrementalSolver {

    // --- Fields ---
    public string solverName { get; } = "Traveling Salesperson Greedy";
    public string solverDefinition { get; } = "This is a greedy solver for the NP-Complete Traveling Salesperson problem";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
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
    public MathematicalFunction timeComplexity { get; } = new("n^3 * m", ", n = |nodes|, m = |edges|");

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

    public string solve(TSP tsp) => Solve(tsp, StepRecorder<Tour>.Off);

    // Steps: for each start city, a Try to start there, an Accept for every nearest-neighbour move (with the running path
    // cost), then either a Reject (a dead end, or a closing edge the graph does not have, or a closed tour that is over K or
    // no better than the best so far) or an Accept for a closed tour that is the best so far. Recorder state lives only in
    // this call.
    public string Solve(TSP tsp, StepRecorder<Tour> rec) {
        if (tsp.nodes.Count == 0) {
            rec.Done(new Tour([]), false, "There are no cities to tour.");
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
            rec.Try(() => new Tour(route), () => $"Start at {startNode}.", startNode);

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
                    string stuckAt = currentNode;
                    rec.Reject(() => new Tour(route),
                        () => $"Dead end at {stuckAt}: it has no edge to a city that is not already on the path, so starting from {startNode} fails.",
                        stuckAt);
                    break;
                }

                string previous = currentNode;
                totalWeight += bestWeight;
                route.Add(nextNode);
                visited.Add(nextNode);
                currentNode = nextNode;
                int weightOfMove = bestWeight, costSoFar = totalWeight;
                rec.Accept(() => new Tour(route),
                    () => $"Go {previous} → {nextNode} (weight {weightOfMove}, path cost {costSoFar}): the nearest city not yet visited.",
                    nextNode, GraphTourFrames.EdgeId(false, previous, nextNode));
            }

            if (pathFailed) {
                continue;
            }

            int returnEdgeWeight;
            if (!getEdgeWeight(tsp, currentNode, startNode, out returnEdgeWeight)) {
                string last = currentNode;
                rec.Reject(() => new Tour(route.Append(startNode)),
                    () => $"Close the tour {last} → {startNode}: there is no such edge, so starting from {startNode} fails.",
                    last, startNode);
                continue;
            }

            totalWeight += returnEdgeWeight;
            route.Add(startNode);
            string certificate = pathToCertificate(route);

            bool withinK = tsp.defaultVerifier.verify(tsp, certificate);
            if (withinK && totalWeight < bestTotalWeight) {
                int previousBest = bestTotalWeight, total = totalWeight;
                bestTotalWeight = totalWeight;
                bestCertificate = certificate;
                rec.Accept(() => new Tour(route),
                    () => $"Close the tour {route[^2]} → {startNode}: total {total}, the best so far{(previousBest == int.MaxValue ? "" : $" (it beats {previousBest})")}.",
                    route[^2], startNode);
            } else {
                int total = totalWeight, best = bestTotalWeight;
                rec.Reject(() => new Tour(route),
                    () => withinK
                        ? $"Close the tour {route[^2]} → {startNode}: total {total} is no better than the best so far ({best})."
                        : $"Close the tour {route[^2]} → {startNode}: total {total} is more than K = {tsp.K}.",
                    // The route was built hop by hop, so what failed is the closing hop, not the whole tour.
                    route[^2], startNode, GraphTourFrames.EdgeId(false, route[^2], startNode));
            }
        }

        if (bestCertificate == "{}") {
            rec.Done(new Tour([]), false, $"No start gave a tour within K = {tsp.K}.");
        } else {
            rec.Done(GraphTourFrames.ParseTour(bestCertificate), true,
                $"The best tour is {GraphTourFrames.Arrows(GraphTourFrames.ParseTour(bestCertificate).Order)}, total {bestTotalWeight}.");
        }
        return bestCertificate;
    }
}
