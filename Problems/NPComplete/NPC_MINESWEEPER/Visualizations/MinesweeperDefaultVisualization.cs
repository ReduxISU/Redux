using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Tables;
using API.Problems.NPComplete.NPC_MINESWEEPER.Solvers;

namespace API.Problems.NPComplete.NPC_MINESWEEPER.Visualizations;

class MinesweeperDefaultVisualization
    : IVisualization<MINESWEEPER, API_TableJSON> {

    public string visualizationName { get; } = "Minesweeper Board Visualization";
    public string visualizationDefinition { get; } =
        "Displays a Minesweeper grid with covered gray squares and revealed " +
        "clues. A verified solution reveals mines and the remaining safe cells; " +
        "blank revealed cells have no neighboring mines.";
    public string source { get; } =
        "Kaye, Richard. 2000. Minesweeper is NP-complete. " +
        "The Mathematical Intelligencer 22(2): 9-15.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Prashant Pant" };
    public VisualizationType visualizationType { get; } = VisualizationType.DynamicTable;
    public ISolver solver { get; } = new MinesweeperSolver();

    public MinesweeperDefaultVisualization() { }

    public API_TableJSON visualize(MINESWEEPER problem) => BuildBoard(problem, null);

    public API_JSON SolvedVisualization(MINESWEEPER problem, string solution) {
        // Only reveal a placement after checking it against the original clues.
        if (!new Verifiers.MinesweeperVerifier().verify(problem, solution))
            return visualize(problem);

        var mines = solution.ReplaceLineEndings(string.Empty).Split(';')
            .Select(row => row.Split(',').Select(cell => cell.Trim() == "1").ToArray())
            .ToArray();
        return BuildBoard(problem, mines);
    }

    private static API_TableJSON BuildBoard(MINESWEEPER problem, bool[][]? mines) {
        var board = new API_TableJSON {
            title = mines == null
                ? "Minesweeper — covered board (■ = covered)"
                : "Minesweeper — one consistent placement (✹ = mine)"
        };
        for (int c = 0; c < problem.Columns; c++)
            board.columns.Add(new TableColumn { key = $"c{c}", label = "" });

        for (int r = 0; r < problem.Rows; r++) {
            var row = new TableRow {
                id = $"r{r}",
                cellColors = new Dictionary<string, string>()
            };
            for (int c = 0; c < problem.Columns; c++) {
                string key = $"c{c}";
                bool covered = mines == null && problem.grid[r][c] == -1;
                bool mine = mines != null && mines[r][c];
                int clue = problem.grid[r][c];
                if (mines != null && clue == -1 && !mine)
                    clue = CountAdjacentMines(mines, r, c);

                row.cells[key] = covered ? "■" : mine ? "✹" : clue == 0 ? "" : clue.ToString();
                // The GUI resolves palette keys, not CSS color literals.
                row.cellColors[key] = covered ? "ElementHighlight" : mine ? "Solution" : "Background";
            }
            board.rows.Add(row);
        }
        return board;
    }

    private static int CountAdjacentMines(bool[][] mines, int row, int column) {
        int count = 0;
        for (int r = Math.Max(0, row - 1); r <= Math.Min(mines.Length - 1, row + 1); r++) {
            for (int c = Math.Max(0, column - 1); c <= Math.Min(mines[r].Length - 1, column + 1); c++) {
                if ((r != row || c != column) && mines[r][c])
                    count++;
            }
        }
        return count;
    }
}
