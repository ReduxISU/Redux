using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Interfaces.SearchTree;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_EXACTCOVER.Solvers;

namespace API.Problems.NPComplete.NPC_EXACTCOVER.Visualizations;

/// <summary>
/// Exact Cover's search tree, a second picture of Knuth's Algorithm X: each level takes one set for the uncovered element
/// with the fewest sets left, an element with no set left is cut, and a set after which the rest could not be covered is
/// taken back. Exact Cover Recursive draws it too (with <c>solver=ExactCoverRecursive</c>). It is served only by
/// <c>format=frames</c> (type <c>SearchTree</c>). The list format, which old frontends read, is exactly
/// <see cref="ExactCoverDefaultVisualization"/>'s. The problem's default visualization is unchanged.
/// </summary>
class ExactCoverSearchTreeVisualization : IVisualization<EXACTCOVER>, ISearchTreeVisualization<EXACTCOVER, ChosenSets>, IOptInVisualization {
    public string visualizationName { get; } = "Exact Cover Search Tree";
    public string visualizationDefinition { get; } = "The choices of Knuth's Algorithm X as a tree: each level takes a set (\"S2\") for the uncovered element with the fewest sets left, a red dashed node is an element no set left can cover, and a solid red node is a set taken back because the rest could not be covered with it. With format=frames, the tree grows step by step and the path to the answer turns green.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.SetD3;
    public ISolver solver { get; } = new DancingLinks();

    private readonly ExactCoverDefaultVisualization _listFormat = new();

    public API_JSON visualize(EXACTCOVER instance) => _listFormat.visualize(instance);

    public API_JSON SolvedVisualization(EXACTCOVER instance, string solution) => _listFormat.SolvedVisualization(instance, solution);

    // --- Search Tree picture (format=frames only) ---
    Type? IVisualization.StepShape => typeof(ChosenSets);

    SearchTreeSpec ISearchTreeVisualization<EXACTCOVER, ChosenSets>.Describe(EXACTCOVER problem) => new("Exact Cover");
}
