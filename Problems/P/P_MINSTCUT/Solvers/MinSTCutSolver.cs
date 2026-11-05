using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;

namespace API.Problems.P.P_MINSTCUT.Solvers;

// Incremental: each step pushes flow along one more augmenting path, and the next path builds on the flow pushed so far.
class MinSTCutSolver : ISolver<MINSTCUT, NetworkFlow>, IIncrementalSolver {
    public string solverName { get; } = "Edmonds-Karp Algorithm";
    public string solverDefinition { get; } = "Uses the Edmonds-Karp algorithm (BFS-based Ford-Fulkerson max-flow) to find the maximum s-t flow, then identifies the minimum cut S side via BFS on the residual graph from the source. Certificate format: {node1,node2,...} representing the S side of the minimum cut.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Edmonds-Karp finds each augmenting path via a breadth-first
    // search of the residual graph.
    public SolverType solverType { get; } = SolverType.BreadthFirstSearch;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
    // Standard Edmonds-Karp: BFS (O(V + E) via a proper adjacency-dictionary residual graph,
    // not a linear edge-list scan) finds a shortest augmenting path each round, and the
    // classic bound of O(V * E) augmentations gives O(V * E) BFS calls.
    public MathematicalFunction timeComplexity { get; } = new("V * E^2", "");

    public MinSTCutSolver() { }

    public string solve(MINSTCUT problem) => Solve(problem, StepRecorder<NetworkFlow>.Off);

    public string Solve(MINSTCUT problem, StepRecorder<NetworkFlow> rec) {
        if (problem.nodes.Count == 0) {
            rec.Done(new NetworkFlow([], [], 0, []), false, "There are no nodes, so there is no cut.");
            return "{}";
        }

        string s = problem.sourceNode, t = problem.targetNode;
        var residual = BuildResidual(problem.nodes, problem.edges);
        // Read only inside recorder lambdas and for the Done step, so it costs nothing when recording is off.
        var reader = new Lazy<FlowReader>(() => new FlowReader(problem));
        int value = 0, paths = 0;

        rec.Try(() => reader.Value.Snapshot(residual, [], 0, []),
            () => $"No flow yet. Each step finds a path from {s} to {t} with room left and pushes as much as it can.");

        while (true) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer;

            var parent = BfsAugmentingPath(residual, problem.sourceNode, problem.targetNode);
            if (parent == null) break;

            int bottleneck = FindBottleneck(residual, parent, problem.sourceNode, problem.targetNode);
            Augment(residual, parent, problem.sourceNode, problem.targetNode, bottleneck);
            value += bottleneck;
            paths++;
            int n = paths, flowValue = value;
            rec.Accept(() => reader.Value.Snapshot(residual, reader.Value.Legs(residual, parent, s, t, bottleneck), flowValue, []),
                () => reader.Value.PathCaption(residual, parent, s, t, bottleneck, n, flowValue),
                () => reader.Value.PathFocus(residual, parent, s, t, bottleneck));
        }

        HashSet<string> sSide = BfsReachable(residual, problem.sourceNode);
        if (rec != StepRecorder<NetworkFlow>.Off)
            rec.Done(reader.Value.Snapshot(residual, [], value, sSide), true, reader.Value.DoneCaption(sSide, value, paths));
        return "{" + string.Join(",", sSide) + "}";
    }

    /// <summary>
    /// Reads the flow off the residual graph for the picture. The residual graph merges parallel edges and both directions
    /// of a pair, so it holds only the net flow from u to v: capacity(u,v) - residual[u][v]. That net flow is drawn on the
    /// edge it runs along (u to v when positive, v to u when negative), which is a valid flow since 0 &lt;= net &lt;=
    /// capacity(u,v) and 0 &lt;= -net &lt;= capacity(v,u).
    /// </summary>
    private sealed class FlowReader {
        private readonly MINSTCUT _problem;
        // The summed capacity of every listed (from, to) pair, in listed order.
        private readonly Dictionary<(string From, string To), int> _capacity = new();

        public FlowReader(MINSTCUT problem) {
            _problem = problem;
            foreach (var (from, to, weight) in problem.edges)
                _capacity[(from, to)] = _capacity.GetValueOrDefault((from, to)) + weight;
        }

        private int Capacity(string from, string to) => _capacity.GetValueOrDefault((from, to));

        // The net flow from u to v.
        private int Net(Dictionary<string, Dictionary<string, int>> residual, string u, string v) =>
            Capacity(u, v) - residual[u].GetValueOrDefault(v);

        private int Units(Dictionary<string, Dictionary<string, int>> residual, string from, string to) =>
            from == to ? 0 : Math.Max(0, Net(residual, from, to));

        public NetworkFlow Snapshot(Dictionary<string, Dictionary<string, int>> residual, IEnumerable<FlowArc> legs, int value, IEnumerable<string> side) =>
            new(_capacity.Keys.Select(e => KeyValuePair.Create(FlowFrames.ArcId(e.From, e.To), Units(residual, e.From, e.To))), legs, value, side);

        private static List<string> PathNodes(Dictionary<string, string> parent, string s, string t) {
            var nodes = new List<string>();
            for (string node = t; node != s; node = parent[node]) nodes.Add(node);
            nodes.Add(s);
            nodes.Reverse();
            return nodes;
        }

        // Each hop x to y of the path just pushed moved `pushed` units: first taking back what y had sent to x, then along x
        // to y. Read after the push, when the net flow from x to y is `pushed` more than it was.
        private List<(string X, string Y, int Back, int Ahead)> Hops(Dictionary<string, Dictionary<string, int>> residual,
            Dictionary<string, string> parent, string s, string t, int pushed) {
            var nodes = PathNodes(parent, s, t);
            var hops = new List<(string, string, int, int)>();
            for (int i = 1; i < nodes.Count; i++) {
                string x = nodes[i - 1], y = nodes[i];
                int before = Net(residual, x, y) - pushed;
                int back = Math.Min(pushed, Math.Max(0, -before));
                hops.Add((x, y, back, pushed - back));
            }
            return hops;
        }

        public List<FlowArc> Legs(Dictionary<string, Dictionary<string, int>> residual, Dictionary<string, string> parent, string s, string t, int pushed) {
            var legs = new List<FlowArc>();
            foreach (var (x, y, back, ahead) in Hops(residual, parent, s, t, pushed)) {
                if (back > 0) legs.Add(new FlowArc(FlowFrames.ArcId(y, x), false, back));
                if (ahead > 0) legs.Add(new FlowArc(FlowFrames.ArcId(x, y), true, ahead));
            }
            return legs;
        }

        public string[] PathFocus(Dictionary<string, Dictionary<string, int>> residual, Dictionary<string, string> parent, string s, string t, int pushed) {
            var focus = new List<string> { FlowFrames.NodeId(s) };
            foreach (var (x, y, back, ahead) in Hops(residual, parent, s, t, pushed)) {
                if (back > 0) focus.Add(FlowFrames.ArcId(y, x));
                if (ahead > 0) focus.Add(FlowFrames.ArcId(x, y));
                focus.Add(FlowFrames.NodeId(y));
            }
            return focus.ToArray();
        }

        public string PathCaption(Dictionary<string, Dictionary<string, int>> residual, Dictionary<string, string> parent, string s, string t,
            int pushed, int n, int value) {
            var hops = Hops(residual, parent, s, t, pushed);
            string along = s + string.Concat(hops.Select(h => (h.Ahead > 0 ? " → " : " ⇠ ") + h.Y));
            var back = hops.Where(h => h.Back > 0).Select(h => $"{h.Y} → {h.X}").ToList();
            var full = hops.Where(h => h.Ahead > 0 && Units(residual, h.X, h.Y) == Capacity(h.X, h.Y)).Select(h => $"{h.X} → {h.Y}").ToList();
            return $"Path {n}: push {pushed} along {along}."
                + (back.Count > 0 ? $" It runs backward over {FlowFrames.List(back)}, taking back flow sent there earlier." : "")
                + (full.Count > 0 ? $" {FlowFrames.List(full)} {(full.Count == 1 ? "is" : "are")} now full." : "")
                + $" Flow is now {value}.";
        }

        public string DoneCaption(HashSet<string> side, int value, int paths) {
            string s = _problem.sourceNode, t = _problem.targetNode;
            if (paths == 0) return $"There is no path from {s} to {t} with room, so the maximum flow and the minimum cut are both 0.";
            string sSide = FlowFrames.Braces(_problem.nodes.Where(side.Contains));
            int cut = _capacity.Keys.Count(e => side.Contains(e.From) && !side.Contains(e.To));
            return $"No augmenting path is left. The nodes still reachable from {s} form S = {sSide}. "
                + (cut == 1
                    ? $"The only edge from S to T is full, and its capacity, {value}, equals the maximum flow."
                    : $"The {cut} edges from S to T are full and add up to {value}, equal to the maximum flow.");
        }
    }

    internal static int GetMinCutCapacity(MINSTCUT problem) {
        var residual = BuildResidual(problem.nodes, problem.edges);

        while (true) {
            var parent = BfsAugmentingPath(residual, problem.sourceNode, problem.targetNode);
            if (parent == null) break;
            int bottleneck = FindBottleneck(residual, parent, problem.sourceNode, problem.targetNode);
            Augment(residual, parent, problem.sourceNode, problem.targetNode, bottleneck);
        }

        HashSet<string> sSide = BfsReachable(residual, problem.sourceNode);
        return CutCapacity(problem.edges, sSide);
    }

    internal static int CutCapacity(List<(string from, string to, int weight)> edges, HashSet<string> S) {
        int total = 0;
        foreach (var (from, to, weight) in edges) {
            if (S.Contains(from) && !S.Contains(to))
                total += weight;
        }
        return total;
    }

    private static Dictionary<string, Dictionary<string, int>> BuildResidual(
        List<string> nodes, List<(string from, string to, int weight)> edges) {
        var residual = new Dictionary<string, Dictionary<string, int>>();
        foreach (string node in nodes)
            residual[node] = new Dictionary<string, int>();

        foreach (var (from, to, weight) in edges) {
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
        string source, string target) {
        var parent = new Dictionary<string, string>();
        parent[source] = source;
        var queue = new Queue<string>();
        queue.Enqueue(source);

        while (queue.Count > 0) {
            string current = queue.Dequeue();
            if (current == target) return parent;

            foreach (var (next, cap) in residual[current]) {
                if (cap > 0 && !parent.ContainsKey(next)) {
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
        string source, string target) {
        int bottleneck = int.MaxValue;
        string node = target;
        while (node != source) {
            string prev = parent[node];
            bottleneck = Math.Min(bottleneck, residual[prev][node]);
            node = prev;
        }
        return bottleneck;
    }

    private static void Augment(
        Dictionary<string, Dictionary<string, int>> residual,
        Dictionary<string, string> parent,
        string source, string target, int bottleneck) {
        string node = target;
        while (node != source) {
            string prev = parent[node];
            residual[prev][node] -= bottleneck;
            residual[node][prev] += bottleneck;
            node = prev;
        }
    }

    private static HashSet<string> BfsReachable(
        Dictionary<string, Dictionary<string, int>> residual,
        string source) {
        var reachable = new HashSet<string>();
        var queue = new Queue<string>();
        reachable.Add(source);
        queue.Enqueue(source);

        while (queue.Count > 0) {
            string current = queue.Dequeue();
            foreach (var (next, cap) in residual[current]) {
                if (cap > 0 && !reachable.Contains(next)) {
                    reachable.Add(next);
                    queue.Enqueue(next);
                }
            }
        }

        return reachable;
    }
}
