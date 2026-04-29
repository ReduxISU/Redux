using API.Interfaces;
<<<<<<< HEAD:Problems/NPComplete/NPC_STRONGLYCONNECTEDCOMPONENTS/Verifiers/SCCVerifier.cs
using API.Problems.NPComplete.NPC_STRONGLYCONNECTEDCOMPONENTS;
using API.Problems.NPComplete.NPC_STRONGLYCONNECTEDCOMPONENTS.Solvers;
=======
using API.Problems.P.P_STRONGLYCONNECTEDCOMPONENTS;
using API.Problems.P.P_STRONGLYCONNECTEDCOMPONENTS.Solvers;
using System.Collections.Generic;
using System.Linq;
>>>>>>> 3f41273 (Address SCC review feedback):Problems/P/P_STRONGLYCONNECTEDCOMPONENTS/Verifiers/SCCVerifier.cs

namespace API.Problems.NPComplete.NPC_STRONGLYCONNECTEDCOMPONENTS.Verifiers;

class SCCVerifier : IVerifier<STRONGLYCONNECTEDCOMPONENTS>
{
    public string verifierName { get; } = "Strongly Connected Components Verifier";
    public string verifierDefinition { get; } = "Verifies an SCC certificate by comparing components as unordered sets.";
    public string source { get; } = "https://en.wikipedia.org/wiki/Strongly_connected_component";
    public string[] contributors { get; } = { "Surendra Thapa", "Rohan Shrestha" };
    public string certificate { get; set; } = string.Empty;

    public bool verify(STRONGLYCONNECTEDCOMPONENTS problem, string certificate)
    {
        if (string.IsNullOrWhiteSpace(certificate))
            return false;

        var solver = new KosarajuSolver();
        string expected = solver.solve(problem);

        return NormalizeSccs(expected).SetEquals(NormalizeSccs(certificate));
    }

    private static HashSet<string> NormalizeSccs(string value)
    {
        var components = new HashSet<string>();
        var clean = value.Replace(" ", "")
                         .Replace("\n", "")
                         .Replace("\r", "")
                         .Replace("\t", "");

        foreach (var part in clean.Split("},{"))
        {
            var trimmed = part.Replace("{", "").Replace("}", "");

            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            var nodes = trimmed.Split(",")
                               .Where(x => !string.IsNullOrWhiteSpace(x))
                               .OrderBy(x => x)
                               .ToList();

            components.Add(string.Join(",", nodes));
        }

        return components;
    }
}