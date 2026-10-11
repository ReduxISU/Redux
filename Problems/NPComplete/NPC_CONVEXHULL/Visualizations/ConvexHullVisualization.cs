using System.Globalization;
using API.Interfaces;
using API.Interfaces.Geometry;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Tables;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_CONVEXHULL;
using API.Problems.NPComplete.NPC_CONVEXHULL.Solvers;

namespace API.Problems.NPComplete.NPC_CONVEXHULL.Visualizations;

/// <summary>
/// Convex Hull as the Geometry picture (format=frames): the points to scale, the hulls the selected solver builds and merges,
/// the chain each merge walks and, at the end, the hull's corners in order. The list format is a table of the points
/// (<c>DynamicTable</c>): one row per point with its x and y and, once solved, its place among the hull's corners in
/// counterclockwise order, the corners marked Solution.
/// </summary>
class ConvexHullVisualization : IVisualization<CONVEXHULL, API_TableJSON>, IGeometryVisualization<CONVEXHULL> {
    public string visualizationName { get; } = "Convex Hull Visualization";
    public string visualizationDefinition { get; } = "Lists the points with their x and y. Once solved, each corner of the convex hull is numbered in counterclockwise order and shown in the Solution color; points inside the hull or on an edge between two corners are left unnumbered.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "TODO";
    public string[] contributors { get; } = { "TODO" };
    public VisualizationType visualizationType { get; } = VisualizationType.DynamicTable;
    public ISolver solver { get; } = new ConvexHullSolver();

    // The Geometry picture (format=frames only). Implemented explicitly so it is not part of /info.
    Type? IVisualization.StepShape => typeof(HullBuild);
    GeometrySpec IGeometryVisualization<CONVEXHULL>.Describe(CONVEXHULL problem) => Spec(problem);
    HullBuild IGeometryVisualization<CONVEXHULL>.ParseAnswer(CONVEXHULL problem, string answer) => ParseHull(problem, answer);

    /// <summary>The instance as the Geometry picture describes it: its points, in instance order.</summary>
    internal static GeometrySpec Spec(CONVEXHULL problem) => new(problem.points.Select(p => (p.x, p.y)).ToList());

    /// <summary>A hull as the solver writes it (<c>((x,y), (x,y), ...)</c>, corners in order) as the answer's HullBuild;
    /// anything else is no hull.</summary>
    internal static HullBuild ParseHull(CONVEXHULL problem, string answer) {
        if (answer.Trim() == "()") return GeometryFrames.Answer(Spec(problem), []);
        try {
            return GeometryFrames.Answer(Spec(problem), problem.defaultVerifier.ParsePoints(answer).Select(p => (p.x, p.y)).ToList());
        } catch (Exception) {
            return new HullBuild([], [], [], [], []);
        }
    }

    // --- Methods Including Constructors ---
    public ConvexHullVisualization() {

    }

    // The list format: one row per point, in instance order, with its x and y and its place (from 1) among the corners of
    // the hull, counterclockwise as the solver writes them; the corners are marked Solution. With no hull (an answer that
    // does not parse, or no answer) the corner column stays empty.
    private static API_TableJSON Table(CONVEXHULL problem, List<(double x, double y)> corners) {
        var table = new API_TableJSON { title = $"{problem.points.Count} point{(problem.points.Count == 1 ? "" : "s")}" };
        table.columns.Add(new TableColumn { key = "point", label = "Point" });
        table.columns.Add(new TableColumn { key = "x", label = "x" });
        table.columns.Add(new TableColumn { key = "y", label = "y" });
        table.columns.Add(new TableColumn { key = "corner", label = "Hull corner" });
        for (int i = 0; i < problem.points.Count; i++) {
            var (x, y) = problem.points[i];
            int corner = corners.IndexOf((x, y));
            table.rows.Add(new TableRow {
                id = $"p{i + 1}",
                color = corner >= 0 ? "Solution" : null,
                cells = new Dictionary<string, string> {
                    ["point"] = $"P{i + 1}",
                    ["x"] = x.ToString("R", CultureInfo.InvariantCulture),
                    ["y"] = y.ToString("R", CultureInfo.InvariantCulture),
                    ["corner"] = corner >= 0 ? (corner + 1).ToString(CultureInfo.InvariantCulture) : "",
                },
            });
        }
        return table;
    }

    public API_TableJSON visualize(CONVEXHULL problem) => Table(problem, []);

    public API_JSON SolvedVisualization(CONVEXHULL problem, string solution) {
        List<(double x, double y)> corners;
        try {
            corners = solution.Trim() == "()" ? [] : problem.defaultVerifier.ParsePoints(solution);
        } catch (Exception) {
            corners = [];
        }
        return Table(problem, corners);
    }
}