using System.Collections.Generic;
using System.Linq;
using API.Interfaces;
using API.Problems.NPComplete.NPC_STRONGLYCONNECTEDCOMPONENTS;

namespace API.Problems.NPComplete.NPC_STRONGLYCONNECTEDCOMPONENTS.Solvers;

class KosarajuSolver : ISolver<STRONGLYCONNECTEDCOMPONENTS> {
    public string solverName { get; } = "Kosaraju's Algorithm";

    public string solverDefinition { get; } =
        "Finds strongly connected components in a directed graph using two depth-first searches: one on the original graph and one on the reversed graph.";

    public string source { get; } =
        "Swati Dhingra, Poorvi S. Dodwad, and Meghna Madan, \"Finding Strongly Connected Components in a Social Network Graph,\" International Journal of Computer Applications, Volume 136, No. 7, February 2016. Section 4.4 describes Kosaraju's algorithm.";

    public string sourceLink { get; } =
        "https://ijcaonline.org/research/volume136/number7/dhingra-2016-ijca-908481.pdf";

    public string[] contributors { get; } = { "Surendra Thapa", "Rohan Shrestha" };

    public bool timerHasExpired { get; set; }
    // Declared, not derived. Runs two depth-first-search passes (forward, then reversed graph).
    public SolverType solverType { get; } = SolverType.DepthFirstSearch;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
    // Builds proper adjacency and reverse-adjacency dictionaries (O(V + E)) and runs two DFS
    // passes over them, each O(V + E) — the standard bound, no linear edge-list rescans.
    public string complexity { get; } = "O(V + E)";

    public string solve(STRONGLYCONNECTEDCOMPONENTS problem) {
        var nodes = problem.graph.Nodes.ToList().Select(n => n.ToString()).Distinct().ToList();
        var edges = problem.graph.Edges.ToList();

        var adj = nodes.ToDictionary(n => n, n => new List<string>());
        var rev = nodes.ToDictionary(n => n, n => new List<string>());

        foreach (var edge in edges) {
            string from = edge[0].ToString();
            string to = edge[1].ToString();

            if (!adj.ContainsKey(from)) adj[from] = new List<string>();
            if (!adj.ContainsKey(to)) adj[to] = new List<string>();
            if (!rev.ContainsKey(from)) rev[from] = new List<string>();
            if (!rev.ContainsKey(to)) rev[to] = new List<string>();

            adj[from].Add(to);
            rev[to].Add(from);
        }

        var visited = new HashSet<string>();
        var order = new Stack<string>();

        foreach (var node in nodes) {
            if (!visited.Contains(node))
                FirstDfs(node, adj, visited, order);
        }

        visited.Clear();
        var components = new List<List<string>>();

        while (order.Count > 0) {
            var node = order.Pop();

            if (!visited.Contains(node)) {
                var component = new List<string>();
                SecondDfs(node, rev, visited, component);
                components.Add(component.OrderBy(x => x).ToList());
            }
        }

        components = components.OrderBy(c => c.First()).ToList();

        return "{" + string.Join(",", components.Select(c => "{" + string.Join(",", c) + "}")) + "}";
    }

    private static void FirstDfs(
        string node,
        Dictionary<string, List<string>> adj,
        HashSet<string> visited,
        Stack<string> order) {
        visited.Add(node);

        foreach (var next in adj[node]) {
            if (!visited.Contains(next))
                FirstDfs(next, adj, visited, order);
        }

        order.Push(node);
    }

    private static void SecondDfs(
        string node,
        Dictionary<string, List<string>> rev,
        HashSet<string> visited,
        List<string> component) {
        visited.Add(node);
        component.Add(node);

        foreach (var next in rev[node]) {
            if (!visited.Contains(next))
                SecondDfs(next, rev, visited, component);
        }
    }
}
