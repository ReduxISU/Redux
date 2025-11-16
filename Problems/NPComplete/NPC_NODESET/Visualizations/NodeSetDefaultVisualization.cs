using API.Interfaces;
using System.Text.Json;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects.Graphs;
using API.Interfaces.JSON_Objects;

namespace API.Problems.NPComplete.NPC_NODESET.Visualizations;

class NodeSetDefaultVisualization : IVisualization<NODESET>
{

    // --- Fields ---
    public string visualizationName { get; } = "Node Set Visualization";
    public string visualizationDefinition { get; } = "This is a default visualization for Node Set";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public UtilCollectionGraph graph { get; set; }
    public string visualizationType { get; } = "Graph D3";

    // --- Methods Including Constructors ---
    public NodeSetDefaultVisualization()
    {

    }
    public API_JSON visualize(NODESET nodeSet)
    {
        return nodeSet.graph.ToAPIGraph();
    }

    public API_JSON SolvedVisualization(NODESET nodeSet, string solution)
    {
        List<string> solutionNodes = GraphParser.parseNodeListWithStringFunctions(solution);

        API_GraphJSON apiGraph = nodeSet.graph.ToAPIGraph();
        
        for (int i = 0; i < apiGraph.nodes.Count; i++)
            if(solutionNodes.Contains(apiGraph.nodes[i].name))
                apiGraph.nodes[i].color = "Solution";

        foreach (var node in solutionNodes)
            foreach (var link in apiGraph.links)
                if (link.source == node || link.target == node)
                    link.color = "Red";

        return apiGraph;
    }
}