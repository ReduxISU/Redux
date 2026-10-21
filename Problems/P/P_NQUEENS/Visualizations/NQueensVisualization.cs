using API.Interfaces;
using API.Interfaces.Board;
using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Tables;
using API.Interfaces.Steps;
using API.Problems.P.P_NQUEENS.Solvers;
using SPADE;

namespace API.Problems.P.P_NQUEENS.Visualizations;

/// <summary>
/// N-Queens as the Board picture (format=frames): an n x n chessboard, each queen as it lands, and the squares skipped in its
/// row linked back to the queen attacking them, step by step for the selected solver. The list format is a table with one
/// row per board row and the column of its queen. It is the problem's default visualization.
/// </summary>
class NQueensVisualization : IVisualization<NQUEENS, API_TableJSON>, IBoardVisualization<NQUEENS>, IOptInVisualization {
    public string visualizationName { get; } = "N-Queens Board Visualization";
    public string visualizationDefinition { get; } = "Draws the n x n chessboard and places the queens row by row as the selected solver does. A square the solver skips because a queen already attacks it is crossed out and linked to that queen; a row with no safe square sends the solver back to move the queen above.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.DynamicTable;
    public ISolver solver { get; } = new NQueensBacktracking();

    // The Board picture (format=frames only). Implemented explicitly so it is not part of /info.
    Type? IVisualization.StepShape => typeof(BoardCells);
    BoardSpec IBoardVisualization<NQUEENS>.Describe(NQUEENS problem) => Spec(problem);
    BoardCells IBoardVisualization<NQUEENS>.ParseAnswer(NQUEENS problem, string answer) =>
        new(Queens(problem, answer).Select((col, row) => new KeyValuePair<string, string>(BoardFrames.CellId(row, col), "Q")), []);

    /// <summary>The instance as the Board picture describes it: an n x n chessboard, labelled from 0 as the certificate
    /// counts, on which a solver puts queens.</summary>
    internal static BoardSpec Spec(NQUEENS problem) => new(BoardGame.Queens, problem.n) { LabelsFrom = 0, PieceKind = "queen" };

    public NQueensVisualization() { }

    // The column of each row's queen in an answer {(row,col),...}, in row order; empty for anything that is not one queen
    // per row on the board ({} included).
    private static int[] Queens(NQUEENS problem, string answer) {
        try {
            var cols = new int[problem.n];
            var seen = new bool[problem.n];
            int count = 0;
            foreach (UtilCollection pair in new UtilCollection(answer)) {
                pair.assertPair();
                int r = pair[0].parseInt(), c = pair[1].parseInt();
                if (r < 0 || r >= problem.n || c < 0 || c >= problem.n || seen[r]) return [];
                seen[r] = true;
                cols[r] = c;
                count++;
            }
            return count == problem.n ? cols : [];
        } catch {
            return [];
        }
    }

    // The list format: one row per board row, with the column of its queen (empty before the solve).
    private static API_TableJSON Table(NQUEENS problem, int[] queens) {
        var table = new API_TableJSON { title = $"{problem.n} queens on a {problem.n} x {problem.n} board" };
        table.columns.Add(new TableColumn { key = "row", label = "Row" });
        table.columns.Add(new TableColumn { key = "column", label = "Column" });
        for (int r = 0; r < problem.n; r++)
            table.rows.Add(new TableRow {
                id = $"r{r}",
                cells = new Dictionary<string, string> { ["row"] = r.ToString(), ["column"] = queens.Length > 0 ? queens[r].ToString() : "" },
                color = queens.Length > 0 ? "Solution" : null,
            });
        return table;
    }

    public API_TableJSON visualize(NQUEENS problem) => Table(problem, []);

    public API_JSON SolvedVisualization(NQUEENS problem, string solution) => Table(problem, Queens(problem, solution));
}
