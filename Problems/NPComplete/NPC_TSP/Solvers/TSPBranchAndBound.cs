using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Graphs.GraphParser;

namespace API.Problems.NPComplete.NPC_TSP.Solvers;

class TSPBranchAndBound : ISolver<TSP> {

    // --- Fields ---
    public string solverName {get;} = "Traveling Sales Person Branch and Bound Solver";
    public string solverDefinition {get;} = "This is a Branch and Bound solver for the NP-Complete Traveling Sales Person problem";
    public string source {get;} = "";
    public string[] contributors {get;} = { "Corbin Hay" };
    public bool timerHasExpired { get; set; }

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
    public TSPBranchAndBound() {}

    public string solve(TSP problem){
        if (timerHasExpired)
            return "timeout";

        List<string> nodes = problem.nodes;
        int n = nodes.Count;

        if (n == 0) {
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
            visited = rootVisited
        };

        var pq = new PriorityQueue<SearchState, PQPriority>();
        Enqueue(pq, root);

        while (pq.Count > 0) {
            if (timerHasExpired)
                return "timeout";
            SearchState current = pq.Dequeue();
            if (current.bound >= bestCost) {
                continue; // Prune
            }
            if (current.path!.Count == n) {  // Complete tour
                double tourCost = ComputeTourCost(current.path, originalMatrix);
                if (tourCost < bestCost) {  // Update BSSF
                    bestCost = tourCost;
                    bestPath = new List<int>(current.path);
                }
                continue;
            }
            int lastNode = current.path[^1];
            for (int next = 0; next < n; next++)
            {
                if (current.visited![next])
                    continue;

                double edgeCost = current.matrix![lastNode, next];
                if (double.IsPositiveInfinity(edgeCost))
                    continue;

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

                if (childBound >= bestCost)
                    continue;   //Prune

                List<int> childPath = new List<int>(current.path) { next };
                bool[] childVisited = (bool[])current.visited.Clone();
                childVisited[next] = true;

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
            if (problem.defaultVerifier.verify(problem, certificate))
            {
                return certificate;
            }
        }
        return "{}";
    }

    private void Enqueue(PriorityQueue<SearchState, PQPriority> pq, SearchState state)
    {
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
        foreach (int i in path)
        {
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

    private static List<int>? CertificateToPath(string certificate, List<string> nodes)
    {
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
        for (int i = 0; i < nodes.Count; i++)
        {
            nodeToIndex[nodes[i]] = i;
        }

        List<int> path = new List<int>();
        // Convert node names to indices, ignoring the last one since it's a repeat of the start
        for (int i = 0; i < parts.Length - 1; i++)
        {
            string node = parts[i].Trim();

            if (!nodeToIndex.TryGetValue(node, out int index))
                return null;

            path.Add(index);
        }

        return path;
    }

    private static double[,] BuildCostMatrix(TSP problem, List<string> nodes)
    {
        int n = nodes.Count;
        double[,] matrix = new double[n, n];

        // Initialize all entries to infinity
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                matrix[i, j] = INF;
            }
        }
        // Build mapping from node names to indices
        Dictionary<string, int> nodeToIndex = new Dictionary<string, int>();
        for (int i = 0; i < n; i++)
        {
            nodeToIndex[nodes[i]] = i;
        }
        // Fill in the cost matrix based on edges
        foreach (var edge in problem.edges)
        {
            int i = nodeToIndex[edge.source];
            int j = nodeToIndex[edge.target];
            matrix[i, j] = edge.weight;
            matrix[j, i] = edge.weight; // Undirected graph
        }

        return matrix;
    }
}

