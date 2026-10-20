using System.Numerics;
using API.Interfaces;

namespace API.Problems.NPComplete.NPC_UNRESTRICTEDLONGESTPATH.Solvers;

class UnrestrictedLongestPathSolver : ISolver<UNRESTRICTEDLONGESTPATH> {

    // --- Fields ---
    public string solverName { get; } = "Longest Path Solver - Color Coding Algorithm";
    public string solverDefinition { get; } =
        "Uses the Alon-Yuster-Zwick color-coding algorithm. In each iteration, it colors"
        + " vertices uniformly with K+1 colors and uses subset dynamic programming to find"
        + " a colorful simple path on K+1 vertices. Independent colorings are tried until"
        + " the probability of missing an existing path is at most 0.1%. A returned path"
        + " is always valid; a negative result has this bounded probability of being a"
        + " false negative.";
    public string source { get; } =
        "Alon, N., Yuster, R., & Zwick, U. (1995). Color-coding. Journal of the ACM, 42(4), 844–856.";
    public string sourceLink { get; } = "https://doi.org/10.1145/210332.210337";
    public string[] contributors { get; } = { "Alice Giola", "Sadman Hasan", "Javier Rodriguez Lopez" };
    public SolverType solverType { get; } = SolverType.Stochastic;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    public MathematicalFunction timeComplexity { get; } = new(
        "2^(K+1) * (n + m) * (K+1)^(K+1) / (K+1)!",
        ", n = |nodes|, m = |edges|, K = required number of edges"
    );
    public bool timerHasExpired { get; set; }

    public string sourceFile { get; } = SourceFile.Path();

    // --- Methods Including Constructors ---
    public UnrestrictedLongestPathSolver() { }

    public string solve(UNRESTRICTEDLONGESTPATH problem) {
        int n = problem.nodes.Count;
        if (n == 0)
            return "{}";

        if (problem.K <= 0)
            return "{" + problem.nodes[0] + "}";
        if (problem.K >= n)
            return "{}";

        int targetVertices = problem.K + 1;
        double logColorfulProbability = 0;
        for (int colorCount = 1; colorCount <= targetVertices; colorCount++)
            logColorfulProbability += Math.Log(colorCount / (double)targetVertices);

        double colorfulProbability = Math.Exp(logColorfulProbability);
        double missProbability = 0.001;
        double trialLimit = colorfulProbability >= 1
            ? 1
            : Math.Ceiling(Math.Log(missProbability) / Math.Log(1 - colorfulProbability));
        if (double.IsInfinity(trialLimit) || trialLimit > long.MaxValue)
            throw new InvalidOperationException(
                "The requested path length requires more color-coding iterations than this solver can represent."
            );

        var indexOf = new Dictionary<string, int>(n);
        for (int i = 0; i < n; i++)
            indexOf[problem.nodes[i]] = i;

        var adjacency = new HashSet<int>[n];
        for (int i = 0; i < n; i++)
            adjacency[i] = new HashSet<int>();

        foreach (var edge in problem.edges) {
            int u = indexOf[edge.Key];
            int v = indexOf[edge.Value];
            if (u == v)
                continue;
            adjacency[u].Add(v);
            adjacency[v].Add(u);
        }

        var neighbors = adjacency.Select(set => set.ToArray()).ToArray();
        for (long trial = 0; trial < trialLimit; trial++) {
            var colorBit = new BigInteger[n];
            for (int v = 0; v < n; v++) {
                colorBit[v] = BigInteger.One << Random.Shared.Next(targetVertices);
            }

            var parents = new Dictionary<(BigInteger mask, int vertex), int>();
            for (int v = 0; v < n; v++) {
                parents[(colorBit[v], v)] = -1;
            }

            BigInteger maskLimit = BigInteger.One << targetVertices;
            BigInteger fullMask = maskLimit - BigInteger.One;
            for (BigInteger mask = BigInteger.One; mask < maskLimit; mask++) {

                for (int v = 0; v < n; v++) {
                    if ((mask & colorBit[v]) == BigInteger.Zero)
                        continue;

                    BigInteger previousMask = mask ^ colorBit[v];
                    foreach (int u in neighbors[v]) {
                        if (!parents.ContainsKey((previousMask, u)))
                            continue;

                        parents[(mask, v)] = u;
                        if (mask == fullMask) {
                            var path = new List<int>(targetVertices);
                            BigInteger pathMask = fullMask;
                            int endpoint = v;
                            while (pathMask != BigInteger.Zero) {
                                path.Add(endpoint);
                                int previous = parents[(pathMask, endpoint)];
                                pathMask ^= colorBit[endpoint];
                                if (pathMask != BigInteger.Zero)
                                    endpoint = previous;
                            }
                            path.Reverse();
                            return "{" + string.Join(",", path.Select(i => problem.nodes[i])) + "}";
                        }
                        break;
                    }
                }
            }
        }

        return "{}";
    }
}
