using API.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using SPADE;

namespace API.Problems.NPComplete.NPC_CONVEXHULL.Verifiers;

class ConvexHullVerifier : IVerifier<CONVEXHULL> {
    public const string CertificateGrammar = "(v1,...,vk) | vi are (x,y) points forming the convex hull's vertices in order";
    public const string CertificateExample = "((0.2723211656942368,-0.8053758131859647), (0.7674622377407927,-0.21537444528240846), (0.6077591838324792,0.5288040272918157), (-0.32705115386597394,0.6744065707101621), (-0.6984449872706371,0.3857380723376367), (-0.9308276577586132,-0.1423800479224624), (-0.27394905790800017,-0.7488048223660126))";

    // --- Fields ---
    public string verifierName { get; } = "Convex Hull Verifier";
    public string verifierDefinition { get; } = "Verifies a proposed convex hull by recomputing the convex hull of the input points using the default solver and comparing it to the provided certificate.";
    public string source { get; } = "";
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
    public bool verify(CONVEXHULL problem, string certificate) {

        List<(double x, double y)> certificatePoints = ParsePoints(certificate);
        problem.defaultSolver.solve(problem);
        List<(double x, double y)> solvedHull = problem.convexHull;

        if (solvedHull.Count != certificatePoints.Count) return false;

        for (int i = 0; i < solvedHull.Count; i++) {
            if (solvedHull[i] != certificatePoints[i]) return false;
        }

        return true;
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
