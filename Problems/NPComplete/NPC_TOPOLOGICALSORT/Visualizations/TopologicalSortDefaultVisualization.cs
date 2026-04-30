using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects.Graphs;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_TOPOLOGICALSORT.Solvers;

namespace API.Problems.NPComplete.NPC_TOPOLOGICALSORT.Visualizations;

class TopologicalSortDefaultVisualization : IVisualization<TOPOLOGICALSORT>
{
    // --- Fields ---
    public string visualizationName { get; } = "Topological Sort Visualization";
    public string visualizationDefinition { get; } = "This is a default visualization for Topological Sort. Nodes and edges are highlighted in the order they appear in the topological ordering produced by Kahn's Algorithm.";
    public string source { get; } = "Kahn, A. B. (1962). Topological sorting of large networks. Communications of the ACM, 5(11), 558-562.";
    public string[] contributors { get; } = { "Pravesh Aryal", "Koras Koirala", "Dinesh Khanal" };
    public string visualizationType { get; } = "Graph D3";
    public ISolver solver { get; } = new KahnsAlgorithm();

    // --- Constructors ---
    public TopologicalSortDefaultVisualization() { }

    // --- Methods ---
    public API_JSON visualize(TOPOLOGICALSORT problem)
    {
        return problem.graph.ToAPIGraph();
    }

    public API_JSON SolvedVisualization(TOPOLOGICALSORT problem, string solution)
    {
        List<string> solutionNodes = GraphParser.parseNodeListWithStringFunctions(solution);
        API_GraphJSON apiGraph = problem.graph.ToAPIGraph();

        for (int i = 0; i < solutionNodes.Count - 1; i++)
        {
            var from = solutionNodes[i];
            var to = solutionNodes[i + 1];

            var link = apiGraph.links.FirstOrDefault(l =>
                l.source == from && l.target == to
            );
            var node = apiGraph.nodes.FirstOrDefault(n => n.name == solutionNodes[i]);

            if (link != null)
            {
                link.color = "Solution";
                link.delay = ((i + 1) * 5000 / apiGraph.nodes.Count).ToString();
            }
            if (node != null)
            {
                node.color = "Solution";
                node.delay = ((i + 1) * 5000 / solutionNodes.Count).ToString();
            }
        }

        // Color the last node in the ordering
        var lastNode = apiGraph.nodes.FirstOrDefault(n => n.name == solutionNodes[solutionNodes.Count - 1]);
        if (lastNode != null)
        {
            lastNode.color = "Solution";
            lastNode.delay = "5000";
        }

        return apiGraph;
    }
}
