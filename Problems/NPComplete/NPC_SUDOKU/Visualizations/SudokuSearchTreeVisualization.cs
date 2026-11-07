using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Interfaces.SearchTree;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_SUDOKU.Solvers;

namespace API.Problems.NPComplete.NPC_SUDOKU.Visualizations;

/// <summary>
/// Sudoku's search tree, a second picture of the backtracking solver: each level puts a digit in the next empty square, in
/// reading order, a square with no digit left is cut, and a digit after which the squares could not all be filled is taken
/// back. It is served only by <c>format=frames</c> (type <c>SearchTree</c>). The list format, which old frontends read, is
/// exactly <see cref="SudokuVisualization"/>'s. The problem's default visualization is unchanged.
/// </summary>
class SudokuSearchTreeVisualization : IVisualization<SUDOKU>, ISearchTreeVisualization<SUDOKU, BoardCells>, IOptInVisualization {
    public string visualizationName { get; } = "Sudoku Search Tree";
    public string visualizationDefinition { get; } = "The choices of Sudoku Backtracking as a tree: each level puts a digit in the next empty square (\"R1C3=4\"), a red dashed node is a square with no digit left, and a solid red node is a digit taken back because the squares after it could not all be filled. With format=frames, the tree grows step by step and the path to the answer turns green.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.DynamicTable;
    public ISolver solver { get; } = new SudokuSolver();

    private readonly SudokuVisualization _listFormat = new();

    public API_JSON visualize(SUDOKU instance) => _listFormat.visualize(instance);

    public API_JSON SolvedVisualization(SUDOKU instance, string solution) => _listFormat.SolvedVisualization(instance, solution);

    // --- Search Tree picture (format=frames only) ---
    Type? IVisualization.StepShape => typeof(BoardCells);

    SearchTreeSpec ISearchTreeVisualization<SUDOKU, BoardCells>.Describe(SUDOKU problem) => new("Sudoku");
}
