using System;
using System.Collections.Generic;
using System.Linq;
using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Problems.P.P_SPSP.Solvers;

namespace API.Problems.P.P_SPSP.Verifiers;

class SPSPVerifier : IVerifier<SPSP> {
        public string verifierName { get; } = "Single Pair Shortest Path Verifier";
        public string verifierDefinition { get; } = "Verifies the solution for the Single Pair Shortest Path problem";
        public string source { get; } = "";
        public string[] contributors { get; } = { "Rajit Nilkar", "Scott Barfuss" };
        private string _certificate = "";
        public string certificate => _certificate;

        public SPSPVerifier() { }

        // verify: takes a problem instance and a solution certificate,
        // and returns true if the certificate is a valid solution to the problem instance,
        // false otherwise
        public bool verify(SPSP problem, string solution) {
                _certificate = solution ?? ""; // Reset certificate before verification

                var nodes = problem.graph.Nodes.ToList().Select(n => n.ToString()).ToList();
                if (nodes.Count == 0)
                        // If there are no nodes, the only valid solution is an empty path
                        return solution == "{}" || string.IsNullOrWhiteSpace(solution);

                string sourceNode = problem.sourceNode;
                string targetNode = problem.targetNode;

                var adjacency = SPSPSolver.BuildAdjacencyList(problem.graph);

                // Compute true shortest distance using Dijkstra's algorithm
                int? shortest = ShortestDistance(adjacency, nodes, sourceNode, targetNode);

                // Allow {} as a certificate meaning "no path"
                if (string.IsNullOrWhiteSpace(solution) || solution.Trim() == "{}")
                        return shortest is null; // Valid if and only if target is unreachable

                List<string> path;
                try {
                        // Parse the certificate as a path
                        path = GraphParser.parseNodeListWithStringFunctions(solution);
                }
                catch {
                        return false; // Invalid certificate format
                }

                if (path.Count == 0)
                        return shortest is null; // Empty path is only valid if target is unreachable

                if (path[0] != sourceNode || path[^1] != targetNode)
                        return false; // Path must start at source and end at target

                // Validate each hop is an edge, and compute total path length
                int length = 0;
                for (int i = 0; i < path.Count - 1; i++) {
                        string u = path[i];
                        string v = path[i + 1];

                        if (!adjacency.TryGetValue(u, out var neighbors))
                                return false;

                        // Pick min weight among possible edges if any
                        var weights = neighbors.Where(e => e.neighbor == v).Select(e => e.weight).ToList();
                        if (weights.Count == 0)
                                return false; // No edges from u to v

                        int w = weights.Min();
                        if (w < 0)
                                return false; // No negative weights allowed

                        length += w;
                }
                return shortest is int s && length == s; // Valid if path is correct and matches true shortest distance
        }

        // ShortestDistance: helper method to compute shortest distance using Dijkstra's algorithm
        private static int? ShortestDistance(
            Dictionary<string, List<(string neighbor, int weight)>> adjacency,
            List<string> allNodes,
            string source,
            string target) {
                var dist = allNodes.ToDictionary(n => n, _ => int.MaxValue);
                var visited = new HashSet<string>();
                PriorityQueue<string, int> pq = new PriorityQueue<string, int>();

                dist[source] = 0;
                pq.Enqueue(source, 0);

                while (pq.Count > 0) {
                        string current = pq.Dequeue();
                        if (visited.Contains(current))
                                continue;
                        visited.Add(current); // Mark as visited

                        if (current == target)
                                break; // Stop if we've reached the target node

                        if (!adjacency.TryGetValue(current, out var neighbors))
                                continue; // No neighbors, skip

                        foreach (var (next, weight) in neighbors) {
                                if (weight < 0)
                                        throw new InvalidOperationException("SPSP does not allow negative edge weights.");

                                if (visited.Contains(next))
                                        continue; // Skip visited neighbors

                                if (dist[current] == int.MaxValue)
                                        continue; // Skip if current node is unreachable

                                int candidate = dist[current] + weight;
                                if (candidate < dist[next]) {
                                        dist[next] = candidate;
                                        pq.Enqueue(next, candidate); // Update priority in the queue
                                }
                        }
                }

                if (dist[target] == int.MaxValue)
                        return null; // Target is unreachable

                return dist[target]; // Return shortest distance to target
        }
}
