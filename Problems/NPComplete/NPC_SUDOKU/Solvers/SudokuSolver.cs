using API.Interfaces;
using API.Interfaces.Board;
using API.Interfaces.Steps;

namespace API.Problems.NPComplete.NPC_SUDOKU.Solvers;

// The steps Sudoku Backtracking records, for the Board picture and the Search Tree picture alike. Square by square, in
// reading order:
//   - a Try for every digit put in an empty square (a first guess, the only digit left, or the next digit after the one
//     before led to a dead end); in the tree, a node "R1C3=4" under the digit of the square before it, the solution in
//     the last empty square;
//   - a Reject when a square has no digit left, so the last choice has to be undone (in the tree, a pruned node "R1C4"
//     under that choice);
//   - a Reject before any search when the clues already clash, with each clashing pair linked (in the tree, the root
//     "start", pruned);
//   - the Done, which names the answer's path in the tree, root first.
// The search never says it takes a digit back: it just tries the next one. So a digit whose squares after it could not all
// be filled is marked failed in the tree with the next step kept (or the Done), together with any digits that failed
// below it.
// Incremental: it fills one square at a time and takes digits back out, so every step's partial is the board so far.
class SudokuSolver : ISolver<SUDOKU, BoardCells>, IIncrementalSolver, ISearchTreeSolver {

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

    // The grid is the solver's only input; all dimensions are derived from it.
    public string Solve(SUDOKU problem, StepRecorder<BoardCells> rec) => SolveInternal(problem.grid, rec);

    // A step of the search: the square it is about, given by its row and column, the digit put there (0: none), and the
    // depth of its search-tree node (0 for the root, d for the d-th square filled on the way), left in state Tree.
    private readonly record struct Move(int Row, int Col, int Digit, int Depth, SearchNodeState Tree);

    // How the steps show a move. The search keeps its own tree (a "start" root, a square with no digit left as a pruned
    // node, a digit marked failed once the squares after it could not all be filled), so it gives RunSteps the tree nodes
    // of each step rather than using SearchSteps.
    private static RunSteps<Move, BoardCells> NewSteps(StepRecorder<BoardCells> rec, int[][] grid, Search search) => new(rec,
        // What every picture shows: the board so far (clues included), and the square the step is about.
        partial: _ => Board(grid, []),
        focus: m => [BoardFrames.CellId(m.Row, m.Col)],
        // The Search Tree picture's nodes only: a digit put in a square ("R1C3=4"), a square with no digit left ("R1C4",
        // noted), or the root when the clues clash, after the digits that failed since the last step kept.
        tree: m => search.Step(m.Depth, m.Digit > 0 ? $"{SquareName(m.Row, m.Col)}={m.Digit}" : SquareName(m.Row, m.Col), m.Tree,
            m.Digit > 0 ? null : m.Depth > 0 ? "no digit left" : "clues clash"));

    // What the search carries besides the grid: its steps, how many digits it has tried, how many squares it has filled on
    // the way to the current one (0: there is no earlier choice to undo), and how many it has to fill. It is the search tree
    // too, one depth for each square filled.
    private sealed class Search(int squares) : DepthFirstTree(squares) {
        public RunSteps<Move, BoardCells> Steps = null!;
        public int Tries;
        public int Depth;
        public readonly int Squares = squares;

        // The Done's nodes for the answer: the answer's path, root first, through each square the clues left empty (in
        // reading order) with its digit.
        public SearchNode[] AnswerPath(int[][] clues, int[][] solution) {
            var labels = new List<string>();
            for (int r = 0; r < clues.Length; r++)
                for (int c = 0; c < clues[r].Length; c++)
                    if (clues[r][c] == 0) labels.Add($"{SquareName(r, c)}={solution[r][c]}");
            return AnswerPath(labels);
        }
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
        // At the time limit, unwind as if solved; SolveInternal then reports the stop.
        if (timerHasExpired)
            return true;
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
        var steps = search.Steps;
        // In the tree, the square's node (a digit tried, or the square itself with no digit left) is a child of the digit
        // tried in the square before it, one level deeper.
        int depth = search.Depth + 1;
        if (candidates.Count == 0) {
            search.Branch(depth);
            var dead = new Move(row, col, 0, depth, SearchNodeState.Pruned);
            if (search.Depth > 0)
                steps.Reject(dead, $"{SquareName(row, col)} has no digit left: every digit from 1 to {gridSize} is already in its row, column or box. Undo the last choice.");
            else
                steps.Reject(dead, $"{SquareName(row, col)} has no digit left: every digit from 1 to {gridSize} is already in its row, column or box. So the grid has no solution.");
            return false;
        }
        int previous = 0;
        foreach (int k in candidates) {
            SetValue(grid, rows, cols, blocks, k, row, col);
            //    var changelog = HiddenSingles(grid, rows, cols, blocks, row, col + 1);        // Hidden singles optimization is unfinished, requires more overhead
            search.Tries++;
            search.Branch(depth);
            // The digit in the last empty square completes the grid, since every candidate fits.
            var tried = new Move(row, col, k, depth, depth == search.Squares ? SearchNodeState.Solution : SearchNodeState.Explored);
            // The candidates are listed in the order this loop tries them (the sets' own order).
            if (previous != 0)
                steps.Try(tried, $"{previous} in {SquareName(row, col)} led to a dead end. Try {k} instead.");
            else if (candidates.Count == 1)
                steps.Try(tried, $"{SquareName(row, col)} can only hold {k}. Try it.");
            else
                steps.Try(tried, $"{SquareName(row, col)} can still hold {BoardFrames.OrList(candidates.Select(v => v.ToString()).ToList())}. Try {k}.");

            search.Depth++;
            if (SolveHelper(grid, rows, cols, blocks, row, col + 1, getAllSolutions, solutions, search))
                return true;
            search.Depth--;
            // The search never records taking the digit back, so the tree marks it failed with the next step kept.
            if (search.Sent(depth)) search.Failed(depth, $"{SquareName(row, col)}={k}");

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

        // The no-solution value is "{}" (the repo-wide convention).
        if (!CluesAreValid(problem)) {
            var clues = NewSteps(rec, problem, new Search(0));
            var clashes = Clashes(problem);
            if (clashes.Count > 0) {
                // This step is about every clashing pair at once: each later clue is linked to the earlier one it repeats.
                var (r1, c1, r2, c2, unit) = clashes[0];
                clues.Reject(new Move(r1, c1, 0, 0, SearchNodeState.Pruned), $"The clues already clash: {SquareName(r1, c1)} and {SquareName(r2, c2)} both hold {problem[r1][c1]} in the same {unit}. No grid can be completed, so there is nothing to search.",
                    focus: () => clashes.SelectMany(k => new[] { BoardFrames.CellId(k.R1, k.C1), BoardFrames.CellId(k.R2, k.C2) }).Distinct().ToArray(),
                    partial: () => Board(problem, clashes.Select(k => new BoardConflict(BoardFrames.CellId(k.R2, k.C2), BoardFrames.CellId(k.R1, k.C1)))));
                clues.Done(new BoardCells([], []), false, $"No solution: the clues break the rules before a single square is filled.");
            } else {
                clues.Done(new BoardCells([], []), false, $"No solution: the grid is not a Sudoku the solver can fill.");
            }
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

        // Pre-scan: remove givens from candidate sets, and count the squares left to fill
        int empty = 0;
        for (int r = 0; r < gridSize; r++) {
            for (int c = 0; c < gridSize; c++) {
                int value = solution[r][c];
                if (value != 0) {
                    rows[r].Remove(value);
                    cols[c].Remove(value);
                    blocks[GetBlockIndex(solution, r, c)].Remove(value);
                } else {
                    empty++;
                }
            }
        }

        var search = new Search(empty);
        var steps = search.Steps = NewSteps(rec, solution, search);
        SolveHelper(solution, rows, cols, blocks, 0, 0, false, new List<int[][]>(), search);
        if (timerHasExpired)
            return SolveRun.TimedOutAnswer;

        // If solution contains any 0s, the problem is unsolveable
        for (int r = 0; r < solution.Length; r++) {
            for (int c = 0; c < solution.Length; c++) {
                if (solution[r][c] == 0) {
                    if (search.Tries == 0)
                        steps.Done(new BoardCells([], []), false, $"This grid has no solution.",
                            tree: () => search.NoAnswer());
                    else
                        steps.Done(new BoardCells([], []), false, $"Every choice ran into a dead end after {BoardFrames.Count(search.Tries)} {(search.Tries == 1 ? "try" : "tries")}. This grid has no solution.",
                            tree: () => search.NoAnswer());
                    return NoSolution;
                }
            }
        }

        if (search.Tries == 0)
            steps.Done(Board(solution, []), true, $"Every square was already filled and no row, column or box repeats a digit, so there was nothing to try.",
                tree: () => search.AnswerPath(problem, solution));
        else
            steps.Done(Board(solution, []), true, $"Every square is filled and no row, column or box repeats a digit. Solved after {BoardFrames.Count(search.Tries)} {(search.Tries == 1 ? "try" : "tries")}.",
                tree: () => search.AnswerPath(problem, solution));

        var sb = new System.Text.StringBuilder();
        for (int r = 0; r < solution.Length; r++) {
            sb.Append(string.Join(",", solution[r]));
            if (r < solution.Length - 1) sb.Append(";");
        }
        return sb.ToString();
    }

    // --- Helpers ---

    private const string NoSolution = "{}";

    // --- The Board picture (built only for a step that is kept, so solving without steps never pays for it) ---

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
