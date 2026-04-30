using API.Interfaces;
using System.Collections.Generic;

namespace API.Problems.NPComplete.NPC_SETPACKING.Solvers;

class SetPackingBruteForce : ISolver<SETPACKING>
{
    public string solverName { get; } = "Set Packing Brute Force";
    public string solverDefinition { get; } = "This is a brute force solver for the Set Packing problem.";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Sansar Kharal" };
    public bool timerHasExpired { get; set; }

    public SetPackingBruteForce()
    {
    }

    private long factorial(long x)
    {
        long y = 1;

        for (long i = 1; i <= x; i++)
        {
            y *= i;
        }

        return y;
    }

    private string indexListToCertificate(List<int> indexes, List<string> setNames)
    {
        string certificate = "";

        foreach (int i in indexes)
        {
            certificate += setNames[i] + ",";
        }

        certificate = certificate.TrimEnd(',');

        return "{" + certificate + "}";
    }

    private List<int> nextCombination(List<int> combination, int size)
    {
        for (int i = combination.Count - 1; i >= 0; i--)
        {
            if (combination[i] + 1 <= (i + size - combination.Count))
            {
                combination[i] += 1;

                for (int j = i + 1; j < combination.Count; j++)
                {
                    combination[j] = combination[j - 1] + 1;
                }

                return combination;
            }
        }

        return combination;
    }

    public string solve(SETPACKING setPacking)
    {
        if (setPacking.K < 0 || setPacking.K > setPacking.setNames.Count)
        {
            return "{}";
        }

        List<int> combination = new List<int>();

        for (int i = 0; i < setPacking.K; i++)
        {
            combination.Add(i);
        }

        long reps = factorial(setPacking.setNames.Count) /
                    (factorial(setPacking.K) * factorial(setPacking.setNames.Count - setPacking.K));

        for (int i = 0; i < reps; i++)
        {
            string certificate = indexListToCertificate(combination, setPacking.setNames);

            if (setPacking.defaultVerifier.verify(setPacking, certificate))
            {
                return certificate;
            }

            combination = nextCombination(combination, setPacking.setNames.Count);
        }

        return "{}";
    }
} 