using System;
using System.Collections.Generic;
using System.Linq;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Problems.NPComplete.NPC_MINIMUMSPANNINGTREE;
using SPADE;

namespace API.Problems.NPComplete.NPC_MINIMUMSPANNINGTREE.Solvers;

class KruskalSolver : ISolver<MINIMUMSPANNINGTREE> {
    public string solverName { get; } = "Kruskal's Algorithm";
    public string solverDefinition { get; } = "Finds a minimum spanning tree by sorting edges by weight and adding each edge if it joins two different components.";
    public string source { get; } = "Kruskal, J. B. \"On the shortest spanning subtree of a graph and the traveling salesman problem.\" Proceedings of the American Mathematical Society 7, no. 1 (1956): 48-50.";
    public string sourceLink { get; } = "https://doi.org/10.2307/2033241";
    public string[] contributors { get; } = { "Andreas Kramer" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Repeatedly adds the lowest-weight edge that doesn't form a
    // cycle -- an irrevocable, locally-optimal choice at each step.
    public SolverType solverType { get; } = SolverType.Greedy;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
    // OrderBy sorts all E edges (O(E log E)); Union-Find uses path compression + union by
    // rank, near-O(1) amortized per operation, so the sort dominates.
    public string complexity { get; } = "O(E log E)";

    public string solve(MINIMUMSPANNINGTREE problem) {
        List<string> nodes = problem.graph.Nodes.ToList().Select(n => n.ToString()).Distinct().ToList();
        var edges = ExtractEdges(problem.graph);

        if (nodes.Count == 0)
            return "{}";

        UnionFind<string> uf = new(nodes);
        List<(string u, string v, int weight)> selected = new();

        // Deterministic tie-breaking keeps certificates stable across runs.
        foreach (var edge in edges.OrderBy(e => e.weight).ThenBy(e => CanonicalKey(e.u, e.v))) {
            if (timerHasExpired)
                return "{}";

            if (uf.Find(edge.u) != uf.Find(edge.v)) {
                uf.Union(edge.u, edge.v);
                selected.Add(edge);
                if (selected.Count == nodes.Count - 1)
                    break;
            }
        }

        if (selected.Count != nodes.Count - 1)
            return "{}";

        return EdgeListToCertificate(selected);
    }

    internal static List<(string u, string v, int weight)> ExtractEdges(UtilCollectionGraph graph) {
        var edges = new List<(string u, string v, int weight)>();
        foreach (UtilCollection rawEdge in graph.Edges.ToList()) {
            if (rawEdge.Count() != 2)
                continue;

            bool firstLooksLikeCollection = LooksLikeCollection(rawEdge[0]);
            bool secondLooksLikeCollection = LooksLikeCollection(rawEdge[1]);
            if (!firstLooksLikeCollection || secondLooksLikeCollection)
                continue;

            // Accept the weighted-edge encoding produced by the MST parser and normalize it here.
            UtilCollection endpoints = rawEdge[0];
            int weight = int.Parse(rawEdge[1].ToString());
            if (endpoints.IsOrdered()) {
                edges.Add((endpoints[0].ToString(), endpoints[1].ToString(), weight));
            } else {
                var cast = endpoints.ToList();
                if (cast.Count == 1) {
                    string node = cast[0].ToString();
                    edges.Add((node, node, weight));
                } else {
                    edges.Add((cast[0].ToString(), cast[1].ToString(), weight));
                }
            }
        }
        return edges;
    }

    internal static string EdgeListToCertificate(List<(string u, string v, int weight)> edges) {
        if (edges == null || edges.Count == 0)
            return "{}";

        // Certificates store only the chosen undirected edges; weights stay in the original instance.
        var sorted = edges
            .Select(edge => (u: edge.u, v: edge.v))
            .Select(edge => (u: edge.u, v: edge.v, key: CanonicalKey(edge.u, edge.v)))
            .OrderBy(edge => edge.key)
            .Select(edge => $"{{{CanonicalFirst(edge.u, edge.v)},{CanonicalSecond(edge.u, edge.v)}}}")
            .ToList();

        return "{" + string.Join(",", sorted) + "}";
    }

    internal static string CanonicalKey(string u, string v) {
        return string.CompareOrdinal(u, v) <= 0 ? $"{u}|{v}" : $"{v}|{u}";
    }

    internal static string CanonicalFirst(string u, string v) {
        return string.CompareOrdinal(u, v) <= 0 ? u : v;
    }

    internal static string CanonicalSecond(string u, string v) {
        return string.CompareOrdinal(u, v) <= 0 ? v : u;
    }

    private static bool LooksLikeCollection(UtilCollection u) {
        string s = u.ToString().TrimStart();
        return s.StartsWith("{") || s.StartsWith("(");
    }

    private class UnionFind<T> where T : notnull {
        private readonly Dictionary<T, T> parent = new();
        private readonly Dictionary<T, int> rank = new();

        public UnionFind(IEnumerable<T> items) {
            foreach (T item in items) {
                parent[item] = item;
                rank[item] = 0;
            }
        }

        public T Find(T item) {
            if (!parent.ContainsKey(item))
                throw new InvalidOperationException("Item is not part of this union-find structure.");

            // Path compression keeps repeated connectivity checks near-constant amortized time.
            if (!EqualityComparer<T>.Default.Equals(parent[item], item))
                parent[item] = Find(parent[item]);
            return parent[item];
        }

        public void Union(T a, T b) {
            T rootA = Find(a);
            T rootB = Find(b);
            if (EqualityComparer<T>.Default.Equals(rootA, rootB))
                return;

            if (rank[rootA] < rank[rootB])
                parent[rootA] = rootB;
            else if (rank[rootA] > rank[rootB])
                parent[rootB] = rootA;
            else {
                parent[rootB] = rootA;
                rank[rootA]++;
            }
        }
    }
}
