using API.Interfaces;
using API.Interfaces.Geometry;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_CONVEXHULL.Visualizations;

namespace API.Problems.NPComplete.NPC_CONVEXHULL.Solvers;

// Incremental: it builds the hull from small hulls, merging two at a time, and each merge walks a chain one corner at a
// time, pushing and popping.
class ConvexHullSolver : ISolver<CONVEXHULL, HullBuild>, IIncrementalSolver {

    public string solverName { get; } = "Convex Hull Divide and Conquer";
    public string solverDefinition { get; } = "Computes the convex hull of a set of 2D points using divide and conquer: the points are sorted by (x, then y), split in half, each half is solved recursively, and the two hulls are merged in linear time. The hull is returned counterclockwise starting at the lowest-leftmost point; collinear points on the boundary are not included.";
    public string source { get; } = "https://doi.org/10.1145/359423.359430";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Bektur Akkabakov" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Splits the point set in half, solves each half recursively, then
    // merges the two hulls -- the textbook divide-and-conquer shape.
    public SolverType solverType { get; } = SolverType.DivideAndConquer;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
    // Sort is O(n log n); the divide-and-conquer merge is a linear merge of the two hulls' vertices
    // followed by a linear monotone-chain pass, giving the classic T(n) = 2T(n/2) + O(n) recurrence.
    public string complexity { get; } = "O(n log n)";

    public ConvexHullSolver() { }

    public string solve(CONVEXHULL problem) => Solve(problem, StepRecorder<HullBuild>.Off);

    // Steps: the sort; a Try per smallest piece (at most 3 points, its hull made at once); per merge, a Try to start it, a Try
    // per corner pushed onto the chain and a Reject per corner popped off it, and a Try with the merged hull; then an Accept
    // with the whole hull, and Done with the verifier's verdict.
    public string Solve(CONVEXHULL problem, StepRecorder<HullBuild> rec) {
        if (timerHasExpired)
            return "timeout";

        // Sort a copy by (x, then y) and drop duplicate points; the problem's own list is left untouched.
        List<(double x, double y)> points = problem.points
            .OrderBy(p => p.x).ThenBy(p => p.y)
            .Distinct()
            .ToList();

        var build = new Build(problem, rec);
        build.Sorted(points);
        // No points: no pieces to make, and the hull is empty.
        List<(double x, double y)> hull = points.Count == 0 ? new() : ConvexHullDC(points, build);

        problem.convexHull = hull;
        problem.solution = Format(hull);
        // Done and its verdict only when recording: the verifier checks every point against every edge, O(n h), which would
        // make a plain solve of a large hull slower than the hull itself.
        if (!ReferenceEquals(rec, StepRecorder<HullBuild>.Off)) {
            build.Finish(hull);
            rec.Done(build.Answer(hull), problem.defaultVerifier.verify(problem, problem.solution), build.DoneCaption(hull));
        }
        return problem.solution;
    }

    // Divide & Conquer. The hull is returned counterclockwise, starting at the lexicographically
    // smallest point (lowest x, then lowest y). Collinear boundary points are NOT vertices of the hull.

    private List<(double x, double y)> ConvexHullDC(List<(double x, double y)> pts, Build build) {
        if (pts.Count <= 3) {
            var piece = Chain(pts, null);
            build.Piece(pts, piece);
            return piece;
        }

        int mid = pts.Count / 2;

        var left = ConvexHullDC(pts.GetRange(0, mid), build);
        var right = ConvexHullDC(pts.GetRange(mid, pts.Count - mid), build);

        return Merge(left, right, build);
    }

    // Merges two hulls in linear time. Every vertex of the combined hull is a vertex of one of the
    // two sub-hulls, so it is enough to rebuild the hull from the union of their vertices. Each
    // hull's vertices are put in (x, y) order by a linear merge of its lower and upper chains, and
    // since the halves were split by index of the sorted points, every left vertex precedes every
    // right vertex, so the two lists just concatenate. Correct for equal x values and collinear points.
    private List<(double x, double y)> Merge(List<(double x, double y)> left, List<(double x, double y)> right, Build build) {
        var all = SortedVertices(left);
        all.AddRange(SortedVertices(right));
        build.StartMerge(left, right, all.Count);
        var merged = Chain(all, build);
        build.EndMerge(left, right, merged);
        return merged;
    }

    // A hull from Chain is counterclockwise starting at its lexicographically smallest vertex: the
    // lower chain (indices 0..max) ascends in (x, y), and the upper chain (the rest) ascends when
    // read backwards. Merging the two chains yields all vertices in (x, y) order in O(h).
    internal static List<(double x, double y)> SortedVertices(List<(double x, double y)> hull) {
        int h = hull.Count;
        var result = new List<(double x, double y)>(h);
        if (h <= 2) {
            result.AddRange(hull);
            if (h == 2 && CompareXY(result[0], result[1]) > 0)
                result.Reverse();
            return result;
        }

        int max = 0;
        for (int i = 1; i < h; i++)
            if (CompareXY(hull[i], hull[max]) > 0)
                max = i;

        int lo = 0;          // walks the lower chain 0..max forwards
        int up = h - 1;      // walks the upper chain h-1..max+1 backwards
        while (lo <= max || up > max) {
            if (up <= max || (lo <= max && CompareXY(hull[lo], hull[up]) <= 0))
                result.Add(hull[lo++]);
            else
                result.Add(hull[up--]);
        }
        return result;
    }

    private static int CompareXY((double x, double y) a, (double x, double y) b) {
        int c = a.x.CompareTo(b.x);
        return c != 0 ? c : a.y.CompareTo(b.y);
    }

    // Monotone chain over points already sorted by (x, y) with no duplicates.
    // Returns the strictly convex hull counterclockwise from the first point. With a build, every push and pop is recorded
    // (a merge); without one, nothing is (a smallest piece, recorded as a whole).
    private List<(double x, double y)> Chain(List<(double x, double y)> sorted, Build? build) {
        int n = sorted.Count;
        if (n <= 1)
            return new List<(double x, double y)>(sorted);

        var h = new List<(double x, double y)>();
        for (int i = 0; i < n; i++) {
            while (h.Count >= 2 && Orientation(h[h.Count - 2], h[h.Count - 1], sorted[i]) <= 0) {
                var popped = h[h.Count - 1];
                h.RemoveAt(h.Count - 1);
                build?.Pop(h, popped, sorted[i], top: false);
            }
            h.Add(sorted[i]);
            build?.Push(h, top: false);
        }
        int lower = h.Count + 1;
        for (int i = n - 2; i >= 0; i--) {
            while (h.Count >= lower && Orientation(h[h.Count - 2], h[h.Count - 1], sorted[i]) <= 0) {
                var popped = h[h.Count - 1];
                h.RemoveAt(h.Count - 1);
                build?.Pop(h, popped, sorted[i], top: true);
            }
            h.Add(sorted[i]);
            build?.Push(h, top: true);
        }
        h.RemoveAt(h.Count - 1);
        return h;
    }

    private static double Orientation((double x, double y) a,
                               (double x, double y) b,
                               (double x, double y) c) {
        return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
    }

    private string Format(List<(double x, double y)> pts) {
        return "(" + string.Join(", ", pts.Select(p => $"({p.x},{p.y})")) + ")";
    }

    // What the run has built so far, for its steps: the finished hulls on the table, the two being merged, the chain the merge
    // has walked and the points dropped. Its ids are read only inside recorder lambdas (and for Done), so they cost nothing
    // when recording is off; keeping the table itself costs a few list operations per piece and merge.
    private sealed class Build {
        private readonly CONVEXHULL _problem;
        private readonly StepRecorder<HullBuild> _rec;
        private Dictionary<(double X, double Y), string>? _ids;
        private readonly List<List<(double x, double y)>> _table = new();
        private readonly HashSet<(double x, double y)> _dropped = new();
        private List<(double x, double y)>? _left, _right;
        private List<(double x, double y)> _chain = new();

        public Build(CONVEXHULL problem, StepRecorder<HullBuild> rec) {
            _problem = problem;
            _rec = rec;
        }

        private string Id((double x, double y) p) => (_ids ??= GeometryFrames.IdsByPoint(ConvexHullVisualization.Spec(_problem)))[p];
        private string[] Ids(IEnumerable<(double x, double y)> points) => points.Select(Id).ToArray();
        private string Names(IEnumerable<(double x, double y)> points) => GeometryFrames.AndList(Ids(points));

        private HullBuild Partial() => new(
            _table.Where(h => !ReferenceEquals(h, _left) && !ReferenceEquals(h, _right)).Select(Ids),
            _left == null ? [] : Ids(_left), _right == null ? [] : Ids(_right), Ids(_chain), Ids(_dropped));

        public HullBuild Answer(List<(double x, double y)> hull) => new(hull.Count == 0 ? [] : [Ids(hull)], [], [], [], Ids(_dropped));

        public void Sorted(List<(double x, double y)> sorted) {
            int n = _problem.points.Count;
            if (sorted.Count <= 1) return;
            _rec.Try(Partial, () => {
                string repeats = sorted.Count < n
                    ? $" {Plural(n - sorted.Count, "point repeats", "points repeat")} another and {(n - sorted.Count == 1 ? "is" : "are")} left out."
                    : "";
                return sorted.Count <= 3
                    ? $"Sort the {Plural(sorted.Count, "point", "points")} by x, then y: {Names(sorted)}.{repeats}"
                    : $"Sort the {sorted.Count} points by x, then y: {Names(sorted)}.{repeats} Then split the list in half, again and again, until each piece has at most 3 points.";
            });
        }

        // A smallest piece (at most 3 points) and its hull, made at once.
        public void Piece(List<(double x, double y)> points, List<(double x, double y)> hull) {
            _table.Add(hull);
            foreach (var p in points.Except(hull)) _dropped.Add(p);
            _rec.Try(Partial, () => points.Count switch {
                1 => $"{Id(points[0])} on its own is its own hull.",
                2 => $"Smallest piece: {Names(points)}. Its hull is the segment between them.",
                _ when hull.Count == 2 => $"Smallest piece: {Names(points)}. They are on one line, so {Names(points.Except(hull))} in the middle is dropped.",
                _ => $"Smallest piece: {Names(points)}. Its hull is the triangle they make.",
            }, () => Ids(hull));
        }

        public void StartMerge(List<(double x, double y)> left, List<(double x, double y)> right, int corners) {
            _left = left;
            _right = right;
            _chain = new();
            _rec.Try(Partial, () =>
                $"Merge the left hull ({Names(left)}) with the right hull ({Names(right)}). Walk their {corners} corners left to right for the bottom edge, then back for the top edge, keeping only left turns.");
        }

        public void Push(List<(double x, double y)> chain, bool top) {
            _chain = chain;
            var p = chain[^1];
            _rec.Try(Partial, () => top && chain.Count > 2 && p == chain[0]
                ? $"Back at {Id(p)}: the top edge closes the hull."
                : $"Add {Id(p)} to the {(top ? "top" : "bottom")} edge.", () => [Id(p)]);
        }

        // "The turn at P4 (from P2 to P7) is clockwise, so pop P4 from the bottom edge."
        public void Pop(List<(double x, double y)> chain, (double x, double y) popped, (double x, double y) next, bool top) {
            _chain = chain;
            var before = chain[^1];
            _rec.Reject(Partial, () => {
                string edge = top ? "top" : "bottom";
                return Orientation(before, popped, next) == 0
                    ? $"{Id(before)}, {Id(popped)} and {Id(next)} are on one line, so pop {Id(popped)} from the {edge} edge."
                    : $"The turn at {Id(popped)} (from {Id(before)} to {Id(next)}) is clockwise, so pop {Id(popped)} from the {edge} edge.";
            }, () => [Id(popped)]);
        }

        public void EndMerge(List<(double x, double y)> left, List<(double x, double y)> right, List<(double x, double y)> merged) {
            var gone = left.Concat(right).Except(merged).ToList();
            foreach (var p in gone) _dropped.Add(p);
            int at = _table.FindIndex(h => ReferenceEquals(h, left));
            _table.RemoveAll(h => ReferenceEquals(h, left) || ReferenceEquals(h, right));
            _table.Insert(at, merged);
            _left = _right = null;
            _chain = new();
            _rec.Try(Partial, () => $"The merged hull is {Names(merged)}."
                + (gone.Count == 0 ? " Every corner of both hulls stays."
                    : $" {Names(gone)} {(gone.Count == 1 ? "is" : "are")} now inside it (or on an edge), so {(gone.Count == 1 ? "it is" : "they are")} dropped."),
                () => Ids(merged));
        }

        public void Finish(List<(double x, double y)> hull) {
            if (hull.Count >= 3) _rec.Accept(() => Answer(hull), () => $"The hull is {Names(hull)}, counterclockwise from {Id(hull[0])}.");
        }

        public string DoneCaption(List<(double x, double y)> hull) {
            int n = _problem.points.Count;
            if (n == 0) return "There are no points, so the hull is empty.";
            if (hull.Count == 1) return $"The only point is {Id(hull[0])}, so the hull is that single point.";
            if (hull.Count == 2) return $"All the points are on one line, so the hull is the segment from {Id(hull[0])} to {Id(hull[1])}.";
            int inside = _dropped.Count;
            return $"Done. The hull has {hull.Count} corners, listed counterclockwise from {Id(hull[0])}: {Names(hull)}."
                + (inside == 0 ? " Every point is a corner." : $" The other {Plural(inside, "point lies", "points lie")} inside it or on an edge.");
        }

        private static string Plural(int n, string one, string many) => $"{n} {(n == 1 ? one : many)}";
    }
}
