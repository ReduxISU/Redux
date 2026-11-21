
using API.Interfaces;

namespace API.Problems.NPComplete.NPC_MINESWEEPER.Verifiers;

class MinesweeperVerifier : IVerifier<MINESWEEPER> {

    public const string CertificateGrammar =
        "A binary grid of 0s and 1s, with the same dimensions as the instance; " +
        "cells separated by commas and rows by semicolons. " +
        "1 indicates a mine and 0 indicates a safe cell.";

    public const string CertificateExample =
        "0,0,0;1,0,1;0,0,0";

    public string verifierName { get; } = "Minesweeper Consistency Solution Verifier";

    public string verifierDefinition { get; } =
        "Verifies that a mine placement is consistent with all revealed " +
        "Minesweeper clues.";

    public string source { get; } =
        "Kaye, Richard. 2000. Minesweeper is NP-complete. " +
        "The Mathematical Intelligencer 22(2): 9-15.";

    public string sourceFile { get; } = SourceFile.Path();

    public string[] contributors { get; } = { "Prashant Pant" };

    public string certificate { get; } = string.Empty;

    public MinesweeperVerifier() { }

    public bool verify(MINESWEEPER problem, string certificate) {
        if (string.IsNullOrWhiteSpace(certificate))
            return false;

        string[] rows = certificate.ReplaceLineEndings(string.Empty)
            .Split(';');

        if (rows.Length != problem.Rows)
            return false;

        int[][] mines = new int[problem.Rows][];

        for (int r = 0; r < problem.Rows; r++) {
            string[] cells = rows[r].Split(',');

            if (cells.Length != problem.Columns)
                return false;

            mines[r] = new int[problem.Columns];

            for (int c = 0; c < problem.Columns; c++) {
                string value = cells[c].Trim();

                if (value != "0" && value != "1")
                    return false;

                mines[r][c] = value == "1" ? 1 : 0;

                // Revealed clue cells cannot contain mines.
                if (problem.grid[r][c] != -1 && mines[r][c] == 1)
                    return false;
            }
        }

        // Verify that every numbered clue is satisfied.
        for (int r = 0; r < problem.Rows; r++) {
            for (int c = 0; c < problem.Columns; c++) {
                int clue = problem.grid[r][c];

                if (clue == -1)
                    continue;

                int adjacentMines = 0;

                for (int dr = -1; dr <= 1; dr++) {
                    for (int dc = -1; dc <= 1; dc++) {
                        if (dr == 0 && dc == 0)
                            continue;

                        int nr = r + dr;
                        int nc = c + dc;

                        if (nr >= 0 && nr < problem.Rows &&
                            nc >= 0 && nc < problem.Columns) {
                            adjacentMines += mines[nr][nc];
                        }
                    }
                }

                if (adjacentMines != clue)
                    return false;
            }
        }

        return true;
    }
}
