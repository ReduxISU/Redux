using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_DOMINATINGSET.Solvers;

// The steps Forced-Vertex Branch-and-Reduce records:
//   - an Accept for each forced vertex (one with no neighbours, which only it can dominate);
//   - a Try for the undominated vertex it branches on, then an Accept for each vertex of its closed neighbourhood taken;
//   - a Backtrack when it backs a taken vertex out again;
//   - a Reject when a branch runs out of picks with vertices still undominated;
//   - the Done, with the dominating set or none.
// The same steps draw the Search Tree picture. In the tree, a node is a vertex picked ("3"), under the pick before it. A
// forced pick ("3 (forced)") is the one child of the pick before it; after the forced picks, the children of a pick are
// the vertices that can dominate the undominated vertex it branches on (the Try, which leaves its node as it is). An
// Accept's node is explored, or the solution when the pick dominates every vertex within K; a pick past K is cut off
// (pruned, "over K = 2"), and so is a pick that leaves no picks with vertices still undominated ("2 vertices not
// dominated"); a cut pick stays cut when it is backed out, and any other pick backed out fails, as do forced picks that
// led nowhere. The Done names the answer's path, root first, or marks the root failed (and the set found, when it did not
// verify).
class DominatingSetForcedVertex : ISolver<DOMINATINGSET, NodeSet>, IIncrementalSolver, ISearchTreeSolver {
    // --- Fields ---
    public string solverName { get; } = "Forced-Vertex Branch-and-Reduce Dominating Set Solver";
    public string solverDefinition { get; } =
        "Exact branching search for a dominating set of size <= K. Repeatedly applies a reduction rule:"
        + " any undominated vertex with no neighbors (degree 0) must be in the solution, so it is picked"
        + " immediately without branching. Once no forced vertices remain, branches on the closed"
        + " neighborhood of the highest-degree undominated vertex, trying each neighbor as the next pick."
        + " Terminates successfully when all vertices are dominated, and fails a branch once K picks are"
        + " exhausted without full domination.";
    public string source { get; } =
        "Fomin, F. V., Grandoni, F., & Kratsch, D. (2009). A measure & conquer approach for the analysis of exact algorithms. Journal of the ACM (JACM), 56(5), 1–32.";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://dl.acm.org/doi/abs/10.1145/1552285.1552286";
    public string[] contributors { get; } = { "Quinton Smith" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Exact search WITH pruning/bounding -- distinct from an unpruned
    // brute-force enumeration.
    public SolverType solverType { get; } = SolverType.Backtracking;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // Declared, not derived. Worst case (forced-vertex reduction never fires): recursion
    // depth is bounded by K, and SearchExact branches over closed[uPick], whose size is
    // bounded by n; each recursive call does O(n) work (AllDominated/forced-vertex scan/
    // ApplyPick). That's O(n^K) leaves at O(n) work apiece. The branch-and-reduce pruning
    // (Akiba & Iwata) makes this far faster in practice -- this bound is worst-case only.
    public MathematicalFunction timeComplexity { get; } = new("n^(K+1)", ", n = |nodes|, K = target dominating-set size");

    // --- Methods Including Constructors ---
    public DominatingSetForcedVertex() { }

    // How the steps show the search: the vertex indices picked along the current branch, the vertex the step is about (-1:
    // none), and the state the step leaves its search-tree node in. The search keeps its own tree (a "start" root, a pick
    // cut off as a pruned node, a pick failed when it is backed out), so it gives RunSteps the tree node of each step rather
    // than using SearchSteps.
    private static RunSteps<(List<int> Chosen, int Vertex, SearchNodeState Tree), NodeSet> NewSteps(StepRecorder<NodeSet> rec, List<string> names,
        int K, List<int>[] adj, DepthFirstTree tree) => new(rec,
        // What every picture shows: the picks so far, and the vertex taken, branched on or backed out.
        partial: s => new NodeSet(s.Chosen.Select(c => names[c])),
        focus: s => s.Vertex < 0 ? [] : [names[s.Vertex]],
        // The Search Tree picture's node only.
        tree: s => TreeNode(s.Chosen, s.Vertex, s.Tree, names, K, adj, tree));

    // The search-tree node of a step. The d-th pick on a branch is the node at depth d (the root is at depth 0). A Backtrack
    // is about the pick it backs out, one deeper than the picks left; any other step is about the newest pick (the root,
    // before the first).
    private static SearchNode[] TreeNode(List<int> chosen, int v, SearchNodeState state, List<string> names, int K, List<int>[] adj, DepthFirstTree tree) {
        bool backedOut = state == SearchNodeState.Failed;
        int depth = backedOut ? chosen.Count + 1 : chosen.Count;
        string label = depth == 0 ? "" : Pick(names, adj, backedOut ? v : chosen[depth - 1]);
        // A pick past K is cut off; one that dominates every vertex within K is the answer, where the search stops.
        if (state == SearchNodeState.Explored && chosen.Count > K)
            state = SearchNodeState.Pruned;
        else if (state == SearchNodeState.Explored && depth > 0 && Undominated(chosen, adj) == 0)
            state = SearchNodeState.Solution;
        string? note = state != SearchNodeState.Pruned ? null
            : chosen.Count > K ? $"over K = {K}"
            : $"{Words.Count(Undominated(chosen, adj), "vertex", "vertices")} not dominated";
        return tree.Step(depth, label, state, note);
    }

    // A node's label: the vertex, marked when the pick was forced. Only a vertex with no neighbours is forced, and no
    // branch ever picks one (the vertex it branches on would have been forced instead).
    private static string Pick(List<string> names, List<int>[] adj, int v) => adj[v].Count == 0 ? $"{names[v]} (forced)" : names[v];

    // How many vertices neither are in 'chosen' nor have a neighbour in it.
    private static int Undominated(List<int> chosen, List<int>[] adj) {
        var dominated = new bool[adj.Length];
        foreach (int v in chosen) {
            dominated[v] = true;
            foreach (int u in adj[v])
                dominated[u] = true;
        }
        return dominated.Count(d => !d);
    }

    public string solve(DOMINATINGSET problem) => Solve(problem, StepRecorder<NodeSet>.Off);

    public string Solve(DOMINATINGSET problem, StepRecorder<NodeSet> rec) {
        //Get problem data
        int n = problem.nodes.Count;
        int K = problem.K;
        var adj = new List<int>[n];
        // A branch picks each vertex at most once.
        var tree = new DepthFirstTree(n);
        var steps = NewSteps(rec, problem.nodes, K, adj, tree);

        // Empty graph case
        if (n == 0) {
            const string emptyCert = "{}";
            bool emptyOk = problem.defaultVerifier.verify(problem, emptyCert);
            steps.Done(new NodeSet([]), emptyOk, $"There are no vertices.");
            return emptyOk ? emptyCert : "{}";
        }

        var indexOf = new Dictionary<string, int>(n);
        for (int i = 0; i < n; i++) {
            indexOf[problem.nodes[i]] = i;
        }

        // Build Adjacency list
        for (int i = 0; i < n; i++) {
            adj[i] = new List<int>();
        }

        foreach (var edge in problem.edges) {
            int u = indexOf[edge.Key],
                v = indexOf[edge.Value];
            if (u == v)
                continue;
            adj[u].Add(v);
            adj[v].Add(u);
        }
        var closed = new List<int>[n];
        for (int v = 0; v < n; v++) {
            var set = new HashSet<int>(adj[v]) { v };
            closed[v] = set.ToList();
        }

        var dominated = new bool[n];
        var chosen = new List<int>();
        var solution = new List<int>();

        bool ok = SearchExact(n, K, adj, closed, dominated, chosen, out solution, problem.nodes, tree, steps);
        if (timerHasExpired) return SolveRun.TimedOutAnswer;
        if (!ok) {
            steps.Done(new NodeSet([]), false, $"No dominating set of size {K} or less exists.", tree: () => tree.NoAnswer());
            return "{}";
        }

        string cert = "{" + string.Join(",", solution.Select(i => problem.nodes[i])) + "}";
        bool verified = problem.defaultVerifier.verify(problem, cert);
        // The answer's path in the tree: the picks, in the order they were made.
        if (verified)
            steps.Done(new NodeSet(solution.Select(i => problem.nodes[i])), true, $"{cert} dominates every vertex, with {solution.Count} of at most {K} allowed.",
                tree: () => tree.AnswerPath(solution.Select(v => Pick(problem.nodes, adj, v)).ToList()));
        else
            steps.Done(new NodeSet([]), false, $"The set found did not verify.", tree: () => {
                // The search stops at the first pick that dominates every vertex, even past K (then it is cut off): every
                // pick of the set found fails, and so does the root.
                PicksFailed(solution.Count, 0, solution, problem.nodes, adj, tree);
                return tree.NoAnswer();
            });
        return verified ? cert : "{}";
    }

    private bool SearchExact(
        int n,
        int K,
        List<int>[] adj,
        List<int>[] closed,
        bool[] dominated,
        List<int> chosen,
        out List<int> solution,
        List<string> names,
        DepthFirstTree tree,
        RunSteps<(List<int> Chosen, int Vertex, SearchNodeState Tree), NodeSet> steps
    ) {
        solution = null!;

        // Unwind at the time limit; Solve() then returns the timed-out answer.
        if (timerHasExpired)
            return false;

        // Fast check: are we done?
        if (AllDominated(dominated)) {
            solution = new List<int>(chosen);
            return true;
        }
        if (K <= 0) {
            // no picks left but not fully dominated; in the tree, the newest pick is cut off
            steps.Reject((chosen, -1, SearchNodeState.Pruned), $"No picks left, and {dominated.Count(d => !d)} vertices are still not dominated.");
            return false;
        }

        // The picks made before this call, so the forced picks it makes can be failed when they lead nowhere.
        int picksBefore = chosen.Count;
        bool forcedApplied;
        do {
            forcedApplied = false;

            // find an undominated vertex with no neighbors that can cover it except itself (i.e., deg == 0)
            int forced = -1;
            for (int v = 0; v < n; v++) {
                if (dominated[v])
                    continue;
                if (adj[v].Count == 0) {
                    forced = v;
                    break;
                } // isolated vertex, must pick it
            }

            if (forced != -1) {
                // pick 'forced'
                chosen.Add(forced);
                ApplyPick(closed, forced, dominated);
                K--;
                // In the tree, a forced pick is the one child of the pick before it.
                tree.Branch(chosen.Count);
                steps.Accept((chosen, forced, SearchNodeState.Explored), $"Take {names[forced]}: it has no neighbors, so only taking it can dominate it.");
                if (K < 0) {
                    PicksFailed(chosen.Count - 1, picksBefore, chosen, names, adj, tree);
                    return false;
                }
                forcedApplied = true;

                // if everything is dominated now, we can finish early
                if (AllDominated(dominated)) {
                    solution = new List<int>(chosen);
                    return true;
                }
            }
        } while (forcedApplied);

        int uPick = -1;
        int bestDeg = -1;
        for (int v = 0; v < n; v++) {
            if (dominated[v])
                continue;
            int deg = adj[v].Count;
            if (deg > bestDeg) {
                bestDeg = deg;
                uPick = v;
            }
        }

        if (uPick == -1) {
            solution = new List<int>(chosen);
            return true;
        }

        // Branch: the undominated vertex of highest degree must be dominated by itself or one of its neighbours.
        steps.Try((chosen, uPick, SearchNodeState.Explored),
            $"{names[uPick]} isn't dominated yet. Some vertex in {GraphSubsetFrames.Braces(closed[uPick].Select(c => names[c]))} has to be taken.");
        foreach (int w in closed[uPick]) {
            var dominated2 = (bool[])dominated.Clone();
            var chosen2 = new List<int>(chosen) { w };
            ApplyPick(closed, w, dominated2);
            tree.Branch(chosen2.Count);
            steps.Accept((chosen2, w, SearchNodeState.Explored),
                $"Take {names[w]}, which dominates {Words.Count(closed[w].Count(c => !dominated[c]), "more vertex", "more vertexes")}.");

            if (SearchExact(n, K - 1, adj, closed, dominated2, chosen2, out solution, names, tree, steps))
                return true; // propagate success
            steps.Backtrack((chosen, w, SearchNodeState.Failed), $"Back out {names[w]}: no dominating set within K follows from taking it.");
        }

        PicksFailed(chosen.Count, picksBefore, chosen, names, adj, tree);
        return false; // no choice worked
    }

    // For the tree: the picks at depths picksBefore + 1 to deepest (the forced picks a call made) led nowhere, so the next
    // step kept marks them failed, the newest first.
    private static void PicksFailed(int deepest, int picksBefore, List<int> chosen, List<string> names, List<int>[] adj, DepthFirstTree tree) {
        for (int d = deepest; d > picksBefore; d--)
            if (tree.Sent(d))
                tree.Failed(d, Pick(names, adj, chosen[d - 1]));
    }

    private void ApplyPick(List<int>[] closed, int v, bool[] dominated) {
        foreach (int u in closed[v])
            dominated[u] = true;
    }

    private bool AllDominated(bool[] dominated) {
        for (int i = 0; i < dominated.Length; i++)
            if (!dominated[i])
                return false;
        return true;
    }
}