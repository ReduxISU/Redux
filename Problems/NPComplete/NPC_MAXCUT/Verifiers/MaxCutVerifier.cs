using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;

namespace API.Problems.NPComplete.NPC_MAXCUT.Verifiers;

class MaxCutVerifier : IVerifier<MAXCUT> {

    // --- Fields ---
    public string verifierName {get;} = "Max Cut Verifier";
    public string verifierDefinition {get;} = "Verifies that the certificate is a valid non-trivial partition S of the graph's vertices (all nodes in S belong to the graph and appear exactly once, and S is neither empty nor the full node set).";
    public string source {get;} = "";
    public string[] contributors {get;} = {"Max Gruenwoldt", "Eric Hill"};

    private string _certificate = "";

    public string certificate {
        get { return _certificate; }
    }

    public MaxCutVerifier() { }

    public bool verify(MAXCUT problem, string certificate) {
        if (string.IsNullOrWhiteSpace(certificate) || certificate == "{}")
            return false;

        List<string> S = GraphParser.parseNodeListWithStringFunctions(certificate)
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .ToList();

        if (S.Count == 0) return false;

        // Every node in S must exist in the graph.
        foreach (string node in S)
            if (!problem.nodes.Contains(node)) return false;

        // No duplicate nodes in S.
        if (S.Count != S.Distinct().Count()) return false;

        // S must be a proper subset (not empty, not the entire vertex set).
        if (S.Count == problem.nodes.Count) return false;

        return true;
    }
}
