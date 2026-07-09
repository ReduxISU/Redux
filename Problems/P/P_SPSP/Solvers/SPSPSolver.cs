using System;
using System.Collections.Generic;
using API.Interfaces;
using API.Interfaces.Graphs;
using SPADE;

namespace API.Problems.P.P_SPSP.Solvers;

class SPSPSolver : ISolver<SPSP>
{
    // ----- Fields ----- //
    public string solverName { get; } = "Dijkstra's Algorithm";
    public string solverDefinition { get; } = "";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Rajit Nilkar", "Scott Barfuss" };
    public bool timerHasExpired { get; set; }
    PriorityQueue<string, int>? pq;

    public string solve(SPSP problem)
    {
        UtilCollectionGraph graph = problem.graph;
        List<string> nodes = graph.Nodes.ToList().Select(n => n.ToString()).ToList();

        if (nodes.Count == 0)
        {
            return "{}"; // No nodes, return empty path
        }

        string sourceNode = problem.sourceNode;
        string targetNode = problem.targetNode;

        if (!nodes.Contains(sourceNode) || !nodes.Contains(targetNode))
        {
            return "{}";
        }

        var adjacency = BuildAdjacencyList(graph);

        var dist = nodes.ToDictionary(n => n, _ => int.MaxValue);
        var prev = nodes.ToDictionary(n => n, _ => (string?)null);
        var visited = new HashSet<string>();
        pq = new PriorityQueue<string, int>();

        dist[sourceNode] = 0;
        pq.Enqueue(sourceNode, 0);

        while (pq.Count > 0)
        {
            if (timerHasExpired)
                return "{}";

            string current = pq.Dequeue();

            if (visited.Contains(current))
                continue; // Skip if we've already visited this node
            visited.Add(current);

            if (current == targetNode)
                break; // Stop if we've reached the target node

            if (!adjacency.TryGetValue(current, out var neighbors))
                continue; // No neighbors, so skip

            foreach (var (next, weight) in neighbors)
            {
                if (visited.Contains(next))
                    continue; // Skip visited neighbors

                if (dist[current] == int.MaxValue)
                    continue; // Skip if current node is unreachable

                int candidate = dist[current] + weight;
                if (candidate < dist[next])
                {
                    dist[next] = candidate;
                    prev[next] = current;
                    pq.Enqueue(next, candidate);
                }
            }
        }

        if (dist[targetNode] == int.MaxValue)
            return "{}"; // No path found
        List<string> path = ReconstructPath(prev, sourceNode, targetNode);
        return NodeListToCertificate(path);
    }

    //Helper method to build an adjacent list from the graph
    internal static Dictionary<string, List<(string neighbor, int weight)>> BuildAdjacencyList(UtilCollectionGraph graph)
    {
        var adjacency = new Dictionary<string, List<(string neighbor, int weight)>>();

        if (graph.Nodes.Count() == 0)
            return adjacency; // No edges, so return empty adjacency list

        foreach (UtilCollection rawEdge in graph.Edges.ToList())
        {
            // check if edge is weighted
            bool firstLooksLikeCollection = LooksLikeCollection(rawEdge[0]);
            bool secondLooksLikeCollection = LooksLikeCollection(rawEdge[1]);
            bool isWeighted = rawEdge.Count() == 2 && firstLooksLikeCollection && !secondLooksLikeCollection;

            if (isWeighted)
            {
                UtilCollection endpoints = rawEdge[0];
                int w = int.Parse(rawEdge[1].ToString()); // Assuming the weight is an integer

                if (endpoints.IsOrdered())
                    AddDirected(adjacency, endpoints[0].ToString(), endpoints[1].ToString(), w);
                else
                {
                    var cast = endpoints.ToList();
                    if (cast.Count == 1) // Add directed edge from the node to itself
                    {
                        string v = cast[0].ToString();
                        AddDirected(adjacency, v, v, w);
                    }
                    else // Undirected edge, add in both directions
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
                int w = 1;

                if (rawEdge.IsOrdered())
                    AddDirected(adjacency, rawEdge[0].ToString(), rawEdge[1].ToString(), w);
                else
                {
                    // If it's an unordered pair, treat it as an undirected edge
                    var cast = rawEdge.ToList();
                    if (cast.Count() == 1)
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
        if (!adjacency.TryGetValue(from, out var list))
        {
            list = new List<(string neighbor, int weight)>();
            adjacency[from] = list;
        }
        list.Add((to, weight));

        if (!adjacency.ContainsKey(to))
            adjacency[to] = new List<(string, int)>();
    }

    // ReconstructPath: Reconstructs the path from source to target using the previous dictionary
    internal static List<string> ReconstructPath(Dictionary<string, string?> prev, string source, string target)
    {
        var path = new List<string>();
        string? current = target;

        while (current != null)
        {
            path.Add(current);
            if (current == source)
            {
                break; // Stop if we've reached the source node
            }
            current = prev[current];
        }

        path.Reverse();

        if (path.Count() == 0 || path[0] != source)
        {
            return new List<string>(); // No valid path
        }
        return path;
    }

    // NodeListCertificate: Converts a list of nodes into the certificate format
    internal static string NodeListToCertificate(List<string> nodes)
    {
        if (nodes == null || nodes.Count() == 0)
            return "{}"; // No path found, empty certificate
        return "{" + string.Join(",", nodes) + "}";
    }

    // LooksLikeCollection: Helper method to determine if a UtilCollection looks like a collection
    private static bool LooksLikeCollection(UtilCollection u)
    {
        string s = u.ToString().TrimStart();
        return s.StartsWith("{") || s.StartsWith("(");
    }

    // GetSteps: The steps are returned as a list of strings, where each string representing a path in the certificate format
    public List<Object> GetSteps(SPSP problem)
    {
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

        while (pq.Count > 0)
        {
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

            foreach (var (next, weight) in neighbors)
            {
                if (visited.Contains(next))
                    continue;

                if (weight < 0)
                    return steps;

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
        }
        return steps;
    }
}