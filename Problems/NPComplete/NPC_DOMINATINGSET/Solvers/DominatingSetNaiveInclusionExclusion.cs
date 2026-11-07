using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_DOMINATINGSET.Solvers;

// The steps Naive Inclusion-Exclusion records. Despite the name it counts nothing: it decides, vertex by vertex, whether
// the vertex is in the set (the "include" branch) or not (the "exclude" branch), so its steps are those of a search:
//   - an Accept when it includes a vertex (keeps it in the set for this branch);
//   - a Backtrack when it backs that vertex out again;
//   - a Try when it leaves the vertex out (the other branch, which can still fail);
//   - for a finished set, a Reject naming the vertices it leaves undominated, or the Accept that ends the search;
//   - a Reject when the set has grown past K;
//   - the Done, with the dominating set or none.
// The set is built one decision at a time, so the solver is an IIncrementalSolver.
// The same steps draw the Search Tree picture. In the tree, a node is the decision on one vertex, "3 in" (included) or
// "3 out" (left out), under the decision on the vertex before it, so its children are the two decisions on the next
// vertex. An Accept's or a Try's node is explored, or the solution for a finished set that dominates every vertex; an
// include that takes the set over K is cut off (pruned, "over K = 2") and stays so when it is backed out; a finished set
// that leaves vertices undominated cuts its last decision off ("2 vertices not dominated"); any other include backed out
// fails, and so does a leave-out whose every finished set fails. The Done names the answer's path, root first, or marks
// the root failed.
class DominatingSetNaiveInclusionExclusion : ISolver<DOMINATINGSET, NodeSet>, IIncrementalSolver, ISearchTreeSolver {
    // --- Fields ---
    public string solverName { get; } = "Naive Inclusion-Exclusion Backtracking Dominating Set Solver";
    public string solverDefinition { get; } =
        "Exhaustively enumerates every subset of vertices via a binary include/exclude recursion:"
        + " at each vertex, branches into one call that includes it in the candidate set D and one"
        + " that excludes it. No pruning is applied while branching -- the full recursion tree is"
        + " built out to all n vertices before a candidate is checked. Once a subset is complete,"
        + " it is accepted if its size is <= K and it dominates every vertex (every vertex is either"
        + " in D or adjacent to a vertex in D). Returns the first such subset found.";
    public string source { get; } =
    "Fomin, F. V., & Kratsch, D. (2010). Exact Exponential Algorithms. Springer Science & Business Media.";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://doi.org/10.1007/978-3-642-16533-7";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public bool timerHasExpired { get; set; }
    public SolverType solverType { get; } = SolverType.Backtracking;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // Declared, not derived. Every vertex branches into include/exclude, giving 2^n leaves;
    // each leaf does an O(n + m) domination check.
    public MathematicalFunction timeComplexity { get; } = new("2^n * (n + m)", ", n = |nodes|, m = |edges|");

    // --- Methods Including Constructors ---
    public DominatingSetNaiveInclusionExclusion() { }

    // How the steps show the search: the vertex indices included so far, the vertex the step decides on (-1: none), and the
    // state the step leaves its search-tree node in. The search keeps its own tree (a "start" root, a decision cut off as a
    // pruned node, a decision failed when it is backed out), so it gives RunSteps the tree node of each step rather than
    // using SearchSteps.
    private static RunSteps<(List<int> Chosen, int Vertex, SearchNodeState Tree), NodeSet> NewSteps(StepRecorder<NodeSet> rec, List<string> names,
        int K, List<int>[] adj, DepthFirstTree tree) => new(rec,
        // What every picture shows: the set so far, and the vertex included, backed out or left out.
        partial: s => new NodeSet(s.Chosen.Select(c => names[c])),
        focus: s => s.Vertex < 0 ? [] : [names[s.Vertex]],
        // The Search Tree picture's node only.
        tree: s => TreeNode(s.Chosen, s.Vertex, s.Tree, names, K, adj, tree));

    // The search-tree node of a step. The decision on vertex v is the node at depth v + 1 (the root is at depth 0). A step
    // about no one vertex is about the decision just made: the include that took the set over K (the root, when K is below
    // 0), or for a finished set, the decision on the last vertex.
    private static SearchNode[] TreeNode(List<int> chosen, int v, SearchNodeState state, List<string> names, int K, List<int>[] adj, DepthFirstTree tree) {
        int n = names.Count;
        int depth = v >= 0 ? v + 1 : chosen.Count > K ? (chosen.Count == 0 ? 0 : chosen[^1] + 1) : n;
        // A Backtrack's node is the include it backs out; any other node is included when the set holds its vertex.
        string label = depth == 0 ? "" : Decision(names, depth - 1, state == SearchNodeState.Failed || chosen.Contains(depth - 1));
        string? note = state != SearchNodeState.Pruned ? null
            : chosen.Count > K ? $"over K = {K}"
            : $"{Words.Count(Undominated(chosen, n, adj).Count(), "vertex", "vertices")} not dominated";
        return tree.Step(depth, label, state, note);
    }

    // A node's label: "3 in" or "3 out". Vertex names are often numbers, so "+3" and "−3" would read as numbers.
    private static string Decision(List<string> names, int v, bool included) => $"{names[v]} {(included ? "in" : "out")}";

    public string solve(DOMINATINGSET problem) => Solve(problem, StepRecorder<NodeSet>.Off);

    public string Solve(DOMINATINGSET problem, StepRecorder<NodeSet> rec) {
        int n = problem.nodes.Count;
        int K = problem.K;
        var adj = new List<int>[n];
        // A branch decides each vertex once.
        var tree = new DepthFirstTree(n);
        var steps = NewSteps(rec, problem.nodes, K, adj, tree);

        if (n == 0) {
            const string emptyCert = "{}";
            bool emptyOk = problem.defaultVerifier.verify(problem, emptyCert);
            steps.Done(new NodeSet([]), emptyOk, $"There are no vertices.");
            return emptyOk ? emptyCert : "{}";
        }

        var indexOf = new Dictionary<string, int>(n);
        for (int i = 0; i < n; i++)
            indexOf[problem.nodes[i]] = i;

        for (int i = 0; i < n; i++)
            adj[i] = new List<int>();

        foreach (var edge in problem.edges) {
            int u = indexOf[edge.Key], v = indexOf[edge.Value];
            if (u == v) continue;
            adj[u].Add(v);
            adj[v].Add(u);
        }

        var chosen = new List<int>();
        List<int> solution = new List<int>();

        bool found = Branch(0, n, K, adj, chosen, ref solution, problem, tree, steps);
        if (timerHasExpired) return SolveRun.TimedOutAnswer;

        if (!found) {
            steps.Done(new NodeSet([]), false, $"No dominating set of size {K} or less exists.", tree: () => tree.NoAnswer());
            return "{}";
        }

        string cert = "{" + string.Join(",", solution.Select(i => problem.nodes[i])) + "}";
        bool verified = problem.defaultVerifier.verify(problem, cert);
        // The answer's path in the tree: the decision on every vertex, in order.
        if (verified)
            steps.Done(new NodeSet(solution.Select(i => problem.nodes[i])), true, $"{cert} dominates every vertex, with {solution.Count} of at most {K} allowed.",
                tree: () => tree.AnswerPath(Enumerable.Range(0, n).Select(v => Decision(problem.nodes, v, solution.Contains(v))).ToList()));
        else
            steps.Done(new NodeSet([]), false, $"The set found did not verify.", tree: () => tree.NoAnswer());
        return verified ? cert : "{}";
    }

    // Naive include/exclude recursion over vertex indices [i, n).
    private bool Branch(int i, int n, int K, List<int>[] adj, List<int> chosen, ref List<int> solution,
        DOMINATINGSET problem, DepthFirstTree tree, RunSteps<(List<int> Chosen, int Vertex, SearchNodeState Tree), NodeSet> steps) {
        // Unwind at the time limit; Solve() then returns the timed-out answer.
        if (timerHasExpired)
            return false;

        // In the tree, the include that took the set over K is cut off.
        if (chosen.Count > K) {
            steps.Reject((chosen, -1, SearchNodeState.Pruned), $"{Words.Braces(chosen.Select(c => problem.nodes[c]))} has more than K = {K} vertices. Stop here.");
            return false;
        }

        // In the tree, a finished set is the decision on the last vertex: the solution, or cut off.
        if (i == n) {
            if (Dominates(chosen, n, adj)) {
                steps.Accept((chosen, -1, SearchNodeState.Solution), $"Try {Words.Braces(chosen.Select(c => problem.nodes[c]))}. It dominates every vertex.",
                    focus: () => chosen.Select(c => problem.nodes[c]).ToArray());
                solution = new List<int>(chosen);
                return true;
            }
            // Dominates only says no, so what fails is looked up for a kept step. The set was built one decision at a time,
            // so the focus is what failed (the vertices left undominated), not the set.
            steps.Reject((chosen, -1, SearchNodeState.Pruned),
                $"Try {Words.Braces(chosen.Select(c => problem.nodes[c]))}: {GraphSubsetFrames.Why(SubsetRule.DominatingSet, problem.nodes, problem.edges, chosen.Select(c => problem.nodes[c]))}.",
                focus: () => Undominated(chosen, n, adj).Select(v => problem.nodes[v]).ToArray());
            return false;
        }

        string vertex = problem.nodes[i];
        chosen.Add(i);
        // Include vertex i, and decide the rest; if that fails, back it out and decide the rest without it.
        tree.Branch(i + 1);
        steps.Accept((chosen, i, SearchNodeState.Explored), $"Include {vertex}.");
        if (Branch(i + 1, n, K, adj, chosen, ref solution, problem, tree, steps))
            return true;
        chosen.RemoveAt(chosen.Count - 1);
        steps.Backtrack((chosen, i, SearchNodeState.Failed), $"Back out {vertex}.");

        tree.Branch(i + 1);
        steps.Try((chosen, i, SearchNodeState.Explored), $"Leave {vertex} out.");
        if (Branch(i + 1, n, K, adj, chosen, ref solution, problem, tree, steps))
            return true;
        // In the tree, the leave-out led nowhere, so the next step kept marks it failed (at the last vertex, the finished
        // set's check already cut it off).
        if (i + 1 < n && tree.Sent(i + 1))
            tree.Failed(i + 1, Decision(problem.nodes, i, false));
        return false;
    }

    private bool Dominates(List<int> chosen, int n, List<int>[] adj) {
        var dominated = new bool[n];
        foreach (int v in chosen) {
            dominated[v] = true;
            foreach (int u in adj[v])
                dominated[u] = true;
        }
        for (int v = 0; v < n; v++)
            if (!dominated[v])
                return false;
        return true;
    }

    // The vertices that neither are in 'chosen' nor have a neighbor in it, in index order.
    private static IEnumerable<int> Undominated(List<int> chosen, int n, List<int>[] adj) {
        var dominated = new bool[n];
        foreach (int v in chosen) {
            dominated[v] = true;
            foreach (int u in adj[v])
                dominated[u] = true;
        }
        return Enumerable.Range(0, n).Where(v => !dominated[v]);
    }
}