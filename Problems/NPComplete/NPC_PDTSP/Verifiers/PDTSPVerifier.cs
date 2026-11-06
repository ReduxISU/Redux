using API.Interfaces;

namespace API.Problems.NPComplete.NPC_PDTSP.Verifiers;

[DefaultFor(typeof(PDTSP))]
class PDTSPVerifier : IVerifier<PDTSP> {
    public const string CertificateGrammar = "{S,v1,...,vn,S} | ordered depot-to-depot tour visiting every nondepot node exactly once, with each pickup before its delivery and total cost <= K";
    public const string CertificateExample = "{S,P1,D1,P2,D2,S}";
    public string verifierName { get; } = "Pickup and Delivery Traveling Salesperson Solution Verifier";
    public string verifierDefinition { get; } = "Checks depot endpoints, exact vertex coverage, pickup-before-delivery precedence, existing edges, and the inclusive cost bound. Does not claim optimality.";
    public string source { get; } = PDTSP.Reference;
    public string sourceLink { get; } = PDTSP.ReferenceLink;
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = Array.Empty<string>();
    public string certificate { get; private set; } = "";

    public bool verify(PDTSP problem, string certificate) {
        this.certificate = certificate;
        string text = certificate?.Trim() ?? "";
        if (text.Length < 2 || text[0] != '{' || text[^1] != '}')
            throw new CertificateParseException(problem, certificate!, "Expected a brace-delimited ordered tour.");
        string body = text[1..^1];
        if (string.IsNullOrWhiteSpace(body))
            return false;
        string[] order = body.Split(',').Select(label => label.Trim()).ToArray();
        if (order.Any(label => label.Length == 0 || label.IndexOfAny(new[] { '{', '}', '(', ')' }) >= 0))
            throw new CertificateParseException(problem, certificate!, "Tour labels must be nonempty scalar values.");

        int n = problem.nodes.Count;
        if (order.Length != n + 1 || order[0] != problem.depot || order[^1] != problem.depot)
            return false;

        bool[] visited = new bool[n];
        long cost = 0;
        int previous = problem.DepotIndex;
        for (int i = 0; i < n; i++) {
            if (!problem.NodeIndices.TryGetValue(order[i], out int current) || visited[current])
                return false;
            int pickup = problem.PickupForDelivery[current];
            if (pickup >= 0 && !visited[pickup])
                return false;
            if (i > 0) {
                long edge = problem.Distance(previous, current);
                if (edge == PDTSP.NoEdge)
                    return false;
                cost += edge;
            }
            visited[current] = true;
            previous = current;
        }
        long closing = problem.Distance(previous, problem.DepotIndex);
        return closing != PDTSP.NoEdge && cost + closing <= problem.K;
    }
}
