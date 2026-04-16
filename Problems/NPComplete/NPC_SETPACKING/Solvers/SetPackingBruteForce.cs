using API.Interfaces;
using System.Collections.Generic;


namespace API.Problems.NPComplete.NPC_SETPACKING.Solvers;

public class SetPackingBruteForce : ISolver<API.Problems.NPComplete.NPC_SETPACKING.SETPACKING> {

    public string solverName { get; } = "Set Packing Brute Force";
    public string solverDefinition { get; } = "Brute force solver for Set Packing";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Sansar Kharal" };
    public bool timerHasExpired { get; set; }

    public string solve(SETPACKING problem)
    {
        List<string> sets = problem.setNames;
        int n = sets.Count;

        List<int> comb = new List<int>();
        for (int i = 0; i < problem.K; i++)
            comb.Add(i);

        while (true)
        {
            string cert = buildCertificate(comb, sets);

            if (problem.defaultVerifier.verify(problem, cert))
                return cert;

            if (!nextCombination(comb, n))
                break;
        }

        return "{}";
    }

    private string buildCertificate(List<int> idx, List<string> sets)
    {
        string res = "{";
        foreach (int i in idx)
            res += sets[i] + ",";
        return res.TrimEnd(',') + "}";
    }

    private bool nextCombination(List<int> comb, int n)
    {
        int k = comb.Count;

        for (int i = k - 1; i >= 0; i--)
        {
            if (comb[i] < n - k + i)
            {
                comb[i]++;
                for (int j = i + 1; j < k; j++)
                    comb[j] = comb[j - 1] + 1;
                return true;
            }
        }
        return false;
    }
} 