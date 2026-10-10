using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;
using API.Interfaces.Steps;
using API.Problems.P.P_MINSTCUT.Solvers;

namespace API.Problems.P.P_MINSTCUT.Visualizations;

class MinSTCutVisualization : IVisualization<MINSTCUT, API_GraphJSON>, IFlowVisualization<MINSTCUT> {
    public string visualizationName { get; } = "Minimum S-T Cut Visualization";
    public string visualizationDefinition { get; } = "Displays a weighted directed graph and highlights the minimum S-T cut: S-side nodes are colored and cut edges (directed from S to T) are colored and dashed.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.GraphD3;
    public ISolver solver { get; } = new MinSTCutSolver();

    // The Flow Network picture (format=frames only; /info keeps GraphD3). Implemented explicitly so it is not part of /info.
    // The list format gets none of the typed steps (it never drew steps), so it is unchanged.
    Type? IVisualization.StepShape => typeof(NetworkFlow);
    FlowSpec IFlowVisualization<MINSTCUT>.Describe(MINSTCUT problem) => Spec(problem);

    /// <summary>The instance as the Flow Network picture describes it.</summary>
    internal static FlowSpec Spec(MINSTCUT problem) =>
        new(problem.nodes, problem.edges.Select(e => new FlowEdgeInput(e.from, e.to, e.weight)).ToList(), problem.sourceNode, problem.targetNode);

    public MinSTCutVisualization() { }

    public API_GraphJSON visualize(MINSTCUT problem) => problem.graph.ToAPIGraph();

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
