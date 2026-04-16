using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects.Graphs;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_SETPACKING;

namespace API.Problems.NPComplete.NPC_SETPACKING.Visualizations;

public class SetPackingDefaultVisualization : IVisualization<SETPACKING>
{
    public string visualizationName { get; } = "Set Packing Visualization";
    public string visualizationDefinition { get; } = "Conflict graph";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Sansar Kharal" };
    public string visualizationType { get; } = "Graph D3";

    public API_JSON visualize(SETPACKING instance)
    {
        return instance.graph.ToAPIGraph();
    }

    public API_JSON SolvedVisualization(SETPACKING instance, string solution)
    {
        var nodes = GraphParser.parseNodeListWithStringFunctions(solution);
        var graph = instance.graph.ToAPIGraph();

        foreach (var n in graph.nodes)
            n.color = nodes.Contains(n.name) ? "Solution" : "Background";

        return graph;
    }
}