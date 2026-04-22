using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Problems.NPComplete.NPC_DFS.Solvers;

namespace API.Problems.NPComplete.NPC_DFS.Verifiers;

class DFSVerifier : IVerifier<DFS>
{
    public string verifierName { get; } = "Depth-First Search Verifier";
    public string verifierDefinition { get; } = "Verifies that a certificate matches the path returned by depth-first search.";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Scott Barfuss" };
    private string _certificate = "";
    public string certificate => _certificate;

    public DFSVerifier() { }

    public bool verify(DFS problem, string solution)
    {
        _certificate = solution ?? "";

        List<string> expectedPath = DFSSolver.Traverse(problem).Path;
        if (string.IsNullOrWhiteSpace(solution) || solution.Trim() == "{}")
            return expectedPath.Count == 0;

        List<string> actualPath;
        try
        {
            actualPath = GraphParser.parseNodeListWithStringFunctions(solution)
                .Select(node => node.Trim())
                .Where(node => node.Length > 0)
                .ToList();
        }
        catch
        {
            return false;
        }

        if (actualPath.Count != expectedPath.Count)
            return false;

        for (int i = 0; i < actualPath.Count; i++)
        {
            if (actualPath[i] != expectedPath[i])
                return false;
        }

        return true;
    }
}
