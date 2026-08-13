using API.Interfaces;
using API.DummyClasses;
using API.Problems.P.P_NQUEENS.Solvers;
using API.Problems.P.P_NQUEENS.Verifiers;

namespace API.Problems.P.P_NQUEENS;

class NQUEENS : IProblem<NQueensConstructive, NQueensVerifier, DummyVisualization> {

    // --- Fields ---
    public string problemName { get; } = "N-Queens";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Eight_queens_puzzle";

    public string formalDefinition { get; } =
        "NQUEENS = {<n> | there exists a placement of n queens on an n x n chessboard such that no two queens attack each other}";

    public string problemDefinition { get; } =
        "Given an integer n, determine whether n queens can be placed on an n x n chessboard so that no two queens share a row, column, or diagonal. "
        + "Although finding a placement is often taught alongside NP-complete search problems, the decision version is trivial (a solution exists for every n except 2 and 3) "
        + "and an explicit placement can be constructed in O(n) time, so N-Queens lies in P.";

    public string source { get; } = "Classic combinatorial problem.";
    public string sourceLink { get; } = "https://en.wikipedia.org/wiki/Eight_queens_puzzle";
    // Declared, not derived. This class takes only n (no partial/pre-placed board), so
    // it models PLACEMENT ("does some valid arrangement exist"), not the NP-complete
    // COMPLETION variant. Placement has known constructive polynomial solutions for
    // every n >= 4. NQueensBacktracking's O(N!) is a correct but needlessly exponential
    // solver for an easy problem, not evidence the problem itself is hard. See
    // redux-tests/Metadata/ComplexityClass_Tests.cs.
    public ComplexityClass complexityClass { get; } = ComplexityClass.P;

    public string[] contributors { get; } = { "Cole Campbell", "Luis Hernandez", "Ethan Wilks" };

    public string instance { get; set; } = string.Empty;

    private static readonly string _defaultInstance = "4";
    public string defaultInstance { get; } = _defaultInstance;

    public string instanceFormat { get; } =
        "A single non-negative integer n giving the board size / number of queens — e.g. \"8\".";

    public string certificateFormat { get; } =
        "Set of (row,col) queen positions, 0-indexed, one per row and column: {(row,col),...} — e.g. {(0,1),(1,3),(2,0),(3,2)}.";

    public string wikiName { get; } = "Eight queens puzzle";

    // Problem variable
    public int n { get; set; }

    public NQueensConstructive defaultSolver { get; } = new NQueensConstructive();
    public NQueensVerifier defaultVerifier { get; } = new NQueensVerifier();
    public DummyVisualization defaultVisualization { get; } = new DummyVisualization();

    // --- Constructors ---
    public NQUEENS() : this(_defaultInstance) { }

    public NQUEENS(string input) {
        instance = input;
        n = int.Parse(input.Trim());
    }
}
