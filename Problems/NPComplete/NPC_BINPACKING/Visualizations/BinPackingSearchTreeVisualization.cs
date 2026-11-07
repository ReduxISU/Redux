using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Interfaces.SearchTree;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_BINPACKING.Solvers;

namespace API.Problems.NPComplete.NPC_BINPACKING.Visualizations;

/// <summary>
/// Bin Packing's search tree, a second picture of the brute force backtracking search: each level puts the next item in a
/// bin with room for it, an item with no bin that has room is cut, and an item put in a bin after which the items left
/// could not all be packed is taken back. It is served only by <c>format=frames</c> (type <c>SearchTree</c>). The list
/// format, which old frontends read, is exactly <see cref="BinPackingVisualization"/>'s. The problem's default
/// visualization is unchanged.
/// </summary>
class BinPackingSearchTreeVisualization : IVisualization<BINPACKING>, ISearchTreeVisualization<BINPACKING, Placement>, IOptInVisualization {
    public string visualizationName { get; } = "Bin Packing Search Tree";
    public string visualizationDefinition { get; } = "The choices of Bin Packing Brute Force as a tree: each level puts the next item in a bin (\"4 in bin 2\"), a red dashed node is an item with no bin that has room for it, and a solid red node is an item taken back out of its bin because the items after it could not all be packed. With format=frames, the tree grows step by step and the path to the answer turns green.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.SetD3;
    public ISolver solver { get; } = new BinPackingBruteForce();

    private readonly BinPackingVisualization _listFormat = new();

    public API_JSON visualize(BINPACKING instance) => _listFormat.visualize(instance);

    public API_JSON SolvedVisualization(BINPACKING instance, string solution) => _listFormat.SolvedVisualization(instance, solution);

    // --- Search Tree picture (format=frames only) ---
    Type? IVisualization.StepShape => typeof(Placement);

    SearchTreeSpec ISearchTreeVisualization<BINPACKING, Placement>.Describe(BINPACKING problem) => new("Bin Packing");
}
