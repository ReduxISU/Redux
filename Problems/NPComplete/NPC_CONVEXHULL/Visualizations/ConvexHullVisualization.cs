using API.Interfaces;
using API.Interfaces.Geometry;
using API.Interfaces.JSON_Objects;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_CONVEXHULL;
using API.Problems.NPComplete.NPC_CONVEXHULL.Solvers;

namespace API.Problems.NPComplete.NPC_CONVEXHULL.Visualizations;

/// <summary>
/// Convex Hull as the Geometry picture (format=frames): the points to scale, the hulls the selected solver builds and merges,
/// the chain each merge walks and, at the end, the hull's corners in order. The list format is unchanged: still empty.
/// </summary>
class ConvexHullVisualization : IVisualization<CONVEXHULL, API_empty>, IGeometryVisualization<CONVEXHULL> {
    public string visualizationName { get; } = "Convex Hull Visualization";
    public string visualizationDefinition { get; } = "TODO";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "TODO";
    public string[] contributors { get; } = { "TODO" };
    public VisualizationType visualizationType { get; } = VisualizationType.Unimplemented; //either "Boolean Satisfiability" or "Graph D3" most likely
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
    public API_empty visualize(CONVEXHULL instance) {
        //TODO: implement visualization

        return new API_empty();
    }

    public API_JSON SolvedVisualization(CONVEXHULL instance, string solution) {
        //TODO: implement SolvedVisualization (remove method if not implemented)

        return new API_empty();
    }
}