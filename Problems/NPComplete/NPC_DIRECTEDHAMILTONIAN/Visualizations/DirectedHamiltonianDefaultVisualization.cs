using API.Interfaces;
using System.Text.Json;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects.Graphs;
using API.Interfaces.JSON_Objects;

namespace API.Problems.NPComplete.NPC_DIRECTEDHAMILTONIAN.Visualizations;

class DirectedHamiltonianDefaultVisualization : IVisualization<DIRECTEDHAMILTONIAN>
{

    // --- Fields ---
    public string visualizationName { get; } = "Directed Hamiltonian Visualization";
    public string visualizationDefinition { get; } = "This is a default visualization for Directed Hamiltonian";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    public string visualizationType { get; } = "Graph D3";

    // --- Methods Including Constructors ---
    public DirectedHamiltonianDefaultVisualization()
    {

    }
    public API_JSON visualize(DIRECTEDHAMILTONIAN directedHamiltonian)
    {
        return directedHamiltonian.graph.ToAPIGraph();
    }

    public API_JSON SolvedVisualization(DIRECTEDHAMILTONIAN directedHamiltonian, string solution)
    {
        List<string> solutionNodes = GraphParser.parseNodeListWithStringFunctions(solution);

        API_GraphJSON apiGraph = directedHamiltonian.graph.ToAPIGraph();

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
        return apiGraph;
    }
}