using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Problems.P.P_SSSP.Solvers;
using API.Interfaces.JSON_Objects;
using SPADE;
using System;

namespace API.Problems.P.P_SPSP.Solvers;

// The steps Dijkstra's Algorithm records for one source and one target, for the Layered, list and table pictures (ids from
// LayeredFrames):
//   - an Update that sets the starting distances: 0 for the source, ∞ for every other node;
//   - an Accept each time a node is settled, after its edges are relaxed: its distance is final, and the why lists the
//     shorter routes the relaxations found (focus: the node and the edges it relaxed). The target is settled without
//     relaxing, and the search stops there;
//   - the Done, with the shortest path, or saying the target can't be reached.
// Each step's partial is the search after that moment. The list and table pictures rebuild their frames from these steps
// (PathSteps, TableSteps), so the visualize endpoint runs Dijkstra once. Incremental: each step settles one more node for
// good, until the target is settled.
class SPSPSolver : ISolver<SPSP, Distances>, IIncrementalSolver {
    // ----- Fields ----- //
    public string solverName { get; } = "Dijkstra's Algorithm";
    public string solverDefinition { get; } = "";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Rajit Nilkar", "Scott Barfuss" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Dijkstra's algorithm always finalizes the nearest unvisited
    // node next -- an irrevocable, locally-optimal choice at each step.
    public SolverType solverType { get; } = SolverType.Greedy;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
    // Uses .NET's built-in PriorityQueue<T> (real binary heap, O(log n) enqueue/dequeue) with
    // lazy re-insertion on relaxation and a `visited` check to skip stale entries — the
    // standard binary-heap Dijkstra bound.
    public MathematicalFunction timeComplexity { get; } = new("(V + E) log V", "");
    PriorityQueue<string, int>? pq;

    // How the steps show the search: every node's distance and predecessor as they are now, and which nodes are settled. A
    // step is about one node, and the neighbours whose edges from it were just relaxed.
    private static RunSteps<(string Node, List<string> Relaxed), Distances> NewSteps(StepRecorder<Distances> rec, bool directed,
        Dictionary<string, int> dist, Dictionary<string, string?> prev, HashSet<string> visited) => new(rec,
        // What every picture shows: the distances, predecessors and settled nodes so far.
        partial: _ => Distances.Of(dist, prev, visited),
        // The node, and the edges it relaxed.
        focus: s => [LayeredFrames.NodeId(s.Node), .. s.Relaxed.Select(next => LayeredFrames.EdgeId(directed, s.Node, next)).Distinct()]);

    public string solve(SPSP problem) => Solve(problem, StepRecorder<Distances>.Off);

    // Dijkstra, stopped at the target: take the unsettled node nearest the source off the queue, settle it (its distance is
    // now final), and relax each of its edges: when going through it is shorter than a neighbour's distance so far, the
    // neighbour takes that distance and this node as its predecessor. Once the target is settled its distance is final, so
    // the search stops. A node already settled can come off the queue again (a stale entry): it is skipped.
    public string Solve(SPSP problem, StepRecorder<Distances> rec) {
        SolveProbe.Hit(GetType());
        UtilCollectionGraph graph = problem.graph;
        List<string> nodes = graph.Nodes.ToList().Select(n => n.ToString()).ToList();
        string sourceNode = problem.sourceNode;
        string targetNode = problem.targetNode;

        var dist = nodes.ToDictionary(n => n, _ => int.MaxValue);
        var prev = nodes.ToDictionary(n => n, _ => (string?)null);
        var visited = new HashSet<string>();
        var steps = NewSteps(rec, problem.isDirected, dist, prev, visited);

        if (nodes.Count == 0) {
            steps.Done(new Distances([], [], []), false, $"The graph has no nodes, so there is no path.");
            return "{}"; // No nodes, return empty path
        }

        if (!nodes.Contains(sourceNode) || !nodes.Contains(targetNode)) {
            steps.Done(new Distances([], [], []), false, $"The source or the target is not a node of the graph.");
            return "{}";
        }

        var adjacency = BuildAdjacencyList(graph);
        pq = new PriorityQueue<string, int>();
        // What the relaxations of the node being settled did, for its step: the neighbours whose edges were relaxed, and
        // each shorter route found (the neighbour, its old distance and its new one). Reused for every node.
        var relaxed = new List<string>();
        var shorter = new List<(string Node, int Old, int New)>();

        dist[sourceNode] = 0;
        pq.Enqueue(sourceNode, 0);
        steps.Update((sourceNode, relaxed), $"Start at {sourceNode} with distance 0. Every other node starts at ∞.");

        while (pq.Count > 0) {
            // The run is stopped; SolveRun.Record adds the stopped Done step when recording.
            if (timerHasExpired)
                return SolveRun.TimedOutAnswer;

            string current = pq.Dequeue();

            if (visited.Contains(current))
                continue; // Skip if we've already visited this node
            visited.Add(current);

            relaxed.Clear();
            shorter.Clear();
            if (current == targetNode) {
                steps.Accept((current, relaxed), $"Settle {current} at distance {LayeredFrames.Distance(dist[current])}. It is the target, so the search stops.");
                break; // Stop if we've reached the target node
            }

            foreach (var (next, weight) in adjacency.TryGetValue(current, out var neighbors) ? neighbors : []) {
                if (visited.Contains(next))
                    continue; // Skip visited neighbors

                if (dist[current] == int.MaxValue)
                    continue; // Skip if current node is unreachable

                relaxed.Add(next);
                int candidate = dist[current] + weight;
                if (candidate < dist[next]) {
                    shorter.Add((next, dist[next], candidate));
                    dist[next] = candidate;
                    prev[next] = current;
                    pq.Enqueue(next, candidate);
                }
            }
            if (shorter.Count == 0)
                steps.Accept((current, relaxed), $"Settle {current} at distance {LayeredFrames.Distance(dist[current])}. No shorter routes from here.");
            else
                steps.Accept((current, relaxed), $"Settle {current} at distance {LayeredFrames.Distance(dist[current])}. Shorter routes found: {string.Join(", ", shorter.Select(r => $"{r.Node}: {LayeredFrames.Distance(r.Old)} → {r.New}"))}.");
        }

        if (dist[targetNode] == int.MaxValue) {
            steps.Done(Distances.Of(dist, prev, visited), false, $"{targetNode} can't be reached from {sourceNode}, so there is no path.");
            return "{}"; // No path found
        }
        List<string> path = ReconstructPath(prev, sourceNode, targetNode);
        steps.Done(Distances.Of(dist, prev, visited), true, $"The shortest path is {string.Join(" → ", path)}, with total {dist[targetNode]}.");
        return NodeListToCertificate(path);
    }

    //Helper method to build an adjacent list from the graph
    internal static Dictionary<string, List<(string neighbor, int weight)>> BuildAdjacencyList(UtilCollectionGraph graph) {
        var adjacency = new Dictionary<string, List<(string neighbor, int weight)>>();

        if (graph.Nodes.Count() == 0)
            return adjacency; // No edges, so return empty adjacency list

        foreach (UtilCollection rawEdge in graph.Edges.ToList()) {
            // check if edge is weighted. An unordered rawEdge (a plain set like {1,2}) is a
            // genuine SPADE set and does not support indexing; it can never be the weighted
            // (endpoints, weight) shape, which is always ordered, so it always takes the
            // unweighted path below.
            bool isWeighted = rawEdge.IsOrdered() && rawEdge.Count() == 2
                && LooksLikeCollection(rawEdge[0])
                && !LooksLikeCollection(rawEdge[1]);

            if (isWeighted) {
                UtilCollection endpoints = rawEdge[0];
                int w = int.Parse(rawEdge[1].ToString()); // Assuming the weight is an integer

                if (endpoints.IsOrdered())
                    AddDirected(adjacency, endpoints[0].ToString(), endpoints[1].ToString(), w);
                else {
                    var cast = endpoints.ToList();
                    if (cast.Count == 1) // Add directed edge from the node to itself
                    {
                        string v = cast[0].ToString();
                        AddDirected(adjacency, v, v, w);
                    } else // Undirected edge, add in both directions
                      {
                        string a = cast[0].ToString();
                        string b = cast[1].ToString();
                        AddDirected(adjacency, a, b, w);
                        AddDirected(adjacency, b, a, w);
                    }
                }
            } else {
                int w = 1;

                if (rawEdge.IsOrdered())
                    AddDirected(adjacency, rawEdge[0].ToString(), rawEdge[1].ToString(), w);
                else {
                    // If it's an unordered pair, treat it as an undirected edge
                    var cast = rawEdge.ToList();
                    if (cast.Count() == 1) {
                        string v = cast[0].ToString();
                        AddDirected(adjacency, v, v, w);
                    } else {
                        string a = cast[0].ToString();
                        string b = cast[1].ToString();
                        AddDirected(adjacency, a, b, w);
                        AddDirected(adjacency, b, a, w);
                    }
                }
            }
        }
        return adjacency;
    }

    // AddDirected: Adds a directed edge to the adjacency list
    private static void AddDirected(Dictionary<string, List<(string neighbor, int weight)>> adjacency, string from, string to, int weight) {
        if (!adjacency.TryGetValue(from, out var list)) {
            list = new List<(string neighbor, int weight)>();
            adjacency[from] = list;
        }
        list.Add((to, weight));

        if (!adjacency.ContainsKey(to))
            adjacency[to] = new List<(string, int)>();
    }

    // ReconstructPath: Reconstructs the path from source to target using the previous dictionary
    internal static List<string> ReconstructPath(Dictionary<string, string?> prev, string source, string target) {
        var path = new List<string>();
        string? current = target;

        while (current != null) {
            path.Add(current);
            if (current == source) {
                break; // Stop if we've reached the source node
            }
            current = prev[current];
        }

        path.Reverse();

        if (path.Count() == 0 || path[0] != source) {
            return new List<string>(); // No valid path
        }
        return path;
    }

    // NodeListCertificate: Converts a list of nodes into the certificate format
    internal static string NodeListToCertificate(List<string> nodes) {
        if (nodes == null || nodes.Count() == 0)
            return "{}"; // No path found, empty certificate
        return "{" + string.Join(",", nodes) + "}";
    }

    // LooksLikeCollection: Helper method to determine if a UtilCollection looks like a collection
    private static bool LooksLikeCollection(UtilCollection u) {
        string s = u.ToString().TrimStart();
        return s.StartsWith("{") || s.StartsWith("(");
    }

    // LegacyPathSteps: the list picture's steps (the path to each settled node, in the certificate format) as this
    // solver made them in a second run, before the pictures read the typed run. The picture now builds them from the
    // typed run (PathSteps); this is kept as the reference the tests compare PathSteps against.
    internal List<Object> LegacyPathSteps(SPSP problem) {
        SolveProbe.Hit(GetType());
        var steps = new List<Object>();
        UtilCollectionGraph graph = problem.graph;

        List<string> nodes = graph.Nodes.ToList().Select(n => n.ToString()).ToList();
        if (nodes.Count == 0)
            return steps; // return empty list of steps

        string sourceNode = problem.sourceNode;
        string targetNode = problem.targetNode;

        var adjacency = BuildAdjacencyList(graph);

        var dist = nodes.ToDictionary(n => n, _ => int.MaxValue);
        var prev = nodes.ToDictionary(n => n, _ => (string?)null);
        var visited = new HashSet<string>();
        pq = new PriorityQueue<string, int>();

        dist[sourceNode] = 0;
        pq.Enqueue(sourceNode, 0);

        while (pq.Count > 0) {
            if (timerHasExpired)
                return steps;

            string current = pq.Dequeue();
            if (visited.Contains(current))
                continue;

            visited.Add(current);

            var path = ReconstructPath(prev, sourceNode, current);
            if (path.Count > 0)
                steps.Add(NodeListToCertificate(path));

            // if we have successfully reached the target node
            if (current == targetNode)
                break;

            if (!adjacency.TryGetValue(current, out var neighbors))
                continue;

            foreach (var (next, weight) in neighbors) {
                if (visited.Contains(next))
                    continue;

                if (weight < 0)
                    return steps;

                if (dist[current] == int.MaxValue)
                    continue;

                int candidate = dist[current] + weight;
                if (candidate < dist[next]) {
                    dist[next] = candidate;
                    prev[next] = current;
                    pq.Enqueue(next, candidate);
                }
            }
        }
        return steps;
    }

    public class SPSPTableStepVertex {
        public string name { get; set; } = "";
        public bool known { get; set; }
        public string cost { get; set; } = "\u221E"; // Infinity symbol
        public string? path { get; set; }
    }

    public class SPSPTableStep : API_JSON {
        public List<SPSPTableStepVertex> vertices { get; set; } = new();
        public string? currentVertex { get; set; }
        public List<string> knownSet { get; set; } = new();
        public string? sourceVertex { get; set; }
        public string? targetVertex { get; set; }
    }

    // GetTableSteps: the table picture's steps from a separate run. The picture now builds them from the typed run
    // (TableSteps); this is kept as the reference the tests compare TableSteps against.
    public List<Object> GetTableSteps(SPSP problem) {
        SolveProbe.Hit(GetType());
        var steps = new List<Object>();
        UtilCollectionGraph graph = problem.graph;

        List<string> nodes = graph.Nodes.ToList()
            .Select(n => n.ToString())
            .OrderBy(n => n)
            .ToList();

        if (nodes.Count == 0)
            return steps; // return empty list of steps

        string sourceNode = problem.sourceNode;
        string targetNode = problem.targetNode;

        var adjacency = BuildAdjacencyList(graph);
        var dist = nodes.ToDictionary(n => n, _ => int.MaxValue);
        var prev = nodes.ToDictionary(n => n, _ => (string?)null);
        var visited = new HashSet<string>();
        var pq = new PriorityQueue<string, int>();

        dist[sourceNode] = 0;
        pq.Enqueue(sourceNode, 0);

        steps.Add(BuildTableStep(nodes, dist, prev, visited, currentVertex: null, sourceVertex: sourceNode, targetVertex: targetNode));

        while (pq.Count > 0) {
            if (timerHasExpired)
                return steps;

            string current = pq.Dequeue();

            if (visited.Contains(current))
                continue;

            visited.Add(current);

            // Record the step after visiting a node before relaxing its neighbors
            steps.Add(BuildTableStep(nodes, dist, prev, visited, currentVertex: null, sourceVertex: sourceNode, targetVertex: targetNode));

            if (current == targetNode)
                break; // Stop if we've reached the target node

            if (!adjacency.TryGetValue(current, out var neighbors))
                continue; // No neighbors, so skip

            foreach (var (next, weight) in neighbors) {
                if (visited.Contains(next))
                    continue; // Skip visited neighbors

                if (dist[current] == int.MaxValue)
                    continue; // Skip if current node is unreachable

                int candidate = dist[current] + weight;
                if (candidate < dist[next]) {
                    dist[next] = candidate;
                    prev[next] = current;
                    pq.Enqueue(next, candidate);
                }
            }
            steps.Add(BuildTableStep(nodes, dist, prev, visited, currentVertex: current, sourceVertex: sourceNode, targetVertex: targetNode));
        }
        return steps;
    }

    private static SPSPTableStep BuildTableStep(
        List<string> nodes,
        Dictionary<string, int> dist,
        Dictionary<string, string?> prev,
        HashSet<string> visited,
        string? currentVertex,
        string? sourceVertex,
        string? targetVertex) {
        return new SPSPTableStep {
            currentVertex = currentVertex,
            sourceVertex = sourceVertex,
            targetVertex = targetVertex,
            knownSet = visited.ToList(),
            vertices = nodes.Select(n => new SPSPTableStepVertex {
                name = n,
                known = visited.Contains(n),
                cost = dist[n] == int.MaxValue ? "\u221E" : dist[n].ToString(),
                path = dist[n] == int.MaxValue ? null : NodeListToCertificate(ReconstructPath(prev, sourceVertex!, n))
            }).ToList()
        };
    }

    // ----- Frames from the typed run ----- //

    // The search as dictionaries over every node: distance (int.MaxValue for ∞) and predecessor (null for none).
    private static Dictionary<string, int> DistOf(List<string> nodes, Distances d) =>
        nodes.ToDictionary(n => n, n => d.Dist.TryGetValue(n, out int v) ? v : int.MaxValue);
    private static Dictionary<string, string?> PrevOf(List<string> nodes, Distances d) =>
        nodes.ToDictionary(n => n, n => d.Pred.TryGetValue(n, out string? p) ? p : null);

    /// <summary>
    /// The list picture's steps (the path to each settled node, in the certificate format), made from the typed run.
    /// The same as <see cref="LegacyPathSteps"/>, which ran Dijkstra a second time; like it, they end at the target.
    /// (Its stop at a negative edge is not repeated: the instance parser rejects negative weights.)
    /// </summary>
    internal static List<string> PathSteps(SPSP problem, IEnumerable<object> steps) {
        var nodes = problem.graph.Nodes.ToList().Select(n => n.ToString()).ToList();
        var result = new List<string>();
        foreach (var (node, before, _) in SSSPSolver.Settles(steps)) {
            var path = ReconstructPath(PrevOf(nodes, before), problem.sourceNode, node);
            if (path.Count > 0)
                result.Add(NodeListToCertificate(path));
            if (node == problem.targetNode)
                break;
        }
        return result;
    }

    // The table lists the nodes by name.
    private static List<string> TableNodesOf(SPSP problem) =>
        problem.graph.Nodes.ToList().Select(n => n.ToString()).OrderBy(n => n).ToList();

    /// <summary>The table before the search starts, or null for a graph with no nodes. Needs no run.</summary>
    internal static SPSPTableStep? InitialTableStep(SPSP problem) {
        var nodes = TableNodesOf(problem);
        if (nodes.Count == 0)
            return null;
        var dist = nodes.ToDictionary(n => n, _ => int.MaxValue);
        dist[problem.sourceNode] = 0;
        var prev = nodes.ToDictionary(n => n, _ => (string?)null);
        return BuildTableStep(nodes, dist, prev, new HashSet<string>(), currentVertex: null, sourceVertex: problem.sourceNode, targetVertex: problem.targetNode);
    }

    /// <summary>
    /// The table picture's steps, made from the typed run: the start, then for each settled node the table once it is
    /// known and (except for the target) again after its edges are relaxed. The same as <see cref="GetTableSteps"/>,
    /// which ran Dijkstra again.
    /// </summary>
    internal static List<SPSPTableStep> TableSteps(SPSP problem, IEnumerable<object> steps) {
        var result = new List<SPSPTableStep>();
        var nodes = TableNodesOf(problem);
        var first = steps.OfType<SolverStep<Distances>>().FirstOrDefault(s => s.Event == StepEvent.Try);
        if (nodes.Count == 0 || first == null)
            return result;
        string source = problem.sourceNode, target = problem.targetNode;
        var adjacency = BuildAdjacencyList(problem.graph);
        var visited = new HashSet<string>();
        result.Add(BuildTableStep(nodes, DistOf(nodes, first.Partial), PrevOf(nodes, first.Partial), visited, currentVertex: null, sourceVertex: source, targetVertex: target));
        foreach (var (node, before, after) in SSSPSolver.Settles(steps)) {
            visited.Add(node);
            result.Add(BuildTableStep(nodes, DistOf(nodes, before), PrevOf(nodes, before), visited, currentVertex: null, sourceVertex: source, targetVertex: target));
            if (node == target)
                break;
            // A node with no edges at all has nothing to relax, and gets no second table.
            if (!adjacency.ContainsKey(node))
                continue;
            result.Add(BuildTableStep(nodes, DistOf(nodes, after), PrevOf(nodes, after), visited, currentVertex: node, sourceVertex: source, targetVertex: target));
        }
        return result;
    }
}
