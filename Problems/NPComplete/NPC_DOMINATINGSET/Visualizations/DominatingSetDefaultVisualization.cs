using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;

namespace API.Problems.NPComplete.NPC_DOMINATINGSET.Visualizations;

class DominatingSetDefaultVisualization : IVisualization<DOMINATINGSET>
{
    public string visualizationName { get; } = "Dominating Set Visualization";
    public string visualizationDefinition { get; } =
        "This is a default visualization for dominating set";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Quinton Smith" };
    public string visualizationType { get; } = "Graph D3";

    // --- Methods Including Constructors ---
    public DominatingSetDefaultVisualization() { }

    public API_JSON visualize(DOMINATINGSET dominatingSet)
    {
        return dominatingSet.graph.ToAPIGraph();
    }

    public API_JSON SolvedVisualization(DOMINATINGSET dominatingSet, string solution)
    {
        List<string> solutionList = GraphParser.parseNodeListWithStringFunctions(solution);

        API_GraphJSON apiGraph = dominatingSet.graph.ToAPIGraph();

        for (int i = 0; i < apiGraph.nodes.Count; i++)
        {
            if (solutionList.Contains(apiGraph.nodes[i].name))
            {
                apiGraph.nodes[i].color = "Solution";
            }
            else
            {
                apiGraph.nodes[i].color = "Background";
            }
        }
        return apiGraph;
    }
}
