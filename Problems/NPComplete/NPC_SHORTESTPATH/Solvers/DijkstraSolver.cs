using System;
using System.Collections.Generic;
using System.Linq;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Problems.NPComplete.NPC_SHORTESTPATH;
using SPADE;

namespace API.Problems.NPComplete.NPC_SHORTESTPATH.Solvers;

class DijkstraSolver : ISolver<SHORTESTPATH>
{
	// ----- Fields ----- //
	public string solverName { get; } = "Dijkstra's Algorithm";
	public string solverDefinition { get; } =
	"This solver implements Dijkstra's algorithm to find the shortest path from a source node to a target node in a positively weighted graph.";
	public string source { get; } = "";
	public string[] contributors { get; } = { "Rajit Nilkar", "Scott Barfuss", "Tiger Sant" };
	public bool timerHasExpired { get; set; }

	public string solve(SHORTESTPATH problem)
	{
		// Get the graph from the problem instance
		UtilCollectionGraph graph = problem.graph;

		//Get nodes as strings
		List<string> nodes = graph.Nodes.ToList().Select(n => n.ToString()).ToList();
		if (nodes.Count == 0)
			return "{}"; // No nodes, return empty path

		string sourceNode = nodes[0];
		string targetNode = nodes[^1];

		var adjacency = BuildAdjacency(graph);

		//Initialize distances
		var dist = nodes.ToDictionary(n => n, _ => int.MaxValue);
        var prev = nodes.ToDictionary(n => n, _ => (string)null);
        var visited = new HashSet<string>();
        var pq = new DijkstraPriorityQueue<string>();

		/*
		//Initialize all nodes
		foreach (string node in nodeList)
		{
			distances[node] = int.MaxValue;
			visited[node] = false;
			previous[node] = null;
        }
		// Source has distance 0
		distances[sourceNode] = 0;
		pq.Enqueue(sourceNode, 0);
		// Get edges list
		List<UtilCollection> edgeList = graph.Edges.ToList();
		*/

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
				continue; // No neighbors, skip

			// Find edges from current node
			foreach (var (next, weight) in neighbors)
			{
				if(visited.Contains(next))
					continue; // Skip visited neighbors

				if(weight < 0)
					return "{}"; // No negative weights

				if(dist[current] == int.MaxValue)
					continue; // Skip if current node is unreachable
				
				int candidate = dist[current] + weight;
				if(candidate < dist[next])
				{
					dist[next] = candidate;
					prev[next] = current;
					pq.Enqueue(next, candidate); // Update priority queue with new distance
				}
			}
		}

		if (dist[targetNode] == int.MaxValue)
			return "{}"; // No path found
		
		List<string> path = ReconstructPath(prev, sourceNode, targetNode);
		return NodeListToCertificate(path);
	}

	// BuildAdjacency: Helper method to build an adjacency list representation of the graph
	internal static Dictionary<string, List<(string neighbor, int weight)>> BuildAdjacency(UtilCollectionGraph graph)
	{
		var adjacency = new Dictionary<string, List<(string neighbor, int weight)>>();
		
		if (graph.Edges.Count() == 0)
			return adjacency; // No edges, return empty adjacency list

		foreach (UtilCollection rawEdge in graph.Edges.ToList())
		{
			// Check if edge is weighted
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
						AddDirected(adjacency, v, v, w); // Self-loop
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
				
				if(rawEdge.IsOrdered())
					// If it's an ordered pair, treat it as a directed edge
					AddDirected(adjacency, rawEdge[0].ToString(), rawEdge[1].ToString(), w);
				else
				{
					// If it's an unordered pair, treat it as an undirected edge
					var cast = rawEdge.ToList();
					if (cast.Count == 1) // Add directed edge from the node to itself
					{
						string v = cast[0].ToString();
						AddDirected(adjacency, v, v, w); // Self-loop
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

		if(path.Count == 0 || path[0] != source)
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

    // The steps are returned as a list of strings, where each string representing a path in the following format: 1,5,3
    public List<string> GetSteps(SHORTESTPATH problem)
    {
        var steps = new List<string>();

        UtilCollectionGraph graph = problem.graph;

        List<string> nodes = graph.Nodes.ToList().Select(n => n.ToString()).ToList();
        if (nodes.Count == 0)
            return steps;

        string sourceNode = nodes[0];
        string targetNode = nodes[^1];

        var adjacency = BuildAdjacency(graph);

        var dist = nodes.ToDictionary(n => n, _ => int.MaxValue);
        var prev = nodes.ToDictionary(n => n, _ => (string)null);
        var visited = new HashSet<string>();
        var pq = new DijkstraPriorityQueue<string>();

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