using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_CLIQUE.Inherited;
using API.Interfaces.JSON_Objects.Graphs;

namespace API.Problems.NPComplete.NPC_CLIQUE.Visualizers;

class cliqueDefaultVisualization : IVisualization<CLIQUE>
{
    public string visualizationName { get; } = "Clique Visualization";
    public string visualizationDefinition { get; } = "This is a default visualization for clique";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Kaden Marchetti", "Alex Diviney" };
    public string visualizationType { get; } = "Graph D3";

    // --- Methods Including Constructors ---
    public cliqueDefaultVisualization()
    {

    }
    public API_JSON visualize(CLIQUE clique)
    {
        return clique.graph.ToAPIGraph();
    }
    public API_JSON SolvedVisualization(CLIQUE clique, string solution)
    {
        List<string> solutionList = GraphParser.parseNodeListWithStringFunctions(solution); //Note, this is just a convenience string to list function.
        API_GraphJSON apiGraph = clique.graph.ToAPIGraph();
        for (int i = 0; i < apiGraph.nodes.Count; i++)
        {
            if (solutionList.Contains(apiGraph.nodes[i].name))
            { 
                apiGraph.nodes[i].color = "Solution";
            }
            else { apiGraph.nodes[i].color = "Background"; }
        }
        return apiGraph;
    }
}