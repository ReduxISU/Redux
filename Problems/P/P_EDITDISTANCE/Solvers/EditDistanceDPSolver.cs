using System;
using System.Collections.Generic;
using System.Linq;
using API.Interfaces;
using API.Problems.P.P_EDITDISTANCE;
using SPADE;


namespace API.Problems.P.P_EDITDISTANCE.Solvers;

class EditDistanceDPSolver : ISolver<EDITDISTANCE> {
    public string solverName { get; } = "Dynamic Programming Edit Distance Solver";
    public string solverDefinition { get; } = "Finds the edit distance between two strings using dynamic programming.";
    public string source { get; } = "https://en.wikipedia.org/wiki/Edit_distance";
    public string[] contributors { get; } = { "Kaosi Ibeabuchi", "Diya Pandey", "Srijan Pant" };

    public bool timerHasExpired { get; set; } = false;

     public EditDistanceDPSolver() {}

    public string solve(EDITDISTANCE problem) {
        string s1 = problem.sourceString;
        string s2 = problem.targetString;

        int m = s1.Length;
        int n = s2.Length;

        int[,] dp = new int[m + 1, n + 1];

        for (int i = 0; i <= m; i++) dp[i, 0] = i;
        for (int j = 0; j <= n; j++) dp[0, j] = j;

        for (int i = 1; i <= m; i++)
        {
            for (int j = 1; j <= n; j++)
            {
                if (s1[i - 1] == s2[j - 1])
                {
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
    
