using System;
using System.Collections.Generic;
using System.Linq;
using API.Interfaces;
using API.Interfaces.Steps;
using API.Interfaces.Table;
using API.Problems.NPComplete.NPC_EDITDISTANCE;
using SPADE;


namespace API.Problems.NPComplete.NPC_EDITDISTANCE.Solvers;

// Incremental: the table is filled cell by cell (nothing of the answer yet), then the trace-back takes the cells of one
// cheapest edit path one at a time. Cell ids are the Table picture's (TableFrames.CellId): row i is the first i letters of
// x, column j the first j letters of y.
class EditDistanceDPSolver : ISolver<EDITDISTANCE, TableCells>, IIncrementalSolver {
    public string solverName { get; } = "Edit Distance Dynamic Programming";
    public string solverDefinition { get; } = "Finds the edit distance between two strings using dynamic programming.";
    public string source { get; } = "R. A. Wagner and M. J. Fischer, “The String-to-String Correction Problem,” Journal of the ACM, vol. 21, no. 1, pp. 168-173, Jan. 1974";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://dl.acm.org/doi/10.1145/321796.321811";
    public string[] contributors { get; } = { "Kaosi Ibeabuchi", "Diya Pandey", "Srijan Pant" };

    public bool timerHasExpired { get; set; }
    // Declared, not derived. Memo table + optimal-substructure recurrence.
    public SolverType solverType { get; } = SolverType.DynamicProgramming;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
    // Fills the full (m+1) x (n+1) DP table with one O(1) transition per cell.
    public string complexity { get; } = "O(m * n)";

    public EditDistanceDPSolver() { }

    public string solve(EDITDISTANCE problem) => Solve(problem, StepRecorder<TableCells>.Off);

    /// <summary>
    /// Whether the steps fill one cell at a time: when every cell, the trace-back and the first and last steps fit in
    /// the recorder's cap. A bigger table is filled one row per step, so the whole fill stays visible instead of
    /// stopping partway through.
    /// </summary>
    internal static bool FillsCellByCell(int m, int n) => (long)m * n + m + n + 3 <= StepRecorder<TableCells>.DefaultCap;

    // Steps: a Try that fills row 0 and column 0; a Try per cell (focus: the cell; reads: the cells it compared, the one it
    // took first), or per row for a big table (focus: the row's cells); then an Accept per cell of the trace-back (focus:
    // the cell reached), and Done with the whole table and path. The table is built only for the steps: solve() fills it
    // exactly as before.
    public string Solve(EDITDISTANCE problem, StepRecorder<TableCells> rec) {
        string s1 = problem.sourceString;
        string s2 = problem.targetString;

        int m = s1.Length;
        int n = s2.Length;

        int[,] dp = new int[m + 1, n + 1];
        bool recording = rec != StepRecorder<TableCells>.Off;
        bool byCell = FillsCellByCell(m, n);
        // The cells filled so far, and the trace-back so far, read only inside recorder lambdas.
        int filledRows = 0, filledInRow = 0;
        var path = new List<(int I, int J)>();

        IEnumerable<KeyValuePair<string, string>> Filled() {
            for (int i = 0; i <= m; i++)
                for (int j = 0; j <= n; j++)
                    if (i == 0 || j == 0 || i < filledRows || (i == filledRows && j <= filledInRow))
                        yield return new(TableFrames.CellId(i, j), Num(dp[i, j]));
        }
        TableCells Table(params (int I, int J)[] reads) =>
            new(Filled(), reads.Select(r => TableFrames.CellId(r.I, r.J)), path.Select(p => TableFrames.CellId(p.I, p.J)));

        for (int i = 0; i <= m; i++) dp[i, 0] = i;
        for (int j = 0; j <= n; j++) dp[0, j] = j;
        rec.Try(() => Table(), () => m == 0 && n == 0
            ? "Row 0 and column 0 hold the cost of turning a prefix into the empty string. Both strings are empty, so that is 0."
            : "Fill row 0 and column 0 first: turning a prefix into the empty string costs one deletion or insertion per letter.");

        for (int i = 1; i <= m; i++) {
            for (int j = 1; j <= n; j++) {
                int at = i, to = j;
                if (s1[i - 1] == s2[j - 1]) {
                    dp[i, j] = dp[i - 1, j - 1];
                    if (byCell) {
                        filledRows = i; filledInRow = j;
                        rec.Try(() => Table((at - 1, to - 1)),
                            () => $"Cell ({at},{to}): '{s1[at - 1]}' = '{s2[to - 1]}', so copy the diagonal: {dp[at, to]}.",
                            () => [TableFrames.CellId(at, to)]);
                    }
                } else {
                    dp[i, j] = 1 + Math.Min(dp[i - 1, j - 1], // substitute
                                 Math.Min(dp[i - 1, j],        // delete
                                 dp[i, j - 1]));            // insert
                    if (byCell) {
                        filledRows = i; filledInRow = j;
                        rec.Try(() => Table(Reads(dp, at, to)), () => FillCaption(dp, s1, s2, at, to), () => [TableFrames.CellId(at, to)]);
                    }
                }
            }
            if (!byCell && n > 0) {
                filledRows = i; filledInRow = n;
                int row = i;
                rec.Try(() => Table(),
                    () => $"Row {row} ('{s1[row - 1]}'): each cell copies the diagonal when the letters match, else adds 1 to the cheapest of replace, delete and insert. The row ends at {dp[row, n]}.",
                    () => Enumerable.Range(1, n).Select(j => TableFrames.CellId(row, j)).ToArray());
            }
        }
        filledRows = m; filledInRow = n;

        if (recording) {
            // One cheapest edit path, traced back from the corner: a matching letter first, then replace, delete, insert.
            int i = m, j = n;
            path.Add((i, j));
            rec.Accept(() => Table(), () => $"Trace back from the corner ({m},{n}), where the edit distance is {dp[m, n]}.",
                () => [TableFrames.CellId(m, n)]);
            int kept = 0, replaced = 0, deleted = 0, inserted = 0;
            while (i > 0 || j > 0) {
                int fi = i, fj = j;
                string move;
                if (i > 0 && j > 0 && s1[i - 1] == s2[j - 1] && dp[i, j] == dp[i - 1, j - 1]) {
                    move = $"'{s1[i - 1]}' matches, so it is kept";
                    kept++; i--; j--;
                } else if (i > 0 && j > 0 && dp[i, j] == dp[i - 1, j - 1] + 1) {
                    move = $"replace '{s1[i - 1]}' with '{s2[j - 1]}'";
                    replaced++; i--; j--;
                } else if (i > 0 && dp[i, j] == dp[i - 1, j] + 1) {
                    move = $"delete '{s1[i - 1]}'";
                    deleted++; i--;
                } else {
                    move = $"insert '{s2[j - 1]}'";
                    inserted++; j--;
                }
                path.Add((i, j));
                int pi = i, pj = j;
                rec.Accept(() => Table(), () => $"({fi},{fj}) came from ({pi},{pj}): {move}.", () => [TableFrames.CellId(pi, pj)]);
            }
            var edits = new List<string>();
            if (replaced > 0) edits.Add(Count(replaced, "replacement"));
            if (deleted > 0) edits.Add(Count(deleted, "deletion"));
            if (inserted > 0) edits.Add(Count(inserted, "insertion"));
            rec.Done(Table(), true, edits.Count == 0
                ? $"Edit distance {dp[m, n]}: the strings are already the same."
                : $"Edit distance {dp[m, n]}: one cheapest way is {string.Join(", ", edits)}.");
        }

        return dp[m, n].ToString();
    }

    // The cells a mismatched fill compares, the one it takes first: replace (diagonal), delete (above), insert (left), the
    // first of them that is cheapest winning, as Math.Min does.
    private static (int, int)[] Reads(int[,] dp, int i, int j) {
        var options = new (int I, int J)[] { (i - 1, j - 1), (i - 1, j), (i, j - 1) };
        int best = Enumerable.Range(0, 3).OrderBy(k => dp[options[k].I, options[k].J]).First();
        return [options[best], .. options.Where((_, k) => k != best)];
    }

    private static string FillCaption(int[,] dp, string s1, string s2, int i, int j) {
        char a = s1[i - 1], b = s2[j - 1];
        int replace = dp[i - 1, j - 1], delete = dp[i - 1, j], insert = dp[i, j - 1];
        int best = Math.Min(replace, Math.Min(delete, insert));
        string cheapest = replace == best ? "replace" : delete == best ? "delete" : "insert";
        return $"Cell ({i},{j}): '{a}' ≠ '{b}'. Replace '{a}' with '{b}': 1 + {replace}. Delete '{a}': 1 + {delete}. "
            + $"Insert '{b}': 1 + {insert}. The cheapest is {cheapest}, so {dp[i, j]}.";
    }

    private static string Count(int n, string what) => n == 1 ? $"1 {what}" : $"{n} {what}s";

    private static string Num(int n) => n.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
