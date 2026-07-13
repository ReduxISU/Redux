using API.Interfaces;

namespace API.Problems.NPComplete.NPC_NQUEENS.Solvers;

class NQueensBacktracking : ISolver<NQUEENS> {

    // --- Fields ---
    public string solverName { get; } = "N-Queens Backtracking Solver";
    public string solverDefinition { get; } =
        "Places queens row by row using backtracking and returns a valid placement if one exists.";
    public string source { get; } = "Classic backtracking approach for the N-Queens problem.";
    public string[] contributors { get; } = { "Cole Campbell", "Luis Hernandez", "Ethan Wilks" };
    public bool timerHasExpired { get; set; }

    public string complexity { get; } = "O(N!)";

    // --- Solver ---
    public string solve(NQUEENS problem) {
        int n = problem.n;
        int[] board = new int[n]; // board[row] = col

        if (backtrack(0, board, n)) {
            return formatCertificate(board);
        }

        return "{}";
    }

    private bool backtrack(int row, int[] board, int n) {
        if (timerHasExpired) return false;

        if (row == n) return true;

        for (int col = 0; col < n; col++) {
            if (isSafe(row, col, board)) {
                board[row] = col;
                if (backtrack(row + 1, board, n)) return true;
            }
        }

        return false;
    }

    private bool isSafe(int row, int col, int[] board) {
        for (int i = 0; i < row; i++) {
            int prevCol = board[i];

            if (prevCol == col) return false;
            if (Math.Abs(prevCol - col) == Math.Abs(i - row)) return false;
        }
        return true;
    }

    // Certificate format: {(row,col),(row,col),...} for every queen
    private string formatCertificate(int[] board) {
        List<string> pairs = new List<string>();

        for (int i = 0; i < board.Length; i++) {
            pairs.Add($"({i},{board[i]})");
        }

        return "{" + string.Join(",", pairs) + "}";
    }
}
