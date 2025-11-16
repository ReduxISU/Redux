using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;
using API.Problems.NPComplete.NPC_VERTEXCOVER;

namespace API.Problems.NPComplete.NPC_VERTEXCOVER.Visualizations;

class VertexCoverDefaultVisualization : IVisualization<VERTEXCOVER>
{
    // --- Fields ---
    public string visualizationName { get; } = "Vertex Cover Visualization";
    public string visualizationDefinition { get; } = "This is a default visualization for Vertex Cover";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Russell Phillips" };
    public string visualizationType { get; } = "Graph D3";

    // --- Methods Including Constructors ---
    public VertexCoverDefaultVisualization()
    {

    }
    public API_JSON visualize(VERTEXCOVER vertexcover)
    {
        return vertexcover.graph.ToAPIGraph();
    }

    public API_JSON SolvedVisualization(VERTEXCOVER vertexcover, string solution)
    {
        List<string> solutionNodes = GraphParser.parseNodeListWithStringFunctions(solution);

        API_GraphJSON apiGraph = vertexcover.graph.ToAPIGraph();
        for (int i = 0; i < apiGraph.nodes.Count; i++)
        {
            if (solutionNodes.Contains(apiGraph.nodes[i].name))
            {
                apiGraph.nodes[i].color = "Solution";
            }
            else { apiGraph.nodes[i].color = "Background"; }
        }
        return apiGraph;
    }
}