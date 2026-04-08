using API.Interfaces;

namespace API.Problems.NPComplete.NPC_CONVEXHULL.Solvers;
class ConvexHullSolver : ISolver<CONVEXHULL> {

    // --- Fields ---
    public string solverName {get;} = "Convex Hull Solver";
    public string solverDefinition {get;} = "Computes the convex hull of a set of 2D points using a divide-and-conquer algorithm. The points are split recursively, and partial hulls are merged by finding upper and lower tangents.";
    public string source {get;} = "https://doi.org/10.1145/359423.359430";
    public string[] contributors {get;} = { "Bektur Akkabakov" };
    public bool timerHasExpired { get; set; }
    // --- Methods Including Constructors ---
    public ConvexHullSolver(){}

    public string solve(CONVEXHULL problem)
    {
        if (timerHasExpired)
            return "timeout";

        List<(double x, double y)> points = problem.points;

        // Sort by x
        points.Sort((a, b) => a.x.CompareTo(b.x));

        List<(double x, double y)> hull = ConvexHullDC(points);

        problem.convexHull = hull;
        problem.solution = Format(hull);
        return problem.solution;
    }

    // Divide & Conquer

    private List<(double x, double y)> ConvexHullDC(List<(double x, double y)> points)
    {
        if (points.Count <= 1)
            return new List<(double x, double y)>(points);

        int mid = points.Count / 2;

        List<(double x, double y)> left = ConvexHullDC(points.GetRange(0, mid));
        List<(double x, double y)> right = ConvexHullDC(points.GetRange(mid, points.Count - mid));

        int rightmostL = RightmostP(left);
        int leftmostR = LeftmostP(right);

        int vertexL_ui = rightmostL;
        int vertexR_ui = leftmostR;
        int vertexL_li = rightmostL;
        int vertexR_li = leftmostR;

        // Upper Tangent
        while (Slope(left[vertexL_ui], right[vertexR_ui]) > Slope(left[(vertexL_ui == 0 ? left.Count - 1 : vertexL_ui - 1)], right[vertexR_ui]) ||
            Slope(left[vertexL_ui], right[vertexR_ui]) < Slope(left[vertexL_ui], right[(vertexR_ui + 1) % right.Count]))
        {
            while (Slope(left[vertexL_ui], right[vertexR_ui]) > Slope(left[(vertexL_ui == 0 ? left.Count - 1 : vertexL_ui - 1)], right[vertexR_ui]))
            {
                vertexL_ui = (vertexL_ui == 0 ? left.Count - 1 : vertexL_ui - 1);
            }

            while (Slope(left[vertexL_ui], right[vertexR_ui]) < Slope(left[vertexL_ui], right[(vertexR_ui + 1) % right.Count]))
            {
                vertexR_ui = (vertexR_ui + 1) % right.Count;
            }
        }

        // Lower Tangent
        while (Slope(right[vertexR_li], left[vertexL_li]) < Slope(right[vertexR_li], left[(vertexL_li + 1) % left.Count]) ||
            Slope(right[vertexR_li], left[vertexL_li]) > Slope(right[(vertexR_li == 0 ? right.Count - 1 : vertexR_li - 1)], left[vertexL_li]))
        {
            while (Slope(right[vertexR_li], left[vertexL_li]) < Slope(right[vertexR_li], left[(vertexL_li + 1) % left.Count]))
            {
                vertexL_li = (vertexL_li + 1) % left.Count;
            }

            while (Slope(right[vertexR_li], left[vertexL_li]) > Slope(right[(vertexR_li == 0 ? right.Count - 1 : vertexR_li - 1)], left[vertexL_li]))
            {
                vertexR_li = (vertexR_li == 0 ? right.Count - 1 : vertexR_li - 1);
            }
        }

        // Merge 
        List<(double x, double y)> merged = new List<(double x, double y)>();

        int v = vertexR_ui;
        while (v != vertexR_li)
        {
            merged.Add(right[v]);
            v = (v + 1) % right.Count;
        }
        merged.Add(right[vertexR_li]);

        v = vertexL_li;
        while (v != vertexL_ui)
        {
            merged.Add(left[v]);
            v = (v + 1) % left.Count;
        }
        merged.Add(left[vertexL_ui]);

        return merged;
    }

    // Helpers

    private int RightmostP(List<(double x, double y)> pts)
    {
        int idx = 0;
        for (int i = 1; i < pts.Count; i++)
        {
            if (pts[i].x > pts[idx].x)
                idx = i;
        }
        return idx;
    }

    private int LeftmostP(List<(double x, double y)> pts)
    {
        int idx = 0;
        for (int i = 1; i < pts.Count; i++)
        {
            if (pts[i].x < pts[idx].x)
                idx = i;
        }
        return idx;
    }

    private double Slope((double x, double y) a, (double x, double y) b)
    {
        return (a.y - b.y) / (a.x - b.x);
    }

    private string Format(List<(double x, double y)> pts)
    {
        return "(" + string.Join(", ", pts.Select(p => $"({p.x},{p.y})")) + ")";
    }

}
