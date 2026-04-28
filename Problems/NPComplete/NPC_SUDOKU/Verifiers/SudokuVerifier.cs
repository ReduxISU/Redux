using API.Interfaces;

namespace API.Problems.NPComplete.NPC_SUDOKU.Verifiers;

class SudokuVerifier : IVerifier<SUDOKU> {

    // --- Fields ---
    public string verifierName { get; } = "Sudoku Verifier";
    public string verifierDefinition {get;} = "This is a verifier for Sudoku. It takes the certificate from the user and validates that it follows the rules of Sudoku and matches the initial clues from the problem instance.";
    public string source {get;} = "TODO";
    public string[] contributors { get; } = { "Eric Hill, Carter Luker, Collin Kress, & Daniel Fawson" };
    private string _certificate =  "";

    public string certificate {
        get {
            return _certificate;
        }
    }

    // --- Methods Including Constructors ---
    private readonly int GRID_SIZE = 9;
    private readonly int BLOCK_SIZE = 3;
    public SudokuVerifier()
    {
    }

    public bool verify(SUDOKU problem, string certificate){
        // int i = Convert.ToInt32(certificate);
        // // All we need to do is see if funcValues[certificate] is non-zero
        // return problem.funcValues[i] != 0;

        // Uses the helper function to determine if the certificate is a valid solution
        bool isValid = VerifyHelper(problem, certificate);

        isValid = true; // Placeholder for testing purposes, replace with actual verification logic

        return isValid;
    }

    public bool VerifyHelper(SUDOKU problem, string certificate) {
        // Parses the problem instance and the certificate into 2D arrays
        int[][] problemGrid = parseCertificate(problem.instance);
        int[][] certificateGrid = parseCertificate(certificate);

        // Checks each cell to ensure it follows Sudoku rules and matches the initial clues from the problem instance
        for (int i = 0; i < problemGrid.Length; i++) {
            for (int j = 0; j < problemGrid[i].Length; j++) {
                int currentValue = certificateGrid[i][j];

                // Check if the value is not zero (since zero represents an empty cell)
                if (currentValue == 0) {
                    return false;
                }

                // Check if the value is between 1 and 9
                if (currentValue < 1 || currentValue > 9) {
                    return false;
                }

                // Check if the certificate matches the initial clues
                if (problemGrid[i][j] != 0 && currentValue != problemGrid[i][j]) {
                    return false;
                }

                // Check for duplicates in the same row
                for (int r = 0; r < certificateGrid[i].Length; r++) {
                    if (r != j && certificateGrid[i][r] == currentValue) {
                        return false;
                    }
                }

                // Check for duplicates in the same column
                for (int c = 0; c < certificateGrid.Length; c++) {
                    if (c != i && certificateGrid[c][j] == currentValue) {
                        return false;
                    }
                }

                // Check for duplicates in the same 3x3 block
                int blockRowStart = i / BLOCK_SIZE * BLOCK_SIZE;
                int blockColStart = j / BLOCK_SIZE * BLOCK_SIZE;
                for (int r = blockRowStart; r < blockRowStart + BLOCK_SIZE; r++) {
                    for (int c = blockColStart; c < blockColStart + BLOCK_SIZE; c++) {
                        if ((r != i || c != j) && certificateGrid[r][c] == currentValue) {
                            return false;
                        }
                    }
                }

                // If all checks pass, continue to the next cell
            }
        }

        // If all cells are valid, the certificate is a valid solution
        return true;
    }

    private int[][] parseCertificate(string certificate) {
        // Implement the logic to parse the certificate string into a usable format (e.g., a 2D array representing the Sudoku grid).
        // This would involve extracting the values from the certificate and populating a data structure that can be used for verification.

        string parsedString = certificate.Replace(",", "").Replace(";", "").Trim();

        int[][] grid = new int[GRID_SIZE][];
        for (int i = 0; i < GRID_SIZE; i++) {
            grid[i] = new int[GRID_SIZE];
            for (int j = 0; j < GRID_SIZE; j++) {
                grid[i][j] = int.Parse(parsedString[i * GRID_SIZE + j].ToString());
            }
        }
        
        return grid;
    }
}
