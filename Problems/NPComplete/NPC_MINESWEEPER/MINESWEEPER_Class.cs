
using API.Interfaces;
using API.Problems.NPComplete.NPC_MINESWEEPER.Solvers;
using API.Problems.NPComplete.NPC_MINESWEEPER.Verifiers;
using API.Problems.NPComplete.NPC_MINESWEEPER.Visualizations;

namespace API.Problems.NPComplete.NPC_MINESWEEPER;

class MINESWEEPER : IProblem<MinesweeperSolver, MinesweeperVerifier, MinesweeperDefaultVisualization> {

    public string problemName { get; } = "Minesweeper Consistency";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Minesweeper_(video_game)#Computational_complexity";

    public string formalDefinition { get; } =
        "Given a finite rectangular grid with revealed clues and unknown cells, " +
        "determine whether there exists an assignment of mines to unknown cells " +
        "such that every numbered clue equals the number of mines in its " +
        "eight neighboring cells.";

    public string problemDefinition { get; } =
        "Minesweeper Consistency asks whether a partially revealed Minesweeper " +
        "board admits at least one mine placement consistent with all clues.";

    public string inputDescription { get; } =
        "A rectangular grid containing ? for unknown cells and digits 0-8 for revealed clues.";

    public string outputDescription { get; } =
        "A complete binary grid where 1 indicates a mine and 0 indicates " +
        "a safe cell, or {} if no valid placement exists.";

    public string source { get; } =
        "Kaye, Richard. 2000. Minesweeper is NP-complete. " +
        "The Mathematical Intelligencer 22(2): 9-15.";

    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://doi.org/10.1007/BF03025367";

    public const string InstanceGrammar =
        "Rectangular grid of ? and digits 0-8, comma-separated cells, semicolon-separated rows";

    private const string DefaultBoard = "1,?,1;?,?,?;1,?,1";

    public string defaultInstance { get; } = DefaultBoard;

    public string instanceFormat { get; } =
        $"Format: {InstanceGrammar}. Example: {DefaultBoard}";

    public string certificateFormat { get; } =
        $"Format: {MinesweeperVerifier.CertificateGrammar}. " +
        $"Example: {MinesweeperVerifier.CertificateExample}";

    public string instance { get; set; } = string.Empty;

    public string wikiName { get; } = "Minesweeper (video game)";

    public MinesweeperSolver defaultSolver { get; } = new();
    public MinesweeperVerifier defaultVerifier { get; } = new();
    public MinesweeperDefaultVisualization defaultVisualization { get; } = new();

    public ComplexityClass complexityClass { get; } = ComplexityClass.NPComplete;
    public ProblemType problemType { get; } = ProblemType.GamesAndPuzzles;
    public ProblemVariant problemVariant { get; } = ProblemVariant.Decision;

    public string[] contributors { get; } = { "Prashant Pant" };

    public int Rows => grid.Length;
    public int Columns => grid[0].Length;

    // -1 = unknown, 0..8 = revealed clue
    public int[][] grid { get; }

    public MINESWEEPER() : this(DefaultBoard) { }

    public MINESWEEPER(string input) {
        instance = input;

        var rows = input.ReplaceLineEndings(string.Empty)
            .Split(';', StringSplitOptions.TrimEntries);

        if (rows.Length == 0 || rows.Any(r => r.Length == 0))
            throw new ProblemParseException(problemName, input, "Board rows cannot be empty.");

        grid = new int[rows.Length][];
        int? width = null;

        for (int r = 0; r < rows.Length; r++) {
            var cells = rows[r].Split(',');

            if (width == null)
                width = cells.Length;

            if (cells.Length != width)
                throw new ProblemParseException(problemName, input, "All rows must have equal width.");

            grid[r] = new int[cells.Length];

            for (int c = 0; c < cells.Length; c++) {
                string value = cells[c].Trim();

                if (value == "?") {
                    grid[r][c] = -1;
                } else if (value.Length == 1 && value[0] >= '0' && value[0] <= '8') {
                    grid[r][c] = value[0] - '0';
                } else {
                    throw new ProblemParseException(
                        problemName, input,
                        $"Invalid cell at row {r + 1}, column {c + 1}: '{value}'.");
                }
            }
        }
    }
}
