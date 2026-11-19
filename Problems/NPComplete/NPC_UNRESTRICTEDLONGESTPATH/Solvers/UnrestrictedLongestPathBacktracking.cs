using API.Interfaces;

namespace API.Problems.NPComplete.NPC_UNRESTRICTEDLONGESTPATH.Solvers;

class UnrestrictedLongestPathBacktracking : ISolver<UNRESTRICTEDLONGESTPATH> {
    public string solverName { get; } = "Unrestricted Longest Path Backtracking";
    public string solverDefinition { get; } =
        "Extends simple paths only along edges to unused vertices, starting at every vertex. " +
        "Prunes a prefix when too few unused vertices remain reachable from its endpoint. " +
        "Returns a path, {} for an exhaustive NO, or Redux's stopped message.";
    public string source { get; } = "Exact depth-first backtracking with a reachable-vertex upper bound.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = Array.Empty<string>();
    private volatile bool expired;
    public bool timerHasExpired { get => expired; set => expired = value; }
    public SolverType solverType { get; } = SolverType.Backtracking;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Factorial;
    public MathematicalFunction timeComplexity { get; } = new("(n + m) * n!", ", n = |V|, m = |E|; conservative worst-case bound, excluding parsing");
    public MathematicalFunction spaceComplexity { get; } = new("n + m", ", n = |V|, m = |E|; excluding parsing");

    public UnrestrictedLongestPathBacktracking() { }

    public string solve(UNRESTRICTEDLONGESTPATH problem) {
        if (timerHasExpired) return UnrestrictedLongestPathSearchGraph.Timeout;
        if (UnrestrictedLongestPathSearchGraph.TryTrivial(problem, out string answer)) return answer;
        var graph = UnrestrictedLongestPathSearchGraph.Create(problem, () => timerHasExpired);
        if (graph is null) return UnrestrictedLongestPathSearchGraph.Timeout;
        int target = checked((int)problem.K + 1);
        int[] path = new int[target];
        int[] nextNeighbor = new int[target];
        bool[] used = new bool[graph.Count];
        int[] marks = new int[graph.Count];
        int[] queue = new int[graph.Count];
        int stamp = 0;

        bool EnoughReachable(int endpoint, int length) {
            int needed = target - length;
            if (needed == 0) return true;
            if (stamp == int.MaxValue) {
                Array.Clear(marks);
                stamp = 0;
            }
            stamp++;
            int head = 0, tail = 1, available = 0;
            queue[0] = endpoint;
            marks[endpoint] = stamp;
            while (head < tail) {
                if (timerHasExpired) return false;
                foreach (int neighbor in graph.Neighbors[queue[head++]]) {
                    if (timerHasExpired) return false;
                    if (used[neighbor] || marks[neighbor] == stamp) continue;
                    marks[neighbor] = stamp;
                    if (++available >= needed) return true;
                    queue[tail++] = neighbor;
                }
            }
            return false;
        }

        for (int start = 0; start < graph.Count; start++) {
            if (timerHasExpired) return UnrestrictedLongestPathSearchGraph.Timeout;
            int depth = 0;
            path[0] = start;
            nextNeighbor[0] = 0;
            used[start] = true;
            if (!EnoughReachable(start, 1)) {
                used[start] = false;
                continue;
            }
            while (depth >= 0) {
                if (timerHasExpired) return UnrestrictedLongestPathSearchGraph.Timeout;
                if (depth + 1 == target) return graph.Certificate(path);
                int current = path[depth];
                if (nextNeighbor[depth] == graph.Neighbors[current].Length) {
                    used[current] = false;
                    depth--;
                    continue;
                }
                int neighbor = graph.Neighbors[current][nextNeighbor[depth]++];
                if (used[neighbor]) continue;
                used[neighbor] = true;
                path[++depth] = neighbor;
                nextNeighbor[depth] = 0;
                if (!EnoughReachable(neighbor, depth + 1)) {
                    used[neighbor] = false;
                    depth--;
                }
            }
        }
        return timerHasExpired ? UnrestrictedLongestPathSearchGraph.Timeout : UnrestrictedLongestPathSearchGraph.NoSolution;
    }
}
