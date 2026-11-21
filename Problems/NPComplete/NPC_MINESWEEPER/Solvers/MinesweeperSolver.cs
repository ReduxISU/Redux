
using API.Interfaces;

namespace API.Problems.NPComplete.NPC_MINESWEEPER.Solvers;

class MinesweeperSolver : ISolver<MINESWEEPER> {

    public string solverName { get; } = "Minesweeper Backtracking";

    public string solverDefinition { get; } =
        "Solves Minesweeper Consistency using recursive backtracking " +
        "with constraint pruning based on revealed clues.";

    public string source { get; } =
        "Kaye, Richard. 2000. Minesweeper is NP-complete. " +
        "The Mathematical Intelligencer 22(2): 9-15.";

    public string sourceFile { get; } = SourceFile.Path();

    public string[] contributors { get; } = { "Prashant Pant" };

    public bool timerHasExpired { get; set; }

    public SolverType solverType { get; } = SolverType.Backtracking;

    public SolverComplexityBucket complexityBucket { get; } =
        SolverComplexityBucket.Exponential;

    public MathematicalFunction timeComplexity { get; } =
        new("2^n * R * C",
            ", n = unknown cells, R = rows, C = columns");

    public MinesweeperSolver() { }

    public string solve(MINESWEEPER problem) {
        int rows = problem.Rows;
        int cols = problem.Columns;

        int[][] assignment = new int[rows][];
        var unknownCells = new List<(int row, int col)>();

        for (int r = 0; r < rows; r++) {
            assignment[r] = new int[cols];

            for (int c = 0; c < cols; c++) {
                if (problem.grid[r][c] == -1) {
                    assignment[r][c] = -1;
                    unknownCells.Add((r, c));
                } else {
                    assignment[r][c] = 0;
                }
            }
        }

        bool solved = Backtrack(problem, assignment, unknownCells, 0);

        if (timerHasExpired)
            return SolveRun.TimedOutAnswer;

        if (!solved)
            return "{}";

        return string.Join(";",
            assignment.Select(row => string.Join(",", row)));
    }

    private bool Backtrack(
        MINESWEEPER problem,
        int[][] assignment,
        List<(int row, int col)> unknownCells,
        int index) {

        if (timerHasExpired)
            return false;

        if (index == unknownCells.Count)
            return ConstraintsSatisfied(problem, assignment);

        var (row, col) = unknownCells[index];

        // First try a safe cell.
        assignment[row][col] = 0;

        if (ConstraintsSatisfied(problem, assignment) &&
            Backtrack(problem, assignment, unknownCells, index + 1))
            return true;

        if (timerHasExpired) {
            assignment[row][col] = -1;
            return false;
        }

        // Then try placing a mine.
        assignment[row][col] = 1;

        if (ConstraintsSatisfied(problem, assignment) &&
            Backtrack(problem, assignment, unknownCells, index + 1))
            return true;

        assignment[row][col] = -1;
        return false;
    }

    private static bool ConstraintsSatisfied(
        MINESWEEPER problem,
        int[][] assignment) {

        for (int r = 0; r < problem.Rows; r++) {
            for (int c = 0; c < problem.Columns; c++) {
                int clue = problem.grid[r][c];

                if (clue == -1)
                    continue;

                int placedMines = 0;
                int unassigned = 0;

                for (int dr = -1; dr <= 1; dr++) {
                    for (int dc = -1; dc <= 1; dc++) {
                        if (dr == 0 && dc == 0)
                            continue;

                        int nr = r + dr;
                        int nc = c + dc;

                        if (nr < 0 || nr >= problem.Rows ||
                            nc < 0 || nc >= problem.Columns)
                            continue;

                        if (assignment[nr][nc] == 1)
                            placedMines++;

                        if (assignment[nr][nc] == -1)
                            unassigned++;
                    }
                }

                // Too many mines already placed.
                if (placedMines > clue)
                    return false;

                // Even assigning all remaining cells as mines
                // would not satisfy this clue.
                if (placedMines + unassigned < clue)
                    return false;
            }
        }

        return true;
    }
}
