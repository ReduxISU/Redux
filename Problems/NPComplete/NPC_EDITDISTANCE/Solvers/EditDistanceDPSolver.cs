using System;
using System.Collections.Generic;
using System.Linq;
using API.Interfaces;
using API.Problems.NPComplete.NPC_EDITDISTANCE;
using SPADE;


namespace API.Problems.NPComplete.NPC_EDITDISTANCE.Solvers;

class EditDistanceDPSolver : ISolver<EDITDISTANCE> {
        public string solverName { get; } = "Dynamic Programming Edit Distance Solver";
        public string solverDefinition { get; } = "Finds the edit distance between two strings using dynamic programming.";
        public string source { get; } = "R. A. Wagner and M. J. Fischer, “The String-to-String Correction Problem,” Journal of the ACM, vol. 21, no. 1, pp. 168-173, Jan. 1974";
        public string sourceLink { get; } = "https://dl.acm.org/doi/10.1145/321796.321811";
        public string[] contributors { get; } = { "Kaosi Ibeabuchi", "Diya Pandey", "Srijan Pant" };

        public bool timerHasExpired { get; set; }
        // Declared, not derived. Memo table + optimal-substructure recurrence.
        public SolverType solverType { get; } = SolverType.DynamicProgramming;
        public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Polynomial;
        // Fills the full (m+1) x (n+1) DP table with one O(1) transition per cell.
        public string complexity { get; } = "O(m * n)";

        public EditDistanceDPSolver() { }

        public string solve(EDITDISTANCE problem) {
                string s1 = problem.sourceString;
                string s2 = problem.targetString;

                int m = s1.Length;
                int n = s2.Length;

                int[,] dp = new int[m + 1, n + 1];

                for (int i = 0; i <= m; i++) dp[i, 0] = i;
                for (int j = 0; j <= n; j++) dp[0, j] = j;

                for (int i = 1; i <= m; i++) {
                        for (int j = 1; j <= n; j++) {
                                if (s1[i - 1] == s2[j - 1]) {
                                        dp[i, j] = dp[i - 1, j - 1];
                                }
                                else
                                        dp[i, j] = 1 + Math.Min(dp[i - 1, j - 1], // substitute
                                                     Math.Min(dp[i - 1, j],        // delete
                                                     dp[i, j - 1]));            // insert
                        }
                }

                return dp[m, n].ToString();
        }
}

