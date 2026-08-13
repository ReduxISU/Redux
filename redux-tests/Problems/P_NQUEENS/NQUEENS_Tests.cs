using Xunit;
using API.Problems.P.P_NQUEENS;
using API.Problems.P.P_NQUEENS.Solvers;
using API.Problems.P.P_NQUEENS.Verifiers;

namespace redux_tests;
#pragma warning disable CS1591

public class NQUEENS_Tests {
    [Fact]
    public void NQUEENS_Default_Constructor_Sets_N_To_4() {
        NQUEENS problem = new NQUEENS();
        Assert.Equal(4, problem.n);
        Assert.Equal("4", problem.defaultInstance);
    }

    [Fact]
    public void NQUEENS_Custom_Constructor_Sets_N_Correctly() {
        NQUEENS problem = new NQUEENS("8");
        Assert.Equal(8, problem.n);
        Assert.Equal("8", problem.instance);
    }

    [Fact]
    public void NQUEENS_Verifier_Validates_Single_Queen() {
        NQUEENS problem = new NQUEENS("1");
        NQueensVerifier verifier = new NQueensVerifier();

        bool result = verifier.verify(problem, "{(0,0)}");

        Assert.True(result);
    }

    [Fact]
    public void NQUEENS_Verifier_Validates_Known_4Queen_Solution() {
        NQUEENS problem = new NQUEENS("4");
        NQueensVerifier verifier = new NQueensVerifier();

        bool result = verifier.verify(problem, "{(0,1),(1,3),(2,0),(3,2)}");

        Assert.True(result);
    }

    [Fact]
    public void NQUEENS_Verifier_Rejects_Duplicate_Column() {
        NQUEENS problem = new NQUEENS("4");
        NQueensVerifier verifier = new NQueensVerifier();

        bool result = verifier.verify(problem, "{(0,1),(1,1),(2,0),(3,2)}");

        Assert.False(result);
    }

    [Fact]
    public void NQUEENS_Verifier_Rejects_Diagonal_Conflict() {
        NQUEENS problem = new NQUEENS("4");
        NQueensVerifier verifier = new NQueensVerifier();

        bool result = verifier.verify(problem, "{(0,0),(1,1),(2,2),(3,3)}");

        Assert.False(result);
    }

    [Fact]
    public void NQUEENS_Verifier_Rejects_Out_Of_Bounds() {
        NQUEENS problem = new NQUEENS("4");
        NQueensVerifier verifier = new NQueensVerifier();

        bool result = verifier.verify(problem, "{(0,1),(1,3),(2,0),(4,2)}");

        Assert.False(result);
    }

    [Fact]
    public void NQUEENS_Verifier_Rejects_Wrong_Number_Of_Queens() {
        NQUEENS problem = new NQUEENS("4");
        NQueensVerifier verifier = new NQueensVerifier();

        bool result = verifier.verify(problem, "{(0,1),(1,3),(2,0)}");

        Assert.False(result);
    }

    [Fact]
    public void NQUEENS_Verifier_Rejects_Empty_Certificate() {
        NQUEENS problem = new NQUEENS("4");
        NQueensVerifier verifier = new NQueensVerifier();

        bool result = verifier.verify(problem, "");

        Assert.False(result);
    }

    [Fact]
    public void NQUEENS_Solver_Finds_Valid_Solution_For_4() {
        NQUEENS problem = new NQUEENS("4");
        NQueensBacktracking solver = new NQueensBacktracking();
        NQueensVerifier verifier = new NQueensVerifier();

        string certificate = solver.solve(problem);

        Assert.True(verifier.verify(problem, certificate));
    }

    [Fact]
    public void NQUEENS_Solver_Returns_No_Solution_For_2() {
        NQUEENS problem = new NQUEENS("2");
        NQueensBacktracking solver = new NQueensBacktracking();

        string certificate = solver.solve(problem);

        Assert.Equal("{}", certificate);
    }

    [Fact]
    public void NQUEENS_Solver_Returns_No_Solution_For_3() {
        NQUEENS problem = new NQUEENS("3");
        NQueensBacktracking solver = new NQueensBacktracking();

        string certificate = solver.solve(problem);

        Assert.Equal("{}", certificate);
    }

    // --- Constructive solver ---

    [Fact]
    public void NQUEENS_Default_Solver_Is_Constructive() {
        NQUEENS problem = new NQUEENS();
        Assert.IsType<NQueensConstructive>(problem.defaultSolver);
    }

    [Fact]
    public void NQUEENS_Constructive_Solves_Single_Queen() {
        NQUEENS problem = new NQUEENS("1");
        NQueensConstructive solver = new NQueensConstructive();
        NQueensVerifier verifier = new NQueensVerifier();

        string certificate = solver.solve(problem);

        Assert.Equal("{(0,0)}", certificate);
        Assert.True(verifier.verify(problem, certificate));
    }

    [Fact]
    public void NQUEENS_Constructive_Returns_No_Solution_For_2() {
        NQUEENS problem = new NQUEENS("2");
        NQueensConstructive solver = new NQueensConstructive();

        Assert.Equal("{}", solver.solve(problem));
    }

    [Fact]
    public void NQUEENS_Constructive_Returns_No_Solution_For_3() {
        NQUEENS problem = new NQUEENS("3");
        NQueensConstructive solver = new NQueensConstructive();

        Assert.Equal("{}", solver.solve(problem));
    }

    // The construction must produce a valid placement for every n >= 4 (and n = 1).
    // Sweeping across all mod-12 remainders exercises each rearrangement branch.
    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(20)]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(100)]
    public void NQUEENS_Constructive_Produces_Verifiable_Solution(int n) {
        NQUEENS problem = new NQUEENS(n.ToString());
        NQueensConstructive solver = new NQueensConstructive();
        NQueensVerifier verifier = new NQueensVerifier();

        string certificate = solver.solve(problem);

        Assert.NotEqual("{}", certificate);
        Assert.True(verifier.verify(problem, certificate),
            $"Constructive solution for n={n} failed verification: {certificate}");
    }
}
