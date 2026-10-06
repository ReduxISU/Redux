using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;
using API.Problems.NPHard.NPH_MINIMUMVERTEXCOVER.Verifiers;
using API.Problems.NPHard.NPH_MINIMUMVERTEXCOVER.Solvers;

namespace API.Problems.NPHard.NPH_MINIMUMVERTEXCOVER.Visualizations;

class MinimumVertexCoverDefaultVisualization : IVisualization<MINIMUMVERTEXCOVER> {
    // --- Fields ---
    public string visualizationName { get; } = "Minimum Vertex Cover Visualization";
    public string visualizationDefinition { get; } = "This is a default visualization for Minimum Vertex Cover";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Russell Phillips" };
    public VisualizationType visualizationType { get; } = VisualizationType.GraphD3;
    public ISolver solver { get; } = new BruteForceMinimumVertexCover();

    // --- Methods Including Constructors ---
    public MinimumVertexCoverDefaultVisualization() {

    }
    public API_JSON visualize(MINIMUMVERTEXCOVER vertexcover) {
        return vertexcover.graph.ToAPIGraph();
    }

    public API_JSON SolvedVisualization(MINIMUMVERTEXCOVER vertexcover, string solution) {
        List<string> solutionNodes = GraphParser.parseNodeListWithStringFunctions(solution);

        API_GraphJSON apiGraph = vertexcover.graph.ToAPIGraph();
        for (int i = 0; i < apiGraph.nodes.Count; i++) {
            if (solutionNodes.Contains(apiGraph.nodes[i].name)) {
                apiGraph.nodes[i].color = "Solution";
            } else { apiGraph.nodes[i].color = "Background"; }
        }
        return apiGraph;
    }
}