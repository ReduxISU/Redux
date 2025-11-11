using API.Interfaces;
using System.Text.Json;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects.Graphs;
using API.Interfaces.JSON_Objects;
using SPADE;

namespace API.Problems.NPComplete.NPC_WEIGHTEDCUT.Visualizations;

class WeightedCutDefaultVisualization : IVisualization<WEIGHTEDCUT>
{

    // --- Fields ---
    public string visualizationName { get; } = "Weighted Cut Visualization";
    public string visualizationDefinition { get; } = "This is a default visualization for Cut";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public UtilCollectionGraph graph { get; set; }
    public string visualizationType { get; } = "Graph D3";

    // --- Methods Including Constructors ---
    public WeightedCutDefaultVisualization()
    {

    }
    public API_JSON visualize(WEIGHTEDCUT weightedCut)
    {
        return weightedCut.graph.ToAPIGraph();
    }

    public List<KeyValuePair<string, string>> parseSolution(string solution)
    {
        UtilCollection sol = new(solution);
        return sol.ToList().Select(edge =>
        {
            List<UtilCollection> cast = edge[0].ToList();
            return new KeyValuePair<string, string>(cast[0].ToString(),cast[1].ToString());
        }).ToList();
    }
    public API_JSON SolvedVisualization(WEIGHTEDCUT weightedCut, string solution)
    {
        List<KeyValuePair<string, string>> solutionEdges = parseSolution(solution);

        API_GraphJSON apiGraph = weightedCut.graph.ToAPIGraph();

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