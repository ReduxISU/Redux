using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Interfaces.JSON_Objects;
using SPADE;
using System;
using System.Collections.Generic;

namespace API.Problems.P.P_SSSP.Solvers;

class SSSPSolver : ISolver<SSSP, Distances> {
    public string solverName { get; } = "Dijkstra's Algorithm";
    public string solverDefinition { get; } = "";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Rajit Nilkar" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Dijkstra's algorithm always finalizes the nearest unvisited
    // node next -- an irrevocable, locally-optimal choice at each step.
    public SolverType solverType { get; } = SolverType.Greedy;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
    // Uses .NET's built-in PriorityQueue<T> (real binary heap, O(log n) enqueue/dequeue) with
    // lazy re-insertion on relaxation and a `visited` check to skip stale entries — the
    // standard binary-heap Dijkstra bound.
    public string complexity { get; } = "O((V + E) log V)";
    PriorityQueue<string, int>? pq;

    public string solve(SSSP problem) => Solve(problem, StepRecorder<Distances>.Off);

    // Steps: a Try for the start, then an Accept each time a node is settled (after its edges are relaxed), then Done.
    // Each step's Partial is the search after that moment. The list and table pictures rebuild their frames from these
    // steps (GraphSteps, TableSteps), so the visualize endpoint runs Dijkstra once.
    public string Solve(SSSP problem, StepRecorder<Distances> rec) {
        SolveProbe.Hit(GetType());
        UtilCollectionGraph graph = problem.graph;
        List<string> nodes = graph.Nodes.ToList().Select(n => n.ToString()).ToList();

        if (nodes.Count == 0) {
            rec.Done(new Distances([], [], []), true, "The graph has no nodes, so there is nothing to reach.");
            return "{}"; // No nodes, return empty path
        }

        string sourceNode = problem.sourceNode;

        if (!nodes.Contains(sourceNode)) {
            rec.Done(new Distances([], [], []), false, $"The source {sourceNode} is not a node of the graph.");
            return "{}";
        }

        var adjacency = BuildAdjacencyList(graph);

        var dist = nodes.ToDictionary(n => n, _ => int.MaxValue);
        var prev = nodes.ToDictionary(n => n, _ => (string?)null);
        var visited = new HashSet<string>();
        pq = new PriorityQueue<string, int>();
        Distances Partial() => Dijkstra.Partial(dist, prev, visited);

        dist[sourceNode] = 0;
        pq.Enqueue(sourceNode, 0);
        rec.Try(Partial, () => $"Start at {sourceNode} with distance 0. Every other node starts at ∞.", sourceNode);

        while (pq.Count > 0) {
            if (timerHasExpired) {
                rec.Done(new Distances([], [], []), false, "The solver ran out of time.");
                return "{}";
            }

            string current = pq.Dequeue();

            if (visited.Contains(current))
                continue;
            visited.Add(current);

            var shorter = new List<string>();
            foreach (var (next, weight) in adjacency.TryGetValue(current, out var neighbors) ? neighbors : []) {
                if (visited.Contains(next))
                    continue;

                if (dist[current] == int.MaxValue)
                    continue;

                int candidate = dist[current] + weight;
                if (candidate < dist[next]) {
                    shorter.Add($"{next}: {Dijkstra.Show(dist[next])} → {candidate}");
                    dist[next] = candidate;
                    prev[next] = current;
                    pq.Enqueue(next, candidate);
                }
            }
            rec.Accept(Partial, () => Dijkstra.SettleCaption(current, dist[current], shorter), current);
        }

        var lost = nodes.Where(n => dist[n] == int.MaxValue).ToList();
        rec.Done(Partial(), true, $"Every node that can be reached is settled; the shortest paths from {sourceNode} form a tree."
            + (lost.Count > 0 ? $" {string.Join(", ", lost)} can't be reached." : ""));
        return NodeListCertificate(nodes, sourceNode, prev);
    }

    internal static Dictionary<string, List<(string neighbor, int weight)>> BuildAdjacencyList(UtilCollectionGraph graph) {
        var adjacency = new Dictionary<string, List<(string neigbor, int weight)>>();

        if (graph.Nodes.Count() == 0)
            return adjacency; // No edges, so return empty adjacency list

        foreach (UtilCollection rawEdge in graph.Edges.ToList()) {
            // check if edge is weighted. An unweighted/undirected raw edge is itself an
            // unordered node-set (e.g. {1,2}), which cannot be indexed by position -- only
            // ordered tuples/lists can be. Guard with IsOrdered() before probing
            // rawEdge[0]/[1] so such edges fall through to the unweighted branch below,
            // which already handles unordered collections correctly.
            bool isWeighted = rawEdge.IsOrdered() && rawEdge.Count() == 2
                && LooksLikeCollection(rawEdge[0]) && !LooksLikeCollection(rawEdge[1]);

            if (isWeighted) {
                UtilCollection endpoints = rawEdge[0];
                int w = int.Parse(rawEdge[1].ToString());

                if (endpoints.IsOrdered())
                    AddDirected(adjacency, endpoints[0].ToString(), endpoints[1].ToString(), w);
                else {
                    var cast = endpoints.ToList();
                    if (cast.Count == 1) {
                        string v = cast[0].ToString();
                        AddDirected(adjacency, v, v, w);
                    } else {
                        string a = cast[0].ToString();
                        string b = cast[1].ToString();
                        AddDirected(adjacency, a, b, w);
                        AddDirected(adjacency, b, a, w);
                    }
                }
            } else {
                int w = 1; // Assign the weight 1 by default if there is no assigned weight

                if (rawEdge.IsOrdered())
                    AddDirected(adjacency, rawEdge[0].ToString(), rawEdge[1].ToString(), w);
                else {
                    var cast = rawEdge.ToList();
                    if (cast.Count == 1) {
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

    // ReconstructPath: Reconstructs the path from source to each node using the previous dictionionary
    internal static List<string> ReconstructPath(Dictionary<string, string?> prev, string? source, string target) {
        var path = new List<string>();
        string? current = target;

        while (current != null) {
            path.Add(current);
            if (current == source) {
                break; // Stop if we've reached the source node
            }
            current = prev[current];
        }

        path.Reverse(); // Reverse the list so path order is source to target

        if (path.Count == 0 || path[0] != source) {
            return new List<string>(); // No valid path
        }
        return path;
    }

    // NodeListCertificate: Converts a set of (node, path) tuples for every node in the graph
    internal static string NodeListCertificate(List<string> nodes, string sourceNode, Dictionary<string, string?> prev) {
        var entries = new List<string>();

        foreach (string node in nodes) {
            List<string> path = ReconstructPath(prev, sourceNode, node);
            string pathCertificate = NodeListToCertificate(path);
            entries.Add($"({node},{pathCertificate})");
        }

        return "{" + string.Join(",", entries) + "}";
    }

    // NodeListToCertificate: Converts a list of nodes into the certificate form
    internal static string NodeListToCertificate(List<string> nodes) {
        if (nodes == null || nodes.Count == 0)
            return "{}";
        return "{" + string.Join(",", nodes) + "}";
    }

    // LooksLikeCollection: Helper method to determine if a UtilCollection looks like a collection
    private static bool LooksLikeCollection(UtilCollection u) {
        string s = u.ToString().TrimStart();
        return s.StartsWith("{") || s.StartsWith("(");
    }

    public class SSSPGraphStep {
        public string? currentNode { get; set; } // node just settled in this step
        public string? currentEdgeFrom { get; set; } // winning edge that determines the shortest path for node
        public List<string> knownNodes { get; set; } = new(); // all settled nodes so far
        public List<(string from, string to)> treeEdges { get; set; } = new(); // accepted tree edges so far
    }

    // LegacyGraphSteps: the list picture's steps as this solver made them in a second run before #734. The picture now
    // builds them from the typed run (GraphSteps); this is kept as the reference the tests compare GraphSteps against.
    internal List<Object> LegacyGraphSteps(SSSP problem) {
        SolveProbe.Hit(GetType());
        var steps = new List<Object>();
        UtilCollectionGraph graph = problem.graph;

        List<string> nodes = graph.Nodes.ToList().Select(n => n.ToString()).ToList();

        if (nodes.Count == 0)
            return steps;

        string sourceNode = problem.sourceNode;

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

            steps.Add(BuildGraphStep(current, prev, visited));

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

    // BuildGraphStep: assembles one snapshot of the shortest path tree's state
    private static SSSPGraphStep BuildGraphStep(string current, Dictionary<string, string?> prev, HashSet<string> visited) {
        var treeEdges = new List<(string from, string to)>();
        foreach (var predecessor in prev) {
            if (predecessor.Value != null)
                treeEdges.Add((predecessor.Value, predecessor.Key));
        }

        return new SSSPGraphStep {
            currentNode = current,
            currentEdgeFrom = prev[current],
            knownNodes = visited.ToList(),
            treeEdges = treeEdges
        };
    }

    public class SSSPTableStepVertex {
        public string name { get; set; } = "";
        public string display { get; set; } = "\u221E";
        public string? path { get; set; }
        public bool known { get; set; }
        public string order { get; set; } = "";
    }

    public class SSSPTableStep : API_JSON {
        public List<SSSPTableStepVertex> vertices { get; set; } = new();
        public List<string> knownSet { get; set; } = new();
        public string? currentVertex { get; set; }
        public string? sourceVertex { get; set; }
    }

    // GetTableSteps: the table picture's steps from a separate run. The picture now builds them from the typed run
    // (TableSteps); this is kept as the reference the tests compare TableSteps against.
    public List<Object> GetTableSteps(SSSP problem) {
        SolveProbe.Hit(GetType());
        var steps = new List<Object>();
        UtilCollectionGraph graph = problem.graph;

        List<string> nodes = graph.Nodes.ToList().Select(n => n.ToString()).ToList();

        if (nodes.Count == 0)
            return steps;

        string sourceNode = problem.sourceNode;

        var adjacency = BuildAdjacencyList(graph);
        var dist = nodes.ToDictionary(n => n, _ => int.MaxValue);
        var prev = nodes.ToDictionary(n => n, _ => (string?)null);
        var visited = new HashSet<string>();
        var finalizationOrder = new Dictionary<string, int>();
        int orderCounter = 1;
        pq = new PriorityQueue<string, int>();

        dist[sourceNode] = 0;
        pq.Enqueue(sourceNode, 0);

        steps.Add(BuildTableStep(nodes, dist, prev, visited, finalizationOrder, currentVertex: null, sourceVertex: sourceNode));

        while (pq.Count > 0) {
            if (timerHasExpired)
                return steps;

            string current = pq.Dequeue();

            if (visited.Contains(current))
                continue;

            visited.Add(current);
            finalizationOrder[current] = orderCounter++;

            steps.Add(BuildTableStep(nodes, dist, prev, visited, finalizationOrder, currentVertex: null, sourceVertex: sourceNode));

            if (!adjacency.TryGetValue(current, out var neighbors))
                continue;

            foreach (var (next, weight) in neighbors) {
                if (visited.Contains(next))
                    continue;

                if (dist[current] == int.MaxValue)
                    continue;

                int candidate = dist[current] + weight;
                if (candidate < dist[next]) {
                    dist[next] = candidate;
                    prev[next] = current;
                    pq.Enqueue(next, candidate);
                }
            }
            bool isLastVertex = pq.Count == 0 || pq.UnorderedItems.All(item => visited.Contains(item.Element));

            steps.Add(BuildTableStep(nodes, dist, prev, visited, finalizationOrder, currentVertex: isLastVertex ? null : current, sourceVertex: sourceNode));
        }
        return steps;
    }

    private static SSSPTableStep BuildTableStep(
        List<string> nodes,
        Dictionary<string, int> dist,
        Dictionary<string, string?> prev,
        HashSet<string> visited,
        Dictionary<string, int> finalizationOrder,
        string? currentVertex,
        string? sourceVertex) {
        return new SSSPTableStep {
            currentVertex = currentVertex,
            sourceVertex = sourceVertex,
            knownSet = visited.ToList(),
            vertices = nodes.Select(n => new SSSPTableStepVertex {
                name = n,
                display = dist[n] == int.MaxValue
                    ? "\u221E"
                    : (prev[n] != null ? $"{dist[n]},{prev[n]}" : $"{dist[n]}"),
                path = dist[n] == int.MaxValue
                    ? null
                    : NodeListToCertificate(ReconstructPath(prev, sourceVertex, n)),
                known = visited.Contains(n),
                order = finalizationOrder.ContainsKey(n) ? finalizationOrder[n].ToString() : ""
            }).ToList()
        };
    }

    // ----- Frames from the typed run ----- //

    private static List<string> NodesOf(SSSP problem) => problem.graph.Nodes.ToList().Select(n => n.ToString()).ToList();

    // The search as dictionaries over every node: distance (int.MaxValue for ∞) and predecessor (null for none).
    private static Dictionary<string, int> DistOf(List<string> nodes, Distances d) =>
        nodes.ToDictionary(n => n, n => d.Dist.TryGetValue(n, out int v) ? v : int.MaxValue);
    private static Dictionary<string, string?> PrevOf(List<string> nodes, Distances d) =>
        nodes.ToDictionary(n => n, n => d.Pred.TryGetValue(n, out string? p) ? p : null);

    /// <summary>
    /// The run's settles: for each node settled, in order, the search just before it was settled and just after its
    /// edges were relaxed. Read from the typed steps (the start Try, then one Accept per settled node), so steps the
    /// recorder did not keep are not there; an Accept that does not settle exactly one more node (one kept out of order
    /// past the step cap) is skipped. Shared with SPSP, whose Dijkstra records the same steps.
    /// </summary>
    internal static List<(string Node, Distances Before, Distances After)> Settles(IEnumerable<object> steps) {
        var result = new List<(string, Distances, Distances)>();
        Distances? before = null;
        var settled = new HashSet<string>();
        foreach (var step in steps.OfType<SolverStep<Distances>>()) {
            if (step.Event == StepEvent.Try && before == null) {
                before = step.Partial;
            } else if (step.Event == StepEvent.Accept && before != null) {
                if (step.Partial.Settled.Length != settled.Count + 1) continue;
                string? node = step.Partial.Settled.FirstOrDefault(n => !settled.Contains(n));
                if (node == null) continue;
                settled.Add(node);
                result.Add((node, before, step.Partial));
                before = step.Partial;
            }
        }
        return result;
    }

    /// <summary>
    /// The list picture's steps (one per settled node), made from the typed run. The same as
    /// <see cref="LegacyGraphSteps"/>, which ran Dijkstra a second time. (Its stop at a negative edge is not repeated:
    /// the instance parser rejects negative weights.)
    /// </summary>
    internal static List<SSSPGraphStep> GraphSteps(SSSP problem, IEnumerable<object> steps) {
        var nodes = NodesOf(problem);
        var result = new List<SSSPGraphStep>();
        var known = new List<string>();
        foreach (var (node, before, _) in Settles(steps)) {
            known.Add(node);
            var prev = PrevOf(nodes, before);
            result.Add(new SSSPGraphStep {
                currentNode = node,
                currentEdgeFrom = prev.GetValueOrDefault(node),
                knownNodes = known.ToList(),
                treeEdges = prev.Where(p => p.Value != null).Select(p => (p.Value!, p.Key)).ToList()
            });
        }
        return result;
    }

    /// <summary>The table before the search starts, or null for a graph with no nodes. Needs no run.</summary>
    internal static SSSPTableStep? InitialTableStep(SSSP problem) {
        var nodes = NodesOf(problem);
        if (nodes.Count == 0)
            return null;
        var dist = nodes.ToDictionary(n => n, _ => int.MaxValue);
        dist[problem.sourceNode] = 0;
        var prev = nodes.ToDictionary(n => n, _ => (string?)null);
        return BuildTableStep(nodes, dist, prev, new HashSet<string>(), new Dictionary<string, int>(), currentVertex: null, sourceVertex: problem.sourceNode);
    }

    /// <summary>
    /// The table picture's steps, made from the typed run: the start, then for each settled node the table once it is
    /// known and again after its edges are relaxed. The same as <see cref="GetTableSteps"/>, which ran Dijkstra again.
    /// </summary>
    internal static List<SSSPTableStep> TableSteps(SSSP problem, IEnumerable<object> steps) {
        var result = new List<SSSPTableStep>();
        var nodes = NodesOf(problem);
        var first = steps.OfType<SolverStep<Distances>>().FirstOrDefault(s => s.Event == StepEvent.Try);
        if (nodes.Count == 0 || first == null)
            return result;
        string source = problem.sourceNode;
        var adjacency = BuildAdjacencyList(problem.graph);
        var visited = new HashSet<string>();
        var order = new Dictionary<string, int>();
        result.Add(BuildTableStep(nodes, DistOf(nodes, first.Partial), PrevOf(nodes, first.Partial), visited, order, currentVertex: null, sourceVertex: source));
        foreach (var (node, before, after) in Settles(steps)) {
            visited.Add(node);
            order[node] = order.Count + 1;
            result.Add(BuildTableStep(nodes, DistOf(nodes, before), PrevOf(nodes, before), visited, order, currentVertex: null, sourceVertex: source));
            // A node with no edges at all has nothing to relax, and gets no second table.
            if (!adjacency.ContainsKey(node))
                continue;
            // After the last node nothing reached is left unsettled, and no row is highlighted.
            bool isLastVertex = !nodes.Any(n => after.Dist.ContainsKey(n) && !visited.Contains(n));
            result.Add(BuildTableStep(nodes, DistOf(nodes, after), PrevOf(nodes, after), visited, order, currentVertex: isLastVertex ? null : node, sourceVertex: source));
        }
        return result;
    }
}
