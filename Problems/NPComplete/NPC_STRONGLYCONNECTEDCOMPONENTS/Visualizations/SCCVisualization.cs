using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_STRONGLYCONNECTEDCOMPONENTS.Solvers;

namespace API.Problems.NPComplete.NPC_STRONGLYCONNECTEDCOMPONENTS.Visualizations;

/// <summary>
/// Strongly Connected Components as the Layered Graph picture (format=frames): Kosaraju's two passes, the finish numbers
/// of the first and the components of the second. The list format draws the graph, and its solved picture the edges that
/// lie inside a component. It is the problem's default visualization.
/// </summary>
class SCCVisualization : IVisualization<STRONGLYCONNECTEDCOMPONENTS, API_GraphJSON>, ILayeredVisualization<STRONGLYCONNECTEDCOMPONENTS> {
    public string visualizationName { get; } = "Strongly Connected Components Visualization";
    public string visualizationDefinition { get; } = "Draws the directed graph in ranks, left to right, and shows Kosaraju's Algorithm finding the strongly connected components: the first pass numbers the nodes as it finishes them, and the second pass collects each component.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.GraphD3;
    public ISolver solver { get; } = new KosarajuSolver();

    // The Layered Graph picture (format=frames only; /info keeps GraphD3). Implemented explicitly so it is not part of /info.
    Type? IVisualization.StepShape => typeof(Components);
    LayeredSpec ILayeredVisualization<STRONGLYCONNECTEDCOMPONENTS>.Describe(STRONGLYCONNECTEDCOMPONENTS problem) => Spec(problem);

    /// <summary>The instance as the Layered Graph picture describes it.</summary>
    internal static LayeredSpec Spec(STRONGLYCONNECTEDCOMPONENTS problem) => new(
        problem.graph.Nodes.ToList().Select(n => n.ToString()).ToList(),
        problem.graph.Edges.ToList().Select(e => new LayeredEdgeInput(e[0].ToString(), e[1].ToString())).ToList(),
        LayeredRule.StronglyConnectedComponents);

    public SCCVisualization() { }

    public API_GraphJSON visualize(STRONGLYCONNECTEDCOMPONENTS problem) => problem.graph.ToAPIGraph();

    public API_JSON SolvedVisualization(STRONGLYCONNECTEDCOMPONENTS problem, string solution) {
        API_GraphJSON graph = problem.graph.ToAPIGraph();
        var of = LayeredFrames.ParseComponents(solution).Of;
        foreach (var link in graph.links)
            if (of.TryGetValue(link.source, out int a) && of.TryGetValue(link.target, out int b) && a == b)
                link.color = "Solution";
        return graph;
    }
}
