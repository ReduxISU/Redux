using API.Interfaces;
using API.Interfaces.Steps;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.Graphs;
using System.Numerics;
using System.Diagnostics;

namespace API.Problems.NPComplete.NPC_CLIQUE.Solvers;

// The steps Carraghan-Pardalos records, as it grows a clique one vertex at a time, for the Graph picture and the Search
// Tree picture alike:
//   - an Accept for each vertex added, saying how many candidates are joined to every member so far; in the tree, a node
//     "3" under the vertex added before it, the solution when it makes the clique K big;
//   - a Reject for a vertex the bound rules out (too few vertices could ever join it): in the tree, a pruned node named
//     after it, noted with the most the clique could reach ("at most 2 < K = 3");
//   - a Backtrack when it backs a vertex out again (that node fails);
//   - the Done, with the clique or none; it names the answer's path in the tree, root first, or marks the root failed.
// In the tree, a node is a vertex added, and its children are the vertices tried after it.
class CarraghanPardalos : ISolver<CLIQUE, NodeSet>, IIncrementalSolver, ISearchTreeSolver {

    // --- Fields ---
    public string solverName { get; } = "Clique Carraghan-Pardalos Branch and Bound";
    public string solverDefinition { get; } = "Orders vertices by ascending degree, then performs a"
    + " depth-first branch-and-bound search: at each node it maintains a partial clique and a candidate"
    + " set (vertices adjacent to all clique members so far). Before recursing on a candidate, it computes"
    + " the induced candidate set restricted to that vertex's neighbors and prunes immediately if the"
    + " partial clique plus the remaining candidates cannot possibly reach size k, avoiding exploration of"
    + " branches that cannot succeed.";
    public string source { get; } = "Randy Carraghan and Panos M. Pardalos. 1990. An exact algorithm for the maximum clique problem. Oper. Res. Lett. 9, 6 (November, 1990), 375–382. https://doi.org/10.1016/0167-6377(90)90057-C";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } =
        "https://doi.org/10.1016/0167-6377(90)90057-C";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public bool timerHasExpired { get; set; }
    public SolverType solverType { get; } = SolverType.Backtracking;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    public MathematicalFunction timeComplexity { get; } = new("2^n", ", n = |nodes|");

    // --- Methods Including Constructors ---
    public CarraghanPardalos() {

    }

    // A step of the search: the clique grown so far, the vertex the step is about, and its search-tree node, at Depth (d
    // for the d-th vertex added on the branch), left in state Tree; Reach is the most the clique could reach, for a vertex
    // the bound rules out.
    private readonly record struct Move(List<string> Clique, string Vertex, int Depth, SearchNodeState Tree, int Reach = 0);

    // How the steps show a move. The search keeps its own tree (a "start" root, a vertex the bound rules out as a pruned
    // node, a vertex failed when it is backed out), so it gives RunSteps the tree node of each step rather than using
    // SearchSteps.
    private static RunSteps<Move, NodeSet> NewSteps(StepRecorder<NodeSet> rec, int K, DepthFirstTree tree) => new(rec,
        // What every picture shows: the clique so far, and the vertex added, skipped or backed out.
        partial: m => new NodeSet(m.Clique),
        focus: m => [m.Vertex],
        // The Search Tree picture's node only: the vertex, noted with the most the clique could reach when the bound rules
        // it out.
        tree: m => tree.Step(m.Depth, m.Vertex, m.Tree, m.Tree == SearchNodeState.Pruned ? $"at most {m.Reach} < K = {K}" : null));

    public string solve(CLIQUE clique) => Solve(clique, StepRecorder<NodeSet>.Off);

    public string Solve(CLIQUE clique, StepRecorder<NodeSet> rec) {
        // A branch adds each vertex at most once.
        var tree = new DepthFirstTree(clique.nodes.Count);
        var steps = NewSteps(rec, clique.K, tree);

        Dictionary<string, HashSet<string>> adj = BuildAdjacency(clique.nodes, clique.edges);

        // 1. Order vertices by ascending degree (per the original paper).
        List<string> order = adj.Keys.OrderBy(v => adj[v].Count).ToList();

        HashSet<string> candidates = new HashSet<string>(order);
        List<string> partial = new List<string>();

        HashSet<string>? found = Expand(candidates, partial, clique.K, adj, tree, steps);
        if (timerHasExpired) return SolveRun.TimedOutAnswer;

        if (found == null) {
            steps.Done(new NodeSet([]), false, $"No clique of size {clique.K} exists.", tree: () => tree.NoAnswer());
            return "{}";
        }
        // The answer's path in the tree: the vertices in the order added.
        steps.Done(new NodeSet(found), true, $"{Words.Braces(found)} is a clique of size {clique.K}.", tree: () => tree.AnswerPath(partial));
        return "{" + string.Join(",", found) + "}";
    }

    private Dictionary<string, HashSet<string>> BuildAdjacency(
        List<string> nodes, List<KeyValuePair<string, string>> edges) {

        Dictionary<string, HashSet<string>> adj = nodes.ToDictionary(n => n, n => new HashSet<string>());
        foreach (KeyValuePair<string, string> e in edges) {
            adj[e.Key].Add(e.Value);
            adj[e.Value].Add(e.Key);
        }
        return adj;
    }

    // Branch-and-bound search: extends 'partial' using vertices from 'candidates', pruning whenever
    // the partial clique plus remaining candidates can no longer reach size k.
    private HashSet<string>? Expand(
        HashSet<string> candidates, List<string> partial, int k, Dictionary<string, HashSet<string>> adj,
        DepthFirstTree tree, RunSteps<Move, NodeSet> steps) {

        if (partial.Count == k) {
            return new HashSet<string>(partial);
        }

        if (partial.Count + candidates.Count < k) {
            return null;
        }

        foreach (string v in candidates.ToList()) {
            if (timerHasExpired) return null; // unwind at the time limit; Solve() returns the timed-out answer

            HashSet<string> newCandidates = new HashSet<string>(
                candidates.Where(u => u != v && adj[v].Contains(u))
            );

            // Bound: skip v if the resulting branch cannot possibly reach size k.
            if (partial.Count + 1 + newCandidates.Count < k) {
                // In the tree, the vertex is a dead end under the vertex added last.
                tree.Branch(partial.Count + 1);
                steps.Reject(new(partial, v, partial.Count + 1, SearchNodeState.Pruned, partial.Count + 1 + newCandidates.Count),
                    $"Skip {v}: with it only {Words.Count(partial.Count + 1 + newCandidates.Count, "vertex", "vertices")} could ever join, but the clique needs {k}.");
                candidates.Remove(v);
                continue;
            }

            partial.Add(v);
            tree.Branch(partial.Count);
            // In the tree, the vertex that makes the clique K big is the solution.
            var added = new Move(partial, v, partial.Count, partial.Count == k ? SearchNodeState.Solution : SearchNodeState.Explored);
            if (newCandidates.Count == 0)
                steps.Accept(added, $"Add {v} to the clique. No vertex is joined to every member so far.");
            else
                steps.Accept(added,
                    $"Add {v} to the clique. {Words.Count(newCandidates.Count, "vertex", "vertices")} {(newCandidates.Count == 1 ? "is" : "are")} joined to every member so far.");
            HashSet<string>? result = Expand(newCandidates, partial, k, adj, tree, steps);
            if (result != null) {
                return result;
            }
            partial.RemoveAt(partial.Count - 1);
            steps.Backtrack(new(partial, v, partial.Count + 1, SearchNodeState.Failed), $"Back out {v}: no clique of size {k} grows from here.");
            candidates.Remove(v);
        }

        return null;
    }
}