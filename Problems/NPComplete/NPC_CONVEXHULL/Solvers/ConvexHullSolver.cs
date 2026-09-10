using API.Interfaces;

namespace API.Problems.NPComplete.NPC_CONVEXHULL.Solvers;

class ConvexHullSolver : ISolver<CONVEXHULL> {

    public string solverName { get; } = "Convex Hull Divide and Conquer";
    public string solverDefinition { get; } = "Computes the convex hull of a set of 2D points using divide and conquer.";
    public string source { get; } = "https://doi.org/10.1145/359423.359430";
    public string[] contributors { get; } = { "Bektur Akkabakov" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Splits the point set in half, solves each half recursively, then
    // merges the two hulls -- the textbook divide-and-conquer shape.
    public SolverType solverType { get; } = SolverType.DivideAndConquer;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
    // Sort is O(n log n); the divide-and-conquer merge finds the two hulls' tangent lines with
    // a linear rotating scan per level, giving the classic T(n) = 2T(n/2) + O(n) recurrence.
    public string complexity { get; } = "O(n log n)";

    public ConvexHullSolver() { }

    public string solve(CONVEXHULL problem) {
        if (timerHasExpired)
            return "timeout";

        List<(double x, double y)> points = problem.points;

        // Sort by x (important for D&C split)
        points.Sort((a, b) => a.x.CompareTo(b.x));

        List<(double x, double y)> hull = ConvexHullDC(points);

        problem.convexHull = hull;
        problem.solution = Format(hull);
        return problem.solution;
    }

    // Divide & Conquer

    private List<(double x, double y)> ConvexHullDC(List<(double x, double y)> pts) {
        if (pts.Count <= 1)
            return new List<(double x, double y)>(pts);

        if (pts.Count <= 3)
            return BaseHull(pts);

        int mid = pts.Count / 2;

        var left = ConvexHullDC(pts.GetRange(0, mid));
        var right = ConvexHullDC(pts.GetRange(mid, pts.Count - mid));

        return Merge(left, right);
    }

    private List<(double x, double y)> Merge(List<(double x, double y)> left, List<(double x, double y)> right) {
        int iL = RightmostP(left);
        int iR = LeftmostP(right);

        int upperL = iL, upperR = iR;
        int lowerL = iL, lowerR = iR;

        // upper tangent
        bool done = false;
        while (!done) {
            done = true;
            // move left counterclockwise
            while (true) {
                int next = (upperL == 0 ? left.Count - 1 : upperL - 1);
                double o = Orientation(right[upperR], left[upperL], left[next]);

                if (o > 0 || (o == 0 && DistSq(right[upperR], left[next]) > DistSq(right[upperR], left[upperL])))
                    upperL = next;
                else break;
            }
            // move right clockwise
            while (true) {
                int next = (upperR + 1) % right.Count;
                double o = Orientation(left[upperL], right[upperR], right[next]);
                if (o < 0 || (o == 0 && DistSq(left[upperL], right[next]) > DistSq(left[upperL], right[upperR]))) {
                    upperR = next;
                    done = false;
                } else break;
            }
        }

        // lower tangent
        done = false;
        while (!done) {
            done = true;
            // move left clockwise
            while (true) {
                int next = (lowerL + 1) % left.Count;
                double o = Orientation(right[lowerR], left[lowerL], left[next]);

                if (o < 0 || (o == 0 && DistSq(right[lowerR], left[next]) > DistSq(right[lowerR], left[lowerL])))
                    lowerL = next;
                else break;
            }
            // move right counterclockwise
            while (true) {
                int next = (lowerR == 0 ? right.Count - 1 : lowerR - 1);
                double o = Orientation(left[lowerL], right[lowerR], right[next]);
                if (o > 0 || (o == 0 && DistSq(left[lowerL], right[next]) > DistSq(left[lowerL], right[lowerR]))) {
                    lowerR = next;
                    done = false;
                } else break;
            }
        }
        // build merged hull
        List<(double x, double y)> merged = new List<(double x, double y)>();
        int v = upperR;
        merged.Add(right[v]);
        while (v != lowerR) {
            v = (v + 1) % right.Count;
            merged.Add(right[v]);
        }
        v = lowerL;
        merged.Add(left[v]);
        while (v != upperL) {
            v = (v + 1) % left.Count;
            merged.Add(left[v]);
        }
        return merged;
    }

    private List<(double x, double y)> BaseHull(List<(double x, double y)> pts) {
        if (pts.Count <= 1)
            return new List<(double x, double y)>(pts);
        if (pts.Count == 2)
            return pts;
        var a = pts[0];
        var b = pts[1];
        var c = pts[2];
        double o = Orientation(a, b, c);
        if (o == 0) {
            // return only endpoints
            var sorted = pts.OrderBy(p => p.x).ThenBy(p => p.y).ToList();
            return new List<(double x, double y)> { sorted[0], sorted[2] };
        }
        if (o > 0)
            return new List<(double x, double y)> { a, b, c };
        else
            return new List<(double x, double y)> { a, c, b };
    }
    // Helpers
    private double Orientation((double x, double y) a,
                               (double x, double y) b,
                               (double x, double y) c) {
        return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
    }

    private double DistSq((double x, double y) a, (double x, double y) b) {
        double dx = a.x - b.x;
        double dy = a.y - b.y;
        return dx * dx + dy * dy;
    }

    private int RightmostP(List<(double x, double y)> pts) {
        int idx = 0;
        for (int i = 1; i < pts.Count; i++)
            if (pts[i].x > pts[idx].x)
                idx = i;
        return idx;
    }

    private int LeftmostP(List<(double x, double y)> pts) {
        int idx = 0;
        for (int i = 1; i < pts.Count; i++)
            if (pts[i].x < pts[idx].x)
                idx = i;
        return idx;
    }

    private string Format(List<(double x, double y)> pts) {
        return "(" + string.Join(", ", pts.Select(p => $"({p.x},{p.y})")) + ")";
    }
}
