using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.JSON_Objects;
using SPADE;
using System;
using System.Collections.Generic;

namespace API.Problems.P.P_SSSP.Solvers;

class SSSPSolver : ISolver<SSSP>
{
    public string solverName { get; } = "Dijkstra's Algorithm";
    public string solverDefinition { get; } = "";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Rajit Nilkar" };
    public bool timerHasExpired { get; set; }
    PriorityQueue<string, int>? pq;

    public string solve(SSSP problem)
    {
        UtilCollectionGraph graph = problem.graph;
        List<string> nodes = graph.Nodes.ToList().Select(n => n.ToString()).ToList();

        if (nodes.Count == 0)
            return "{}"; // No nodes, return empty path

        string sourceNode = problem.sourceNode;

        if (!nodes.Contains(sourceNode))
            return "{}";

        var adjacency = BuildAdjacencyList(graph);

        var dist = nodes.ToDictionary(n => n, _ => int.MaxValue);
        var prev = nodes.ToDictionary(n => n, _ => (string?)null);
        var visited = new HashSet<string>();
        pq = new PriorityQueue<string, int>();

        dist[sourceNode] = 0;
        pq.Enqueue(sourceNode, 0);

        while(pq.Count > 0)
        {
            if (timerHasExpired)
                return "{}";

            string current = pq.Dequeue();

            if (visited.Contains(current))
                continue;
            visited.Add(current);

            if (!adjacency.TryGetValue(current, out var neighbors))
                continue;

            foreach(var (next, weight) in neighbors)
            {
                if (visited.Contains(next))
                    continue;

                if (dist[current] == int.MaxValue)
                    continue;

                int candidate = dist[current] + weight;
                if(candidate < dist[next])
                {
                    dist[next] = candidate;
                    prev[next] = current;
                    pq.Enqueue(next, candidate);
                }
            }
        }

        return NodeListCertificate(nodes, sourceNode, prev);
    }

    internal static Dictionary<string, List<(string neighbor, int weight)>> BuildAdjacencyList(UtilCollectionGraph graph)
    {
        var adjacency = new Dictionary<string, List<(string neigbor, int weight)>>();

        if (graph.Nodes.Count() == 0)
            return adjacency; // No edges, so return empty adjacency list

        foreach(UtilCollection rawEdge in graph.Edges.ToList())
        {
            // check if edge is weighted
            bool firstLooksLikeCollection = LooksLikeCollection(rawEdge[0]);
            bool secondLooksLikeCollection = LooksLikeCollection(rawEdge[1]);
            bool isWeighted = rawEdge.Count() == 2 && firstLooksLikeCollection && !secondLooksLikeCollection;

            if(isWeighted)
            {
                UtilCollection endpoints = rawEdge[0];
                int w = int.Parse(rawEdge[1].ToString());

                if (endpoints.IsOrdered())
                    AddDirected(adjacency, endpoints[0].ToString(), endpoints[1].ToString(), w);
                else
                {
                    var cast = endpoints.ToList();
                    if(cast.Count == 1)
                    {
                        string v = cast[0].ToString();
                        AddDirected(adjacency, v, v, w);
                    }
                    else
                    {
                        string a = cast[0].ToString();
                        string b = cast[1].ToString();
                        AddDirected(adjacency, a, b, w);
                        AddDirected(adjacency, b, a, w);
                    }
                }
            }
            else
            {
                int w = 1; // Assign the weight 1 by default if there is no assigned weight

                if (rawEdge.IsOrdered())
                    AddDirected(adjacency, rawEdge[0].ToString(), rawEdge[1].ToString(), w);
                else
                {
                    var cast = rawEdge.ToList();
                    if (cast.Count == 1)
                    {
                        string v = cast[0].ToString();
                        AddDirected(adjacency, v, v, w);
                    }
                    else
                    {
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
    private static void AddDirected(Dictionary<string, List<(string neighbor, int weight)>> adjacency, string from, string to, int weight)
    {
        if(!adjacency.TryGetValue(from, out var list))
        {
            list = new List<(string neighbor, int weight)>();
            adjacency[from] = list;
        }
        list.Add((to, weight));

        if (!adjacency.ContainsKey(to))
            adjacency[to] = new List<(string, int)>();
    }

    // ReconstructPath: Reconstructs the path from source to each node using the previous dictionionary
    internal static List<string> ReconstructPath(Dictionary<string, string?> prev, string? source, string target)
    {
        var path = new List<string>();
        string? current = target;

        while(current != null)
        {
            path.Add(current);
            if(current == source)
            {
                break; // Stop if we've reached the source node
            }
            current = prev[current];
        }

        path.Reverse(); // Reverse the list so path order is source to target

        if(path.Count == 0 || path[0] != source)
        {
            return new List<string>(); // No valid path
        }
        return path;
    }

    // NodeListCertificate: Converts a set of (node, path) tuples for every node in the graph
    internal static string NodeListCertificate(List<string> nodes, string sourceNode, Dictionary<string, string?> prev)
    {
        var entries = new List<string>();

        foreach(string node in nodes)
        {
            List<string> path = ReconstructPath(prev, sourceNode, node);
            string pathCertificate = NodeListToCertificate(path);
            entries.Add($"({node},{pathCertificate})");
        }

        return "{" + string.Join(",", entries) + "}";
    }

    // NodeListToCertificate: Converts a list of nodes into the certificate form
    internal static string NodeListToCertificate(List<string> nodes)
    {
        if (nodes == null || nodes.Count == 0)
            return "{}";
        return "{" + string.Join(",", nodes) + "}";
    }

    // LooksLikeCollection: Helper method to determine if a UtilCollection looks like a collection
    private static bool LooksLikeCollection(UtilCollection u)
    {
        string s = u.ToString().TrimStart();
        return s.StartsWith("{") || s.StartsWith("(");
    }

    public class SSSPGraphStep
    {
        public string? currentNode { get; set; } // node just settled in this step
        public string? currentEdgeFrom { get; set; } // winning edge that determines the shortest path for node
        public List<string> knownNodes { get; set; } = new(); // all settled nodes so far
        public List<(string from, string to)> treeEdges { get; set; } = new(); // accepted tree edges so far
    }

    // GetSteps: The steps are returned as a list of string, where each string represents a path in the certificate format
    public List<Object> GetSteps(SSSP problem)
    {
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

        while(pq.Count > 0)
        {
            if (timerHasExpired)
                return steps;

            string current = pq.Dequeue();
            if (visited.Contains(current))
                continue;

            visited.Add(current);

            steps.Add(BuildGraphStep(current, prev, visited));

            if (!adjacency.TryGetValue(current, out var neighbors))
                continue;

            foreach(var (next, weight) in neighbors)
            {
                if (visited.Contains(next))
                    continue;

                if (weight < 0)
                    return steps;

                if (dist[current] == int.MaxValue)
                    continue;

                int candidate = dist[current] + weight;
                if(candidate < dist[next])
                {
                    dist[next] = candidate;
                    prev[next] = current;
                    pq.Enqueue(next, candidate);
                }
            }
        }
        return steps;
    }

    // BuildGraphStep: assembles one snapshot of the shortest path tree's state
    private static SSSPGraphStep BuildGraphStep(string current, Dictionary<string, string?> prev, HashSet<string> visited)
    {
        var treeEdges = new List<(string from, string to)>();
        foreach(var predecessor in prev)
        {
            if (predecessor.Value != null)
                treeEdges.Add((predecessor.Value, predecessor.Key));
        }

        return new SSSPGraphStep
        {
            currentNode = current,
            currentEdgeFrom = prev[current],
            knownNodes = visited.ToList(),
            treeEdges = treeEdges
        };
    }

    public class SSSPTableStepVertex
    {
        public string name { get; set; } = "";
        public string display { get; set; } = "\u221E";
        public string? path { get; set; }
    }

    public class SSSPTableStep : API_JSON
    {
        public List<SSSPTableStepVertex> vertices { get; set; } = new();
        public List<string> knownSet { get; set; } = new();
        public string? currentVertex { get; set; }
        public string? sourceVertex { get; set; }
    }

    public List<Object> GetTableSteps(SSSP problem)
    {
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

        steps.Add(BuildTableStep(nodes, dist, prev, visited, currentVertex: null, sourceVertex: sourceNode));

        while(pq.Count > 0)
        {
            if (timerHasExpired)
                return steps;

            string current = pq.Dequeue();

            if (visited.Contains(current))
                continue;

            visited.Add(current);

            steps.Add(BuildTableStep(nodes, dist, prev, visited, currentVertex: null, sourceVertex: sourceNode));

            if (!adjacency.TryGetValue(current, out var neighbors))
                continue;

            foreach(var (next, weight) in neighbors)
            {
                if (visited.Contains(next))
                    continue;

                if (dist[current] == int.MaxValue)
                    continue;

                int candidate = dist[current] + weight;
                if (candidate < dist[next])
                {
                    dist[next] = candidate;
                    prev[next] = current;
                    pq.Enqueue(next, candidate);
                }
            }
            steps.Add(BuildTableStep(nodes, dist, prev, visited, currentVertex: current, sourceVertex: sourceNode));
        }
        return steps;
    }

    private static SSSPTableStep BuildTableStep(
        List<string> nodes,
        Dictionary<string, int> dist,
        Dictionary<string, string?> prev,
        HashSet<string> visited,
        string? currentVertex,
        string? sourceVertex)
    {
        return new SSSPTableStep
        {
            currentVertex = currentVertex,
            sourceVertex = sourceVertex,
            knownSet = visited.ToList(),
            vertices = nodes.Select(n => new SSSPTableStepVertex
            {
                name = n,
                display = dist[n] == int.MaxValue
                    ? "\u221E"
                    : (prev[n] != null ? $"{dist[n]}, {prev[n]}" : $"{dist[n]}"),
                path = dist[n] == int.MaxValue
                    ? null
                    : NodeListToCertificate(ReconstructPath(prev, sourceVertex, n))
            }).ToList()
        };
    }
}