using API.Interfaces;

namespace API.Problems.NPComplete.NPC_TOPOLOGICALSORT.Solvers;

class KahnsAlgorithm : ISolver<TOPOLOGICALSORT> {
        // --- Fields ---
        public string solverName { get; } = "Kahn's Algorithm";
        public string solverDefinition { get; } = "Kahn's algorithm is a BFS-based approach to topological sorting. It repeatedly removes vertices with zero in-degree from the graph, appending them to the output ordering. If all vertices are removed, the ordering is valid; otherwise, the graph contains a cycle and no topological sort exists.";
        public string source { get; } = "Kahn, A. B. (1962). Topological sorting of large networks. Communications of the ACM, 5(11), 558-562.";
        public string sourceLink { get; } = "https://dl.acm.org/doi/10.1145/368996.369025";
        public string[] contributors { get; } = { "Pravesh Aryal", "Koras Koirala", "Dinesh Khanal" };
        public bool timerHasExpired { get; set; }
        // Declared, not derived. Processes the zero-in-degree frontier via a queue, breadth-first.
        public SolverType solverType { get; } = SolverType.BreadthFirstSearch;
        public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
        // No adjacency list is built: for every one of the V dequeued nodes, the inner loop does
        // "foreach (var edge in problem.edges)" — a full linear scan of all E edges to find those
        // leaving `current` — instead of an O(1)-per-neighbor adjacency lookup. That's O(V * E),
        // not the textbook O(V + E).
        public string complexity { get; } = "O(V * E)";

        // --- Constructors ---
        public KahnsAlgorithm() { }

        // --- Methods ---
        public string solve(TOPOLOGICALSORT problem) {
                // Build in-degree map
                Dictionary<string, int> inDegree = new Dictionary<string, int>();
                foreach (var node in problem.nodes)
                        inDegree[node] = 0;

                foreach (var edge in problem.edges) {
                        if (inDegree.ContainsKey(edge.Value))
                                inDegree[edge.Value]++;
                }

                // Enqueue all zero in-degree nodes
                Queue<string> queue = new Queue<string>();
                foreach (var node in problem.nodes) {
                        if (inDegree[node] == 0)
                                queue.Enqueue(node);
                }

                List<string> order = new List<string>();

                while (queue.Count > 0) {
                        string current = queue.Dequeue();
                        order.Add(current);

                        // Reduce in-degree of neighbors
                        foreach (var edge in problem.edges) {
                                if (edge.Key == current) {
                                        inDegree[edge.Value]--;
                                        if (inDegree[edge.Value] == 0)
                                                queue.Enqueue(edge.Value);
                                }
                        }
                }

                // If not all nodes processed, graph has a cycle
                if (order.Count != problem.nodes.Count)
                        return "{}";

                return "{" + string.Join(",", order) + "}";
        }
}
