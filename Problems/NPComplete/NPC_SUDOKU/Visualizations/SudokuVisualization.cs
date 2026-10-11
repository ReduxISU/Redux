using API.Interfaces;
using API.Interfaces.Board;
using API.Interfaces.JSON_Objects;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_SUDOKU;
using API.Problems.NPComplete.NPC_SUDOKU.Solvers;
using API.Tools;
using System.Text.Json;

namespace API.Problems.NPComplete.NPC_SUDOKU.Visualizations;

/// <summary>
/// Sudoku as the Board picture (format=frames): the clues in bold, the digits the solver puts down, and the square it is
/// trying, step by step for the selected solver. The list format is unchanged: still an empty placeholder.
/// </summary>
class SudokuVisualization : IVisualization<SUDOKU, API_QUANTUMCIRCUIT>, IBoardVisualization<SUDOKU> {
    public string visualizationName { get; } = "Sudoku Visualization";
    public string visualizationDefinition { get; } = "TODO";
    public string source { get; } = "TODO";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Eric Hill" };
    public VisualizationType visualizationType { get; } = VisualizationType.Unimplemented;
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
    public API_QUANTUMCIRCUIT visualize(SUDOKU instance) {
        var qc = new API_QUANTUMCIRCUIT();
        // var solvers = new UnstructuredGroverSolver();
        // qc.solution = solvers.solve(instance);
        // qc.circuit = instance.circuit;
        // return qc;
        return qc;
    }
}
