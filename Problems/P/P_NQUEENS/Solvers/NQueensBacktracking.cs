using API.Interfaces;
using API.Interfaces.Board;
using API.Interfaces.Steps;

namespace API.Problems.P.P_NQUEENS.Solvers;

// Incremental: it places one queen per row and takes queens back, so every step's partial is the queens placed so far.
class NQueensBacktracking : ISolver<NQUEENS, BoardCells>, IIncrementalSolver {

    // --- Fields ---
    public string solverName { get; } = "N-Queens Backtracking";
    public string solverDefinition { get; } =
        "Places queens row by row using backtracking and returns a valid placement if one exists.";
    public string source { get; } = "Classic backtracking approach for the N-Queens problem.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Cole Campbell", "Luis Hernandez", "Ethan Wilks" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Exact backtracking search WITH pruning over permutation-style placements
    // -- factorial worst case (own complexity string above already says "O(N!)").
    public SolverType solverType { get; } = SolverType.Backtracking;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Factorial;

    public string complexity { get; } = "O(N!)";

    // --- Solver ---
    public string solve(NQUEENS problem) => Solve(problem, StepRecorder<BoardCells>.Off);

    // Steps (the Board picture): a Try for every queen placed (with the squares skipped in its row, each linked to the queen
    // attacking it), a Reject when a row has no safe square left, then the answer.
    public string Solve(NQUEENS problem, StepRecorder<BoardCells> rec) {
        int n = problem.n;
        int[] board = new int[n]; // board[row] = col
        var search = new Search { Rec = rec };

        if (backtrack(0, board, n, search)) {
            string answer = formatCertificate(board);
            rec.Done(Queens(board, n, []), true, n == 0
                ? "An empty board needs no queens, so the answer is {}."
                : $"{(n == 1 ? "The one queen is placed" : $"All {n} queens are placed and none attacks another")}{(n <= 16 ? ": " + answer : "")}. "
                    + $"Found after {Count(search.Placements, "placement")}.");
            return answer;
        }

        rec.Done(new BoardCells([], []), false, timerHasExpired
            ? "The solver's timer ran out before it found a placement."
            : $"Every arrangement runs into an attack. No placement exists for n = {n}, so the solver returns {{}}.");
        return "{}";
    }

    // What the search carries besides the board: the recorder and how many queens it has placed.
    private sealed class Search {
        public required StepRecorder<BoardCells> Rec { get; init; }
        public int Placements;
    }

    private bool backtrack(int row, int[] board, int n, Search search) {
        if (timerHasExpired) return false;

        if (row == n) return true;

        var rec = search.Rec;
        // The columns skipped since the last queen placed in this row, each with the row of the queen attacking it.
        var skipped = new List<(int Col, int By)>();
        bool resumed = false;
        for (int col = 0; col < n; col++) {
            int by = attacker(row, col, board);
            if (by >= 0) {
                skipped.Add((col, by));
                continue;
            }
            board[row] = col;
            search.Placements++;
            var rejected = skipped.ToArray();
            bool again = resumed;
            rec.Try(() => Queens(board, row + 1, rejected.Select(x => Conflict(row, x.Col, x.By, board))),
                () => placeCaption(row, col, rejected, again, board),
                () => [BoardFrames.CellId(row, col)]);
            skipped.Clear();
            resumed = true;
            if (backtrack(row + 1, board, n, search)) return true;
        }

        if (!timerHasExpired) {
            var rejectedAtEnd = skipped.ToArray();
            bool again = resumed;
            rec.Reject(() => Queens(board, row, rejectedAtEnd.Select(x => Conflict(row, x.Col, x.By, board))),
                () => deadCaption(row, rejectedAtEnd.Length, again),
                // The whole row failed, and so did the queen above it that has to move.
                () => [.. Enumerable.Range(0, n).Select(c => BoardFrames.CellId(row, c)), .. row > 0 ? new[] { BoardFrames.CellId(row - 1, board[row - 1]) } : []]);
        }
        return false;
    }

    // The row of the first queen above that attacks (row, col), or -1 when the square is safe.
    private static int attacker(int row, int col, int[] board) {
        for (int i = 0; i < row; i++) {
            int prevCol = board[i];

            if (prevCol == col) return i;
            if (Math.Abs(prevCol - col) == Math.Abs(i - row)) return i;
        }
        return -1;
    }

    // --- The Board picture (built only inside recorder lambdas, so solving without steps never pays for it) ---

    // The queens in rows 0 .. rows-1, and the conflicts a step found.
    private static BoardCells Queens(int[] board, int rows, IEnumerable<BoardConflict> conflicts) =>
        new(Enumerable.Range(0, rows).Select(r => new KeyValuePair<string, string>(BoardFrames.CellId(r, board[r]), "Q")), conflicts);

    private static BoardConflict Conflict(int row, int col, int by, int[] board) =>
        new(BoardFrames.CellId(row, col), BoardFrames.CellId(by, board[by]));

    private static string Count(int count, string what) => $"{BoardFrames.Count(count)} {what}{(count == 1 ? "" : "s")}";

    // "columns 0 and 1 are attacked (0 along its column by the queen at (0,0))": why the first skipped column was skipped.
    private static string reason((int Col, int By)[] rejected, int row, int[] board) {
        if (rejected.Length == 0) return "";
        var cols = rejected.Select(x => x.Col.ToString()).ToList();
        var (col, by) = rejected[0];
        string how = board[by] == col ? "along its column" : "diagonally";
        string which = cols.Count == 1 ? $"column {cols[0]} is" : $"columns {BoardFrames.AndList(cols)} are";
        return $"{which} attacked ({col} {how} by the queen at ({by},{board[by]}))";
    }

    private static string placeCaption(int row, int col, (int Col, int By)[] rejected, bool again, int[] board) {
        string why = reason(rejected, row, board);
        if (again) return $"Back in row {row}: {(why.Length > 0 ? why + ", so move" : "move")} the queen on to column {col}.";
        return why.Length > 0 ? $"Row {row}: {why}. Place the queen at column {col}." : $"Row {row}: column {col} is safe. Place the queen there.";
    }

    private static string deadCaption(int row, int skipped, bool resumed) {
        if (row == 0) return "Row 0 has no column left to try.";
        string back = $"so back up and move the queen in row {row - 1}.";
        if (skipped == 0) return $"Row {row}: its queen was already in the last column, {back}";
        return resumed ? $"Row {row}: the rest of the row is attacked too, {back}" : $"Row {row}: every column is attacked, {back}";
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
