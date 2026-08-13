using API.Interfaces;
using API.Problems.NPComplete.NPC_MAXCUT.Solvers;

namespace API.Problems.NPComplete.NPC_MAXCUT.Verifiers;

class MaxCutVerifier : IVerifier<MAXCUT> {
    public string verifierName { get; } = "Max Cut Verifier";
    public string verifierDefinition { get; } = "Verifies that the certificate represents a valid non-trivial partition S whose crossing-edge weight equals the true maximum cut weight computed by brute force enumeration.";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Max Gruenwoldt", "Eric Hill", "Michael Trosper" };

    private string _certificate = "";
    public string certificate => _certificate;

    public MaxCutVerifier() { }

    public bool verify(MAXCUT problem, string certificate) {
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

        // All nodes in S must exist in the graph.
        foreach (string node in S)
            if (!problem.nodes.Contains(node)) return false;

        // No duplicate nodes in S.
        if (S.Count != S.Distinct().Count()) return false;

        // S must be a proper subset (not the entire vertex set).
        if (S.Count == problem.nodes.Count) return false;

        // Cut weight for this S must equal the true maximum cut weight.
        HashSet<string> sSet = new(S);
        int cutWeight = MaxCutSolver.CutWeight(problem.edges, sSet);
        int maxCutWeight = MaxCutSolver.GetMaxCutWeight(problem);

        return cutWeight == maxCutWeight;
    }
}
