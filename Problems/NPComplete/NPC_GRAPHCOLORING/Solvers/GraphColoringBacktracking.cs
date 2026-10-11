using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_GRAPHCOLORING.Solvers;

// Incremental: it colors one node at a time and takes colors back, so every step's partial is the coloring so far.
class GraphColoringBacktracking : ISolver<GRAPHCOLORING, Groups>, IIncrementalSolver {

    // --- Fields ---
    public string solverName { get; } = "Graph Coloring Backtracking";
    public string solverDefinition { get; } = "Colors the nodes one at a time, always taking next the node with the fewest colors left (the most neighbours' colors already used, then the most neighbours). It tries each color its colored neighbours do not use, and backs up when a node has no color left. A node never takes a color more than one past the highest color used so far, since renaming the colors gives the same coloring.";
    public string source { get; } = "Brélaz, D. (1979). New methods to color the vertices of a graph. Communications of the ACM, 22(4), 251-256.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Exact depth-first search WITH pruning: a color a neighbour already uses is never tried.
    public SolverType solverType { get; } = SolverType.Backtracking;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // Declared, not derived. At most K choices per node over n levels; picking the next node scans every node and its
    // neighbours, O(n + m) per level.
    public string complexity { get; } = "O(K^n * (n + m)), n = |nodes|, m = |edges|, K = number of colors";

    public GraphColoringBacktracking() { }

    public string solve(GRAPHCOLORING problem) => Solve(problem, StepRecorder<Groups>.Off);

    // Steps (the Graph picture, and the Search Tree picture through SolverStep.Tree): for the node taken next, a Reject for each
    // color a colored neighbour already uses (the clashing edge is named; in the tree a pruned node "a=1"), and an Accept for
    // each color it takes (an explored node, the solution when it is the last node). When every color of a node has failed,
    // a Backtrack takes back the color of the node above it (that node fails). The Done step names the answer's path.
    public string Solve(GRAPHCOLORING problem, StepRecorder<Groups> rec) {
        var nodes = problem.nodes.Distinct().ToList();
        if (nodes.Count == 0) {
            bool ok = false;
            try { ok = problem.defaultVerifier.verify(problem, "{}"); } catch { }
            rec.Done(new Groups([]), ok, "The graph has no nodes, so there is nothing to color.");
            return "{}";
        }
        if (problem.K <= 0) {
            rec.Done(new Groups([]), false, $"K = {problem.K} allows no colors, so the {nodes.Count} {(nodes.Count == 1 ? "node" : "nodes")} can't be colored.");
            return "{}";
        }

        var search = new Search(problem, nodes, rec, () => timerHasExpired);
        if (search.Run()) {
            var coloring = search.Coloring();
            int used = coloring.Values.Max() + 1;
            rec.Done(new Groups(coloring), true,
                $"Every node is colored with {used} {(used == 1 ? "color" : "colors")}, within K = {problem.K}, and no edge joins two nodes of one color. "
                + $"Found after {Count(search.Placed, "color")} given.",
                () => search.AnswerPath());
            return Certificate(nodes, coloring);
        }
        rec.Done(new Groups([]), false, timerHasExpired
            ? "The solver's timer ran out before it found a coloring."
            : search.SelfLoop >= 0
                ? $"Node {nodes[search.SelfLoop]} has an edge to itself, so it clashes with itself whatever its color: no coloring exists."
                : $"Every way of coloring the nodes runs into a clash. No coloring with {problem.K} {(problem.K == 1 ? "color" : "colors")} exists.");
        return "{}";
    }

    // {{a,d},{b},{c}}: the color classes in color order, each in the graph's node order.
    private static string Certificate(List<string> nodes, Dictionary<string, int> coloring) =>
        "{" + string.Join(",", Enumerable.Range(0, coloring.Values.Max() + 1)
            .Select(c => nodes.Where(n => coloring[n] == c).ToList())
            .Where(cls => cls.Count > 0)
            .Select(cls => "{" + string.Join(",", cls) + "}")) + "}";

    private static string Count(int count, string what) => $"{count:N0} {what}{(count == 1 ? "" : "s")}";

    // The depth-first search and its bookkeeping. The search tree's nodes are plain ints here (id, parent, color), and the
    // SearchNode records are built only inside recorder lambdas, so solving without steps allocates nothing for the tree.
    private sealed class Search {
        private readonly List<string> _nodes;
        private readonly Func<bool> _expired;   // the solver's timer
        private readonly StepRecorder<Groups> _rec;
        private readonly int _k;
        private readonly int[][] _neighbours;   // distinct neighbours, self excluded
        private readonly bool[] _selfLoop;
        private readonly int[] _color;          // -1 while uncolored
        private readonly int[] _usedCount;      // how many nodes have each color
        private int _colored;
        public int Placed;                      // colors given in all, counting the ones taken back
        public readonly int SelfLoop = -1;      // a node with an edge to itself, or -1

        // The decisions on the current path, root first: the node colored, its color and its tree node id.
        private readonly List<(int Node, int Color, int Id)> _path = new();
        private int _nextId = 1;
        private bool _rootSent;

        public static readonly SearchNode Root = new(0, null, "start", SearchNodeState.Explored);

        public Search(GRAPHCOLORING problem, List<string> nodes, StepRecorder<Groups> rec, Func<bool> expired) {
            _nodes = nodes;
            _expired = expired;
            _rec = rec;
            _k = problem.K;
            var index = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < nodes.Count; i++) index[nodes[i]] = i;
            var adjacent = Enumerable.Range(0, nodes.Count).Select(_ => new HashSet<int>()).ToArray();
            _selfLoop = new bool[nodes.Count];
            foreach (var e in problem.edges) {
                if (!index.TryGetValue(e.Key, out int a) || !index.TryGetValue(e.Value, out int b)) continue;
                if (a == b) {
                    _selfLoop[a] = true;
                    if (SelfLoop < 0) SelfLoop = a;
                    continue;
                }
                adjacent[a].Add(b);
                adjacent[b].Add(a);
            }
            _neighbours = adjacent.Select(s => s.Order().ToArray()).ToArray();
            _color = Enumerable.Repeat(-1, nodes.Count).ToArray();
            _usedCount = new int[_k];
        }

        public Dictionary<string, int> Coloring() {
            var coloring = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < _nodes.Count; i++)
                if (_color[i] >= 0) coloring[_nodes[i]] = _color[i];
            return coloring;
        }

        // The coloring so far plus node u in color c (for a step about that color).
        private Groups With(int u, int c) {
            var coloring = Coloring();
            coloring[_nodes[u]] = c;
            return new Groups(coloring);
        }

        // The highest color any node has, or -1 when none has one.
        private int HighestUsed() {
            for (int c = _k - 1; c >= 0; c--)
                if (_usedCount[c] > 0) return c;
            return -1;
        }

        // The colored neighbour of u that has color c, or -1 when none has.
        private int ClashOf(int u, int c) {
            foreach (int v in _neighbours[u])
                if (_color[v] == c) return v;
            return -1;
        }

        // The uncolored node with the fewest colors left to try (the most neighbour colors already used); ties go to the one
        // with the most neighbours, then to the first in the graph's order.
        private int Next(int limit) {
            int best = -1, bestFree = int.MaxValue, bestDegree = -1;
            for (int u = 0; u < _nodes.Count; u++) {
                if (_color[u] >= 0) continue;
                int free = 0;
                if (!_selfLoop[u])
                    for (int c = 0; c <= limit; c++)
                        if (ClashOf(u, c) < 0) free++;
                int degree = _neighbours[u].Length;
                if (free < bestFree || (free == bestFree && degree > bestDegree)) {
                    best = u;
                    bestFree = free;
                    bestDegree = degree;
                }
            }
            return best;
        }

        private SearchNode Node(int id, int parent, int u, int c, SearchNodeState state, string? bound = null) =>
            new(id, parent, $"{_nodes[u]}={c}", state) { Bound = bound };

        // The root patch, sent with the first step only.
        private SearchNode[] WithRoot(SearchNode node) {
            if (_rootSent) return [node];
            _rootSent = true;
            return [Root, node];
        }

        // The answer's path in the search tree, root first, the last decision the solution.
        public SearchNode[] AnswerPath() {
            var path = new List<SearchNode> { Root };
            int parent = 0;
            for (int i = 0; i < _path.Count; i++) {
                var (u, c, id) = _path[i];
                path.Add(Node(id, parent, u, c, i == _path.Count - 1 ? SearchNodeState.Solution : SearchNodeState.Explored));
                parent = id;
            }
            return path.ToArray();
        }

        // Colors the rest of the graph, depth first; false when no coloring of the rest exists under the current path.
        public bool Run() {
            if (_colored == _nodes.Count) return true;
            if (TimerExpired()) return false;
            int limit = Math.Min(_k - 1, HighestUsed() + 1);
            int u = Next(limit);
            int parentId = _path.Count == 0 ? 0 : _path[^1].Id;
            string node = _nodes[u];
            for (int c = 0; c <= limit; c++) {
                int color = c, id = _nextId++;
                int clash = _selfLoop[u] ? u : ClashOf(u, c);
                if (clash >= 0) {
                    string other = _nodes[clash];
                    _rec.Reject(() => With(u, color),
                        () => clash == u
                            ? $"Node {node} has an edge to itself, so color {color} clashes with {node} itself."
                            : $"Color {color} for node {node} clashes: its neighbour {other} already has color {color}.",
                        () => [node, GraphSubsetFrames.EdgeId(node, other)],
                        () => WithRoot(Node(id, parentId, u, color, SearchNodeState.Pruned, $"{other} has {color}")));
                    continue;
                }
                _color[u] = c;
                _usedCount[c]++;
                _colored++;
                Placed++;
                _path.Add((u, c, id));
                bool last = _colored == _nodes.Count;
                _rec.Accept(() => With(u, color),
                    () => $"Color node {node} with color {color}: {Why(u, color)}.",
                    () => [node],
                    () => WithRoot(Node(id, parentId, u, color, last ? SearchNodeState.Solution : SearchNodeState.Explored)));
                if (Run()) return true;
                _path.RemoveAt(_path.Count - 1);
                _color[u] = -1;
                _usedCount[c]--;
                _colored--;
                if (TimerExpired()) return false;
                _rec.Backtrack(() => new Groups(Coloring()),
                    () => $"The nodes after {node} can't all be colored with {node} in color {color}, so take that color back.",
                    () => [node],
                    () => WithRoot(Node(id, parentId, u, color, SearchNodeState.Failed)));
            }
            return false;
        }

        private bool TimerExpired() => _expired();

        // Why color c is free for u: its colored neighbours and the colors they use (only called when recording).
        private string Why(int u, int c) {
            var colored = _neighbours[u].Where(v => _color[v] >= 0).ToList();
            if (colored.Count == 0) return c == 0 ? "no neighbour is colored yet" : "no neighbour is colored yet, and every lower color has been tried";
            var colors = colored.Select(v => _color[v]).Distinct().Order().ToList();
            string names = GraphGroupFrames.And(colored.Select(v => _nodes[v]).ToList());
            return $"{(colored.Count == 1 ? "neighbour" : "neighbours")} {names} {(colored.Count == 1 ? "uses" : "use")} {(colors.Count == 1 ? "color" : "colors")} "
                + GraphGroupFrames.And(colors.Select(x => x.ToString()).ToList());
        }
    }
}
