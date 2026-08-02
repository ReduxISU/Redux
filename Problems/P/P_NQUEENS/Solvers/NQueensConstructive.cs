using API.Interfaces;

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
class NQueensConstructive : ISolver<NQUEENS> {

    // --- Fields ---
    public string solverName { get; } = "N-Queens Constructive Solver";
    public string solverDefinition { get; } =
        "Places all n queens directly from a closed-form (mod 12) formula in O(n) time, "
        + "witnessing that N-Queens is solvable in polynomial time. Returns a valid placement "
        + "for every n except 2 and 3, which have no solution.";
    public string source { get; } =
        "Explicit solution to the N-Queens problem (Hoffman, Loessi & Moore; see Wikipedia, \"Eight queens puzzle\").";
    public string sourceLink { get; } = "https://en.wikipedia.org/wiki/Eight_queens_puzzle#Explicit_solutions";
    public string[] contributors { get; } = { "Jason Wright" };
    public bool timerHasExpired { get; set; }

    public string complexity { get; } = "O(n)";

    // --- Solver ---
    public string solve(NQUEENS problem) {
        int n = problem.n;

        // n = 2 and n = 3 are the only sizes with no solution; n <= 0 is empty.
        if (n <= 0 || n == 2 || n == 3) return "{}";

        int[] columns = buildColumns(n); // columns[row] = col, both 0-indexed
        return formatCertificate(columns);
    }

    // Returns a 0-indexed array where board[row] = col for each row 0..n-1.
    private int[] buildColumns(int n) {
        int rem = n % 12;

        // Even columns 2,4,...  and odd columns 1,3,... (1-indexed for now).
        List<int> evens = new List<int>();
        for (int c = 2; c <= n; c += 2) evens.Add(c);

        List<int> odds = new List<int>();
        for (int c = 1; c <= n; c += 2) odds.Add(c);

        // Remainder-dependent rearrangements.
        if (rem == 3 || rem == 9) {
            moveToEnd(evens, 2); // move column 2 to the end of the even list
        }

        if (rem == 8) {
            // Swap the odd columns in adjacent pairs: 1,3,5,7,... -> 3,1,7,5,...
            for (int i = 0; i + 1 < odds.Count; i += 2) {
                (odds[i], odds[i + 1]) = (odds[i + 1], odds[i]);
            }
        } else if (rem == 2) {
            swapValues(odds, 1, 3); // switch the places of columns 1 and 3
            moveToEnd(odds, 5);     // then move column 5 to the end
        } else if (rem == 3 || rem == 9) {
            moveToEnd(odds, 1); // move columns 1 and 3 to the end (1 first, then 3)
            moveToEnd(odds, 3);
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
