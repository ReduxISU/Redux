using System.Collections.Generic;
using System.Linq;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_STRONGLYCONNECTEDCOMPONENTS;

namespace API.Problems.NPComplete.NPC_STRONGLYCONNECTEDCOMPONENTS.Solvers;

// Incremental: the second pass commits one component at a time, and a node never leaves its component.
class KosarajuSolver : ISolver<STRONGLYCONNECTEDCOMPONENTS, Components>, IIncrementalSolver {
    public string solverName { get; } = "Kosaraju's Algorithm";

    public string solverDefinition { get; } =
        "Finds strongly connected components in a directed graph using two depth-first searches: one on the original graph and one on the reversed graph.";

    public string source { get; } =
        "Swati Dhingra, Poorvi S. Dodwad, and Meghna Madan, \"Finding Strongly Connected Components in a Social Network Graph,\" International Journal of Computer Applications, Volume 136, No. 7, February 2016. Section 4.4 describes Kosaraju's algorithm.";
    public string sourceFile { get; } = SourceFile.Path();

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

    public string solve(STRONGLYCONNECTEDCOMPONENTS problem) => Solve(problem, StepRecorder<Components>.Off);

    public string Solve(STRONGLYCONNECTEDCOMPONENTS problem, StepRecorder<Components> rec) {
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

        // What the steps show: the nodes the first pass has visited, the order it finished them in, and each node's component.
        var seen = new List<string>();
        var finished = new List<string>();
        var of = new Dictionary<string, int>();
        Components Partial() => new(of, seen, finished);

        var visited = new HashSet<string>();
        var order = new Stack<string>();

        foreach (var node in nodes) {
            if (!visited.Contains(node))
                FirstDfs(node, adj, visited, order, rec, seen, finished, Partial);
        }

        visited.Clear();
        var components = new List<List<string>>();

        while (order.Count > 0) {
            var node = order.Pop();

            if (!visited.Contains(node)) {
                var component = new List<string>();
                int g = components.Count;
                of[node] = g;
                rec.Accept(Partial,
                    () => $"Pass 2: {node} has the highest finish number left, so it starts component {g + 1}.",
                    LayeredFrames.NodeId(node));
                SecondDfs(node, rev, visited, component, rec, of, g, Partial);
                components.Add(component.OrderBy(x => x).ToList());
            }
        }

        components = components.OrderBy(c => c.First()).ToList();

        string answer = "{" + string.Join(",", components.Select(c => "{" + string.Join(",", c) + "}")) + "}";
        rec.Done(Partial(), true, $"{components.Count} strongly connected {(components.Count == 1 ? "component" : "components")}: "
            + string.Join(", ", components.Select(c => LayeredFrames.Braces(c))) + ".");
        return answer;
    }

    private static void FirstDfs(
        string node,
        Dictionary<string, List<string>> adj,
        HashSet<string> visited,
        Stack<string> order,
        StepRecorder<Components> rec,
        List<string> seen,
        List<string> finished,
        Func<Components> partial) {
        visited.Add(node);
        seen.Add(node);
        rec.Try(partial, () => $"Pass 1: visit {node}.", LayeredFrames.NodeId(node));

        foreach (var next in adj[node]) {
            if (!visited.Contains(next))
                FirstDfs(next, adj, visited, order, rec, seen, finished, partial);
        }

        order.Push(node);
        finished.Add(node);
        rec.Try(partial, () => $"Pass 1: {node} is finished (number {finished.Count}).", LayeredFrames.NodeId(node));
    }

    private static void SecondDfs(
        string node,
        Dictionary<string, List<string>> rev,
        HashSet<string> visited,
        List<string> component,
        StepRecorder<Components> rec,
        Dictionary<string, int> of,
        int g,
        Func<Components> partial) {
        visited.Add(node);
        component.Add(node);

        foreach (var next in rev[node]) {
            if (!visited.Contains(next)) {
                of[next] = g;
                rec.Accept(partial,
                    () => $"Pass 2: follow the edge {next} → {node} backward, so {next} joins component {g + 1}.",
                    LayeredFrames.NodeId(next), LayeredFrames.ArcId(next, node));
                SecondDfs(next, rev, visited, component, rec, of, g, partial);
            }
        }
    }
}
