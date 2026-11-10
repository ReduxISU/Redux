using API.Interfaces;
using API.Problems.NPComplete.NPC_AKARI.Solvers;
using API.Problems.NPComplete.NPC_AKARI.Verifiers;
using API.Problems.NPComplete.NPC_AKARI.Visualizations;
using Microsoft.Net.Http.Headers;
using SPADE;

namespace API.Problems.NPComplete.NPC_AKARI;

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
    public string instanceFormat { get; } = $"Nested list of rows, each row a parenthesized comma-separated list of cells: _ (white), # (black wall), or 0-4 (clue wall). Example: {_defaultInstance}";
    public string certificateFormat { get; } = $"The instance grid with every light bulb marked '*' in place of a white cell '_'; walls and clues are copied unchanged, and \"{{}}\" means no solution. Example (default instance): {AkariVerifier.CertificateExample}";
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

    public int[][] grid { get; } = Array.Empty<int[]>();

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
    }

    // Parses a nested list such as "((_,_,#,_),(#,_,_,1))" into an int[][].
    // Whitespace between tokens is ignored; valid cells are _ (EMPTY_CELL), # (WALL), and digits 0-4 (clue value),
    // plus * (BULB) when allowBulbs is set, which is how the verifier reads a certificate with the same grammar.
    // Errors report line/column in the original input, plus row/cell for bad cell characters; `fail` turns the
    // message into the exception to throw (ProblemParseException for an instance, CertificateParseException for a certificate).
    // This custom parser was built since SPADE currently struggles with nested lists (as of 10-15-2026).
    internal static int[][] ParseGrid(string input, bool allowBulbs, Func<string, Exception> fail) {
        string what = allowBulbs ? "certificate" : "instance";
        string allowed = allowBulbs ? "_, *, #, 0-4" : "_, #, 0-4";
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
                    '_' => EMPTY_CELL,
                    '#' => WALL,
                    >= '0' and <= '4' => c - '0',
                    '*' when allowBulbs => BULB,
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
