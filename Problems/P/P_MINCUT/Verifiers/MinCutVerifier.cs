using API.Interfaces;
using API.Problems.P.P_MINCUT.Solvers;
using SPADE;

namespace API.Problems.P.P_MINCUT.Verifiers;

class MinCutVerifier : IVerifier<MINCUT> {
    public string verifierName { get; } = "Minimum Cut Verifier";
    public string verifierDefinition { get; } = "Verifies that a proposed set of edges is a valid minimum cut of the graph by checking all edges exist, then comparing the total weight to the true minimum cut weight from Stoer-Wagner.";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Michael Trosper" };
    private string _certificate = "";
    public string certificate => _certificate;

    public MinCutVerifier() { }

    public bool verify(MINCUT problem, string certificate) {
        _certificate = certificate ?? "";
        int minCutWeight = MinCutStoerWagner.GetMinCutWeight(problem);

        if (string.IsNullOrWhiteSpace(certificate) || certificate.Trim() == "{}")
            return minCutWeight == 0;

        UtilCollection edgeList;
        try { edgeList = new UtilCollection(certificate); } catch { return false; }

        int totalWeight = 0;
        foreach (UtilCollection item in edgeList) {
            try {
                List<UtilCollection> endpoints = item[0].ToList();
                string src = endpoints[0].ToString();
                string dst = endpoints[1].ToString();
                int wt = int.Parse(item[1].ToString());

                bool exists = problem.edges.Any(e =>
                    (e.source == src && e.destination == dst && e.weight == wt) ||
                    (e.source == dst && e.destination == src && e.weight == wt));
                if (!exists) return false;

                totalWeight += wt;
            } catch { return false; }
        }

        return totalWeight == minCutWeight;
    }
}
