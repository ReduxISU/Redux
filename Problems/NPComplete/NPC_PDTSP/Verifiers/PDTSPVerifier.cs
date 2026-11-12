using API.Interfaces;

namespace API.Problems.NPComplete.NPC_PDTSP.Verifiers;

class PDTSPVerifier : IVerifier<PDTSP>
{
    public const string CertificateGrammar =
        "{Depot,...,Depot} | visit every non-depot vertex exactly once, "
        + "each pickup before its delivery, total edge weight <= K";

    public const string CertificateExample = "{Depot,P1,D1,Depot}";

    public string verifierName { get; } =
        "Pickup and Delivery Traveling Salesperson Solution Verifier";

    public string verifierDefinition { get; } =
        "Checks the tour, pickup/delivery order, and maximum weight.";

    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Quinton Smith" };
    public string certificate { get; private set; } = "";

    public bool verify(PDTSP problem, string certificate)
    {
        this.certificate = certificate;

        List<string> order = certificate
            .Replace("{", "")
            .Replace("}", "")
            .Split(',', StringSplitOptions.TrimEntries)
            .ToList();

        // The tour must start and end at the depot.
        if (
            order.Count != problem.nodes.Count + 1
            || order[0] != problem.depot
            || order[^1] != problem.depot
        )
        {
            return false;
        }

        // Exclude the repeated depot when checking exactly-once visits.
        List<string> visited = order.Take(order.Count - 1).ToList();

        if (
            visited.Distinct().Count() != problem.nodes.Count
            || visited.Any(node => !problem.nodes.Contains(node))
        )
        {
            return false;
        }

        // Each pickup must occur before its corresponding delivery.
        foreach (var (pickup, delivery) in problem.requests)
        {
            int pickupIndex = visited.IndexOf(pickup);
            int deliveryIndex = visited.IndexOf(delivery);

            if (pickupIndex < 0 || deliveryIndex < 0 || pickupIndex >= deliveryIndex)
            {
                return false;
            }
        }

        long sum = 0;

        // Check each edge, including the return to the depot.
        for (int i = 0; i < order.Count - 1; i++)
        {
            string from = order[i];
            string to = order[i + 1];

            // Undirected edges can appear in either orientation.
            int edgeIndex = problem.edges.FindIndex(edge =>
                (edge.source == from && edge.target == to)
                || (edge.source == to && edge.target == from)
            );

            if (edgeIndex < 0)
            {
                return false;
            }

            sum += problem.edges[edgeIndex].weight;
        }

        return sum <= problem.K;
    }
}
