using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using System.Linq;

namespace API.Problems.NPComplete.NPC_VERTEXCOVER.Solvers;

// The steps Vertex Cover Bounded Search Tree records, for the Graph picture and the Search Tree picture alike, as it
// branches on the two ends of an uncovered edge:
//   - an Accept for each end it takes into the cover (in the tree, a node named after the end, under the end taken
//     before it; the solution when it covers the last edge);
//   - a Reject when the budget runs out with edges still uncovered (that node is pruned, its note the edges left);
//   - a Backtrack when it backs an end out again (that node fails, or stays pruned, with its note, when the budget ran
//     out under it);
//   - the Done, which names the answer's path in the tree, root first, or marks the root failed when there is none.
// In the tree, a node is an end taken into the cover, and its children are the two ends of the next uncovered edge.
class VertexCoverBoundedSearchTree : ISolver<VERTEXCOVER, NodeSet>, IIncrementalSolver, ISearchTreeSolver {

    // --- Fields ---
    public string solverName { get; } = "Vertex Cover Bounded Search Tree";
    public string solverDefinition { get; } = "Recursively branches on a remaining budget k: if no edges"
 + " remain, returns the empty set as a solution; if k has been exhausted and edges remain, returns"
 + " failure for that branch; otherwise selects an uncovered edge (u,v), first recursing with u added"
 + " to the cover and the budget decremented, and if that branch fails, recursing with v added instead."
 + " Returns the first successful branch's cover, or failure if both branches fail, meaning no vertex "
 + " cover of size at most k exists.";
    public string source { get; } = "David S. Johnson. 1974. Approximation algorithms for combinatorial problems. J. Comput. Syst. Sci. 9, 3 (December, 1974), 256–278. https://doi.org/10.1016/S0022-0000(74)80044-9";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://dl.acm.org/doi/10.1145/800125.804034";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Parameterized by the budget K, and search() branches on the two endpoints
    // of an uncovered edge, abandoning a branch once the budget runs out, which is Backtracking.
    public SolverType solverType { get; } = SolverType.Backtracking | SolverType.Parameterized;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    public MathematicalFunction timeComplexity { get; } = new("2^K * (n + m)", ", K = target cover size, n = |nodes|, m = |edges|");

    // --- Methods Including Constructors ---
    public VertexCoverBoundedSearchTree() {

    }

    // A step of the search: the cover built along the current branch, the end just taken or backed out (null: none), and
    // the depth of the search-tree node the step is about (0 for the root, d for the d-th end taken on the branch), left
    // in state Tree. Uncovered is how many edges a branch that ran out of budget leaves.
    private readonly record struct Move(List<string> Chosen, string? Node, int Depth, SearchNodeState Tree, int Uncovered = 0);

    // How the steps show a move. The search keeps its own tree (a "start" root, a node failed when it is backed out), so it
    // gives RunSteps the tree node of each step rather than using SearchSteps.
    private static RunSteps<Move, NodeSet> NewSteps(StepRecorder<NodeSet> rec, DepthFirstTree tree) => new(rec,
        // What every picture shows: the cover so far, and the node the step is about.
        partial: m => new NodeSet(m.Chosen),
        focus: m => m.Node == null ? [] : [m.Node],
        // The Search Tree picture's node only: the end taken ("a"), with the edges left uncovered as the note of a branch
        // that ran out of budget.
        tree: m => tree.Step(m.Depth, m.Node ?? (m.Depth == 0 ? "" : m.Chosen[m.Depth - 1]), m.Tree,
            m.Uncovered > 0 ? $"{Words.Count(m.Uncovered, "edge")} uncovered" : null));

    public string solve(VERTEXCOVER G) => Solve(G, StepRecorder<NodeSet>.Off);

    public string Solve(VERTEXCOVER G, StepRecorder<NodeSet> rec) {
        var edges = new List<KeyValuePair<string, string>>(G.edges);
        // Each end taken covers at least one more edge, so a branch is never deeper than K or the number of edges.
        var tree = new DepthFirstTree(Math.Max(0, Math.Min(G.K, edges.Count)));
        var steps = NewSteps(rec, tree);
        var chosen = new List<string>();
        var result = search(edges, G.K, chosen, tree, steps);
        if (timerHasExpired) return SolveRun.TimedOutAnswer;
        if (result == null) {
            steps.Done(new NodeSet([]), false, $"No vertex cover of size {G.K} or less exists.", tree: () => tree.NoAnswer());
            return "{}";
        }
        steps.Done(new NodeSet(result), true, $"{GraphSubsetFrames.Braces(result)} covers every edge, within K = {G.K}.",
            tree: () => tree.AnswerPath(chosen));
        return "{" + string.Join(",", result) + "}";
    }

    // Every cover has one end of every edge, so for the first uncovered edge (u,v): take u and search on with one less
    // budget; if that fails, take v instead. 'chosen' is the cover built along the current branch (a stack); the
    // returned list is built on the way back out.
    private List<string>? search(List<KeyValuePair<string, string>> edges, int k, List<string> chosen, DepthFirstTree tree,
        RunSteps<Move, NodeSet> steps) {
        // At the time limit, unwind as a failed branch; Solve() reports the stop.
        if (timerHasExpired) {
            return null;
        }
        if (edges.Count == 0) {
            return new List<string>();
        }
        if (k <= 0) {
            steps.Reject(new(chosen, null, chosen.Count, SearchNodeState.Pruned, edges.Count), $"Out of budget with {edges.Count} edge{(edges.Count == 1 ? "" : "s")} still uncovered.");
            return null;
        }

        var e = edges[0];

        var withoutU = edges.Where(x => x.Key != e.Key && x.Value != e.Key).ToList();
        chosen.Add(e.Key);
        tree.Branch(chosen.Count);
        steps.Accept(new(chosen, e.Key, chosen.Count, withoutU.Count == 0 ? SearchNodeState.Solution : SearchNodeState.Explored),
            $"Edge {{{e.Key},{e.Value}}} isn't covered. Take {e.Key} ({k - 1} more allowed).");
        var resultU = search(withoutU, k - 1, chosen, tree, steps);
        if (resultU != null) {
            resultU.Add(e.Key);
            return resultU;
        }
        chosen.RemoveAt(chosen.Count - 1);
        // In the tree, the end backed out fails, unless the budget cut it off (the tree keeps it pruned, with its note).
        steps.Backtrack(new(chosen, e.Key, chosen.Count + 1, SearchNodeState.Failed),
            $"Back out {e.Key}: no cover within budget follows from taking it.");

        var withoutV = edges.Where(x => x.Key != e.Value && x.Value != e.Value).ToList();
        chosen.Add(e.Value);
        tree.Branch(chosen.Count);
        steps.Accept(new(chosen, e.Value, chosen.Count, withoutV.Count == 0 ? SearchNodeState.Solution : SearchNodeState.Explored),
            $"Take {e.Value} instead, the other end of {{{e.Key},{e.Value}}} ({k - 1} more allowed).");
        var resultV = search(withoutV, k - 1, chosen, tree, steps);
        if (resultV != null) {
            resultV.Add(e.Value);
            return resultV;
        }
        chosen.RemoveAt(chosen.Count - 1);
        steps.Backtrack(new(chosen, e.Value, chosen.Count + 1, SearchNodeState.Failed),
            $"Back out {e.Value}: neither end of {{{e.Key},{e.Value}}} works here.");

        return null;
    }
}
