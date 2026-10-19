using API.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using SPADE;

namespace API.Problems.NPComplete.NPC_CONVEXHULL.Verifiers;

class ConvexHullVerifier : IVerifier<CONVEXHULL> {
    public const string CertificateGrammar = "(v1,...,vk) | vi are (x,y) points forming the convex hull's vertices in counterclockwise order, starting at any vertex";
    public const string CertificateExample = "((0.2723211656942368,-0.8053758131859647), (0.7674622377407927,-0.21537444528240846), (0.6077591838324792,0.5288040272918157), (-0.32705115386597394,0.6744065707101621), (-0.6984449872706371,0.3857380723376367), (-0.9308276577586132,-0.1423800479224624), (-0.27394905790800017,-0.7488048223660126))";

    // --- Fields ---
    public string verifierName { get; } = "Default Convex Hull Verifier";
    public string verifierDefinition { get; } = "Verifies a proposed convex hull geometrically: the certificate must list distinct input points forming a strictly convex polygon in counterclockwise order (any starting vertex) with every input point on or inside it. Points lying on a hull edge are not vertices and must be omitted.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "";
    public string[] contributors { get; } = { "Bektur Akkabakov" };
    private string _certificate = "";

    public string certificate {
        get {
            return _certificate;
        }
    }

    // --- Methods Including Constructors ---
    public ConvexHullVerifier() { }
    // Verifies geometrically, without re-running the solver and without touching the problem's state.
    // The certificate is accepted when it lists the hull's corners counterclockwise, starting at any
    // vertex. Points that lie on a hull edge but are not corners must be omitted (strictly convex).
    public bool verify(CONVEXHULL problem, string certificate) {
        List<(double x, double y)> hull;
        try {
            hull = ParsePoints(certificate);
        } catch (Exception) {
            return false;
        }

        var points = problem.points;
        int k = hull.Count;

        if (k == 0) return points.Count == 0;
        if (hull.Distinct().Count() != k) return false;
        if (!hull.All(v => points.Contains(v))) return false;

        var distinct = points.Distinct().ToList();

        if (k == 1) return distinct.Count == 1;

        if (k == 2) {
            // Degenerate hull: every point is on the segment between the two endpoints.
            var (a, b) = (hull[0], hull[1]);
            return distinct.All(p => Cross(a, b, p) == 0
                && Math.Min(a.x, b.x) <= p.x && p.x <= Math.Max(a.x, b.x)
                && Math.Min(a.y, b.y) <= p.y && p.y <= Math.Max(a.y, b.y));
        }

        for (int i = 0; i < k; i++) {
            var a = hull[i];
            var b = hull[(i + 1) % k];
            var c = hull[(i + 2) % k];
            // Strict left turn at every corner: counterclockwise and no collinear corners.
            if (Cross(a, b, c) <= 0) return false;
            // Every instance point is on the left of, or on, each edge.
            foreach (var p in distinct)
                if (Cross(a, b, p) < 0) return false;
        }
        return true;
    }

    private static double Cross((double x, double y) a, (double x, double y) b, (double x, double y) c) {
        return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
    }


    public List<(double x, double y)> ParsePoints(string s) {
        if (string.IsNullOrWhiteSpace(s)) throw new Exception("Certificate is empty or whitespace.");

        // SPADE's tokenizer doesn't tolerate whitespace between elements, so
        // insignificant spacing is stripped before handing the structure
        // (the ordered list of (x,y) tuples) to UtilCollection to parse.
        UtilCollection collection = new UtilCollection(s.Replace(" ", ""));
        collection.assertOrdered();

        return collection.ToList().Select(point => {
            point.assertPair();
            double x = double.Parse(point[0].ToString(), NumberStyles.Float, CultureInfo.InvariantCulture);
            double y = double.Parse(point[1].ToString(), NumberStyles.Float, CultureInfo.InvariantCulture);
            return (x, y);
        }).ToList();
    }
}
