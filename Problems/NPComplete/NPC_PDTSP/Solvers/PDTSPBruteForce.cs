using API.Interfaces;

namespace API.Problems.NPComplete.NPC_PDTSP.Solvers;

class PDTSPBruteForce : ISolver<PDTSP>
{
    // --- Fields ---
    public string solverName { get; } = "Pickup and Delivery Traveling Salesperson Brute Force";

    public string solverDefinition { get; } =
        "Enumerates tours starting and ending at the depot, requiring "
        + "each pickup before its delivery. Returns the first valid tour "
        + "with total weight <= K.";

    public string source { get; } =
        "Problem constraints: Dumitrescu et al. (2010), "
        + "https://doi.org/10.1007/s10107-008-0234-9";

    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Quinton Smith" };
    public bool timerHasExpired { get; set; }

    public SolverType solverType { get; } = SolverType.BruteForce;

    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Factorial;

    // Generate permutations one at a time, like TSPBruteForce.
    private static IEnumerable<List<int>> EnumerateCombinations(int count)
    {
        List<int> combination = Enumerable.Range(1, count).ToList();

        do
        {
            yield return new List<int>(combination);
        } while (GetNextCombination(combination));
    }

    private static bool GetNextCombination(List<int> combination)
    {
        int i = combination.Count - 2;

        while (i >= 0 && combination[i] >= combination[i + 1])
        {
            i--;
        }

        if (i < 0)
        {
            return false;
        }

        int j = combination.Count - 1;

        while (combination[j] <= combination[i])
        {
            j--;
        }

        int temp = combination[i];
        combination[i] = combination[j];
        combination[j] = temp;

        combination.Reverse(i + 1, combination.Count - i - 1);

        return true;
    }

    public string solve(PDTSP problem)
    {
        // Keep the depot fixed; permute only pickup/delivery vertices.
        List<string> stops = problem.nodes.Where(node => node != problem.depot).ToList();

        foreach (List<int> combination in EnumerateCombinations(stops.Count))
        {
            if (timerHasExpired)
            {
                return SolveRun.TimedOutAnswer;
            }

            List<string> order = combination.Select(index => stops[index - 1]).ToList();

            // A pickup must appear before its corresponding delivery.
            bool validOrder = true;

            foreach (var (pickup, delivery) in problem.requests)
            {
                int pickupIndex = order.IndexOf(pickup);
                int deliveryIndex = order.IndexOf(delivery);

                if (pickupIndex < 0 || deliveryIndex < 0 || pickupIndex >= deliveryIndex)
                {
                    validOrder = false;
                    break;
                }
            }

            if (!validOrder)
            {
                continue;
            }

            List<string> tour = new() { problem.depot };
            tour.AddRange(order);
            tour.Add(problem.depot);

            string certificate = "{" + string.Join(",", tour) + "}";

            // The PDTSP verifier checks the complete tour, edges and K.
            if (problem.defaultVerifier.verify(problem, certificate))
            {
                return certificate;
            }
        }

        return "{}";
    }
}
