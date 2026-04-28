using Xunit;
using API.Problems.NPComplete.NPC_NQUEENS;
using API.Problems.NPComplete.NPC_NQUEENS.Solvers;
using API.Problems.NPComplete.NPC_NQUEENS.Verifiers;

namespace redux_tests;
#pragma warning disable CS1591

public class NQUEENS_Tests
{
    [Fact]
    public void NQUEENS_Default_Constructor_Sets_N_To_4()
    {
        NQUEENS problem = new NQUEENS();
        Assert.Equal(4, problem.n);
        Assert.Equal("4", problem.defaultInstance);
    }

    [Fact]
    public void NQUEENS_Custom_Constructor_Sets_N_Correctly()
    {
        NQUEENS problem = new NQUEENS("8");
        Assert.Equal(8, problem.n);
        Assert.Equal("8", problem.instance);
    }

    [Fact]
    public void NQUEENS_Verifier_Validates_Single_Queen()
    {
        NQUEENS problem = new NQUEENS("1");
        NQueensVerifier verifier = new NQueensVerifier();

        bool result = verifier.verify(problem, "{(0,0)}");

        Assert.True(result);
    }

    [Fact]
    public void NQUEENS_Verifier_Validates_Known_4Queen_Solution()
    {
        NQUEENS problem = new NQUEENS("4");
        NQueensVerifier verifier = new NQueensVerifier();

        bool result = verifier.verify(problem, "{(0,1),(1,3),(2,0),(3,2)}");

        Assert.True(result);
    }

    [Fact]
    public void NQUEENS_Verifier_Rejects_Duplicate_Column()
    {
        NQUEENS problem = new NQUEENS("4");
        NQueensVerifier verifier = new NQueensVerifier();

        bool result = verifier.verify(problem, "{(0,1),(1,1),(2,0),(3,2)}");

        Assert.False(result);
    }

    [Fact]
    public void NQUEENS_Verifier_Rejects_Diagonal_Conflict()
    {
        NQUEENS problem = new NQUEENS("4");
        NQueensVerifier verifier = new NQueensVerifier();

        bool result = verifier.verify(problem, "{(0,0),(1,1),(2,2),(3,3)}");

        Assert.False(result);
    }

    [Fact]
    public void NQUEENS_Verifier_Rejects_Out_Of_Bounds()
    {
        NQUEENS problem = new NQUEENS("4");
        NQueensVerifier verifier = new NQueensVerifier();

        bool result = verifier.verify(problem, "{(0,1),(1,3),(2,0),(4,2)}");

        Assert.False(result);
    }

    [Fact]
    public void NQUEENS_Verifier_Rejects_Wrong_Number_Of_Queens()
    {
        NQUEENS problem = new NQUEENS("4");
        NQueensVerifier verifier = new NQueensVerifier();

        bool result = verifier.verify(problem, "{(0,1),(1,3),(2,0)}");

        Assert.False(result);
    }

    [Fact]
    public void NQUEENS_Verifier_Rejects_Empty_Certificate()
    {
        NQUEENS problem = new NQUEENS("4");
        NQueensVerifier verifier = new NQueensVerifier();

        bool result = verifier.verify(problem, "");

        Assert.False(result);
    }

    [Fact]
    public void NQUEENS_Solver_Finds_Valid_Solution_For_4()
    {
        NQUEENS problem = new NQUEENS("4");
        NQueensBacktracking solver = new NQueensBacktracking();
        NQueensVerifier verifier = new NQueensVerifier();

        string certificate = solver.solve(problem);

        Assert.True(verifier.verify(problem, certificate));
    }

    [Fact]
    public void NQUEENS_Solver_Returns_No_Solution_For_2()
    {
        NQUEENS problem = new NQUEENS("2");
        NQueensBacktracking solver = new NQueensBacktracking();

        string certificate = solver.solve(problem);

        Assert.Equal("", certificate);
    }

    [Fact]
    public void NQUEENS_Solver_Returns_No_Solution_For_3()
    {
        NQUEENS problem = new NQUEENS("3");
        NQueensBacktracking solver = new NQueensBacktracking();

        string certificate = solver.solve(problem);

        Assert.Equal("", certificate);
    }
}
