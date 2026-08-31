using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;

namespace API.Problems.NPComplete.NPC_HAMILTONIAN.Verifiers;

class HamiltonianVerifier : IVerifier<HAMILTONIAN> {

    // --- Fields ---
    public string verifierName { get; } = "Default Verifier";
    public string verifierDefinition { get; } = "This is a verifier for Hamiltonian Path";
    public string source { get; } = " ";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };


    private string _certificate = "";

    public string certificate {
        get {
            return _certificate;
        }
    }


    // --- Methods Including Constructors ---
    public HamiltonianVerifier() {

    }


    public bool verify(HAMILTONIAN problem, string certificate) {
        List<string> order = certificate.Replace("{", "").Replace("}", "").Split(',').ToList();
        List<string> check = new List<string>(problem.nodes);

        // Solvers repeat the starting node at the end (e.g. {1,2,3,4,5,1}).
        // Work on the deduplicated node sequence so the closing edge is always checked explicitly.
        int nodeCount = (order.Count > 1 && order[0] == order[order.Count - 1])
            ? order.Count - 1
            : order.Count;

        for (int i = 0; i < nodeCount; i++) {
            string from = order[i];
            string to = order[(i + 1) % nodeCount];
            KeyValuePair<string, string> pairCheck1 = new KeyValuePair<string, string>(from, to);
            KeyValuePair<string, string> pairCheck2 = new KeyValuePair<string, string>(to, from);
            if (!(problem.edges.Contains(pairCheck1) || problem.edges.Contains(pairCheck2))) {
                return false;
            }
            if (check.Contains(from)) {
                check.Remove(from);
            } else {
                return false;
            }
        }

        if (check.Any()) {
            return false;
        }

        return true;

    }
}