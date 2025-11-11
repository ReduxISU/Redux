using API.Interfaces;
using System.Text.Json;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects.Graphs;
using API.Interfaces.JSON_Objects;

namespace API.Problems.NPComplete.NPC_STEINERTREE.Visualizations;

class SteinerTreeDefaultVisualization : IVisualization<STEINERTREE>
{

    // --- Fields ---
    public string visualizationName { get; } = "Steiner Tree Visualization";
    public string visualizationDefinition { get; } = "This is a default visualization for Steiner Tree";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public UtilCollectionGraph graph { get; set; }
    public string visualizationType { get; } = "Graph D3";

    // --- Methods Including Constructors ---
    public SteinerTreeDefaultVisualization()
    {

    }
    public API_JSON visualize(STEINERTREE steinerTree)
    {
        return steinerTree.graph.ToAPIGraph();
    }

    public API_JSON SolvedVisualization(STEINERTREE steinerTree, string solution)
    {
        List<KeyValuePair<string, string>> solutionEdges = GraphParser.parseUndirectedEdgeListWithStringFunctions(solution);

        API_GraphJSON apiGraph = steinerTree.graph.ToAPIGraph();
        foreach (var kv in solutionEdges)
            foreach (var node in apiGraph.nodes)
            {
                if (node.name == kv.Key || node.name == kv.Value) node.color = "Solution";
                if (steinerTree.terminals.Contains(node.name)) node.outline = "Red";
            }

        foreach (var kv in solutionEdges)
        {
            var link = apiGraph.links.FirstOrDefault(l =>
                (l.source == kv.Key && l.target == kv.Value) ||
                (l.source == kv.Value && l.target == kv.Key)
            );

            if (link != null)
                link.color = "Solution";
        }
        return apiGraph;
    }
}