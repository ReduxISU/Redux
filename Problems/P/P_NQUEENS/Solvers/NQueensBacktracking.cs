using API.Interfaces;
using API.Interfaces.Board;
using API.Interfaces.Steps;

namespace API.Problems.P.P_NQUEENS.Solvers;

// The steps N-Queens Backtracking records, for the Board picture and the Search Tree picture alike. Row by row:
//   - a Try for each queen placed, with the squares skipped in its row since the last queen, each linked to the queen
//     attacking it (in the tree, the skipped squares are pruned nodes "(row,col)" whose note names the attacker, and the
//     queen is an explored node under the queen of the row above, the solution in the last row);
//   - a Reject when a row has no safe square left: the rest of the row's skipped squares, and the queen above that has to
//     move (that node fails; the root "start" when row 0 runs out);
//   - the Done, which names the answer's path in the tree again, root first.
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
    // -- factorial worst case (own complexity string above already says "O(N!)"). Also
    // DepthFirstSearch: backtrack() places row by row, recursing before trying the next column.
    public SolverType solverType { get; } = SolverType.Backtracking | SolverType.DepthFirstSearch;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Factorial;

    public MathematicalFunction timeComplexity { get; } = new("N!", "");

    // A step about row Row: the queen placed at column Col (Placed), or the row given up (not Placed). Skipped is the
    // squares skipped in the row since its last queen, each with the row of the queen attacking it and its search-tree node.
    private readonly record struct Move(int Row, int Col, bool Placed, IReadOnlyList<(int Col, int By, int Node)> Skipped);

    // How the steps show a move. The search keeps its own tree (a "start" root, pruned squares with notes), so it gives
    // RunSteps the tree nodes of each step rather than using SearchSteps.
    private static RunSteps<Move, BoardCells> NewSteps(StepRecorder<BoardCells> rec, int[] board, int n, Search search) => new(rec,
        // What every picture shows: the queens placed (the new one too), each skipped square linked to its attacker, and
        // the new queen's square, or for a row given up the whole row and the queen above that has to move.
        partial: m => Queens(board, m.Placed ? m.Row + 1 : m.Row, m.Skipped.Select(x => Conflict(m.Row, x.Col, x.By, board))),
        focus: m => m.Placed ? [BoardFrames.CellId(m.Row, m.Col)]
            : [.. Enumerable.Range(0, n).Select(c => BoardFrames.CellId(m.Row, c)), .. m.Row > 0 ? new[] { BoardFrames.CellId(m.Row - 1, board[m.Row - 1]) } : []],
        // The Search Tree picture's nodes only: the skipped squares, pruned, then the queen placed (after the root, on the
        // first step), or the queen above, failed.
        tree: m => m.Placed
            ? [.. search.Placements == 1 ? [Search.Root] : Array.Empty<SearchNode>(), .. m.Skipped.Select(x => search.Cut(board, m.Row, x)),
                search.Queen(board, m.Row, m.Row == n - 1 ? SearchNodeState.Solution : SearchNodeState.Explored)]
            : [.. m.Skipped.Select(x => search.Cut(board, m.Row, x)), search.Queen(board, m.Row - 1, SearchNodeState.Failed)]);

    // --- Solver ---
    public string solve(NQUEENS problem) => Solve(problem, StepRecorder<BoardCells>.Off);

    public string Solve(NQUEENS problem, StepRecorder<BoardCells> rec) {
        int n = problem.n;
        int[] board = new int[n]; // board[row] = col
        var search = new Search { RowIds = new int[n] };
        var steps = NewSteps(rec, board, n, search);

        if (backtrack(0, board, n, search, steps)) {
            string answer = formatCertificate(board);
            var queens = Queens(board, n, []);
            // The answer is spelled out only for a board this small; a large n would make the text huge.
            if (n == 0)
                steps.Done(queens, true, $"An empty board needs no queens, so the answer is {{}}.");
            else if (n == 1)
                steps.Done(queens, true, $"The one queen is placed: {answer}. Found after {BoardFrames.Count(search.Placements)} {(search.Placements == 1 ? "placement" : "placements")}.",
                    tree: () => search.AnswerPath(board, n));
            else if (n <= 16)
                steps.Done(queens, true, $"All {n} queens are placed and none attacks another: {answer}. Found after {BoardFrames.Count(search.Placements)} {(search.Placements == 1 ? "placement" : "placements")}.",
                    tree: () => search.AnswerPath(board, n));
            else
                steps.Done(queens, true, $"All {n} queens are placed and none attacks another. Found after {BoardFrames.Count(search.Placements)} {(search.Placements == 1 ? "placement" : "placements")}.",
                    tree: () => search.AnswerPath(board, n));
            return answer;
        }

        if (timerHasExpired) {
            // backtrack() returned false because it was stopped. A timed run is stopped by SolveRun.Record, which puts its
            // stopped Done in place of this one.
            steps.Done(new BoardCells([], []), false, $"The solver's timer ran out before it found a placement.");
            return SolveRun.TimedOutAnswer;
        }
        steps.Done(new BoardCells([], []), false, $"Every arrangement runs into an attack. No placement exists for n = {n}, so the solver returns {{}}.");
        return "{}";
    }

    // What the search carries besides the board: how many queens it has placed, and the search tree's bookkeeping: the node
    // id of each row's current queen and the next id (plain ints, so solving without steps allocates nothing for the tree;
    // the nodes themselves are built only for a step that is kept).
    private sealed class Search {
        public int Placements;
        public required int[] RowIds { get; init; }
        public int NextNode = 1;

        public static readonly SearchNode Root = new(0, null, "start", SearchNodeState.Explored);

        // The node of the queen in row r (the root for r = -1), in the given state.
        public SearchNode Queen(int[] board, int r, SearchNodeState state) =>
            r < 0 ? Root with { State = state } : new(RowIds[r], r == 0 ? 0 : RowIds[r - 1], $"({r},{board[r]})", state);

        // The node of a square skipped in row r, under the queen of the row above: pruned, its note the queen attacking it.
        public SearchNode Cut(int[] board, int r, (int Col, int By, int Node) x) =>
            new(x.Node, r == 0 ? 0 : RowIds[r - 1], $"({r},{x.Col})", SearchNodeState.Pruned) { Bound = $"hit by ({x.By},{board[x.By]})" };

        // The answer's path, root first, the last row's queen the solution.
        public SearchNode[] AnswerPath(int[] board, int n) =>
            [Root, .. Enumerable.Range(0, n).Select(r => Queen(board, r, r == n - 1 ? SearchNodeState.Solution : SearchNodeState.Explored))];
    }

    // Places queens in rows row .. n-1 under the queens already placed; false when the rest can't be placed. It goes along
    // the row from the left, skips a column a queen above attacks, places the queen on the first safe one and recurses on
    // the next row. When the rows below fail, it moves the queen on to the next safe column; when the row runs out, it gives
    // up, and the row above moves its queen.
    private bool backtrack(int row, int[] board, int n, Search search, RunSteps<Move, BoardCells> steps) {
        if (timerHasExpired) return false;

        if (row == n) return true;

        // The columns skipped since the last queen placed in this row, each with the row of the queen attacking it and its
        // node of the search tree.
        var skipped = new List<(int Col, int By, int Node)>();
        bool resumed = false;
        for (int col = 0; col < n; col++) {
            int by = attacker(row, col, board);
            if (by >= 0) {
                skipped.Add((col, by, search.NextNode++));
                continue;
            }
            board[row] = col;
            search.Placements++;
            search.RowIds[row] = search.NextNode++;
            var placed = new Move(row, col, true, skipped);
            if (skipped.Count == 0) {
                if (resumed)
                    steps.Try(placed, $"Back in row {row}: move the queen on to column {col}.");
                else
                    steps.Try(placed, $"Row {row}: column {col} is safe. Place the queen there.");
            } else {
                // The first column skipped, and the row of the queen attacking it: the why says how it attacks.
                var (first, from, _) = skipped[0];
                if (skipped.Count == 1 && resumed)
                    steps.Try(placed, $"Back in row {row}: column {first} is attacked ({first} {(board[from] == first ? "along its column" : "diagonally")} by the queen at ({from},{board[from]})), so move the queen on to column {col}.");
                else if (skipped.Count == 1)
                    steps.Try(placed, $"Row {row}: column {first} is attacked ({first} {(board[from] == first ? "along its column" : "diagonally")} by the queen at ({from},{board[from]})). Place the queen at column {col}.");
                else if (resumed)
                    steps.Try(placed, $"Back in row {row}: columns {BoardFrames.AndList(skipped.Select(x => x.Col.ToString()).ToList())} are attacked ({first} {(board[from] == first ? "along its column" : "diagonally")} by the queen at ({from},{board[from]})), so move the queen on to column {col}.");
                else
                    steps.Try(placed, $"Row {row}: columns {BoardFrames.AndList(skipped.Select(x => x.Col.ToString()).ToList())} are attacked ({first} {(board[from] == first ? "along its column" : "diagonally")} by the queen at ({from},{board[from]})). Place the queen at column {col}.");
            }
            skipped.Clear();
            resumed = true;
            if (backtrack(row + 1, board, n, search, steps)) return true;
        }

        if (!timerHasExpired) {
            // The whole row failed, and so did the queen above it that has to move.
            var dead = new Move(row, -1, false, skipped);
            if (row == 0)
                steps.Reject(dead, $"Row 0 has no column left to try.");
            else if (skipped.Count == 0)
                steps.Reject(dead, $"Row {row}: its queen was already in the last column, so back up and move the queen in row {row - 1}.");
            else if (resumed)
                steps.Reject(dead, $"Row {row}: the rest of the row is attacked too, so back up and move the queen in row {row - 1}.");
            else
                steps.Reject(dead, $"Row {row}: every column is attacked, so back up and move the queen in row {row - 1}.");
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

    // --- The Board picture (built only for a step that is kept, so solving without steps never pays for it) ---

    // The queens in rows 0 .. rows-1, and the conflicts a step found.
    private static BoardCells Queens(int[] board, int rows, IEnumerable<BoardConflict> conflicts) =>
        new(Enumerable.Range(0, rows).Select(r => new KeyValuePair<string, string>(BoardFrames.CellId(r, board[r]), "Q")), conflicts);

    private static BoardConflict Conflict(int row, int col, int by, int[] board) =>
        new(BoardFrames.CellId(row, col), BoardFrames.CellId(by, board[by]));

    // Certificate format: {(row,col),(row,col),...} for every queen
    private string formatCertificate(int[] board) {
        List<string> pairs = new List<string>();

        for (int i = 0; i < board.Length; i++) {
            pairs.Add($"({i},{board[i]})");
        }

        return "{" + string.Join(",", pairs) + "}";
    }
}
