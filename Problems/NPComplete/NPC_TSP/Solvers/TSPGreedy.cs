using API.Interfaces;
using API.Interfaces.Steps;
using static API.Interfaces.Graphs.GraphTourFrames;

namespace API.Problems.NPComplete.NPC_TSP.Solvers;

// The steps Traveling Salesperson Greedy records. For each start city: a Try to start there, an Accept for every
// nearest-neighbour move (with the running path cost), then either a Reject (a dead end, a closing edge the graph does
// not have, or a closed tour that is over K or no better than the best so far) or an Accept for a closed tour that is the
// best so far. The Done gives the best tour.
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

    // An outer solver's live time-limit flag (TSPBranchAndBound runs this for its first bound).
    private readonly Func<bool>? outerTimerHasExpired;
    internal TSPGreedy(Func<bool> outerTimerHasExpired) {
        this.outerTimerHasExpired = outerTimerHasExpired;
    }

    // How the steps show the route so far: the path, and the city it ends on with the edge it came by (just the city at the
    // start). A step about something else (a dead end, the closing hop) says its own focus.
    private static RunSteps<List<string>, Tour> NewSteps(StepRecorder<Tour> rec) => new(rec,
        // What every picture shows: the route, and its last city and hop.
        partial: route => new Tour(route),
        focus: route => route.Count < 2 ? [route[^1]] : [route[^1], EdgeId(false, route[^2], route[^1])]);

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

    // Nearest neighbour from every start city in turn, keeping the cheapest closed tour within K.
    public string Solve(TSP tsp, StepRecorder<Tour> rec) {
        var steps = NewSteps(rec);
        if (tsp.nodes.Count == 0) {
            steps.Done(new Tour([]), false, $"There are no cities to tour.");
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
            steps.Try(route, $"Start at {startNode}.");

            bool pathFailed = false;
            int totalWeight = 0;

            while (visited.Count < tsp.nodes.Count) {
                if (timerHasExpired || outerTimerHasExpired?.Invoke() == true) return SolveRun.TimedOutAnswer;
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
                    steps.Reject(route, $"Dead end at {currentNode}: it has no edge to a city that is not already on the path, so starting from {startNode} fails.",
                        focus: () => [currentNode]);
                    break;
                }

                string previous = currentNode;
                totalWeight += bestWeight;
                route.Add(nextNode);
                visited.Add(nextNode);
                currentNode = nextNode;
                steps.Accept(route, $"Go {previous} → {nextNode} (weight {bestWeight}, path cost {totalWeight}): the nearest city not yet visited.");
            }

            if (pathFailed) {
                continue;
            }

            int returnEdgeWeight;
            if (!getEdgeWeight(tsp, currentNode, startNode, out returnEdgeWeight)) {
                steps.Reject(route, $"Close the tour {currentNode} → {startNode}: there is no such edge, so starting from {startNode} fails.",
                    focus: () => [currentNode, startNode], partial: () => new Tour(route.Append(startNode)));
                continue;
            }

            totalWeight += returnEdgeWeight;
            route.Add(startNode);
            string certificate = pathToCertificate(route);

            bool withinK = tsp.defaultVerifier.verify(tsp, certificate);
            if (withinK && totalWeight < bestTotalWeight) {
                steps.Accept(route, $"Close the tour {currentNode} → {startNode}: total {totalWeight}, the best so far{(bestTotalWeight == int.MaxValue ? "" : $" (it beats {bestTotalWeight})")}.",
                    focus: () => [currentNode, startNode]);
                bestTotalWeight = totalWeight;
                bestCertificate = certificate;
            } else {
                // The route was built hop by hop, so what failed is the closing hop, not the whole tour.
                steps.Reject(route, $"Close the tour {currentNode} → {startNode}: total {totalWeight} {(withinK ? $"is no better than the best so far ({bestTotalWeight})" : $"is more than K = {tsp.K}")}.",
                    focus: () => [currentNode, startNode, EdgeId(false, currentNode, startNode)]);
            }
        }

        if (bestCertificate == "{}")
            steps.Done(new Tour([]), false, $"No start gave a tour within K = {tsp.K}.");
        else
            steps.Done(ParseTour(bestCertificate), true, $"The best tour is {Arrows(ParseTour(bestCertificate).Order)}, total {bestTotalWeight}.");
        return bestCertificate;
    }
}
