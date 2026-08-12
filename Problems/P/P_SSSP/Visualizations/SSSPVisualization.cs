using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;
using API.Problems.P.P_SSSP.Solvers;
using API.Problems.P.P_SSSP.Verifiers;

namespace API.Problems.P.P_SSSP.Visualizations;

class SSSPVisualization : IVisualization<SSSP> {
        public string visualizationName { get; } = "Single Source Shortest Path Visualization";
        public string visualizationDefinition { get; } = "Visualizes the Single Source Shortest Path problem for non-negative weighted directed cyclic graphs using Dijkstra's algorithm";
        public string source { get; } = "";
        public string[] contributors { get; } = { "Rajit Nilkar" };
        public VisualizationType visualizationType => VisualizationType.GraphD3;
        public ISolver solver { get; } = new SSSPSolver();
        public IVerifier verifier { get; } = new SSSPVerifier();

        public SSSPVisualization() { }

        public API_JSON visualize(SSSP problem) {
                // For simplicity, we will just return a JSON representation of the graph
                // In a real implementation, this would be more complex and would include visual elements
                return problem.graph.ToAPIGraph();
        }

        // SolvedVisualization: takes a problem instance and a solution certificate,
        // and returns a visualization of the problem instance with the solution highlighted
        public API_JSON SolvedVisualization(SSSP problem, string solution) {
                if (string.IsNullOrWhiteSpace(solution) || solution.Trim() == "{}")
                        return visualize(problem); // No entries so return graph with no highlights

                Dictionary<string, List<string>> pathsByNode;
                try {
                        pathsByNode = SSSPVerifier.ParseSSSPCertificate(solution);
                }
                catch {
                        return visualize(problem); // Invalid solution format, return graph with no highlights
                }

                API_GraphJSON graph = problem.graph.ToAPIGraph();

                var reachedNodes = new HashSet<string>();
                var treeEdges = new HashSet<(string u, string v)>();

                foreach (var (node, path) in pathsByNode) {
                        if (pathsByNode.Count == 0)
                                continue; // unreachable node, no highlight

                        foreach (string pathNode in path)
                                reachedNodes.Add(pathNode);

                        for (int i = 0; i < path.Count - 1; i++)
                                treeEdges.Add((path[i], path[i + 1]));

                        for (int i = 0; i < graph.nodes.Count; i++) {
                                graph.nodes[i].color = reachedNodes.Contains(graph.nodes[i].name) ? "Solution" : "Background";
                        }

                        for (int i = 0; i < graph.links.Count; i++) {
                                var link = graph.links[i];
                                bool isForwardTreeEdge = treeEdges.Contains((link.source, link.target));
                                bool isReversedTreeEdge = !problem.isDirected && treeEdges.Contains((link.source, link.target));

                                link.color = (isForwardTreeEdge || isReversedTreeEdge) ? "Solution" : "Background";
                        }
                }
                return graph;
        }

        // StepsVisualization : takes a problem instance and a list of step objects,
        // and returns a step-by-step visualization with a highlighted path for the current node being processed and final path solution for reachable nodes
        public List<API_JSON> StepsVisualization(SSSP problem, List<Object> steps) {
                var result = new List<API_JSON>();

                for (int s = 0; s < steps.Count; s++) {
                        var step = steps[s] as SSSPSolver.SSSPGraphStep;
                        if (step == null) {
                                result.Add(visualize(problem));
                                continue;
                        }

                        API_GraphJSON graph = problem.graph.ToAPIGraph();
                        var knownNodes = new HashSet<string>(step.knownNodes);

                        for (int i = 0; i < graph.nodes.Count; i++) {
                                if (graph.nodes[i].name == step.currentNode) {
                                        graph.nodes[i].color = "ElementHighlight";
                                        graph.nodes[i].outline = "Purple";
                                }
                                else if (knownNodes.Contains(graph.nodes[i].name)) {
                                        graph.nodes[i].color = "Solution";
                                        graph.nodes[i].outline = "SolutionAlt";
                                }
                                else {
                                        graph.nodes[i].color = "Background";
                                }
                        }

                        var treeEdges = new HashSet<(string from, string to)>(step.treeEdges);
                        (string from, string to)? currentEdge = step.currentEdgeFrom != null && step.currentNode != null ? (step.currentEdgeFrom, step.currentNode) : null;

                        for (int i = 0; i < graph.links.Count; i++) {
                                var link = graph.links[i];
                                var forward = (link.source, link.target);
                                var reverse = (link.source, link.target);

                                bool isCurrentEdge = currentEdge.HasValue && (forward == currentEdge.Value || (!problem.isDirected && reverse == currentEdge.Value));
                                bool isTreeEdge = treeEdges.Contains(forward) || (!problem.isDirected && treeEdges.Contains(reverse));

                                if (isCurrentEdge)
                                        link.color = "ElementHighlight";
                                else if (isTreeEdge)
                                        link.color = "Solution";
                                else
                                        link.color = "Background";
                        }
                        result.Add(graph);
                }
                return result;
        }
}