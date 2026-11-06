using System.Text;
using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using System.Linq;

namespace API.Problems.NPComplete.NPC_HAMILTONIAN.Solvers;

// The steps Hamiltonian Brute Force records, in the order the search runs:
//   - a Try for each node the tour starts at;
//   - an Accept for each edge the path walks on to a new node;
//   - a Reject for a node the path can't go to next, because there is no edge to it;
//   - a Backtrack when every way on from a node has failed, giving the node up;
//   - for each complete tour, closing edge included, an Accept if the verifier agrees (the run ends there), else a
//     Reject naming the first hop that is not an edge;
//   - the Done, with the cycle or none.
// Despite its name it is a backtracking search: it builds the path one edge at a time and backs out of dead ends, so
// it is an IIncrementalSolver (its steps hold the path so far). It records no search tree, so it uses RunSteps.
class HamiltonianBruteForce : ISolver<HAMILTONIAN, Tour>, IIncrementalSolver {

    // --- Fields ---
    public string solverName { get; } = "Hamiltonian Brute Force";
    public string solverDefinition { get; } = "This is a brute force solver for the NP-Complete Hamiltonian Cycle problem";
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
    // permutation costs O(n) to build (CombinationToCertificate), and HamiltonianVerifier walks it in
    // O(n) steps, each doing two O(m) List<>.Contains edge lookups -- O(n*m) per verify call.
    public MathematicalFunction timeComplexity { get; } = new("n! * n * m", ", n = |nodes|, m = |edges|");

    // --- Methods Including Constructors ---
    public HamiltonianBruteForce() { }


    private string CombinationToCertificate(List<string> combination) {
        var sb = new StringBuilder();
        sb.Append("{");
        foreach (var node in combination)
            sb.Append(node).Append(",");
        sb.Append(combination[0]).Append("}");
        return sb.ToString();
    }

    // Undirected edge check
    private bool HasEdgeUndirected(HAMILTONIAN graph, string nodeA, string nodeB) {
        return graph.edges.Any(e =>
            (e.Key == nodeA && e.Value == nodeB) ||
            (e.Key == nodeB && e.Value == nodeA));
    }

    // How the steps show the search: the path so far (the first Length nodes of the order), and the node the step is
    // about, with the edge it came by when From (the node before it) is given. The two steps about a whole tour show the
    // closed tour and say their own focus.
    private static RunSteps<(int Length, string Node, string? From), Tour> NewSteps(List<string> order, StepRecorder<Tour> rec) => new(rec,
        // What every picture shows: the path so far, and the node (and edge) the step is about.
        partial: s => new Tour(order.Take(s.Length)),
        focus: s => s.From == null ? [s.Node] : [s.Node, GraphTourFrames.EdgeId(false, s.From, s.Node)]);

    public string solve(HAMILTONIAN hamiltonian) => Solve(hamiltonian, StepRecorder<Tour>.Off);

    // The nodes are permuted in a copy, in the order the solver always tried them, so the answer is the same as before
    // and the problem's own node list is left alone.
    public string Solve(HAMILTONIAN hamiltonian, StepRecorder<Tour> rec) {
        var order = new List<string>(hamiltonian.nodes);
        var steps = NewSteps(order, rec);
        int tried = 0;
        string? answer = Search(hamiltonian, order, 0, steps, ref tried);
        if (answer != null) {
            steps.Done(GraphTourFrames.ParseTour(answer), true, $"Found the Hamiltonian cycle {GraphTourFrames.Arrows(GraphTourFrames.ParseTour(answer).Order)} after checking {tried} complete {(tried == 1 ? "tour" : "tours")}.");
            return answer;
        }
        if (timerHasExpired) return SolveRun.TimedOutAnswer; // stopped by the time limit, not exhausted
        if (order.Count == 0)
            steps.Done(new Tour([]), false, $"The graph has no nodes, so there is no cycle.");
        else
            steps.Done(new Tour([]), false, $"No Hamiltonian cycle: every way of walking the edges was tried, and none visits every node once and returns to its start.");
        return "{}";
    }

    // Places order[start], trying each node not yet on the path in turn (by swapping it into place), and goes on only
    // along an edge from the node before it. When one node is left, the whole tour, closing edge included, goes to the
    // verifier. Returns the tour's certificate, or null when no tour follows from the path so far.
    private string? Search(HAMILTONIAN hamiltonian, List<string> order, int start, RunSteps<(int Length, string Node, string? From), Tour> steps, ref int tried) {
        if (start == order.Count - 1) {
            tried++;
            string certificate = CombinationToCertificate(order);
            if (hamiltonian.defaultVerifier.verify(hamiltonian, certificate)) {
                steps.Accept((order.Count, order[0], null),
                    $"Close the tour {order[^1]} → {order[0]}: every node is visited once, so {GraphTourFrames.Arrows(order.Append(order[0]))} is a Hamiltonian cycle.",
                    focus: () => [order[^1], order[0]], partial: () => GraphTourFrames.ParseTour(certificate));
                return certificate;
            }
            // The verifier only says no, so the hop that is not an edge is looked up for the step alone.
            steps.Reject((order.Count, order[0], null),
                $"Try the tour {GraphTourFrames.Arrows(order.Append(order[0]))}: there is no edge between {FirstMissingHop(hamiltonian, order)?.From} and {FirstMissingHop(hamiltonian, order)?.To}.",
                focus: () => FirstMissingHop(hamiltonian, order) is { } hop ? [hop.From, hop.To] : [], partial: () => GraphTourFrames.ParseTour(certificate));
            return null;
        }
        for (int i = start; i < order.Count; i++) {
            if (timerHasExpired) return null; // stop at the time limit (Solve() then reports it)
            (order[start], order[i]) = (order[i], order[start]);
            string node = order[start];

            if (start == 0) {
                steps.Try((1, node, null), $"Start the tour at {node}.");
                string? found = Search(hamiltonian, order, start + 1, steps, ref tried);
                if (found != null) return found;
                steps.Backtrack((0, node, null), $"Every tour that starts at {node} fails.");
            }
            // Early pruning: check the edge between the last node in the partial path and the current node.
            else if (HasEdgeUndirected(hamiltonian, order[start - 1], node)) {
                string previous = order[start - 1];
                steps.Accept((start + 1, node, previous), $"Go {previous} → {node}: the path is {GraphTourFrames.Arrows(order.Take(start + 1))}.");
                string? found = Search(hamiltonian, order, start + 1, steps, ref tried);
                if (found != null) return found;
                steps.Backtrack((start, previous, null), $"Every way on from {node} fails: back up to {previous}.");
            } else {
                string previous = order[start - 1];
                steps.Reject((start + 1, node, null), $"There is no edge between {previous} and {node}, so {node} can't come after {previous}.");
            }

            (order[start], order[i]) = (order[i], order[start]);
        }
        return null;
    }

    // The first step of the closed tour (the order, then back to its first node) that is not an edge, or null.
    private (string From, string To)? FirstMissingHop(HAMILTONIAN hamiltonian, List<string> order) {
        for (int i = 0; i < order.Count; i++) {
            string from = order[i], to = order[(i + 1) % order.Count];
            if (!HasEdgeUndirected(hamiltonian, from, to)) return (from, to);
        }
        return null;
    }
}
