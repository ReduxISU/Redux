using API.Interfaces;
using API.Problems.NPComplete.NPC_STRONGLYCONNECTEDCOMPONENTS;
using API.Problems.NPComplete.NPC_STRONGLYCONNECTEDCOMPONENTS.Solvers;

namespace API.Problems.NPComplete.NPC_STRONGLYCONNECTEDCOMPONENTS.Verifiers;

class SCCVerifier : IVerifier<STRONGLYCONNECTEDCOMPONENTS>
{
    public string verifierName { get; } = "Strongly Connected Components Verifier";
    public string verifierDefinition { get; } = "Verifies an SCC certificate by comparing it with the output produced by Kosaraju's Algorithm.";
    public string source { get; } = "https://en.wikipedia.org/wiki/Strongly_connected_component";
    public string[] contributors { get; } = { "Surendra Thapa", "Rohan Shrestha" };
    public string certificate { get; set; } = string.Empty;

    public bool verify(STRONGLYCONNECTEDCOMPONENTS problem, string certificate)
    {
        if (string.IsNullOrWhiteSpace(certificate))
            return false;

        var solver = new KosarajuSolver();
        string expected = solver.solve(problem);

        return Normalize(expected) == Normalize(certificate);
    }

    private static string Normalize(string value)
    {
        return value.Replace(" ", "")
                    .Replace("\n", "")
                    .Replace("\r", "")
                    .Replace("\t", "");
    }
}