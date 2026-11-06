using API.Interfaces;
using API.Interfaces.Steps;
using static API.Interfaces.Graphs.GraphTourFrames;
using static API.Problems.NPComplete.NPC_TSP.Steps.TSPTourSteps;

namespace API.Problems.NPComplete.NPC_TSP.Solvers;

// The search starts from the greedy tour (an Accept; the greedy solver's own steps are not recorded) and records, best first:
// a Try for each path it expands, an Accept for each branch it queues, a Reject for a branch with no edge or a bound no better
// than the best tour so far, a Reject for a queued path the best tour has caught up with, and for a complete tour an Accept
// if it beats the best so far or a Reject if not. The SearchTree builds the Search Tree picture's nodes from those steps.
class TSPBranchAndBound : ISolver<TSP, Tour>, IIncrementalSolver {

    // --- Fields ---
    public string solverName { get; } = "Traveling Salesperson Branch and Bound";
    public string solverDefinition { get; } = "This is a Branch and Bound solver for the NP-Complete Traveling Salesperson problem";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Corbin Hay" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Exact search WITH pruning/bounding (branch-and-bound) -- distinct from
    // TSPBruteForce, which does not prune. Not DepthFirstSearch: Enqueue() orders
    // the frontier by bound per depth in a priority queue, which is best-first, not depth-first.
    public SolverType solverType { get; } = SolverType.Backtracking;
    // Corrected from Exponential: the search tree is the same permutation tree TSPBruteForce
    // enumerates (each node is a distinct partial path with a fixed start node), so without
    // effective pruning (achievable on adversarial instances where the reduced-cost bound never
    // tightens) the node count is O((n-1)!) -- factorial, not merely exponential, growth.
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Factorial;
    // Declared, not derived. Worst case (bound provides no effective pruning) visits O(n!) search
    // nodes; each node does O(n^2) work (CloneMatrix + ReduceMatrix over the full cost matrix).
    public MathematicalFunction timeComplexity { get; } = new("n^2 * n!", ", n = |nodes|");

    private const double INF = double.PositiveInfinity;
    private long pushID = 0;

    private class SearchState {
        public List<int>? path;
        public double[,]? matrix;
        public double bound;
        public bool[]? visited;
    }

    private readonly struct PQPriority : IComparable<PQPriority> {
        public double score { get; }
        public double bound { get; }
        public int negDepth { get; }
        public long id { get; }

        public PQPriority(double score, double bound, int negDepth, long id) {
            this.score = score;
            this.bound = bound;
            this.negDepth = negDepth;
            this.id = id;
        }

        /// Compare by score, then bound, then depth, then ID to ensure deterministic ordering
        public int CompareTo(PQPriority other) {
            int cmp = score.CompareTo(other.score);
            if (cmp != 0) return cmp;
            cmp = bound.CompareTo(other.bound);
            if (cmp != 0) return cmp;
            cmp = negDepth.CompareTo(other.negDepth);
            if (cmp != 0) return cmp;
            return id.CompareTo(other.id);
        }
    }
    // --- Methods Including Constructors ---
    public TSPBranchAndBound() { }

    public string solve(TSP problem) => Solve(problem, StepRecorder<Tour>.Off);

    public string Solve(TSP problem, StepRecorder<Tour> rec) {
        var tree = new SearchTree<SearchState, Tour>(rec);
        if (timerHasExpired) {
            // A timed run is stopped by SolveRun.Record, which puts its stopped Done in place of this one.
            tree.Done(new Tour([]), false, "Ran out of time before the search finished.");
            return SolveRun.TimedOutAnswer;
        }

        List<string> nodes = problem.nodes;
        int n = nodes.Count;

        if (n == 0) {
            tree.Done(new Tour([]), false, "There are no cities to tour.");
            return "{}";
        }

        // Build original cost matrix
        double[,] originalMatrix = BuildCostMatrix(problem, nodes);

        double[,] initialMatrix = CloneMatrix(originalMatrix);
        double initialLowerBound = ReduceMatrix(initialMatrix);

        // Get initial BSSF using greedy solver
        List<int>? bestPath = GetInitialBSSF(problem, nodes);
        double bestCost = bestPath is null ? INF : ComputeTourCost(bestPath, originalMatrix);

        bool[] rootVisited = new bool[n];
        rootVisited[0] = true;

        SearchState root = new SearchState {
            path = new List<int> { 0 },
            matrix = initialMatrix,
            bound = initialLowerBound,
            visited = rootVisited,
        };
        bool beatGreedy = false;
        tree.Root(root, nodes[0], $"lb {Num(root.bound)}");
        if (bestPath is not null)
            tree.Accept(() => new Tour(Names(nodes, bestPath, close: true)), $"Start from the greedy tour {Arrows(Names(nodes, bestPath, close: true))}: total {Num(bestCost)}, the best so far.",
                () => EdgeIds(problem, Names(nodes, bestPath, close: true)), child: "greedy tour", bound: $"tour {Num(bestCost)}");
        else
            tree.Try(() => new Tour([nodes[0]]), $"The greedy solver found no tour to start from, so there is no best so far. Start at {nodes[0]}.", () => [nodes[0]]);

        var pq = new PriorityQueue<SearchState, PQPriority>();
        Enqueue(pq, root);

        while (pq.Count > 0) {
            if (timerHasExpired) {
                tree.Done(new Tour([]), false, "Ran out of time before the search finished.");
                return SolveRun.TimedOutAnswer;
            }
            SearchState current = pq.Dequeue();
            if (current.bound >= bestCost) {
                tree.Reject(() => new Tour(Names(nodes, current.path!)), $"Drop the path {Arrows(Names(nodes, current.path!))}: its lower bound {Num(current.bound)} is no better than the best tour so far ({Num(bestCost)}).",
                    () => [nodes[current.path![^1]]], at: current, bound: $"lb {Num(current.bound)} ≥ {Num(bestCost)}");
                continue; // Prune
            }
            if (current.path!.Count == n) {  // Complete tour
                double tourCost = ComputeTourCost(current.path, originalMatrix);
                string last = nodes[current.path[^1]], first = nodes[current.path[0]];
                if (tourCost < bestCost) {  // Update BSSF
                    tree.Accept(() => new Tour(Names(nodes, current.path, close: true)), $"Close the tour {last} → {first}: total {Num(tourCost)}, the best so far{ItBeats(bestCost)}.", () => [last, first],
                        at: current, bound: $"tour {Num(tourCost)}");
                    bestCost = tourCost;
                    bestPath = new List<int>(current.path);
                    beatGreedy = true;
                } else if (double.IsPositiveInfinity(tourCost)) {
                    tree.Reject(() => new Tour(Names(nodes, current.path, close: true)), $"Close the tour {last} → {first}: there is no such edge.", () => [last, first],
                        at: current, bound: "no way back", state: SearchNodeState.Failed);
                } else {
                    // The path was built hop by hop, so what failed is the closing hop, not the whole tour.
                    tree.Reject(() => new Tour(Names(nodes, current.path, close: true)), $"Close the tour {last} → {first}: total {Num(tourCost)} is no better than the best so far ({Num(bestCost)}).",
                        () => [last, first, EdgeId(false, last, first)], at: current, bound: $"tour {Num(tourCost)}", state: SearchNodeState.Failed);
                }
                continue;
            }
            int lastNode = current.path[^1];
            tree.Try(() => new Tour(Names(nodes, current.path)), $"Expand the path {Arrows(Names(nodes, current.path))}: lower bound {Num(current.bound)}.", () => [nodes[lastNode]], at: current);
            for (int next = 0; next < n; next++) {
                if (current.visited![next])
                    continue;

                double edgeCost = current.matrix![lastNode, next];
                if (double.IsPositiveInfinity(edgeCost)) {
                    tree.Reject(() => new Tour(Names(nodes, current.path).Append(nodes[next])), $"There is no edge between {nodes[lastNode]} and {nodes[next]}.", () => [nodes[next]],
                        child: nodes[next], bound: "no edge");
                    continue;
                }

                double[,] childMatrix = CloneMatrix(current.matrix);
                // Invalidate outgoing edges from current
                for (int j = 0; j < n; j++)
                    childMatrix[lastNode, j] = INF;

                // Invalidate incoming edges to next
                for (int i = 0; i < n; i++)
                    childMatrix[i, next] = INF;

                // Invalidate edge back to start
                childMatrix[next, 0] = INF;

                // Prevent immediate reverse edge
                childMatrix[next, lastNode] = INF;

                double reductionCost = ReduceMatrix(childMatrix);
                double childBound = current.bound + edgeCost + reductionCost;

                if (childBound >= bestCost) {
                    tree.Reject(() => new Tour(Names(nodes, current.path).Append(nodes[next])), $"Skip {nodes[next]} after {nodes[lastNode]}: the lower bound {Num(childBound)} is no better than the best tour so far ({Num(bestCost)}).",
                        () => [nodes[next], EdgeId(false, nodes[lastNode], nodes[next])], child: nodes[next], bound: $"lb {Num(childBound)} ≥ {Num(bestCost)}");
                    continue;   //Prune
                }

                List<int> childPath = new List<int>(current.path) { next };
                bool[] childVisited = (bool[])current.visited.Clone();
                childVisited[next] = true;

                // Create child state and enqueue
                SearchState child = new SearchState {
                    path = childPath,
                    visited = childVisited,
                    matrix = childMatrix,
                    bound = childBound,
                };
                tree.Accept(() => new Tour(Names(nodes, childPath)), $"Branch {nodes[lastNode]} → {nodes[next]} (weight {Num(originalMatrix[lastNode, next])}, path cost {Num(PathCost(childPath, originalMatrix))}): lower bound {Num(childBound)}, queued.",
                    () => [nodes[next], EdgeId(false, nodes[lastNode], nodes[next])], child: nodes[next], queued: child, bound: $"lb {Num(childBound)}");
                Enqueue(pq, child);
            }
        }
        if (bestPath is not null && IsCompleteTour(bestPath, originalMatrix)) {
            string certificate = PathToCertificate(bestPath, nodes);
            if (problem.defaultVerifier.verify(problem, certificate)) {
                tree.Done(ParseTour(certificate), true, $"The cheapest tour is {(beatGreedy ? "" : "the greedy tour ")}{Arrows(Names(nodes, bestPath, close: true))}, total {Num(bestCost)}, within K = {problem.K}{(beatGreedy ? "" : ": the search found nothing cheaper")}.");
                return certificate;
            }
            tree.Done(new Tour([]), false, $"The cheapest tour costs {Num(bestCost)}, which is more than K = {problem.K}.");
            return "{}";
        }
        tree.Done(new Tour([]), false, "No tour visits every city once.");
        return "{}";
    }

    private void Enqueue(PriorityQueue<SearchState, PQPriority> pq, SearchState state) {
        int depth = state.path!.Count;
        double score = state.bound / depth;

        var priority = new PQPriority(score, state.bound, -depth, pushID++);
        pq.Enqueue(state, priority);
    }

    private static double ReduceMatrix(double[,] matrix) {
        int n = matrix.GetLength(0);
        double reductionCost = 0;

        // Row reduction
        for (int i = 0; i < n; i++) {
            double rowMin = INF;
            for (int j = 0; j < n; j++) {
                if (matrix[i, j] < rowMin) {
                    rowMin = matrix[i, j];
                }
            }
            if (rowMin > 0 && rowMin < INF) {
                reductionCost += rowMin;
                for (int j = 0; j < n; j++) {
                    if (matrix[i, j] < INF) {
                        matrix[i, j] -= rowMin;
                    }
                }
            }
        }

        // Column reduction
        for (int j = 0; j < n; j++) {
            double colMin = INF;
            for (int i = 0; i < n; i++) {
                if (matrix[i, j] < colMin) {
                    colMin = matrix[i, j];
                }
            }
            if (colMin > 0 && colMin < INF) {
                reductionCost += colMin;
                for (int i = 0; i < n; i++) {
                    if (matrix[i, j] < INF) {
                        matrix[i, j] -= colMin;
                    }
                }
            }
        }
        return reductionCost;
    }

    private static double[,] CloneMatrix(double[,] matrix) {
        int n = matrix.GetLength(0);
        double[,] clone = new double[n, n];
        for (int i = 0; i < n; i++) {
            for (int j = 0; j < n; j++) {
                clone[i, j] = matrix[i, j];
            }
        }
        return clone;
    }

    private static double ComputeTourCost(List<int> path, double[,] costMatrix) {
        double totalCost = 0;
        int n = path.Count;
        if (n == 0)
            return INF;
        for (int i = 0; i < n - 1; i++) {   // Sum edge costs along the path
            double edgeCost = costMatrix[path[i], path[i + 1]];
            if (edgeCost == INF) {
                return INF; // Invalid tour
            }
            totalCost += edgeCost;
        }
        double returnEdgeCost = costMatrix[path[^1], path[0]];
        if (returnEdgeCost == INF) {
            return INF; // Invalid tour
        }
        totalCost += returnEdgeCost;    // Add cost to return to start
        return totalCost;
    }

    private static bool IsCompleteTour(List<int> path, double[,] costMatrix) {
        if (path.Count != costMatrix.GetLength(0)) {
            return false; // Not all nodes visited
        }
        return ComputeTourCost(path, costMatrix) < INF; // Valid tour if cost is finite
    }

    private static string PathToCertificate(List<int> path, List<string> nodes) {
        string certificate = "";
        // Convert path of indices back to node names
        foreach (int i in path) {
            certificate += nodes[i] + ",";
        }
        // Add return to start
        return "{" + certificate + nodes[path[0]] + "}";
    }

    private List<int>? GetInitialBSSF(TSP problem, List<string> nodes) {
        string greedyCertificate = new TSPGreedy(() => timerHasExpired).solve(problem);
        return CertificateToPath(greedyCertificate, nodes);
    }

    private static List<int>? CertificateToPath(string certificate, List<string> nodes) {
        if (string.IsNullOrWhiteSpace(certificate) || certificate == "{}" || certificate == SolveRun.TimedOutAnswer)
            return null;
        // Remove braces and split by commas
        string trimmed = certificate.Trim().TrimStart('{').TrimEnd('}');
        string[] parts = trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries);
        // Validate that we have at least 2 nodes (start and return)
        if (parts.Length < 2)
            return null;

        // Build mapping from node names to indices
        Dictionary<string, int> nodeToIndex = new Dictionary<string, int>();
        for (int i = 0; i < nodes.Count; i++) {
            nodeToIndex[nodes[i]] = i;
        }

        List<int> path = new List<int>();
        // Convert node names to indices, ignoring the last one since it's a repeat of the start
        for (int i = 0; i < parts.Length - 1; i++) {
            string node = parts[i].Trim();

            if (!nodeToIndex.TryGetValue(node, out int index))
                return null;

            path.Add(index);
        }

        return path;
    }

    private static double[,] BuildCostMatrix(TSP problem, List<string> nodes) {
        int n = nodes.Count;
        double[,] matrix = new double[n, n];

        // Initialize all entries to infinity
        for (int i = 0; i < n; i++) {
            for (int j = 0; j < n; j++) {
                matrix[i, j] = INF;
            }
        }
        // Build mapping from node names to indices
        Dictionary<string, int> nodeToIndex = new Dictionary<string, int>();
        for (int i = 0; i < n; i++) {
            nodeToIndex[nodes[i]] = i;
        }
        // Fill in the cost matrix based on edges
        foreach (var edge in problem.edges) {
            int i = nodeToIndex[edge.source];
            int j = nodeToIndex[edge.target];
            matrix[i, j] = edge.weight;
            matrix[j, i] = edge.weight; // Undirected graph
        }

        return matrix;
    }

    // --- What the steps say ---

    // The city names of a path, closed back to its start when `close` is set.
    private static List<string> Names(List<string> cities, List<int> path, bool close = false) {
        var names = path.Select(i => cities[i]).ToList();
        if (close) names.Add(cities[path[0]]);
        return names;
    }

    // The weight of a path's steps (not counting the way back to the start).
    private static double PathCost(List<int> path, double[,] costs) {
        double total = 0;
        for (int i = 0; i + 1 < path.Count; i++) total += costs[path[i], path[i + 1]];
        return total;
    }

    // " (it beats 5,075)", or nothing when there was no best tour to beat.
    private static string ItBeats(double previousBest) => double.IsPositiveInfinity(previousBest) ? "" : $" (it beats {Num(previousBest)})";
}

