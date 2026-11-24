using API.Interfaces;

namespace API.Problems.NPComplete.NPC_UNRESTRICTEDLONGESTPATH.Solvers;

// Shared representation only: the three search algorithms remain separate.
internal sealed class UnrestrictedLongestPathSearchGraph {
    internal const string NoSolution = "{}";
    internal const string Timeout = SolveRun.TimedOutAnswer;
    internal string[] Labels { get; }
    internal int[][] Neighbors { get; }
    private readonly HashSet<(int, int)> edges;
    internal int Count => Labels.Length;

    private UnrestrictedLongestPathSearchGraph(string[] labels, int[][] neighbors, HashSet<(int, int)> edges) {
        Labels = labels;
        Neighbors = neighbors;
        this.edges = edges;
    }

    internal static bool TryTrivial(UNRESTRICTEDLONGESTPATH problem, out string answer) {
        ArgumentNullException.ThrowIfNull(problem);
        answer = NoSolution;
        if (problem.K < 0 || problem.nodes.Count == 0) {
            throw new ArgumentException("The problem must have a nonempty vertex set and nonnegative K.", nameof(problem));
        }
        if (problem.K == 0) {
            answer = "{" + problem.nodes[0] + "}";
            return true;
        }
        return problem.K > problem.nodes.Count - 1;
    }

    // Null denotes cancellation, never an exhausted search or an invalid graph.
    internal static UnrestrictedLongestPathSearchGraph? Create(UNRESTRICTEDLONGESTPATH problem, Func<bool> cancelled) {
        string[] labels = problem.nodes.ToArray();
        Dictionary<string, int> index = new(StringComparer.Ordinal);
        HashSet<int>[] neighbors = new HashSet<int>[labels.Length];
        for (int i = 0; i < labels.Length; i++) {
            if (cancelled()) return null;
            index.Add(labels[i], i);
            neighbors[i] = new HashSet<int>();
        }
        HashSet<(int, int)> edges = new();
        foreach (var edge in problem.edges) {
            if (cancelled()) return null;
            int u = index[edge.Key];
            int v = index[edge.Value];
            edges.Add(u < v ? (u, v) : (v, u));
            neighbors[u].Add(v);
            neighbors[v].Add(u);
        }
        int[][] adjacency = new int[labels.Length][];
        for (int i = 0; i < labels.Length; i++) {
            if (cancelled()) return null;
            adjacency[i] = neighbors[i].ToArray();
        }
        return new UnrestrictedLongestPathSearchGraph(labels, adjacency, edges);
    }

    internal bool HasEdge(int u, int v) => edges.Contains(u < v ? (u, v) : (v, u));

    internal string Certificate(IEnumerable<int> path) => "{" + string.Join(",", path.Select(i => Labels[i])) + "}";
}
