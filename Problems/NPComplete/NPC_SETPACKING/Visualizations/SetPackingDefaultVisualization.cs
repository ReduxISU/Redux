using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;
using System.Collections.Generic;

namespace API.Problems.NPComplete.NPC_SETPACKING.Visualizations;

class SetPackingDefaultVisualization : IVisualization<SETPACKING>
{
    public string visualizationName { get; } = "Set Packing Visualization";
    public string visualizationDefinition { get; } = "This is a conflict graph visualization for Set Packing.";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Sansar Kharal" };
    public string visualizationType { get; } = "Graph D3";

    public SetPackingDefaultVisualization()
    {
    }

    public API_JSON visualize(SETPACKING setPacking)
    {
        return setPacking.graph.ToAPIGraph();
    }

    public API_JSON SolvedVisualization(SETPACKING setPacking, string solution)
    {
        List<string> solutionNodes = GraphParser.parseNodeListWithStringFunctions(solution);

        API_GraphJSON apiGraph = setPacking.graph.ToAPIGraph();

        for (int i = 0; i < apiGraph.nodes.Count; i++)
        {
            if (solutionNodes.Contains(apiGraph.nodes[i].name))
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