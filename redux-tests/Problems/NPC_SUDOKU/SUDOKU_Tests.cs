using Xunit;
using API.Problems.NPComplete.NPC_SUDOKU;
using API.Problems.NPComplete.NPC_SUDOKU.Verifiers;
using API.Problems.NPComplete.NPC_SUDOKU.Solvers;

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

public class SUDOKU_Tests
{
	private readonly SudokuVerifier _verifier = new();

	#region Valid Solution Tests

	[Fact]
	public void Verify_ValidCompleteSudoku_ReturnsTrue()
	{
		// Arrange
		var problem = new SUDOKU
		{
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
	public void Verify_SolutionWithEmptyCell_ReturnsFalse()
	{
		// Arrange
		var problem = new SUDOKU
		{
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
	public void Verify_SolutionWithValueGreaterThan9_ReturnsFalse()
	{
		// Arrange
		var problem = new SUDOKU
		{
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
	public void Verify_SolutionWithNegativeValue_ReturnsFalse()
	{
		// Arrange
		var problem = new SUDOKU
		{
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
	public void Verify_SolutionDoesNotMatchInitialClues_ReturnsFalse()
	{
		// Arrange
		var problem = new SUDOKU
		{
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
	public void Verify_SolutionWithDuplicateInRow_ReturnsFalse()
	{
		// Arrange
		var problem = new SUDOKU
		{
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
	public void Verify_SolutionWithDuplicateInColumn_ReturnsFalse()
	{
		// Arrange
		var problem = new SUDOKU
		{
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
	public void Verify_SolutionWithDuplicateIn3x3Block_ReturnsFalse()
	{
		// Arrange
		var problem = new SUDOKU
		{
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
}


//Putting code being tested here for ease of testing. Will move to correct file once tests are working
/*

*/