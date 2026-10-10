using System.Collections.Generic;
using System.Linq;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_MINIMUMSPANNINGTREE;

namespace API.Problems.NPComplete.NPC_MINIMUMSPANNINGTREE.Solvers;

class PrimSolver : ISolver<MINIMUMSPANNINGTREE, EdgeSet>, IIncrementalSolver {
    public string solverName { get; } = "Prim's Algorithm";
    public string solverDefinition { get; } = "Finds a minimum spanning tree by repeatedly adding the lowest-weight edge that connects the growing tree to a new vertex.";
    public string source { get; } = "Prim, R. C. \"Shortest connection networks and some generalizations.\" Bell System Technical Journal 36, no. 6 (1957): 1389-1401.";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://doi.org/10.1002/j.1538-7305.1957.tb01515.x";
    public string[] contributors { get; } = { "Val Kimbrough" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Repeatedly adds the lowest-weight edge connecting the growing
    // tree to a new vertex -- an irrevocable, locally-optimal choice at each step.
    public SolverType solverType { get; } = SolverType.Greedy;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
    // Does NOT maintain a running frontier/priority queue of best-known edges. Each of the
    // V-1 while-loop iterations recomputes candidateEdges from scratch via
    // visited.SelectMany(node => adjacency[node]) — up to O(E) edges — and re-sorts that
    // whole list with OrderBy (O(E log E)) just to pick the minimum. That's O(V) iterations
    // times O(E log E) per iteration, not the textbook O(E log V) (heap) or O(V^2) (array).
    public string complexity { get; } = "O(V * E log E)";

    public string solve(MINIMUMSPANNINGTREE problem) => Solve(problem, StepRecorder<EdgeSet>.Off);

    // Steps: start at the first node, then one Accept per edge added to the growing tree.
    public string Solve(MINIMUMSPANNINGTREE problem, StepRecorder<EdgeSet> rec) {
        // Sort once so equal-weight instances still produce deterministic certificates.
        List<string> nodes = problem.graph.Nodes.ToList().Select(n => n.ToString()).Distinct().OrderBy(n => n).ToList();
        var edges = KruskalSolver.ExtractEdges(problem.graph);

        if (nodes.Count == 0) {
            rec.Done(new EdgeSet([]), true, "There are no nodes, so there is nothing to connect.");
            return "{}";
        }

        var adjacency = BuildAdjacency(nodes, edges);
        var visited = new HashSet<string> { nodes[0] };
        var selected = new List<(string u, string v, int weight)>();
        EdgeSet Chosen() => new(selected.Select(e => GraphSubsetFrames.EdgeId(e.u, e.v)));
        rec.Try(Chosen, () => $"Start the tree at node {nodes[0]}.", nodes[0]);

        while (selected.Count < nodes.Count - 1) {
            if (timerHasExpired)
                return "{}";

            var candidateEdges = visited
                .SelectMany(node => adjacency[node])
                .Where(edge => !visited.Contains(edge.to))
                .ToList();

            if (candidateEdges.Count == 0) {
                rec.Done(new EdgeSet([]), false, "No edge leaves the tree, so the graph is in more than one piece and has no spanning tree.");
                return "{}";
            }

            // Tie-break lexicographically so the API returns stable output when multiple MSTs exist.
            var nextEdge = candidateEdges
                .OrderBy(edge => edge.weight)
                .ThenBy(edge => KruskalSolver.CanonicalKey(edge.from, edge.to))
                .ThenBy(edge => edge.to)
                .First();

            selected.Add((nextEdge.from, nextEdge.to, nextEdge.weight));
            visited.Add(nextEdge.to);
            rec.Accept(Chosen,
                () => $"Edge {GraphSubsetFrames.EdgeId(nextEdge.from, nextEdge.to)} is the lightest edge leaving the tree, so add it (weight {nextEdge.weight}). Node {nextEdge.to} joins.",
                GraphSubsetFrames.EdgeId(nextEdge.from, nextEdge.to));
        }

        rec.Done(Chosen(), true, selected.Count == 0
            ? "A single node needs no edges."
            : $"The tree uses {selected.Count} edges with total weight {selected.Sum(e => e.weight)}.");
        return KruskalSolver.EdgeListToCertificate(selected);
    }

    private static Dictionary<string, List<(string from, string to, int weight)>> BuildAdjacency(
        List<string> nodes,
        List<(string u, string v, int weight)> edges) {
        var adjacency = nodes.ToDictionary(node => node, _ => new List<(string from, string to, int weight)>());

        foreach (var edge in edges) {
            if (edge.u == edge.v || !adjacency.ContainsKey(edge.u) || !adjacency.ContainsKey(edge.v))
                continue;

            // Store each undirected edge in both directions so frontier scans stay local to visited nodes.
            adjacency[edge.u].Add((edge.u, edge.v, edge.weight));
            adjacency[edge.v].Add((edge.v, edge.u, edge.weight));
        }

        return adjacency;
    }
}
