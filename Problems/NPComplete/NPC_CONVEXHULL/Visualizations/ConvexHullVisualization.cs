using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_CONVEXHULL;

namespace API.Problems.NPComplete.NPC_CONVEXHULL.Visualizations;

class ConvexHullVisualization : IVisualization<CONVEXHULL>
{
    public string visualizationName { get; } = "Convex Hull Visualization";
    public string visualizationDefinition { get; } = "TODO";
    public string source { get; } = "";
    public string sourceLink {get;} = "TODO";
    public string[] contributors { get; } = { "TODO" };
    public string visualizationType { get; } = "TODO"; //either "Boolean Satisfiability" or "Graph D3" most likely

    // --- Methods Including Constructors ---
    public ConvexHullVisualization()
    {

    }
    public API_JSON visualize(CONVEXHULL instance)
    {
        //TODO: implement visualization
        
        return new API_empty();
    }

    public API_JSON SolvedVisualization(CONVEXHULL instance, string solution)
    {
        //TODO: implement SolvedVisualization (remove method if not implemented)

        return new API_empty();
    }
}