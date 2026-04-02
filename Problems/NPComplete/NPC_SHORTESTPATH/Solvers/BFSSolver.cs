using System;
using System.Collections.Generic;
using System.Linq;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Problems.NPComplete.NPC_SHORTESTPATH;
using SPADE;

namespace API.Problems.NPComplete.NPC_SHORTESTPATH.Solvers;

class BFSSolver : ISolver<SHORTESTPATH>
{
    // ----- Fields ----- //
    public string solverName { get; } = "Breadth-First Search Algorithm";
    public string solverDefinition { get; } =
    "This solver implements the BFS algorithm to find the shortest path from a source node to a target node in a non-weighted graph.";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Rajit Nilkar" };
    public bool timerHasExpired { get; set; }

    public string solve(SHORTESTPATH problem)
    {
        // Get the graph from the problem instance
        UtilCollectionGraph graph = problem.graph;

        List<string> nodes = graph.Nodes.ToList().Select(n => n.ToString()).ToList();

        if (nodes.Count == 0)
            return "{}"; // No nodes, return empty path

        string sourceNode = nodes[0];
        string targetNode = nodes[^1];

        var adjacency = BuildAdjacency(graph);

        // Initialize distances
        var dist = nodes.ToDictionary(n => n, _ => int.MaxValue);
        var prev = nodes.ToDictionary(n => n, _ => (string)null);
        var visited = new HashSet<string>();
        Queue<string> queue = new Queue<string>();

        dist[sourceNode] = 0;
        visited.Add(sourceNode);
        queue.Enqueue(sourceNode);
        while (queue.Count > 0)
        {
            if (timerHasExpired)
                return "{}"; //Return empty path if timer has expired

            var current = queue.Dequeue();

            if (current == targetNode)
                return NodeListToCertificate(ReconstructPath(prev, sourceNode, targetNode));

            if (!adjacency.TryGetValue(current, out var neighbors))
                continue; //No neighbors, skip

            foreach(var neighbor in neighbors)
            {
                if(!visited.Contains(neighbor))
                {
                    visited.Add(neighbor);
                    dist[neighbor] = dist[current] + 1;
                    prev[neighbor] = current;
                    queue.Enqueue(neighbor);
                }
            }
        }

        if (dist[targetNode] == int.MaxValue)
            return "{}"; // No path found

        List<string> path = ReconstructPath(prev, sourceNode, targetNode);
        return NodeListToCertificate(path);

    }

    //Helper method to build an adjacent list from the graph
    internal static Dictionary<string, List<string>> BuildAdjacency(UtilCollectionGraph graph)
    {
        var adjacency = new Dictionary<string, List<string>>();

        // Ensure every node appears in adjacency (even isolated)
        foreach (var node in graph.Nodes)
            adjacency[node.ToString()] = new List<string>();

        if (graph.Edges.Count() == 0)
            return adjacency; // No edges, return empty adjacency list

        foreach (UtilCollection rawEdge in graph.Edges.ToList())
        {
            // Detect whether the edge uses a weighted representation like Dijkstra's parsing:
            bool firstLooksLikeCollection = LooksLikeCollection(rawEdge[0]);
            bool secondLooksLikeCollection = rawEdge.Count() > 1 && LooksLikeCollection(rawEdge[1]);
            bool isWeighted = rawEdge.Count() == 2 && firstLooksLikeCollection && !secondLooksLikeCollection;

            if (isWeighted)
            {
                // Weighted edge; ignore weights for BFS but extract endpoints
                UtilCollection endpoints = rawEdge[0];
                var cast = endpoints.ToList();
                if (endpoints.IsOrdered())
                {
                    // ordered endpoints -> directed
                    AddDirected(adjacency, endpoints[0].ToString(), endpoints[1].ToString());
                }
                else
                {
                    // unordered endpoints -> undirected set {a,b}
                    if (cast.Count == 1)
                    {
                        string v = cast[0].ToString();
                        AddDirected(adjacency, v, v);
                    }
                    else
                    {
                        string a = cast[0].ToString();
                        string b = cast[1].ToString();
                        AddDirected(adjacency, a, b);
                        AddDirected(adjacency, b, a);
                    }
                }
            }
            else
            {
                // Unweighted edge
                if (rawEdge.IsOrdered())
                {
                    // ordered pair (a,b) -> directed
                    AddDirected(adjacency, rawEdge[0].ToString(), rawEdge[1].ToString());
                }
                else
                {
                    // unordered pair -> undirected
                    var cast = rawEdge.ToList();
                    if (cast.Count == 1)
                    {
                        string v = cast[0].ToString();
                        AddDirected(adjacency, v, v);
                    }
                    else
                    {
                        string a = cast[0].ToString();
                        string b = cast[1].ToString();
                        AddDirected(adjacency, a, b);
                        AddDirected(adjacency, b, a);
                    }
                }
            }
        }

        return adjacency;
    }

    // AddDirected: Adds a directed edge to the adjacency list (creates list if missing)
    private static void AddDirected(Dictionary<string, List<string>> adjacency, string from, string to)
    {
        if (!adjacency.TryGetValue(from, out var list))
        {
            list = new List<string>();
            adjacency[from] = list;
        }
        list.Add(to);

        // Ensure 'to' node exists in dictionary so TryGetValue works later
        if (!adjacency.ContainsKey(to))
            adjacency[to] = new List<string>();
    }

    // ReconstructPath: Reconstructs the path from source to target using the prev dictionary
    internal static List<string> ReconstructPath(Dictionary<string, string?> prev, string source, string target)
    {
        var path = new List<string>();
        string current = target;

        while (current != null)
        {
            path.Add(current);
            if (current == source)
                break; // Stop if we've reached the source node
            current = prev[current];
        }
        path.Reverse();

        if (path.Count == 0 || path[0] != source)
            return new List<string>(); // No valid path found
        return path;
    }

    // NodeListToCertificate: Converts a list of nodes into the certificate format
    internal static string NodeListToCertificate(List<string> nodes)
    {
        if (nodes == null || nodes.Count == 0)
            return "{}"; // No path found, return empty certificate
        return "{" + string.Join(",", nodes) + "}";
    }

    // LooksLikeCollection: Helper method to determine if a UtilCollection looks like a collection
    private static bool LooksLikeCollection(UtilCollection u)
    {
        string s = u.ToString().TrimStart();
        return s.StartsWith("{") || s.StartsWith("(");
    }
}
