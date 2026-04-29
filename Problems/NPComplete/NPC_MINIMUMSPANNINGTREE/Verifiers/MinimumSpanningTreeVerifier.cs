using System;
using System.Collections.Generic;
using System.Linq;
using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Problems.NPComplete.NPC_MINIMUMSPANNINGTREE.Solvers;
using API.Problems.NPComplete.NPC_MINIMUMSPANNINGTREE;
using SPADE;

namespace API.Problems.NPComplete.NPC_MINIMUMSPANNINGTREE.Verifiers;

class MinimumSpanningTreeVerifier : IVerifier<NPC_MINIMUMSPANNINGTREE>
{
    public string verifierName { get; } = "Minimum Spanning Tree Verifier";
    public string verifierDefinition { get; } = "Verifies that a proposed edge set is a valid minimum spanning tree for the input graph.";
    public string source { get; } = "https://en.wikipedia.org/wiki/Minimum_spanning_tree";
    public string[] contributors { get; } = { "Andreas Kramer", "Val Kimbrough" };
    private string _certificate = string.Empty;
    public string certificate => _certificate;

    public bool verify(NPC_MINIMUMSPANNINGTREE problem, string solution)
    {
        _certificate = solution ?? string.Empty;
        var nodes = problem.graph.Nodes.ToList().Select(n => n.ToString()).Distinct().ToList();
        if (nodes.Count == 0)
            return string.IsNullOrWhiteSpace(solution) || solution.Trim() == "{}";

        if (string.IsNullOrWhiteSpace(solution) || solution.Trim() == "{}")
            return nodes.Count <= 1;

        List<string> parsedNodes = GraphParser.parseNodeListWithStringFunctions(solution);
        if (parsedNodes.Count == 0)
            return false;

        var uniqueEdges = ParseCertificateEdges(solution);
        if (nodes.Count == 1 && uniqueEdges.Count == 0)
            return true;

        if (uniqueEdges.Count != nodes.Count - 1)
            return false;

        var graphEdges = BuildGraphEdgeMap(problem.graph);
        foreach (var edge in uniqueEdges)
        {
            if (!graphEdges.ContainsKey(edge))
                return false;
        }

        if (!IsAcyclicAndSpanning(uniqueEdges, nodes))
            return false;

        string optimal = new KruskalSolver().solve(problem);
        if (optimal == "{}")
            return false;

        var certificateWeight = TotalWeight(uniqueEdges, graphEdges);
        var optimalWeight = TotalWeight(ParseCertificateEdges(optimal), graphEdges);
        return certificateWeight == optimalWeight;
    }

    private static HashSet<(string u, string v)> ParseCertificateEdges(string certificate)
    {
        var edgePairs = GraphParser.parseUndirectedEdgeListWithStringFunctions(certificate);
        var uniqueEdges = new HashSet<(string u, string v)>();

        foreach (var pair in edgePairs)
        {
            string u = pair.Key;
            string v = pair.Value;
            var canonical = CanonicalPair(u, v);
            uniqueEdges.Add(canonical);
        }

        return uniqueEdges;
    }

    private static Dictionary<(string u, string v), int> BuildGraphEdgeMap(UtilCollectionGraph graph)
    {
        var map = new Dictionary<(string u, string v), int>();
        foreach (UtilCollection rawEdge in graph.Edges.ToList())
        {
            if (rawEdge.Count() != 2)
                continue;

            bool firstLooksLikeCollection = LooksLikeCollection(rawEdge[0]);
            bool secondLooksLikeCollection = LooksLikeCollection(rawEdge[1]);
            if (!firstLooksLikeCollection || secondLooksLikeCollection)
                continue;

            UtilCollection endpoints = rawEdge[0];
            int weight = int.Parse(rawEdge[1].ToString());
            string u;
            string v;
            if (endpoints.IsOrdered())
            {
                u = endpoints[0].ToString();
                v = endpoints[1].ToString();
            }
            else
            {
                var cast = endpoints.ToList();
                if (cast.Count == 1)
                {
                    u = cast[0].ToString();
                    v = cast[0].ToString();
                }
                else
                {
                    u = cast[0].ToString();
                    v = cast[1].ToString();
                }
            }

            var canonical = CanonicalPair(u, v);
            if (!map.ContainsKey(canonical) || weight < map[canonical])
                map[canonical] = weight;
        }
        return map;
    }

    private static bool IsAcyclicAndSpanning(HashSet<(string u, string v)> edges, List<string> nodes)
    {
        var uf = new UnionFind<string>(nodes);
        foreach (var edge in edges)
        {
            if (uf.Find(edge.u) == uf.Find(edge.v))
                return false;
            uf.Union(edge.u, edge.v);
        }

        string root = uf.Find(nodes[0]);
        return nodes.All(node => uf.Find(node).Equals(root));
    }

    private static int TotalWeight(HashSet<(string u, string v)> edges, Dictionary<(string u, string v), int> weights)
    {
        int total = 0;
        foreach (var edge in edges)
        {
            total += weights[edge];
        }
        return total;
    }

    private static (string u, string v) CanonicalPair(string u, string v)
    {
        return string.CompareOrdinal(u, v) <= 0 ? (u, v) : (v, u);
    }

    private static bool LooksLikeCollection(UtilCollection u)
    {
        string s = u.ToString().TrimStart();
        return s.StartsWith("{") || s.StartsWith("(");
    }

    private class UnionFind<T> where T : notnull
    {
        private readonly Dictionary<T, T> parent = new();
        private readonly Dictionary<T, int> rank = new();

        public UnionFind(IEnumerable<T> items)
        {
            foreach (T item in items)
            {
                parent[item] = item;
                rank[item] = 0;
            }
        }

        public T Find(T item)
        {
            if (!parent.ContainsKey(item))
                throw new InvalidOperationException("Item is not part of this union-find structure.");

            if (!EqualityComparer<T>.Default.Equals(parent[item], item))
                parent[item] = Find(parent[item]);
            return parent[item];
        }

        public void Union(T a, T b)
        {
            T rootA = Find(a);
            T rootB = Find(b);
            if (EqualityComparer<T>.Default.Equals(rootA, rootB))
                return;

            if (rank[rootA] < rank[rootB])
                parent[rootA] = rootB;
            else if (rank[rootA] > rank[rootB])
                parent[rootB] = rootA;
            else
            {
                parent[rootB] = rootA;
                rank[rootA]++;
            }
        }
    }
}
