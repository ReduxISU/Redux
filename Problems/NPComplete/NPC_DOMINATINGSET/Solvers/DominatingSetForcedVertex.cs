using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_DOMINATINGSET.Solvers;

class DominatingSetForcedVertex : ISolver<DOMINATINGSET, NodeSet> {
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
    // ApplyPick). That's O(n^K) leaves at O(n) work apiece. The forced-vertex pruning
    // makes this far faster in practice -- this bound is worst-case only.
    public string complexity { get; } = "O(n^(K+1)), n = |nodes|, K = target dominating-set size";

    // --- Methods Including Constructors ---
    public DominatingSetForcedVertex() { }

    public string solve(DOMINATINGSET problem) => Solve(problem, StepRecorder<NodeSet>.Off);

    // Steps: a forced vertex taken, the vertex branched on, each neighbor tried, a branch backed out or out of picks.
    // Recorder state lives only in this call.
    public string Solve(DOMINATINGSET problem, StepRecorder<NodeSet> rec) {
        //Get problem data
        int n = problem.nodes.Count;
        int K = problem.K;

        // Empty graph case
        if (n == 0) {
            const string emptyCert = "{}";
            bool emptyOk = problem.defaultVerifier.verify(problem, emptyCert);
            rec.Done(new NodeSet([]), emptyOk, "There are no vertices.");
            return emptyOk ? emptyCert : "{}";
        }

        var indexOf = new Dictionary<string, int>(n);
        for (int i = 0; i < n; i++) {
            indexOf[problem.nodes[i]] = i;
        }

        // Build Adjacency list
        var adj = new List<int>[n];

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

        bool ok = SearchExact(n, K, adj, closed, dominated, chosen, out solution, problem.nodes, rec);
        if (!ok) {
            rec.Done(new NodeSet([]), false, $"No dominating set of size {K} or less exists.");
            return "{}";
        }

        string cert = "{" + string.Join(",", solution.Select(i => problem.nodes[i])) + "}";
        bool verified = problem.defaultVerifier.verify(problem, cert);
        rec.Done(verified ? new NodeSet(solution.Select(i => problem.nodes[i])) : new NodeSet([]), verified,
            verified ? $"{cert} dominates every vertex, with {solution.Count} of at most {K} allowed." : "The set found did not verify.");
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
        StepRecorder<NodeSet> rec
    ) {
        solution = null!;
        string[] Names(List<int> picks) => picks.Select(c => names[c]).ToArray();

        // Fast check: are we done?
        if (AllDominated(dominated)) {
            solution = new List<int>(chosen);
            return true;
        }
        if (K <= 0) {
            // no picks left but not fully dominated
            rec.Reject(() => new NodeSet(Names(chosen)),
                () => $"No picks left, and {dominated.Count(d => !d)} vertices are still not dominated.");
            return false;
        }

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
                rec.Accept(() => new NodeSet(Names(chosen)),
                    () => $"Take {names[forced]}: it has no neighbors, so only taking it can dominate it.", names[forced]);
                if (K < 0)
                    return false;
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

        rec.Try(() => new NodeSet(Names(chosen)),
            () => $"{names[uPick]} isn't dominated yet. Some vertex in {GraphSubsetFrames.Braces(closed[uPick].Select(c => names[c]))} has to be taken.",
            names[uPick]);
        foreach (int w in closed[uPick]) {
            var dominated2 = (bool[])dominated.Clone();
            var chosen2 = new List<int>(chosen) { w };
            ApplyPick(closed, w, dominated2);
            rec.Accept(() => new NodeSet(Names(chosen2)),
                () => $"Take {names[w]}, which dominates {closed[w].Count(c => !dominated[c])} more vertex{(closed[w].Count(c => !dominated[c]) == 1 ? "" : "es")}.",
                names[w]);

            if (SearchExact(n, K - 1, adj, closed, dominated2, chosen2, out solution, names, rec))
                return true; // propagate success
            rec.Backtrack(() => new NodeSet(Names(chosen)), () => $"Back out {names[w]}: no dominating set within K follows from taking it.", names[w]);
        }

        return false; // no choice worked
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