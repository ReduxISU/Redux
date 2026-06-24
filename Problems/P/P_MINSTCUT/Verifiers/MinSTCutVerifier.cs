using API.Interfaces;
using API.Problems.P.P_MINSTCUT.Solvers;

namespace API.Problems.P.P_MINSTCUT.Verifiers;

class MinSTCutVerifier : IVerifier<MINSTCUT>
{
    public string verifierName { get; } = "Minimum S-T Cut Verifier";
    public string verifierDefinition { get; } = "Verifies that the certificate represents a valid S side of a minimum S-T cut: source is in S, target is not in S, all nodes exist, no duplicates, and the cut capacity equals the true minimum cut capacity computed by Edmonds-Karp.";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Michael Trosper" };

    private string _certificate = "";
    public string certificate => _certificate;

    public MinSTCutVerifier() { }

    public bool verify(MINSTCUT problem, string certificate)
    {
        _certificate = certificate ?? "";

        if (string.IsNullOrWhiteSpace(_certificate) || _certificate.Trim() == "{}")
            return false;

        List<string> S = _certificate.Trim()
            .TrimStart('{').TrimEnd('}')
            .Split(',')
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();

        if (S.Count == 0) return false;

        if (S.Count != S.Distinct().Count()) return false;

        foreach (string node in S)
            if (!problem.nodes.Contains(node)) return false;

        HashSet<string> sSet = new(S);

        if (!sSet.Contains(problem.sourceNode)) return false;
        if (sSet.Contains(problem.targetNode)) return false;

        int cutCapacity = MinSTCutSolver.CutCapacity(problem.edges, sSet);
        int minCutCapacity = MinSTCutSolver.GetMinCutCapacity(problem);

        return cutCapacity == minCutCapacity;
    }
}
