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
    public string formalDefinition { get; } =       //"Sudoku = {{(x_1, y_1, z_1), (x_2, y_2, z_2), ... (x_n, y_n, z_n)} | x_i is int 0-8, y is int 0-8, z is int 1-9}"; //TODO: make this true to the actual format of the problem instance once we decide on that format
        "SUDOKU = {⟨C,n⟩ | n is a perfect square, C ⊆ {0,…,n−1} × {0,…,n−1} × {1,…,n}, " +
        "and there exists an n×n grid H satisfying: " +
        "(1) ∀(x,y,z) ∈ C, H[x,y] = z; " +
        "(2) each row contains each value 1…n exactly once; " +
        "(3) each column contains each value 1…n exactly once; " +
        "(4) each √n×√n block contains each value 1…n exactly once }"; //TODO: maybe make this more clear/only be about how to write the problem instance in REDUX

    public string problemDefinition {get;} = "Sudoku is a logic-based, combinatorial number-placement puzzle where the goal is to fill a 9x9 grid with digits so that each column, row, and 3x3 box contains all of the digits from 1 to 9."; //"The problem is meant to represent and solve an instance of a classic sudoku problem. Each tuple describes one of the starting hints - the position (x and y) and the value (z)";
    public string source {get;} = "TODO";
    public string sourceLink {get;} = "https://medium.com/@davidcarmel/solving-sudoku-by-heuristic-search-b0c2b2c5346e and https://www.geeksforgeeks.org/dsa/sudoku-backtracking-7/"; //TODO: Do we need both of these sources? Maybe we can find a more academic source???
    private static readonly string _defaultInstance = "0,0,0,1,0,0,2,0,3;0,2,0,0,4,0,5,0,6;0,7,0,0,0,6,4,0,0;5,0,0,6,0,0,8,0,0;0,6,0,4,0,2,0,5,0;0,0,4,0,0,9,0,0,7;0,0,9,5,0,0,0,4,0;7,0,6,0,8,0,0,1,0;4,0,3,0,0,7,0,0,0";
    public string defaultInstance {get;} = _defaultInstance;
    public string instance {get;set;} = string.Empty;
    public string wikiName {get;} = "Sudoku";
    public SudokuSolver defaultSolver {get;} = new SudokuSolver();
    public SudokuVerifier defaultVerifier {get;} = new SudokuVerifier();
    public SudokuVisualization defaultVisualization {get;} = new SudokuVisualization();
    public string[] contributors { get; }= { "Eric Hill, Carter Luker, Collin Kress, & Daniel Fawson" }; //TODO: keep Eric? I think so but not sure 

    public int[][] grid { get; set; }

    // --- Methods and Constructors ---
    public SUDOKU() : this(_defaultInstance) {

    }

    public SUDOKU(string input) {
        // Parser is not currently working, I wasn't sure how to get SPADE to handle a list of lists/tuples or a set of lists/tuples.

        // StringParser parser = new("{N | N is set}");
        //StringParser parser = new("{N | N subset {(x, y, z) | x is int, y is int, z is int }}");

        // parser.parse(instance);

        // UtilCollection bitslist = parser["y"];
        // SPADE.UtilCollection parsedSet = parser["N"];
        // funcValues = new List<int>();

        // Parses the input string into a 2D array representing the Sudoku grid
        instance = input;

        var rows = input.Split(';', StringSplitOptions.RemoveEmptyEntries);

        grid = new int[rows.Length][];

        for (int i = 0; i < rows.Length; i++)
        {
            var nums = rows[i].Split(',', StringSplitOptions.RemoveEmptyEntries);
            grid[i] = Array.ConvertAll(nums, int.Parse);
        }


        /*int[,] p1 = new int[,]
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
        //Needs to be in comma seperated string and semicolon seperated rows for the parser to work
        //It should look like this: "0,0,0,1,0,0,2,0,3; 0,2,0,0,4,0,5,0,6; 0,7,0,0,0,6,4,0,0; 5,0,0,6,0,0,8,0,0; 0,6,0,4,0,2,0,5,0; 0,0,4,0,0,9,0,0,7; 0,0,9,5,0,0,0,4,0; 7,0,6,0,8,0,0,1,0; 4,0,3,0,0 ,7 ,  0 ,  0 ,  0"

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
        //This should look like this: "6,4,8,1,9,5,2,7,3; 3,2,1,7,4,8,5,9,6; 9,7,5,3,2,6,4,8,1; 5,9,2,6,7,1,8,3,4; 8,6,7,4 ,3 ,2 ,1 ,5 ,9; 1 ,3 ,4 ,8 ,5 ,9 ,6 ,2 ,7; 2 ,1 ,9 ,5 ,6 ,3 ,7 ,4 ,8; 7 ,5 ,6 ,9 ,8 ,4 ,3 ,1 ,2; 4 ,8 ,3 ,2 ,1 ,7 ,9 ,6 ,5"

        SudokuSolver solver = new SudokuSolver(p1);
        int[,] solution = solver.solve();
        Debug.Assert(solution.Cast<int>().SequenceEqual(s1.Cast<int>()), "Solver solution does not match expected output");*/
    }
}
