using System.Text;
using API.Interfaces;
using API.Problems.NPComplete.NPC_AKARI.Solvers;
using API.Problems.NPComplete.NPC_AKARI.Verifiers;
using API.Problems.NPComplete.NPC_AKARI.Visualizations;
using SPADE;

namespace API.Problems.NPComplete.NPC_AKARI;

/// <summary>
/// The Akari (Light Up) problem: the instance, its parser, and the grid geometry that the verifier and every solver
/// read the rules from. This class is the single source of truth for Akari in Redux.
/// <para>
/// White cells are numbered 0..<see cref="WhiteCount"/>-1 in row-major order; a bulb placement is a <c>bool[]</c>
/// indexed by that number. Each white cell lies in exactly one <b>row segment</b> and one <b>column segment</b>: a
/// maximal run of white cells in its row or column between walls (or the edge of the grid). A bulb lights exactly the
/// cells of its two segments, so "lit" and "two bulbs see each other" are both questions about segments.
/// </para>
/// <para>
/// The geometry members are internal so they stay out of the serialized problem JSON (as SAT's <c>formula</c>).
/// </para>
/// </summary>
class AKARI : IProblem<AkariConstraintPropagation, AkariVerifier, AkariVisualization> {

    // --- Fields ---
    public string problemName { get; } = "Akari (Light Up Puzzle)";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Light_Up_(puzzle)";
    public string formalDefinition { get; } = "TODO"; // Consider excluding due to lack of formal definition
    public string problemDefinition { get; } = "An Akari (Light Up) puzzle is comprised of a rectangular m x n grid containing sets of white 'unlit' cells and black 'walls'. 'Light bulbs' can be placed in white cells that illuminate their row and column, until blocked by a wall. Black wall cells may optionally include a clue in the form of an integer indicating the number of expected adjacent bulbs. A puzzle instance is considered solvable if there is an arrangement of 'light bulbs' that satisfies the following conditions:\n1. Every white cell has line-of-sight to a light bulb.\n2. No light bulb illuminates another bulb.\n3. Walls with clue labeling are satisfied.";
    public string inputDescription { get; } = "An array of characters or integers corresponding to each cell in the grid. White cells are represented by '_'. Black cells are indicated by either '#' or a number (0-4) representing the clue.";
    public string outputDescription { get; } = "If solvable, the orignal puzzle board is returned with a valid bulb placement.";
    public string source { get; } = "Asaokitan. 2001. Bijutsukan. Puzzle Communication Nikoli, vol. 95. Nikoli Co., Ltd., Tokyo, Japan.";
    public string sourceLink { get; } = "https://www.nikoli.co.jp/en/puzzles/akari/";
    public string sourceFile { get; } = SourceFile.Path();
    private static readonly string _defaultInstance = """
    ((_,_,#,_,_,_,#),
    (_,4,_,_,1,_,#),
    (_,_,_,2,_,_,_),
    (_,#,_,_,_,#,_),
    (_,_,_,#,_,_,_),
    (#,_,#,_,_,1,_),
    (1,_,_,_,1,_,_))
    """;
    public string defaultInstance { get; } = _defaultInstance;
    // Short descriptive sentence with a concrete example. Consumed by
    // /ProblemProvider/info and by parse-error responses; this is the
    // canonical place to tell an LLM (or a human, or the GUI) what
    // shape your instance/certificate strings take.
    public string instanceFormat { get; } = $"Nested list of rows, each row a parenthesized comma-separated list of cells: {EMPTY_SYMBOL} (white), {WALL_SYMBOL} (black wall), or 0-4 (clue wall). Example: {_defaultInstance}";
    public string certificateFormat { get; } = $"The instance grid with every light bulb marked '{BULB_SYMBOL}' in place of a white cell '{EMPTY_SYMBOL}'; walls and clues are copied unchanged, and \"{{}}\" means no solution. Example (default instance): {AkariVerifier.CertificateExample}";
    public string instance { get; set; } = string.Empty;
    public string wikiName { get; } = ""; // Deprecated, do not implement
    public AkariConstraintPropagation defaultSolver { get; } = new AkariConstraintPropagation();
    public AkariVerifier defaultVerifier { get; } = new AkariVerifier();
    public AkariVisualization defaultVisualization { get; } = new AkariVisualization();
    public string[] contributors { get; } = { "Carter Luker", "Danny Fawson" };
    // Declared, not derived — do NOT infer this from which Problems/<Folder>/ this file
    // lives under. The folder is a filing convention only and is wrong for a dozen-plus
    // existing problems; declaring it explicitly here is the only source of truth the
    // API trusts. Leave as Unclassified (the default — this line can simply be deleted)
    // until someone has actually checked the literature/argued the class. Getting this
    // wrong is a public correctness claim the API will serve to callers.
    public ComplexityClass complexityClass { get; } = ComplexityClass.NPComplete;
    public ProblemType problemType { get; } = ProblemType.GamesAndPuzzles;

    // --- Properties ---

    // Integer encodings for non-clue cells in the parsed grid. Clue walls are stored as their value (0-4).
    public const int EMPTY_CELL = -2;
    public const int WALL = -1;
    // Only appears in a parsed certificate (see AkariVerifier), never in an instance's grid.
    public const int BULB = -3;

    // The text symbols for those cells, shared by the parser (ParseGrid) and the writer (Format). Clues are the digits 0-4.
    internal const char EMPTY_SYMBOL = '_';
    internal const char WALL_SYMBOL = '#';
    internal const char BULB_SYMBOL = '*';

    // Up, down, left, right.
    private static readonly (int dr, int dc)[] Directions = { (-1, 0), (1, 0), (0, -1), (0, 1) };

    public int[][] grid { get; } = Array.Empty<int[]>();

    // --- Geometry (internal: built once per instance, read by the verifier and the solvers) ---

    internal int Rows { get; private set; }
    internal int Cols { get; private set; }
    internal int WhiteCount => CellRow.Length;

    /// <summary>White-cell index to its row and column in the grid.</summary>
    internal int[] CellRow { get; private set; } = Array.Empty<int>();
    internal int[] CellCol { get; private set; } = Array.Empty<int>();

    /// <summary>Grid position to white-cell index, or -1 for a wall or clue.</summary>
    internal int[][] WhiteIndex { get; private set; } = Array.Empty<int[]>();

    /// <summary>White-cell index to the id of its row segment and of its column segment (one id space for both).</summary>
    internal int[] RowSegment { get; private set; } = Array.Empty<int>();
    internal int[] ColSegment { get; private set; } = Array.Empty<int>();

    /// <summary>Segment id to the white cells in it.</summary>
    internal int[][] SegmentCells { get; private set; } = Array.Empty<int[]>();

    /// <summary>Clue index to the clue's number and to the white cells orthogonally next to it.</summary>
    internal int[] ClueValue { get; private set; } = Array.Empty<int>();
    internal int[][] ClueNeighbors { get; private set; } = Array.Empty<int[]>();

    /// <summary>White-cell index to the clues orthogonally next to it.</summary>
    internal int[][] CellClues { get; private set; } = Array.Empty<int[]>();

    // --- Methods and Constructors ---
    public AKARI() : this(_defaultInstance) {

    }

    public AKARI(string stringInstance) {
        instance = stringInstance;

        // On a malformed instance, throw `ProblemParseException` (see
        // Interfaces/ParseExceptions.cs). The controller layer catches it
        // and returns HTTP 400 with `instanceFormat` quoted back to the
        // caller. Example:
        //
        //   if (string.IsNullOrWhiteSpace(stringInstance)) {
        //       throw new ProblemParseException("Akari", stringInstance, "instance is empty");
        //   }


        // Wall Set Version: The instance is comprised of the grid dimensions and the set of wall positions (and hints/clues).
        /*
        StringParser parser = new("{ (m, n, B) | m is int, n is int, B is set }");
        parser.parse(instance);
        int m = int.Parse(parser["m"].ToString());
        int n = int.Parse(parser["n"].ToString());
        UtilCollection walls = parser["B"];
        */

        // Puzzle Grid Version: 2D array of integer-encoded cells (see EMPTY_CELL / WALL).
        grid = ParseGrid(stringInstance, allowBulbs: false, detail => new ProblemParseException("Akari", stringInstance, detail));
        BuildGeometry();
    }

    // Works out the white-cell numbering, the segments and the clue adjacency of `grid`.
    private void BuildGeometry() {
        Rows = grid.Length;
        Cols = Rows == 0 ? 0 : grid[0].Length;

        var cellRow = new List<int>();
        var cellCol = new List<int>();
        var whiteIndex = new int[Rows][];
        for (int r = 0; r < Rows; r++) {
            whiteIndex[r] = new int[Cols];
            for (int c = 0; c < Cols; c++) {
                if (grid[r][c] == EMPTY_CELL) {
                    whiteIndex[r][c] = cellRow.Count;
                    cellRow.Add(r);
                    cellCol.Add(c);
                } else {
                    whiteIndex[r][c] = -1;
                }
            }
        }
        WhiteIndex = whiteIndex;
        CellRow = cellRow.ToArray();
        CellCol = cellCol.ToArray();

        // Segments: scan each row, then each column, starting a new segment after every wall.
        var segments = new List<List<int>>();
        var rowSegment = new int[WhiteCount];
        var colSegment = new int[WhiteCount];
        for (int r = 0; r < Rows; r++) {
            List<int>? current = null;
            for (int c = 0; c < Cols; c++) {
                int i = whiteIndex[r][c];
                if (i < 0) { current = null; continue; }
                if (current == null) { current = new List<int>(); segments.Add(current); }
                current.Add(i);
                rowSegment[i] = segments.Count - 1;
            }
        }
        for (int c = 0; c < Cols; c++) {
            List<int>? current = null;
            for (int r = 0; r < Rows; r++) {
                int i = whiteIndex[r][c];
                if (i < 0) { current = null; continue; }
                if (current == null) { current = new List<int>(); segments.Add(current); }
                current.Add(i);
                colSegment[i] = segments.Count - 1;
            }
        }
        RowSegment = rowSegment;
        ColSegment = colSegment;
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
                foreach (var (dr, dc) in Directions) {
                    int nr = r + dr, nc = c + dc;
                    if (nr < 0 || nr >= Rows || nc < 0 || nc >= Cols) continue;
                    int i = whiteIndex[nr][nc];
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
    internal bool IsSolution(bool[] bulb) {
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
    /// Reads a parsed certificate grid (cells as in <see cref="grid"/>, plus <see cref="BULB"/>) against this
    /// instance. Returns the bulb placement, or null when the certificate does not describe this instance: a
    /// different size, a wall or clue changed, or a bulb where the instance has a wall or clue.
    /// </summary>
    internal bool[]? BulbsFrom(int[][] certificateGrid) {
        if (certificateGrid.Length != Rows) return null;
        var bulb = new bool[WhiteCount];
        for (int r = 0; r < Rows; r++) {
            if (certificateGrid[r].Length != Cols) return null;
            for (int c = 0; c < Cols; c++) {
                int cell = certificateGrid[r][c];
                int i = WhiteIndex[r][c];
                if (i >= 0) {
                    if (cell == BULB) bulb[i] = true;
                    else if (cell != EMPTY_CELL) return null;
                } else if (cell != grid[r][c]) {
                    return null;
                }
            }
        }
        return bulb;
    }

    /// <summary>Writes a bulb placement as a certificate: the instance grid on one line, with bulbs as <see cref="BULB_SYMBOL"/>.</summary>
    internal string Format(bool[] bulb) {
        var sb = new StringBuilder("(");
        for (int r = 0; r < Rows; r++) {
            if (r > 0) sb.Append(',');
            sb.Append('(');
            for (int c = 0; c < Cols; c++) {
                if (c > 0) sb.Append(',');
                int i = WhiteIndex[r][c];
                int cell = grid[r][c];
                sb.Append(i >= 0 ? (bulb[i] ? BULB_SYMBOL : EMPTY_SYMBOL) : cell == WALL ? WALL_SYMBOL : (char)('0' + cell));
            }
            sb.Append(')');
        }
        return sb.Append(')').ToString();
    }

    // Parses a nested list such as "((_,_,#,_),(#,_,_,1))" into an int[][].
    // Whitespace between tokens is ignored; valid cells are _ (EMPTY_CELL), # (WALL), and digits 0-4 (clue value),
    // plus * (BULB) when allowBulbs is set, which is how the verifier reads a certificate with the same grammar.
    // Errors report line/column in the original input, plus row/cell for bad cell characters; `fail` turns the
    // message into the exception to throw (ProblemParseException for an instance, CertificateParseException for a certificate).
    // This custom parser was built since SPADE currently struggles with nested lists (as of 10-15-2026).
    internal static int[][] ParseGrid(string input, bool allowBulbs, Func<string, Exception> fail) {
        string what = allowBulbs ? "certificate" : "instance";
        string allowed = allowBulbs ? $"{EMPTY_SYMBOL}, {BULB_SYMBOL}, {WALL_SYMBOL}, 0-4" : $"{EMPTY_SYMBOL}, {WALL_SYMBOL}, 0-4";
        if (string.IsNullOrWhiteSpace(input)) {
            throw fail($"{what} is empty");
        }

        // The input is scanned as written (whitespace skipped between tokens) so that
        // reported positions match what the caller sees, including multi-line instances.
        int pos = 0;

        void SkipWhitespace() {
            while (pos < input.Length && char.IsWhiteSpace(input[pos])) { pos++; }
        }

        // 1-based line and column of the current position in the original input.
        string Location() {
            int line = 1;
            int lineStart = 0;
            for (int i = 0; i < pos && i < input.Length; i++) {
                if (input[i] == '\n') { line++; lineStart = i + 1; }
            }
            return $"line {line}, column {pos - lineStart + 1}";
        }

        void Expect(char c) {
            SkipWhitespace();
            if (pos >= input.Length || input[pos] != c) {
                string found = pos >= input.Length ? "end of input" : $"'{input[pos]}'";
                throw fail($"expected '{c}' but found {found} at {Location()}");
            }
            pos++;
        }

        bool TryConsume(char c) {
            SkipWhitespace();
            if (pos < input.Length && input[pos] == c) { pos++; return true; }
            return false;
        }

        var rows = new List<int[]>();
        Expect('(');
        while (true) {
            Expect('(');
            var row = new List<int>();
            while (true) {
                SkipWhitespace();
                if (pos >= input.Length) {
                    throw fail($"unexpected end of input inside row {rows.Count + 1} at {Location()}");
                }
                char c = input[pos];
                int cell = c switch {
                    EMPTY_SYMBOL => EMPTY_CELL,
                    WALL_SYMBOL => WALL,
                    >= '0' and <= '4' => c - '0',
                    BULB_SYMBOL when allowBulbs => BULB,
                    _ => throw fail($"invalid cell character '{c}' at row {rows.Count + 1}, cell {row.Count + 1} ({Location()}); allowed: {allowed}")
                };
                row.Add(cell);
                pos++;
                if (TryConsume(',')) { continue; }
                break;
            }
            Expect(')');
            if (rows.Count > 0 && row.Count != rows[0].Length) {
                throw fail($"row {rows.Count + 1} has {row.Count} cells but row 1 has {rows[0].Length}");
            }
            rows.Add(row.ToArray());
            if (TryConsume(',')) { continue; }
            break;
        }
        Expect(')');
        SkipWhitespace();
        if (pos != input.Length) {
            throw fail($"unexpected trailing characters at {Location()}");
        }
        return rows.ToArray();
    }
}
