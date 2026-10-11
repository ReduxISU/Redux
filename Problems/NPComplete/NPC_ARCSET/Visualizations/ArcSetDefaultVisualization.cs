using API.Interfaces;
using System.Text.Json;
using API.Interfaces.Graphs.GraphParser;
using API.Interfaces.JSON_Objects.Graphs;
using API.Interfaces.JSON_Objects;
using SPADE;
using API.Interfaces.Graphs;
using API.Problems.NPComplete.NPC_ARCSET.Solvers;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_ARCSET.Visualizations;

class ArcSetDefaultVisualization : IVisualization<ARCSET, API_GraphJSON>, ILayeredVisualization<ARCSET> {

    // --- Fields ---
    public string visualizationName { get; } = "Arc Set Visualization";
    public string visualizationDefinition { get; } = "This is a default visualization for Arc Set";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Russell Phillips" };
    public VisualizationType visualizationType { get; } = VisualizationType.GraphD3;

    public ISolver solver { get; } = new ArcSetBruteForce();

    // The Layered Graph picture (format=frames only; /info keeps GraphD3). Implemented explicitly so it is not part of /info.
    Type? IVisualization.StepShape => typeof(EdgeSet);
    LayeredSpec ILayeredVisualization<ARCSET>.Describe(ARCSET problem) => Spec(problem);

    /// <summary>The instance as the Layered Graph picture describes it.</summary>
    internal static LayeredSpec Spec(ARCSET problem) => new(
        problem.graph.Nodes.ToList().Select(n => n.ToString()).ToList(),
        problem.graph.Edges.ToList().Select(e => new LayeredEdgeInput(e[0].ToString(), e[1].ToString())).ToList(),
        LayeredRule.FeedbackArcSet) { K = problem.K };

    // --- Methods Including Constructors ---
    public ArcSetDefaultVisualization() {

    }

    private bool LinkInUtil(UtilCollection solutionEdges, API_Link link) {
        foreach (UtilCollection Edge in solutionEdges) {
            if (Edge[0].ToString() == link.source && Edge[1].ToString() == link.target) return true;
        }
        return false;
    }

    public API_GraphJSON visualize(ARCSET arcset) {
        return arcset.graph.ToAPIGraph();
    }
    public API_JSON SolvedVisualization(ARCSET arcset, string solution) {
        UtilCollection solutionEdges = new(solution);

        API_GraphJSON apiGraph = arcset.graph.ToAPIGraph();
        for (int i = 0; i < apiGraph.links.Count; i++) {
            if (LinkInUtil(solutionEdges, apiGraph.links[i])) {
                apiGraph.links[i].color = "Red";
            } else { apiGraph.links[i].color = "Background"; }
        }
        return apiGraph;
    }
}