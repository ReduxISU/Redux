using System;
using System.Collections.Generic;
using System.Linq;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Problems.NPComplete.NPC_SHORTESTPATH;
using Xunit;

namespace API.Problems.NPComplete.NPC_SHORTESTPATH.Solvers;

public class DijkstraSolver : ISolver<SHORTESTPATH>
{
	// ----- Fields ----- //
	public string solverName { get; } = "Dijkstra's Algorithm";
	public string solverDefinition { get; } = "This solver implements Dijkstra's algorithm to find the shortest path from a source node to a target node in a positively weighted graph.";
	public string source { get; } = "";
	public string[] contributors { get; } = { "Rajit Nilkar" };
	public bool timerHasExpired { get; set; }

	public string solve(SHORTESTPATH problem)
	{
		// Get the graph from the problem instance
		UtilCollectionGraph graph = problem.graph;

		//Get nodes as strings
		List<string> nodeList = graph.Nodes.ToList().Select(n => n.ToString()).ToList();

		string sourceNode = nodeist[0];

		//Initialize distances
		Dictionary<string, int> distances = new Dictionary<string, int>();
		Dictionary<string, bool> visited = new Dictionary<string, bool>();
		Dictionary<string, string> previous = new Dictionary<string, string>();

		DijkstraPriorityQueue<string> pq = new DijkstraPriorityQueue<string>();

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

		while (pq.Count > 0)
		{
			if (timerHasExpired)
			{
				break;
			}

			string current = pq.Dequeue();

			if (visited[current])
			{
				continue;
			}

			visited[current] = true;

			// Find edges from current node
			foreach (var edge in edgeList)
			{
				string from, to;
				int weight;

				if(graph.IsWeighted && graph.IsDirected)
				{
					// Format: ((a,b), w)
					from = edge[0][0].ToString();
					to = edge[0][1].ToString();
					weight = int.Parse(edge[1].ToString());
				}

                // Check if this edge starts from current node
                if (from == current && !visited[to])
                {
                    int newDistance = distances[current] + weight;

                    if (newDistance < distances[to])
                    {
                        distances[to] = newDistance;
                        previous[to] = current;
                        pq.Enqueue(to, newDistance);
                    }
                }

            }
        }

		return FormatResult(distances, previous);
    }

    private string FormatResult(Dictionary<string, int> distances, Dictionary<string, string> previous)
    {
        // Format as needed for your project
        // Example: return JSON or simple string representation
        var result = string.Join(", ", distances.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
        return $"Distances: {result}";
    }
}
