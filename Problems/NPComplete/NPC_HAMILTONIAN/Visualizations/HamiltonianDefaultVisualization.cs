using API.Interfaces;
using System.Text.Json;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects.Graphs;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_HAMILTONIAN.Solvers;

namespace API.Problems.NPComplete.NPC_HAMILTONIAN.Visualizations;

class HamiltonianDefaultVisualization : IVisualization<HAMILTONIAN> {

        // --- Fields ---
        public string visualizationName { get; } = " Hamiltonian Path Visualization";
        public string visualizationDefinition { get; } = "This is a default visualization for Hamiltonian Path";
        public string source { get; } = "";
        public string[] contributors { get; } = { "Andrija Sevaljevic" };
        public VisualizationType visualizationType { get; } = VisualizationType.GraphD3;
        public ISolver solver { get; } = new HamiltonianBruteForce();

        // --- Methods Including Constructors ---
        public HamiltonianDefaultVisualization() {

        }
        public API_JSON visualize(HAMILTONIAN hamiltonian) {
                return hamiltonian.graph.ToAPIGraph();
        }

        public API_JSON SolvedVisualization(HAMILTONIAN hamiltonian, string solution) {
                if (string.IsNullOrWhiteSpace(solution) || solution == "{}")
                        return hamiltonian.graph.ToAPIGraph();

                List<string> solutionNodes = GraphParser.parseNodeListWithStringFunctions(solution)
                    .Where(n => !string.IsNullOrWhiteSpace(n)).ToList();

                if (solutionNodes.Count == 0)
                        return hamiltonian.graph.ToAPIGraph();

                API_GraphJSON apiGraph = hamiltonian.graph.ToAPIGraph();

                for (int i = 0; i < solutionNodes.Count - 1; i++) {
                        var from = solutionNodes[i];
                        var to = solutionNodes[i + 1];

                        var link = apiGraph.links.FirstOrDefault(l =>
                            (l.source == from && l.target == to) || (l.source == to && l.target == from)
                        );
                        var node = apiGraph.nodes.FirstOrDefault(n => n.name == solutionNodes[i]);

                        if (link != null) {
                                link.color = "Solution";
                                link.delay = ((i + 1) * 5000 / apiGraph.nodes.Count).ToString();
                        }

                        if (node != null) {
                                node.color = "Solution";
                                node.delay = ((i + 1) * 5000 / solutionNodes.Count).ToString();
                        }
                }
                return apiGraph;
        }
}