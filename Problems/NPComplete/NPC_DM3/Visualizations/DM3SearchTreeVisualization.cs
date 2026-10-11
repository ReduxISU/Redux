using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Interfaces.SearchTree;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_DM3.Solvers;

namespace API.Problems.NPComplete.NPC_DM3.Visualizations;

/// <summary>
/// 3-Dimensional Matching's search tree, a second picture of the backtracking solver: each level chooses one triple for the
/// element with the fewest triples left, a triple that would use an element twice is cut, and an element with no triple left
/// sends it back to take back the triple above. It is served only by <c>format=frames</c> (type <c>SearchTree</c>). The list
/// format, which old frontends read, is exactly <see cref="DM3Visualization"/>'s. The problem's default visualization is
/// unchanged.
/// </summary>
class DM3SearchTreeVisualization : IVisualization<DM3>, ISearchTreeVisualization<DM3, ChosenTriples> {
    public string visualizationName { get; } = "3-Dimensional Matching Search Tree";
    public string visualizationDefinition { get; } = "The choices of 3-Dimensional Matching Backtracking as a tree: each level chooses a triple (\"T2\") for the element with the fewest triples left, a red dashed node is a triple that would use an element twice, and a solid red node is a triple taken back because no matching of the rest went with it. With format=frames, the tree grows step by step and the path to the answer turns green.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.DynamicTable;
    public ISolver solver { get; } = new ThreeDimensionalMatchingBacktracking();

    private readonly DM3Visualization _listFormat = new();

    public API_JSON visualize(DM3 instance) => _listFormat.visualize(instance);

    public API_JSON SolvedVisualization(DM3 instance, string solution) => _listFormat.SolvedVisualization(instance, solution);

    // --- Search Tree picture (format=frames only) ---
    Type? IVisualization.StepShape => typeof(ChosenTriples);

    SearchTreeSpec ISearchTreeVisualization<DM3, ChosenTriples>.Describe(DM3 problem) => new("3-Dimensional Matching");
}
