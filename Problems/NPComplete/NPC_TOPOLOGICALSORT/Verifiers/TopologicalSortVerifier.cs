using API.Interfaces;

namespace API.Problems.NPComplete.NPC_TOPOLOGICALSORT.Verifiers;

class TopologicalSortVerifier : IVerifier<TOPOLOGICALSORT> {
    // --- Fields ---
    public string verifierName { get; } = "Topological Order Verifier";
    public string verifierDefinition { get; } = "Given a proposed ordering of vertices, the verifier checks that (1) every vertex in the graph appears exactly once in the ordering, and (2) for every directed edge (u,v) in the graph, u appears before v in the ordering.";
    public string source { get; } = "Kahn, A. B. (1962). Topological sorting of large networks. Communications of the ACM, 5(11), 558-562.";
    public string sourceLink { get; } = "https://dl.acm.org/doi/10.1145/368996.369025";
    public string[] contributors { get; } = { "Pravesh Aryal", "Koras Koirala", "Dinesh Khanal" };
    private string _certificate = "";
    public string certificate { get { return _certificate; } }

    // --- Constructors ---
    public TopologicalSortVerifier() { }

    // --- Methods ---
    public bool verify(TOPOLOGICALSORT problem, string certificate) {
        // Parse the certificate into an ordered list of nodes
        List<string> order = certificate.Replace("{", "").Replace("}", "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

        // Check 1: every node appears exactly once
        if (order.Count != problem.nodes.Count)
            return false;

        List<string> check = new List<string>(problem.nodes);
        foreach (string node in order) {
            if (!check.Contains(node))
                return false;
            check.Remove(node);
        }
        if (check.Any())
            return false;

        // Check 2: for every edge (u,v), u appears before v
        Dictionary<string, int> position = new Dictionary<string, int>();
        for (int i = 0; i < order.Count; i++)
            position[order[i]] = i;

        foreach (var edge in problem.edges) {
            if (!position.ContainsKey(edge.Key) || !position.ContainsKey(edge.Value))
                return false;
            if (position[edge.Key] >= position[edge.Value])
                return false;
        }

        return true;
    }
}
