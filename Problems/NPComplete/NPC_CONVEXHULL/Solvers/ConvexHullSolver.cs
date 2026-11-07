using API.Interfaces;
using API.Interfaces.Geometry;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_CONVEXHULL.Visualizations;

namespace API.Problems.NPComplete.NPC_CONVEXHULL.Solvers;

// The steps Convex Hull Divide and Conquer records, for the Convex Hull picture:
//   - an Update for the sort (with the repeated points left out);
//   - an Update per smallest piece (at most 3 points), its hull made at once;
//   - per merge: an Update to start it, an Update per corner pushed onto the chain and a Reject per corner popped off it
//     (a clockwise turn, or three points on one line), and an Update with the merged hull and the corners it dropped;
//   - an Accept with the whole hull, and the Done with the verifier's verdict (both only when recording).
// Nothing is judged until a corner is popped, so the other steps are Updates.
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
    public MathematicalFunction timeComplexity { get; } = new("n log n", "");

    public ConvexHullSolver() { }

    // How the steps show the run: the table of hulls built so far (see Build). Every step is about that same table, as it
    // stands when the step is recorded.
    private static RunSteps<Build, HullBuild> NewSteps(StepRecorder<HullBuild> rec) => new(rec,
        // What every picture shows: the finished hulls, the two being merged, the chain walked so far and the points
        // dropped. A step about some points (a piece, a corner) names them in its own focus.
        partial: build => build.Partial());

    public string solve(CONVEXHULL problem) => Solve(problem, StepRecorder<HullBuild>.Off);

    public string Solve(CONVEXHULL problem, StepRecorder<HullBuild> rec) {
        if (timerHasExpired)
            return SolveRun.TimedOutAnswer;

        // Sort a copy by (x, then y) and drop duplicate points; the problem's own list is left untouched.
        List<(double x, double y)> points = problem.points
            .OrderBy(p => p.x).ThenBy(p => p.y)
            .Distinct()
            .ToList();

        var build = new Build(problem);
        var steps = NewSteps(rec);
        int n = problem.points.Count;
        int repeats = n - points.Count;
        if (points.Count > 1) {
            if (points.Count <= 3 && repeats == 0)
                steps.Update(build, $"Sort the {Words.Count(points.Count, "point")} by x, then y: {build.Names(points)}.");
            else if (points.Count <= 3)
                steps.Update(build, $"Sort the {Words.Count(points.Count, "point")} by x, then y: {build.Names(points)}. {Words.Count(repeats, "point repeats", "points repeat")} another and {(repeats == 1 ? "is" : "are")} left out.");
            else if (repeats == 0)
                steps.Update(build, $"Sort the {points.Count} points by x, then y: {build.Names(points)}. Then split the list in half, again and again, until each piece has at most 3 points.");
            else
                steps.Update(build, $"Sort the {points.Count} points by x, then y: {build.Names(points)}. {Words.Count(repeats, "point repeats", "points repeat")} another and {(repeats == 1 ? "is" : "are")} left out. Then split the list in half, again and again, until each piece has at most 3 points.");
        }
        // No points: no pieces to make, and the hull is empty.
        List<(double x, double y)> hull = points.Count == 0 ? new() : ConvexHullDC(points, build, steps);

        problem.convexHull = hull;
        problem.solution = Format(hull);
        // The last Accept, the verdict and the Done only when recording: the verifier checks every point against every edge,
        // O(n h), which would make a plain solve of a large hull slower than the hull itself.
        if (rec.Enabled) {
            if (hull.Count >= 3)
                steps.Accept(build, $"The hull is {build.Names(hull)}, counterclockwise from {build.Id(hull[0])}.", partial: () => build.Answer(hull));
            bool ok = problem.defaultVerifier.verify(problem, problem.solution);
            int inside = build.DroppedCount;
            if (n == 0)
                steps.Done(build.Answer(hull), ok, $"There are no points, so the hull is empty.");
            else if (hull.Count == 1)
                steps.Done(build.Answer(hull), ok, $"The only point is {build.Id(hull[0])}, so the hull is that single point.");
            else if (hull.Count == 2)
                steps.Done(build.Answer(hull), ok, $"All the points are on one line, so the hull is the segment from {build.Id(hull[0])} to {build.Id(hull[1])}.");
            else if (inside == 0)
                steps.Done(build.Answer(hull), ok, $"Done. The hull has {hull.Count} corners, listed counterclockwise from {build.Id(hull[0])}: {build.Names(hull)}. Every point is a corner.");
            else
                steps.Done(build.Answer(hull), ok, $"Done. The hull has {hull.Count} corners, listed counterclockwise from {build.Id(hull[0])}: {build.Names(hull)}. The other {Words.Count(inside, "point lies", "points lie")} inside it or on an edge.");
        }
        return problem.solution;
    }

    // Divide & Conquer. The hull is returned counterclockwise, starting at the lexicographically
    // smallest point (lowest x, then lowest y). Collinear boundary points are NOT vertices of the hull.

    private List<(double x, double y)> ConvexHullDC(List<(double x, double y)> pts, Build build, RunSteps<Build, HullBuild> steps) {
        if (pts.Count <= 3) {
            // A smallest piece: its hull is made at once, with no steps of its own.
            var piece = Chain(pts, build, null);
            build.AddPiece(pts, piece);
            if (pts.Count == 1)
                steps.Update(build, $"{build.Id(pts[0])} on its own is its own hull.", focus: () => build.Ids(piece));
            else if (pts.Count == 2)
                steps.Update(build, $"Smallest piece: {build.Names(pts)}. Its hull is the segment between them.", focus: () => build.Ids(piece));
            else if (piece.Count == 2)
                steps.Update(build, $"Smallest piece: {build.Names(pts)}. They are on one line, so {build.Names(pts.Except(piece))} in the middle is dropped.", focus: () => build.Ids(piece));
            else
                steps.Update(build, $"Smallest piece: {build.Names(pts)}. Its hull is the triangle they make.", focus: () => build.Ids(piece));
            return piece;
        }

        int mid = pts.Count / 2;

        var left = ConvexHullDC(pts.GetRange(0, mid), build, steps);
        var right = ConvexHullDC(pts.GetRange(mid, pts.Count - mid), build, steps);

        return Merge(left, right, build, steps);
    }

    // Merges two hulls in linear time. Every vertex of the combined hull is a vertex of one of the
    // two sub-hulls, so it is enough to rebuild the hull from the union of their vertices. Each
    // hull's vertices are put in (x, y) order by a linear merge of its lower and upper chains, and
    // since the halves were split by index of the sorted points, every left vertex precedes every
    // right vertex, so the two lists just concatenate. Correct for equal x values and collinear points.
    private List<(double x, double y)> Merge(List<(double x, double y)> left, List<(double x, double y)> right, Build build, RunSteps<Build, HullBuild> steps) {
        var all = SortedVertices(left);
        all.AddRange(SortedVertices(right));
        build.StartMerge(left, right);
        steps.Update(build, $"Merge the left hull ({build.Names(left)}) with the right hull ({build.Names(right)}). Walk their {all.Count} corners left to right for the bottom edge, then back for the top edge, keeping only left turns.");
        var merged = Chain(all, build, steps);
        var gone = build.EndMerge(left, right, merged);
        if (gone.Count == 0)
            steps.Update(build, $"The merged hull is {build.Names(merged)}. Every corner of both hulls stays.", focus: () => build.Ids(merged));
        else if (gone.Count == 1)
            steps.Update(build, $"The merged hull is {build.Names(merged)}. {build.Names(gone)} is now inside it (or on an edge), so it is dropped.", focus: () => build.Ids(merged));
        else
            steps.Update(build, $"The merged hull is {build.Names(merged)}. {build.Names(gone)} are now inside it (or on an edge), so they are dropped.", focus: () => build.Ids(merged));
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
    // Returns the strictly convex hull counterclockwise from the first point. With steps, every push and pop is recorded
    // (a merge); without, nothing is (a smallest piece, recorded as a whole).
    // The bottom edge walks the points left to right, the top edge back right to left. A corner whose turn (from the one
    // before it to the next point) is not a left turn can't be on the hull, so it is popped off before the next is pushed.
    private List<(double x, double y)> Chain(List<(double x, double y)> sorted, Build build, RunSteps<Build, HullBuild>? steps) {
        int n = sorted.Count;
        if (n <= 1)
            return new List<(double x, double y)>(sorted);

        var h = new List<(double x, double y)>();
        if (steps != null) build.Chain = h;
        for (int i = 0; i < n; i++) {
            while (h.Count >= 2) {
                double turn = Orientation(h[h.Count - 2], h[h.Count - 1], sorted[i]);
                if (turn > 0) break;
                var popped = h[h.Count - 1];
                h.RemoveAt(h.Count - 1);
                if (steps == null) continue;
                if (turn == 0)
                    steps.Reject(build, $"{build.Id(h[^1])}, {build.Id(popped)} and {build.Id(sorted[i])} are on one line, so pop {build.Id(popped)} from the bottom edge.", focus: () => [build.Id(popped)]);
                else
                    steps.Reject(build, $"The turn at {build.Id(popped)} (from {build.Id(h[^1])} to {build.Id(sorted[i])}) is clockwise, so pop {build.Id(popped)} from the bottom edge.", focus: () => [build.Id(popped)]);
            }
            h.Add(sorted[i]);
            steps?.Update(build, $"Add {build.Id(h[^1])} to the bottom edge.", focus: () => [build.Id(h[^1])]);
        }
        int lower = h.Count + 1;
        for (int i = n - 2; i >= 0; i--) {
            while (h.Count >= lower) {
                double turn = Orientation(h[h.Count - 2], h[h.Count - 1], sorted[i]);
                if (turn > 0) break;
                var popped = h[h.Count - 1];
                h.RemoveAt(h.Count - 1);
                if (steps == null) continue;
                if (turn == 0)
                    steps.Reject(build, $"{build.Id(h[^1])}, {build.Id(popped)} and {build.Id(sorted[i])} are on one line, so pop {build.Id(popped)} from the top edge.", focus: () => [build.Id(popped)]);
                else
                    steps.Reject(build, $"The turn at {build.Id(popped)} (from {build.Id(h[^1])} to {build.Id(sorted[i])}) is clockwise, so pop {build.Id(popped)} from the top edge.", focus: () => [build.Id(popped)]);
            }
            h.Add(sorted[i]);
            if (h.Count > 2 && h[^1] == h[0])
                steps?.Update(build, $"Back at {build.Id(h[^1])}: the top edge closes the hull.", focus: () => [build.Id(h[^1])]);
            else
                steps?.Update(build, $"Add {build.Id(h[^1])} to the top edge.", focus: () => [build.Id(h[^1])]);
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
    // is walking and the points dropped. Its ids are read only for a step that is kept (and for Done), so they cost nothing
    // when recording is off; keeping the table itself costs a few list operations per piece and merge.
    private sealed class Build {
        private readonly CONVEXHULL _problem;
        private Dictionary<(double X, double Y), string>? _ids;
        private readonly List<List<(double x, double y)>> _table = new();
        private readonly HashSet<(double x, double y)> _dropped = new();
        private List<(double x, double y)>? _left, _right;

        public Build(CONVEXHULL problem) => _problem = problem;

        // The chain the merge in progress is walking (the list Chain pushes onto and pops from); empty between merges.
        public List<(double x, double y)> Chain { get; set; } = new();

        // How many points are dropped so far: inside a hull, or in the middle of three on one line.
        public int DroppedCount => _dropped.Count;

        // A point's id in the picture ("P3"), and the ids of some points, as a list or as "P1, P2 and P3".
        public string Id((double x, double y) p) => (_ids ??= GeometryFrames.IdsByPoint(ConvexHullVisualization.Spec(_problem)))[p];
        public string[] Ids(IEnumerable<(double x, double y)> points) => points.Select(Id).ToArray();
        public string Names(IEnumerable<(double x, double y)> points) => Words.List(Ids(points), most: 12);

        public HullBuild Partial() => new(
            _table.Where(h => !ReferenceEquals(h, _left) && !ReferenceEquals(h, _right)).Select(Ids),
            _left == null ? [] : Ids(_left), _right == null ? [] : Ids(_right), Ids(Chain), Ids(_dropped));

        public HullBuild Answer(List<(double x, double y)> hull) => new(hull.Count == 0 ? [] : [Ids(hull)], [], [], [], Ids(_dropped));

        // A smallest piece (at most 3 points) and its hull, made at once: the hull goes on the table, the rest is dropped.
        public void AddPiece(List<(double x, double y)> points, List<(double x, double y)> hull) {
            _table.Add(hull);
            foreach (var p in points.Except(hull)) _dropped.Add(p);
        }

        // The two hulls a merge starts from, taken off the table's finished hulls; the chain starts empty.
        public void StartMerge(List<(double x, double y)> left, List<(double x, double y)> right) {
            _left = left;
            _right = right;
            Chain = new();
        }

        // The merged hull takes the two hulls' place on the table. Returns the corners of the two that it dropped.
        public List<(double x, double y)> EndMerge(List<(double x, double y)> left, List<(double x, double y)> right, List<(double x, double y)> merged) {
            var gone = left.Concat(right).Except(merged).ToList();
            foreach (var p in gone) _dropped.Add(p);
            int at = _table.FindIndex(h => ReferenceEquals(h, left));
            _table.RemoveAll(h => ReferenceEquals(h, left) || ReferenceEquals(h, right));
            _table.Insert(at, merged);
            _left = _right = null;
            Chain = new();
            return gone;
        }
    }
}
