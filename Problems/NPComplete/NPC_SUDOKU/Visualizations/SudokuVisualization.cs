using API.Interfaces;
using API.Interfaces.Board;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Tables;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_SUDOKU;
using API.Problems.NPComplete.NPC_SUDOKU.Solvers;
using API.Tools;
using System.Text.Json;

namespace API.Problems.NPComplete.NPC_SUDOKU.Visualizations;

/// <summary>
/// Sudoku as the Board picture (format=frames): the clues in bold, the digits the solver puts down, and the square it is
/// trying, step by step for the selected solver. The list format is the grid as a table (<c>DynamicTable</c>): one row per
/// grid row, one column per grid column, the clues as given and, once solved, every square the solver filled in marked
/// Solution.
/// </summary>
[LegacyVisualizationType(VisualizationType.Unimplemented)]
class SudokuVisualization : IVisualization<SUDOKU, API_TableJSON>, IBoardVisualization<SUDOKU> {
    public string visualizationName { get; } = "Sudoku Visualization";
    public string visualizationDefinition { get; } = "Draws the 9 x 9 grid as a table, rows and columns numbered from 1. The clues stand as given; once solved, every square the solver filled in is shown in the Solution color.";
    public string source { get; } = "TODO";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Eric Hill", "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.DynamicTable;
    public ISolver solver { get; } = new SudokuSolver();

    // The Board picture (format=frames only). Implemented explicitly so it is not part of /info.
    Type? IVisualization.StepShape => typeof(BoardCells);
    BoardSpec IBoardVisualization<SUDOKU>.Describe(SUDOKU problem) => Spec(problem);
    BoardCells IBoardVisualization<SUDOKU>.ParseAnswer(SUDOKU problem, string answer) => ParseGrid(answer);

    /// <summary>The instance as the Board picture describes it: a 9 x 9 board with boxes of 3, labelled from 1, and its clues.</summary>
    internal static BoardSpec Spec(SUDOKU problem) {
        int n = problem.grid.Length;
        var clues = new List<BoardClue>();
        for (int r = 0; r < n; r++)
            for (int c = 0; c < problem.grid[r].Length; c++)
                if (problem.grid[r][c] != 0) clues.Add(new BoardClue(r, c, problem.grid[r][c].ToString()));
        return new BoardSpec(BoardGame.Sudoku, n) { Box = (int)Math.Sqrt(n), LabelsFrom = 1, Clues = clues };
    }

    /// <summary>A solved grid as the solver writes it (rows of comma-separated digits, separated by semicolons) as a board;
    /// anything else (<c>{}</c>, no answer) is an empty board.</summary>
    internal static BoardCells ParseGrid(string answer) {
        var values = new List<KeyValuePair<string, string>>();
        var rows = answer.ReplaceLineEndings(string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (int r = 0; r < rows.Length; r++) {
            var cells = rows[r].Split(',', StringSplitOptions.TrimEntries);
            for (int c = 0; c < cells.Length; c++) {
                if (!int.TryParse(cells[c], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int v))
                    return new BoardCells([], []);
                if (v != 0) values.Add(new(BoardFrames.CellId(r, c), v.ToString()));
            }
        }
        return new BoardCells(values, []);
    }

    // --- Methods Including Constructors ---
    public SudokuVisualization() {
    }

    // The list format: one row per grid row ("row" holds its number), one column per grid column. A clue keeps its square;
    // a square the clues leave empty holds the answer's digit, marked Solution. With no answer
    // ({} or anything that is not a grid) only the clues are shown.
    private static API_TableJSON Table(SUDOKU problem, BoardCells answer) {
        int n = problem.grid.Length;
        var table = new API_TableJSON();
        table.columns.Add(new TableColumn { key = "row", label = "" });
        for (int c = 0; c < n; c++) table.columns.Add(new TableColumn { key = $"c{c + 1}", label = (c + 1).ToString() });
        for (int r = 0; r < n; r++) {
            var cells = new Dictionary<string, string> { ["row"] = (r + 1).ToString() };
            var colors = new Dictionary<string, string>();
            for (int c = 0; c < n; c++) {
                string key = $"c{c + 1}";
                int clue = c < problem.grid[r].Length ? problem.grid[r][c] : 0;
                if (clue != 0) {
                    cells[key] = clue.ToString();
                } else if (answer.Values.TryGetValue(BoardFrames.CellId(r, c), out var digit)) {
                    cells[key] = digit;
                    colors[key] = "Solution";
                } else {
                    cells[key] = "";
                }
            }
            table.rows.Add(new TableRow { id = $"r{r}", cells = cells, cellColors = colors.Count > 0 ? colors : null });
        }
        return table;
    }

    public API_TableJSON visualize(SUDOKU problem) => Table(problem, new BoardCells([], []));

    public API_JSON SolvedVisualization(SUDOKU problem, string solution) => Table(problem, ParseGrid(solution));
}
