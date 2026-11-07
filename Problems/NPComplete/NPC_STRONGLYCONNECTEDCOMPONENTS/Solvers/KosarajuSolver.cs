using System.Collections.Generic;
using System.Linq;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_STRONGLYCONNECTEDCOMPONENTS;

namespace API.Problems.NPComplete.NPC_STRONGLYCONNECTEDCOMPONENTS.Solvers;

// The steps Kosaraju's Algorithm records, for the Components picture:
//   - pass 1 (a depth-first search on the graph): an Update when a node is visited and one when it is finished, with its
//     finish number;
//   - pass 2 (depth-first searches on the reversed graph, in falling finish order): an Accept when a node starts a new
//     component, and an Accept per node that joins it along an edge followed backward;
//   - the Done, listing the components.
// Pass 1 only orders the nodes, so its steps are Updates; pass 2 puts each node in its component for good.
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
    public MathematicalFunction timeComplexity { get; } = new("V + E", "");

    // How the steps show a node: the passes so far, and the node, with the edge it was reached along in pass 2 (Via, the
    // node it leads to; null for none).
    private static RunSteps<(string Node, string? Via), Components> NewSteps(StepRecorder<Components> rec, Dictionary<string, int> of,
                                                                           List<string> seen, List<string> finished) => new(rec,
        // What every picture shows: each node's component so far, the nodes pass 1 has visited and the order it finished
        // them in, and the node with its edge.
        partial: _ => new Components(of, seen, finished),
        focus: s => s.Via == null ? [LayeredFrames.NodeId(s.Node)] : [LayeredFrames.NodeId(s.Node), LayeredFrames.ArcId(s.Node, s.Via)]);

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
        var steps = NewSteps(rec, of, seen, finished);

        var visited = new HashSet<string>();
        var order = new Stack<string>();

        foreach (var node in nodes) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer; // stop at the time limit
            if (!visited.Contains(node))
                FirstDfs(node, adj, visited, order, steps, seen, finished);
        }

        visited.Clear();
        var components = new List<List<string>>();

        while (order.Count > 0) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer;
            var node = order.Pop();

            if (!visited.Contains(node)) {
                var component = new List<string>();
                int g = components.Count;
                of[node] = g;
                steps.Accept((node, null), $"In pass 2, {node} has the highest finish number left, so it starts component {g + 1}.");
                SecondDfs(node, rev, visited, component, steps, of, g);
                components.Add(component.OrderBy(x => x).ToList());
            }
        }

        components = components.OrderBy(c => c.First()).ToList();

        string answer = "{" + string.Join(",", components.Select(c => "{" + string.Join(",", c) + "}")) + "}";
        steps.Done(new Components(of, seen, finished), true, $"{Words.Count(components.Count, "strongly connected component")}: {string.Join(", ", components.Select(c => Words.Braces(c)))}.");
        return answer;
    }

    // Pass 1: a depth-first search from node; a node is finished (pushed on the order) once every node it leads to is.
    private static void FirstDfs(
        string node,
        Dictionary<string, List<string>> adj,
        HashSet<string> visited,
        Stack<string> order,
        RunSteps<(string Node, string? Via), Components> steps,
        List<string> seen,
        List<string> finished) {
        visited.Add(node);
        seen.Add(node);
        steps.Update((node, null), $"In pass 1, visit {node}.");

        foreach (var next in adj[node]) {
            if (!visited.Contains(next))
                FirstDfs(next, adj, visited, order, steps, seen, finished);
        }

        order.Push(node);
        finished.Add(node);
        steps.Update((node, null), $"In pass 1, {node} is finished (number {finished.Count}).");
    }

    // Pass 2: a depth-first search from node on the reversed graph; every node it reaches that has no component yet joins
    // component g.
    private static void SecondDfs(
        string node,
        Dictionary<string, List<string>> rev,
        HashSet<string> visited,
        List<string> component,
        RunSteps<(string Node, string? Via), Components> steps,
        Dictionary<string, int> of,
        int g) {
        visited.Add(node);
        component.Add(node);

        foreach (var next in rev[node]) {
            if (!visited.Contains(next)) {
                of[next] = g;
                steps.Accept((next, node), $"In pass 2, follow the edge {next} → {node} backward, so {next} joins component {g + 1}.");
                SecondDfs(next, rev, visited, component, steps, of, g);
            }
        }
    }
}
