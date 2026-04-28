using API.Interfaces;
using API.DummyClasses;
using API.Problems.NPComplete.NPC_SUDOKU.Solvers;
using API.Problems.NPComplete.NPC_SUDOKU.Verifiers;
using API.Problems.NPComplete.NPC_SUDOKU.Visualizations;
using SPADE;

namespace API.Problems.NPComplete.NPC_SUDOKU;

class SUDOKU : IProblem<SudokuSolver, SudokuVerifier, SudokuVisualization> {

    // --- Fields ---
    public string problemName {get;} = "Sudoku";
    public string problemLink {get;} = "https://en.wikipedia.org/wiki/Sudoku";
    public string formalDefinition {get;} = "Sudoku = {{(x_1, y_1, z_1), (x_2, y_2, z_2), ... (x_n, y_n, z_n)} | x_i is int 0-8, y is int 0-8, z is int 1-9}";
    public string problemDefinition {get;} = "The problem is meant to represent and solve an instance of a classic sudoku problem. Each tuple describes one of the starting hints - the position (x and y) and the value (z)";
    public string source {get;} = "TODO";
    public string sourceLink {get;} = "TODO";
    private static readonly string _defaultInstance = "{(0, 0, 1), (5, 8, 4), (5, 7, 1), (0, 1, 6), (8, 8, 9)}";
    public string defaultInstance {get;} = _defaultInstance;
    public string instance {get;set;} = string.Empty;
    public string wikiName {get;} = "Sudoku";
    public SudokuSolver defaultSolver {get;} = new SudokuSolver();
    public SudokuVerifier defaultVerifier {get;} = new SudokuVerifier();
    public SudokuVisualization defaultVisualization {get;} = new SudokuVisualization();
    public string[] contributors { get; }= { "Eric Hill" };

    private string _circuit = "";

    public string circuit
    {
        get
        {
            return _circuit;
        }
        set
        {
            _circuit = value;
        }
    }

    private List<int> _funcValues = new List<int>();

    public List<int> funcValues
    {
        get
        {
            return _funcValues;
        }
        set
        {
            _funcValues = value;
        }
    }

    // --- Methods and Constructors ---
    public SUDOKU() : this(_defaultInstance) {

    }

    public SUDOKU(string input) {
        instance = input;

        // Parser is not currently working, I wasn't sure how to get SPADE to handle a list of lists/tuples or a set of lists/tuples.

        // StringParser parser = new("{N | N is set}");
        //StringParser parser = new("{N | N subset {(x, y, z) | x is int, y is int, z is int }}");

        // parser.parse(instance);

        // UtilCollection bitslist = parser["y"];
        // SPADE.UtilCollection parsedSet = parser["N"];
        // funcValues = new List<int>();
        int[,] p1 = new int[,]
        {
            {0, 0, 0, 1, 0, 0, 2, 0, 3},
            {0, 2, 0, 0, 4, 0, 5, 0, 6},
            {0, 7, 0, 0, 0, 6, 4, 0, 0},
            {5, 0, 0, 6, 0, 0, 8, 0, 0},
            {0, 6, 0, 4, 0, 2, 0, 5, 0},
            {0, 0, 4, 0, 0, 9, 0, 0, 7},
            {0, 0, 9, 5, 0, 0, 0, 4, 0},
            {7, 0, 6, 0, 8, 0, 0, 1, 0},
            {4, 0, 3, 0, 0, 7, 0, 0, 0},
        };

        int[,] s1 = new int[,]
        {
            { 6, 4, 8, 1, 9, 5, 2, 7, 3 },
            { 3, 2, 1, 7, 4, 8, 5, 9, 6 },
            { 9, 7, 5, 3, 2, 6, 4, 8, 1 },
            { 5, 9, 2, 6, 7, 1, 8, 3, 4 },
            { 8, 6, 7, 4, 3, 2, 1, 5, 9 },
            { 1, 3, 4, 8, 5, 9, 6, 2, 7 },
            { 2, 1, 9, 5, 6, 3, 7, 4, 8 },
            { 7, 5, 6, 9, 8, 4, 3, 1, 2 },
            { 4, 8, 3, 2, 1, 7, 9, 6, 5 },
        };

        SudokuSolver solver = new SudokuSolver(p1);
        int[,] solution = solver.solve();
        Debug.Assert(solution.Cast<int>().SequenceEqual(s1.Cast<int>()), "Solver solution does not match expected output");
    }
}
