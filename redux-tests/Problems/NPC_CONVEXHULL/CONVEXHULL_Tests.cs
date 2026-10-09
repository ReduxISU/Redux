using Xunit;
using API.Problems.NPComplete.NPC_CONVEXHULL;
using API.Problems.NPComplete.NPC_CONVEXHULL.Verifiers;
using API.Problems.NPComplete.NPC_CONVEXHULL.Solvers;
using API.Interfaces;

namespace redux_tests;

#pragma warning disable CS1591

public class CONVEXHULL_Tests {

    [Fact]
    public void CONVEXHULL_Default_Instantiation() {
        CONVEXHULL hull = new CONVEXHULL();
        string actual_result = hull.defaultSolver.solve(hull);
        Assert.Equal("((-0.9308276577586132,-0.1423800479224624), (-0.27394905790800017,-0.7488048223660126), (0.2723211656942368,-0.8053758131859647), (0.7674622377407927,-0.21537444528240846), (0.6077591838324792,0.5288040272918157), (-0.32705115386597394,0.6744065707101621), (-0.6984449872706371,0.3857380723376367))", actual_result);
    }

    [Fact]
    public void CONVEXHULL_Custom_String_Input_Test() {
        string input = "{(0.14910331775506291, -0.8406444850820369), (0.07257532737672512, -0.251706857318015), (0.1144445878616871, 0.41987963593139965), (-0.5016809602104544, 0.4032473346040213), (0.3928123439811402, -0.8253116563139804), (0.36781650107298325, -0.24266892313868738), (0.2782909741500923, 0.2851627802061465), (0.7051482337938415, -0.45763755657931227), (0.14071041744097257, 0.3448575040369797), (0.7381887883615372, 0.38173355278244414)}";
        string expected_result = "((-0.5016809602104544,0.4032473346040213), (0.14910331775506291,-0.8406444850820369), (0.3928123439811402,-0.8253116563139804), (0.7051482337938415,-0.45763755657931227), (0.7381887883615372,0.38173355278244414), (0.1144445878616871,0.41987963593139965))";
        CONVEXHULL hull = new CONVEXHULL(input);
        string actual_result = hull.defaultSolver.solve(hull);
        Assert.Equal(expected_result, actual_result);
    }

    [Theory] //Test convex hull verifier with a few certificates

    [InlineData("{(-0.17055262956662953,-0.34879784136536185), (0.5628748918213995,0.2992518362152232), (0.5506724939545227,0.19610502706432276), (-0.6314260885584542,-0.7142578381816922), (0.29261853988332254,-0.6103940921033697)}", "((0.29261853988332254,-0.6103940921033697), (0.5506724939545227,0.19610502706432276), (0.5628748918213995,0.2992518362152232), (-0.6314260885584542,-0.7142578381816922))", true)]
    [InlineData("{(0,0),(2,0),(1,2)}", "((0,0), (2,0), (1,2))", true)]
    [InlineData("{(0,1),(2,5),(1,4)}", "((2,5), (1,4))", false)]
    [InlineData("{(1,1),(0,0),(2,2),(3,3)}", "((3,3),(0,0))", true)]
    [InlineData("{(0,0), (2,0), (2,2), (0,2), (1,1)}", "((0,0), (2,0), (2,2), (1,1), (0,2))", false)]
    public void CONVEXHULL_verifier(string instance, string certificate, bool expected) {
        CONVEXHULL convexHull = new CONVEXHULL(instance);
        bool result = convexHull.defaultVerifier.verify(convexHull, certificate);
        Assert.Equal(expected, result);

    }


    [Theory] //test solver
    [InlineData("{(-1,-1), (1,-1), (1,1), (-1,1), (0,0), (0.5,0.2)}", "((-1,-1), (1,-1), (1,1), (-1,1))")]
    [InlineData("{(0,0), (2,1), (3,3), (1,4), (-1,2), (1,2)}", "((-1,2), (0,0), (2,1), (3,3), (1,4))")]
    public void CONVEXHULL_solver(string instance, string certificate) {
        CONVEXHULL convexHull = new CONVEXHULL(instance);
        ConvexHullSolver solver = convexHull.defaultSolver;
        string solvedString = solver.solve(convexHull);
        Assert.Equal(certificate, solvedString);
    }

    // -------------------------------------------------------------------------
    // Format declarations
    // -------------------------------------------------------------------------

    [Fact]
    public void CONVEXHULL_Instance_Format_Described() {
        CONVEXHULL hull = new CONVEXHULL();
        Assert.NotNull(hull.instanceFormat);
        Assert.NotEmpty(hull.instanceFormat);
        Assert.Contains("(x,y)", hull.instanceFormat);
    }

    [Fact]
    public void CONVEXHULL_Certificate_Format_Described() {
        CONVEXHULL hull = new CONVEXHULL();
        Assert.NotNull(hull.certificateFormat);
        Assert.NotEmpty(hull.certificateFormat);
        Assert.Contains("convex hull", hull.certificateFormat);
    }

    [Fact]
    public void CONVEXHULL_Certificate_Format_Example_Is_Actually_Valid() {
        // The example quoted in certificateFormat must be a real, verifiable
        // certificate for defaultInstance — not just descriptive prose.
        CONVEXHULL hull = new CONVEXHULL();
        Assert.True(hull.defaultVerifier.verify(hull, ConvexHullVerifier.CertificateExample));
    }


    // -------------------------------------------------------------------------
    // Regression tests for #708 / #709 (ties in x, collinear points, rotations, orientation)
    // -------------------------------------------------------------------------

    private static double Cross((double x, double y) a, (double x, double y) b, (double x, double y) c)
        => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);

    // Reference: Andrew's monotone chain, strictly convex, counterclockwise from the lowest-leftmost point.
    private static List<(double x, double y)> ReferenceHull(IEnumerable<(double x, double y)> input) {
        var p = input.Distinct().OrderBy(t => t.x).ThenBy(t => t.y).ToList();
        if (p.Count <= 2) return p;
        var h = new List<(double x, double y)>();
        foreach (var q in p) {
            while (h.Count >= 2 && Cross(h[^2], h[^1], q) <= 0) h.RemoveAt(h.Count - 1);
            h.Add(q);
        }
        int lower = h.Count + 1;
        for (int i = p.Count - 2; i >= 0; i--) {
            while (h.Count >= lower && Cross(h[^2], h[^1], p[i]) <= 0) h.RemoveAt(h.Count - 1);
            h.Add(p[i]);
        }
        h.RemoveAt(h.Count - 1);
        return h;
    }

    private static string Fmt(IEnumerable<(double x, double y)> pts)
        => "(" + string.Join(", ", pts.Select(p => $"({p.x.ToString(System.Globalization.CultureInfo.InvariantCulture)},{p.y.ToString(System.Globalization.CultureInfo.InvariantCulture)})")) + ")";

    private static string FmtSet(IEnumerable<(double x, double y)> pts) => "{" + Fmt(pts)[1..^1] + "}";

    [Fact]
    public void CONVEXHULL_Solver_Random_Sets_With_Repeated_X_Match_Reference() {
        var rng = new Random(708);
        for (int t = 0; t < 500; t++) {
            int n = rng.Next(1, 25);
            // Small integer grid so x values repeat and points are often collinear or duplicated.
            var pts = Enumerable.Range(0, n).Select(_ => ((double)rng.Next(0, 6), (double)rng.Next(0, 6))).ToList();
            var problem = new CONVEXHULL(FmtSet(pts));
            problem.defaultSolver.solve(problem);
            var expected = ReferenceHull(pts);
            Assert.True(expected.SequenceEqual(problem.convexHull), $"set {FmtSet(pts)}: expected {Fmt(expected)}, got {Fmt(problem.convexHull)}");
            Assert.True(problem.defaultVerifier.verify(problem, Fmt(problem.convexHull)), $"verifier rejected solver output for {FmtSet(pts)}");
        }
    }

    [Fact]
    public void CONVEXHULL_Solver_Random_Real_Sets_Match_Reference() {
        var rng = new Random(709);
        for (int t = 0; t < 100; t++) {
            var pts = Enumerable.Range(0, rng.Next(3, 60)).Select(_ => (Math.Round(rng.NextDouble(), 6), Math.Round(rng.NextDouble(), 6))).ToList();
            var problem = new CONVEXHULL(FmtSet(pts));
            problem.defaultSolver.solve(problem);
            Assert.True(ReferenceHull(pts).SequenceEqual(problem.convexHull));
        }
    }

    [Fact]
    public void CONVEXHULL_Solver_Shared_X_Column_And_Collinear_Boundary() {
        // Grid: collinear points on every edge are not vertices.
        var problem = new CONVEXHULL("{(0,0),(0,1),(0,2),(1,0),(1,2),(2,0),(2,1),(2,2),(1,1)}");
        Assert.Equal("((0,0), (2,0), (2,2), (0,2))", problem.defaultSolver.solve(problem));
        Assert.True(problem.defaultVerifier.verify(problem, "((0,0), (2,0), (2,2), (0,2))"));
        // Including a collinear edge point is rejected.
        Assert.False(problem.defaultVerifier.verify(problem, "((0,0), (1,0), (2,0), (2,2), (0,2))"));
    }

    [Fact]
    public void CONVEXHULL_Solver_Does_Not_Mutate_Input() {
        var problem = new CONVEXHULL("{(3,1),(1,2),(2,0),(0,0),(1,1)}");
        var before = problem.points.ToList();
        problem.defaultSolver.solve(problem);
        Assert.Equal(before, problem.points);
    }

    [Fact]
    public void CONVEXHULL_Verifier_Does_Not_Mutate_Problem() {
        var problem = new CONVEXHULL("{(3,1),(1,2),(2,0),(0,0),(1,1)}");
        var before = problem.points.ToList();
        problem.defaultVerifier.verify(problem, "((0,0), (2,0), (3,1), (1,2))");
        Assert.Equal(before, problem.points);
        Assert.Empty(problem.convexHull);
        Assert.Equal(string.Empty, problem.solution);
    }

    [Theory]
    [InlineData("((0,0), (2,0), (2,2), (0,2))")]
    [InlineData("((2,0), (2,2), (0,2), (0,0))")]
    [InlineData("((2,2), (0,2), (0,0), (2,0))")]
    [InlineData("((0,2), (0,0), (2,0), (2,2))")]
    public void CONVEXHULL_Verifier_Accepts_Any_Rotation(string certificate) {
        var problem = new CONVEXHULL("{(0,0),(2,0),(2,2),(0,2),(1,1)}");
        Assert.True(problem.defaultVerifier.verify(problem, certificate));
    }

    [Theory]
    [InlineData("((0,0), (0,2), (2,2), (2,0))")] // clockwise
    [InlineData("((0,2), (2,2), (2,0), (0,0))")] // clockwise
    [InlineData("((0,0), (2,0), (2,2))")] // misses a corner
    [InlineData("((0,0), (2,0), (2,2), (0,2), (0,0))")] // repeated vertex
    [InlineData("((0,0), (2,0), (2,2), (0,2), (1,1))")] // interior point
    [InlineData("((0,0), (2,0), (2,2), (0,3))")] // not an instance point
    public void CONVEXHULL_Verifier_Rejects_Wrong_Hulls(string certificate) {
        var problem = new CONVEXHULL("{(0,0),(2,0),(2,2),(0,2),(1,1)}");
        Assert.False(problem.defaultVerifier.verify(problem, certificate));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("garbage")]
    [InlineData("((a,b), (c,d))")]
    [InlineData("((1,2,3), (4,5,6))")]
    [InlineData("(((0,0)")]
    public void CONVEXHULL_Verifier_Garbage_Returns_False(string certificate) {
        var problem = new CONVEXHULL("{(0,0),(2,0),(2,2),(0,2),(1,1)}");
        Assert.False(problem.defaultVerifier.verify(problem, certificate));
    }

    [Fact]
    public void CONVEXHULL_Degenerate_Collinear_And_Single_Point() {
        var line = new CONVEXHULL("{(1,1),(0,0),(2,2),(3,3)}");
        Assert.Equal("((0,0), (3,3))", line.defaultSolver.solve(line));
        Assert.True(line.defaultVerifier.verify(line, "((3,3),(0,0))"));
        Assert.False(line.defaultVerifier.verify(line, "((0,0),(2,2))"));

        var vertical = new CONVEXHULL("{(1,0),(1,3),(1,1)}");
        Assert.Equal("((1,0), (1,3))", vertical.defaultSolver.solve(vertical));

        var single = new CONVEXHULL("{(4,5),(4,5)}");
        Assert.Equal("((4,5))", single.defaultSolver.solve(single));
        Assert.True(single.defaultVerifier.verify(single, "((4,5))"));
    }

    [Fact]
    public void CONVEXHULL_SortedVertices_Returns_Hull_In_XY_Order_Without_Sorting() {
        var rng = new Random(7);
        for (int t = 0; t < 300; t++) {
            var pts = Enumerable.Range(0, rng.Next(1, 30)).Select(_ => ((double)rng.Next(0, 6), (double)rng.Next(0, 6))).ToList();
            var hull = ReferenceHull(pts);
            var expected = hull.OrderBy(p => p.x).ThenBy(p => p.y).ToList();
            Assert.Equal(expected, API.Problems.NPComplete.NPC_CONVEXHULL.Solvers.ConvexHullSolver.SortedVertices(hull));
        }
    }
}
