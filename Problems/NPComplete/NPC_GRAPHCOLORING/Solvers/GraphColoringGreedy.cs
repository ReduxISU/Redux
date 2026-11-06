using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using System.Linq;
using System.Collections.Generic;

namespace API.Problems.NPComplete.NPC_GRAPHCOLORING.Solvers;

// The steps Graph Coloring Greedy records: one per node, in the graph's order. A node colored with a color under K is an
// Accept; a node that needs color K or more is a Reject (the greedy goes on, as it always does, and gives up at the end).
// Each why names the node's colored neighbours and the colors they use. Then the Done.
class GraphColoringGreedy : ISolver<GRAPHCOLORING, Groups>, IIncrementalSolver {

    public string solverName { get; } = "Graph Coloring Greedy";
    public string solverDefinition { get; } = "A greedy algorithm that iterates through each vertex and assigns the smallest color not used by any of its neighbors.";
    public string source { get; } = "Dasgupta, S, Papadimitriou, C, & Vazirani, U. (2006). Algorithms. McGraw-Hill. Chapter 9.2";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Pramesh Shah" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Irrevocable locally-optimal choice each step.
    public SolverType solverType { get; } = SolverType.Greedy;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
    // Adjacency build + the per-node color assignment loop are the textbook O(n + m) greedy-coloring
    // pass (neighbor scans and usedColors lookups amortize to O(m) and O(n) respectively across all
    // nodes). BuildCertificate adds a factor beyond that textbook bound though: for each of up to
    // numColors (<= n) color groups it does a fresh O(n) nodes.Where(...) scan, i.e. O(n^2) worst case
    // when many colors are used -- so the actual bound is O(n^2 + m), not the source text's O(V + E).
    public MathematicalFunction timeComplexity { get; } = new("n^2 + m", ", n = |nodes|, m = |edges|");

    // Constructor 
    public GraphColoringGreedy() { }

    // Builds certificate string in format {{a,d},{b},{c}}
    private string BuildCertificate(Dictionary<string, int> colorAssignment, List<string> nodes, int numColors) {
        List<string> colorGroups = new List<string>();
        for (int i = 0; i < numColors; i++) {
            List<string> group = nodes.Where(node => colorAssignment[node] == i).ToList();
            colorGroups.Add("{" + string.Join(",", group) + "}");
        }
        return "{" + string.Join(",", colorGroups) + "}";
    }

    // How the steps show the run: the coloring so far, and the node the step colors.
    private static RunSteps<string, Groups> NewSteps(Dictionary<string, int> colorAssignment, StepRecorder<Groups> rec) => new(rec,
        // What every picture shows: the coloring so far, and the node just colored.
        partial: _ => new Groups(colorAssignment),
        focus: node => [node]);

    public string solve(GRAPHCOLORING gColor) => Solve(gColor, StepRecorder<Groups>.Off);

    // Colors the nodes in the graph's order, each with the smallest color none of its colored neighbours uses. The greedy
    // never takes a color back, so a node that needs color K or more is a Reject, and the greedy goes on to the end.
    public string Solve(GRAPHCOLORING gColor, StepRecorder<Groups> rec) {
        List<string> nodes = gColor.nodes;
        List<KeyValuePair<string, string>> edges = gColor.edges;
        Dictionary<string, int> colorAssignment = new Dictionary<string, int>();
        var steps = NewSteps(colorAssignment, rec);

        // Guard, empty graph
        if (nodes == null || nodes.Count == 0) {
            bool ok = false;
            try { ok = gColor.defaultVerifier.verify(gColor, "{}"); } catch { }
            steps.Done(new Groups([]), ok, $"The graph has no nodes, so there is nothing to color.");
            return "{}";
        }

        // Build adjacency list
        Dictionary<string, List<string>> adjacency = new Dictionary<string, List<string>>();
        foreach (string node in nodes)
            adjacency[node] = new List<string>();

        foreach (KeyValuePair<string, string> edge in edges) {
            adjacency[edge.Key].Add(edge.Value);
            adjacency[edge.Value].Add(edge.Key);
        }

        // For each vertex assign smallest color not used by neighbors
        foreach (string node in nodes) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer; // stop at the time limit
            HashSet<int> usedColors = new HashSet<int>();
            foreach (string neighbor in adjacency[node])
                if (colorAssignment.ContainsKey(neighbor))
                    usedColors.Add(colorAssignment[neighbor]);

            int color = 0;
            while (usedColors.Contains(color))
                color++;

            colorAssignment[node] = color;

            // The neighbours that already have a color, by name (for the steps' why).
            List<string> Colored() => adjacency[node].Where(n => n != node && colorAssignment.ContainsKey(n)).Distinct().Order(StringComparer.Ordinal).ToList();
            if (color < gColor.K)
                steps.Accept(node, $"Color node {node} with color {color}: {GraphGroupFrames.NeighboursUse(Colored(), usedColors.Order().ToList(), already: true)}.");
            else if (gColor.K <= 0)
                steps.Reject(node, $"Node {node} needs color {color}, but K = {gColor.K} allows no colors: {GraphGroupFrames.NeighboursUse(Colored(), usedColors.Order().ToList(), already: true)}.");
            else if (gColor.K == 1)
                steps.Reject(node, $"Node {node} needs color {color}, but K = {gColor.K} allows only color 0: {GraphGroupFrames.NeighboursUse(Colored(), usedColors.Order().ToList(), already: true)}.");
            else
                steps.Reject(node, $"Node {node} needs color {color}, but K = {gColor.K} allows only colors 0 to {gColor.K - 1}: {GraphGroupFrames.NeighboursUse(Colored(), usedColors.Order().ToList(), already: true)}.");
        }

        int numColors = colorAssignment.Values.Max() + 1;

        // Guard, greedy used more colors than K allows
        if (numColors > gColor.K) {
            steps.Done(new Groups([]), false, $"Greedy used {numColors} colors, more than K = {gColor.K}. Greedy doesn't always find the fewest colors.");
            return "{}";
        }

        steps.Done(new Groups(colorAssignment), true, $"Every node is colored with {Words.Count(numColors, "color")}, within K = {gColor.K}.");
        return BuildCertificate(colorAssignment, nodes, numColors);
    }
}