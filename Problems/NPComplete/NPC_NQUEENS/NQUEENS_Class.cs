using API.Interfaces;
using API.DummyClasses;
using API.Problems.NPComplete.NPC_NQUEENS.Solvers;
using API.Problems.NPComplete.NPC_NQUEENS.Verifiers;

namespace API.Problems.NPComplete.NPC_NQUEENS;

class NQUEENS : IProblem<NQueensBacktracking, NQueensVerifier, DummyVisualization> {

    // --- Fields ---
    public string problemName { get; } = "N-Queens";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Eight_queens_puzzle";

    public string formalDefinition { get; } =
        "NQUEENS = {<n> | there exists a placement of n queens on an n x n chessboard such that no two queens attack each other}";

    public string problemDefinition { get; } =
        "Given an integer n, determine whether n queens can be placed on an n x n chessboard so that no two queens share a row, column, or diagonal.";

    public string source { get; } = "Classic combinatorial problem.";
    public string sourceLink { get; } = "https://en.wikipedia.org/wiki/Eight_queens_puzzle";

    public string[] contributors { get; } = { "Cole Campbell", "Luis Hernandez", "Ethan Wilks" };

    public string instance { get; set; } = string.Empty;

    private static readonly string _defaultInstance = "4";
    public string defaultInstance { get; } = _defaultInstance;

    public string wikiName { get; } = "Eight queens puzzle";

    // Problem variable
    public int n { get; set; }

    public NQueensBacktracking defaultSolver { get; } = new NQueensBacktracking();
    public NQueensVerifier defaultVerifier { get; } = new NQueensVerifier();
    public DummyVisualization defaultVisualization { get; } = new DummyVisualization();

    // --- Constructors ---
    public NQUEENS() : this(_defaultInstance) { }

    public NQUEENS(string input) {
        instance = input;
        n = int.Parse(input.Trim());
    }
}
