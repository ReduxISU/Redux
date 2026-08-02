using API.Interfaces;

namespace API.Problems.NPComplete.NPC_SUDOKU.Solvers;
class SudokuSolver : ISolver<SUDOKU> {

    // --- Fields ---
    public string solverName { get; } = "Backtrack";
    public string solverDefinition { get; } = "This solver uses backtracking and heuristic search to solve Sudoku problems.";
    public string source { get; } = "Bhattarai, Apekshya, Dinisha Uprety, Pooja Pathak, Safal Shrestha, Salina Narkarmi, and Sanjog Sigdel. 2025. “A Study of Sudoku Solving Algorithms: Backtracking and Heuristic.” Department of Computer Science, Kathmandu University.";
    public string[] contributors { get; } = { "Eric Hill, Carter Luker, Collin Kress, & Daniel Fawson" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Does real (if minor) pruning, but tagged BruteForce per its class name
    // and because the pruning is incidental, not the algorithm's defining feature -- contrast with
    // the Backtracking cluster, where pruning/bounding IS the defining feature.
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // Declared, not derived. Recursive cell-by-cell backtracking over an n x n grid
    // (n^2 cells, up to n candidate values per cell after row/col/block pruning); the
    // row/col/block HashSets cut branches in practice but don't change the worst-case bound.
    public string complexity { get; } = "O(n ^ (n^2)), n = grid dimension";

    // --- Methods Including Constructors ---
    public SudokuSolver() {}

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

    public string solve(SUDOKU problem)
    {
        //if (timerHasExpired)
        //    return "timeout";

        // The grid is the solver's only input; all dimensions are derived from it.
        return SolveInternal(problem.grid);
    }

    // Given a cell's row and column index, return the index of the corresponding block unit.
    private static int GetBlockIndex(int[][] grid, int row, int col)
    {
        int blockSize = (int)Math.Sqrt(grid.Length);
        return row / blockSize * blockSize + col / blockSize;
    }

    // Sets the value of a particular cell and removes it from the relevant sets
    private void SetValue(int[][] grid, HashSet<int>[] rows, HashSet<int>[] cols, HashSet<int>[] blocks,
                          int value, int row, int col)
    {
        grid[row][col] = value;
        int b = GetBlockIndex(grid, row, col);
        rows[row].Remove(value);
        cols[col].Remove(value);
        blocks[b].Remove(value);
    }

    // Clears the value of a particular cell and adds it to the relevant sets
    private int ClearValue(int[][] grid, HashSet<int>[] rows, HashSet<int>[] cols, HashSet<int>[] blocks,
                            int row, int col)
    {
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
        int startRow, int startCol)
    {
        int gridSize = grid.Length;
        int blockSize = (int)Math.Sqrt(gridSize);
        var changelog = new List<(int, int, int)>();
        var queue = new Queue<(int row, int col)>();
        var visited = new HashSet<(int, int)>();

        queue.Enqueue((startRow, startCol));

        while (queue.Count > 0)
        {
            var (row, col) = queue.Dequeue();

            if (!visited.Add((row, col)))
                continue;

            // Check row for singles
            for (int c = 0; c < gridSize; c++)
            {
                if (grid[row][c] != 0) continue;
                int b = GetBlockIndex(grid, row, c);
                var possible = Intersect(rows[row], cols[c], blocks[b]);
                if (possible.Count == 1)
                {
                    int value = First(possible);
                    SetValue(grid, rows, cols, blocks, value, row, c);
                    changelog.Add((value, row, c));
                    queue.Enqueue((row, c));
                }
            }

            // Check column for singles
            for (int r = 0; r < gridSize; r++)
            {
                if (grid[r][col] != 0) continue;
                int b = GetBlockIndex(grid, r, col);
                var possible = Intersect(rows[r], cols[col], blocks[b]);
                if (possible.Count == 1)
                {
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

            for (int r = blockRow; r < blockRow + blockSize; r++)
            {
                for (int c = blockCol; c < blockCol + blockSize; c++)
                {
                    if (grid[r][c] != 0) continue;
                    var possible = Intersect(rows[r], cols[c], blocks[blockIdx]);
                    if (possible.Count == 1)
                    {
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
                                    List<(int value, int row, int col)> changelog)
    {
        foreach (var (_, row, col) in changelog)
            ClearValue(grid, rows, cols, blocks, row, col);
    }

    private bool SolveHelper(int[][] grid, HashSet<int>[] rows, HashSet<int>[] cols, HashSet<int>[] blocks,
                              int row, int col, bool getAllSolutions, List<int[][]> solutions)
    {
        int gridSize = grid.Length;
        // Base case: all rows processed
        if (row == gridSize)
        {
            if (getAllSolutions)
            {
                solutions.Add(CopyGrid(grid));
                return false;
            }
            return true;
        }

        // Move to next row
        if (col == gridSize)
            return SolveHelper(grid, rows, cols, blocks, row + 1, 0, getAllSolutions, solutions);

        // Skip filled cells
        if (grid[row][col] != 0)
            return SolveHelper(grid, rows, cols, blocks, row, col + 1, getAllSolutions, solutions);

        // Try each candidate value
        int b = GetBlockIndex(grid, row, col);
        foreach (int k in Intersect(rows[row], cols[col], blocks[b]))
        {
            SetValue(grid, rows, cols, blocks, k, row, col);
        //    var changelog = HiddenSingles(grid, rows, cols, blocks, row, col + 1);        // Hidden singles optimization is unfinished, requires more overhead

            if (SolveHelper(grid, rows, cols, blocks, row, col + 1, getAllSolutions, solutions))
                return true;

            // Undo edits if solution is not valid
            ClearValue(grid, rows, cols, blocks, row, col);
        //    UndoHiddenSingles(grid, rows, cols, blocks, changelog);
        }

        return false;
    }

    //public string solve(SUDOKU problem) => solve();

    public string SolveInternal(int[][] problem)
    {
        int gridSize = problem.Length;
        int[][] solution = CopyGrid(problem);

        var rows   = new HashSet<int>[gridSize];
        var cols   = new HashSet<int>[gridSize];
        var blocks = new HashSet<int>[gridSize];

        for (int i = 0; i < gridSize; i++)
        {
            rows[i]   = BuildValueSet(gridSize);
            cols[i]   = BuildValueSet(gridSize);
            blocks[i] = BuildValueSet(gridSize);
        }

        // Pre-scan: remove givens from candidate sets
        for (int r = 0; r < gridSize; r++)
        {
            for (int c = 0; c < gridSize; c++)
            {
                int value = solution[r][c];
                if (value != 0)
                {
                    rows[r].Remove(value);
                    cols[c].Remove(value);
                    blocks[GetBlockIndex(solution, r, c)].Remove(value);
                }
            }
        }

        SolveHelper(solution, rows, cols, blocks, 0, 0, false, new List<int[][]>());

        // If solution contains any 0s, the problem is unsolveable
        for (int r = 0; r < solution.Length; r++)
        {
            for (int c = 0; c < solution.Length; c++)
            {
                if (solution[r][c] == 0) return "";
            }
        }

        var sb = new System.Text.StringBuilder();
        for (int r = 0; r < solution.Length; r++)
        {
            sb.Append(string.Join(",", solution[r]));
            if (r < solution.Length - 1) sb.Append(";");
        }
        return sb.ToString();
    }

    // --- Helpers ---

    // Returns a new HashSet containing the intersection of three sets, without mutating any of them.
    private static HashSet<int> Intersect(HashSet<int> a, HashSet<int> b, HashSet<int> c)
    {
        var result = new HashSet<int>(a);
        result.IntersectWith(b);
        result.IntersectWith(c);
        return result;
    }

    private static int First(HashSet<int> set)
    {
        foreach (int v in set) return v;
        throw new InvalidOperationException("Set is empty.");
    }

    private static HashSet<int> BuildValueSet(int gridSize)
    {
        var set = new HashSet<int>(gridSize);
        for (int i = 1; i <= gridSize; i++)
            set.Add(i);
        return set;
    }

    private static int[][] CopyGrid(int[][] grid)
    {
        int[][] copy = new int[grid.Length][];
        for (int i = 0; i < grid.Length; i++)
            copy[i] = (int[])grid[i].Clone();
        return copy;
    }
        
}
