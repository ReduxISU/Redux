using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Interfaces.SearchTree;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_VERTEXCOVER.Solvers;

namespace API.Problems.NPComplete.NPC_VERTEXCOVER.Visualizations;

/// <summary>
/// Vertex Cover's search tree, a second picture of the bounded search tree solver: each level takes one end of the first
/// edge still uncovered into the cover, a branch that runs out of budget with edges left is cut, and an end whose branches
/// all fail is taken back. It is served only by <c>format=frames</c> (type <c>SearchTree</c>). The list format, which old
/// frontends read, is exactly <see cref="VertexCoverDefaultVisualization"/>'s. The problem's default visualization is
/// unchanged.
/// </summary>
class VertexCoverSearchTreeVisualization : IVisualization<VERTEXCOVER>, ISearchTreeVisualization<VERTEXCOVER, NodeSet>, IOptInVisualization {
    public string visualizationName { get; } = "Vertex Cover Search Tree";
    public string visualizationDefinition { get; } = "The choices of Vertex Cover Bounded Search Tree as a tree: each level takes one end (\"a\") of the first edge still uncovered into the cover, a red dashed node is an end after which the budget runs out with edges left, and a solid red node is an end taken back because neither end of the next edge worked. With format=frames, the tree grows step by step and the path to the answer turns green.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.GraphD3;
    public ISolver solver { get; } = new VertexCoverBoundedSearchTree();

    private readonly VertexCoverDefaultVisualization _listFormat = new();

    public API_JSON visualize(VERTEXCOVER instance) => _listFormat.visualize(instance);

    public API_JSON SolvedVisualization(VERTEXCOVER instance, string solution) => _listFormat.SolvedVisualization(instance, solution);

    // --- Search Tree picture (format=frames only) ---
    Type? IVisualization.StepShape => typeof(NodeSet);

    SearchTreeSpec ISearchTreeVisualization<VERTEXCOVER, NodeSet>.Describe(VERTEXCOVER problem) => new("Vertex Cover");
}
