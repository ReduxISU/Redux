using API.Interfaces;
using API.Interfaces.Board;
using API.Interfaces.Steps;

namespace API.Problems.P.P_NQUEENS.Solvers;

// Constructive (closed-form) N-Queens solver.
//
// Unlike the backtracking solver, this does not search: it places every queen
// directly from an explicit formula, so it runs in O(n) time. Its existence is
// exactly why N-Queens is a P problem — a valid placement can be written down
// for every n >= 1 (except n = 2 and n = 3, which have no solution at all)
// without exploring the search tree.
//
// The construction is the standard "mod 12" explicit solution (see the
// "Explicit solutions" section of the Eight queens puzzle Wikipedia article):
// build a column list where entry i is the column of the queen in row i, using
// the even columns followed by the odd columns with a few remainder-dependent
// rearrangements.
// Incremental: it places one queen per row, straight from the formula, and never takes one back.
class NQueensConstructive : ISolver<NQUEENS, BoardCells>, IIncrementalSolver {

    // --- Fields ---
    public string solverName { get; } = "N-Queens Constructive";
    public string solverDefinition { get; } =
        "Places all n queens directly from a closed-form (mod 12) formula in O(n) time, "
        + "witnessing that N-Queens is solvable in polynomial time. Returns a valid placement "
        + "for every n except 2 and 3, which have no solution.";
    public string source { get; } =
        "Explicit solution to the N-Queens problem (Hoffman, Loessi & Moore; see Wikipedia, \"Eight queens puzzle\").";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://en.wikipedia.org/wiki/Eight_queens_puzzle#Explicit_solutions";
    public string[] contributors { get; } = { "Jason L. Wright" };
    public bool timerHasExpired { get; set; }

    // Declared, not derived. This solver builds the placement directly from a closed-form
    // rule with no search, which is exactly the SolverType.Constructive category. The
    // runtime is linear in n, so the worst-case bucket is Polynomial.
    public SolverType solverType { get; } = SolverType.Constructive;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;

    public MathematicalFunction timeComplexity { get; } = new("n", "");

    // --- Solver ---
    public string solve(NQUEENS problem) => Solve(problem, StepRecorder<BoardCells>.Off);

    // Steps (the Board picture): the column list the formula writes down, then a Try per row placing its queen straight from
    // the list, then the answer. The rows are Try steps, not Accept: a recorder builds every Accept past its step cap, which
    // for a large n would build the board n times.
    public string Solve(NQUEENS problem, StepRecorder<BoardCells> rec) {
        int n = problem.n;
        if (timerHasExpired) return SolveRun.TimedOutAnswer; // #935: O(n), no search loop to stop

        // n = 2 and n = 3 are the only sizes with no solution; n <= 0 is empty.
        if (n <= 0 || n == 2 || n == 3) {
            if (n <= 0) rec.Done(new BoardCells([], []), true, "An empty board needs no queens, so the answer is {}.");
            else rec.Done(new BoardCells([], []), false, $"The formula has no answer for n = {n}, and none exists, so the solver returns {{}} without placing anything.");
            return "{}";
        }

        var notes = new List<string>();
        int[] columns = buildColumns(n, notes); // columns[row] = col, both 0-indexed
        rec.Try(() => new BoardCells([], []),
            () => $"n = {n}, so n mod 12 = {n % 12}. Write the even columns, then the odd ones"
                + (notes.Count > 0 ? ", and " + string.Join("; ", notes) : "")
                + $": {ListText(columns.Select(c => c + 1))}. Row i gets the i-th number, minus 1.",
            () => []);
        for (int row = 0; row < n; row++) {
            int r = row;
            rec.Try(() => Queens(columns, r + 1), () => $"Row {r}: column {columns[r]}, straight from the list. No checking needed.",
                () => [BoardFrames.CellId(r, columns[r])]);
        }

        string answer = formatCertificate(columns);
        rec.Done(Queens(columns, n), true, n == 1 ? $"The one queen is placed in 1 step, with no search: {answer}."
            : n <= ShortBoard ? $"All {n} queens placed in {n} steps, with no search: {answer}."
            : $"All {BoardFrames.Count(n)} queens placed in {BoardFrames.Count(n)} steps, with no search.");
        return answer;
    }

    // Captions spell out the column list and the answer only for a board this small; a large n would make them huge.
    private const int ShortBoard = 16;

    private static string ListText(IEnumerable<int> numbers) {
        var list = numbers.ToList();
        return list.Count <= ShortBoard ? string.Join(", ", list) : string.Join(", ", list.Take(ShortBoard)) + $", … ({BoardFrames.Count(list.Count)} in all)";
    }

    // The queens in rows 0 .. rows-1 (built only inside recorder lambdas).
    private static BoardCells Queens(int[] columns, int rows) =>
        new(Enumerable.Range(0, rows).Select(r => new KeyValuePair<string, string>(BoardFrames.CellId(r, columns[r]), "Q")), []);

    // Returns a 0-indexed array where board[row] = col for each row 0..n-1.
    // notes gets each rearrangement made, in words, for the steps' caption.
    private int[] buildColumns(int n, List<string> notes) {
        int rem = n % 12;

        // Even columns 2,4,...  and odd columns 1,3,... (1-indexed for now).
        List<int> evens = new List<int>();
        for (int c = 2; c <= n; c += 2) evens.Add(c);

        List<int> odds = new List<int>();
        for (int c = 1; c <= n; c += 2) odds.Add(c);

        // Remainder-dependent rearrangements.
        if (rem == 3 || rem == 9) {
            moveToEnd(evens, 2); // move column 2 to the end of the even list
            notes.Add("move 2 to the end of the evens");
        }

        if (rem == 8) {
            // Swap the odd columns in adjacent pairs: 1,3,5,7,... -> 3,1,7,5,...
            for (int i = 0; i + 1 < odds.Count; i += 2) {
                (odds[i], odds[i + 1]) = (odds[i + 1], odds[i]);
            }
            notes.Add("swap the odds in pairs");
        } else if (rem == 2) {
            swapValues(odds, 1, 3); // switch the places of columns 1 and 3
            moveToEnd(odds, 5);     // then move column 5 to the end
            notes.Add("swap 1 and 3, then move 5 to the end of the odds");
        } else if (rem == 3 || rem == 9) {
            moveToEnd(odds, 1); // move columns 1 and 3 to the end (1 first, then 3)
            moveToEnd(odds, 3);
            notes.Add("move 1 and 3 to the end of the odds");
        }

        // Concatenate: row i (0-indexed) gets column list[i] (converted to 0-indexed).
        List<int> ordered = new List<int>(evens);
        ordered.AddRange(odds);

        int[] board = new int[n];
        for (int row = 0; row < n; row++) {
            board[row] = ordered[row] - 1;
        }
        return board;
    }

    // Moves the first occurrence of value to the end of the list (no-op if absent).
    private void moveToEnd(List<int> list, int value) {
        if (list.Remove(value)) list.Add(value);
    }

    // Swaps the positions of two values within the list (no-op if either is absent).
    private void swapValues(List<int> list, int a, int b) {
        int ia = list.IndexOf(a);
        int ib = list.IndexOf(b);
        if (ia < 0 || ib < 0) return;
        (list[ia], list[ib]) = (list[ib], list[ia]);
    }

    // Certificate format: {(row,col),(row,col),...} for every queen.
    private string formatCertificate(int[] board) {
        List<string> pairs = new List<string>();
        for (int i = 0; i < board.Length; i++) {
            pairs.Add($"({i},{board[i]})");
        }
        return "{" + string.Join(",", pairs) + "}";
    }
}
