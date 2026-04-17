using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;
using SPADE;

namespace API.Problems.NPComplete.NPC_SETCOVER.Visualizations;

class SetCoverDefaultVisualization : IVisualization<SETCOVER>
{
    public string visualizationName { get; } = "Set Cover Visualization";
    public string visualizationDefinition { get; } = "This is a default visualization for Set Cover";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public string visualizationType { get; } = "Set D3";

    // --- Methods Including Constructors ---
    public SetCoverDefaultVisualization()
    {

    }
    public API_JSON visualize(SETCOVER setcover)
    {
        return new API_SET(new UtilCollection(setcover.instance));
    }
    public API_JSON SolvedVisualization(SETCOVER setcover, string solution)
    {
        //return new API_SET(new UtilCollection(setcover.instance));
        API_SET sc = new API_SET(new UtilCollection(setcover.instance));

        return new API_SET(new UtilCollection(setcover.instance));
    }
}