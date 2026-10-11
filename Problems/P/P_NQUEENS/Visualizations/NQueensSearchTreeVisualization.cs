using API.Interfaces;
using API.Interfaces.Board;
using API.Interfaces.JSON_Objects;
using API.Interfaces.SearchTree;
using API.Interfaces.Steps;
using API.Problems.P.P_NQUEENS.Solvers;

namespace API.Problems.P.P_NQUEENS.Visualizations;

/// <summary>
/// N-Queens' search tree, a second picture of the backtracking solver: each level places one row's queen, the squares it
/// skips because a queen above attacks them are cut, and a row with no safe square sends it back to move the queen above. It
/// is served only by <c>format=frames</c> (type <c>SearchTree</c>). The list format, which old frontends read, is exactly
/// <see cref="NQueensVisualization"/>'s, the board picture for the same solver. The problem's default visualization is still
/// <c>DummyVisualization</c>.
/// </summary>
class NQueensSearchTreeVisualization : IVisualization<NQUEENS>, ISearchTreeVisualization<NQUEENS, BoardCells> {
    public string visualizationName { get; } = "N-Queens Search Tree";
    public string visualizationDefinition { get; } = "The choices of N-Queens Backtracking as a tree: each level places one row's queen, a red dashed node is a square a queen above already attacks, and a solid red node is a queen taken back because no square below it was safe. With format=frames, the tree grows step by step and the path to the answer turns green.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.DynamicTable;
    public ISolver solver { get; } = new NQueensBacktracking();

    private readonly NQueensVisualization _listFormat = new();

    public API_JSON visualize(NQUEENS instance) => _listFormat.visualize(instance);

    public API_JSON SolvedVisualization(NQUEENS instance, string solution) => _listFormat.SolvedVisualization(instance, solution);

    // --- Search Tree picture (format=frames only) ---
    Type? IVisualization.StepShape => typeof(BoardCells);

    SearchTreeSpec ISearchTreeVisualization<NQUEENS, BoardCells>.Describe(NQUEENS problem) => new("N-Queens");
}
