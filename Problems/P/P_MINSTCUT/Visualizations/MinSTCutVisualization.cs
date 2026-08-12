using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;
using API.Problems.P.P_MINSTCUT.Solvers;

namespace API.Problems.P.P_MINSTCUT.Visualizations;

class MinSTCutVisualization : IVisualization<MINSTCUT> {
        public string visualizationName { get; } = "Minimum S-T Cut Visualization";
        public string visualizationDefinition { get; } = "Displays a weighted directed graph and highlights the minimum S-T cut: S-side nodes are colored and cut edges (directed from S to T) are colored and dashed.";
        public string source { get; } = "";
        public string[] contributors { get; } = { "Michael Trosper" };
        public VisualizationType visualizationType { get; } = VisualizationType.GraphD3;
        public ISolver solver { get; } = new MinSTCutSolver();

        public MinSTCutVisualization() { }

        public API_JSON visualize(MINSTCUT problem) => problem.graph.ToAPIGraph();

        public API_JSON SolvedVisualization(MINSTCUT problem, string solution) {
                if (string.IsNullOrWhiteSpace(solution) || solution.Trim() == "{}")
                        return visualize(problem);

                API_GraphJSON apiGraph = problem.graph.ToAPIGraph();

                HashSet<string> sNodes = solution.Trim()
                    .TrimStart('{').TrimEnd('}')
                    .Split(',')
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToHashSet();

                // Color cut edges directed from S to T.
                foreach (var link in apiGraph.links) {
                        bool srcInS = sNodes.Contains(link.source);
                        bool dstInS = sNodes.Contains(link.target);
                        if (srcInS && !dstInS) {
                                link.color = "Solution";
                                link.dashed = "True";
                        }
                }

                // Color S-side nodes; T-side nodes become Background.
                foreach (var node in apiGraph.nodes)
                        node.color = sNodes.Contains(node.name) ? "Solution" : "Background";

                return apiGraph;
        }
}
