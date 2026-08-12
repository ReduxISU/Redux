using API.Interfaces;

namespace API.Problems.NPComplete.NPC_SUDOKU.Verifiers;

class SudokuVerifier : IVerifier<SUDOKU> {

        // --- Fields ---
        public string verifierName { get; } = "Sudoku Verifier";
        public string verifierDefinition { get; } = "This is a verifier for Sudoku. It takes the certificate from the user and validates that it follows the rules of Sudoku and matches the initial clues from the problem instance.";
        public string source { get; } = "Bhattarai, Apekshya, Dinisha Uprety, Pooja Pathak, Safal Shrestha, Salina Narkarmi, and Sanjog Sigdel. 2025. “A Study of Sudoku Solving Algorithms: Backtracking and Heuristic.” Department of Computer Science, Kathmandu University.";
        public string[] contributors { get; } = { "Eric Hill, Carter Luker, Collin Kress, & Daniel Fawson" };
        private readonly string _certificate = "";

        public string certificate {
                get {
                        return _certificate;
                }
        }

        // --- Methods Including Constructors ---
        private int GRID_SIZE;
        private int BLOCK_SIZE;
        public SudokuVerifier() {

        }

        public bool verify(SUDOKU problem, string certificate) {
                // int i = Convert.ToInt32(certificate);
                // // All we need to do is see if funcValues[certificate] is non-zero
                // return problem.funcValues[i] != 0;

                GRID_SIZE = problem.grid.Length;
                BLOCK_SIZE = (int)Math.Sqrt(GRID_SIZE);

                // Uses the helper function to determine if the certificate is a valid solution
                bool isValid = VerifyHelper(problem, certificate);

                //isValid = true; // Placeholder for testing purposes, replace with actual verification logic

                return isValid;
        }

        public bool VerifyHelper(SUDOKU problem, string certificate) {
                // Parses the problem instance and the certificate into 2D arrays
                int[][] problemGrid = ParseSudokuInput(problem.instance);
                int[][] certificateGrid = ParseSudokuInput(certificate);

                // Checks each cell to ensure it follows Sudoku rules and matches the initial clues from the problem instance
                for (int i = 0; i < problemGrid.Length; i++) {
                        for (int j = 0; j < problemGrid[i].Length; j++) {
                                int currentValue = certificateGrid[i][j];

                                // Check if the value is not zero (since zero represents an empty cell)
                                if (currentValue == 0) {
                                        return false;
                                }

                                // Check if the value is between 1 and GRID_SIZE
                                if (currentValue < 1 || currentValue > GRID_SIZE) {
                                        return false;
                                }

                                // Check if the certificate matches the initial clues
                                if (problemGrid[i][j] != 0 && currentValue != problemGrid[i][j]) {
                                        return false;
                                }

                                // Check for duplicates in the same row
                                if (isDuplicateInRow(certificateGrid, i, j, currentValue)) {
                                        return false;
                                }

                                // Check for duplicates in the same column
                                if (isDuplicateInColumn(certificateGrid, i, j, currentValue)) {
                                        return false;
                                }

                                // Check for duplicates in the same 3x3 block
                                int blockRowStart = i / BLOCK_SIZE * BLOCK_SIZE;
                                int blockColStart = j / BLOCK_SIZE * BLOCK_SIZE;
                                if (isDuplicateInBlock(certificateGrid, blockRowStart, blockColStart, currentValue, i, j)) {
                                        return false;
                                }

                                // If all checks pass, continue to the next cell
                        }
                }

                // If all cells are valid, the certificate is a valid solution
                return true;
        }

        private static int[][] ParseSudokuInput(string input) {
                // Implement the logic to parse the input string into a usable format (e.g., a 2D array representing the Sudoku grid).
                // This would involve extracting the values from the input and populating a data structure that can be used for verification.

                /*string parsedString = input.Replace(",", "").Replace(";", "").Trim();

                int[][] grid = new int[GRID_SIZE][];
                for (int i = 0; i < GRID_SIZE; i++) {
                    grid[i] = new int[GRID_SIZE];
                    for (int j = 0; j < GRID_SIZE; j++) {
                        grid[i][j] = int.Parse(parsedString[i * GRID_SIZE + j].ToString());
                    }
                }

                return grid;*/

                input = input.ReplaceLineEndings(string.Empty);

                var rows = input.Split(';', StringSplitOptions.RemoveEmptyEntries);

                int[][] grid = new int[rows.Length][];

                for (int i = 0; i < rows.Length; i++) {
                        var nums = rows[i].Split(',', StringSplitOptions.RemoveEmptyEntries);
                        grid[i] = Array.ConvertAll(nums, int.Parse);
                }
                return grid;
        }

        private static bool isDuplicateInRow(int[][] grid, int row, int col, int value) {
                for (int c = 0; c < grid[row].Length; c++) {
                        if (c != col && grid[row][c] == value) {
                                return true;
                        }
                }
                return false;
        }

        private static bool isDuplicateInColumn(int[][] grid, int row, int col, int value) {
                for (int r = 0; r < grid.Length; r++) {
                        if (r != row && grid[r][col] == value) {
                                return true;
                        }
                }
                return false;
        }

        private bool isDuplicateInBlock(int[][] grid, int blockRowStart, int blockColStart, int value, int row, int col) {
                for (int r = blockRowStart; r < blockRowStart + BLOCK_SIZE; r++) {
                        for (int c = blockColStart; c < blockColStart + BLOCK_SIZE; c++) {
                                if ((r != row || c != col) && grid[r][c] == value) {
                                        return true;
                                }
                        }
                }
                return false;
        }
}
