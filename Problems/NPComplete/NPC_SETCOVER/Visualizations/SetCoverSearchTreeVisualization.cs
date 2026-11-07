using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Interfaces.SearchTree;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_SETCOVER.Solvers;

namespace API.Problems.NPComplete.NPC_SETCOVER.Visualizations;

/// <summary>
/// Set Cover's search tree, a second picture of the Algorithm X bounded search: each level takes one set for the uncovered
/// element in the fewest sets, a set that takes the cover over K is cut, and a set after which the rest could not be covered
/// within K is taken back. It is served only by <c>format=frames</c> (type <c>SearchTree</c>). The list format, which old
/// frontends read, is exactly <see cref="SetCoverDefaultVisualization"/>'s. The problem's default visualization is
/// unchanged.
/// </summary>
class SetCoverSearchTreeVisualization : IVisualization<SETCOVER>, ISearchTreeVisualization<SETCOVER, ChosenSets>, IOptInVisualization {
    public string visualizationName { get; } = "Set Cover Search Tree";
    public string visualizationDefinition { get; } = "The choices of Set Cover Algorithm X Bounded Search as a tree: each level takes a set (\"S2\") for the uncovered element in the fewest sets, a red dashed node is a set that takes the cover over K (or an element in no set), and a solid red node is a set taken back because the rest could not be covered within K. With format=frames, the tree grows step by step and the path to the answer turns green.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.SetD3;
    public ISolver solver { get; } = new HeuristicSolver();

    private readonly SetCoverDefaultVisualization _listFormat = new();

    public API_JSON visualize(SETCOVER instance) => _listFormat.visualize(instance);

    public API_JSON SolvedVisualization(SETCOVER instance, string solution) => _listFormat.SolvedVisualization(instance, solution);

    // --- Search Tree picture (format=frames only) ---
    Type? IVisualization.StepShape => typeof(ChosenSets);

    SearchTreeSpec ISearchTreeVisualization<SETCOVER, ChosenSets>.Describe(SETCOVER problem) => new("Set Cover");
}
