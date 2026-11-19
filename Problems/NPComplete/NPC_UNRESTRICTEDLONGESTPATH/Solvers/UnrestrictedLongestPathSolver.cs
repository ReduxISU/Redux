using System.Numerics;
using API.Interfaces;

namespace API.Problems.NPComplete.NPC_UNRESTRICTEDLONGESTPATH.Solvers;

class UnrestrictedLongestPathSolver : ISolver<UNRESTRICTEDLONGESTPATH> {

    // --- Fields ---
    public string solverName { get; } = "Alon-Yuster-Zwick Color Coding Algorithm";
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
    // Redux sets this flag when the compute time limit runs out; the solver must notice it and stop.
    // volatile: the flag is written by the timer thread while solve() is running on another thread.
    private volatile bool _timerHasExpired;
    public bool timerHasExpired { get => _timerHasExpired; set => _timerHasExpired = value; }

    public string sourceFile { get; } = SourceFile.Path();

    // --- Methods Including Constructors ---
    public UnrestrictedLongestPathSolver() { }

    // ln(1 - p) for 0 < p < 1. For small p it uses the series -p - p^2/2 - p^3/3 (the error is below 1e-12 of the
    // result for p < 1e-4); otherwise 1 - p is exact enough for Math.Log.
    private static double LogOneMinus(double p) =>
        p < 1e-4 ? -p * (1 + p / 2 + p * p / 3) : Math.Log(1 - p);

    public string solve(UNRESTRICTEDLONGESTPATH problem) {
        if (timerHasExpired)
            return SolveRun.TimedOutAnswer;

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
        // Math.Log(1 - p) rounds 1 - p to 1 once p is below about 1e-16 (a target of 40 or more vertices), which made
        // the trial count too small or infinite. LogOneMinus stays accurate for tiny p.
        double trialLimit = colorfulProbability >= 1
            ? 1
            : Math.Ceiling(Math.Log(missProbability) / LogOneMinus(colorfulProbability));
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
            if (timerHasExpired)
                return SolveRun.TimedOutAnswer;

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
                if (timerHasExpired)
                    return SolveRun.TimedOutAnswer;

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
