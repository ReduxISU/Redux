
using Xunit;
using API.Interfaces;
using API.Problems.NPComplete.NPC_MINESWEEPER;
using API.Problems.NPComplete.NPC_MINESWEEPER.Solvers;
using API.Problems.NPComplete.NPC_MINESWEEPER.Verifiers;
using API.Problems.NPComplete.NPC_MINESWEEPER.Visualizations;

namespace redux_tests;

#pragma warning disable CS1591

public class MINESWEEPER_Tests {

    private readonly MinesweeperSolver _solver = new();
    private readonly MinesweeperVerifier _verifier = new();

    // Parser tests

    [Theory]
    [InlineData("?", 1, 1)]
    [InlineData("0,?;?,1", 2, 2)]
    [InlineData("1,?,1;?,?,?;1,?,1", 3, 3)]
    [InlineData("0,?,?;1,?,?", 2, 3)]
    public void Parser_AcceptsValidBoards(
        string input, int rows, int columns) {

        var problem = new MINESWEEPER(input);

        Assert.Equal(rows, problem.Rows);
        Assert.Equal(columns, problem.Columns);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1,?;?,1,?")]
    [InlineData("1,9")]
    [InlineData("1,-1")]
    [InlineData("1,x")]
    [InlineData("1,;?,1")]
    [InlineData(";1,?")]
    [InlineData("1,?;")]
    public void Parser_RejectsInvalidBoards(string input) {
        Assert.Throws<ProblemParseException>(
            () => new MINESWEEPER(input));
    }

    // Verifier tests

    [Fact]
    public void Verifier_AcceptsValidCertificate() {
        var problem = new MINESWEEPER();

        Assert.True(_verifier.verify(
            problem, MinesweeperVerifier.CertificateExample));
    }

    [Theory]
    [InlineData("0,0,0;0,0,0;0,0,0")]
    [InlineData("0,1,0;0,0,0;0,0,0")]
    [InlineData("1,0,0;1,0,1;0,0,0")]
    [InlineData("0,0,0;1,1,1;0,0,0")]
    public void Verifier_RejectsIncorrectPlacements(string certificate) {
        var problem = new MINESWEEPER();

        Assert.False(_verifier.verify(problem, certificate));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("0,0;1,0")]
    [InlineData("0,0,0;1,0;0,0,0")]
    [InlineData("0,0,0;1,x,1;0,0,0")]
    [InlineData("0,0,0;1,2,1;0,0,0")]
    [InlineData("0,0,0;1,-1,1;0,0,0")]
    [InlineData("0,0,0;1,0,1;0,0,0;0,0,0")]
    public void Verifier_RejectsMalformedCertificates(
        string certificate) {

        Assert.False(_verifier.verify(
            new MINESWEEPER(), certificate));
    }

    // Solver tests

    [Theory]
    [InlineData("1,?,1;?,?,?;1,?,1")]
    [InlineData("0,?")]
    [InlineData("1,?")]
    [InlineData("?,1,?")]
    [InlineData("?")]
    [InlineData("0")]
    public void Solver_ProducesVerifiableSolution(string input) {
        var problem = new MINESWEEPER(input);

        string solution = _solver.solve(problem);

        Assert.NotEqual("{}", solution);
        Assert.True(_verifier.verify(problem, solution),
            $"Invalid solver output for {input}: {solution}");
    }

    [Theory]
    [InlineData("1")]
    [InlineData("2,?")]
    [InlineData("0,1")]
    [InlineData("1,?;?,0")]
    public void Solver_ReturnsNoSolution(string input) {
        var problem = new MINESWEEPER(input);

        Assert.Equal("{}", _solver.solve(problem));
    }

    [Fact]
    public void Solver_DoesNotModifyOriginalBoard() {
        var problem = new MINESWEEPER();
        var original = problem.grid.Select(
            row => row.ToArray()).ToArray();

        _solver.solve(problem);

        for (int r = 0; r < problem.Rows; r++)
            Assert.Equal(original[r], problem.grid[r]);
    }

    [Fact]
    public void Solver_RespectsTimeout() {
        var problem = new MINESWEEPER();

        var solver = new MinesweeperSolver {
            timerHasExpired = true
        };

        Assert.Equal(SolveRun.TimedOutAnswer,
            solver.solve(problem));
    }

    // Metadata tests

    [Fact]
    public void CertificateExample_IsValidForDefaultInstance() {
        var problem = new MINESWEEPER();

        Assert.True(_verifier.verify(
            problem, MinesweeperVerifier.CertificateExample));
    }

    [Fact]
    public void Problem_DeclaresCorrectComplexity() {
        var problem = new MINESWEEPER();

        Assert.Equal(ComplexityClass.NPComplete,
            problem.complexityClass);

        Assert.Equal(ProblemVariant.Decision,
            problem.problemVariant);
    }

    // Visualization tests

    [Theory]
    [InlineData("1,?,1;?,?,?;1,?,1", 3, 3)]
    [InlineData("0,?,?;1,?,?", 2, 3)]
    [InlineData("?", 1, 1)]
    [InlineData("?;?;?", 3, 1)]
    public void Visualization_PreservesBoardLayout(string input, int rows, int columns) {
        var visualization = new MinesweeperDefaultVisualization();
        var board = visualization.visualize(new MINESWEEPER(input));

        Assert.Equal(VisualizationType.DynamicTable, visualization.visualizationType);
        Assert.Equal(columns, board.columns.Count);
        Assert.Equal(rows, board.rows.Count);
        Assert.Equal(columns, board.columns.Select(column => column.key).Distinct().Count());
        Assert.Equal(rows, board.rows.Select(row => row.id).Distinct().Count());
        Assert.All(board.rows, row => Assert.Equal(
            board.columns.Select(column => column.key), row.cells.Keys));
    }

    [Fact]
    public void Visualization_ShowsCluesAndCoveredCells() {
        var board = new MinesweeperDefaultVisualization().visualize(
            new MINESWEEPER("0,?;?,1"));

        Assert.Equal("", board.rows[0].cells["c0"]);
        Assert.Equal("1", board.rows[1].cells["c1"]);
        Assert.Equal("■", board.rows[0].cells["c1"]);
        Assert.Equal("■", board.rows[1].cells["c0"]);
        Assert.Equal("Background", board.rows[0].cellColors!["c0"]);
        Assert.Equal("ElementHighlight", board.rows[0].cellColors!["c1"]);
        Assert.NotEqual(board.rows[0].cellColors!["c0"], board.rows[0].cellColors!["c1"]);
    }

    [Fact]
    public void SolvedVisualization_RevealsMinesAndAdjacentCounts() {
        var board = Assert.IsType<API.Interfaces.JSON_Objects.Tables.API_TableJSON>(
            new MinesweeperDefaultVisualization().SolvedVisualization(
                new MINESWEEPER(), MinesweeperVerifier.CertificateExample));

        Assert.Equal("✹", board.rows[1].cells["c0"]);
        Assert.Equal("✹", board.rows[1].cells["c2"]);
        Assert.Equal("Solution", board.rows[1].cellColors!["c0"]);
        Assert.Equal("2", board.rows[1].cells["c1"]);
        Assert.Equal("2", board.rows[0].cells["c1"]);
        Assert.Equal("2", board.rows[2].cells["c1"]);
        Assert.Equal("1", board.rows[0].cells["c0"]);
        Assert.NotEqual(board.rows[1].cellColors!["c0"], board.rows[0].cellColors!["c0"]);
        Assert.DoesNotContain(board.rows.SelectMany(row => row.cells.Values), cell => cell == "■");
    }

    [Theory]
    [InlineData("0,?", "0,0", "", "")]
    [InlineData("?,?;?,?", "1,0;0,1", "✹", "2")]
    [InlineData("?;?;?", "1;0;0", "✹", "1")]
    [InlineData("?", "1", "✹", "✹")]
    public void SolvedVisualization_HandlesEdgesAndBlankSafeCells(
        string input, string solution, string first, string second) {
        var board = Assert.IsType<API.Interfaces.JSON_Objects.Tables.API_TableJSON>(
            new MinesweeperDefaultVisualization().SolvedVisualization(new MINESWEEPER(input), solution));

        Assert.Equal(first, board.rows[0].cells["c0"]);
        Assert.Equal(second, board.columns.Count > 1
            ? board.rows[0].cells["c1"] : board.rows[Math.Min(1, board.rows.Count - 1)].cells["c0"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("{}")]
    [InlineData(SolveRun.TimedOutAnswer)]
    [InlineData("0,0,0;0,0,0;0,0,0")]
    [InlineData("1,0,0;1,0,1;0,0,0")]
    [InlineData("not a certificate")]
    public void SolvedVisualization_InvalidSolutionKeepsBoardCovered(string solution) {
        var visualization = new MinesweeperDefaultVisualization();
        var problem = new MINESWEEPER();
        var original = visualization.visualize(problem);
        var result = visualization.SolvedVisualization(problem, solution);

        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(original),
            System.Text.Json.JsonSerializer.Serialize(result, result.GetType()));
    }

    [Fact]
    public void SolvedVisualization_AcceptsCertificateLineEndingsAndDoesNotModifyBoard() {
        var problem = new MINESWEEPER();
        var original = problem.grid.Select(row => row.ToArray()).ToArray();
        var visualization = new MinesweeperDefaultVisualization();
        var before = visualization.visualize(problem);
        var board = Assert.IsType<API.Interfaces.JSON_Objects.Tables.API_TableJSON>(
            visualization.SolvedVisualization(problem, " 0,0,0;\r\n1,0,1;\n0,0,0 "));

        Assert.Equal("✹", board.rows[1].cells["c0"]);
        Assert.Equal("■", before.rows[1].cells["c0"]);
        for (int r = 0; r < problem.Rows; r++)
            Assert.Equal(original[r], problem.grid[r]);
    }
}
