using Xunit;
using API.Interfaces;
using API.Problems.NPComplete.NPC_AKARI;

namespace redux_tests;
#pragma warning disable CS1591

// Tests for the AKARI problem class definition only (AKARI_Class.cs): metadata,
// cell encodings, and the instance parser. Solver/verifier/visualization are not covered here.
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
}
