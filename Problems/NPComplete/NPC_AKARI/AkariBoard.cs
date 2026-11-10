using System.Text;

namespace API.Problems.NPComplete.NPC_AKARI;

/// <summary>
/// The geometry of one Akari grid, worked out once so the verifier and every solver read the rules the same way.
/// <para>
/// White cells are numbered 0..<see cref="WhiteCount"/>-1 in row-major order; a bulb placement is a <c>bool[]</c>
/// indexed by that number. Each white cell lies in exactly one <b>row segment</b> and one <b>column segment</b>: a
/// maximal run of white cells in its row or column between walls (or the edge of the grid). A bulb lights exactly the
/// cells of its two segments, so "lit" and "two bulbs see each other" are both questions about segments.
/// </para>
/// </summary>
internal sealed class AkariBoard {
    /// <summary>The certificate symbol for a light bulb (a white cell holding one).</summary>
    public const char BulbSymbol = '*';

    public int[][] Grid { get; }
    public int Rows { get; }
    public int Cols { get; }
    public int WhiteCount => CellRow.Length;

    /// <summary>White-cell index to its row and column in the grid.</summary>
    public int[] CellRow { get; }
    public int[] CellCol { get; }

    /// <summary>Grid position to white-cell index, or -1 for a wall or clue.</summary>
    public int[][] IndexOf { get; }

    /// <summary>White-cell index to the id of its row segment and of its column segment (one id space for both).</summary>
    public int[] RowSegment { get; }
    public int[] ColSegment { get; }

    /// <summary>Segment id to the white cells in it.</summary>
    public int[][] SegmentCells { get; }

    /// <summary>Clue index to the clue's number and to the white cells orthogonally next to it.</summary>
    public int[] ClueValue { get; }
    public int[][] ClueNeighbors { get; }

    /// <summary>White-cell index to the clues orthogonally next to it.</summary>
    public int[][] CellClues { get; }

    public AkariBoard(int[][] grid) {
        Grid = grid;
        Rows = grid.Length;
        Cols = Rows == 0 ? 0 : grid[0].Length;

        var cellRow = new List<int>();
        var cellCol = new List<int>();
        IndexOf = new int[Rows][];
        for (int r = 0; r < Rows; r++) {
            IndexOf[r] = new int[Cols];
            for (int c = 0; c < Cols; c++) {
                if (grid[r][c] == AKARI.EMPTY_CELL) {
                    IndexOf[r][c] = cellRow.Count;
                    cellRow.Add(r);
                    cellCol.Add(c);
                } else {
                    IndexOf[r][c] = -1;
                }
            }
        }
        CellRow = cellRow.ToArray();
        CellCol = cellCol.ToArray();

        // Segments: scan each row, then each column, starting a new segment after every wall.
        var segments = new List<List<int>>();
        RowSegment = new int[WhiteCount];
        ColSegment = new int[WhiteCount];
        for (int r = 0; r < Rows; r++) {
            List<int>? current = null;
            for (int c = 0; c < Cols; c++) {
                int i = IndexOf[r][c];
                if (i < 0) { current = null; continue; }
                if (current == null) { current = new List<int>(); segments.Add(current); }
                current.Add(i);
                RowSegment[i] = segments.Count - 1;
            }
        }
        for (int c = 0; c < Cols; c++) {
            List<int>? current = null;
            for (int r = 0; r < Rows; r++) {
                int i = IndexOf[r][c];
                if (i < 0) { current = null; continue; }
                if (current == null) { current = new List<int>(); segments.Add(current); }
                current.Add(i);
                ColSegment[i] = segments.Count - 1;
            }
        }
        SegmentCells = segments.Select(s => s.ToArray()).ToArray();

        var clueValue = new List<int>();
        var clueNeighbors = new List<int[]>();
        var cellClues = Enumerable.Range(0, WhiteCount).Select(_ => new List<int>()).ToArray();
        for (int r = 0; r < Rows; r++) {
            for (int c = 0; c < Cols; c++) {
                if (grid[r][c] < 0) continue; // white cell or unnumbered wall
                int clue = clueValue.Count;
                clueValue.Add(grid[r][c]);
                var neighbors = new List<int>();
                foreach (var (dr, dc) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) }) {
                    int nr = r + dr, nc = c + dc;
                    if (nr < 0 || nr >= Rows || nc < 0 || nc >= Cols) continue;
                    int i = IndexOf[nr][nc];
                    if (i < 0) continue;
                    neighbors.Add(i);
                    cellClues[i].Add(clue);
                }
                clueNeighbors.Add(neighbors.ToArray());
            }
        }
        ClueValue = clueValue.ToArray();
        ClueNeighbors = clueNeighbors.ToArray();
        CellClues = cellClues.Select(l => l.ToArray()).ToArray();
    }

    /// <summary>
    /// True when <paramref name="bulb"/> (one entry per white cell) solves the puzzle: every white cell is lit, no
    /// two bulbs share a row or column segment (so none lights another), and every clue has exactly its number of
    /// bulbs next to it. O(rows * cols).
    /// </summary>
    public bool IsSolution(bool[] bulb) {
        if (bulb.Length != WhiteCount) return false;

        var segmentBulbs = new int[SegmentCells.Length];
        for (int i = 0; i < WhiteCount; i++) {
            if (!bulb[i]) continue;
            if (++segmentBulbs[RowSegment[i]] > 1) return false;
            if (++segmentBulbs[ColSegment[i]] > 1) return false;
        }
        for (int i = 0; i < WhiteCount; i++) {
            if (segmentBulbs[RowSegment[i]] == 0 && segmentBulbs[ColSegment[i]] == 0) return false;
        }
        for (int k = 0; k < ClueValue.Length; k++) {
            int count = 0;
            foreach (int i in ClueNeighbors[k]) {
                if (bulb[i]) count++;
            }
            if (count != ClueValue[k]) return false;
        }
        return true;
    }

    /// <summary>
    /// Reads a parsed certificate grid (cells as in <see cref="AKARI.grid"/>, plus <see cref="AKARI.BULB"/>) against
    /// this board. Returns the bulb placement, or null when the certificate does not describe this instance: a
    /// different size, a wall or clue changed, or a bulb where the instance has a wall or clue.
    /// </summary>
    public bool[]? BulbsFrom(int[][] certificateGrid) {
        if (certificateGrid.Length != Rows) return null;
        var bulb = new bool[WhiteCount];
        for (int r = 0; r < Rows; r++) {
            if (certificateGrid[r].Length != Cols) return null;
            for (int c = 0; c < Cols; c++) {
                int cell = certificateGrid[r][c];
                int i = IndexOf[r][c];
                if (i >= 0) {
                    if (cell == AKARI.BULB) bulb[i] = true;
                    else if (cell != AKARI.EMPTY_CELL) return null;
                } else if (cell != Grid[r][c]) {
                    return null;
                }
            }
        }
        return bulb;
    }

    /// <summary>Writes a bulb placement as a certificate: the instance grid on one line, with bulbs as '*'.</summary>
    public string Format(bool[] bulb) {
        var sb = new StringBuilder("(");
        for (int r = 0; r < Rows; r++) {
            if (r > 0) sb.Append(',');
            sb.Append('(');
            for (int c = 0; c < Cols; c++) {
                if (c > 0) sb.Append(',');
                int i = IndexOf[r][c];
                int cell = Grid[r][c];
                sb.Append(i >= 0 ? (bulb[i] ? BulbSymbol : '_') : cell == AKARI.WALL ? '#' : (char)('0' + cell));
            }
            sb.Append(')');
        }
        return sb.Append(')').ToString();
    }
}
