using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.JSON_Objects;
using API.Interfaces.SearchTree;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_GRAPHCOLORING.Solvers;

namespace API.Problems.NPComplete.NPC_GRAPHCOLORING.Visualizations;

/// <summary>
/// Graph Coloring's search tree, a second picture of the backtracking solver: each level colors one node, a color a
/// neighbour already uses is cut, and a node with no color left sends it back to recolor the node above. It is served only
/// by <c>format=frames</c> (type <c>SearchTree</c>). The list format, which old frontends read, is exactly
/// <see cref="GraphColoringDefaultVisualization"/>'s. The problem's default visualization is unchanged.
/// </summary>
class GraphColoringSearchTreeVisualization : IVisualization<GRAPHCOLORING>, ISearchTreeVisualization<GRAPHCOLORING, Groups>, IOptInVisualization {
    public string visualizationName { get; } = "Graph Coloring Search Tree";
    public string visualizationDefinition { get; } = "The choices of Graph Coloring Backtracking as a tree: each level gives one node a color (\"a=1\"), a red dashed node is a color a neighbour already uses, and a solid red node is a color taken back because the nodes below it could not all be colored. With format=frames, the tree grows step by step and the path to the answer turns green.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.GraphD3;
    public ISolver solver { get; } = new GraphColoringBacktracking();

    private readonly GraphColoringDefaultVisualization _listFormat = new();

    public API_JSON visualize(GRAPHCOLORING instance) => _listFormat.visualize(instance);

    public API_JSON SolvedVisualization(GRAPHCOLORING instance, string solution) => _listFormat.SolvedVisualization(instance, solution);

    // --- Search Tree picture (format=frames only) ---
    Type? IVisualization.StepShape => typeof(Groups);

    SearchTreeSpec ISearchTreeVisualization<GRAPHCOLORING, Groups>.Describe(GRAPHCOLORING problem) => new("Graph Coloring");
}
