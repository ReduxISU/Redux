using System;
using System.Linq;
using System.Collections.Generic;
using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Problems.P.P_SSSP.Solvers;

namespace API.Problems.P.P_SSSP.Verifiers;

class SSSPVerifier : IVerifier<SSSP> {
        public string verifierName { get; } = "Single Source Shortest Path Verifier";
        public string verifierDefinition { get; } = "Verifies the solution for the Single Source Shortest Path problem";
        public string source { get; } = "";
        public string[] contributors { get; } = { "Rajit Nilkar" };
        private string _certificate = "";
        public string certificate => _certificate;

        public SSSPVerifier() { }

        // verify : takes a problem instance and a solution certificate,
        // and returns true if the certificate is a valid solution to the problem instance
        // and false otherwise
        public bool verify(SSSP problem, string solution) {
                _certificate = solution ?? "";

                var nodes = problem.graph.Nodes.ToList().Select(n => n.ToString()).ToList();

                if (nodes.Count == 0)
                        return solution == "{}" || string.IsNullOrWhiteSpace(solution);

                string sourceNode = problem.sourceNode;
                var adjacency = SSSPSolver.BuildAdjacencyList(problem.graph);

                // Compute shortest distance for each node
                var trueDist = AllShortestDistances(adjacency, nodes, sourceNode);

                // Parse the certificate as a set of (node, path) tuples
                Dictionary<string, List<string>> certPaths;
                try {
                        certPaths = ParseSSSPCertificate(_certificate);
                }
                catch {
                        return false; // Invalid certificate format
                }

                if (certPaths.Count != nodes.Count || !nodes.All(n => certPaths.ContainsKey(n)))
                        return false;

                foreach (string node in nodes) {
                        List<string> path = certPaths[node];
                        bool trueUnreachable = trueDist[node] == null;

                        if (path.Count == 0) {
                                if (!trueUnreachable) {
                                        return false; // certificate says unreachable, but actually reachable
                                }
                                continue;
                        }

                        if (trueUnreachable)
                                return false; // certificate gives a path for a node that is actually unreachable

                        if (path[0] != sourceNode || path[^1] != node)
                                return false; // path must start at the source and end at the node

                        int length = 0;
                        for (int i = 0; i < path.Count - 1; i++) {
                                string u = path[i];
                                string v = path[i + 1];

                                if (!adjacency.TryGetValue(u, out var neighbors))
                                        return false;

                                var weights = neighbors.Where(e => e.neighbor == v).Select(e => e.weight).ToList();
                                if (weights.Count == 0)
                                        return false;

                                int w = weights.Min();
                                if (w < 0)
                                        return false;

                                length += w;
                        }
                        if (length != trueDist[node])
                                return false; // path exists but is not the shortest
                }
                return true;
        }

        private static Dictionary<string, int?> AllShortestDistances(Dictionary<string, List<(string neighbor, int weight)>> adjacency, List<string> allNodes, string source) {
                var dist = allNodes.ToDictionary(n => n, _ => int.MaxValue);
                var visited = new HashSet<string>();
                var pq = new PriorityQueue<string, int>();

                dist[source] = 0;
                pq.Enqueue(source, 0);

                while (pq.Count > 0) {
                        string current = pq.Dequeue();
                        if (visited.Contains(current))
                                continue;
                        visited.Add(current);

                        if (!adjacency.TryGetValue(current, out var neighbors))
                                continue;

                        foreach (var (next, weight) in neighbors) {
                                if (weight < 0)
                                        throw new InvalidOperationException("SSSP using Dijkstra's algorithm cannot handle negative edge weights.");

                                if (visited.Contains(next))
                                        continue;

                                if (dist[current] == int.MaxValue)
                                        continue;

                                int candidate = dist[current] + weight;
                                if (candidate < dist[next]) {
                                        dist[next] = candidate;
                                        pq.Enqueue(next, candidate);
                                }
                        }
                }
                return allNodes.ToDictionary(n => n, n => dist[n] == int.MaxValue ? (int?)null : dist[n]);
        }

        public static Dictionary<string, List<string>> ParseSSSPCertificate(string certificate) {
                var result = new Dictionary<string, List<string>>();

                string trimmed = certificate.Trim();
                if (trimmed.Length < 2 || trimmed[0] != '{' || trimmed[^1] != '}')
                        throw new FormatException("Certificate must be wrapped in { }");

                string inner = trimmed.Substring(1, trimmed.Length - 2).Trim();
                if (inner.Length == 0)
                        return result; // empty certificate, no entries

                List<string> tupleChunks = SplitTopLevel(inner);

                foreach (string chunk in tupleChunks) {
                        string tupleInner = chunk.Trim();
                        if (tupleInner.Length < 2 || tupleInner[0] != '(' || tupleInner[^1] != ')')
                                throw new FormatException($"Malformed tuple: {chunk}");

                        tupleInner = tupleInner.Substring(1, tupleInner.Length - 2);

                        // splits just the outer tuple into exactly 2 top-level pieces: node, path
                        List<string> parts = SplitTopLevel(tupleInner);
                        if (parts.Count != 2)
                                throw new FormatException($"Expected (node, path) pair, got: {chunk}");

                        string node = parts[0].Trim();
                        string pathStr = parts[1].Trim();

                        List<string> path = pathStr == "{}" ? new List<string>() : GraphParser.parseNodeListWithStringFunctions(pathStr);

                        result[node] = path;
                }
                return result;
        }

        // SplitTopLevel: splits a string on commas that sit at depth 0 (ignoring commas nested inside { } or ( ))
        private static List<string> SplitTopLevel(string s) {
                var parts = new List<string>();
                int depth = 0;
                int start = 0;

                for (int i = 0; i < s.Length; i++) {
                        char c = s[i];
                        if (c == '{' || c == '(')
                                depth++;
                        else if (c == '}' || c == ')')
                                depth--;
                        else if (c == ',' && depth == 0) {
                                parts.Add(s.Substring(start, i - start));
                                start = i + 1;
                        }
                }
                parts.Add(s.Substring(start));
                return parts;
        }
}