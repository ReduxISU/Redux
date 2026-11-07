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
// it is an IIncrementalSolver (its steps hold the path so far).
// The same steps draw the Search Tree picture. In the tree, a node is a node the path walks to ("3"), under the node
// before it, and its children are the nodes tried next; the root's children are the nodes the tour starts at. A Try's or
// an Accept's node is explored; a node with no edge to it from the one before is pruned ("no edge 2–3"); a node given up
// in a Backtrack fails. The whole tour's step is about its last node: the solution when the verifier agrees, else pruned
// with the first hop that is not an edge. The Done names the answer's path, root first, or marks the root failed.
class HamiltonianBruteForce : ISolver<HAMILTONIAN, Tour>, IIncrementalSolver, ISearchTreeSolver {

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

    // A step of the search: the path so far (the first Length nodes of the order), the node the step is about, with the
    // edge it came by when From (the node before it) is given, and its search-tree node: the node at Depth of the order
    // (order[Depth - 1]; Depth n for a whole tour), left in state Tree.
    private readonly record struct Move(int Length, string Node, string? From, int Depth, SearchNodeState Tree);

    // How the steps show a move. The two steps about a whole tour show the closed tour and say their own focus. The search
    // keeps its own tree (a "start" root, a node with no edge to it as a pruned node, a node failed when it is given up), so
    // it gives RunSteps the tree node of each step rather than using SearchSteps.
    private RunSteps<Move, Tour> NewSteps(HAMILTONIAN hamiltonian, List<string> order, StepRecorder<Tour> rec, DepthFirstTree tree) => new(rec,
        // What every picture shows: the path so far, and the node (and edge) the step is about.
        partial: m => new Tour(order.Take(m.Length)),
        focus: m => m.From == null ? [m.Node] : [m.Node, GraphTourFrames.EdgeId(false, m.From, m.Node)],
        // The Search Tree picture's node only: the node walked to, noted with the hop that is not an edge when it is pruned
        // (for a whole tour, the first such hop).
        tree: m => tree.Step(m.Depth, order[m.Depth - 1], m.Tree, m.Tree != SearchNodeState.Pruned ? null
            : m.Depth < order.Count ? $"no edge {order[m.Depth - 2]}–{order[m.Depth - 1]}"
            : FirstMissingHop(hamiltonian, order) is { } hop ? $"no edge {hop.From}–{hop.To}" : null));

    public string solve(HAMILTONIAN hamiltonian) => Solve(hamiltonian, StepRecorder<Tour>.Off);

    // The nodes are permuted in a copy, in the order the solver always tried them, so the answer is the same as before
    // and the problem's own node list is left alone.
    public string Solve(HAMILTONIAN hamiltonian, StepRecorder<Tour> rec) {
        var order = new List<string>(hamiltonian.nodes);
        // A branch places each node once.
        var tree = new DepthFirstTree(order.Count);
        var steps = NewSteps(hamiltonian, order, rec, tree);
        int tried = 0;
        string? answer = Search(hamiltonian, order, 0, tree, steps, ref tried);
        if (answer != null) {
            // The answer's path in the tree: the nodes in the order the tour visits them (Search leaves the order so).
            steps.Done(GraphTourFrames.ParseTour(answer), true, $"Found the Hamiltonian cycle {GraphTourFrames.Arrows(GraphTourFrames.ParseTour(answer).Order)} after checking {tried} complete {(tried == 1 ? "tour" : "tours")}.",
                tree: () => tree.AnswerPath(order));
            return answer;
        }
        if (timerHasExpired) return SolveRun.TimedOutAnswer; // stopped by the time limit, not exhausted
        if (order.Count == 0)
            steps.Done(new Tour([]), false, $"The graph has no nodes, so there is no cycle.");
        else
            steps.Done(new Tour([]), false, $"No Hamiltonian cycle: every way of walking the edges was tried, and none visits every node once and returns to its start.",
                tree: () => tree.NoAnswer());
        return "{}";
    }

    // Places order[start], trying each node not yet on the path in turn (by swapping it into place), and goes on only
    // along an edge from the node before it. When one node is left, the whole tour, closing edge included, goes to the
    // verifier. Returns the tour's certificate, or null when no tour follows from the path so far.
    private string? Search(HAMILTONIAN hamiltonian, List<string> order, int start, DepthFirstTree tree, RunSteps<Move, Tour> steps, ref int tried) {
        if (start == order.Count - 1) {
            tried++;
            string certificate = CombinationToCertificate(order);
            // In the tree, the whole tour is its last node: the solution, or pruned at the hop that is not an edge.
            tree.Branch(order.Count);
            if (hamiltonian.defaultVerifier.verify(hamiltonian, certificate)) {
                steps.Accept(new(order.Count, order[0], null, order.Count, SearchNodeState.Solution),
                    $"Close the tour {order[^1]} → {order[0]}: every node is visited once, so {GraphTourFrames.Arrows(order.Append(order[0]))} is a Hamiltonian cycle.",
                    focus: () => [order[^1], order[0]], partial: () => GraphTourFrames.ParseTour(certificate));
                return certificate;
            }
            // The verifier only says no, so the hop that is not an edge is looked up for the step alone.
            steps.Reject(new(order.Count, order[0], null, order.Count, SearchNodeState.Pruned),
                $"Try the tour {GraphTourFrames.Arrows(order.Append(order[0]))}: there is no edge between {FirstMissingHop(hamiltonian, order)?.From} and {FirstMissingHop(hamiltonian, order)?.To}.",
                focus: () => FirstMissingHop(hamiltonian, order) is { } hop ? [hop.From, hop.To] : [], partial: () => GraphTourFrames.ParseTour(certificate));
            return null;
        }
        for (int i = start; i < order.Count; i++) {
            if (timerHasExpired) return null; // stop at the time limit (Solve() then reports it)
            (order[start], order[i]) = (order[i], order[start]);
            string node = order[start];

            tree.Branch(start + 1);
            if (start == 0) {
                steps.Try(new(1, node, null, 1, SearchNodeState.Explored), $"Start the tour at {node}.");
                string? found = Search(hamiltonian, order, start + 1, tree, steps, ref tried);
                if (found != null) return found;
                steps.Backtrack(new(0, node, null, 1, SearchNodeState.Failed), $"Every tour that starts at {node} fails.");
            }
            // Early pruning: check the edge between the last node in the partial path and the current node.
            else if (HasEdgeUndirected(hamiltonian, order[start - 1], node)) {
                string previous = order[start - 1];
                steps.Accept(new(start + 1, node, previous, start + 1, SearchNodeState.Explored), $"Go {previous} → {node}: the path is {GraphTourFrames.Arrows(order.Take(start + 1))}.");
                string? found = Search(hamiltonian, order, start + 1, tree, steps, ref tried);
                if (found != null) return found;
                steps.Backtrack(new(start, previous, null, start + 1, SearchNodeState.Failed), $"Every way on from {node} fails: back up to {previous}.");
            } else {
                string previous = order[start - 1];
                // In the tree, the node is a dead end under the node before it.
                steps.Reject(new(start + 1, node, null, start + 1, SearchNodeState.Pruned), $"There is no edge between {previous} and {node}, so {node} can't come after {previous}.");
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
