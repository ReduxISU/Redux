using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_TOPOLOGICALSORT.Solvers;

// The steps Kahn's Algorithm records:
//   - an Update to start: the nodes with no incoming edges, ready to be placed (or none, when every node has one);
//   - an Accept for each node it places, naming the nodes that placing it frees and the nodes still ready;
//   - a Reject when nodes are left that never lose their last incoming edge (a cycle);
//   - the Done, with the order or none.
// Incremental: each step places one more node, and the order placed so far is never undone.
class KahnsAlgorithm : ISolver<TOPOLOGICALSORT, Tour>, IIncrementalSolver {
    // --- Fields ---
    public string solverName { get; } = "Kahn's Algorithm";
    public string solverDefinition { get; } = "Kahn's algorithm is a BFS-based approach to topological sorting. It repeatedly removes vertices with zero in-degree from the graph, appending them to the output ordering. If all vertices are removed, the ordering is valid; otherwise, the graph contains a cycle and no topological sort exists.";
    public string source { get; } = "Kahn, A. B. (1962). Topological sorting of large networks. Communications of the ACM, 5(11), 558-562.";
    public string sourceFile { get; } = SourceFile.Path();
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
    public MathematicalFunction timeComplexity { get; } = new("V * E", "");

    // --- Constructors ---
    public KahnsAlgorithm() { }

    // How the steps show the run: the order placed so far, and the node just placed (null: none).
    private static RunSteps<(List<string> Order, string? Placed), Tour> NewSteps(StepRecorder<Tour> rec) => new(rec,
        // What every picture shows: the order so far, and the node the step placed.
        partial: s => new Tour(s.Order),
        focus: s => s.Placed == null ? [] : [LayeredFrames.NodeId(s.Placed)]);

    // --- Methods ---
    public string solve(TOPOLOGICALSORT problem) => Solve(problem, StepRecorder<Tour>.Off);

    // Kahn's algorithm: a node can go next once every edge into it comes from a node already placed. Keep a queue of such
    // nodes (in-degree 0), place them one at a time, and take away the edges out of each placed node; a node whose last
    // incoming edge goes joins the queue. If the queue runs dry before every node is placed, the rest lie on a cycle.
    public string Solve(TOPOLOGICALSORT problem, StepRecorder<Tour> rec) {
        var steps = NewSteps(rec);
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
        if (queue.Count > 0)
            steps.Update((order, null), $"Ready to place (no incoming edges): {Words.List(queue.ToList())}.");
        else
            steps.Update((order, null), $"No node is free of incoming edges, so the graph has a cycle.");

        while (queue.Count > 0) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer; // stop at the time limit
            string current = queue.Dequeue();
            order.Add(current);

            // Reduce in-degree of neighbors
            var freed = new List<string>();
            foreach (var edge in problem.edges) {
                if (edge.Key == current) {
                    inDegree[edge.Value]--;
                    if (inDegree[edge.Value] == 0) {
                        queue.Enqueue(edge.Value);
                        freed.Add(edge.Value);
                    }
                }
            }
            if (freed.Count > 0 && queue.Count > 0)
                steps.Accept((order, current), $"Place {current} at position {order.Count}. That frees {Words.List(freed)}. Ready: {Words.List(queue.ToList())}.");
            else if (freed.Count > 0)
                steps.Accept((order, current), $"Place {current} at position {order.Count}. That frees {Words.List(freed)}.");
            else if (queue.Count > 0)
                steps.Accept((order, current), $"Place {current} at position {order.Count}. Ready: {Words.List(queue.ToList())}.");
            else
                steps.Accept((order, current), $"Place {current} at position {order.Count}.");
        }

        // If not all nodes processed, graph has a cycle
        if (order.Count != problem.nodes.Count) {
            var stuck = problem.nodes.Where(n => !order.Contains(n)).ToList();
            steps.Reject((order, null), $"{Words.List(stuck)} never {(stuck.Count > 1 ? "lose their" : "loses its")} last incoming edge, because of a cycle.",
                focus: () => stuck.Select(LayeredFrames.NodeId).ToArray());
            steps.Done(new Tour([]), false, $"The graph has a cycle, so no topological order exists.");
            return "()";
        }

        steps.Done(new Tour(order), true, $"Every node is placed and every edge points forward: {string.Join(", ", order)}.");
        return "(" + string.Join(",", order) + ")";
    }
}
