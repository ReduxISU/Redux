using API.Interfaces;
using API.Interfaces.Steps;
using API.Interfaces.Tripartite;
using API.Problems.NPComplete.NPC_DM3.Visualizations;

namespace API.Problems.NPComplete.NPC_DM3.Solvers;

// The steps 3-Dimensional Matching Backtracking records, for the Tripartite Matching picture and the Search Tree picture
// alike. For the element taken next:
//   - a Reject for each of its triples that would use an element twice, naming the triple and that element (in the tree,
//     a pruned node "T5" whose note says which triple already uses the element);
//   - an Accept for each triple it chooses (an explored node in the tree, the solution when it completes the matching);
//   - a Backtrack when nothing below a triple worked, taking it back again (that node fails);
//   - the Done, which names the answer's path in the tree, or marks the root failed when the search found none.
// Incremental: it chooses one triple at a time and takes triples back, so every step's partial is the triples chosen so far.
class ThreeDimensionalMatchingBacktracking : ISolver<DM3, ChosenTriples>, IIncrementalSolver, ISearchTreeSolver {

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

    // Chooses triples one at a time, depth first: always for the element still to cover with the fewest triples left that
    // use no element twice, trying each of those triples in M's order.
    public string Solve(DM3 problem, StepRecorder<ChosenTriples> rec) {
        int n = problem.X.Count, m = problem.M.Count;
        var search = new Search(problem, rec, () => timerHasExpired);
        var steps = search.Steps;
        // A matching needs |X| triples; with fewer in M there is none.
        if (m < n) {
            if (m == 0)
                steps.Done(new ChosenTriples([]), false, $"M has no triples, so nothing can match the {n:N0} {(n == 1 ? "element" : "elements")} of X.");
            else
                steps.Done(new ChosenTriples([]), false, $"M has only {m:N0} {(m == 1 ? "triple" : "triples")}, but a matching needs {n}, one for each element of X. There is nothing to try.");
            return "{}";
        }
        if (search.Uncoverable() is string missing) {
            steps.Done(new ChosenTriples([]), false, $"No triple of M uses {missing}, so no matching can cover it.");
            return "{}";
        }
        if (search.Run()) {
            var chosen = search.Chosen().Order().ToList();
            // With |X| = |Y| = |Z| the matching uses every element of all three; otherwise only X has to be covered.
            if (search.EqualSizes)
                steps.Done(TripartiteFrames.Chosen(chosen), true,
                    $"{Words.List(chosen.Select(i => TripartiteFrames.TripleLabel(search.Spec, i)).ToList())} {(chosen.Count == 1 ? "uses" : "use")} every element of X, Y and Z exactly once. Found after {search.Placed:N0} {(search.Placed == 1 ? "choice" : "choices")}.",
                    tree: () => search.AnswerPath(search.Chosen().Select(TripartiteFrames.TripleId).ToList()));
            else
                steps.Done(TripartiteFrames.Chosen(chosen), true,
                    $"{Words.List(chosen.Select(i => TripartiteFrames.TripleLabel(search.Spec, i)).ToList())} {(chosen.Count == 1 ? "uses" : "use")} every element of X exactly once. Found after {search.Placed:N0} {(search.Placed == 1 ? "choice" : "choices")}.",
                    tree: () => search.AnswerPath(search.Chosen().Select(TripartiteFrames.TripleId).ToList()));
            return Certificate(problem, chosen);
        }
        if (timerHasExpired)
            steps.Done(new ChosenTriples([]), false, $"The solver's timer ran out before it found a matching.");
        else
            steps.Done(new ChosenTriples([]), false, $"Every way of choosing the triples uses an element twice or leaves one out, so there is no perfect matching.",
                tree: () => search.NoAnswer());
        return "{}";
    }

    // {{Paul,Austin,Jake},{Sally,Madison,Frank}}: the chosen triples by their names, in M's order (as the brute force writes them).
    private static string Certificate(DM3 problem, List<int> chosen) =>
        "{" + string.Join(",", chosen.Select(i => "{" + string.Join(",", problem.M[i]) + "}")) + "}";

    // The depth-first search and its bookkeeping. Elements are numbered X first, then Y, then Z. It is the search tree too,
    // one depth for each triple chosen.
    private sealed class Search : DepthFirstTree {
        // A move of the search, as a step is about it: triple Triple, the depth of its search-tree node (d for the d-th triple
        // chosen on the branch), and the state the step leaves that tree node in. Reused is the element the triple would use
        // a second time (a pruned move), or -1.
        public readonly record struct Move(int Triple, int Depth, SearchNodeState Tree, int Reused = -1);

        // How the steps show a move. It records its own search tree (with a "start" root and its own node states), so it
        // gives RunSteps the tree node of each step rather than using SearchSteps.
        private RunSteps<Move, ChosenTriples> NewSteps(StepRecorder<ChosenTriples> rec) => new(rec,
            // What every picture shows: the triples chosen so far (with a pruned triple added, to show the clash), and the
            // triple, with the element it would use twice.
            partial: m => m.Tree == SearchNodeState.Pruned ? With(m.Triple) : TripartiteFrames.Chosen(Chosen()),
            focus: m => m.Reused < 0 ? [TripartiteFrames.TripleId(m.Triple)] : [TripartiteFrames.TripleId(m.Triple), Id(m.Reused)],
            // The Search Tree picture's node only: "T5", with the element it reuses and the triple using it as a pruned
            // node's note.
            tree: m => Step(m.Depth, TripartiteFrames.TripleId(m.Triple), m.Tree,
                m.Reused < 0 ? null : $"{Name(m.Reused)} used by {TripartiteFrames.TripleId(_usedBy[m.Reused])}"));

        private readonly DM3 _problem;
        private readonly Func<bool> _expired;
        private readonly int _n;
        private readonly int[][] _elementsOf;   // each triple's three elements
        private readonly int[][] _triplesOf;    // each element's triples, in M's order
        private readonly int[] _usedBy;         // the chosen triple that uses each element, or -1
        private readonly bool[] _branch;        // the elements a matching must cover
        private readonly List<int> _path = new();   // the triples chosen on the current branch
        public int Placed;
        public readonly TripartiteSpec Spec;
        public readonly bool EqualSizes;
        public readonly RunSteps<Move, ChosenTriples> Steps;

        // A branch chooses at most |X| triples.
        public Search(DM3 problem, StepRecorder<ChosenTriples> rec, Func<bool> expired) : base(problem.X.Count) {
            _problem = problem;
            _expired = expired;
            Steps = NewSteps(rec);
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

        public IEnumerable<int> Chosen() => _path;

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
            int depth = _path.Count + 1;
            string element = Name(e);
            int left = _triplesOf[e].Count(t => Reused(t) < 0);
            foreach (int t in _triplesOf[e]) {
                Branch(depth);
                int reused = Reused(t);
                // A triple that would use an element a chosen triple already uses is never chosen.
                if (reused >= 0) {
                    Steps.Reject(new Move(t, depth, SearchNodeState.Pruned, reused),
                        $"{TripartiteFrames.TripleLabel(Spec, t)} would use {Name(reused)} twice: {TripartiteFrames.TripleId(_usedBy[reused])} already uses it.");
                    continue;
                }
                foreach (int x in _elementsOf[t]) _usedBy[x] = t;
                _path.Add(t);
                Placed++;
                bool complete = _path.Count == _n && Verifies();
                var chose = new Move(t, depth, complete ? SearchNodeState.Solution : SearchNodeState.Explored);
                if (left == 1)
                    Steps.Accept(chose, $"{element} has one triple left that uses no element twice. Choose {TripartiteFrames.TripleLabel(Spec, t)}.");
                else
                    Steps.Accept(chose, $"{element} has {left} triples left that use no element twice. Choose {TripartiteFrames.TripleLabel(Spec, t)}.");
                if (_path.Count == _n ? complete : Run()) return true;
                // Nothing below this triple worked: take it back and try the element's next triple.
                _path.RemoveAt(_path.Count - 1);
                foreach (int x in _elementsOf[t]) _usedBy[x] = -1;
                if (_expired()) return false;
                var back = new Move(t, depth, SearchNodeState.Failed);
                if (_path.Count + 1 == _n)
                    Steps.Backtrack(back, $"The verifier rejects these triples with {TripartiteFrames.TripleLabel(Spec, t)}, so take it back.");
                else
                    Steps.Backtrack(back, $"No matching of the rest goes with {TripartiteFrames.TripleLabel(Spec, t)}, so take it back.");
            }
            return false;
        }
    }
}
