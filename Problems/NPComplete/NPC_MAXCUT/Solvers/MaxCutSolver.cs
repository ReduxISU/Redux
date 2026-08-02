using API.Interfaces;

namespace API.Problems.NPComplete.NPC_MAXCUT.Solvers;

class MaxCutSolver : ISolver<MAXCUT>
{
    public string solverName { get; } = "Max Cut Brute Force Solver";
    public string solverDefinition { get; } = "Enumerates all 2^n partitions of the vertex set and returns the S side of the partition whose crossing edges have maximum total weight. Certificate format: {node1,node2,...} representing the S side.";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Max Gruenwoldt", "Eric Hill" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Unpruned exhaustive enumeration.
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // Declared, not derived. Iterates every mask from 1 to (1<<n)-2, i.e. all 2^n vertex
    // subsets; each mask costs O(n) to build S plus O(m) to sum crossing-edge weights
    // (CutWeight, HashSet lookups are O(1)).
    public string complexity { get; } = "O(2^n * (n + m)), n = |nodes|, m = |edges|";

    public MaxCutSolver() { }

    public string solve(MAXCUT problem)
    {
        List<string> nodes = problem.nodes;
        int n = nodes.Count;
        if (n < 2) return "{}";

        int bestWeight = -1;
        HashSet<string> bestS = new();

        int limit = 1 << n;
        for (int mask = 1; mask < limit - 1; mask++)
        {
            if (timerHasExpired) return "{}";

            HashSet<string> S = new();
            for (int i = 0; i < n; i++)
                if ((mask >> i & 1) == 1) S.Add(nodes[i]);

            int w = CutWeight(problem.edges, S);
            if (w > bestWeight)
            {
                bestWeight = w;
                bestS = S;
            }
        }

        if (bestS.Count == 0) return "{}";
        return "{" + string.Join(",", bestS) + "}";
    }

    internal static int GetMaxCutWeight(MAXCUT problem)
    {
        List<string> nodes = problem.nodes;
        int n = nodes.Count;
        if (n < 2) return 0;

        int bestWeight = 0;
        int limit = 1 << n;
        for (int mask = 1; mask < limit - 1; mask++)
        {
            HashSet<string> S = new();
            for (int i = 0; i < n; i++)
                if ((mask >> i & 1) == 1) S.Add(nodes[i]);

            int w = CutWeight(problem.edges, S);
            if (w > bestWeight)
                bestWeight = w;
        }
        return bestWeight;
    }

    internal static int CutWeight(List<(string source, string destination, int weight)> edges, HashSet<string> S)
    {
        int total = 0;
        foreach (var edge in edges)
        {
            bool srcInS = S.Contains(edge.source);
            bool dstInS = S.Contains(edge.destination);
            if (srcInS != dstInS)
                total += edge.weight;
        }
        return total;
    }
}
