using API.Interfaces;
using SPADE;

namespace API.Problems.NPComplete.NPC_NQUEENS.Verifiers;

class NQueensVerifier : IVerifier<NQUEENS> {

    // --- Fields ---
    public string verifierName { get; } = "N-Queens Verifier";
    public string verifierDefinition { get; } =
        "Checks that no two queens share the same row, column, or diagonal.";
    public string source { get; } = "Standard verification for N-Queens by checking row, column, and diagonal conflicts.";
    public string[] contributors { get; } = { "Cole Campbell", "Luis Hernandez", "Ethan Wilks" };

    private string _complexity = "O(n^2)";
    private string _certificate = "{(0,1),(1,3),(2,0),(3,2)}";

    public string complexity {
        get { return _complexity; }
    }

    public string certificate {
        get { return _certificate; }
    }

    // --- Verifier ---
    public bool verify(NQUEENS problem, string certificate) {
        UtilCollection solution;

        try {
            solution = parseCertificate(certificate);
        }
        catch {
            return false;
        }

        int n = problem.n;

        if (solution.Count() != n) return false;

        int[] rows = new int[n];
        int[] cols = new int[n];

        List<(int r, int c)> queens = new List<(int, int)>();

        foreach (UtilCollection pair in solution) {
            pair.assertPair();

            int r = pair[0].parseInt();
            int c = pair[1].parseInt();

            if (r < 0 || r >= n || c < 0 || c >= n) return false;

            rows[r]++;
            cols[c]++;

            queens.Add((r, c));
        }

        // Check one queen per row and column
        for (int i = 0; i < n; i++) {
            if (rows[i] != 1 || cols[i] != 1) return false;
        }

        // Check diagonals
        for (int i = 0; i < queens.Count; i++) {
            for (int j = i + 1; j < queens.Count; j++) {
                if (Math.Abs(queens[i].r - queens[j].r) ==
                    Math.Abs(queens[i].c - queens[j].c)) {
                    return false;
                }
            }
        }

        return true;
    }

    private UtilCollection parseCertificate(string certificate) {
        return new UtilCollection(certificate);
    }
}
