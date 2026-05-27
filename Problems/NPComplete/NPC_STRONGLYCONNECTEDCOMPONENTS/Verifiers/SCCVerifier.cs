using API.Interfaces;
using API.Problems.NPComplete.NPC_STRONGLYCONNECTEDCOMPONENTS;
using API.Problems.NPComplete.NPC_STRONGLYCONNECTEDCOMPONENTS.Solvers;

namespace API.Problems.NPComplete.NPC_STRONGLYCONNECTEDCOMPONENTS.Verifiers;

class SCCVerifier : IVerifier<STRONGLYCONNECTEDCOMPONENTS>
{
    public string verifierName { get; } = "Strongly Connected Components Verifier";

    public string verifierDefinition { get; } =
        "Verifies an SCC certificate by comparing components as unordered sets, since SCC ordering is not unique.";

    public string source { get; } =
        "Swati Dhingra, Poorvi S. Dodwad, and Meghna Madan, \"Finding Strongly Connected Components in a Social Network Graph,\" International Journal of Computer Applications, Volume 136, No. 7, February 2016.";

    public string sourceLink { get; } =
        "https://ijcaonline.org/research/volume136/number7/dhingra-2016-ijca-908481.pdf";

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