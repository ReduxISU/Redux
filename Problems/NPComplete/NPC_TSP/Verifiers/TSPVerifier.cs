using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;

namespace API.Problems.NPComplete.NPC_TSP.Verifiers;

class TSPVerifier : IVerifier<TSP> {

    // --- Fields ---
    public string verifierName { get; } = "Traveling Sales Person Verifier";
    public string verifierDefinition { get; } = "This is a verifier for the Traveling Sales Person problem";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };


    private string _certificate = "";

    public string certificate {
        get {
            return _certificate;
        }
    }


    // --- Methods Including Constructors ---
    public TSPVerifier() {

    }


    public bool verify(TSP problem, string certificate) {
        List<string> order = certificate.Replace("{", "").Replace("}", "").Split(',').ToList();
        int sum = 0;

        // Determine the effective node count — solvers repeat the starting node at the end
        // (e.g. {A,B,C,D,A}), so clamp the loop to avoid checking A→A.
        int nodeCount = (order.Count > 1 && order[0] == order[order.Count - 1])
            ? order.Count - 1
            : order.Count;

        for (int i = 0; i < nodeCount; i++) {
            string from = order[i];
            string to = order[(i + 1) % nodeCount];
            bool check1 = problem.edges.Any(tuple => tuple.source == from && tuple.target == to);
            bool check2 = problem.edges.Any(tuple => tuple.source == to && tuple.target == from);
            if (check1) {
                var matchingTuple = problem.edges.FirstOrDefault(tuple => tuple.Item1 == from && tuple.Item2 == to);
                if (matchingTuple != default) {
                    sum += matchingTuple.Item3;
                }
            } else if (check2) {
                var matchingTuple = problem.edges.FirstOrDefault(tuple => tuple.Item1 == to && tuple.Item2 == from);
                if (matchingTuple != default) {
                    sum += matchingTuple.Item3;
                }
            } else {
                return false;
            }
        }

        if (sum <= problem.K) return true;

        return false;

    }
}