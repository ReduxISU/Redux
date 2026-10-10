using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;
using API.Problems.NPComplete.NPC_MAXCUT.Solvers;

namespace API.Problems.NPComplete.NPC_MAXCUT.Visualizations;

class MaxCutVisualization : IVisualization<MAXCUT, API_GraphJSON>, IGraphCutVisualization<MAXCUT> {
    public string visualizationName { get; } = "Max Cut Visualization";
    public string visualizationDefinition { get; } = "Displays a weighted undirected graph and highlights the edges belonging to the maximum cut, coloring S-side nodes and crossing edges.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Max Grünwoldt", "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.GraphD3;
    public ISolver solver { get; } = new MaxCutSolver();

    public MaxCutVisualization() { }

    public API_GraphJSON visualize(MAXCUT problem) => problem.graph.ToAPIGraph();

    public API_JSON SolvedVisualization(MAXCUT problem, string solution) {
        if (string.IsNullOrWhiteSpace(solution) || solution.Trim() == "{}")
            return visualize(problem);

        API_GraphJSON apiGraph = problem.graph.ToAPIGraph();

        // solution is the S-side node set: {node1,node2,...}
        HashSet<string> sNodes = solution.Trim()
            .TrimStart('{').TrimEnd('}')
            .Split(',')
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToHashSet();

        // Color edges crossing the cut (one endpoint in S, one in T).
        foreach (var link in apiGraph.links) {
            bool srcInS = sNodes.Contains(link.source);
            bool dstInS = sNodes.Contains(link.target);
            if (srcInS != dstInS) {
                link.color = "Solution";
                link.dashed = "True";
            }
        }

        // Color S-side nodes.
        foreach (var node in apiGraph.nodes)
            if (sNodes.Contains(node.name))
                node.color = "Solution";

        return apiGraph;
    }

    // --- Graph picture (format=frames only; the list-format methods above are unchanged) ---
    Type? IVisualization.StepShape => typeof(Groups);

    GraphEdgeSpec IGraphCutVisualization<MAXCUT>.Describe(MAXCUT problem) =>
        new(problem.nodes, problem.listedEdges.Select(e => new GraphEdgeInput(e.source, e.destination, e.weight)).ToList(), EdgeRule.MaxCut);

    // Max Cut's answer is the S side itself, not the cut edges.
    Groups IGraphCutVisualization<MAXCUT>.ParseAnswer(MAXCUT problem, string answer) {
        var sSide = answer.Replace("{", "").Replace("}", "").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        return sSide.Count == 0 ? new Groups([]) : GraphEdgeFrames.Split(problem.nodes, sSide);
    }
}
