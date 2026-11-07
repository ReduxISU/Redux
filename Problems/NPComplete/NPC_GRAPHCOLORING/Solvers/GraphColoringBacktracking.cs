using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_GRAPHCOLORING.Solvers;

// The steps Graph Coloring Backtracking records, for the Graph picture and the Search Tree picture alike. For the node
// taken next:
//   - a Reject for each color a colored neighbour already uses, naming the clashing edge (in the tree, a pruned node
//     "a=1" whose note says which neighbour has the color);
//   - an Accept for each color it takes (an explored node in the tree, the solution when it is the last node);
//   - a Backtrack when every color below has failed, taking the color back again (that node fails);
//   - the Done, which names the answer's path in the tree.
// Incremental: it colors one node at a time and takes colors back, so every step's partial is the coloring so far.
class GraphColoringBacktracking : ISolver<GRAPHCOLORING, Groups>, IIncrementalSolver, ISearchTreeSolver {

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
    public MathematicalFunction timeComplexity { get; } = new("K^n * (n + m)", ", n = |nodes|, m = |edges|, K = number of colors");

    public GraphColoringBacktracking() { }

    public string solve(GRAPHCOLORING problem) => Solve(problem, StepRecorder<Groups>.Off);

    public string Solve(GRAPHCOLORING problem, StepRecorder<Groups> rec) {
        var nodes = problem.nodes.Distinct().ToList();
        var search = new Search(problem, nodes, rec, () => timerHasExpired);
        var steps = search.Steps;
        if (nodes.Count == 0) {
            bool ok = false;
            try { ok = problem.defaultVerifier.verify(problem, "{}"); } catch { }
            steps.Done(new Groups([]), ok, $"The graph has no nodes, so there is nothing to color.");
            return "{}";
        }
        if (problem.K <= 0) {
            steps.Done(new Groups([]), false, $"K = {problem.K} allows no colors, so the {Words.Count(nodes.Count, "node")} can't be colored.");
            return "{}";
        }

        if (search.Run()) {
            var coloring = search.Coloring();
            int used = coloring.Values.Max() + 1;
            steps.Done(new Groups(coloring), true,
                $"Every node is colored with {Words.Count(used, "color")}, within K = {problem.K}, and no edge joins two nodes of one color. Found after {search.Placed:N0} {(search.Placed == 1 ? "color" : "colors")} given.",
                tree: () => search.AnswerPath(search.PathLabels()));
            return Certificate(nodes, coloring);
        }
        if (timerHasExpired)
            steps.Done(new Groups([]), false, $"The solver's timer ran out before it found a coloring.");
        else if (search.SelfLoop >= 0)
            steps.Done(new Groups([]), false, $"Node {nodes[search.SelfLoop]} has an edge to itself, so it clashes with itself whatever its color: no coloring exists.");
        else
            steps.Done(new Groups([]), false, $"Every way of coloring the nodes runs into a clash. No coloring with {Words.Count(problem.K, "color")} exists.");
        return "{}";
    }

    // {{a,d},{b},{c}}: the color classes in color order, each in the graph's node order.
    private static string Certificate(List<string> nodes, Dictionary<string, int> coloring) =>
        "{" + string.Join(",", Enumerable.Range(0, coloring.Values.Max() + 1)
            .Select(c => nodes.Where(n => coloring[n] == c).ToList())
            .Where(cls => cls.Count > 0)
            .Select(cls => "{" + string.Join(",", cls) + "}")) + "}";

    // The depth-first search and its bookkeeping. It is the search tree too, one depth for each node colored.
    private sealed class Search : DepthFirstTree {
        // A move of the search, as a step is about it: node Node in color Color, the depth of its search-tree node (d for the
        // d-th node colored on the branch), and the state the step leaves that tree node in. Clash is the colored neighbour
        // that rules the color out (the node itself, for an edge to itself), or -1.
        public readonly record struct Move(int Node, int Color, int Depth, SearchNodeState Tree, int Clash = -1);

        // How the steps show a move. It records its own search tree (with a "start" root and its own node states), so it
        // gives RunSteps the tree node of each step rather than using SearchSteps.
        private RunSteps<Move, Groups> NewSteps(StepRecorder<Groups> rec) => new(rec,
            // What every picture shows: the coloring so far with the move's color in it (taken back again, for a failed
            // move), and the node, with the edge to the neighbour it clashes with.
            partial: m => m.Tree == SearchNodeState.Failed ? new Groups(Coloring()) : With(m.Node, m.Color),
            focus: m => m.Clash < 0 ? [_nodes[m.Node]] : [_nodes[m.Node], GraphSubsetFrames.EdgeId(_nodes[m.Node], _nodes[m.Clash])],
            // The Search Tree picture's node only: "b=1", with the clashing neighbour's color as the note of a pruned one.
            tree: m => Step(m.Depth, $"{_nodes[m.Node]}={m.Color}", m.Tree, m.Clash < 0 ? null : $"{_nodes[m.Clash]} has {m.Color}"));

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
        public readonly RunSteps<Move, Groups> Steps;

        // The decisions on the current path, root first: the node colored and its color.
        private readonly List<(int Node, int Color)> _path = new();

        // A branch colors each node at most once.
        public Search(GRAPHCOLORING problem, List<string> nodes, StepRecorder<Groups> rec, Func<bool> expired) : base(nodes.Count) {
            _nodes = nodes;
            _expired = expired;
            _rec = rec;
            Steps = NewSteps(rec);
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
            _usedCount = new int[Math.Max(_k, 0)];
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

        // The labels of the answer's path in the search tree, root first ("a=0", "b=1"): the decisions on the current path.
        public List<string> PathLabels() => _path.Select(p => $"{_nodes[p.Node]}={p.Color}").ToList();

        // Colors the rest of the graph, depth first; false when no coloring of the rest exists under the current path.
        // It takes next the uncolored node with the fewest colors left, tries each color up to one past the highest used
        // so far, skips a color a neighbour already has, and takes a color back when nothing below it works.
        public bool Run() {
            if (_colored == _nodes.Count) return true;
            if (TimerExpired()) return false;
            int limit = Math.Min(_k - 1, HighestUsed() + 1);
            int u = Next(limit);
            int depth = _path.Count + 1;
            string node = _nodes[u];
            for (int c = 0; c <= limit; c++) {
                Branch(depth);
                int clash = _selfLoop[u] ? u : ClashOf(u, c);
                if (clash >= 0) {
                    var pruned = new Move(u, c, depth, SearchNodeState.Pruned, clash);
                    if (clash == u)
                        Steps.Reject(pruned, $"Node {node} has an edge to itself, so color {c} clashes with {node} itself.");
                    else
                        Steps.Reject(pruned, $"Color {c} for node {node} clashes: its neighbour {_nodes[clash]} already has color {c}.");
                    continue;
                }
                _color[u] = c;
                _usedCount[c]++;
                _colored++;
                Placed++;
                _path.Add((u, c));
                bool last = _colored == _nodes.Count;
                var move = new Move(u, c, depth, last ? SearchNodeState.Solution : SearchNodeState.Explored);
                // Whether a neighbour of u has a color, for the step's why (worked out only while recording).
                bool neighbourColored = _rec.Enabled && _neighbours[u].Any(v => _color[v] >= 0);
                if (c > 0 && !neighbourColored)
                    Steps.Accept(move, $"Color node {node} with color {c}: no neighbour is colored yet, and every lower color has been tried.");
                else
                    Steps.Accept(move, $"Color node {node} with color {c}: {GraphGroupFrames.NeighboursUse(ColoredNeighbours(u), ColorsAround(u))}.");
                if (Run()) return true;
                _path.RemoveAt(_path.Count - 1);
                _color[u] = -1;
                _usedCount[c]--;
                _colored--;
                if (TimerExpired()) return false;
                Steps.Backtrack(new Move(u, c, depth, SearchNodeState.Failed),
                    $"The nodes after {node} can't all be colored with {node} in color {c}, so take that color back.");
            }
            return false;
        }

        private bool TimerExpired() => _expired();

        // The colored neighbours of u, by name, in the graph's node order.
        private List<string> ColoredNeighbours(int u) => _neighbours[u].Where(v => _color[v] >= 0).Select(v => _nodes[v]).ToList();

        // The colors u's colored neighbours use, each once, smallest first.
        private List<int> ColorsAround(int u) => _neighbours[u].Where(v => _color[v] >= 0).Select(v => _color[v]).Distinct().Order().ToList();
    }
}
