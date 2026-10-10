using System.Text;
using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using System.Linq;

namespace API.Problems.NPComplete.NPC_DIRECTEDHAMILTONIAN.Solvers;

// Despite its name it is a backtracking search: it builds the path one edge at a time and backs out of dead ends, so
// it is an IIncrementalSolver (its steps hold the path so far).
class DirectedHamiltonianBruteForce : ISolver<DIRECTEDHAMILTONIAN, Tour>, IIncrementalSolver {

    // --- Fields ---
    public string solverName { get; } = "Directed Hamiltonian Brute Force";
    public string solverDefinition { get; } = "This is a brute force solver for the NP-Complete Directed Hamiltonian Cycle problem";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Does real (if minor) pruning, but tagged BruteForce per its class name
    // and because the pruning is incidental, not the algorithm's defining feature -- contrast with
    // the Backtracking cluster, where pruning/bounding IS the defining feature. Factorial: enumerates
    // permutations of vertices.
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Factorial;
    // Search's edge check only trims a branch once an edge is missing; on a dense/complete
    // graph (the worst case) it never trips, so all n! permutations are still generated. Each yielded
    // permutation costs O(n) to build (CombinationToCertificate), and DirectedHamiltonianVerifier walks
    // it in O(n) steps, each doing an O(m) List<>.Contains edge lookup -- O(n*m) per verify call.
    public string complexity { get; } = "O(n! * n * m), n = |nodes|, m = |edges|";

    // --- Methods Including Constructors ---

    public DirectedHamiltonianBruteForce() { }

    private string CombinationToCertificate(List<string> combination) {
        var sb = new StringBuilder();
        sb.Append("{");
        foreach (var node in combination)
            sb.Append(node).Append(",");
        sb.Append(combination[0]).Append("}");
        return sb.ToString();
    }

    private bool HasEdge(DIRECTEDHAMILTONIAN graph, string fromNode, string toNode) {
        return graph.edges.Any(e => e.Key == fromNode && e.Value == toNode);
    }

    // Steps: the search in the order it runs. Start (Try) picks the first node; Accept walks one edge on; Reject is a node
    // the path cannot go to (no such edge: the partial includes it, so the missing edge is drawn); Backtrack gives a node
    // up after every way on from it failed. When the last node is placed the whole tour, closing edge included, is checked
    // once: Accept if the verifier agrees (and the run ends), else Reject naming the first missing edge. The nodes are
    // permuted in a copy, in the order the solver always tried them, so the answer is the same as before and the problem's
    // own node list is left alone. Recorder state lives only in this call.
    public string solve(DIRECTEDHAMILTONIAN directedhamiltonian) => Solve(directedhamiltonian, StepRecorder<Tour>.Off);

    public string Solve(DIRECTEDHAMILTONIAN directedhamiltonian, StepRecorder<Tour> rec) {
        var order = new List<string>(directedhamiltonian.nodes);
        int tried = 0;
        string? answer = Search(directedhamiltonian, order, 0, rec, ref tried);
        if (answer != null) {
            rec.Done(GraphTourFrames.ParseTour(answer), true, $"Found the Hamiltonian cycle {GraphTourFrames.Arrows(GraphTourFrames.ParseTour(answer).Order)} after checking {tried} complete {(tried == 1 ? "tour" : "tours")}.");
            return answer;
        }
        rec.Done(new Tour([]), false, order.Count == 0
            ? "The graph has no nodes, so there is no cycle."
            : "No Hamiltonian cycle: every way of walking the edges was tried, and none visits every node once and returns to its start.");
        return "{}";
    }

    private string? Search(DIRECTEDHAMILTONIAN directedhamiltonian, List<string> order, int start, StepRecorder<Tour> rec, ref int tried) {
        if (start == order.Count - 1) {
            tried++;
            string certificate = CombinationToCertificate(order);
            if (directedhamiltonian.defaultVerifier.verify(directedhamiltonian, certificate)) {
                rec.Accept(() => GraphTourFrames.ParseTour(certificate),
                    () => $"Close the tour {order[^1]} → {order[0]}: every node is visited once, so {GraphTourFrames.Arrows(order.Append(order[0]))} is a Hamiltonian cycle.",
                    order[^1], order[0]);
                return certificate;
            }
            rec.Reject(() => GraphTourFrames.ParseTour(certificate),
                () => $"Try the tour {GraphTourFrames.Arrows(order.Append(order[0]))}: there is no edge from {FirstMissingHop(directedhamiltonian, order)?.From} to {FirstMissingHop(directedhamiltonian, order)?.To}.",
                () => FirstMissingHop(directedhamiltonian, order) is { } hop ? new[] { hop.From, hop.To } : Array.Empty<string>());
            return null;
        }
        for (int i = start; i < order.Count; i++) {
            (order[start], order[i]) = (order[i], order[start]);
            string node = order[start];

            if (start == 0) {
                rec.Try(() => new Tour([node]), () => $"Start the tour at {node}.", node);
                string? found = Search(directedhamiltonian, order, start + 1, rec, ref tried);
                if (found != null) return found;
                rec.Backtrack(() => new Tour([]), () => $"Every tour that starts at {node} fails.", node);
            }
            // Early pruning: check the edge between the last node in the partial path and the current node.
            else if (HasEdge(directedhamiltonian, order[start - 1], node)) {
                string previous = order[start - 1];
                rec.Accept(() => new Tour(order.Take(start + 1)), () => $"Go {previous} → {node}: the path is {GraphTourFrames.Arrows(order.Take(start + 1))}.", node, GraphTourFrames.EdgeId(true, previous, node));
                string? found = Search(directedhamiltonian, order, start + 1, rec, ref tried);
                if (found != null) return found;
                rec.Backtrack(() => new Tour(order.Take(start)), () => $"Every way on from {node} fails: back up to {previous}.", previous);
            } else {
                string previous = order[start - 1];
                rec.Reject(() => new Tour(order.Take(start + 1)), () => $"There is no edge from {previous} to {node}, so {node} can't come after {previous}.", node);
            }

            (order[start], order[i]) = (order[i], order[start]);
        }
        return null;
    }

    // The first step of the closed tour (the order, then back to its first node) that is not an edge, or null.
    private (string From, string To)? FirstMissingHop(DIRECTEDHAMILTONIAN directedhamiltonian, List<string> order) {
        for (int i = 0; i < order.Count; i++) {
            string from = order[i], to = order[(i + 1) % order.Count];
            if (!HasEdge(directedhamiltonian, from, to)) return (from, to);
        }
        return null;
    }
}
