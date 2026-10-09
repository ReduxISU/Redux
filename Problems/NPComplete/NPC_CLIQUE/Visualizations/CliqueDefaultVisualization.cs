using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_CLIQUE.Inherited;
using API.Interfaces.JSON_Objects.Graphs;
using API.Problems.NPComplete.NPC_CLIQUE.Solvers;

namespace API.Problems.NPComplete.NPC_CLIQUE.Visualizations;

class CliqueDefaultVisualization : IVisualization<CLIQUE, API_GraphJSON>, IGraphSubsetVisualization<CLIQUE> {
    public string visualizationName { get; } = "Clique Visualization";
    public string visualizationDefinition { get; } = "This is a default visualization for Clique";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Kaden Marchetti", "Alex Diviney", "Andrija Sevaljevic" };
    public VisualizationType visualizationType { get; } = VisualizationType.GraphD3;
    public ISolver solver { get; } = new CliqueBruteForce();

    // --- Methods Including Constructors ---
    public CliqueDefaultVisualization() {

    }
    public API_GraphJSON visualize(CLIQUE clique) {
        return clique.graph.ToAPIGraph();
    }
    public API_JSON SolvedVisualization(CLIQUE clique, string solution) {
        List<string> solutionList = GraphParser.parseNodeListWithStringFunctions(solution); //Note, this is just a convenience string to list function.
        API_GraphJSON apiGraph = clique.graph.ToAPIGraph();
        for (int i = 0; i < apiGraph.nodes.Count; i++) {
            if (solutionList.Contains(apiGraph.nodes[i].name)) {
                apiGraph.nodes[i].color = "Solution";
            } else { apiGraph.nodes[i].color = "Background"; }
        }

        for (int i = 0; i < apiGraph.links.Count; i++) {
            var edge = apiGraph.links[i];

            if (solutionList.Contains(edge.source) &&
                solutionList.Contains(edge.target)) {
                edge.color = "Solution";
            } else {
                edge.color = "Background";
            }
        }

        return apiGraph;
    }

    // --- Graph picture (format=frames only; the list-format methods above are unchanged) ---
    Type? IVisualization.StepShape => typeof(NodeSet);

    GraphSubsetSpec IGraphSubsetVisualization<CLIQUE>.Describe(CLIQUE clique) =>
        new(clique.nodes, clique.edges, clique.K, SubsetRule.Clique);
}
