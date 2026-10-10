using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_TSP.Solvers;

class TSPBranchAndBound : ISolver<TSP, Tour>, IIncrementalSolver {

    // --- Fields ---
    public string solverName { get; } = "Traveling Salesperson Branch and Bound";
    public string solverDefinition { get; } = "This is a Branch and Bound solver for the NP-Complete Traveling Salesperson problem";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Corbin Hay" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Exact search WITH pruning/bounding (branch-and-bound) -- distinct from
    // TSPBruteForce, which does not prune.
    public SolverType solverType { get; } = SolverType.Backtracking;
    // Corrected from Exponential: the search tree is the same permutation tree TSPBruteForce
    // enumerates (each node is a distinct partial path with a fixed start node), so without
    // effective pruning (achievable on adversarial instances where the reduced-cost bound never
    // tightens) the node count is O((n-1)!) -- factorial, not merely exponential, growth.
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Factorial;
    // Declared, not derived. Worst case (bound provides no effective pruning) visits O(n!) search
    // nodes; each node does O(n^2) work (CloneMatrix + ReduceMatrix over the full cost matrix).
    public string complexity { get; } = "O(n^2 * n!), n = |nodes|";

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

    // Steps, in the order the search runs (best first, so the path on screen jumps between branches): a Try for the path
    // taken off the queue to be expanded; for each way of extending it, an Accept when the branch is queued, or a Reject when
    // there is no such edge or its lower bound is already no better than the best tour so far; a Reject for a queued path
    // dropped because the best tour so far has improved since it was queued; and for a complete tour, an Accept if it beats
    // the best so far or a Reject if not. The greedy tour the search starts from is the first step. Its own steps are not
    // recorded (the greedy solver runs with recording off). Recorder state lives only in this call.
    public string Solve(TSP problem, StepRecorder<Tour> rec) {
        if (timerHasExpired) {
            rec.Done(new Tour([]), false, "Ran out of time before the search finished.");
            return "timeout";
        }

        List<string> nodes = problem.nodes;
        int n = nodes.Count;

        if (n == 0) {
            rec.Done(new Tour([]), false, "There are no cities to tour.");
            return "{}";
        }

        List<string> Names(List<int> path, bool close = false) {
            var names = path.Select(i => nodes[i]).ToList();
            if (close) names.Add(nodes[path[0]]);
            return names;
        }

        // Build original cost matrix
        double[,] originalMatrix = BuildCostMatrix(problem, nodes);

        double[,] initialMatrix = CloneMatrix(originalMatrix);
        double initialLowerBound = ReduceMatrix(initialMatrix);

        // Get initial BSSF using greedy solver
        List<int>? bestPath = GetInitialBSSF(problem, nodes);
        double bestCost = bestPath is null ? INF : ComputeTourCost(bestPath, originalMatrix);
        if (bestPath is not null) {
            var greedy = bestPath;
            double greedyCost = bestCost;
            rec.Accept(() => new Tour(Names(greedy, close: true)),
                () => $"Start from the greedy tour {GraphTourFrames.Arrows(Names(greedy, close: true))}: total {TSPTourSteps.Num(greedyCost)}, the best so far.",
                () => TSPTourSteps.EdgeIds(problem, Names(greedy, close: true)));
        } else {
            rec.Try(() => new Tour([nodes[0]]), () => $"The greedy solver found no tour to start from, so there is no best so far. Start at {nodes[0]}.", nodes[0]);
        }

        bool[] rootVisited = new bool[n];
        rootVisited[0] = true;

        SearchState root = new SearchState {
            path = new List<int> { 0 },
            matrix = initialMatrix,
            bound = initialLowerBound,
            visited = rootVisited
        };

        var pq = new PriorityQueue<SearchState, PQPriority>();
        Enqueue(pq, root);

        while (pq.Count > 0) {
            if (timerHasExpired) {
                rec.Done(new Tour([]), false, "Ran out of time before the search finished.");
                return "timeout";
            }
            SearchState current = pq.Dequeue();
            double bestNow = bestCost;
            if (current.bound >= bestCost) {
                rec.Reject(() => new Tour(Names(current.path!)),
                    () => $"Drop the path {GraphTourFrames.Arrows(Names(current.path!))}: its lower bound {TSPTourSteps.Num(current.bound)} is no better than the best tour so far ({TSPTourSteps.Num(bestNow)}).",
                    nodes[current.path![^1]]);
                continue; // Prune
            }
            if (current.path!.Count == n) {  // Complete tour
                double tourCost = ComputeTourCost(current.path, originalMatrix);
                if (tourCost < bestCost) {  // Update BSSF
                    double previousBest = bestCost;
                    bestCost = tourCost;
                    bestPath = new List<int>(current.path);
                    rec.Accept(() => new Tour(Names(current.path, close: true)),
                        () => $"Close the tour {nodes[current.path[^1]]} → {nodes[current.path[0]]}: total {TSPTourSteps.Num(tourCost)}, the best so far{(double.IsPositiveInfinity(previousBest) ? "" : $" (it beats {TSPTourSteps.Num(previousBest)})")}.",
                        nodes[current.path[^1]], nodes[current.path[0]]);
                } else {
                    rec.Reject(() => new Tour(Names(current.path, close: true)),
                        () => double.IsPositiveInfinity(tourCost)
                            ? $"Close the tour {nodes[current.path[^1]]} → {nodes[current.path[0]]}: there is no such edge."
                            : $"Close the tour {nodes[current.path[^1]]} → {nodes[current.path[0]]}: total {TSPTourSteps.Num(tourCost)} is no better than the best so far ({TSPTourSteps.Num(bestNow)}).",
                        // The path was built hop by hop, so what failed is the closing hop, not the whole tour.
                        () => double.IsPositiveInfinity(tourCost)
                            ? new[] { nodes[current.path[^1]], nodes[current.path[0]] }
                            : new[] { nodes[current.path[^1]], nodes[current.path[0]], GraphTourFrames.EdgeId(false, nodes[current.path[^1]], nodes[current.path[0]]) });
                }
                continue;
            }
            int lastNode = current.path[^1];
            rec.Try(() => new Tour(Names(current.path)),
                () => $"Expand the path {GraphTourFrames.Arrows(Names(current.path))}: lower bound {TSPTourSteps.Num(current.bound)}.",
                nodes[lastNode]);
            for (int next = 0; next < n; next++) {
                if (current.visited![next])
                    continue;

                double edgeCost = current.matrix![lastNode, next];
                if (double.IsPositiveInfinity(edgeCost)) {
                    int missingNext = next;
                    rec.Reject(() => new Tour(Names(current.path).Append(nodes[missingNext])),
                        () => $"There is no edge between {nodes[lastNode]} and {nodes[missingNext]}.",
                        nodes[missingNext]);
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
                    int skipped = next;
                    double boundNow = bestCost;
                    rec.Reject(() => new Tour(Names(current.path).Append(nodes[skipped])),
                        () => $"Skip {nodes[skipped]} after {nodes[lastNode]}: the lower bound {TSPTourSteps.Num(childBound)} is no better than the best tour so far ({TSPTourSteps.Num(boundNow)}).",
                        nodes[skipped], GraphTourFrames.EdgeId(false, nodes[lastNode], nodes[skipped]));
                    continue;   //Prune
                }

                List<int> childPath = new List<int>(current.path) { next };
                bool[] childVisited = (bool[])current.visited.Clone();
                childVisited[next] = true;

                int queued = next;
                rec.Accept(() => new Tour(Names(childPath)),
                    () => $"Branch {nodes[lastNode]} → {nodes[queued]} (weight {TSPTourSteps.Num(originalMatrix[lastNode, queued])}, path cost {TSPTourSteps.Num(PathCost(childPath, originalMatrix))}): lower bound {TSPTourSteps.Num(childBound)}, queued.",
                    nodes[queued], GraphTourFrames.EdgeId(false, nodes[lastNode], nodes[queued]));

                // Create child state and enqueue
                SearchState child = new SearchState {
                    path = childPath,
                    visited = childVisited,
                    matrix = childMatrix,
                    bound = childBound
                };
                Enqueue(pq, child);
            }
        }
        if (bestPath is not null && IsCompleteTour(bestPath, originalMatrix)) {
            string certificate = PathToCertificate(bestPath, nodes);
            if (problem.defaultVerifier.verify(problem, certificate)) {
                var best = bestPath;
                double finalCost = bestCost;
                rec.Done(GraphTourFrames.ParseTour(certificate), true,
                    $"The cheapest tour is {GraphTourFrames.Arrows(Names(best, close: true))}, total {TSPTourSteps.Num(finalCost)}, within K = {problem.K}.");
                return certificate;
            }
            rec.Done(new Tour([]), false, $"The cheapest tour costs {TSPTourSteps.Num(bestCost)}, which is more than K = {problem.K}.");
            return "{}";
        }
        rec.Done(new Tour([]), false, "No tour visits every city once.");
        return "{}";
    }

    // The weight of a path's steps (not counting the way back to the start).
    private static double PathCost(List<int> path, double[,] costMatrix) {
        double total = 0;
        for (int i = 0; i + 1 < path.Count; i++) total += costMatrix[path[i], path[i + 1]];
        return total;
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
        string greedyCertificate = new TSPGreedy() {
            timerHasExpired = timerHasExpired
        }.solve(problem);
        return CertificateToPath(greedyCertificate, nodes);
    }

    private static List<int>? CertificateToPath(string certificate, List<string> nodes) {
        if (string.IsNullOrWhiteSpace(certificate) || certificate == "{}" || certificate == "timeout")
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
}

