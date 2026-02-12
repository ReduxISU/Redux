using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;
using SPADE;

namespace API.Problems.NPComplete.NPC_EXACTCOVER.Visualizers;

class ExactCoverDefaultVisualization : IVisualization<EXACTCOVER>
{
    public string visualizationName { get; } = "Exact Cover Visualization";
    public string visualizationDefinition { get; } = "This is a default visualization for Exact Cover";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Russell Phillips, Andrija Sevaljevic" };
    public string visualizationType { get; } = "Set D3";

    // --- Methods Including Constructors ---
    public ExactCoverDefaultVisualization()
    {

    }
    public API_JSON visualize(EXACTCOVER exactCover)
    {
        return new API_SET(new UtilCollection(exactCover.instance));
    }
    public API_JSON SolvedVisualization(EXACTCOVER exactCover, string solution)
    {
        //return new API_SET(new UtilCollection(exactCover.instance));
        API_SET ec = new API_SET(new UtilCollection(exactCover.instance));
        
        return new API_SET(new UtilCollection(exactCover.instance));
    }
}