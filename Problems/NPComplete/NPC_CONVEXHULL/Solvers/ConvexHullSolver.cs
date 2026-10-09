using API.Interfaces;

namespace API.Problems.NPComplete.NPC_CONVEXHULL.Solvers;

class ConvexHullSolver : ISolver<CONVEXHULL> {

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

    public string solve(CONVEXHULL problem) {
        if (timerHasExpired)
            return "timeout";

        // Sort a copy by (x, then y) and drop duplicate points; the problem's own list is left untouched.
        List<(double x, double y)> points = problem.points
            .OrderBy(p => p.x).ThenBy(p => p.y)
            .Distinct()
            .ToList();

        List<(double x, double y)> hull = ConvexHullDC(points);

        problem.convexHull = hull;
        problem.solution = Format(hull);
        return problem.solution;
    }

    // Divide & Conquer. The hull is returned counterclockwise, starting at the lexicographically
    // smallest point (lowest x, then lowest y). Collinear boundary points are NOT vertices of the hull.

    private List<(double x, double y)> ConvexHullDC(List<(double x, double y)> pts) {
        if (pts.Count <= 3)
            return Chain(pts);

        int mid = pts.Count / 2;

        var left = ConvexHullDC(pts.GetRange(0, mid));
        var right = ConvexHullDC(pts.GetRange(mid, pts.Count - mid));

        return Merge(left, right);
    }

    // Merges two hulls. Every vertex of the combined hull is a vertex of one of the two sub-hulls, so
    // it is enough to take the union of their vertices (in (x, y) order, a linear merge) and rebuild
    // the hull from it in linear time. This is correct for equal x values and collinear points.
    private List<(double x, double y)> Merge(List<(double x, double y)> left, List<(double x, double y)> right) {
        var all = new List<(double x, double y)>(left.Count + right.Count);
        all.AddRange(left);
        all.AddRange(right);
        all.Sort(CompareXY);
        return Chain(all);
    }

    private static int CompareXY((double x, double y) a, (double x, double y) b) {
        int c = a.x.CompareTo(b.x);
        return c != 0 ? c : a.y.CompareTo(b.y);
    }

    // Monotone chain over points already sorted by (x, y) with no duplicates.
    // Returns the strictly convex hull counterclockwise from the first point.
    private List<(double x, double y)> Chain(List<(double x, double y)> sorted) {
        int n = sorted.Count;
        if (n <= 1)
            return new List<(double x, double y)>(sorted);

        var h = new List<(double x, double y)>();
        for (int i = 0; i < n; i++) {
            while (h.Count >= 2 && Orientation(h[h.Count - 2], h[h.Count - 1], sorted[i]) <= 0)
                h.RemoveAt(h.Count - 1);
            h.Add(sorted[i]);
        }
        int lower = h.Count + 1;
        for (int i = n - 2; i >= 0; i--) {
            while (h.Count >= lower && Orientation(h[h.Count - 2], h[h.Count - 1], sorted[i]) <= 0)
                h.RemoveAt(h.Count - 1);
            h.Add(sorted[i]);
        }
        h.RemoveAt(h.Count - 1);
        return h;
    }

    private double Orientation((double x, double y) a,
                               (double x, double y) b,
                               (double x, double y) c) {
        return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
    }

    private string Format(List<(double x, double y)> pts) {
        return "(" + string.Join(", ", pts.Select(p => $"({p.x},{p.y})")) + ")";
    }
}
