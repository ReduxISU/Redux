using API.Interfaces;

namespace API.Problems.P.P_MINCUT.Solvers;

class MinCutStoerWagner : ISolver<MINCUT> {
        public string solverName { get; } = "Stoer-Wagner Minimum Cut";
        public string solverDefinition { get; } = "Finds the global minimum cut of a weighted undirected graph using the Stoer-Wagner algorithm in O(V³) time.";
        public string source { get; } = "Stoer, Mechthild, and Frank Wagner. \"A simple min-cut algorithm.\" Journal of the ACM 44, no. 4 (1997): 585-591.";
        public string[] contributors { get; } = { "Michael Trosper" };
        public bool timerHasExpired { get; set; }
        // Declared, not derived. Each phase greedily grows a "maximum adjacency" ordering by
        // repeatedly picking the most tightly-connected remaining vertex.
        public SolverType solverType { get; } = SolverType.Greedy;
        public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
        // n-1 phases; each phase's maximum-adjacency-ordering search does an O(n) array scan to
        // pick the next node, n times (O(n^2) per phase), matching the solverDefinition's stated
        // O(V^3) bound (n = V here, on the dense adjacency matrix).
        public string complexity { get; } = "O(V^3)";

        public MinCutStoerWagner() { }

        public string solve(MINCUT problem) {
                int n = problem.nodes.Count;
                if (n < 2) return "{}";

                Dictionary<string, int> nodeIndex = BuildNodeIndex(problem.nodes);
                int[,] w = BuildAdjacencyMatrix(n, nodeIndex, problem.edges);

                if (timerHasExpired) return "{}";
                var (_, minSide) = RunStoerWagner(n, w);

                List<string> cutEdgeParts = new();
                HashSet<string> seen = new();
                foreach (var edge in problem.edges) {
                        int u = nodeIndex[edge.source];
                        int v = nodeIndex[edge.destination];
                        if (minSide.Contains(u) == minSide.Contains(v)) continue;

                        string key = $"{Math.Min(u, v)}-{Math.Max(u, v)}";
                        if (!seen.Add(key)) continue;
                        cutEdgeParts.Add("({" + edge.source + "," + edge.destination + "}," + edge.weight + ")");
                }

                return "{" + string.Join(",", cutEdgeParts) + "}";
        }

        // Runs Stoer-Wagner and returns (minCutWeight, one side of the min cut as original node indices).
        internal static (int weight, HashSet<int> side) RunStoerWagner(int n, int[,] wInput) {
                int[,] w = (int[,])wInput.Clone();
                List<HashSet<int>> merged = Enumerable.Range(0, n).Select(i => new HashSet<int> { i }).ToList();
                bool[] active = Enumerable.Repeat(true, n).ToArray();

                int bestWeight = int.MaxValue;
                HashSet<int> bestSide = new();

                for (int phase = 0; phase < n - 1; phase++) {
                        int[] key = new int[n];
                        bool[] inA = new bool[n];
                        int lastAdded = -1, secondLastAdded = -1;
                        int activeCount = active.Count(x => x);

                        for (int step = 0; step < activeCount; step++) {
                                int z = -1;
                                for (int i = 0; i < n; i++) {
                                        if (active[i] && !inA[i] && (z == -1 || key[i] > key[z]))
                                                z = i;
                                }
                                if (z == -1) break;

                                secondLastAdded = lastAdded;
                                lastAdded = z;
                                inA[z] = true;

                                for (int i = 0; i < n; i++)
                                        if (active[i] && !inA[i])
                                                key[i] += w[z, i];
                        }

                        if (lastAdded == -1 || secondLastAdded == -1) break;

                        // The cut-of-the-phase separates lastAdded from the rest.
                        // key[lastAdded] = sum of edges from lastAdded to all other active nodes.
                        int phaseWeight = key[lastAdded];
                        if (phaseWeight < bestWeight) {
                                bestWeight = phaseWeight;
                                bestSide = new HashSet<int>(merged[lastAdded]);
                        }

                        // Merge lastAdded into secondLastAdded.
                        for (int i = 0; i < n; i++) {
                                w[secondLastAdded, i] += w[lastAdded, i];
                                w[i, secondLastAdded] += w[i, lastAdded];
                        }
                        merged[secondLastAdded].UnionWith(merged[lastAdded]);
                        active[lastAdded] = false;
                }

                return (bestWeight == int.MaxValue ? 0 : bestWeight, bestSide);
        }

        internal static int GetMinCutWeight(MINCUT problem) {
                int n = problem.nodes.Count;
                if (n < 2) return 0;
                Dictionary<string, int> nodeIndex = BuildNodeIndex(problem.nodes);
                int[,] w = BuildAdjacencyMatrix(n, nodeIndex, problem.edges);
                var (weight, _) = RunStoerWagner(n, w);
                return weight;
        }

        internal static Dictionary<string, int> BuildNodeIndex(List<string> nodes) {
                Dictionary<string, int> index = new();
                for (int i = 0; i < nodes.Count; i++) index[nodes[i]] = i;
                return index;
        }

        internal static int[,] BuildAdjacencyMatrix(int n, Dictionary<string, int> nodeIndex,
            List<(string source, string destination, int weight)> edges) {
                int[,] w = new int[n, n];
                foreach (var edge in edges) {
                        int u = nodeIndex[edge.source];
                        int v = nodeIndex[edge.destination];
                        w[u, v] += edge.weight;
                        w[v, u] += edge.weight;
                }
                return w;
        }
}
