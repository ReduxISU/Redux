using API.Interfaces;
using API.Interfaces.Steps;
using API.Interfaces.Tripartite;
using API.Problems.NPComplete.NPC_DM3.Visualizations;

namespace API.Problems.NPComplete.NPC_DM3.Solvers;

// Incremental: it chooses one triple at a time and takes triples back, so every step's partial is the triples chosen so far.
class ThreeDimensionalMatchingBacktracking : ISolver<DM3, ChosenTriples>, IIncrementalSolver {

    // --- Fields ---
    public string solverName { get; } = "3-Dimensional Matching Backtracking";
    public string solverDefinition { get; } = "Chooses triples one at a time. Each time it takes the element not yet used with the fewest triples left that could still cover it (none of whose elements is used yet), tries each of those triples, and backs up when an element has none left.";
    public string source { get; } = "Knuth, D. E. (2000). Dancing links. Millennial Perspectives in Computer Science, 187-214 (the fewest-choices-first rule).";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Exact depth-first search WITH pruning: a triple that reuses an element is never chosen.
    public SolverType solverType { get; } = SolverType.Backtracking;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // Declared, not derived. At most C(m, n) sets of triples are reached, and choosing the next element scans every
    // element's triples, O(m) per level.
    public MathematicalFunction timeComplexity { get; } = new("C(m, n) * m", ", m = |M| (candidate triples), n = |X|");

    public ThreeDimensionalMatchingBacktracking() { }

    public string solve(DM3 problem) => Solve(problem, StepRecorder<ChosenTriples>.Off);

    // Steps (the Tripartite Matching picture, and the Search Tree picture through SolverStep.Tree): for the element taken next,
    // a Reject for each of its triples that would use an element twice (the triple and that element are named; in the tree a
    // pruned node "T5"), and an Accept for each triple it chooses (an explored node, the solution when it completes the
    // matching). When every triple of an element has failed, a Backtrack takes back the triple chosen above it (that node
    // fails). The Done step names the answer's path.
    public string Solve(DM3 problem, StepRecorder<ChosenTriples> rec) {
        int n = problem.X.Count, m = problem.M.Count;
        // A matching needs |X| triples; with fewer in M there is none (#745).
        if (m < n) {
            rec.Done(new ChosenTriples([]), false, m == 0
                ? $"M has no triples, so nothing can match the {Plural(n, "element")} of X."
                : $"M has only {Plural(m, "triple")}, but a matching needs {n}, one for each element of X. There is nothing to try.");
            return "{}";
        }
        var search = new Search(problem, rec, () => timerHasExpired);
        if (search.Uncoverable() is string missing) {
            rec.Done(new ChosenTriples([]), false, $"No triple of M uses {missing}, so no matching can cover it.");
            return "{}";
        }
        if (search.Run()) {
            var chosen = search.Chosen().Order().ToList();
            rec.Done(TripartiteFrames.Chosen(chosen), true,
                $"{TripartiteFrames.AndList(chosen.Select(i => TripartiteFrames.TripleLabel(search.Spec, i)).ToList())} "
                + $"{(chosen.Count == 1 ? "uses" : "use")} every element {(search.EqualSizes ? "of X, Y and Z" : "of X")} exactly once. "
                + $"Found after {Plural(search.Placed, "choice")}.",
                () => search.AnswerPath());
            return Certificate(problem, chosen);
        }
        rec.Done(new ChosenTriples([]), false, timerHasExpired
            ? "The solver's timer ran out before it found a matching."
            : "Every way of choosing the triples uses an element twice or leaves one out, so there is no perfect matching.");
        return "{}";
    }

    // {{Paul,Austin,Jake},{Sally,Madison,Frank}}: the chosen triples by their names, in M's order (as the brute force writes them).
    private static string Certificate(DM3 problem, List<int> chosen) =>
        "{" + string.Join(",", chosen.Select(i => "{" + string.Join(",", problem.M[i]) + "}")) + "}";

    private static string Plural(int n, string one) => $"{n:N0} {(n == 1 ? one : one + "s")}";

    // The depth-first search and its bookkeeping. Elements are numbered X first, then Y, then Z. The search tree's nodes are
    // plain ints (id, triple), and the SearchNode records are built only inside recorder lambdas.
    private sealed class Search {
        private readonly DM3 _problem;
        private readonly StepRecorder<ChosenTriples> _rec;
        private readonly Func<bool> _expired;
        private readonly int _n;
        private readonly int[][] _elementsOf;   // each triple's three elements
        private readonly int[][] _triplesOf;    // each element's triples, in M's order
        private readonly int[] _usedBy;         // the chosen triple that uses each element, or -1
        private readonly bool[] _branch;        // the elements a matching must cover
        private readonly List<(int Triple, int Id)> _path = new();
        private int _nextId = 1;
        private bool _rootSent;
        public int Placed;
        public readonly TripartiteSpec Spec;
        public readonly bool EqualSizes;

        public static readonly SearchNode Root = new(0, null, "start", SearchNodeState.Explored);

        public Search(DM3 problem, StepRecorder<ChosenTriples> rec, Func<bool> expired) {
            _problem = problem;
            _rec = rec;
            _expired = expired;
            Spec = DM3Visualization.Spec(problem);
            _n = problem.X.Count;
            int ny = problem.Y.Count, nz = problem.Z.Count, elements = _n + ny + nz;
            EqualSizes = _n == ny && _n == nz;
            _elementsOf = Spec.Triples.Select(t => new[] { t.X, _n + t.Y, _n + ny + t.Z }).ToArray();
            var triplesOf = Enumerable.Range(0, elements).Select(_ => new List<int>()).ToArray();
            for (int t = 0; t < _elementsOf.Length; t++)
                foreach (int e in _elementsOf[t]) triplesOf[e].Add(t);
            _triplesOf = triplesOf.Select(l => l.ToArray()).ToArray();
            _usedBy = Enumerable.Repeat(-1, elements).ToArray();
            // With |X| = |Y| = |Z|, |X| triples that use no element twice use every element, so any element can be branched
            // on. Otherwise only X has to be covered (the verifier asks for nothing more).
            _branch = Enumerable.Range(0, elements).Select(e => EqualSizes || e < _n).ToArray();
        }

        private string Id(int e) =>
            e < _n ? TripartiteFrames.XId(e) : e < _n + Spec.Y.Count ? TripartiteFrames.YId(e - _n) : TripartiteFrames.ZId(e - _n - Spec.Y.Count);

        private string Name(int e) => TripartiteFrames.ElementName(Spec, Id(e));

        // An element that must be covered but that no triple uses, by name; null when there is none.
        public string? Uncoverable() {
            for (int e = 0; e < _triplesOf.Length; e++)
                if (_branch[e] && _triplesOf[e].Length == 0) return Name(e);
            return null;
        }

        public IEnumerable<int> Chosen() => _path.Select(p => p.Triple);

        private ChosenTriples With(int t) => TripartiteFrames.Chosen(Chosen().Append(t));

        // The first element of t that a chosen triple already uses, or -1.
        private int Reused(int t) {
            foreach (int e in _elementsOf[t])
                if (_usedBy[e] >= 0) return e;
            return -1;
        }

        // The element still to cover with the fewest triples that reuse nothing; ties go to the first (X, then Y, then Z).
        private int Next() {
            int best = -1, bestCount = int.MaxValue;
            for (int e = 0; e < _triplesOf.Length; e++) {
                if (!_branch[e] || _usedBy[e] >= 0) continue;
                int count = 0;
                foreach (int t in _triplesOf[e])
                    if (Reused(t) < 0) count++;
                if (count < bestCount) {
                    best = e;
                    bestCount = count;
                }
            }
            return best;
        }

        private SearchNode Node(int id, int parent, int t, SearchNodeState state, string? bound = null) =>
            new(id, parent, TripartiteFrames.TripleId(t), state) { Bound = bound };

        private SearchNode[] WithRoot(SearchNode node) {
            if (_rootSent) return [node];
            _rootSent = true;
            return [Root, node];
        }

        public SearchNode[] AnswerPath() {
            var path = new List<SearchNode> { Root };
            int parent = 0;
            for (int i = 0; i < _path.Count; i++) {
                path.Add(Node(_path[i].Id, parent, _path[i].Triple, i == _path.Count - 1 ? SearchNodeState.Solution : SearchNodeState.Explored));
                parent = _path[i].Id;
            }
            return path.ToArray();
        }

        // True when the chosen triples are a matching the verifier accepts (only asked once |X| triples are chosen).
        private bool Verifies() {
            string certificate = Certificate(_problem, Chosen().Order().ToList());
            try { return _problem.defaultVerifier.verify(_problem, certificate); } catch { return false; }
        }

        // Chooses the rest of the triples, depth first; false when no matching extends the current choice.
        public bool Run() {
            if (_path.Count == _n) return true;
            if (_expired()) return false;
            int e = Next();
            if (e < 0) return false;
            int parentId = _path.Count == 0 ? 0 : _path[^1].Id;
            string element = Name(e);
            int left = _triplesOf[e].Count(t => Reused(t) < 0);
            foreach (int t in _triplesOf[e]) {
                int triple = t, id = _nextId++;
                int reused = Reused(t);
                if (reused >= 0) {
                    string again = Name(reused), by = TripartiteFrames.TripleId(_usedBy[reused]), reusedId = Id(reused);
                    _rec.Reject(() => With(triple),
                        () => $"{TripartiteFrames.TripleLabel(Spec, triple)} would use {again} twice: {by} already uses it.",
                        () => [TripartiteFrames.TripleId(triple), reusedId],
                        () => WithRoot(Node(id, parentId, triple, SearchNodeState.Pruned, $"{again} used by {by}")));
                    continue;
                }
                foreach (int x in _elementsOf[t]) _usedBy[x] = t;
                _path.Add((t, id));
                Placed++;
                bool complete = _path.Count == _n && Verifies();
                int options = left;
                _rec.Accept(() => TripartiteFrames.Chosen(Chosen()),
                    () => $"{element} has {(options == 1 ? "one triple" : $"{options} triples")} left that use no element twice. Choose {TripartiteFrames.TripleLabel(Spec, triple)}.",
                    () => [TripartiteFrames.TripleId(triple)],
                    () => WithRoot(Node(id, parentId, triple, complete ? SearchNodeState.Solution : SearchNodeState.Explored)));
                if (_path.Count == _n ? complete : Run()) return true;
                _path.RemoveAt(_path.Count - 1);
                foreach (int x in _elementsOf[t]) _usedBy[x] = -1;
                if (_expired()) return false;
                _rec.Backtrack(() => TripartiteFrames.Chosen(Chosen()),
                    () => _path.Count + 1 == _n
                        ? $"The verifier rejects these triples with {TripartiteFrames.TripleLabel(Spec, triple)}, so take it back."
                        : $"No matching of the rest goes with {TripartiteFrames.TripleLabel(Spec, triple)}, so take it back.",
                    () => [TripartiteFrames.TripleId(triple)],
                    () => WithRoot(Node(id, parentId, triple, SearchNodeState.Failed)));
            }
            return false;
        }
    }
}
