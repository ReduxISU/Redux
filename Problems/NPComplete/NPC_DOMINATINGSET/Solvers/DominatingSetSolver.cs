using API.Interfaces;

namespace API.Problems.NPComplete.NPC_DOMINATINGSET.Solvers
{
    class DominatingSetSolver : ISolver<DOMINATINGSET>
    {
        // --- Fields ---
        private string _solverName = "Dominating Set Solver";
        private string _solverDefinition =
            "Greedy approximation: pick an uncovered vertex, mark it and all neighbors covered.";
        private string _source =
            "Exactly solving minimum dominating set and its generalizations: A branch-and-reduce approach, Akiba and Iwata, 2016";
        private string[] _contributors = { "Quinton Smith" };
        public bool timerHasExpired { get; set; }

        // --- Properties ---
        public string solverName => _solverName;
        public string solverDefinition => _solverDefinition;
        public string source => _source;
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

            // --- Choose an undominated vertex u to branch on ---
            // Heuristic: highest degree among undominated vertices
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

            // Safety: if somehow none found (shouldn't happen), success
            if (uPick == -1)
            {
                solution = new List<int>(chosen);
                return true;
            }

            // --- Branch: in any dominating set, at least one vertex from {uPick} ∪ N(uPick) must be chosen ---
            // Try each candidate w in closed neighborhood of uPick
            foreach (int w in closed[uPick])
            {
                // Recurse with w added
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
