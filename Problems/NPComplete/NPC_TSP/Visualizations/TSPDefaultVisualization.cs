using API.Interfaces;
using System.Text.Json;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects.Graphs;
using API.Interfaces.JSON_Objects;

namespace API.Problems.NPComplete.NPC_TSP.Visualizations;

class TSPDefaultVisualization : IVisualization<TSP>
{

    // --- Fields ---
    public string visualizationName { get; } = "Travelling Sales Person Visualization";
    public string visualizationDefinition { get; } = "This is a default visualization for Travelling Sales Person";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public string visualizationType { get; } = "Graph D3";

    // --- Methods Including Constructors ---
    public TSPDefaultVisualization()
    {

    }
    public API_JSON visualize(TSP tsp)
    {
        return tsp.graph.ToAPIGraph();
    }

    public API_JSON SolvedVisualization(TSP tsp, string solution)
    {
        List<string> solutionNodes = GraphParser.parseNodeListWithStringFunctions(solution);

        API_GraphJSON apiGraph = tsp.graph.ToAPIGraph();
        for (int i = 0; i < apiGraph.nodes.Count; i++)
            apiGraph.nodes[i].color = "Solution";

        for (int i = 0; i < solutionNodes.Count - 1; i++)
        {
            var from = solutionNodes[i];
            var to = solutionNodes[i + 1];

            var link = apiGraph.links.FirstOrDefault(l =>
                (l.source == from && l.target == to) ||
                (l.source == to && l.target == from)
            );

            if (link != null)
                link.color = "Solution";
        }
        return apiGraph;
    }
}