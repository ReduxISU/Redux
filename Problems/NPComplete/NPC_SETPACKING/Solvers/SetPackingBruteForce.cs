using API.Interfaces;
using System.Collections.Generic;

namespace API.Problems.NPComplete.NPC_SETPACKING.Solvers;

class SetPackingBruteForce : ISolver<SETPACKING>
{
    public string solverName { get; } = "Set Packing Brute Force";
    public string solverDefinition { get; } = "Brute force solver for Set Packing";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Sansar Kharal" };
    public bool timerHasExpired { get; set; }

    string ISolver<SETPACKING>.solve(SETPACKING problem)
    {
        List<string> setNames = problem.setNames;
        int n = setNames.Count;

        if (problem.K < 0 || problem.K > n)
        {
            return "{}";
        }

        List<int> combination = new();

        for (int i = 0; i < problem.K; i++)
        {
            combination.Add(i);
        }

        IVerifier<SETPACKING> verifier = problem.defaultVerifier;

        while (true)
        {
            string certificate = buildCertificate(combination, setNames);

            if (verifier.verify(problem, certificate))
            {
                return certificate;
            }

            if (!nextCombination(combination, n))
            {
                break;
            }
        }

        return "{}";
    }

    private string buildCertificate(List<int> indexes, List<string> setNames)
    {
        string result = "{";

        foreach (int index in indexes)
        {
            result += setNames[index] + ",";
        }

        return result.TrimEnd(',') + "}";
    }

    private bool nextCombination(List<int> combination, int n)
    {
        int k = combination.Count;

        for (int i = k - 1; i >= 0; i--)
        {
            if (combination[i] < n - k + i)
            {
                combination[i]++;

                for (int j = i + 1; j < k; j++)
                {
                    combination[j] = combination[j - 1] + 1;
                }

                return true;
            }
        }

        return false;
    }
}