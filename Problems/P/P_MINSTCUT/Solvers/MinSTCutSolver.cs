using API.Interfaces;

namespace API.Problems.P.P_MINSTCUT.Solvers;

class MinSTCutSolver : ISolver<MINSTCUT>
{
    public string solverName { get; } = "Minimum S-T Cut Solver (Edmonds-Karp)";
    public string solverDefinition { get; } = "Uses the Edmonds-Karp algorithm (BFS-based Ford-Fulkerson max-flow) to find the maximum s-t flow, then identifies the minimum cut S side via BFS on the residual graph from the source. Certificate format: {node1,node2,...} representing the S side of the minimum cut.";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Michael Trosper" };
    public bool timerHasExpired { get; set; }

    public MinSTCutSolver() { }

    public string solve(MINSTCUT problem)
    {
        if (problem.nodes.Count == 0) return "{}";

        var residual = BuildResidual(problem.nodes, problem.edges);

        while (true)
        {
            if (timerHasExpired) return "{}";

            var parent = BfsAugmentingPath(residual, problem.sourceNode, problem.targetNode);
            if (parent == null) break;

            int bottleneck = FindBottleneck(residual, parent, problem.sourceNode, problem.targetNode);
            Augment(residual, parent, problem.sourceNode, problem.targetNode, bottleneck);
        }

        HashSet<string> sSide = BfsReachable(residual, problem.sourceNode);
        return "{" + string.Join(",", sSide) + "}";
    }

    internal static int GetMinCutCapacity(MINSTCUT problem)
    {
        var residual = BuildResidual(problem.nodes, problem.edges);

        while (true)
        {
            var parent = BfsAugmentingPath(residual, problem.sourceNode, problem.targetNode);
            if (parent == null) break;
            int bottleneck = FindBottleneck(residual, parent, problem.sourceNode, problem.targetNode);
            Augment(residual, parent, problem.sourceNode, problem.targetNode, bottleneck);
        }

        HashSet<string> sSide = BfsReachable(residual, problem.sourceNode);
        return CutCapacity(problem.edges, sSide);
    }

    internal static int CutCapacity(List<(string from, string to, int weight)> edges, HashSet<string> S)
    {
        int total = 0;
        foreach (var (from, to, weight) in edges)
        {
            if (S.Contains(from) && !S.Contains(to))
                total += weight;
        }
        return total;
    }

    private static Dictionary<string, Dictionary<string, int>> BuildResidual(
        List<string> nodes, List<(string from, string to, int weight)> edges)
    {
        var residual = new Dictionary<string, Dictionary<string, int>>();
        foreach (string node in nodes)
            residual[node] = new Dictionary<string, int>();

        foreach (var (from, to, weight) in edges)
        {
            if (!residual[from].ContainsKey(to))
                residual[from][to] = 0;
            residual[from][to] += weight;

            if (!residual[to].ContainsKey(from))
                residual[to][from] = 0;
        }

        return residual;
    }

    private static Dictionary<string, string>? BfsAugmentingPath(
        Dictionary<string, Dictionary<string, int>> residual,
        string source, string target)
    {
        var parent = new Dictionary<string, string>();
        parent[source] = source;
        var queue = new Queue<string>();
        queue.Enqueue(source);

        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            if (current == target) return parent;

            foreach (var (next, cap) in residual[current])
            {
                if (cap > 0 && !parent.ContainsKey(next))
                {
                    parent[next] = current;
                    queue.Enqueue(next);
                }
            }
        }

        return parent.ContainsKey(target) ? parent : null;
    }

    private static int FindBottleneck(
        Dictionary<string, Dictionary<string, int>> residual,
        Dictionary<string, string> parent,
        string source, string target)
    {
        int bottleneck = int.MaxValue;
        string node = target;
        while (node != source)
        {
            string prev = parent[node];
            bottleneck = Math.Min(bottleneck, residual[prev][node]);
            node = prev;
        }
        return bottleneck;
    }

    private static void Augment(
        Dictionary<string, Dictionary<string, int>> residual,
        Dictionary<string, string> parent,
        string source, string target, int bottleneck)
    {
        string node = target;
        while (node != source)
        {
            string prev = parent[node];
            residual[prev][node] -= bottleneck;
            residual[node][prev] += bottleneck;
            node = prev;
        }
    }

    private static HashSet<string> BfsReachable(
        Dictionary<string, Dictionary<string, int>> residual,
        string source)
    {
        var reachable = new HashSet<string>();
        var queue = new Queue<string>();
        reachable.Add(source);
        queue.Enqueue(source);

        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            foreach (var (next, cap) in residual[current])
            {
                if (cap > 0 && !reachable.Contains(next))
                {
                    reachable.Add(next);
                    queue.Enqueue(next);
                }
            }
        }

        return reachable;
    }
}
