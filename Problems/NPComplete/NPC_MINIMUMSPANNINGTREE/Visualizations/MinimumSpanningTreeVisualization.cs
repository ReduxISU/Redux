using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;
using API.Problems.NPComplete.NPC_MINIMUMSPANNINGTREE.Solvers;

namespace API.Problems.NPComplete.NPC_MINIMUMSPANNINGTREE.Visualizations;

class MinimumSpanningTreeVisualization : IVisualization<MINIMUMSPANNINGTREE> {
        public string visualizationName { get; } = "Minimum Spanning Tree Visualization";
        public string visualizationDefinition { get; } = "Displays a weighted graph and highlights the edges selected for a minimum spanning tree.";
        public string source { get; } = "Original visualization implementation for this repository.";
        public string sourceLink { get; } = string.Empty;
        public string[] contributors { get; } = { "Andreas Kramer", "Val Kimbrough" };
        public VisualizationType visualizationType { get; } = VisualizationType.GraphD3;
        public ISolver solver { get; } = new KruskalSolver();

        public API_JSON visualize(MINIMUMSPANNINGTREE problem) {
                return problem.graph.ToAPIGraph();
        }

        public API_JSON SolvedVisualization(MINIMUMSPANNINGTREE problem, string solution) {
                if (string.IsNullOrWhiteSpace(solution) || solution.Trim() == "{}")
                        return visualize(problem);

                List<KeyValuePair<string, string>> parsedEdges;
                try {
                        parsedEdges = GraphParser.parseUndirectedEdgeListWithStringFunctions(solution);
                }
                catch {
                        return visualize(problem);
                }

                var selectedKeys = new HashSet<string>();
                var selectedNodes = new HashSet<string>();

                foreach (var edge in parsedEdges) {
                        // Normalize undirected edges so either endpoint ordering highlights the same link.
                        selectedKeys.Add(KruskalSolver.CanonicalKey(edge.Key, edge.Value));
                        selectedNodes.Add(edge.Key);
                        selectedNodes.Add(edge.Value);
                }

                API_GraphJSON apiGraph = problem.graph.ToAPIGraph();

                foreach (var node in apiGraph.nodes)
                        node.color = selectedNodes.Contains(node.name) ? "Solution" : "Background";

                foreach (var link in apiGraph.links) {
                        string linkKey = KruskalSolver.CanonicalKey(link.source, link.target);
                        link.color = selectedKeys.Contains(linkKey) ? "Solution" : "Background";
                }

                return apiGraph;
        }
}
