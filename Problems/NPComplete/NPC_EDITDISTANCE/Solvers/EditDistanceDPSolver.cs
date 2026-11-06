using System;
using System.Collections.Generic;
using System.Linq;
using API.Interfaces;
using API.Interfaces.Steps;
using API.Interfaces.Table;
using API.Problems.NPComplete.NPC_EDITDISTANCE;
using SPADE;
using static API.Interfaces.Table.TableFrames;


namespace API.Problems.NPComplete.NPC_EDITDISTANCE.Solvers;

// The steps Edit Distance Dynamic Programming records, for the Table picture (row i is the first i letters of x, column j
// the first j letters of y; cell ids from TableFrames.CellId):
//   - an Update that fills row 0 and column 0: turning a prefix into the empty string costs one edit per letter;
//   - an Update per cell, in reading order, saying which case of the recurrence it took and why. A table too big for the
//     step cap gets one Update per row instead, so the whole fill stays visible;
//   - the trace-back: an Accept for the corner, then an Accept for each cell one cheapest edit path steps back to, saying
//     which edit that step is;
//   - the Done, with the edit distance and the edits.
// The fill has no verdicts (every cell is simply worked out), so it is Updates; the trace-back keeps cells for the answer,
// so it is Accepts. Incremental: a filled cell never changes, and the trace-back only adds cells to the path.
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
    public MathematicalFunction timeComplexity { get; } = new("m * n", "");

    public EditDistanceDPSolver() { }

    // How the steps show the table: it is filled, in reading order, up to the cell (Row, Col). Compared means the step is
    // about that one cell, so the picture marks it and the cells its recurrence compared.
    private static RunSteps<(int Row, int Col, bool Compared), TableCells> NewSteps(StepRecorder<TableCells> rec,
        string x, string y, int[,] dp, List<(int I, int J)> path) => new(rec,
        // What every picture shows: the cells filled so far, the cells a fill compared (the one it took first), and the
        // trace-back's path so far.
        partial: s => new TableCells(Filled(dp, s.Row, s.Col), s.Compared ? Compared(x, y, dp, s.Row, s.Col) : [],
            path.Select(p => CellId(p.I, p.J))),
        // The cell just filled; in the trace-back, the cell the path has just reached.
        focus: s => s.Compared ? [CellId(s.Row, s.Col)] : path.Count > 0 ? [CellId(path[^1].I, path[^1].J)] : []);

    public string solve(EDITDISTANCE problem) => Solve(problem, StepRecorder<TableCells>.Off);

    /// <summary>
    /// Whether the steps fill one cell at a time: when every cell, the trace-back and the first and last steps fit in
    /// the recorder's cap. A bigger table is filled one row per step, so the whole fill stays visible instead of
    /// stopping partway through.
    /// </summary>
    internal static bool FillsCellByCell(int m, int n) => (long)m * n + m + n + 3 <= StepRecorder<TableCells>.DefaultCap;

    // dp[i, j] is the edit distance from the first i letters of x to the first j letters of y. Row 0 and column 0 are the
    // edits to or from the empty string; every other cell comes from the three cells above and to its left. The answer is
    // the corner dp[m, n]. The trace-back, for the steps only, walks from the corner back to (0,0) along one cheapest path.
    public string Solve(EDITDISTANCE problem, StepRecorder<TableCells> rec) {
        string x = problem.sourceString;
        string y = problem.targetString;

        int m = x.Length;
        int n = y.Length;

        int[,] dp = new int[m + 1, n + 1];
        // One cheapest edit path, from the corner back towards (0,0); filled only by the trace-back.
        var path = new List<(int I, int J)>();
        var steps = NewSteps(rec, x, y, dp, path);
        bool byCell = FillsCellByCell(m, n);

        for (int i = 0; i <= m; i++) dp[i, 0] = i;
        for (int j = 0; j <= n; j++) dp[0, j] = j;
        if (m == 0 && n == 0)
            steps.Update((0, 0, false), $"Row 0 and column 0 hold the cost of turning a prefix into the empty string. Both strings are empty, so that is 0.");
        else
            steps.Update((0, 0, false), $"Fill row 0 and column 0 first: turning a prefix into the empty string costs one deletion or insertion per letter.");

        for (int i = 1; i <= m; i++) {
            if (timerHasExpired) return SolveRun.TimedOutAnswer;
            for (int j = 1; j <= n; j++) {
                if (x[i - 1] == y[j - 1]) {
                    // The letters match: no edit, so the cost is the diagonal's.
                    dp[i, j] = dp[i - 1, j - 1];
                    if (byCell)
                        steps.Update((i, j, true), $"Cell ({i},{j}): '{x[i - 1]}' = '{y[j - 1]}', so copy the diagonal: {dp[i, j]}.");
                } else {
                    // They differ: one edit more than the cheapest neighbour. Replace comes from the diagonal, delete from
                    // the cell above, insert from the cell to the left.
                    int replace = dp[i - 1, j - 1], delete = dp[i - 1, j], insert = dp[i, j - 1];
                    int cheapest = Math.Min(replace, Math.Min(delete, insert));
                    dp[i, j] = 1 + cheapest;
                    // The why names the cheapest edit (replace first, then delete, then insert on a tie): one step line
                    // per edit, so each reads as a plain sentence. Picking the line is skipped when nothing is recorded.
                    if (byCell && rec.Enabled) {
                        if (replace == cheapest)
                            steps.Update((i, j, true), $"Cell ({i},{j}): '{x[i - 1]}' ≠ '{y[j - 1]}'. Replace '{x[i - 1]}' with '{y[j - 1]}': 1 + {replace}. Delete '{x[i - 1]}': 1 + {delete}. Insert '{y[j - 1]}': 1 + {insert}. The cheapest is replace, so {dp[i, j]}.");
                        else if (delete == cheapest)
                            steps.Update((i, j, true), $"Cell ({i},{j}): '{x[i - 1]}' ≠ '{y[j - 1]}'. Replace '{x[i - 1]}' with '{y[j - 1]}': 1 + {replace}. Delete '{x[i - 1]}': 1 + {delete}. Insert '{y[j - 1]}': 1 + {insert}. The cheapest is delete, so {dp[i, j]}.");
                        else
                            steps.Update((i, j, true), $"Cell ({i},{j}): '{x[i - 1]}' ≠ '{y[j - 1]}'. Replace '{x[i - 1]}' with '{y[j - 1]}': 1 + {replace}. Delete '{x[i - 1]}': 1 + {delete}. Insert '{y[j - 1]}': 1 + {insert}. The cheapest is insert, so {dp[i, j]}.");
                    }
                }
            }
            if (!byCell && n > 0) {
                int row = i;
                steps.Update((i, n, false), $"Row {i} ('{x[i - 1]}'): each cell copies the diagonal when the letters match, else adds 1 to the cheapest of replace, delete and insert. The row ends at {dp[i, n]}.",
                    focus: () => Enumerable.Range(1, n).Select(j => CellId(row, j)).ToArray());
            }
        }

        // The trace-back is only for the steps: a plain solve skips it.
        if (rec != StepRecorder<TableCells>.Off) {
            // One cheapest edit path, traced back from the corner: a matching letter first, then replace, delete, insert.
            // Each step moves to a cell the current one could have come from at its cost.
            int i = m, j = n;
            path.Add((i, j));
            steps.Accept((m, n, false), $"Trace back from the corner ({m},{n}), where the edit distance is {dp[m, n]}.");
            int replaced = 0, deleted = 0, inserted = 0;
            while (i > 0 || j > 0) {
                int fromI = i, fromJ = j;
                if (i > 0 && j > 0 && x[i - 1] == y[j - 1] && dp[i, j] == dp[i - 1, j - 1]) {
                    i--; j--;
                    path.Add((i, j));
                    steps.Accept((m, n, false), $"({fromI},{fromJ}) came from ({i},{j}): '{x[fromI - 1]}' matches, so it is kept.");
                } else if (i > 0 && j > 0 && dp[i, j] == dp[i - 1, j - 1] + 1) {
                    replaced++; i--; j--;
                    path.Add((i, j));
                    steps.Accept((m, n, false), $"({fromI},{fromJ}) came from ({i},{j}): replace '{x[fromI - 1]}' with '{y[fromJ - 1]}'.");
                } else if (i > 0 && dp[i, j] == dp[i - 1, j] + 1) {
                    deleted++; i--;
                    path.Add((i, j));
                    steps.Accept((m, n, false), $"({fromI},{fromJ}) came from ({i},{j}): delete '{x[fromI - 1]}'.");
                } else {
                    inserted++; j--;
                    path.Add((i, j));
                    steps.Accept((m, n, false), $"({fromI},{fromJ}) came from ({i},{j}): insert '{y[fromJ - 1]}'.");
                }
            }
            var edits = new List<string>();
            if (replaced > 0) edits.Add(Words.Count(replaced, "replacement"));
            if (deleted > 0) edits.Add(Words.Count(deleted, "deletion"));
            if (inserted > 0) edits.Add(Words.Count(inserted, "insertion"));
            var table = new TableCells(Filled(dp, m, n), [], path.Select(p => CellId(p.I, p.J)));
            if (edits.Count == 0)
                steps.Done(table, true, $"Edit distance {dp[m, n]}: the strings are already the same.");
            else
                steps.Done(table, true, $"Edit distance {dp[m, n]}: one cheapest way is {string.Join(", ", edits)}.");
        }

        return dp[m, n].ToString();
    }

    // The table's cells filled so far: row 0, column 0, and every cell up to (row, col) in reading order.
    private static IEnumerable<KeyValuePair<string, string>> Filled(int[,] dp, int row, int col) {
        for (int i = 0; i < dp.GetLength(0); i++)
            for (int j = 0; j < dp.GetLength(1); j++)
                if (i == 0 || j == 0 || i < row || (i == row && j <= col))
                    yield return new(CellId(i, j), Num(dp[i, j]));
    }

    // The cells cell (i, j)'s recurrence compared, the one it took first: the diagonal for matching letters; otherwise
    // replace (diagonal), delete (above) and insert (left), the first of them that is cheapest first, as Math.Min picks.
    private static IEnumerable<string> Compared(string x, string y, int[,] dp, int i, int j) {
        if (x[i - 1] == y[j - 1]) return [CellId(i - 1, j - 1)];
        var options = new (int I, int J)[] { (i - 1, j - 1), (i - 1, j), (i, j - 1) };
        int best = Enumerable.Range(0, 3).OrderBy(k => dp[options[k].I, options[k].J]).First();
        return new[] { options[best] }.Concat(options.Where((_, k) => k != best)).Select(c => CellId(c.I, c.J));
    }
}
