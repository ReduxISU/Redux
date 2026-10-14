using Xunit;
using API.Problems.NPComplete.NPC_SUDOKU;
using API.Problems.NPComplete.NPC_SUDOKU.Verifiers;
using API.Problems.NPComplete.NPC_SUDOKU.Solvers;
using System.Runtime.InteropServices;

namespace redux_tests;
#pragma warning disable CS1591

/*
Example Sudoku Puzzle:
"0,0,0,0,0,0,2,0,0;" +
"0,8,0,0,0,7,0,9,0;" +
"6,0,2,0,0,0,5,0,0;" +
"0,7,0,0,6,0,0,0,0;" +
"0,0,0,9,0,1,0,0,0;" +
"0,0,0,0,2,0,0,4,0;" +
"0,0,5,0,0,0,6,0,3;" +
"0,9,0,4,0,0,0,7,0;" +
"0,0,6,0,0,0,0,0,0"

Example Solution:
"9,5,7,6,1,3,2,8,4;" +
"4,8,3,2,5,7,1,9,6;" +
"6,1,2,8,4,9,5,3,7;" +
"1,7,8,3,6,4,9,5,2;" +
"5,2,4,9,7,1,3,6,8;" +
"3,6,9,5,2,8,7,4,1;" +
"8,4,5,7,9,2,6,1,3;" +
"2,9,1,4,3,6,8,7,5;" +
"7,3,6,1,8,5,4,2,9"
*/

public class SUDOKU_Tests {
    private readonly SudokuVerifier _verifier = new();
    private SudokuSolver _solver = new();

    // Verifier Tests

    #region Valid Solution Tests

    [Fact]
    public void Verify_ValidCompleteSudoku_ReturnsTrue() {
        // Arrange
        var problem = new SUDOKU {
            instance = "0,0,0,0,0,0,2,0,0;" +
                       "0,8,0,0,0,7,0,9,0;" +
                       "6,0,2,0,0,0,5,0,0;" +
                       "0,7,0,0,6,0,0,0,0;" +
                       "0,0,0,9,0,1,0,0,0;" +
                       "0,0,0,0,2,0,0,4,0;" +
                       "0,0,5,0,0,0,6,0,3;" +
                       "0,9,0,4,0,0,0,7,0;" +
                       "0,0,6,0,0,0,0,0,0"
        };

        var validSolution = "9,5,7,6,1,3,2,8,4;" +
                            "4,8,3,2,5,7,1,9,6;" +
                            "6,1,2,8,4,9,5,3,7;" +
                            "1,7,8,3,6,4,9,5,2;" +
                            "5,2,4,9,7,1,3,6,8;" +
                            "3,6,9,5,2,8,7,4,1;" +
                            "8,4,5,7,9,2,6,1,3;" +
                            "2,9,1,4,3,6,8,7,5;" +
                            "7,3,6,1,8,5,4,2,9";
        // Act
        var result = _verifier.verify(problem, validSolution);

        // Assert
        Assert.True(result);
    }

    #endregion

    #region Empty Cell Tests

    [Fact]
    public void Verify_SolutionWithEmptyCell_ReturnsFalse() {
        // Arrange
        var problem = new SUDOKU {
            instance = "0,0,0,0,0,0,2,0,0;" +
                       "0,8,0,0,0,7,0,9,0;" +
                       "6,0,2,0,0,0,5,0,0;" +
                       "0,7,0,0,6,0,0,0,0;" +
                       "0,0,0,9,0,1,0,0,0;" +
                       "0,0,0,0,2,0,0,4,0;" +
                       "0,0,5,0,0,0,6,0,3;" +
                       "0,9,0,4,0,0,0,7,0;" +
                       "0,0,6,0,0,0,0,0,0"
        };

        var solutionWithEmpty = "9,5,7,6,1,3,2,8,4;" +
                                "4,8,3,2,5,7,1,9,6;" +
                                "6,1,2,8,4,9,5,3,7;" +
                                "1,7,8,3,6,4,9,5,2;" +
                                "5,2,4,9,0,1,3,6,8;" + // Empty cell at [4,4]
                                "3,6,9,5,2,8,7,4,1;" +
                                "8,4,5,7,9,2,6,1,3;" +
                                "2,9,1,4,3,6,8,7,5;" +
                                "7,3,6,1,8,5,4,2,9";

        // Act
        var result = _verifier.verify(problem, solutionWithEmpty);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region Invalid Value Tests

    [Fact]
    public void Verify_SolutionWithValueGreaterThan9_ReturnsFalse() {
        // Arrange
        var problem = new SUDOKU {
            instance = "0,0,0,0,0,0,2,0,0;" +
                       "0,8,0,0,0,7,0,9,0;" +
                       "6,0,2,0,0,0,5,0,0;" +
                       "0,7,0,0,6,0,0,0,0;" +
                       "0,0,0,9,0,1,0,0,0;" +
                       "0,0,0,0,2,0,0,4,0;" +
                       "0,0,5,0,0,0,6,0,3;" +
                       "0,9,0,4,0,0,0,7,0;" +
                       "0,0,6,0,0,0,0,0,0"
        };

        var solutionWithInvalidValue = "9,5,7,6,1,3,2,8,4;" +
                                        "4,8,3,2,5,7,1,9,6;" +
                                        "6,1,2,8,4,9,5,3,7;" +
                                        "1,7,8,3,6,4,9,5,2;" +
                                        "5,2,4,9,7,10,3,6,8;" + // Invalid value 10 at [4,5]
                                        "3,6,9,5,2,8,7,4,1;" +
                                        "8,4,5,7,9,2,6,1,3;" +
                                        "2,9,1,4,3,6,8,7,5;" +
                                        "7,3,6,1,8,5,4,2,9";

        // Act
        var result = _verifier.verify(problem, solutionWithInvalidValue);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Verify_SolutionWithNegativeValue_ReturnsFalse() {
        // Arrange
        var problem = new SUDOKU {
            instance = "0,0,0,0,0,0,2,0,0;" +
                       "0,8,0,0,0,7,0,9,0;" +
                       "6,0,2,0,0,0,5,0,0;" +
                       "0,7,0,0,6,0,0,0,0;" +
                       "0,0,0,9,0,1,0,0,0;" +
                       "0,0,0,0,2,0,0,4,0;" +
                       "0,0,5,0,0,0,6,0,3;" +
                       "0,9,0,4,0,0,0,7,0;" +
                       "0,0,6,0,0,0,0,0,0"
        };

        var solutionWithNegative = "9,5,7,6,1,3,2,8,4;" +
                                    "4,8,3,2,5,7,1,9,6;" +
                                    "6,1,2,8,4,9,5,3,7;" +
                                    "1,7,8,3,6,4,9,5,2;" +
                                    "5,2,4,9,7,1,3,6,8;" +
                                    "3,6,9,5,2,8,7,4,-1;" + // Negative value -1 at [5,8]
                                    "8,4,5,7,9,2,6,1,3;" +
                                    "2,9,1,4,3,6,8,7,5;" +
                                    "7,3,6,1,8,5,4,2,9";

        // Act
        var result = _verifier.verify(problem, solutionWithNegative);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region Clue Matching Tests

    [Fact]
    public void Verify_SolutionDoesNotMatchInitialClues_ReturnsFalse() {
        // Arrange
        var problem = new SUDOKU {
            instance = "0,0,0,0,0,0,2,0,0;" + //Must have 2 at [0,6]
                       "0,8,0,0,0,7,0,9,0;" +
                       "6,0,2,0,0,0,5,0,0;" +
                       "0,7,0,0,6,0,0,0,0;" +
                       "0,0,0,9,0,1,0,0,0;" +
                       "0,0,0,0,2,0,0,4,0;" +
                       "0,0,5,0,0,0,6,0,3;" +
                       "0,9,0,4,0,0,0,7,0;" +
                       "0,0,6,0,0,0,0,0,0"
        };

        var solutionWithMismatch = "6,1,3,2,8,4,9,5,7;" +  // Has 9 at [0,6] instead of 2
                                    "2,5,7,1,9,6,4,8,3;" +
                                    "8,4,9,5,3,7,6,1,2;" +
                                    "3,6,4,9,5,2,1,7,8;" +
                                    "9,7,1,3,6,8,5,2,4;" +
                                    "5,2,8,7,4,1,3,6,9;" +
                                    "7,9,2,6,1,3,8,4,5;" +
                                    "4,3,6,8,7,5,2,9,1;" +
                                    "1,8,5,4,2,9,7,3,6";

        // Act
        var result = _verifier.verify(problem, solutionWithMismatch);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region Row Duplicate Tests

    [Fact]
    public void Verify_SolutionWithDuplicateInRow_ReturnsFalse() {
        // Arrange
        var problem = new SUDOKU {
            instance = "0,0,0,0,0,0,2,0,0;" +
                       "0,8,0,0,0,7,0,9,0;" +
                       "6,0,2,0,0,0,5,0,0;" +
                       "0,7,0,0,6,0,0,0,0;" +
                       "0,0,0,9,0,1,0,0,0;" +
                       "0,0,0,0,2,0,0,4,0;" +
                       "0,0,5,0,0,0,6,0,3;" +
                       "0,9,0,4,0,0,0,7,0;" +
                       "0,0,6,0,0,0,0,0,0"
        };

        var solutionWithRowDuplicate = "9,5,7,6,1,3,2,8,9;" + // 9 appears twice in row 0
                                        "4,8,3,2,5,7,1,9,6;" +
                                        "6,1,2,8,4,9,5,3,7;" +
                                        "1,7,8,3,6,4,9,5,2;" +
                                        "5,2,4,9,7,1,3,6,8;" +
                                        "3,6,9,5,2,8,7,4,1;" +
                                        "8,4,5,7,9,2,6,1,3;" +
                                        "2,9,1,4,3,6,8,7,5;" +
                                        "7,3,6,1,8,5,4,2,9";

        // Act
        var result = _verifier.verify(problem, solutionWithRowDuplicate);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region Column Duplicate Tests

    [Fact]
    public void Verify_SolutionWithDuplicateInColumn_ReturnsFalse() {
        // Arrange
        var problem = new SUDOKU {
            instance = "0,0,0,0,0,0,2,0,0;" +
                       "0,8,0,0,0,7,0,9,0;" +
                       "6,0,2,0,0,0,5,0,0;" +
                       "0,7,0,0,6,0,0,0,0;" +
                       "0,0,0,9,0,1,0,0,0;" +
                       "0,0,0,0,2,0,0,4,0;" +
                       "0,0,5,0,0,0,6,0,3;" +
                       "0,9,0,4,0,0,0,7,0;" +
                       "0,0,6,0,0,0,0,0,0"
        };

        var solutionWithColDuplicate = "9,5,7,6,1,3,2,8,4;" +
                                        "4,8,3,2,5,7,1,9,6;" +
                                        "6,1,2,8,4,9,5,3,7;" +
                                        "1,7,8,3,6,4,9,5,2;" +
                                        "5,2,4,9,7,1,3,6,1;" + // 1 appears twice in column 8
                                        "3,6,9,5,2,8,7,4,1;" +
                                        "8,4,5,7,9,2,6,1,3;" +
                                        "2,9,1,4,3,6,8,7,5;" +
                                        "7,3,6,1,8,5,4,2,9";

        // Act
        var result = _verifier.verify(problem, solutionWithColDuplicate);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region Block Duplicate Tests

    [Fact]
    public void Verify_SolutionWithDuplicateIn3x3Block_ReturnsFalse() {
        // Arrange
        var problem = new SUDOKU {
            instance = "0,0,0,0,0,0,2,0,0;" +
                       "0,8,0,0,0,7,0,9,0;" +
                       "6,0,2,0,0,0,5,0,0;" +
                       "0,7,0,0,6,0,0,0,0;" +
                       "0,0,0,9,0,1,0,0,0;" +
                       "0,0,0,0,2,0,0,4,0;" +
                       "0,0,5,0,0,0,6,0,3;" +
                       "0,9,0,4,0,0,0,7,0;" +
                       "0,0,6,0,0,0,0,0,0"
        };

        var solutionWithBlockDuplicate = "9,5,7,6,1,3,2,8,4;" +
                                        "4,8,3,2,5,7,1,9,6;" +
                                        "6,1,9,8,4,9,5,3,7;" + // 9 appears twice in the top-left 3x3 block
                                        "1,7,8,3,6,4,9,5,2;" +
                                        "5,2,4,9,7,1,3,6,8;" +
                                        "3,6,9,5,2,8,7,4,1;" +
                                        "8,4,5,7,9,2,6,1,3;" +
                                        "2,9,1,4,3,6,8,7,5;" +
                                        "7,3,6,1,8,5,4,2,9";

        // Act
        var result = _verifier.verify(problem, solutionWithBlockDuplicate);

        // Assert
        Assert.False(result);
    }

    #endregion

    // Solver Tests

    #region One-Solution Problem Tests

    [Theory]

    [InlineData("0,0,0,0,0,0,0,1,0;4,0,0,0,0,0,0,0,0;0,2,0,0,0,0,0,0,0;0,0,0,0,5,0,4,0,7;0,0,8,0,0,0,3,0,0;0,0,1,0,9,0,0,0,0;3,0,0,4,0,0,2,0,0;0,5,0,1,0,0,0,0,0;0,0,0,8,0,6,0,0,0", "6,9,3,7,8,4,5,1,2;4,8,7,5,1,2,9,3,6;1,2,5,9,6,3,8,7,4;9,3,2,6,5,1,4,8,7;5,6,8,2,4,7,3,9,1;7,4,1,3,9,8,6,2,5;3,1,9,4,7,5,2,6,8;8,5,6,1,2,9,7,4,3;2,7,4,8,3,6,1,5,9")]
    [InlineData("0,4,8,1,9,5,2,7,3;3,2,1,7,4,8,5,9,6;9,7,5,3,2,6,4,8,1;5,9,2,6,7,1,8,3,4;8,6,7,4,3,2,1,5,9;1,3,4,8,5,9,6,2,7;2,1,9,5,6,3,7,4,8;7,5,6,9,8,4,3,1,2;4,8,3,2,1,7,9,6,5", "6,4,8,1,9,5,2,7,3;3,2,1,7,4,8,5,9,6;9,7,5,3,2,6,4,8,1;5,9,2,6,7,1,8,3,4;8,6,7,4,3,2,1,5,9;1,3,4,8,5,9,6,2,7;2,1,9,5,6,3,7,4,8;7,5,6,9,8,4,3,1,2;4,8,3,2,1,7,9,6,5")]
    public void Verify_OneSolutionProblem_ReturnsCorrect(string instance, string expected) {
        var problem = new SUDOKU(instance);

        Assert.Equivalent(expected, _solver.solve(problem));
    }

    #endregion

    #region Unsolvable Problem Tests

    [Theory]

    [InlineData("0,0,1,1,0,0,2,0,3;0,2,0,0,4,0,5,0,6;0,7,0,0,0,6,4,0,0;5,0,0,6,0,0,8,0,0;0,6,0,4,0,2,0,5,0;0,0,4,0,0,9,0,0,7;0,0,9,5,0,0,0,4,0;7,0,6,0,8,0,0,1,0;4,0,3,0,0,7,0,0,0")]
    [InlineData("7,8,1,5,4,3,9,2,6;0,0,6,1,7,9,5,0,0;9,5,4,6,2,8,7,3,1;6,9,5,8,3,7,2,1,4;1,4,8,2,6,5,3,7,9;3,2,7,9,1,4,8,0,0;4,1,3,7,5,2,6,9,8;0,0,2,0,0,0,4,0,0;5,7,9,4,8,6,1,0,3")]
    public void Verify_UnsolvableProblem_ReturnsNoSolution(string instance) {
        var problem = new SUDOKU(instance);

        Assert.Equal("{}", _solver.solve(problem));
    }

    #endregion

    // -------------------------------------------------------------------------
    // Format declarations
    // -------------------------------------------------------------------------

    [Fact]
    public void SUDOKU_Instance_Format_Described() {
        SUDOKU problem = new SUDOKU();
        Assert.NotNull(problem.instanceFormat);
        Assert.NotEmpty(problem.instanceFormat);
        Assert.Contains("semicolons", problem.instanceFormat);
    }

    [Fact]
    public void SUDOKU_Certificate_Format_Described() {
        SUDOKU problem = new SUDOKU();
        Assert.NotNull(problem.certificateFormat);
        Assert.NotEmpty(problem.certificateFormat);
        Assert.Contains("3x3 block", problem.certificateFormat);
    }

    [Fact]
    public void SUDOKU_Certificate_Format_Example_Is_Actually_Valid() {
        // The example quoted in certificateFormat must be a real, verifiable
        // certificate for defaultInstance — not just descriptive prose.
        SUDOKU problem = new SUDOKU();
        SudokuVerifier verifier = new SudokuVerifier();
        Assert.True(verifier.verify(problem, SudokuVerifier.CertificateExample));
    }

    #region Solver Output Passes Verifier

    [Theory]
    [InlineData("0,0,0,0,0,0,0,1,0;4,0,0,0,0,0,0,0,0;0,2,0,0,0,0,0,0,0;0,0,0,0,5,0,4,0,7;0,0,8,0,0,0,3,0,0;0,0,1,0,9,0,0,0,0;3,0,0,4,0,0,2,0,0;0,5,0,1,0,0,0,0,0;0,0,0,8,0,6,0,0,0")]
    [InlineData("0,4,8,1,9,5,2,7,3;3,2,1,7,4,8,5,9,6;9,7,5,3,2,6,4,8,1;5,9,2,6,7,1,8,3,4;8,6,7,4,3,2,1,5,9;1,3,4,8,5,9,6,2,7;2,1,9,5,6,3,7,4,8;7,5,6,9,8,4,3,1,2;4,8,3,2,1,7,9,6,5")]
    public void SudokuSolver_Output_Passes_Verifier(string instance) {
        var problem = new SUDOKU(instance);
        string solution = _solver.solve(problem);
        Assert.True(_verifier.verify(problem, solution), $"Solver output failed verifier for: {instance}");
    }

    #endregion

    #region Invalid clues (#704)

    private const string Empty9 = "0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0";

    private static string WithCells(params (int r, int c, int v)[] cells) {
        var g = Empty9.Split(';').Select(row => row.Split(',').Select(int.Parse).ToArray()).ToArray();
        foreach (var (r, c, v) in cells) g[r][c] = v;
        return string.Join(";", g.Select(row => string.Join(",", row)));
    }

    [Theory]
    [InlineData(0, 0, 5, 0, 8, 5)] // duplicate in row 0
    [InlineData(0, 0, 5, 8, 0, 5)] // duplicate in column 0
    [InlineData(0, 0, 5, 2, 2, 5)] // duplicate in the top-left box only
    [InlineData(4, 4, 9, 3, 3, 9)] // duplicate in the centre box only
    public void SudokuSolver_DuplicateClue_ReturnsNoSolution_Quickly(int r1, int c1, int v1, int r2, int c2, int v2) {
        // With an otherwise empty grid the unvalidated search would take a long time to
        // discover (or never discover) the conflict; validation rejects it immediately.
        var problem = new SUDOKU(WithCells((r1, c1, v1), (r2, c2, v2)));
        var sw = System.Diagnostics.Stopwatch.StartNew();
        string result = _solver.solve(problem);
        sw.Stop();
        Assert.Equal("{}", result);
        Assert.True(sw.ElapsedMilliseconds < 1000, $"took {sw.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void SudokuSolver_OutOfRangeClue_ReturnsNoSolution_WhenGivenDirectly() {
        // The parser rejects these, but SolveInternal is public and must not trust its caller.
        var grid = Empty9.Split(';').Select(row => row.Split(',').Select(int.Parse).ToArray()).ToArray();
        grid[0][0] = 10;
        Assert.Equal("{}", _solver.SolveInternal(grid));
        grid[0][0] = -1;
        Assert.Equal("{}", _solver.SolveInternal(grid));
    }

    [Fact]
    public void SudokuSolver_DefaultInstance_StillSolves() {
        var problem = new SUDOKU();
        string solution = _solver.solve(problem);
        Assert.NotEqual("{}", solution);
        Assert.True(_verifier.verify(problem, solution));
    }

    #endregion

    #region 9x9 enforcement (#705)

    [Theory]
    [InlineData("0,0,0,0;0,0,0,0;0,0,0,0;0,0,0,0")] // 4x4
    [InlineData("1,2,3,4;3,4,1,2;2,1,4,3;4,3,2,1")] // complete 4x4
    [InlineData("0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0")] // 16x16
    [InlineData("0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0")] // too few rows
    [InlineData("0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0")] // ragged row
    [InlineData("0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0;0,0,0,0,0,0,0,0,0")] // too many rows
    [InlineData("")]
    [InlineData("not a grid")]
    public void SUDOKU_Parser_Rejects_NonNineByNine(string instance) {
        Assert.Throws<API.Interfaces.ProblemParseException>(() => new SUDOKU(instance));
    }

    [Theory]
    [InlineData(0, 0, "10")]
    [InlineData(0, 0, "-1")]
    [InlineData(4, 4, "a")]
    [InlineData(8, 8, "")]
    public void SUDOKU_Parser_Rejects_OutOfRangeOrNonDigitValues(int r, int c, string value) {
        var g = Empty9.Split(';').Select(row => row.Split(',')).ToArray();
        g[r][c] = value;
        string instance = string.Join(";", g.Select(row => string.Join(",", row)));
        Assert.Throws<API.Interfaces.ProblemParseException>(() => new SUDOKU(instance));
    }

    [Fact]
    public void SUDOKU_Parser_Accepts_Whitespace_And_Trailing_Semicolon() {
        var problem = new SUDOKU(SudokuVerifier.CertificateExample.Replace(",", ", ") + ";" + Environment.NewLine);
        Assert.Equal(9, problem.grid.Length);
        Assert.All(problem.grid, row => Assert.Equal(9, row.Length));
    }

    [Theory]
    [InlineData("6,4,8,1,9,5,2,7,3;3,2,1,7,4,8,5,9,6")] // too few rows
    [InlineData("6,4,8,1,9,5,2,7;3,2,1,7,4,8,5,9,6;9,7,5,3,2,6,4,8,1;5,9,2,6,7,1,8,3,4;8,6,7,4,3,2,1,5,9;1,3,4,8,5,9,6,2,7;2,1,9,5,6,3,7,4,8;7,5,6,9,8,4,3,1,2;4,8,3,2,1,7,9,6,5")] // ragged
    [InlineData("6,4,8,1,9,5,2,7,3,1;3,2,1,7,4,8,5,9,6;9,7,5,3,2,6,4,8,1;5,9,2,6,7,1,8,3,4;8,6,7,4,3,2,1,5,9;1,3,4,8,5,9,6,2,7;2,1,9,5,6,3,7,4,8;7,5,6,9,8,4,3,1,2;4,8,3,2,1,7,9,6,5")] // long row
    [InlineData("1,2,3,4;3,4,1,2;2,1,4,3;4,3,2,1")] // 4x4
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("6,4,x,1,9,5,2,7,3;3,2,1,7,4,8,5,9,6;9,7,5,3,2,6,4,8,1;5,9,2,6,7,1,8,3,4;8,6,7,4,3,2,1,5,9;1,3,4,8,5,9,6,2,7;2,1,9,5,6,3,7,4,8;7,5,6,9,8,4,3,1,2;4,8,3,2,1,7,9,6,5")]
    public void SudokuVerifier_RaggedOrWrongSizeCertificate_ReturnsFalse(string certificate) {
        var problem = new SUDOKU();
        Assert.False(_verifier.verify(problem, certificate));
    }

    [Fact]
    public void SUDOKU_Text_Describes_9x9_And_Solved_Grid() {
        var problem = new SUDOKU();
        Assert.DoesNotContain("n×n", problem.formalDefinition);
        Assert.Contains("9×9", problem.formalDefinition);
        Assert.DoesNotContain("True or False", problem.outputDescription);
        Assert.Contains("solved", problem.outputDescription);
    }

    #endregion
}


//Putting code being tested here for ease of testing. Will move to correct file once tests are working
/*

*/