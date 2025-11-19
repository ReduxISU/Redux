using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;
using SPADE;

namespace API.Problems.NPComplete.NPC_EXACTCOVER.Visualizers;

class ExactCoverDefaultVisualization : IVisualization<EXACTCOVER>
{
    public string visualizationName { get; } = "Clique Visualization";
    public string visualizationDefinition { get; } = "This is a default visualization for clique";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Russell Phillips" };
    public string visualizationType { get; } = "Set";

    // --- Methods Including Constructors ---
    public ExactCoverDefaultVisualization()
    {

    }
    public API_JSON visualize(EXACTCOVER clique)
    {
        return new API_SET(new UtilCollection(clique.instance));
    }
    public API_JSON SolvedVisualization(EXACTCOVER clique, string solution)
    {
        return new API_SET(new UtilCollection(clique.instance));
    }
}