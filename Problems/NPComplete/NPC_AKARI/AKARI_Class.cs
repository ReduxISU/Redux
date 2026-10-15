using API.Interfaces;
using API.Problems.NPComplete.NPC_AKARI.Solvers;
using API.Problems.NPComplete.NPC_AKARI.Verifiers;
using API.Problems.NPComplete.NPC_AKARI.Visualizations;
using Microsoft.Net.Http.Headers;
using SPADE;

namespace API.Problems.NPComplete.NPC_AKARI;

class AKARI : IProblem<AkariSolver, AkariVerifier, AkariVisualization> {

    // --- Fields ---
    public string problemName {get;} = "Akari (Light Up Puzzle)";
    public string problemLink {get;} = "https://en.wikipedia.org/wiki/Light_Up_(puzzle)";
    public string formalDefinition {get;} = "TODO"; // Consider excluding due to lack of formal definition
    public string problemDefinition {get;} = "An Akari (Light Up) puzzle is comprised of a rectangular m x n grid containing sets of white 'unlit' cells and black 'walls'. 'Light bulbs' can be placed in white cells that illuminate their row and column, until blocked by a wall. Black wall cells may optionally include a clue in the form of integer indicating the number of expected adjacent bulbs. A puzzle instance is considered solvable if there is an arangement of 'light bulbs' that satisifes the following conditions:\n1. Every white cell has line-of-sight to a light bulb.\n2. No light bulb illuminates another bulb.\n3. Walls with clue labeling are satisfied.";
    public string inputDescription {get;} = "An array of characters or integers corresponding to each cell in the grid. White cells are represented by 'w' or 'W'. Black cells are indicated by either 'b', 'B', or a number (0-4) representing the clue.";
    public string outputDescription {get;} = "A boolean indicating if the puzzle instance is solvable.";
    public string source {get;} = "Bram Pulles. 2021. Analysis of Akari. Bachelor Thesis. Radboud University, Nijmegen, Netherlands.";
    public string sourceLink {get;} = "https://www.cs.ru.nl/bachelors-theses/2021/Bram_Pulles___1015194___Analysis_of_Akari.pdf";
    public string sourceFile {get;} = SourceFile.Path();
    private static readonly string _defaultInstance = """
    ((W,W,B,W,W,W,B),
    (W,4,W,W,1,W,B),
    (W,W,W,2,W,W,W),
    (W,B,W,W,W,B,W),
    (W,W,W,B,W,W,W),
    (B,W,B,W,W,1,W),
    (1,W,W,W,1,W,W))
    """;
    public string defaultInstance {get;} = _defaultInstance;
    // Short descriptive sentence with a concrete example. Consumed by
    // /ProblemProvider/info and by parse-error responses; this is the
    // canonical place to tell an LLM (or a human, or the GUI) what
    // shape your instance/certificate strings take.
    public string instanceFormat {get;} = "Nested list of rows, each row a parenthesized comma-separated list of cells: w/W (white), b/B (black wall), or 1-4 (clue wall). Example: ((w,w,b,w),(b,w,w,1))";
    public string certificateFormat {get;} = "TODO: describe the certificate string the verifier accepts with an embedded example, e.g. '(x1:True,x2:False,x3:True) -- one variable:Boolean pair per variable, comma-separated'";
    public string instance {get;set;} = string.Empty;
    public string wikiName {get;} = ""; // Depreciated, do not implement
    public AkariSolver defaultSolver {get;} = new AkariSolver();
    public AkariVerifier defaultVerifier {get;} = new AkariVerifier();
    public AkariVisualization defaultVisualization {get;} = new AkariVisualization();
    public string[] contributors {get;} = { "Carter Luker", "Daniel Fawson" };
    // Declared, not derived — do NOT infer this from which Problems/<Folder>/ this file
    // lives under. The folder is a filing convention only and is wrong for a dozen-plus
    // existing problems; declaring it explicitly here is the only source of truth the
    // API trusts. Leave as Unclassified (the default — this line can simply be deleted)
    // until someone has actually checked the literature/argued the class. Getting this
    // wrong is a public correctness claim the API will serve to callers.
    public ComplexityClass complexityClass {get;} = ComplexityClass.NPComplete;
    public ProblemType problemType {get;} = ProblemType.GamesAndPuzzles;

    // --- Properties ---

    public char[][] grid { get; set; } = Array.Empty<char[]>();

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

        // Puzzle Grid Version: 2D list of characters associated with each cell.
        grid = ParseGrid(stringInstance);
    }

    // Parses a nested list such as "((w,w,b,w),(b,w,w,1))" into a char[][].
    // Whitespace is ignored; valid cells are w/W/b/B and digits 1-4.
    // This custom parser was built since SPADE currently struggles with nested lists (as of 10-15-2026).
    private static char[][] ParseGrid(string input) {
        if (string.IsNullOrWhiteSpace(input)) {
            throw new ProblemParseException("Akari", input, "instance is empty");
        }

        string s = string.Concat(input.Where(c => !char.IsWhiteSpace(c)));
        int pos = 0;

        void Expect(char c) {
            if (pos >= s.Length || s[pos] != c) {
                string found = pos >= s.Length ? "end of input" : $"'{s[pos]}'";
                throw new ProblemParseException("Akari", input, $"expected '{c}' but found {found} at position {pos}");
            }
            pos++;
        }

        var rows = new List<char[]>();
        Expect('(');
        while (true) {
            Expect('(');
            var row = new List<char>();
            while (true) {
                if (pos >= s.Length) {
                    throw new ProblemParseException("Akari", input, "unexpected end of input inside a row");
                }
                char c = s[pos];
                if (c != 'w' && c != 'W' && c != 'b' && c != 'B' && (c < '0' || c > '4')) {
                    throw new ProblemParseException("Akari", input, $"invalid cell character '{c}' at position {pos}; allowed: w, W, b, B, 1-4");
                }
                row.Add(c);
                pos++;
                if (pos < s.Length && s[pos] == ',') { pos++; continue; }
                break;
            }
            Expect(')');
            if (rows.Count > 0 && row.Count != rows[0].Length) {
                throw new ProblemParseException("Akari", input, $"row {rows.Count + 1} has {row.Count} cells but row 1 has {rows[0].Length}");
            }
            rows.Add(row.ToArray());
            if (pos < s.Length && s[pos] == ',') { pos++; continue; }
            break;
        }
        Expect(')');
        if (pos != s.Length) {
            throw new ProblemParseException("Akari", input, $"unexpected trailing characters at position {pos}");
        }
        return rows.ToArray();
    }
}
