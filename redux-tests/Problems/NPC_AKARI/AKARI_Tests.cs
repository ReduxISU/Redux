using Xunit;
using API.Interfaces;
using API.Problems.NPComplete.NPC_AKARI;
using API.Problems.NPComplete.NPC_AKARI.Solvers;
using API.Problems.NPComplete.NPC_AKARI.Verifiers;

namespace redux_tests;
#pragma warning disable CS1591

// Tests for AKARI: the problem class (metadata, cell encodings, instance parser), the verifier, and the three
// solvers (brute force, backtracking, constraint propagation). The visualization is not covered here.
public class AKARI_Tests {

    private const int E = AKARI.EMPTY_CELL;
    private const int W = AKARI.WALL;

    #region Metadata

    [Fact]
    public void AKARI_Metadata_IsDeclared() {
        var problem = new AKARI();
        Assert.Equal("Akari (Light Up Puzzle)", problem.problemName);
        Assert.Equal(ComplexityClass.NPComplete, problem.complexityClass);
        Assert.Equal(ProblemType.GamesAndPuzzles, problem.problemType);
        Assert.Contains("Carter Luker", problem.contributors);
        Assert.Contains("Danny Fawson", problem.contributors);
        Assert.NotNull(problem.defaultSolver);
        Assert.NotNull(problem.defaultVerifier);
        Assert.NotNull(problem.defaultVisualization);
    }

    [Fact]
    public void AKARI_Instance_Format_Described() {
        var problem = new AKARI();
        Assert.NotEmpty(problem.instanceFormat);
        Assert.Contains("_ (white)", problem.instanceFormat);
        Assert.Contains("# (black wall)", problem.instanceFormat);
        Assert.Contains("0-4 (clue wall)", problem.instanceFormat);
        Assert.Contains(problem.defaultInstance, problem.instanceFormat);
    }

    [Fact]
    public void AKARI_Descriptions_UseCurrentCellSymbols() {
        var problem = new AKARI();
        Assert.Contains("'_'", problem.inputDescription);
        Assert.Contains("'#'", problem.inputDescription);
        Assert.DoesNotContain("'w'", problem.inputDescription);
        Assert.DoesNotContain("'b'", problem.inputDescription);
    }

    #endregion

    #region Cell Encodings

    [Fact]
    public void AKARI_CellEncodings_AreDistinctAndOutsideClueRange() {
        Assert.NotEqual(AKARI.EMPTY_CELL, AKARI.WALL);
        Assert.True(AKARI.EMPTY_CELL < 0 || AKARI.EMPTY_CELL > 4);
        Assert.True(AKARI.WALL < 0 || AKARI.WALL > 4);
    }

    [Fact]
    public void AKARI_Grid_HasNoSetter() {
        var property = typeof(AKARI).GetProperty(nameof(AKARI.grid));
        Assert.NotNull(property);
        Assert.False(property!.CanWrite);
    }

    #endregion

    #region Default Instance

    [Fact]
    public void AKARI_DefaultConstructor_UsesDefaultInstance() {
        var problem = new AKARI();
        Assert.Equal(problem.defaultInstance, problem.instance);
    }

    [Fact]
    public void AKARI_DefaultInstance_ParsesToExpectedGrid() {
        var problem = new AKARI();
        int[][] expected = {
            new[] { E, E, W, E, E, E, W },
            new[] { E, 4, E, E, 1, E, W },
            new[] { E, E, E, 2, E, E, E },
            new[] { E, W, E, E, E, W, E },
            new[] { E, E, E, W, E, E, E },
            new[] { W, E, W, E, E, 1, E },
            new[] { 1, E, E, E, 1, E, E },
        };
        Assert.Equal(expected, problem.grid);
    }

    #endregion

    #region Parser - Valid Instances

    [Fact]
    public void AKARI_Parser_StoresOriginalInstanceString() {
        const string input = "((_, #),\n (0, 4))";
        var problem = new AKARI(input);
        Assert.Equal(input, problem.instance);
    }

    [Theory]
    [InlineData('_', AKARI.EMPTY_CELL)]
    [InlineData('#', AKARI.WALL)]
    [InlineData('0', 0)]
    [InlineData('1', 1)]
    [InlineData('2', 2)]
    [InlineData('3', 3)]
    [InlineData('4', 4)]
    public void AKARI_Parser_EncodesEachCellSymbol(char symbol, int expected) {
        var problem = new AKARI($"(({symbol}))");
        Assert.Single(problem.grid);
        Assert.Equal(new[] { expected }, problem.grid[0]);
    }

    [Fact]
    public void AKARI_Parser_AcceptsNonSquareGrid() {
        var problem = new AKARI("((_,#,1),(2,_,_))");
        Assert.Equal(2, problem.grid.Length);
        Assert.All(problem.grid, row => Assert.Equal(3, row.Length));
        Assert.Equal(new[] { E, W, 1 }, problem.grid[0]);
        Assert.Equal(new[] { 2, E, E }, problem.grid[1]);
    }

    [Theory]
    [InlineData("((_,#),(1,_))")]
    [InlineData("  ( ( _ , # ) , ( 1 , _ ) )  ")]
    [InlineData("((_,#),\n(1,_))")]
    [InlineData("((_,#),\r\n(1,_))\r\n")]
    [InlineData("\t((_,\t#),\n\t(1,\t_))")]
    public void AKARI_Parser_IgnoresWhitespaceBetweenTokens(string input) {
        var problem = new AKARI(input);
        Assert.Equal(new[] { new[] { E, W }, new[] { 1, E } }, problem.grid);
    }

    #endregion

    #region Parser - Invalid Instances

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t")]
    public void AKARI_Parser_Rejects_EmptyInstance(string input) {
        var ex = Assert.Throws<ProblemParseException>(() => new AKARI(input));
        Assert.Equal("instance is empty", ex.Message);
    }

    [Fact]
    public void AKARI_Parser_Rejects_NullInstance() {
        Assert.Throws<ProblemParseException>(() => new AKARI(null!));
    }

    [Theory]
    [InlineData("((w))")] // legacy white symbol
    [InlineData("((W))")]
    [InlineData("((b))")] // legacy black symbol
    [InlineData("((B))")]
    [InlineData("((5))")] // clue out of range
    [InlineData("((9))")]
    [InlineData("((-1))")]
    [InlineData("((x))")]
    public void AKARI_Parser_Rejects_InvalidCellCharacter(string input) {
        var ex = Assert.Throws<ProblemParseException>(() => new AKARI(input));
        Assert.Contains("invalid cell character", ex.Message);
        Assert.Contains("allowed: _, #, 0-4", ex.Message);
    }

    [Theory]
    [InlineData("_,#")]           // no parentheses
    [InlineData("(_,#)")]         // missing outer list
    [InlineData("((_,#)")]        // unclosed outer list
    [InlineData("((_,#),(1,_)")]  // unclosed outer list after second row
    [InlineData("((_,#),)")]      // trailing comma after rows
    [InlineData("((_,))")]        // trailing comma inside row
    [InlineData("(())")]          // empty row
    [InlineData("()")]            // no rows
    [InlineData("((_ #))")]       // missing comma between cells
    [InlineData("((_)(#))")]      // missing comma between rows
    [InlineData("((14))")]        // multi-digit clue
    [InlineData("((_))x")]        // trailing characters
    [InlineData("((_))((#))")]    // second top-level list
    public void AKARI_Parser_Rejects_MalformedStructure(string input) {
        Assert.Throws<ProblemParseException>(() => new AKARI(input));
    }

    [Theory]
    [InlineData("((_,#),(1))", "row 2 has 1 cells but row 1 has 2")]
    [InlineData("((_),(1,_))", "row 2 has 2 cells but row 1 has 1")]
    [InlineData("((_,_),(_,_),(_))", "row 3 has 1 cells but row 1 has 2")]
    public void AKARI_Parser_Rejects_RaggedRows(string input, string expectedMessage) {
        var ex = Assert.Throws<ProblemParseException>(() => new AKARI(input));
        Assert.Equal(expectedMessage, ex.Message);
    }

    [Fact]
    public void AKARI_Parser_Exception_CarriesProblemNameAndInput() {
        const string input = "((x))";
        var ex = Assert.Throws<ProblemParseException>(() => new AKARI(input));
        Assert.Equal("Akari", ex.ProblemName);
        Assert.Equal(input, ex.Received);
    }

    #endregion

    #region Parser - Error Locations

    // Positions are reported against the original input (whitespace included), not a stripped copy.

    [Fact]
    public void AKARI_Parser_InvalidCell_ReportsRowCellLineAndColumn_SingleLine() {
        var ex = Assert.Throws<ProblemParseException>(() => new AKARI("((_, _, x))"));
        Assert.Equal("invalid cell character 'x' at row 1, cell 3 (line 1, column 9); allowed: _, #, 0-4", ex.Message);
    }

    [Fact]
    public void AKARI_Parser_InvalidCell_ReportsRowCellLineAndColumn_MultiLine() {
        var ex = Assert.Throws<ProblemParseException>(() => new AKARI("((_,_),\n(_,x))"));
        Assert.Equal("invalid cell character 'x' at row 2, cell 2 (line 2, column 4); allowed: _, #, 0-4", ex.Message);
    }

    [Fact]
    public void AKARI_Parser_InvalidCell_InIndentedDefaultStyleInstance_ReportsVisibleColumn() {
        // Mirrors the multi-line, indented layout of defaultInstance.
        string input = "((_,_,#),\n    (_,4,b))";
        var ex = Assert.Throws<ProblemParseException>(() => new AKARI(input));
        Assert.Equal("invalid cell character 'b' at row 2, cell 3 (line 2, column 10); allowed: _, #, 0-4", ex.Message);
    }

    [Fact]
    public void AKARI_Parser_MissingOpeningParen_ReportsLocation() {
        var ex = Assert.Throws<ProblemParseException>(() => new AKARI("(_,#)"));
        Assert.Equal("expected '(' but found '_' at line 1, column 2", ex.Message);
    }

    [Fact]
    public void AKARI_Parser_UnclosedList_ReportsEndOfInput() {
        var ex = Assert.Throws<ProblemParseException>(() => new AKARI("((_,_)"));
        Assert.Equal("expected ')' but found end of input at line 1, column 7", ex.Message);
    }

    [Fact]
    public void AKARI_Parser_EndOfInputInsideRow_ReportsRowAndLocation() {
        var ex = Assert.Throws<ProblemParseException>(() => new AKARI("((_,_),\n(_,"));
        Assert.Equal("unexpected end of input inside row 2 at line 2, column 4", ex.Message);
    }

    [Fact]
    public void AKARI_Parser_TrailingCharacters_ReportsLocation() {
        var ex = Assert.Throws<ProblemParseException>(() => new AKARI("((_))\n  x"));
        Assert.Equal("unexpected trailing characters at line 2, column 3", ex.Message);
    }

    [Fact]
    public void AKARI_Parser_AcceptsTrailingWhitespace() {
        var problem = new AKARI("((_))  \n\t ");
        Assert.Equal(new[] { new[] { E } }, problem.grid);
    }

    #endregion

    #region Certificate Format

    [Fact]
    public void AKARI_CertificateFormat_DescribesBulbsAndQuotesExample() {
        var problem = new AKARI();
        Assert.Contains("'*'", problem.certificateFormat);
        Assert.Contains("\"{}\"", problem.certificateFormat);
        Assert.Contains(AkariVerifier.CertificateExample, problem.certificateFormat);
    }

    [Fact]
    public void AKARI_DefaultSolver_IsConstraintPropagation() {
        Assert.IsType<AkariConstraintPropagation>(new AKARI().defaultSolver);
    }

    #endregion

    #region Verifier - Accepts

    [Theory]
    [InlineData("((_))", "((*))")]                                         // smallest instance: one white cell
    [InlineData("((#))", "((#))")]                                         // no white cells, no clues: no bulbs needed
    [InlineData("((0))", "((0))")]                                         // a 0 clue with no neighbours
    [InlineData("((_,_))", "((*,_))")]                                     // one bulb lights its whole row segment
    [InlineData("((_,_))", "((_,*))")]                                     // ...from either end
    [InlineData("((_,2,_))", "((*,2,*))")]                                 // clue satisfied by bulbs on both sides
    [InlineData("((_,_),(_,_))", "((*,_),(_,*))")]                         // diagonal: lights both rows and columns
    [InlineData("((_,_,_),(_,4,_),(_,_,_))", "((_,*,_),(*,4,*),(_,*,_))")] // a 4 clue; the corners are lit across
    [InlineData("((1,_,1))", "((1,*,1))")]                                 // one bulb serving two clues
    public void AKARI_Verifier_Accepts_Valid_Certificate(string instance, string certificate) {
        Assert.True(new AkariVerifier().verify(new AKARI(instance), certificate));
    }

    [Fact]
    public void AKARI_Verifier_Accepts_CertificateExample_On_DefaultInstance() {
        Assert.True(new AkariVerifier().verify(new AKARI(), AkariVerifier.CertificateExample));
    }

    [Fact]
    public void AKARI_Verifier_Accepts_Certificate_With_Whitespace_And_Newlines() {
        Assert.True(new AkariVerifier().verify(new AKARI("((_,2,_))"), "  (\n  ( * , 2 , * )\n)  "));
    }

    #endregion

    #region Verifier - Rejects

    [Theory]
    [InlineData("((_,_))", "((_,_))")]             // no bulbs: cells stay dark
    [InlineData("((_,#,_))", "((*,#,_))")]         // the wall blocks the light, so the right cell is dark
    [InlineData("((_,_))", "((*,*))")]             // two bulbs in one row segment see each other
    [InlineData("((_),(_))", "((*),(*))")]         // two bulbs in one column segment see each other
    [InlineData("((_,2,_))", "((*,2,_))")]         // clue 2 has only one bulb (and a dark cell)
    [InlineData("((_,1,_))", "((*,1,*))")]         // clue 1 has two bulbs
    [InlineData("((0,_))", "((0,*))")]             // a 0 clue with an adjacent bulb
    [InlineData("((_,_),(_,_))", "((*,_),(_,_))")] // one bulb leaves the opposite corner dark
    public void AKARI_Verifier_Rejects_Rule_Violations(string instance, string certificate) {
        Assert.False(new AkariVerifier().verify(new AKARI(instance), certificate));
    }

    [Theory]
    [InlineData("((_,#))", "((*,_))")]             // a wall replaced by a white cell
    [InlineData("((_,#))", "((*,*))")]             // a bulb on a wall
    [InlineData("((_,1),(_,_))", "((*,2),(_,*))")] // a clue's number changed
    [InlineData("((_,1),(_,_))", "((*,#),(_,*))")] // a clue replaced by a plain wall
    [InlineData("((_,_))", "((*))")]               // fewer columns than the instance
    [InlineData("((_,_))", "((*,_),(_,_))")]       // more rows than the instance
    [InlineData("((_))", "((*,_))")]               // more columns than the instance
    public void AKARI_Verifier_Rejects_Certificate_Not_Matching_Instance(string instance, string certificate) {
        Assert.False(new AkariVerifier().verify(new AKARI(instance), certificate));
    }

    [Fact]
    public void AKARI_Verifier_Rejects_NoSolution_Answer() {
        // "{}" is the solvers' "no solution" answer: well-formed, so false rather than a parse error.
        Assert.False(new AkariVerifier().verify(new AKARI(), "{}"));
        Assert.False(new AkariVerifier().verify(new AKARI("((1))"), " {} "));
    }

    [Fact]
    public void AKARI_Verifier_Uses_The_Instance() {
        // The same certificate is right for one instance and wrong for another.
        Assert.True(new AkariVerifier().verify(new AKARI("((_,2,_))"), "((*,2,*))"));
        Assert.False(new AkariVerifier().verify(new AKARI("((_,1,_))"), "((*,2,*))"));
    }

    [Fact]
    public void AKARI_Verifier_StringInterface_Accepts_And_Rejects() {
        // Through the string-based IVerifier entry point the API uses.
        IVerifier verifier = new AkariVerifier();
        Assert.True(verifier.verify("((_,2,_))", "((*,2,*))"));
        Assert.False(verifier.verify("((_,2,_))", "((*,2,_))"));
    }

    #endregion

    #region Verifier - Malformed

    [Theory]
    [InlineData("")]                  // empty
    [InlineData("   ")]               // whitespace only
    [InlineData("((x))")]             // unknown cell symbol
    [InlineData("((5))")]             // clue out of range
    [InlineData("((_,*)")]            // unclosed outer list
    [InlineData("(_,*)")]             // missing outer list
    [InlineData("((_,*),(_))")]       // ragged rows
    [InlineData("((_ *))")]           // missing comma
    [InlineData("{a,b}")]             // wrong shape
    [InlineData("{{{")]
    [InlineData("not a certificate")]
    [InlineData("((*))extra")]        // trailing characters
    public void AKARI_Verifier_Throws_On_Malformed_Certificate(string certificate) {
        Assert.Throws<CertificateParseException>(() => new AkariVerifier().verify(new AKARI(), certificate));
    }

    [Fact]
    public void AKARI_Verifier_Malformed_Certificate_Reports_Location_And_Allowed_Symbols() {
        var ex = Assert.Throws<CertificateParseException>(() => new AkariVerifier().verify(new AKARI("((_,_))"), "((*,x))"));
        Assert.Contains("invalid cell character 'x' at row 1, cell 2", ex.Message);
        Assert.Contains("allowed: _, *, #, 0-4", ex.Message);
    }

    [Fact]
    public void AKARI_Verifier_Empty_Certificate_Says_Certificate_Is_Empty() {
        var ex = Assert.Throws<CertificateParseException>(() => new AkariVerifier().verify(new AKARI(), ""));
        Assert.Contains("certificate is empty", ex.Message);
    }

    [Fact]
    public void AKARI_Instance_Parser_Still_Rejects_Bulb_Symbol() {
        // '*' is a certificate symbol only; an instance cannot contain bulbs.
        var ex = Assert.Throws<ProblemParseException>(() => new AKARI("((*))"));
        Assert.Contains("allowed: _, #, 0-4", ex.Message);
    }

    #endregion

    #region Solvers

    // Every test below runs against each solver named here.
    public static TheoryData<string> AllSolvers => new() { nameof(AkariBruteForce), nameof(AkariBacktracking), nameof(AkariConstraintPropagation) };

    // The two that can finish the default instance (35 white cells: 2^35 placements for brute force) and larger grids.
    public static TheoryData<string> SearchSolvers => new() { nameof(AkariBacktracking), nameof(AkariConstraintPropagation) };

    private static ISolver<AKARI> SolverNamed(string name) => name switch {
        nameof(AkariBruteForce) => new AkariBruteForce(),
        nameof(AkariBacktracking) => new AkariBacktracking(),
        nameof(AkariConstraintPropagation) => new AkariConstraintPropagation(),
        _ => throw new ArgumentException(name),
    };

    private static string Solve(string solver, string instance) => SolverNamed(solver).solve(new AKARI(instance));

    // Small solvable instances, each with its unique solution (worked out by hand).
    public static TheoryData<string, string, string> UniqueSolutions {
        get {
            var cases = new (string Instance, string Solution)[] {
                ("((_))", "((*))"),                                         // smallest instance
                ("((#))", "((#))"),                                         // no white cells
                ("((0))", "((0))"),                                         // a lone 0 clue
                ("((_,2,_))", "((*,2,*))"),
                ("((_,#,_),(#,_,#),(_,#,_))", "((*,#,*),(#,*,#),(*,#,*))"), // isolated cells each need their own bulb
                ("((0,_,_))", "((0,_,*))"),                                 // only the LAST cell can hold the bulb
                ("((_,_,0))", "((*,_,0))"),                                 // only the FIRST cell can hold the bulb
                ("((_,_,_),(_,4,_),(_,_,_))", "((_,*,_),(*,4,*),(_,*,_))"),
                ("((1,_,1))", "((1,*,1))"),                                 // one bulb serving two clues
                ("((2,_),(_,_))", "((2,*),(*,_))"),
            };
            var data = new TheoryData<string, string, string>();
            foreach (string solver in AllSolvers)
                foreach (var (instance, solution) in cases)
                    data.Add(solver, instance, solution);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(UniqueSolutions))]
    public void AKARI_Solver_Finds_Unique_Solution(string solver, string instance, string expected) {
        string solution = Solve(solver, instance);
        Assert.Equal(expected, solution);
        Assert.True(new AkariVerifier().verify(new AKARI(instance), solution));
    }

    // Small instances with several solutions: any answer is fine as long as the verifier accepts it.
    public static TheoryData<string, string> SeveralSolutions {
        get {
            var instances = new[] {
                "((_,_))",
                "((_,_),(_,_))",
                "((_,_,_),(_,0,_),(_,_,_))",
                "((_,_,#,_),(_,1,_,_),(#,_,_,2),(_,_,_,_))",
            };
            var data = new TheoryData<string, string>();
            foreach (string solver in AllSolvers)
                foreach (string instance in instances)
                    data.Add(solver, instance);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(SeveralSolutions))]
    public void AKARI_Solver_ProducesVerifiableCertificate(string solver, string instance) {
        string solution = Solve(solver, instance);
        Assert.True(new AkariVerifier().verify(new AKARI(instance), solution), $"{solver} returned {solution} for {instance}");
    }

    // Instances with no solution, each for a different reason.
    public static TheoryData<string, string> Unsolvable {
        get {
            var instances = new[] {
                "((1))",             // a clue with no neighbours to hold its bulb
                "((_,3,_))",         // a 3 clue with only two neighbours
                "((_,1,_))",         // each side needs its own bulb, but the clue allows only one
                "((2,_,2),(_,_,_))", // the clues force bulbs at both ends of row 2, which see each other
                "((0,_,0),(_,0,_))", // every white cell is next to a 0 clue, so none can be lit
            };
            var data = new TheoryData<string, string>();
            foreach (string solver in AllSolvers)
                foreach (string instance in instances)
                    data.Add(solver, instance);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Unsolvable))]
    public void AKARI_Solver_Returns_NoSolution_When_Unsolvable(string solver, string instance) {
        Assert.Equal(AkariVerifier.NoSolution, Solve(solver, instance));
    }

    [Theory]
    [MemberData(nameof(SearchSolvers))]
    public void AKARI_Solver_Solves_DefaultInstance(string solver) {
        var problem = new AKARI();
        string solution = SolverNamed(solver).solve(problem);
        Assert.True(problem.defaultVerifier.verify(problem, solution));
        // Both search orders land on the published example (CertificateExample_Tests checks it verifies too).
        Assert.Equal(AkariVerifier.CertificateExample, solution);
    }

    [Fact]
    public void AKARI_DefaultSolver_Output_Passes_DefaultVerifier() {
        var problem = new AKARI();
        string solution = problem.defaultSolver.solve(problem);
        Assert.True(problem.defaultVerifier.verify(problem, solution));
    }

    [Theory]
    [MemberData(nameof(SearchSolvers))]
    public void AKARI_Solver_Solves_Open_8x8_Grid(string solver) {
        // No walls: needs 8 bulbs, one per row and column (8 non-attacking rooks). 2^64 placements for brute force.
        string row = "(" + string.Join(",", Enumerable.Repeat("_", 8)) + ")";
        string instance = "(" + string.Join(",", Enumerable.Repeat(row, 8)) + ")";
        string solution = Solve(solver, instance);
        Assert.True(new AkariVerifier().verify(new AKARI(instance), solution));
        Assert.Equal(8, solution.Count(c => c == '*'));
    }

    // Builds a puzzle that is solvable by construction: random walls, then a bulb on every cell still dark in a random
    // order (so no bulb lights another and every cell ends up lit), then each wall labelled with its count of adjacent
    // bulbs with probability clueRate. A fixed seed keeps the instance the same on every run.
    private static string GeneratePuzzle(int rows, int cols, int seed, double wallRate = 0.2, double clueRate = 0.6) {
        var rng = new Random(seed);
        var wall = new bool[rows, cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                wall[r, c] = rng.NextDouble() < wallRate;

        var bulb = new bool[rows, cols];
        var lit = new bool[rows, cols];
        var order = Enumerable.Range(0, rows * cols).OrderBy(_ => rng.Next()).ToList();
        foreach (int cell in order) {
            int r = cell / cols, c = cell % cols;
            if (wall[r, c] || lit[r, c]) continue;
            bulb[r, c] = true;
            lit[r, c] = true;
            foreach (var (dr, dc) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) }) {
                for (int nr = r + dr, nc = c + dc; nr >= 0 && nr < rows && nc >= 0 && nc < cols && !wall[nr, nc]; nr += dr, nc += dc)
                    lit[nr, nc] = true;
            }
        }

        var lines = new List<string>();
        for (int r = 0; r < rows; r++) {
            var cells = new List<string>();
            for (int c = 0; c < cols; c++) {
                if (!wall[r, c]) { cells.Add("_"); continue; }
                int adjacent = new[] { (r - 1, c), (r + 1, c), (r, c - 1), (r, c + 1) }
                    .Count(p => p.Item1 >= 0 && p.Item1 < rows && p.Item2 >= 0 && p.Item2 < cols && bulb[p.Item1, p.Item2]);
                cells.Add(rng.NextDouble() < clueRate ? adjacent.ToString() : "#");
            }
            lines.Add("(" + string.Join(",", cells) + ")");
        }
        return "(" + string.Join(",", lines) + ")";
    }

    [Theory]
    [InlineData(nameof(AkariBacktracking), 10, 1)]
    [InlineData(nameof(AkariBacktracking), 10, 2)]
    [InlineData(nameof(AkariBacktracking), 10, 3)]
    [InlineData(nameof(AkariConstraintPropagation), 10, 1)]
    [InlineData(nameof(AkariConstraintPropagation), 10, 2)]
    [InlineData(nameof(AkariConstraintPropagation), 10, 3)]
    [InlineData(nameof(AkariConstraintPropagation), 20, 4)]
    [InlineData(nameof(AkariConstraintPropagation), 20, 5)]
    [InlineData(nameof(AkariConstraintPropagation), 30, 6)]
    public void AKARI_Solver_Solves_Generated_Puzzle(string solver, int size, int seed) {
        string instance = GeneratePuzzle(size, size, seed);
        string solution = Solve(solver, instance);
        Assert.True(new AkariVerifier().verify(new AKARI(instance), solution), $"{solver} returned {solution} for {instance}");
    }

    [Theory]
    [MemberData(nameof(SearchSolvers))]
    public void AKARI_Solver_Returns_NoSolution_On_Larger_Unsolvable_Grid(string solver) {
        // A 6x6 open grid around a 2x2 block of 2 clues. The top two clues each have just two white neighbours, so
        // both bulbs above the block are forced, and they share row 2. The search has to prove there is no way out.
        const string instance = "((_,_,_,_,_,_),(_,_,_,_,_,_),(_,_,2,2,_,_),(_,_,2,2,_,_),(_,_,_,_,_,_),(_,_,_,_,_,_))";
        Assert.Equal(AkariVerifier.NoSolution, Solve(solver, instance));
    }

    [Fact]
    public void AKARI_Solvers_Agree_With_BruteForce() {
        // Brute force is the reference: on every small instance the search solvers find a solution exactly when it does.
        var instances = new[] {
            "((_,_,_),(_,1,_),(_,_,_))", "((_,_,_),(_,2,_),(_,_,_))", "((_,_,_),(_,3,_),(_,_,_))",
            "((_,#,_),(1,_,1),(_,#,_))", "((2,_,_),(_,_,_),(_,_,2))", "((_,1,_),(1,_,1),(_,1,_))",
            "((_,_,_,_),(_,3,_,_),(_,_,1,_),(_,_,_,_))", "((0,_,_),(_,_,_),(_,_,0))", "((_,4,_),(_,_,_),(_,_,_))",
            "((_,_,#,_),(_,1,_,_),(#,_,_,2),(_,_,_,_))", "((_,2,_,_),(_,_,_,1),(3,_,_,_),(_,_,0,_))",
        };
        foreach (string instance in instances) {
            bool solvable = Solve(nameof(AkariBruteForce), instance) != AkariVerifier.NoSolution;
            foreach (string solver in SearchSolvers) {
                string solution = Solve(solver, instance);
                Assert.True(solvable == (solution != AkariVerifier.NoSolution), $"{solver} disagrees with brute force on {instance}");
                if (solvable) Assert.True(new AkariVerifier().verify(new AKARI(instance), solution));
            }
        }
    }

    [Theory]
    [MemberData(nameof(AllSolvers))]
    public void AKARI_Solver_Stops_When_Timer_Has_Expired(string solver) {
        // An expired timer makes the solver give up: it has no answer, so it says it was stopped.
        var s = SolverNamed(solver);
        s.timerHasExpired = true;
        Assert.Equal(SolveRun.TimedOutAnswer, s.solve(new AKARI()));
    }

    [Fact]
    public void AKARI_BruteForce_Stops_At_Time_Limit_On_DefaultInstance() {
        // 2^35 candidates: far too many to finish, so the run must come back stopped by the limit instead of hanging.
        ISolver solver = new AkariBruteForce();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        SolveRun run = solver.Run(new AKARI().instance, withSteps: false, TimeSpan.FromMilliseconds(200));
        Assert.True(run.TimedOut);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"took {watch.Elapsed}");
    }

    [Theory]
    [MemberData(nameof(AllSolvers))]
    public void AKARI_Solver_Metadata_IsDeclared(string solver) {
        ISolver s = SolverNamed(solver);
        Assert.StartsWith("Akari ", s.solverName);
        Assert.NotEqual(SolverType.Unclassified, s.solverType);
        Assert.Equal(SolverComplexityBucket.Exponential, s.complexityBucket);
        Assert.NotNull(s.timeComplexity);
        Assert.Contains("w", s.timeComplexity!.VariableNames);
    }

    #endregion
}
