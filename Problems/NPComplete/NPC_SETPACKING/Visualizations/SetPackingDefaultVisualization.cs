using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects;

namespace API.Problems.NPComplete.NPC_SETPACKING.Visualizations;

class SetPackingDefaultVisualization : IVisualization<SETPACKING>
{
    public string visualizationName { get; } = "Set Packing Visualization";
    public string visualizationDefinition { get; } = "Conflict graph visualization for Set Packing";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Sansar Kharal" };
    public string visualizationType { get; } = "Graph D3";

    API_JSON IVisualization<SETPACKING>.visualize(SETPACKING instance)
    {
        return instance.graph.ToAPIGraph();
    }

    API_JSON IVisualization<SETPACKING>.SolvedVisualization(SETPACKING instance, string solution)
    {
        var solutionNodes = GraphParser.parseNodeListWithStringFunctions(solution);
        var graphJson = instance.graph.ToAPIGraph();

        foreach (var node in graphJson.nodes)
        {
            node.color = solutionNodes.Contains(node.name) ? "Solution" : "Background";
        }

        return graphJson;
    }
} 