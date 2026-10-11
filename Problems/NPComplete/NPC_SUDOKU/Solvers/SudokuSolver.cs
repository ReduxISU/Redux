using API.Interfaces;
using API.Interfaces.Board;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_SUDOKU.Solvers;

// Incremental: it fills one square at a time and takes digits back out, so every step's partial is the board so far.
class SudokuSolver : ISolver<SUDOKU, BoardCells>, IIncrementalSolver {

    // --- Fields ---
    public string solverName { get; } = "Sudoku Backtracking";
    public string solverDefinition { get; } = "This solver uses backtracking and heuristic search to solve Sudoku problems.";
    public string source { get; } = "Bhattarai, Apekshya, Dinisha Uprety, Pooja Pathak, Safal Shrestha, Salina Narkarmi, and Sanjog Sigdel. 2025. “A Study of Sudoku Solving Algorithms: Backtracking and Heuristic.” Department of Computer Science, Kathmandu University.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Eric Hill", "Carter Luker", "Collin M Kress", "Danny Fawson" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Does real (if minor) pruning, but tagged BruteForce per its class name
    // and because the pruning is incidental, not the algorithm's defining feature -- contrast with
    // the Backtracking cluster, where pruning/bounding IS the defining feature.
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // Declared, not derived. Recursive cell-by-cell backtracking over an n x n grid
    // (n^2 cells, up to n candidate values per cell after row/col/block pruning); the
    // row/col/block HashSets cut branches in practice but don't change the worst-case bound.
    public MathematicalFunction timeComplexity { get; } = new("n ^ (n^2)", ", n = grid dimension");

    // --- Methods Including Constructors ---
    public SudokuSolver() { }

    /*public SudokuSolver(int[][] grid)
    {
        _gridSize = grid.Length;

        if (_gridSize == 0)
            throw new ArgumentException("Grid must not be empty.");
        foreach (var row in grid)
            if (row.Length != _gridSize)
                throw new ArgumentException("Grid must be square.");

        int sqrt = (int)Math.Sqrt(_gridSize);
        if (sqrt * sqrt != _gridSize)
            throw new ArgumentException("Grid side length must be a perfect square.");

        _blockSize = sqrt;
        _problem = grid;
    }*/

    public string solve(SUDOKU problem) => Solve(problem, StepRecorder<BoardCells>.Off);

    // Steps (the Board picture): a Try for every digit put in a square, a Reject when a square has no digit left (or when
    // the clues already clash), then the answer. The grid is the solver's only input; all dimensions are derived from it.
    public string Solve(SUDOKU problem, StepRecorder<BoardCells> rec) => SolveInternal(problem.grid, rec);

    // What the search carries besides the grid: the recorder, how many digits it has tried, and how many squares it has
    // filled on the way to the current one (0: there is no earlier choice to undo).
    private sealed class Search {
        public required StepRecorder<BoardCells> Rec { get; init; }
        public int Tries;
        public int Depth;
    }

    // Given a cell's row and column index, return the index of the corresponding block unit.
    private static int GetBlockIndex(int[][] grid, int row, int col) {
        int blockSize = (int)Math.Sqrt(grid.Length);
        return row / blockSize * blockSize + col / blockSize;
    }

    // Sets the value of a particular cell and removes it from the relevant sets
    private void SetValue(int[][] grid, HashSet<int>[] rows, HashSet<int>[] cols, HashSet<int>[] blocks,
                          int value, int row, int col) {
        grid[row][col] = value;
        int b = GetBlockIndex(grid, row, col);
        rows[row].Remove(value);
        cols[col].Remove(value);
        blocks[b].Remove(value);
    }

    // Clears the value of a particular cell and adds it to the relevant sets
    private int ClearValue(int[][] grid, HashSet<int>[] rows, HashSet<int>[] cols, HashSet<int>[] blocks,
                            int row, int col) {
        int value = grid[row][col];
        grid[row][col] = 0;
        int b = GetBlockIndex(grid, row, col);
        rows[row].Add(value);
        cols[col].Add(value);
        blocks[b].Add(value);
        return value;
    }

    // UNFINISHED. Returns a list of (value, row, col) changes made, for undoing on backtrack.
    private List<(int value, int row, int col)> HiddenSingles(
        int[][] grid, HashSet<int>[] rows, HashSet<int>[] cols, HashSet<int>[] blocks,
        int startRow, int startCol) {
        int gridSize = grid.Length;
        int blockSize = (int)Math.Sqrt(gridSize);
        var changelog = new List<(int, int, int)>();
        var queue = new Queue<(int row, int col)>();
        var visited = new HashSet<(int, int)>();

        queue.Enqueue((startRow, startCol));

        while (queue.Count > 0) {
            var (row, col) = queue.Dequeue();

            if (!visited.Add((row, col)))
                continue;

            // Check row for singles
            for (int c = 0; c < gridSize; c++) {
                if (grid[row][c] != 0) continue;
                int b = GetBlockIndex(grid, row, c);
                var possible = Intersect(rows[row], cols[c], blocks[b]);
                if (possible.Count == 1) {
                    int value = First(possible);
                    SetValue(grid, rows, cols, blocks, value, row, c);
                    changelog.Add((value, row, c));
                    queue.Enqueue((row, c));
                }
            }

            // Check column for singles
            for (int r = 0; r < gridSize; r++) {
                if (grid[r][col] != 0) continue;
                int b = GetBlockIndex(grid, r, col);
                var possible = Intersect(rows[r], cols[col], blocks[b]);
                if (possible.Count == 1) {
                    int value = First(possible);
                    SetValue(grid, rows, cols, blocks, value, r, col);
                    changelog.Add((value, r, col));
                    queue.Enqueue((r, col));
                }
            }

            // Check block for singles
            int blockRow = row / blockSize * blockSize;
            int blockCol = col / blockSize * blockSize;
            int blockIdx = GetBlockIndex(grid, row, col);

            for (int r = blockRow; r < blockRow + blockSize; r++) {
                for (int c = blockCol; c < blockCol + blockSize; c++) {
                    if (grid[r][c] != 0) continue;
                    var possible = Intersect(rows[r], cols[c], blocks[blockIdx]);
                    if (possible.Count == 1) {
                        int value = First(possible);
                        SetValue(grid, rows, cols, blocks, value, r, c);
                        changelog.Add((value, r, c));
                        queue.Enqueue((r, c));
                    }
                }
            }
        }

        return changelog;
    }

    // UNFINISHED
    private void UndoHiddenSingles(int[][] grid, HashSet<int>[] rows, HashSet<int>[] cols, HashSet<int>[] blocks,
                                    List<(int value, int row, int col)> changelog) {
        foreach (var (_, row, col) in changelog)
            ClearValue(grid, rows, cols, blocks, row, col);
    }

    private bool SolveHelper(int[][] grid, HashSet<int>[] rows, HashSet<int>[] cols, HashSet<int>[] blocks,
                              int row, int col, bool getAllSolutions, List<int[][]> solutions, Search search) {
        int gridSize = grid.Length;
        // Base case: all rows processed
        if (row == gridSize) {
            if (getAllSolutions) {
                solutions.Add(CopyGrid(grid));
                return false;
            }
            return true;
        }

        // Move to next row
        if (col == gridSize)
            return SolveHelper(grid, rows, cols, blocks, row + 1, 0, getAllSolutions, solutions, search);

        // Skip filled cells
        if (grid[row][col] != 0)
            return SolveHelper(grid, rows, cols, blocks, row, col + 1, getAllSolutions, solutions, search);

        // Try each candidate value
        int b = GetBlockIndex(grid, row, col);
        var candidates = Intersect(rows[row], cols[col], blocks[b]);
        var rec = search.Rec;
        if (candidates.Count == 0) {
            rec.Reject(() => Board(grid, []),
                () => $"{SquareName(row, col)} has no digit left: every digit from 1 to {gridSize} is already in its row, column or box. "
                    + (search.Depth > 0 ? "Undo the last choice." : "So the grid has no solution."),
                () => [BoardFrames.CellId(row, col)]);
            return false;
        }
        int previous = 0;
        foreach (int k in candidates) {
            SetValue(grid, rows, cols, blocks, k, row, col);
            //    var changelog = HiddenSingles(grid, rows, cols, blocks, row, col + 1);        // Hidden singles optimization is unfinished, requires more overhead
            search.Tries++;
            // The candidates are listed in the order this loop tries them (the sets' own order).
            rec.Try(() => Board(grid, []),
                () => previous != 0 ? $"{previous} in {SquareName(row, col)} led to a dead end. Try {k} instead."
                    : candidates.Count == 1 ? $"{SquareName(row, col)} can only hold {k}. Try it."
                    : $"{SquareName(row, col)} can still hold {BoardFrames.OrList(candidates.Select(v => v.ToString()).ToList())}. Try {k}.",
                () => [BoardFrames.CellId(row, col)]);

            search.Depth++;
            if (SolveHelper(grid, rows, cols, blocks, row, col + 1, getAllSolutions, solutions, search))
                return true;
            search.Depth--;

            // Undo edits if solution is not valid
            ClearValue(grid, rows, cols, blocks, row, col);
            //    UndoHiddenSingles(grid, rows, cols, blocks, changelog);
            previous = k;
        }

        return false;
    }

    //public string solve(SUDOKU problem) => solve();

    public string SolveInternal(int[][] problem) => SolveInternal(problem, StepRecorder<BoardCells>.Off);

    private string SolveInternal(int[][] problem, StepRecorder<BoardCells> rec) {
        int gridSize = problem.Length;

        // The no-solution value is "{}" (the repo-wide convention, see #605).
        if (!CluesAreValid(problem)) {
            var clashes = Clashes(problem);
            if (clashes.Count > 0) {
                var (r1, c1, r2, c2, unit) = clashes[0];
                rec.Reject(() => Board(problem, clashes.Select(k => new BoardConflict(BoardFrames.CellId(k.R2, k.C2), BoardFrames.CellId(k.R1, k.C1)))),
                    () => $"The clues already clash: {SquareName(r1, c1)} and {SquareName(r2, c2)} both hold {problem[r1][c1]} in the same {unit}. "
                        + "No grid can be completed, so there is nothing to search.",
                    () => clashes.SelectMany(k => new[] { BoardFrames.CellId(k.R1, k.C1), BoardFrames.CellId(k.R2, k.C2) }).Distinct().ToArray());
            }
            rec.Done(new BoardCells([], []), false, clashes.Count > 0
                ? "No solution: the clues break the rules before a single square is filled."
                : "No solution: the grid is not a Sudoku the solver can fill.");
            return NoSolution;
        }

        int[][] solution = CopyGrid(problem);

        var rows = new HashSet<int>[gridSize];
        var cols = new HashSet<int>[gridSize];
        var blocks = new HashSet<int>[gridSize];

        for (int i = 0; i < gridSize; i++) {
            rows[i] = BuildValueSet(gridSize);
            cols[i] = BuildValueSet(gridSize);
            blocks[i] = BuildValueSet(gridSize);
        }

        // Pre-scan: remove givens from candidate sets
        for (int r = 0; r < gridSize; r++) {
            for (int c = 0; c < gridSize; c++) {
                int value = solution[r][c];
                if (value != 0) {
                    rows[r].Remove(value);
                    cols[c].Remove(value);
                    blocks[GetBlockIndex(solution, r, c)].Remove(value);
                }
            }
        }

        var search = new Search { Rec = rec };
        SolveHelper(solution, rows, cols, blocks, 0, 0, false, new List<int[][]>(), search);
        string tries = $"{BoardFrames.Count(search.Tries)} {(search.Tries == 1 ? "try" : "tries")}";

        // If solution contains any 0s, the problem is unsolveable
        for (int r = 0; r < solution.Length; r++) {
            for (int c = 0; c < solution.Length; c++) {
                if (solution[r][c] == 0) {
                    rec.Done(new BoardCells([], []), false, search.Tries == 0
                        ? "This grid has no solution."
                        : $"Every choice ran into a dead end after {tries}. This grid has no solution.");
                    return NoSolution;
                }
            }
        }

        rec.Done(Board(solution, []), true, search.Tries == 0
            ? "Every square was already filled and no row, column or box repeats a digit, so there was nothing to try."
            : $"Every square is filled and no row, column or box repeats a digit. Solved after {tries}.");

        var sb = new System.Text.StringBuilder();
        for (int r = 0; r < solution.Length; r++) {
            sb.Append(string.Join(",", solution[r]));
            if (r < solution.Length - 1) sb.Append(";");
        }
        return sb.ToString();
    }

    // --- Helpers ---

    private const string NoSolution = "{}";

    // --- The Board picture (built only inside recorder lambdas, so solving without steps never pays for it) ---

    // How captions name a square: R1C1 for the top-left (rows and columns counted from 1, as Sudoku players do).
    private static string SquareName(int row, int col) => $"R{row + 1}C{col + 1}";

    // The board as the picture draws it: every filled square, clues included, and the conflicts a step found.
    private static BoardCells Board(int[][] grid, IEnumerable<BoardConflict> conflicts) {
        var values = new List<KeyValuePair<string, string>>();
        for (int r = 0; r < grid.Length; r++)
            for (int c = 0; c < grid[r].Length; c++)
                if (grid[r][c] != 0) values.Add(new(BoardFrames.CellId(r, c), grid[r][c].ToString()));
        return new BoardCells(values, conflicts);
    }

    // Every pair of clues that share a row, column or block and hold the same digit, in reading order: the earlier square
    // first, then the later one, and which unit they share.
    private static List<(int R1, int C1, int R2, int C2, string Unit)> Clashes(int[][] grid) {
        int n = grid.Length;
        int blockSize = (int)Math.Sqrt(n);
        var found = new List<(int, int, int, int, string)>();
        for (int p = 0; p < n * n; p++) {
            int r1 = p / n, c1 = p % n;
            if (grid[r1].Length != n || grid[r1][c1] == 0) continue;
            for (int q = p + 1; q < n * n; q++) {
                int r2 = q / n, c2 = q % n;
                if (grid[r2].Length != n || grid[r2][c2] != grid[r1][c1]) continue;
                string? unit = r1 == r2 ? "row" : c1 == c2 ? "column"
                    : blockSize > 0 && r1 / blockSize == r2 / blockSize && c1 / blockSize == c2 / blockSize ? "box" : null;
                if (unit != null) found.Add((r1, c1, r2, c2, unit));
            }
        }
        return found;
    }

    // True when the grid is square, every value is in 0..n, and no non-zero clue repeats within a
    // row, column or block. A grid that fails this has no solution, so there is nothing to search.
    private static bool CluesAreValid(int[][] grid) {
        int n = grid.Length;
        int blockSize = (int)Math.Sqrt(n);
        if (n == 0 || blockSize * blockSize != n) return false;

        var rowSeen = new bool[n, n + 1];
        var colSeen = new bool[n, n + 1];
        var blockSeen = new bool[n, n + 1];

        for (int r = 0; r < n; r++) {
            if (grid[r].Length != n) return false;
            for (int c = 0; c < n; c++) {
                int v = grid[r][c];
                if (v < 0 || v > n) return false;
                if (v == 0) continue;
                int b = r / blockSize * blockSize + c / blockSize;
                if (rowSeen[r, v] || colSeen[c, v] || blockSeen[b, v]) return false;
                rowSeen[r, v] = colSeen[c, v] = blockSeen[b, v] = true;
            }
        }
        return true;
    }

    // Returns a new HashSet containing the intersection of three sets, without mutating any of them.
    private static HashSet<int> Intersect(HashSet<int> a, HashSet<int> b, HashSet<int> c) {
        var result = new HashSet<int>(a);
        result.IntersectWith(b);
        result.IntersectWith(c);
        return result;
    }

    private static int First(HashSet<int> set) {
        foreach (int v in set) return v;
        throw new InvalidOperationException("Set is empty.");
    }

    private static HashSet<int> BuildValueSet(int gridSize) {
        var set = new HashSet<int>(gridSize);
        for (int i = 1; i <= gridSize; i++)
            set.Add(i);
        return set;
    }

    private static int[][] CopyGrid(int[][] grid) {
        int[][] copy = new int[grid.Length][];
        for (int i = 0; i < grid.Length; i++)
            copy[i] = (int[])grid[i].Clone();
        return copy;
    }

}
