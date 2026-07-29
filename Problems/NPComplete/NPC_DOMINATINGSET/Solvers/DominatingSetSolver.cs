using API.Interfaces;

namespace API.Problems.NPComplete.NPC_DOMINATINGSET.Solvers
{
    class DominatingSetSolver : ISolver<DOMINATINGSET>
    {
        // --- Fields ---
        private string _solverName = "Dominating Set Solver";
        private string _solverDefinition =
            "Exact branching search using closed neighborhoods to find a dominating set of size <= K.";
        private string _source =
            "Exactly solving minimum dominating set and its generalizations: A branch-and-reduce approach, Akiba and Iwata, 2016";
        private string _sourceLink = "https://arxiv.org/abs/1603.02882";
        private string[] _contributors = { "Quinton Smith" };
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
        public string complexity { get; } = "O(n^(K+1)), n = |nodes|, K = target dominating-set size";

        // --- Properties ---
        public string solverName => _solverName;
        public string solverDefinition => _solverDefinition;
        public string source => _source;
        public string sourceLink => _sourceLink;
        public string[] contributors => _contributors;

        // --- Methods Including Constructors ---
        public DominatingSetSolver() { }

        public string solve(DOMINATINGSET problem)
        {
            //Get problem data
            int n = problem.nodes.Count;
            int K = problem.K;

            // Empty graph case
            if (n == 0)
            {
                const string emptyCert = "{}";
                return problem.defaultVerifier.verify(problem, emptyCert) ? emptyCert : "{}";
            }

            var indexOf = new Dictionary<string, int>(n);
            for (int i = 0; i < n; i++)
            {
                indexOf[problem.nodes[i]] = i;
            }

            // Build Adjacency list
            var adj = new List<int>[n];

            for (int i = 0; i < n; i++)
            {
                adj[i] = new List<int>();
            }

            foreach (var edge in problem.edges)
            {
                int u = indexOf[edge.Key],
                    v = indexOf[edge.Value];
                if (u == v)
                    continue;
                adj[u].Add(v);
                adj[v].Add(u);
            }
            var closed = new List<int>[n];
            for (int v = 0; v < n; v++)
            {
                var set = new HashSet<int>(adj[v]) { v };
                closed[v] = set.ToList();
            }

            var dominated = new bool[n];
            var chosen = new List<int>();
            var solution = new List<int>();

            bool ok = SearchExact(n, K, adj, closed, dominated, chosen, out solution);
            if (!ok)
                return "{}";

            string cert = "{" + string.Join(",", solution.Select(i => problem.nodes[i])) + "}";
            return problem.defaultVerifier.verify(problem, cert) ? cert : "{}";
        }

        private bool SearchExact(
            int n,
            int K,
            List<int>[] adj,
            List<int>[] closed,
            bool[] dominated,
            List<int> chosen,
            out List<int> solution
        )
        {
            solution = null!;

            // Fast check: are we done?
            if (AllDominated(dominated))
            {
                solution = new List<int>(chosen);
                return true;
            }
            if (K < 0)
                return false; // used too many picks already
            if (K == 0)
                return false; // no picks left but not fully dominated

            bool forcedApplied;
            do
            {
                forcedApplied = false;

                // find an undominated vertex with no neighbors that can cover it except itself (i.e., deg == 0)
                int forced = -1;
                for (int v = 0; v < n; v++)
                {
                    if (dominated[v])
                        continue;
                    if (adj[v].Count == 0)
                    {
                        forced = v;
                        break;
                    } // isolated vertex, must pick it
                }

                if (forced != -1)
                {
                    // pick 'forced'
                    chosen.Add(forced);
                    ApplyPick(closed, forced, dominated);
                    K--;
                    if (K < 0)
                        return false;
                    forcedApplied = true;

                    // if everything is dominated now, we can finish early
                    if (AllDominated(dominated))
                    {
                        solution = new List<int>(chosen);
                        return true;
                    }
                }
            } while (forcedApplied);

            int uPick = -1;
            int bestDeg = -1;
            for (int v = 0; v < n; v++)
            {
                if (dominated[v])
                    continue;
                int deg = adj[v].Count;
                if (deg > bestDeg)
                {
                    bestDeg = deg;
                    uPick = v;
                }
            }

            if (uPick == -1)
            {
                solution = new List<int>(chosen);
                return true;
            }

            foreach (int w in closed[uPick])
            {
                var dominated2 = (bool[])dominated.Clone();
                var chosen2 = new List<int>(chosen) { w };
                ApplyPick(closed, w, dominated2);

                if (SearchExact(n, K - 1, adj, closed, dominated2, chosen2, out solution))
                    return true; // propagate success
            }

            return false; // no choice worked
        }

        private void ApplyPick(List<int>[] closed, int v, bool[] dominated)
        {
            foreach (int u in closed[v])
                dominated[u] = true;
        }

        private bool AllDominated(bool[] dominated)
        {
            for (int i = 0; i < dominated.Length; i++)
                if (!dominated[i])
                    return false;
            return true;
        }
    }
}
