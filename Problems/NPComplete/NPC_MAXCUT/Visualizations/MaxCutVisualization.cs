using API.Interfaces;
using System.Text.Json;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects.Graphs;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_MAXCUT.Solvers;

namespace API.Problems.NPComplete.NPC_MAXCUT.Visualizations;

class MaxCutVisualization : IVisualization<MAXCUT>
{

    // --- Fields ---
    public string visualizationName { get; } = " Max Cut Visualization";
    public string visualizationDefinition { get; } = "TODO";
    public string source { get; } = "TODO";
    public string[] contributors { get; } = { "Max Gruenwoldt" };
    public string visualizationType { get; } = "TODO";
    public ISolver solver { get; } = new MaxCutSolver();

    // --- Methods Including Constructors ---
    public MaxCutVisualization()
    {

    }
    public API_JSON visualize(MAXCUT maxcut)
    {
        return maxcut.graph.ToAPIGraph();
    }

    public API_JSON SolvedVisualization(MAXCUT maxcut, string solution)
    {
        List<KeyValuePair<string, string>> solutionEdges = GraphParser.parseUndirectedEdgeListWithStringFunctions(solution);
        // removing duplicate edges since visualization cares about first edge only
        for (int i = solutionEdges.Count - 1; i >= 0; i--)
            if (i % 2 == 1) solutionEdges.RemoveAt(i);

        API_GraphJSON apiGraph = maxcut.graph.ToAPIGraph();

        foreach (var edge in solutionEdges)
        {
            var link = apiGraph.links.FirstOrDefault(l =>
                (l.source == edge.Key && l.target == edge.Value) || (l.source == edge.Value && l.target == edge.Key)
            );

            var node = apiGraph.nodes.FirstOrDefault(n => n.name == edge.Key);

            if (link != null)
            {
                link.color = "Solution";
                link.dashed = "True";
            }

            if (node != null)
            {
                node.color = "Solution";
            }
        }

        foreach (var link in apiGraph.links)
        {
            var node1 = apiGraph.nodes.FirstOrDefault(n => n.name == link.source);
            var node2 = apiGraph.nodes.FirstOrDefault(n => n.name == link.target);
            if (node1 != null && node2 != null && node1.color == "Solution" && node2.color == "Solution")
                link.color = "Solution";
        }

        return apiGraph;
    }
}