using API.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace API.Problems.NPComplete.NPC_CONVEXHULL.Verifiers;

class ConvexHullVerifier : IVerifier<CONVEXHULL> {

    // --- Fields ---
    public string verifierName { get; } = "Default Verifier";
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
        var pointsList = new List<(double x, double y)>();
        if (string.IsNullOrWhiteSpace(s)) throw new Exception("Certificate is empty or whitespace.");

        s = s.Trim().Trim('{', '}');
        // Normalize
        s = s.Replace("), (", "),(");

        // Split points
        string[] pointsArr = s.Split(new string[] { "),(" }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var point in pointsArr) {
            string cleaned = point.Replace("(", "").Replace(")", "").Trim();
            string[] coords = cleaned.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (coords.Length != 2)
                throw new Exception("Invalid point format: " + point);

            if (!double.TryParse(coords[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double x))
                throw new Exception("Invalid X coordinate: " + coords[0]);

            if (!double.TryParse(coords[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double y))
                throw new Exception("Invalid Y coordinate: " + coords[1]);

            pointsList.Add((x, y));
        }

        return pointsList;
    }
}
