using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;
using API.Problems.P.P_SPSP.Solvers;

namespace API.Problems.P.P_SPSP.Visualizations;

class SPSPVisualization : IVisualization<SPSP> {
        public string visualizationName { get; } = "Single Pair Shortest Path Visualization";
        public string visualizationDefinition { get; } = "Visualizes the Single Pair Shortest Path problem for non-negative weighted directed cyclic graphs using Dijkstra's algorithm";
        public string source { get; } = "";
        public string[] contributors { get; } = { "Rajit Nilkar", "Scott Barfuss", "Tiger Sant", "Malaya Witt" };
        public VisualizationType visualizationType => VisualizationType.GraphD3;
        public ISolver solver { get; } = new SPSPSolver();

        public SPSPVisualization() { }

        public API_JSON visualize(SPSP problem) {
                // For simplicity, we will just return a JSON representation of the graph
                // In a real implementation, this would be more complex and would include visual elements
                return problem.graph.ToAPIGraph();
        }

        // SolvedVisualization: takes a problem instance and a solution certificate,
        // and returns a visualization of the problem instance with the solution highlighted
        public API_JSON SolvedVisualization(SPSP problem, string solution) {
                if (string.IsNullOrWhiteSpace(solution) || solution.Trim() == "{}")
                        // No path found, return graph with no highlights
                        return visualize(problem);

                List<string> path;
                try {
                        // Parse the solution as a path
                        path = GraphParser.parseNodeListWithStringFunctions(solution);
                }
                catch {
                        // Invalid solution format, return graph with no highlights
                        return visualize(problem);
                }

                API_GraphJSON graph = problem.graph.ToAPIGraph(); // Convert to API graph format

                var pathNodes = new HashSet<string>(path);
                for (int i = 0; i < graph.nodes.Count; i++)
                        graph.nodes[i].color = pathNodes.Contains(graph.nodes[i].name)
                        ? "Solution" : "Background";

                var pathEdges = new HashSet<(string u, string v)>();
                for (int i = 0; i < path.Count - 1; i++)
                        pathEdges.Add((path[i], path[i + 1]));

                for (int i = 0; i < graph.links.Count; i++) {
                        var link = graph.links[i];
                        bool isForwardPathEdge = pathEdges.Contains((link.source, link.target));
                        bool isReversePathEdge = !problem.isDirected && pathEdges.Contains((link.target, link.source));

                        if (isForwardPathEdge || isReversePathEdge)
                                link.color = "Solution";
                        else
                                link.color = "Background";
                }
                return graph;
        }

        public List<API_JSON> StepsVisualization(SPSP problem, List<Object> steps) {
                var result = new List<API_JSON>();

                for (int s = 0; s < steps.Count; s++) {
                        string? step = steps[s].ToString();

                        if (string.IsNullOrWhiteSpace(step) || step.Trim() == "{}") {
                                result.Add(visualize(problem));
                                continue;
                        }

                        List<string> path;
                        try {
                                path = GraphParser.parseNodeListWithStringFunctions(step);
                        }
                        catch {
                                result.Add(visualize(problem));
                                continue;
                        }

                        API_GraphJSON graph = problem.graph.ToAPIGraph();

                        string? currentNode = path.Count > 0 ? path[path.Count - 1] : null;
                        var pathNodes = new HashSet<string>(path);

                        for (int i = 0; i < graph.nodes.Count; i++) {
                                if (graph.nodes[i].name == currentNode) {
                                        graph.nodes[i].color = "ElementHighlight";
                                        graph.nodes[i].outline = "Purple";
                                }
                                else if (pathNodes.Contains(graph.nodes[i].name)) {
                                        graph.nodes[i].color = "Solution";
                                }
                                else {
                                        graph.nodes[i].color = "Background";
                                }
                        }

                        var pathEdges = new HashSet<(string u, string v)>();
                        for (int i = 0; i < path.Count - 1; i++)
                                pathEdges.Add((path[i], path[i + 1]));

                        for (int i = 0; i < graph.links.Count; i++) {
                                var link = graph.links[i];
                                bool isForwardPathEdge = pathEdges.Contains((link.source, link.target));
                                bool isReversedPathEdge = !problem.isDirected && pathEdges.Contains((link.target, link.source));

                                link.color = (isForwardPathEdge || isReversedPathEdge) ? "Solution" : "Background";
                        }
                        result.Add(graph);
                }

                return result;
        }
}
